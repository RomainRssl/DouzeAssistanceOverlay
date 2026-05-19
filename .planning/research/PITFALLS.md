# Domain Pitfalls — LMUOverlay Milestone

**Domain:** WPF game overlay + racing sim fuel strategy (Le Mans Ultimate / rF2)
**Researched:** 2026-05-19
**Scope:** Rendering migration, drag/resize customization, multi-class fuel strategy, CPU/GPU optimization

---

## Critical Pitfalls

Mistakes that cause rewrites or hours of debugging.

---

### Pitfall 1: AllowsTransparency Forces Software Rendering — Then VR Capture Breaks

**What goes wrong:**
`AllowsTransparency=true` on WPF windows disables hardware acceleration for those windows entirely. All WPF rendering falls back to the CPU-bound software pipeline. The existing `OpenXRService` captures frames via `RenderTargetBitmap`, which is also software-only: it cannot render GPU-backed elements (ShaderEffects become no-ops). The result is a double software-render penalty: WPF renders in software, then `RenderTargetBitmap` copies it again in software to feed OpenXR.

**Why it happens:**
Microsoft's DWM composition pipeline requires per-pixel alpha to blend layered windows. WPF's hardware renderer (MIL) cannot produce per-pixel alpha output — only the software renderer can. The moment `AllowsTransparency=true` is set, WPF opts out of the hardware render path for that window.

**Consequences:**
- CPU load scales with the number of overlay windows (each is an independent transparent window)
- On mid-range sim-racing PCs, running 10+ overlays simultaneously at 30Hz causes measurable frame pacing impact on the game
- VR performance is worst here: `RenderTargetBitmap.Render()` is synchronous and blocks the UI thread for every VR frame
- Any future GPU-accelerated effect (blur, shadow) added in WPF will silently degrade to software

**Prevention:**
- Before migrating to SkiaSharp or D3D11, benchmark `RenderTargetBitmap` CPU time per frame with 8+ overlays active (use `Stopwatch` around the call in `OpenXRService`)
- If migrating: SkiaSharp with `SKGLControl` embedded via `WindowsFormsHost` regains GPU acceleration, but only if the overlay is NOT using `AllowsTransparency` at the window level — the two are incompatible
- For a pure D3D11 path: create a single D3D11 render target per overlay, composite into the existing swapchain — bypasses WPF transparency entirely
- Do not add WPF visual effects (DropShadow, blur) to any overlay that runs at 30Hz

**Warning signs:**
- CPU usage creeps up proportionally with overlay count, not just data complexity
- `RenderTargetBitmap` call appears in CPU profiles for VR frames
- Any `ShaderEffect` applied to an overlay has no visual result

**Phase address:** Rendering migration evaluation phase

---

### Pitfall 2: WS_EX_TRANSPARENT + DWM — Click-Through Is Not Reliable Across Windows Versions

**What goes wrong:**
The classic game overlay click-through pattern (WS_EX_LAYERED | WS_EX_TRANSPARENT via `SetWindowLong`) does not fully work with DWM's composition model on Windows 11. On some builds, transparent areas of a DWM-composited window still intercept mouse events even with WS_EX_TRANSPARENT set. This is documented as "WS_EX_TRANSPARENT does not work with DWM" — the flag works at the HWND level but DWM composites above it.

**Why it happens:**
DWM owns the final composition buffer. WS_EX_TRANSPARENT tells Windows to route mouse hits to the window below, but when DWM is actively compositing alpha layers, hit-testing becomes non-deterministic at pixel regions that are "visually transparent but DWM-owned."

**Consequences:**
- On some Windows 11 builds (23H2+), clicks on visually-empty overlay areas fail to pass through to the game
- Dragging inside the game near overlay edges intercepts mouse input, breaking game UI
- The bug is version-specific and hard to reproduce consistently across hardware

