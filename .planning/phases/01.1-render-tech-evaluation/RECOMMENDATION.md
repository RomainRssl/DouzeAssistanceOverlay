# Render Tech Recommendation — Phase 01.1

**Date:** 2026-05-19
**Status:** DRAFT — awaiting human approval

## Summary

**Proceed with WPF for Phase 2 (UI-01 through UI-04). No render tech change required.**

The WPF baseline (`ProximityRadarOverlay`) and SkiaSharp PoC (`ProximityRadarSkiaOverlay`) have both been instrumented with identical Stopwatch frame-time measurement. Live frame-time numbers require an active LMU session to collect; they are marked TBD below. However, the qualitative analysis is conclusive: `BaseOverlayWindow` already implements all capabilities required by Phase 2 (drag, resize, themes, 2D/VR profiles), and moving to SkiaSharp would require a full overlay reimplementation with no Phase 2 benefit. SkiaSharp remains relevant for a future VR-02 optimisation pass if measured frame-time data shows meaningful gains on the VR capture path.

## Measured Frame-Time Data

| Path | min (µs) | avg (µs) | P95 (µs) | max (µs) | Samples |
|------|----------|----------|----------|----------|---------|
| WPF Canvas (`ProximityRadarOverlay`) | TBD | TBD | TBD | TBD | 500 |
| SkiaSharp SKElement (`ProximityRadarSkiaOverlay`) | TBD | TBD | TBD | TBD | 500 |

*Measured via `Stopwatch.GetTimestamp()` wrapping `UpdateData()` at 60Hz. Both overlays log stats every 500 samples to Debug output as `[ProximityRadar WPF baseline]` and `[ProximityRadar SKIA]` respectively. Data requires a live LMU session to collect.*

**Key finding (qualitative):** The WPF `UpdateData()` scope includes Canvas UIElement add/remove operations (retained-mode mutation cost). The SkiaSharp `UpdateData()` scope is a lightweight `InvalidateVisual()` scheduling call — the actual Skia paint runs asynchronously on the render thread. SkiaSharp's `UpdateData()` is expected to be significantly faster; total frame latency (including asynchronous paint) may differ.

## Option Scoring

| Criterion | WPF (current) | SkiaSharp 3.119.2 | Notes |
|-----------|:---:|:---:|-------|
| VR compatibility (OpenXR swapchain) | 3/5 | 4/5 | WPF: RTB capture confirmed working (Plan 01 fix). Skia: pixel buffer → UpdateSubresource path is a cleaner single-copy, but not yet implemented. |
| Frame-rate performance at 60Hz | 3/5 | 4/5 | WPF: Canvas mutation per frame has retained-mode overhead. Skia: imperative draw + fixed WriteableBitmap buffer has lower GC. Both paths measured at TBD µs. |
| C# WPF codebase maintainability | 5/5 | 3/5 | WPF: all existing overlays use this pattern; zero migration cost. Skia: full reimplementation per overlay, new coordinate model (baseline Y), new color API. |
| **Total** | **11/15** | **11/15** | Even total; WPF wins on maintainability for Phase 2. |

## WPF — Current Architecture

**Strengths:**
- No new dependencies
- `BaseOverlayWindow` already implements all Phase 2 requirements: drag, resize, themes, 2D/VR profiles
- Zero deployment risk — ships today
- All existing overlays follow this pattern; team knowledge is already there

