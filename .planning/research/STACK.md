# Technology Stack — Rendering Evaluation

**Project:** LMUOverlay / Douze Assistance (v2.2.9+)
**Domain:** .NET 8 Windows game overlay with dual 2D screen + OpenXR VR display
**Researched:** 2026-05-19
**Overall confidence:** HIGH (grounded in actual codebase + official docs + multiple sources)

---

## Context: What the Current Code Actually Does

The codebase (read directly) reveals the exact bottleneck pattern:

**2D path:**
- `BaseOverlayWindow` extends `Window` with `AllowsTransparency=true`, `WindowStyle=None`, `Topmost=true`
- Each overlay is an independent WPF layered window
- `UpdateData()` is called on a timer; overlays redraw via code-behind (no data-binding)

**VR path (OpenXRService.UpdateAll):**
```
WPF UI thread → new RenderTargetBitmap(w, h) → rtb.Render(content) → CopyPixels(byte[])
             → BGRA→RGBA swap (CPU loop) → lock(ovl.Lock) → write to PixelBuf
XR frame thread → UploadPixels → D3D11Native.UpdateSubresource(vtable P/Invoke)
```

This pipeline allocates a fresh `RenderTargetBitmap` + `byte[]` every frame, per overlay. `RenderTargetBitmap.Render` is software-only (confirmed by dotnet/wpf#9021 — hardware RTB is an open unimplemented request as of 2024). With 10+ overlays at 60Hz this is significant.

---

## Recommendation

**SkiaSharp standalone (with D3D11 backend via existing P/Invoke device) for VR; keep WPF for 2D screen overlays with targeted optimizations.**

This is a **hybrid approach**, not a full rewrite. Here is the rationale:

The app already owns a raw `ID3D11Device*` and `ID3D11DeviceContext*` (created for OpenXR in `D3D11Native`). SkiaSharp 3.119.x supports a D3D backend via `GRVorticeD3DBackendContext` (confirmed on Microsoft Learn). This means each overlay can render directly to a D3D11 texture that is shared with the OpenXR swapchain — eliminating the `RenderTargetBitmap → CopyPixels → UpdateSubresource` chain entirely.

For the 2D screen overlays, the WPF `AllowsTransparency=true` layered-window model is battle-tested and already works. The code is already code-behind with no bindings, which removes WPF's main overhead source. Targeted fixes (WriteableBitmap for high-frequency panels, cache-aware drawing) cost less than a full migration.

---

## Option Analysis

### Option 1: WPF Optimized (WriteableBitmap / SkiaSharp in WPF)

**Performance (2D):** Medium. WPF `AllowsTransparency=true` imposes a layered-window composition hit (~30% CPU on Vista-era; modern Windows is better but still real). `WriteableBitmap` is the best WPF path for 60Hz custom-drawn content — it avoids layout recalculation and directly maps a pixel buffer. GitHub issue dotnet/wpf#8045 shows `Lock()` can spike at certain resolutions. Sustained 60Hz with large windows strains it.

**Performance (VR):** Poor. `RenderTargetBitmap` is confirmed software-only rendering. Every frame allocates a new RTB and `byte[]`. For 10 overlays at 60Hz this is ~60MB/s of allocations plus CPU copy.

**VR integration complexity:** High cost to improve. Even with WriteableBitmap, you still need to read back bytes to CPU to call UpdateSubresource. No GPU path exists within WPF.

**Rendering quality:** Medium. WPF uses a MILCORE DirectX 9 path for compositing, but software fallback for off-screen capture. ClearType is available for text on-screen, not in RTB renders.

**Migration effort:** Zero (already there).

**Verdict:** Keep for the 2D window shell and main UI panels. Do NOT use for VR capture. Appropriate for low-frequency overlays (damage, flags, weather).

### Option 2: SkiaSharp Standalone (recommended for VR path)

**Current stable version:** 3.119.2 (NuGet: `SkiaSharp`, `SkiaSharp.Views.WindowsForms`)
**Preview version:** 4.147.0-preview.1.1 (SkiaSharp 4.0 stable planned mid-2026 per .NET Blog)

**Performance (rendering):** High. Skia is the rendering engine behind Chrome, Android, Flutter. GPU-backed via `GRContext` + `SKSurface`. All draw calls stay on GPU — text, rounded rects, paths, anti-aliased lines. No managed-heap allocations per frame if objects (SKPaint, SKPath) are pre-allocated and reused.

**Performance (VR):** This is the key finding. SkiaSharp exposes `GRVorticeD3DBackendContext` which wraps an existing D3D11 device (confirmed: `GRVorticeD3DBackendContext Class | Microsoft Learn`). Workflow:

```
Existing ID3D11Device* (already owned by OpenXRService)
  → GRVorticeD3DBackendContext
  → GRContext.CreateD3D11(context)
  → SKSurface.Create(grContext, target: ID3D11Texture2D from XR swapchain)
  → SKCanvas.DrawXxx (GPU, zero copy)
  → SKSurface.Flush()
  → xrReleaseSwapchainImage
```

This eliminates the entire `RenderTargetBitmap → CopyPixels → byte[] BGRA/RGBA swap → UpdateSubresource` chain. The overlay renders directly into the OpenXR swapchain texture. No CPU readback required.

**D3D backend stability note (MEDIUM confidence):** The Skia team discussed possibly removing the D3D11/D3D12 native backend (skia-discuss ML, 2023) but current plans route through Dawn for Graphite. The `GRVorticeD3DBackendContext` in SkiaSharp 3.119 uses Vortice.Windows under the hood. For SkiaSharp 4.0, the Dawn/WebGPU backend becomes the unified path — monitor the upgrade path.

**VR integration complexity:** Medium-low. The D3D11 device is already created in `D3D11Native`. The main work is: (1) create `GRContext` once, (2) per-overlay create `SKSurface` wrapping the swapchain texture, (3) implement `DrawXxx` calls replacing the current `UpdateData()` WPF draw logic.

**Rendering quality:** High. Skia provides sub-pixel text rendering, proper anti-aliasing, and GPU-accelerated compositing. Better than WPF's software RTB path.

**Migration effort for VR path:** Medium. `UpdateData()` in each overlay currently sets WPF element properties (TextBlock.Text, Border.Background, etc.). These must be translated to `SKCanvas` drawing calls (DrawText, DrawRoundRect, DrawLine, etc.). The overlays are code-behind with no XAML data-binding, which makes the translation more mechanical. Estimate: 2-4 days per complex overlay, 0.5 day per simple one. 15 overlays → ~3-4 weeks of pure coding.

**Migration effort for 2D path:** Full rewrite required if you replace the WPF window shell too. Not recommended — see hybrid approach.

**Package dependencies to add:**
```xml
<PackageReference Include="SkiaSharp" Version="3.119.2" />
<PackageReference Include="Vortice.Direct3D11" Version="3.3.11" />
<!-- Only if D3D context wrapping via Vortice is used -->
```

Note: `SkiaSharp.Views.WPF` (SKElement / SKGLElement) is NOT what you want here. That is for embedding Skia inside WPF UI, not for rendering to D3D11 textures.

### Option 3: D3D11 Direct Rendering (Silk.NET.Direct3D11 or SharpDX)

**Current stable version:** Silk.NET.Direct3D11 2.23.0 (actively maintained, part of dotnet foundation). SharpDX 4.2.0 (archived April 2024, read-only repo).

**Performance:** Maximum possible. Rendering at GPU speed, directly into swapchain textures. This is what OpenKneeboard does: renders with Direct2D to DXGI surfaces (ID3D11Texture2D), then copies to shared buffers accessible by VR runtimes.

**VR integration complexity:** Low in principle (the app already does raw D3D11 P/Invoke). But Direct2D text rendering is far more verbose than SkiaSharp. Drawing a rounded rectangle with text, drop shadow, and per-pixel alpha requires 10-20× more code than the equivalent SkiaSharp calls.

**Rendering quality:** Maximum — Direct2D with DirectWrite provides the best Windows text rendering.

**2D overlay approach:** Using `CreateSwapChainForComposition` + DirectComposition, or `CreateSwapChainForHwnd` into a `WS_EX_LAYERED` window with `UpdateLayeredWindow`. Both are viable. The existing WPF layered-window model already solves this problem adequately.

**SharpDX status:** DEAD. Archived April 2024. Do not add new dependencies on it. The project already uses SharpDX for DirectInput only — an acceptable legacy dependency since DirectInput is not being replaced.

**Silk.NET.Direct3D11 status:** Active. Version 2.23.0. The project already uses Silk.NET.OpenXR — adding Silk.NET.Direct3D11 is low friction.

**Migration effort:** High. Full rewrite of all 15 overlay rendering implementations from WPF element trees to Direct2D/DirectWrite command lists. No abstraction layer. Text layout alone (word wrap, per-character metrics) requires explicit `IDWriteTextLayout` objects. Estimate: 8-12 weeks.

**Verdict:** Technically superior but excessive for this use case. Use if SkiaSharp D3D backend proves unstable. The current P/Invoke D3D11 layer (without SharpDX/Silk.NET) is already functional for texture upload.

### Option 4: WinUI 3

**Current version:** Windows App SDK 1.6 / WinUI 3 (Windows App SDK 1.7 in preview)

**Performance:** Mixed. Benchmarks show WinUI 3 is *measurably slower* than WPF at current state (2025 benchmarks, GitHub microsoft-ui-xaml#11096). Uses DirectX 12 compositor vs WPF's DirectX 9 — but the DX12 path adds CPU overhead for state management that offsets the GPU gains for simple 2D overlays. Memory usage 15-20% lower than WPF per CTCO 2026 blog.

**VR integration complexity:** Very high. WinUI 3 has no API to render to a D3D11 texture. `WS_EX_TRANSPARENT` and transparent windows are supported via P/Invoke hacks but not officially stable (microsoft-ui-xaml#2956). The WinUI 3 XAML island approach does not expose an `ID3D11Device` for sharing with OpenXR.

**Rendering quality:** High — DX12-backed, modern text rendering.

**Migration effort:** Complete rewrite. WinUI 3 uses a different namespace (Microsoft.UI.Xaml, not System.Windows), different window management APIs, different threading model. The existing `BaseOverlayWindow : Window` hierarchy would need full replacement.

**Key disqualifier:** WinUI 3's transparent overlay support for game overlays is not officially documented and relies on P/Invoke workarounds that may break with Windows updates. The existing WPF `AllowsTransparency=true` model is more stable for this use case.

**Verdict:** Do not migrate to WinUI 3. The performance is not better than WPF for this use case, VR integration is harder, and migration cost is the same as D3D11 direct (full rewrite).

### Option 5: MAUI

**Verdict:** Immediately disqualified. MAUI targets mobile/cross-platform. It does not support `AllowsTransparency`, `Topmost`, `WS_EX_TRANSPARENT`, or any game overlay semantics on Windows. No OpenXR integration path.

### Option 6: Veldrid

**Status:** ABANDONED. Original developer confirmed February 2023 they can no longer maintain it publicly. A fork exists (`ppy.Veldrid`) maintained by the osu! team but it is specialized for their use case.

**Verdict:** Do not use. Abandoned upstream.

---

## Recommended Stack

### Core Framework (unchanged)

| Technology | Version | Purpose | Why |
|------------|---------|---------|-----|
| WPF .NET 8 | net8.0-windows | 2D window shell, main UI panels | Already in production, AllowsTransparency works, zero migration cost |
| Silk.NET.OpenXR | 2.22.0 → 2.23.0 | OpenXR session, swapchain, quad layer submission | Already in production, actively maintained |

### New: Rendering Layer for VR Path

| Technology | Version | Purpose | Why |
|------------|---------|---------|-----|
| SkiaSharp | **3.119.2** | GPU-backed 2D drawing into D3D11 textures | Replaces RenderTargetBitmap; renders directly to XR swapchain textures |
| Vortice.Direct3D11 | 3.3.11 | D3D11 COM type wrappers for GRVorticeD3DBackendContext | Required to pass the existing ID3D11Device* to Skia's D3D backend |

### Existing Dependencies (keep as-is)

| Technology | Version | Purpose | Keep? |
|------------|---------|---------|-------|
| SharpDX / SharpDX.DirectInput | 4.2.0 | Joystick/wheel input | YES — archived but functional, no replacement needed |
| CommunityToolkit.Mvvm | 8.2.2 | Main window settings UI | YES |
| Newtonsoft.Json | 13.0.3 | Config serialization | YES |
| AutoUpdater.NET.Official | 1.9.2 | Auto-update | YES |
| ClosedXML | 0.103.0 | Excel export | YES |
| System.Speech | 8.0.0 | Voice assistance | YES |

### Do NOT Add

| Technology | Why Not |
|------------|---------|
| SkiaSharp 4.0 (preview) | Not stable; stable release planned mid-2026. Upgrade after GA |
| Silk.NET.Direct3D11 | Not needed — Vortice.Direct3D11 is the right wrapper for SkiaSharp's D3D context. Silk.NET.Direct3D11 is raw unsafe bindings, not idiomatic for this use |
| WinUI 3 | No benefit for game overlay, harder VR integration, full rewrite required |
| GameOverlay.Net | Main repo archived April 2024. Abandoned |
| Veldrid | Abandoned |
| MAUI | Wrong platform |

---

## Architecture for the Hybrid Approach

### 2D Path (unchanged)

```
[Timer 60Hz] → UpdateData() on each BaseOverlayWindow
             → Sets WPF element properties (TextBlock.Text, etc.)
             → WPF renders via layered window composition (DWM)
             → Displayed on desktop above game
```

No change required. This path already bypasses data-binding (pure code-behind).

Optional micro-optimization: for the 3-4 highest-frequency overlays (InputGraph, DeltaTime, Chrono), replace the WPF Grid/TextBlock content with a single `SKElement` (SkiaSharp.Views.WPF). This gives GPU-accelerated drawing within the existing WPF window shell, with no window-model change. One `SKGLElement` per window (not multiple — known issue: multiple SKGLElements sharing a context is unstable per SkiaSharp#920).

### VR Path (new)

```
[D3D11 device — already exists in D3D11Native]
  → GRContext.CreateD3D11(grVorticeContext)  [created once at startup]

Per overlay, per frame:
  [XR swapchain texture — already acquired via xrAcquireSwapchainImage]
    → SKSurface.Create(grContext, backendRenderTarget: swapchainTex)
    → canvas.Clear(SKColors.Transparent)
    → DrawOverlayContent(canvas, data)  [replaces UpdateData() for VR]
    → surface.Flush()
    → xrReleaseSwapchainImage
```

This eliminates the `RenderTargetBitmap → byte[] → UpdateSubresource` triple allocation.

### Data Flow

The `DataService` DTOs (`GetFuelData()`, `GetAllVehicles()`, etc.) are renderer-agnostic. No change needed. Each overlay will have two render paths:
- `UpdateData()` — WPF element updates (2D path, unchanged)
- `DrawVR(SKCanvas canvas, OverlayData data)` — Skia draw calls (VR path, new)

The `OpenXRService.UpdateAll()` method currently calls `RenderTargetBitmap.Render()`. This becomes `DrawVR(canvas, data)`.

---

## Migration Path from Current WPF

### Phase 1: Zero-risk foundation (no visible change)
1. Add `SkiaSharp 3.119.2` and `Vortice.Direct3D11` to the csproj
2. In `OpenXRService`, create `GRContext` from the existing `_d3dDevice` at init time
3. Validate: `GRContext` is not null, no exceptions at startup

### Phase 2: VR rendering replacement (one overlay at a time)
4. Pick the simplest overlay (e.g., `ClockOverlay` or `FlagOverlay`)
5. Add `DrawVR(SKCanvas canvas)` method with Skia equivalents of its WPF drawing code
6. In `UploadPixels()` → replace with `SKSurface.Create(...)` + `DrawVR()` + `Flush()`
7. Validate rendering quality in VR headset
8. Repeat per overlay (15 total, mix of complexity)

### Phase 3: 2D optimization (optional, post-VR)
9. For high-frequency overlays: replace WPF Grid/TextBlock with `SKElement`
10. Benchmark before/after CPU usage

### What does NOT change
- `BaseOverlayWindow` architecture
- All 15 overlay `UpdateData()` methods (they keep working for 2D)
- `DataService` and all data DTOs
- `ConfigService`, `ProfileService`, JSON configs
- The `AllowsTransparency=true` window model
- All non-overlay code (chrono panels, voice, CSV export, etc.)

---

## Installation

```xml
<!-- Add to LMUOverlay.csproj -->
<PackageReference Include="SkiaSharp" Version="3.119.2" />
<PackageReference Include="Vortice.Direct3D11" Version="3.3.11" />
<!-- For optional 2D optimization in WPF overlays: -->
<PackageReference Include="SkiaSharp.Views.WPF" Version="3.119.2" />
```

```bash
# Via dotnet CLI
dotnet add package SkiaSharp --version 3.119.2
dotnet add package Vortice.Direct3D11 --version 3.3.11
```

---

## Alternatives Considered

| Category | Recommended | Alternative | Why Not |
|----------|-------------|-------------|---------|
| VR render path | SkiaSharp + D3D11 backend | Silk.NET.Direct3D11 + Direct2D | Direct2D requires 10× more code per overlay; no productivity gain |
| 2D overlay shell | WPF (keep) | WinUI 3 | Full rewrite, no overlay stability guarantees, no VR path |
| VR render path | SkiaSharp 3.119 | SkiaSharp 4.0 preview | Not stable yet; upgrade after mid-2026 GA release |
| GPU abstraction | Vortice.Direct3D11 | SharpDX | SharpDX archived April 2024; Vortice is the actively maintained successor |

---

## Confidence Assessment

| Area | Confidence | Notes |
|------|------------|-------|
| WPF RenderTargetBitmap is software-only | HIGH | Confirmed via dotnet/wpf#9021 (open request as of 2024) and dotnet/wpf source |
| SkiaSharp 3.119.2 current stable | HIGH | NuGet Gallery confirmed |
| GRVorticeD3DBackendContext exists for D3D11 | HIGH | Microsoft Learn docs confirmed |
| SkiaSharp D3D backend future stability | MEDIUM | Skia team discussed deprecation; Dawn/WebGPU is future path. 3.119 is stable now |
| WPF AllowsTransparency overhead | MEDIUM | 2008-era data; modern DWM is better but layered window cost is real |
| SKGLElement multi-instance instability | HIGH | SkiaSharp#920 confirmed via official GitHub issue |
| Veldrid abandoned | HIGH | Developer confirmed February 2023 |
| GameOverlay.Net archived | HIGH | GitHub confirms archived April 2024 |
| WinUI 3 slower than WPF benchmarks | MEDIUM | Community benchmarks (2025), not Microsoft official measurements |

---

## Sources

- dotnet/wpf#9021 — RenderTargetBitmap hardware rendering request (open 2024): https://github.com/dotnet/wpf/issues/9021
- dotnet/wpf#8045 — WriteableBitmap.Lock() performance spikes: https://github.com/dotnet/wpf/issues/8045
- GRVorticeD3DBackendContext (Microsoft Learn / SkiaSharp 3.119): https://learn.microsoft.com/en-us/dotnet/api/skiasharp.grvorticed3dbackendcontext
- SkiaSharp NuGet 3.119.2: https://www.nuget.org/packages/SkiaSharp/
- SkiaSharp 4.0 Preview 1 announcement (.NET Blog): https://devblogs.microsoft.com/dotnet/welcome-to-skia-sharp-40-preview1/
- SkiaSharp D3D backend discussion (skia-discuss): https://groups.google.com/g/skia-discuss/c/WY7yzRjGGFA
- SkiaSharp#920 — SKGLElement multi-instance crosstalk: https://github.com/mono/SkiaSharp/issues/920
- SkiaSharp#2817 — Direct3D support tracking: https://github.com/mono/SkiaSharp/issues/2817
- SkiaSharp#2911 — SkiaSharp and DirectX discussion: https://github.com/mono/SkiaSharp/discussions/2911
- OpenKneeboard internals — Direct2D to D3D11 texture pattern: https://openkneeboard.com/internals/README/
- GameOverlay.Net archive notice: https://github.com/michel-pi/GameOverlay.Net
- Silk.NET.Direct3D11 2.23.0: https://www.nuget.org/packages/Silk.NET.Direct3D11/2.23.0
- WinUI 3 performance discussion: https://github.com/microsoft/microsoft-ui-xaml/discussions/11096
- WinUI vs WPF 2026 comparison (CTCO Blog): https://www.ctco.blog/posts/winui-vs-wpf-2026-practical-comparison/
- Veldrid — developer announcement February 2023: https://github.com/veldrid/veldrid
- OpenXR D3D11 Graphics tutorial (Khronos): https://openxr-tutorial.com/windows/d3d11/3-graphics.html