**Prevention:**
- Always apply BOTH flags: `WS_EX_LAYERED | WS_EX_TRANSPARENT` together, not just one
- After setting window style, verify with `GetWindowLong` that the flags are actually set
- For panels in "locked" mode (user has finished positioning), set both flags. For panels in "unlocked/edit" mode, remove WS_EX_TRANSPARENT so the user can click and drag
- Test click-through on Windows 11 23H2 and 24H2 separately — behavior differs
- If migrating to D3D11 overlay: consider using `DirectComposition` for pixel-level hit testing (alpha=0 passes through, alpha>0 blocks) — this is the modern correct solution

**Warning signs:**
- User reports "can't click in-game when overlay is visible"
- Bug only reproducible on specific Windows builds
- WS_EX_TRANSPARENT set but click still intercepted

**Phase address:** Drag/resize customization phase (toggle transparency flags when lock state changes)

---

### Pitfall 3: SkiaSharp + WPF Airspace Problem Blocks Transparent Overlay Use

**What goes wrong:**
Embedding `SKGLControl` (the GPU-accelerated SkiaSharp control) into a WPF window via `WindowsFormsHost` triggers the WPF "airspace problem": the HWND created by `WindowsFormsHost` sits in a completely separate rendering tree from WPF. You cannot achieve:
- Transparency/alpha blending between the SkiaSharp surface and the WPF background
- WPF visual effects applied over the SkiaSharp area
- Context menus that appear over the SkiaSharp area
- `DragMove()` initiated from within the SkiaSharp area

For a transparent overlay window, this is fatal: the SkiaSharp area renders with a solid black or opaque background regardless of the window's transparency settings.

**Why it happens:**
WPF and WinForms use different HWND layering. `WindowsFormsHost` creates a child HWND for the WinForms control. The DWM compositor cannot alpha-blend between two HWNDs owned by different rendering systems — the z-order is honored but not the alpha composition.

**Consequences:**
- Transparent overlay windows cannot use `SKGLControl` inside `WindowsFormsHost`
- The alternative (`SKElement` + software SkiaSharp) has no GPU benefit, just adds a dependency
- A complete VR pipeline change is needed if SkiaSharp is the target

**Prevention:**
- If choosing SkiaSharp as the renderer, use a pure native Win32 window with SkiaSharp (see: `OverlaySharp` pattern on GitHub — `WS_EX_LAYERED | WS_EX_TRANSPARENT`, SkiaSharp renders to bitmap, `UpdateLayeredWindow` composites it)
- Alternatively: render SkiaSharp off-screen to a `WriteableBitmap`, then display via WPF `Image` control — avoids the airspace problem, but reintroduces CPU copy on every frame
- The existing WPF code-behind pattern (no XAML binding) is already close to optimal for the current architecture; migration should be evaluated only if benchmarks prove WPF is the bottleneck

**Warning signs:**
- Black rectangles appearing where SkiaSharp control sits
- Transparency settings on the WPF window have no effect in the SkiaSharp area
- `DragMove()` throws or does nothing when called from a SkiaSharp event handler

**Phase address:** Rendering migration evaluation phase

---

### Pitfall 4: Multi-Class Fuel Calculation Uses Player's Own Lap Count, Not the Global Leader's

**What goes wrong:**
The current `GetFuelData()` calculates `raceLapsLeft` as `info.mMaxLaps - currentLap` (for lap races) or `sessionLeft / T_tour` (for time races), where `currentLap = scr.mTotalLaps` is the player's own lap counter. In a multi-class race (Hypercar/LMP2/GT3), the Hypercar class leader is on a faster lap time and will complete more laps than an LMP2 or GT3 driver. The race ends when the overall leader finishes — not when the player finishes.

**Concrete example:**
Race: 60 laps total. Player is in GT3, on lap 40. Hypercar leader is on lap 45. The race ends in 15 more Hypercar laps, which are ~28 GT3 laps at GT3 pace. If the player calculates fuel for 20 more GT3 laps (60 - 40), they over-fuel by ~8 laps. Conversely, if the Hypercar leader is on lap 58 and the GT3 player is on lap 52, the player needs only 6 GT3 laps of fuel, not 8.