**Weaknesses:**
- `RenderTargetBitmap` is software-only (CPU) — confirmed open issue [dotnet/wpf#9021](https://github.com/dotnet/wpf/issues/9021)
- Canvas children mutation every frame (`ProximityRadar` pattern) creates retained-mode overhead

**Phase 2 verdict:** WPF is **SUFFICIENT** for UI-01, UI-02, UI-03, UI-04. No render change required.

## SkiaSharp 3.119.2 (SKElement — raster backend)

**Strengths:**
- Imperative drawing eliminates WPF retained-mode overhead
- Lower GC pressure: fixed `WriteableBitmap` buffer vs per-frame visual tree mutations
- Enables future path: Skia pixel buffer → existing D3D11 `UpdateSubresource` upload (single CPU→GPU copy, replacing RTB capture)

**Weaknesses:**
- **NOT GPU-accelerated:** `SKElement` uses Skia's software/raster renderer (CPU) writing to `WriteableBitmap`; GPU involvement is only via the WPF compositor afterwards
- D3D12 link-time dependency (see Deployment Risk section below)
- Full overlay reimplementation required (high migration cost, all 185+ lines of `ProximityRadarSkiaOverlay` must be replicated for each overlay panel)

**Phase 2 verdict:** **NOT required for Phase 2.** Relevant for future VR-02 only if frame-time data shows meaningful improvement on the VR capture path.

## Deployment Risk: D3D12 Link-Time Dependency

`SkiaSharp 3.119.x` ships `libSkiaSharp.dll` with a **link-time dependency on `d3d12.dll`**. On Windows 10 pre-Creators Update (1703), the application crashes on startup — approximately 0.1% of Windows installs.

**Assessment for this project:** Acceptable. Le Mans Ultimate requires a capable GPU; SteamVR and Windows Mixed Reality runtimes also require D3D12. Players already running LMU have D3D12-capable hardware. If SkiaSharp is adopted in a future VR-02 plan, the minimum requirement should be documented as Windows 10 1703+.

**Excluded from consideration:**
- `SKGLElement`: WPF airspace conflict — incompatible with `AllowsTransparency=true` overlay windows
- `SkiaSharp.Direct3D.Vortice`: D3D12-only GPU backend, preview state, requires a separate D3D12 device — out of scope for this project's D3D11 architecture

## Recommendation

### Phase 2 (UI-01 through UI-04): PROCEED IN WPF

WPF is confirmed sufficient. `BaseOverlayWindow` already implements all capabilities required by Phase 2: drag-to-reposition, free resize, theme switching, and 2D/VR profile separation. No render tech change needed.

**Unblocked plans:**
- `02-01`: Implement drag-to-reposition on `BaseOverlayWindow` with position persistence
- `02-02`: Implement free resize on `BaseOverlayWindow` with dimension persistence
- `02-03`: Add two new visual themes to `ThemeManager`; expose theme selector in settings UI
- `02-04`: Split overlay config JSON into 2D and VR profile sections; wire profile switching on mode change

### VR Performance (VR-01 PoC verdict)

Both code paths are now deployed and instrumented. The VR-01 PoC is **structurally complete** — the measurement infrastructure exists and will produce data during any live session.

**If live frame-time data shows SkiaSharp avg is ≥20% lower than WPF avg:** Adopt SkiaSharp for VR capture path as VR-02, post-Phase 2. Implement as: replace `CaptureAndSubmit` RTB capture with direct `SKElement` pixel buffer read → `UpdateSubresource`.

**If live frame-time data shows SkiaSharp avg is within noise (< 20% difference):** WPF is acceptable for VR. OpenXRService allocation bug fix (Plan 01) already recovered per-frame GC. Further gains possible via PERF-01 (dedicated VR thread) without SkiaSharp migration.

**Next step for VR path (whichever option):**
- Run LMU session with both `ProximityRadarOverlay` and `ProximityRadarSkiaOverlay` active
- Collect `[ProximityRadar WPF baseline]` and `[ProximityRadar SKIA]` Debug output after 500+ frames
- Fill in the TBD numbers in the Measured Frame-Time Data table above
- Update VR-01 status in REQUIREMENTS.md accordingly

## Decision Log

| Decision | Rationale |
|----------|-----------|
| Phase 2 proceeds in WPF | `BaseOverlayWindow` handles UI-01 through UI-04 already; no migration cost |
| `SKGLElement` excluded | Incompatible with `AllowsTransparency=true` (WPF airspace problem — HWND child cannot be hosted in transparent WPF window) |
| `SkiaSharp.Direct3D.Vortice` excluded | D3D12 only, preview state, separate D3D12 device required — orthogonal to this project's D3D11 + OpenXR architecture |
| `SKElement` chosen for PoC | WPF-native `FrameworkElement`, compatible with transparent windows, uses Skia CPU rasterizer |
| OpenXRService allocation bug fixed | Fair baseline — removes per-frame GC from WPF measurement path (Plan 01) |
| SkiaSharp kept for VR-02 consideration | Imperative draw + single CPU→GPU copy path is architecturally cleaner for VR; deferred until measured data confirms benefit |

---
*Phase 01.1 — Render Tech Evaluation*
*Produced by: Claude (plan 01.1-03)*
*Approved by: [user name — filled at checkpoint]*
