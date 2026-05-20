---
phase: 03-web-browser-overlay
plan: 02
subsystem: ui
tags: [webview2, wpf, overlay, csharp, webbrowser, url-validation]

# Dependency graph
requires:
  - phase: 03-web-browser-overlay
    plan: 01
    provides: xUnit RED stubs for WebBrowserUrlValidator, AppConfig.WebBrowser, navigation failure handler
  - phase: 02-ui-customization
    provides: BaseOverlayWindow pattern with UseRawResize, TwitchChatOverlay reference implementation
provides:
  - WebBrowserUrlValidator static helper (pure, zero WPF dependency, unit-testable)
  - WebBrowserOverlay class with WebView2 (AllowsTransparency=false fix, async init, LoadUrl)
  - AppConfig.WebBrowser property (Newtonsoft.Json round-trip compatible)
  - OverlayManager registration + persistent overlay membership for WebBrowser
affects: [03-03-PLAN.md — MainWindow UI wiring for URL input and Charger button]

# Tech tracking
tech-stack:
  added: [Microsoft.Web.WebView2 v1.0.2849.39]
  patterns:
    - AllowsTransparency=false override in constructor before Show() — mandatory for HwndHost/WebView2 compatibility
    - HWND airspace constraint — WPF elements must not overlap WebView2 area; title bandeau placed above WebView2
    - Async Loaded handler pattern for WebView2 EnsureCoreWebView2Async initialization
    - Silent disable (Settings.IsEnabled=false) on invalid URL or navigation failure — no error UI shown

key-files:
  created:
    - LMUOverlay/LMUOverlay/LMUOverlay/LMUOverlay/Helpers/WebBrowserUrlValidator.cs
    - LMUOverlay/LMUOverlay/LMUOverlay/LMUOverlay/Views/Overlays/WebBrowserOverlay.cs
  modified:
    - LMUOverlay/LMUOverlay/LMUOverlay/LMUOverlay/LMUOverlay.csproj
    - LMUOverlay/LMUOverlay/LMUOverlay/LMUOverlay/Models/OverlayConfig.cs
    - LMUOverlay/LMUOverlay/LMUOverlay/LMUOverlay/Services/OverlayManager.cs

key-decisions:
  - "AllowsTransparency=false must be set in constructor BEFORE any Show() call — WPF throws InvalidOperationException if changed after window is shown"
  - "WebBrowserUrlValidator placed in Helpers namespace with zero WPF using statements — test project (UseWPF=false) can reference it directly"
  - "WebBrowser added to _persistentOverlays so it shows without LMU connection (same as Clock and TwitchChat)"
  - "AppConfig.WebBrowser default is IsEnabled=false — user must explicitly enable in Plan 03-03 UI"

patterns-established:
  - "HwndHost overlay pattern: AllowsTransparency=false + WindowStyle=None for WebView2 in WPF overlay"
  - "Silent URL validation: LoadUrl sets IsEnabled=false on invalid URL without showing error UI"

requirements-completed: [WEB-01, WEB-02, WEB-03, WEB-04]

# Metrics
duration: 3min
completed: 2026-05-20
---

# Phase 03 Plan 02: WebBrowserOverlay Production Code Summary

**WebView2-based overlay with AllowsTransparency=false airspace fix, URL validation helper, AppConfig property, and OverlayManager registration — all 10 TDD tests GREEN**

## Performance

- **Duration:** 3 min
- **Started:** 2026-05-20T12:36:51Z
- **Completed:** 2026-05-20T12:39:21Z
- **Tasks:** 2
- **Files modified:** 5

## Accomplishments
- Created `WebBrowserUrlValidator.cs` — pure static helper (no WPF dependency) with `IsValidWebUrl` accepting only http/https URLs
- Created `WebBrowserOverlay.cs` with `AllowsTransparency=false` override (critical WebView2/HwndHost fix), async `EnsureCoreWebView2Async` in Loaded handler, `LoadUrl` with validation, and silent disable on navigation failure
- Added `AppConfig.WebBrowser` property with Newtonsoft.Json backward compatibility (old JSON without key deserializes to non-null default)
- Registered `"WebBrowser"` in both `_persistentOverlays` HashSet and `Initialize()` Reg() call in OverlayManager
- All 10 WebBrowser tests pass (6 URL validation, 2 navigation failure, 2 AppConfig round-trip); full suite 38/38 GREEN

## Task Commits

Each task was committed atomically:

1. **Task 1: Add WebView2 NuGet and create WebBrowserUrlValidator helper** - `3605324` (feat)
2. **Task 2: Create WebBrowserOverlay.cs + wire AppConfig + OverlayManager** - `6658e0e` (feat)

## Files Created/Modified
- `LMUOverlay/.../Helpers/WebBrowserUrlValidator.cs` - Static `IsValidWebUrl` accepting only http/https, rejecting ftp/javascript/empty
- `LMUOverlay/.../Views/Overlays/WebBrowserOverlay.cs` - WebView2 overlay with AllowsTransparency=false fix, async init, LoadUrl, OnNavigationCompleted
- `LMUOverlay/.../LMUOverlay.csproj` - Added Microsoft.Web.WebView2 v1.0.2849.39 PackageReference
- `LMUOverlay/.../Models/OverlayConfig.cs` - Added `WebBrowser` property (Navigateur Web, disabled by default)
- `LMUOverlay/.../Services/OverlayManager.cs` - Added "WebBrowser" to _persistentOverlays + Reg() in Initialize()

## Decisions Made
- `AllowsTransparency=false` must be set in the constructor before any `Show()` call — `BaseOverlayWindow` sets it to `true`, so the override in `WebBrowserOverlay`'s constructor corrects it before the window is ever shown
- `WebBrowserUrlValidator` has zero WPF `using` statements — kept in `LMUOverlay.Helpers` namespace so the test project (compiled without WPF) can reference it transitively via ProjectReference
- `WebBrowser` added to `_persistentOverlays` so it shows independently of LMU connection — mirrors TwitchChat behavior

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered
None - build and tests passed on first attempt.

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- `WebBrowserOverlay` compiled and registered; Plan 03-03 can now add MainWindow UI (URL text box + "Charger" button) to wire to `LoadUrl()`
- WebView2 runtime must be installed on the user's machine (typically bundled with Edge/Windows 11)
- No MainWindow entry point yet — overlay exists but has no way to set a URL from the UI

---
*Phase: 03-web-browser-overlay*
*Completed: 2026-05-20*