**Why it happens:**
The rF2 shared memory field `info.mMaxLaps` is the total lap count for the session. For lap races, it is the same for all classes. But "laps remaining for the player" must be computed as `leaderLaps + correctingFactor - playerLaps`, not `maxLaps - playerLaps`.

For time-based races (Le Mans 24h format), `mEndET` is global, but each class runs at different pace, so `sessionLeft / playerLapTime` overestimates or underestimates compared to what the race actually requires of the player.

**Consequences:**
- In GT3 class: fuel calculation systematically over-estimates laps remaining (player carries too much fuel, penalizing lap time)
- In Hypercar class during a safety car period: laps remaining are under-estimated (player risks fuel shortage)
- The VE calculation inherits the same error: `energyToEnd = raceLapsLeft * C_ve` uses the wrong `raceLapsLeft`

**Prevention:**
To fix, compute `raceLapsLeft` from the overall leader's lap count:
```csharp
// Find the overall P1 vehicle in scoring
int leaderLaps = 0;
for (int i = 0; i < numVeh; i++)
{
    var v = scoringVehicles[i];
    if (v.mPlace == 1) { leaderLaps = v.mTotalLaps; break; }
}
// For lap race: laps remaining for player = (leaderLaps + estimatedLeaderLapsToFinish) - playerLaps
// For time race: laps remaining for player = ceil(sessionLeft / playerOwnLapTime)
//    because time-based races don't have an mMaxLaps — they end by time regardless of class
```
For time races, the player's own `sessionLeft / T_tour` is actually correct — the race ends by the clock, not by laps. The error only applies to **lap-count races**.

Also add a safety margin: `+1` lap for formation/out-lap after pit.

**Warning signs:**
- In multi-class races, "CARBURANT À AJOUTER" is visibly wrong compared to what experienced engineers calculate manually
- GT3 class consistently shows higher fuel-to-add than expected
- After a pit stop, fuel-to-add drops more than one lap's worth

**Phase address:** Fuel strategy refactoring phase — this is the core bug to fix

---

### Pitfall 5: DispatcherTimer Cannot Reliably Fire at 30Hz+ Under WPF Load

**What goes wrong:**
`OverlayManager` uses a `DispatcherTimer` at `1000 / UpdateRateHz` ms. `DispatcherTimer` does not create a separate thread — it posts ticks to the WPF UI dispatcher queue. Under UI load (many overlays updating simultaneously), the dispatcher queue backs up and timer ticks are delayed or dropped. At 30Hz, a single tick that takes >33ms to process causes the next tick to be late, creating a snowball effect.

**Why it happens:**
The update loop in `OnUpdateTick` calls `overlay.UpdateData()` synchronously for every enabled overlay on the UI thread. With 20+ overlays, even cheap updates accumulate. If one overlay triggers a layout pass (e.g., `SizeToContent` recalculation from a `Visibility.Collapsed → Visible` toggle), WPF queues a full layout/render cycle that can take 10-20ms.

**Consequences:**
- Timer fires at 20Hz effective instead of 30Hz under moderate overlay load
- VR frame submission happens late relative to the XR frame loop, causing judder
- `VRService?.UpdateAll()` is called on the UI thread — any VR frame submission delay blocks all overlay updates

**Prevention:**
- Profile `OnUpdateTick` total duration before optimizing. Use `Stopwatch` around the full tick body
- Move VR frame submission to the existing `_frameThread` in `OpenXRService` and stop calling `_vrService?.UpdateAll()` from the UI thread
- Add per-tick budget logging: if tick takes >25ms, log which overlay was last updated — identifies slow overlays
- The `_slowOverlays` throttling (1 tick in 3) is good; extend this pattern to any overlay with complex layout
- Avoid `Visibility.Collapsed ↔ Visible` toggling inside `UpdateData()` — it triggers layout recalculation. Use `Opacity = 0` + `IsHitTestVisible = false` instead, or use a fixed-size placeholder

