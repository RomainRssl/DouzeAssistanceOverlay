# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

**Douze Assistance** is a real-time overlay HUD for Le Mans Ultimate (LMU) racing simulator. It renders 20 configurable WPF windows on top of the game, displaying telemetry, standings, strategy data, and more. Data is sourced from the game's shared memory via the rF2SharedMemoryMapPlugin.

## Build Commands

```bash
# Development build
cd LMUOverlay
dotnet build -c Debug

# Run in development
dotnet run -c Debug

# Release build (single-folder publish)
dotnet publish -c Release -r win-x64 --no-self-contained
```

The `build.bat` script at the repo root handles the full release pipeline: version bump → icon generation → `dotnet publish` → Inno Setup installer → GitHub release via `gh` CLI.

**There are no automated tests and no linting tools configured.** Testing is manual against the live game with rF2SharedMemoryMapPlugin installed.

## Solution Structure

Two projects in `LMUOverlay.sln`:

- **`rF2SharedMemory/`** — Class library. `rF2Data.cs` contains the raw structs mirroring the game's memory layout. `SharedMemoryReader.cs` opens three memory-mapped files (telemetry, scoring, extended) and reads them with a dirty-check spinlock (`versionBegin == versionEnd`) to avoid corrupt mid-write reads.

- **`LMUOverlay/`** — Main WPF application (net8.0-windows). All UI is built programmatically in C#; there are no `.xaml` markup files (only `App.xaml` and `DarkTheme.xaml` for resources).

## Architecture & Data Flow

```
rF2 Game (shared memory MMF)
  └─► SharedMemoryReader       — spinlock dirty-check, 3 buffers
        └─► DataService        — translates raw structs to domain models,
                                 tracks lap deltas, fuel consumption
              └─► OverlayManager  — DispatcherTimer at 10–60 Hz (default 30 Hz)
                    └─► [20 Overlay Windows]  — each calls UpdateData() each tick
```

**`OverlayManager`** (`Services/OverlayManager.cs`) is the central coordinator. It owns the update timer, creates/destroys overlay windows, handles VR service selection, and pauses the loop when the game disconnects.

**`DataService`** (`Services/DataService.cs`) is the single source of truth for all overlay data. It exposes typed methods (`GetVehicleData()`, `GetTireData()`, etc.) that overlays call in their `UpdateData()` override.

## Adding a New Overlay

1. Create `Views/Overlays/MyOverlay.cs` inheriting `BaseOverlayWindow`.
2. Build all UI elements in the constructor (no XAML). Use `OverlayHelper` for standard UI primitives (`CreateTextBlock()`, `CreateBorder()`, theme colors like `OverlayHelper.AccentColor`).
3. Override `UpdateData(DataService data)` to refresh live values each tick.
4. Add an `OverlaySettings` property to `AppConfig` in `Models/OverlayConfig.cs`.
5. Register it in `OverlayManager` (instantiation + lifecycle) and wire a toggle in `MainWindow`.
6. Use `BrushCache` (`Helpers/BrushCache.cs`) for any brushes created per-frame — never `new SolidColorBrush()` inside `UpdateData()`.

See `GUIDE_OVERLAYS.md` (French) for detailed overlay development guidance.

## Key Conventions

**WPF code-first:** All overlay layouts are constructed in C# constructors. Use `Grid`, `StackPanel`, `Viewbox` (for scaling), and `Canvas` — not XAML bindings. Live data is pushed imperatively in `UpdateData()`.

**Brush caching:** `BrushCache.Get(color)` is mandatory for any brush referenced each frame. This is a performance-critical path at 30 Hz.

**Config persistence:** `ConfigService` serializes `AppConfig` to `%APPDATA%/DouzeAssistance/config.json` via Newtonsoft.Json. `OverlaySettings` (common fields: position, scale, opacity, locked) is embedded per overlay. Per-circuit profiles are saved separately via `ProfileService`.

**Naming:** Class and method names are English; UI-facing strings and comments are French. Private fields use `_camelCase`. Overlay classes are named `<Feature>Overlay`.

**VR:** `IVRService` abstracts two backends (`OpenXRService`, `OpenVRApi`/SteamVR). `OverlayManager` auto-selects based on runtime detection. VR renders overlays as 3D billboards; non-VR overlays are standard `Window`s with `AllowsTransparency=true`.

**Unsafe code:** Allowed in both projects (needed for `Marshal` operations in shared memory reading and P/Invoke in VR).