**Warning signs:**
- Profiler shows `OnUpdateTick` taking >25ms average
- VR overlay appears stuttery independent of game frame rate
- CPU usage on the UI thread spikes during data-heavy sessions (many cars on track)

**Phase address:** CPU/GPU optimization phase

---

## Moderate Pitfalls

---

### Pitfall 6: VRR / G-Sync Disabled by Topmost Overlay Window on NVIDIA

**What goes wrong:**
NVIDIA's G-Sync activation algorithm only triggers when the game's window is the exclusive frontmost HWND or when using Exclusive Fullscreen mode. An HWND_TOPMOST overlay window (the current architecture) causes NVIDIA to detect a compositor chain, which disables G-Sync for the game window. This is a known issue — Discord's overlay causes the same problem. AMD FreeSync and Intel VRR are not affected (they work in windowed/borderless mode).

**Why it happens:**
NVIDIA's G-Sync implementation in windowed mode requires no HWND above the game window. A Topmost WPF window satisfies this condition for HWND_TOPMOST, which NVIDIA interprets as "compositor active → disable G-Sync."

**Consequences:**
- Users with NVIDIA GPUs and G-Sync monitors lose variable refresh rate when the overlay is visible
- Sim racing at fixed refresh creates micro-stutter that feels worse during high-speed sections
- Users may not connect the overlay to the issue and blame the game

**Prevention:**
- This is a known Windows/NVIDIA limitation with no perfect fix from the overlay side
- Document in release notes: "NVIDIA G-Sync users may need to enable 'G-Sync for windowed and full screen' in NVIDIA Control Panel"
- Consider adding a "disable Topmost" mode that uses `HWND_TOP` (non-topmost) — works only if LMU runs borderless windowed
- LMU runs in borderless windowed by default; G-Sync in borderless mode requires "Enable G-Sync, G-Sync Compatible for windowed and full screen" in NVIDIA settings

**Warning signs:**
- User reports "overlay works but feels like the game is stuttering more"
- NVIDIA FrameView shows fixed refresh rate instead of variable when overlay is open

**Phase address:** CPU/GPU optimization phase — document as known limitation, add user guidance

---

### Pitfall 7: DPI Scaling — Window.Left / Window.Top Are in Physical Pixels on PerMonitorV2

**What goes wrong:**
With `PerMonitorV2` DPI awareness (required for sharp rendering on high-DPI monitors), `Window.Left` and `Window.Top` in WPF report physical pixel coordinates when moving between monitors with different DPI. The current drag-to-reposition code saves `Settings.PosX = Left` and `Settings.PosY = Top`, which stores physical pixel values. On a mixed DPI setup (e.g., 4K 150% + 1080p 100%), a saved position from the 4K monitor will render at the wrong location when loaded on the 1080p monitor.

**Why it happens:**
There is an open bug in dotnet/wpf (issue #4127): `Window.Left` and `Window.Top` do not return DPI-scaled logical coordinates as documented — they return physical pixels under PerMonitorV2. This is marked "by design" in some discussions but contradicts the WPF coordinate model.

**Consequences:**
- Overlay positions saved on one monitor are wrong when the user changes monitor configuration
- After a Windows display settings change (resolution or scale change), all overlays jump to wrong positions
- On a single-monitor setup at 100% DPI: no issue at all

**Prevention:**
- When saving position, convert physical-to-logical using the monitor's DPI: `logicalPos = physicalPos / (dpiX / 96.0)`
- Use `PresentationSource.FromVisual(this)?.CompositionTarget.TransformFromDevice` to get the correct logical position
- Add a "Reset positions" button to the config UI that restores all overlays to center-screen at current DPI
- Test specifically on a 150% DPI monitor and a mixed dual-monitor setup before shipping drag-to-reposition

**Warning signs:**
- Overlays appear off-screen after display configuration change
- Position values in JSON config are suspiciously large (e.g., 2880 for a 1920px monitor would indicate physical pixel storage on 150% DPI)

**Phase address:** Drag/resize customization phase

---

### Pitfall 8: rF2 mLastLapTime Is Zero or Stale at Session Start and After Pit Stops

**What goes wrong:**
The fuel calculation uses `T_tour = scr.mLastLapTime` as the basis for time-based `raceLapsLeft` estimation. `mLastLapTime` is 0.0 at session start (no lap completed yet), -1.0 or stale after a drive-through penalty, and does not update during an out-lap after a pit stop. The code guards with `T_tour > 10` but falls back to `raceLapsLeft = 0`, which makes fuel-to-add show "--" for the first lap and for one full lap after every pit stop.

**Why it happens:**
rF2 shared memory updates `mLastLapTime` only when the player crosses the start/finish line after completing a full timed lap. Formation laps and out-laps do not update it.

**Consequences:**
- Fuel panel shows "--" for CARBURANT À AJOUTER during the first lap (usable) and for 1-2 minutes after every pit stop (annoying when you need to know fuel for the next stint)
- VE calculation is also blocked during this window

**Prevention:**
- Maintain a session-persistent `_lastValidLapTime` that is only overwritten when `mLastLapTime > 10`. Use this as the fallback when `scr.mLastLapTime <= 10`
- After a pit stop, the out-lap time estimate can use `_lastValidLapTime * 1.05` (out-laps are ~5% slower)
- Also consider using `mEstimatedLapTime` from the rF2 extended shared memory as a real-time estimate during the lap

**Warning signs:**
- Fuel panel shows "--" for more than 30 seconds after a pit stop when data should be available
- `ValidFuelSamples < 2` guard blocks display even though fuel consumption history exists from before the pit

**Phase address:** Fuel strategy refactoring phase

---

### Pitfall 9: Safety Car Laps Corrupt Per-Lap Fuel Average

**What goes wrong:**
`_fuelSamples` is a rolling average of fuel consumed per lap. Safety car laps consume ~30-50% less fuel than race laps (lower speed, no aggressive braking/acceleration). If a full-course yellow lasts 3 laps, those 3 low-consumption samples pull the rolling average down by 15-25%, which then under-predicts fuel requirement for the subsequent green-flag laps.

**Why it happens:**
The current code samples fuel delta per lap with no filtering for safety car or slow lap conditions. There is no mechanism to flag or exclude anomalous lap samples.

**Consequences:**
- After a safety car period, `FuelToAdd` is under-estimated by 10-20%
- The driver pits with this under-estimate and runs short of fuel in the final laps
- The error self-corrects over several green-flag laps, but the damage (wrong pit decision) is already done

**Prevention:**
- Tag each lap sample with the `mGamePhase` value at the time of capture. rF2 phase 4 = Full Course Yellow, phase 8 = Formation lap
- Exclude samples where `mGamePhase != 5` (normal race) or where the lap time is >115% of the rolling median lap time
- Alternatively: weight samples by inverse time-deviation from the median, giving low weight to slow laps
- Display the number of valid samples (`ValidFuelSamples`) in the UI so users can see when the average is based on limited data

**Warning signs:**
- Fuel estimate drops significantly after a safety car period then rises again over the next 3-4 laps
- Users report "fuel was wrong after yellow flag restart"

**Phase address:** Fuel strategy refactoring phase

---

### Pitfall 10: HDR Display — WPF Renders in SDR Color Space, Overlay Appears Washed Out

**What goes wrong:**
When the user enables HDR on their Windows 11 display, the desktop compositor switches to a wide color gamut pipeline. WPF renders in sRGB (SDR). The colors defined in `ThemeManager` (e.g., bright green `StateGood`, red `StateDanger`) are tone-mapped by Windows HDR into the HDR color space, making them appear dimmer and less saturated than intended. The overlay becomes hard to read in a bright HDR game environment.

**Why it happens:**
WPF does not have native HDR output support. It always renders in sRGB and relies on DWM to composite onto the HDR surface. DWM applies SDR → HDR tonemapping that compresses the sRGB range.

**Consequences:**
- Overlays look "washed out" or "grey" on HDR displays, especially bright status colors
- Users increase overlay opacity to compensate, making overlays more distracting
- Text readability decreases against bright in-game backgrounds

**Prevention:**
- Multiply critical UI colors by a configurable "HDR brightness boost" factor when HDR is detected
- Detect HDR via `Screen.PrimaryScreen.BitsPerPixel > 24` or `HdrEnabled` registry key
- In the near term: increase baseline color saturation in themes to compensate for HDR tonemapping
- This is a known WPF limitation with no clean solution; document it

**Warning signs:**
- User screenshots show muted overlay colors even though the theme looks correct in non-HDR preview
- ThemeManager colors look correct in the settings panel (SDR context) but wrong on-screen in game (HDR context)

**Phase address:** CPU/GPU optimization phase — document limitation, add configurable brightness boost

---

## Minor Pitfalls

---

### Pitfall 11: Resize Drag with PointToScreen Coordinates Breaks on Non-Primary Monitor

**What goes wrong:**
`StartResize` calls `PointToScreen(e.GetPosition(this))` to get screen-space coordinates for delta calculation during resize. On multi-monitor setups where the secondary monitor has a different DPI than the primary, `PointToScreen` returns values in physical pixels but the stored start position may be in logical pixels (or vice versa), causing the resize delta to be scaled by the DPI ratio. The window jumps to an incorrect size on drag start.

**Prevention:**
- Use `VisualTreeHelper.GetDpi(this)` to get the current monitor DPI and apply the same coordinate space to both `_resizeScreenStart` and the current mouse position
- Test resize specifically on a monitor that is not the primary and has different DPI

**Phase address:** Drag/resize customization phase

---

### Pitfall 12: Theme Change Causes Full Overlay Reconstruction — All Windows Flicker

**What goes wrong:**
`OnThemeChangedRestart()` calls `ReinitializeOverlays()` which closes all `BaseOverlayWindow` instances and recreates them. This causes all overlays to briefly disappear (white flash or transparency flicker on-screen while racing). The user sees the game view for ~500ms during theme change.

**Prevention:**
- Instead of destroying and recreating windows, propagate theme changes via `OnThemeChanged()` override in each overlay — update `BrushCache` references in-place without rebuilding the visual tree
- The `BrushCache` pattern already supports this; the destruction/recreation in `ReinitializeOverlays` should be the last resort (e.g., for layout changes), not the default for color changes

**Phase address:** Fuel/UI refactoring phase — fix as part of extended theming system

---

### Pitfall 13: OpenXR EnsureSwapchainForOverlay Called Every Tick Before Layout Is Complete

**What goes wrong:**
`EnsureSwapchainForOverlay` is called in `OnUpdateTick` for every overlay, every tick. If an overlay's `ActualWidth` or `ActualHeight` is 0 (layout not yet measured), the swapchain is created with 0 dimensions and OpenXR returns an error. The code presumably guards against this, but the call overhead is non-trivial when called 30 times/second for 20+ overlays.

**Prevention:**
- Guard the call: only call `EnsureSwapchainForOverlay` if the overlay has been laid out (`ActualWidth > 0 && ActualHeight > 0`)
- Cache a "swapchain ready" flag per overlay; skip the call once the swapchain is confirmed valid
- Move the "ensure swapchain" logic to the overlay's `Loaded` event rather than polling every tick

**Phase address:** CPU/GPU optimization phase

---

## Phase-Specific Warnings

| Phase Topic | Likely Pitfall | Mitigation |
|---|---|---|
| Rendering migration (WPF → SkiaSharp/D3D11) | Pitfall 1 (software rendering), Pitfall 3 (airspace) | Benchmark first; SkiaSharp via UpdateLayeredWindow or pure D3D11 are the only viable GPU paths for transparent overlays |
| Drag-to-reposition implementation | Pitfall 2 (click-through flags), Pitfall 7 (DPI coordinates), Pitfall 11 (PointToScreen) | Toggle WS_EX_TRANSPARENT on lock state; use DPI-corrected coordinate transforms |
| Multi-class fuel strategy refactoring | Pitfall 4 (leader laps), Pitfall 8 (mLastLapTime), Pitfall 9 (safety car samples) | Three distinct bugs to fix; tackle as a coordinated unit, not piecemeal |
| CPU/GPU optimization | Pitfall 5 (DispatcherTimer overload), Pitfall 6 (G-Sync), Pitfall 10 (HDR), Pitfall 13 (swapchain polling) | Profile before optimizing; move VR frame submission off UI thread first |
| Extended theming system | Pitfall 12 (flicker on theme change) | Replace destroy/recreate with in-place color updates via BrushCache |

---

## Sources

- [WPF AllowsTransparency and software rendering — Microsoft](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/advanced/optimizing-performance-taking-advantage-of-hardware)
- [WPF Graphics Rendering Tiers — Microsoft](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/advanced/graphics-rendering-tiers)
- [RenderTargetBitmap hardware rendering request — dotnet/wpf #9021](https://github.com/dotnet/wpf/issues/9021)
- [SkiaSharp WPF airspace issue — mono/SkiaSharp #745](https://github.com/mono/SkiaSharp/issues/745)
- [SKGLElement performance — mono/SkiaSharp #1874](https://github.com/mono/SkiaSharp/issues/1874)
- [WPF transparent overlay + SkiaSharp without WindowsFormsHost — freezy/wpf-skia-opengl](https://github.com/freezy/wpf-skia-opengl)
- [OverlaySharp — SkiaSharp transparent overlay pattern](https://github.com/Joey0x646576/OverlaySharp)
- [WS_EX_TRANSPARENT and DWM — Direct2D overlay article](https://medium.com/@python-javascript-php-html-css/using-c-and-direct2d-to-create-a-circular-transparent-window-with-mouse-click-passthrough-035aaa7e3209)
- [Fix VRR for Overlays — NVIDIA Developer Forums](https://forums.developer.nvidia.com/t/fix-vrr-for-overlays-always-on-top-windows/296168)
- [Discord overlay breaks G-Sync — Erik McClure's blog](https://erikmcclure.com/blog/discord-overlay-breaks-gsync/)
- [WPF Window.Left / Window.Top broken with PerMonitorV2 — dotnet/wpf #4127](https://github.com/dotnet/wpf/issues/4127)
- [CenterScreen bug with PerMonitorV2 — dotnet/wpf #6103](https://github.com/dotnet/wpf/issues/6103)
- [DispatcherTimer vs Thread polling in WPF](https://boyan.io/timer-vs-dispatchertimer-in-wpf/)
- [rF2 Shared Memory Map Plugin — TheIronWolfModding](https://github.com/TheIronWolfModding/rF2SharedMemoryMapPlugin)
- [rF2 SimHub low fuel LED bug — SHWotever/SimHub #455](https://github.com/SHWotever/SimHub/issues/455)
- [Sim Racing fuel strategy safety margins — SimXPro](https://simxpro.com/blogs/guides/fuel-strategy-basics-for-sim-racing-a-method-you-can-trust)
- [TinyPedal — open-source rF2/LMU overlay reference](https://github.com/TinyPedal/TinyPedal)
- [WPF fullscreen performance degradation — dotnet/wpf #3626](https://github.com/dotnet/wpf/issues/3626)
