# Phase 02: UI Customization - Research

**Researched:** 2026-05-19
**Domain:** WPF overlay window management, C# code-behind pattern, JSON theme system, INotifyPropertyChanged persistence
**Confidence:** HIGH — all findings verified directly from source code; no external dependencies required

---

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions
- Global edit mode: single "Déverrouiller tout" button in MainWindow toggles IsLocked on ALL overlays simultaneously
- Edit mode shows colored border (accent primary) + resize handles visible on every overlay
- Snap-to-grid 10px default during drag
- Edit mode only disables on explicit "Verrouiller tout" click — no auto-lock on session start
- 3 new themes: Racing Clair (light), Bleu Nuit LMP2 (dark blue), Minimaliste Mono (pure black)
- ThemesTab editor (color pickers, live preview, new/duplicate/delete/import/export) — NO modifications
- Per-overlay color overrides: background, text, accent — stored in OverlaySettings via CustomOptions or new ColorOverrides field
- Per-overlay opacity + background opacity already in OverlaySettings — expose in edit-mode contextual bar
- VR positions: new VrPosX/VrPosY/VrWidth/VrHeight fields in OverlaySettings; 2D fields unchanged
- Auto-switch on AppConfig.VREnabled toggle; first VR init copies 2D layout as starting point
- Per-overlay contextual edit bar for opacity/color controls when overlay is focused in edit mode

### Claude's Discretion
- Design exact de la barre contextuelle per-overlay en mode édition (position, taille, contenu)
- Animation/transition visuelle au basculement de profil 2D/VR
- Comportement du snap quand deux overlays s'alignent (snap inter-overlays ou grille globale seulement)
- Rendu exact des bordures colorées en mode édition (épaisseur, couleur exacte, animation)

### Deferred Ideas (OUT OF SCOPE)
- Phase 1.5 évaluation technologie de rendu (already completed as Phase 01.1 — WPF confirmed)
- Z-order (ordre d'affichage entre overlays)
</user_constraints>

---

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|-----------------|
| UI-01 | L'utilisateur peut repositionner chaque panneau overlay par drag & drop pendant une session de configuration | BaseOverlayWindow already has full drag — OnMouseDown/Move/Up saves PosX/PosY to OverlaySettings. Only missing: snap-to-grid (10px) and colored border in edit mode. |
| UI-02 | L'utilisateur peut redimensionner librement chaque panneau overlay (largeur et hauteur) | BaseOverlayWindow already has full resize via _edgeRight, _edgeBottom, _grip controlled by IsLocked. No new resize logic needed — only visual feedback in edit mode. |
| UI-03 | L'utilisateur peut choisir parmi plusieurs thèmes visuels (au minimum : dark actuel + 2 nouveaux) | ThemeManager loads JSON from %AppData%/DouzeAssistance/themes/. Adding 3 new .json files is sufficient. Theme selector already exists in ThemesTab — no new UI needed. |
| UI-04 | Les positions et tailles des panneaux sont sauvegardées dans des profils séparés pour l'affichage 2D et VR | OverlaySettings needs 4 new nullable fields (VrPosX/VrPosY/VrWidth/VrHeight). VR toggle in OnToggleVR applies/saves appropriate profile. ConfigService backward compat guaranteed via Newtonsoft.Json nullable fields. |
</phase_requirements>

---

## Summary

Phase 2 has the largest surface area but the lowest implementation risk of the project — every requirement maps directly to already-working infrastructure. `BaseOverlayWindow` already implements drag, resize, lock, theme subscribe, and opacity. `OverlayManager` already has `SetAllLocked()`. `ThemeManager` already loads JSON presets from disk. `ConfigService` already handles backward-compatible nullable fields via Newtonsoft.Json.

The work is additive: snap-to-grid during drag, colored edit-mode border, 3 new theme JSON files, per-overlay color overrides in `CustomOptions`, new VR position fields in `OverlaySettings`, and a contextual bar shown during edit mode. Nothing requires architectural change — only targeted extensions to existing classes.

The biggest design decision is the contextual per-overlay edit bar (opacity/color controls). The recommended approach is a separate small WPF `Window` (not a child of the overlay) that tracks the overlay's position and appears when the overlay is clicked in unlocked mode. This avoids WPF airspace and hit-testing complexity inside the `AllowsTransparency=true` overlay window itself.

**Primary recommendation:** Proceed plan-by-plan in this order: (1) edit mode global toggle with visual feedback, (2) snap-to-grid + color overrides, (3) 3 new theme JSON files, (4) VR profile fields + auto-switch. Each plan is independently shippable and testable.

---

## Standard Stack

### Core (already in project — no new dependencies)
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| WPF (net8.0-windows) | .NET 8 | Overlay windows, UI | Existing — confirmed by Phase 01.1 |
| Newtonsoft.Json | existing | Config serialization/deserialization | Already used in ConfigService and ThemeManager |
| System.ComponentModel.INotifyPropertyChanged | BCL | Property change propagation | Already on OverlaySettings — BaseOverlayWindow subscribes |

### No New Packages Required
All Phase 2 functionality is achievable with existing dependencies. No NuGet additions needed.

---

## Architecture Patterns

### Recommended Project Structure (no changes to folder layout)
```
LMUOverlay/
├── Models/OverlayConfig.cs      — add VrPosX/VrPosY/VrWidth/VrHeight + ColorOverrides to OverlaySettings
├── Views/Overlays/
│   ├── BaseOverlayWindow.cs     — add snap-to-grid, edit border, color override apply
│   └── OverlayEditBar.cs        — NEW: small floating bar (per-overlay controls in edit mode)
├── Services/OverlayManager.cs   — SetAllLocked() already exists; add ApplyVrProfile/Apply2dProfile
├── Views/MainWindow.xaml(.cs)   — update BtnLock label + wire theme selector dropdown
└── themes/ (AppData)            — add racing-clair.json, bleu-nuit-lmp2.json, minimaliste-mono.json
```

### Pattern 1: Edit Mode Toggle (UI-01 / UI-02)
**What:** Single button in MainWindow action bar toggles `IsLocked` on all overlays simultaneously via `OverlayManager.SetAllLocked()`.
**When to use:** Already implemented (`SetAllLocked` + `UpdateLockState`). Only missing: label sync and visual border.

**Existing code — already works:**
```csharp
// OverlayManager.cs — already exists
public void SetAllLocked(bool locked)
{
    foreach (var o in _overlays.Values)
    {
        o.Settings.IsLocked = locked;
        o.UpdateLockState();
    }
}

// MainWindow.xaml.cs — already exists, OnToggleLock:
private void OnToggleLock(object s, RoutedEventArgs e)
{
    _isLocked = !_isLocked;
    _overlayManager.SetAllLocked(_isLocked);
    BtnLock.Content = _isLocked ? "🔓 UNLOCK" : "🔒 LOCK";
    SetActive(BtnLock, _isLocked);
}
```

**What to change:** The current button label logic is inverted relative to the user's mental model. User wants "DÉVERROUILLER TOUT" when overlays are locked, and "VERROUILLER TOUT" when unlocked. The `_isLocked` field starts as `false` (unlocked by default is wrong — should start locked). Fix: initialize `_isLocked = true` and update initial label.

**Edit mode border — add to BaseOverlayWindow.OnSettingsChanged:**
```csharp
// In OnSettingsChanged, case nameof(OverlaySettings.IsLocked):
// Add/remove a Border element that wraps the outer grid in edit mode
case nameof(OverlaySettings.IsLocked):
    var vis = Settings.IsLocked ? Visibility.Collapsed : Visibility.Visible;
    _edgeRight.Visibility = vis;
    // ... existing handle visibility ...
    UpdateEditBorder(!Settings.IsLocked);  // NEW: show colored border
    break;
```

### Pattern 2: Snap-to-Grid During Drag (UI-01)
**What:** Round drag position to nearest 10px grid in `OnMouseMoveHandler`.
**When to use:** Only when `!Settings.IsLocked` (edit mode active).

**Implementation:**
```csharp
// In BaseOverlayWindow.OnMouseMoveHandler — replace the direct assignment:
private const double SNAP_GRID = 10.0;

private void OnMouseMoveHandler(object sender, MouseEventArgs e)
{
    if (!_isDragging) return;
    var pos = e.GetPosition(this);
    double newLeft = Left + pos.X - _dragStart.X;
    double newTop  = Top  + pos.Y - _dragStart.Y;
    // Snap to grid
    Left = Math.Round(newLeft / SNAP_GRID) * SNAP_GRID;
    Top  = Math.Round(newTop  / SNAP_GRID) * SNAP_GRID;
}
```

**Note:** Snap applies during live drag only. `OnMouseUp` already saves the final `Left`/`Top` to `Settings.PosX`/`PosY`, so persisted value will be snapped.

### Pattern 3: Per-Overlay Color Overrides (UI-01 / UI-02 visual)
**What:** Store per-overlay color overrides in `OverlaySettings.CustomOptions` (existing `Dictionary<string, object>`).
**Why `CustomOptions` not a new class:** `CustomOptions` is already present, already serialized, and the pattern is already used for `BlindSpot.Scale`, `BlindSpot.Gap`, `TrackMap.OutlineThickness`, etc. Adding a new typed class (`ColorOverrides`) would require changes to ConfigService and add migration complexity. `CustomOptions` with keys `"ColorBg"`, `"ColorText"`, `"ColorAccent"` follows the established pattern and is backward-compatible by definition.

**Reading overrides in overlay:**
```csharp
// Example in a BaseOverlayWindow subclass or in BaseOverlayWindow itself:
protected Color? GetColorOverride(string key)
{
    if (Settings.CustomOptions.TryGetValue(key, out var v) && v is string hex)
        return ThemeManager.ParseColor(hex);
    return null;
}
// Usage: background = GetColorOverride("ColorBg") ?? ThemeManager.Current.PanelBackground
```

**Setting overrides from OverlayEditBar:**
```csharp
Settings.CustomOptions["ColorBg"]   = "#FF0000";  // hex string
Settings.CustomOptions["ColorText"] = "#FFFFFF";
OnThemeChanged();  // call virtual method to trigger re-render
```

### Pattern 4: VR Profile Fields (UI-04)
**What:** Add 4 nullable `double?` fields to `OverlaySettings`. Nullable ensures backward-compatible JSON deserialization (old config.json without these fields deserializes to `null`).

**Model change:**
```csharp
// OverlayConfig.cs — add to OverlaySettings class:
private double? _vrPosX;
private double? _vrPosY;
private double? _vrWidth;
private double? _vrHeight;

public double? VrPosX   { get => _vrPosX;  set { _vrPosX  = value; OnPropertyChanged(); } }
public double? VrPosY   { get => _vrPosY;  set { _vrPosY  = value; OnPropertyChanged(); } }
public double? VrWidth  { get => _vrWidth; set { _vrWidth  = value; OnPropertyChanged(); } }
public double? VrHeight { get => _vrHeight; set { _vrHeight = value; OnPropertyChanged(); } }
```

**Profile switch in OverlayManager or MainWindow (on VR toggle):**
```csharp
// When switching TO VR: if VrPosX == null, copy 2D values as starting point
public void ApplyVrProfile(OverlaySettings s)
{
    if (s.VrPosX == null)
    {
        s.VrPosX = s.PosX; s.VrPosY = s.PosY;
        s.VrWidth = s.OverlayWidth; s.VrHeight = s.OverlayHeight;
    }
    // Apply VR values to window position (via PropertyChanged → BaseOverlayWindow)
    // Note: VR windows are positioned by the VR service, not WPF Left/Top
    // VrPosX/VrPosY are passed to IVRService.RegisterOverlay
}

// When switching TO 2D: PosX/PosY/OverlayWidth/OverlayHeight unchanged
// When saving in VR mode: save to VrPosX/VrPosY, not PosX/PosY
```

**Key distinction:** In VR mode, `BaseOverlayWindow.Left`/`Top` are NOT what positions the overlay in the headset — `IVRService.RegisterOverlay(key, window, settings)` reads from `OverlaySettings`. The VR fields inform the VR service. The 2D fields (`PosX`/`PosY`) must not be modified when in VR mode.

**Where to intercept drag/resize saves in VR mode:**
The `OnMouseUp` event in `BaseOverlayWindow` saves to `Settings.PosX`/`PosY`. In VR mode, it should save to `Settings.VrPosX`/`VrPosY` instead. Solution: add a static/injectable `bool IsVRMode` flag (or read from OverlayManager) that BaseOverlayWindow checks when persisting position.

### Pattern 5: New Theme JSON Files (UI-03)
**What:** Create 3 JSON files in the same format as `endurance-noir.json`. Deployed to `%AppData%/DouzeAssistance/themes/` via `ThemeManager.EnsureDefaultThemeExists()` pattern — extend to write all preset themes on first run.

**File names and key color values:**

`racing-clair.json`:
- background: `#F5F5F5`, panelBackground: `#FFFFFF`, panelAlpha: 230
- accentPrimary: `#C41E28`, textPrimary: `#1A1A1A`, textSecondary: `#555555`
- border: `#DDDDDD`, effects: barGradient false, accentLine true

`bleu-nuit-lmp2.json`:
- background: `#060B14`, panelBackground: `#0A1220`, panelAlpha: 240
- accentPrimary: `#0090FF` (LMP2 blue per user decision), accentSecondary: `#0060CC`
- textPrimary: `#D0E4FF`, textSecondary: `#607090`, border: `#0D1A2E`
- effects: barGradient true, accentLine true, alertGlow true

`minimaliste-mono.json`:
- background: `#000000`, panelBackground: `#000000`, panelAlpha: 255
- accentPrimary: `#FFFFFF`, accentSecondary: `#AAAAAA`
- textPrimary: `#FFFFFF`, textSecondary: `#888888`, textMuted: `#333333`
- border: `#111111`, effects: barGradient false, accentLine false, alertGlow false

**Deployment — extend App.xaml.cs startup:**
```csharp
// In App.xaml.cs OnStartup, after EnsureDefaultThemeExists():
ThemeManager.EnsurePresetThemesExist();

// In ThemeManager.cs:
public static void EnsurePresetThemesExist()
{
    EnsureDefaultThemeExists(); // existing
    WritePresetIfAbsent("racing-clair",      BuildRacingClair());
    WritePresetIfAbsent("bleu-nuit-lmp2",    BuildBleuNuitLmp2());
    WritePresetIfAbsent("minimaliste-mono",  BuildMinimalisteMono());
}
// WritePresetIfAbsent: only writes if file doesn't exist (user edits preserved)
```

### Pattern 6: Contextual Per-Overlay Edit Bar
**What:** A small floating window (separate `Window`, not embedded) that appears near an overlay when clicked in unlocked mode. Contains: overlay name, opacity slider, background opacity slider, color override pickers (Bg, Text, Accent).
**Position:** Directly below the overlay, or adjacent if near bottom of screen.
**Lifetime:** Single instance, repositioned as user clicks different overlays. Hidden when edit mode locked.

**Why separate Window (not Panel inside overlay):**
- `AllowsTransparency=true` overlay windows cannot host opaque child controls due to WPF airspace limitations
- A separate `Window` with `WindowStyle=None`, `AllowsTransparency=false`, `Topmost=true`, `ShowInTaskbar=false` is clean, simple, and follows the pattern used by the existing `MainWindow`
- Hit testing remains clean — the edit bar doesn't interfere with the overlay's drag/resize handlers

**Implementation sketch:**
```csharp
// OverlayEditBar.cs
public class OverlayEditBar : Window
{
    private BaseOverlayWindow? _target;

    public void AttachTo(BaseOverlayWindow overlay)
    {
        _target = overlay;
        // Position below overlay
        Left = overlay.Left;
        Top  = overlay.Top + overlay.ActualHeight + 4;
        // Load current values
        LoadSettings(overlay.Settings);
        Show();
    }

    public void Hide() => Visibility = Visibility.Collapsed;
}
```

**Trigger in BaseOverlayWindow:** Override `OnMouseDown` — if `!Settings.IsLocked`, raise a static event `OverlayFocused` that `OverlayManager`/`MainWindow` handles to show/reposition the `OverlayEditBar`.

### Anti-Patterns to Avoid
- **Embedding edit controls inside overlay windows:** WPF airspace (`AllowsTransparency=true`) breaks opaque child controls. Separate Window is the only clean path.
- **New ColorOverrides typed class in OverlaySettings:** Adds migration code, breaks ConfigService backward compat pattern. Use `CustomOptions` dict — it is already established and serialized.
- **Modifying PosX/PosY in VR mode:** 2D and VR positions must stay independent. BaseOverlayWindow's `OnMouseUp` must check VR mode before deciding which field to update.
- **ThemesTab modifications:** User has explicitly locked this — do not touch ThemesTab for theme selector. The theme selector for new presets uses the existing ThemesTab list which auto-discovers JSON files.
- **Writing preset theme files on every launch:** Use `WritePresetIfAbsent` pattern (check file exists first) so user customizations are preserved.
- **Re-initializing all overlays on VR profile switch:** `OverlayManager.ReinitializeOverlays()` is called on theme change and closes/reopens all windows — do NOT use this for VR profile switch. Apply position changes via `Settings.PropertyChanged` instead.

---

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| JSON serialization backward compat | Custom migration code | Newtonsoft.Json + nullable fields | Null fields in old JSON deserialize to `null?` automatically |
| Theme JSON parsing | Custom parser | Existing `ThemeManager.ApplyJson()` | Already handles every field with Try/fallback pattern |
| WPF color picker | Custom control | `System.Windows.Forms.ColorDialog` or simple hex TextBox | Edit bar is lightweight; full color picker is overkill — hex input matches existing ThemesTab pattern |
| Property change propagation | Manual UI refresh calls | `INotifyPropertyChanged` on `OverlaySettings` | BaseOverlayWindow already subscribes to `Settings.PropertyChanged` and handles all cases in `OnSettingsChanged` |

---

## Common Pitfalls

### Pitfall 1: IsLocked initial state vs. "Déverrouiller tout" button label
**What goes wrong:** `_isLocked` in MainWindow is initialized to `false`, meaning the button shows "LOCK" on startup — but overlays load with whatever `IsLocked` was saved in config. The button state and actual overlay lock state diverge.
**Why it happens:** `_isLocked` is a local MainWindow field, not driven by config values.
**How to avoid:** On MainWindow startup, after `Initialize()`, sync `_isLocked` from the first overlay's `Settings.IsLocked` or from a majority — or simply always start with all overlays locked (`_isLocked = true`, set all `Settings.IsLocked = true` on first run before saving).
**Warning signs:** "LOCK" button shows but overlays are already unlocked from a previous session.

### Pitfall 2: Snap changes position on every mouse-move event
**What goes wrong:** Snap math `Math.Round(Left / 10) * 10` applied on every `MouseMove` event causes jittery movement when `Left` is already on a grid boundary and a small sub-pixel mouse delta keeps snapping back.
**Why it happens:** `Left += delta` then `Left = Round(Left/10)*10` oscillates when delta < 5px.
**How to avoid:** Accumulate raw position, snap only for display, save snapped value only on `MouseUp`.
```csharp
// Track raw position separately:
private double _rawDragLeft, _rawDragTop;
// In OnMouseDown: _rawDragLeft = Left; _rawDragTop = Top;
// In OnMouseMove: _rawDragLeft += dx; Left = Snap(_rawDragLeft);
// In OnMouseUp: Settings.PosX = Left; // already snapped
```

### Pitfall 3: VR profile switch during active drag/resize
**What goes wrong:** If `VREnabled` toggles while an overlay is being dragged, `OnMouseUp` saves to the wrong profile (2D when VR is now active, or vice versa).
**Why it happens:** The VR toggle and overlay drag operate on different threads of interaction.
**How to avoid:** Complete any in-progress drag before applying VR profile switch. In `OnToggleVR`, check `_isDragging`/`_isResizing` on all overlays, or simply apply profile switch at `OnMouseUp`/`StopResize` boundaries by reading current VR state.

### Pitfall 4: Color override breaks ThemeManager hot-reload
**What goes wrong:** `OnThemeChanged()` is called on all overlays when the theme changes, resetting colors. If the overlay re-reads `ThemeManager.Current.PanelBackground` in `OnThemeChanged` it loses the custom override.
**Why it happens:** `OnThemeChanged()` typically rebuilds the UI unconditionally.
**How to avoid:** In `OnThemeChanged()` (and in `BuildUI()`), always apply overrides AFTER theme colors:
```csharp
protected override void OnThemeChanged()
{
    base.OnThemeChanged();
    // Apply theme first, then overlay overrides
    if (Settings.CustomOptions.TryGetValue("ColorBg", out var bg))
        _bgBorder.Background = new SolidColorBrush(ThemeManager.ParseColor(bg.ToString()));
}
```

### Pitfall 5: New theme JSON files overwrite user edits on every launch
**What goes wrong:** User edits the "Racing Clair" theme to suit their preferences. On next launch, preset file is overwritten with defaults.
**Why it happens:** `EnsurePresetThemesExist()` writes unconditionally.
**How to avoid:** Always use `WritePresetIfAbsent(path, ...)` — write only if `!File.Exists(path)`.

### Pitfall 6: ThemeManager ReinitializeOverlays on theme change destroys VR profile
**What goes wrong:** `OverlayManager.OnThemeChangedRestart()` closes and reopens all overlays. On reopen, `BaseOverlayWindow` constructor reads `Settings.PosX/PosY` (2D position) — in VR mode this re-positions the WPF window to the 2D position, causing a flash/jump.
**Why it happens:** `ReinitializeOverlays()` creates fresh windows from current Settings. In VR mode, VR position is not in `Left`/`Top`.
**How to avoid:** After `ReinitializeOverlays()`, if VR is active, re-apply VR profile positions. Or: defer `ReinitializeOverlays` pattern and instead call `OnThemeChanged()` on all overlays without closing/reopening windows (already supported by the `ThemeChanged` event that overlays subscribe to).

---

## Code Examples

### How BaseOverlayWindow drag currently works
```csharp
// Source: BaseOverlayWindow.cs lines 399-422
private void OnMouseDown(object sender, MouseButtonEventArgs e)
{
    if (Settings.IsLocked || _isResizing) return;  // IsLocked gates drag
    _isDragging = true;
    _dragStart = e.GetPosition(this);
    CaptureMouse();
}

private void OnMouseMoveHandler(object sender, MouseEventArgs e)
{
    if (!_isDragging) return;
    var pos = e.GetPosition(this);
    Left += pos.X - _dragStart.X;  // direct position update
    Top  += pos.Y - _dragStart.Y;
}

private void OnMouseUp(object sender, MouseButtonEventArgs e)
{
    if (!_isDragging) return;
    _isDragging = false;
    ReleaseMouseCapture();
    Settings.PosX = Left;   // persist to OverlaySettings
    Settings.PosY = Top;
}
```

### How IsLocked change is already handled
```csharp
// Source: BaseOverlayWindow.cs OnSettingsChanged case IsLocked (line 316)
case nameof(OverlaySettings.IsLocked):
    var vis = Settings.IsLocked ? Visibility.Collapsed : Visibility.Visible;
    _edgeRight.Visibility = vis;
    if (!UseWidthOnlyResize)
    {
        _grip.Visibility       = vis;
        _edgeBottom.Visibility = vis;
    }
    break;
// Extension point: add colored border toggling here
```

### How OverlayManager.SetAllLocked already works
```csharp
// Source: OverlayManager.cs lines 491-497
public void SetAllLocked(bool locked)
{
    foreach (var o in _overlays.Values)
    {
        o.Settings.IsLocked = locked;
        o.UpdateLockState();
    }
}
// This already triggers BaseOverlayWindow.OnSettingsChanged for each overlay via PropertyChanged
```

### How ThemeManager loads JSON
```csharp
// Source: ThemeManager.cs lines 83-101
public static void Load(string themeName)
{
    string path = Path.Combine(ThemesDirectory, themeName + ".json");
    if (!File.Exists(path)) path = Path.Combine(ThemesDirectory, themeName);
    if (!File.Exists(path)) { Debug.WriteLine($"Thème introuvable"); return; }
    LoadFromFile(path);
}
// ThemesDirectory = %AppData%/DouzeAssistance/themes/
// New themes: drop .json file there, ThemeManager.GetAvailableThemes() auto-discovers them
```

### How ConfigService handles backward compat
```csharp
// Source: ConfigService.cs lines 23-36
var cfg = JsonConvert.DeserializeObject<AppConfig>(json) ?? new AppConfig();
cfg.General      ??= new GeneralSettings();   // null-coalescing pattern
cfg.FuelStrategy ??= new FuelStrategyConfig();
// Pattern: add nullable fields to OverlaySettings, then add null-coalescing in ConfigService.Load()
// New nullable double? fields in OverlaySettings will deserialize to null from old JSON — safe
```

---

## State of the Art

| Old Approach | Current Approach | Impact for Phase 2 |
|--------------|------------------|--------------------|
| ThemeChanged restarts all overlays (ReinitializeOverlays) | ThemeChanged event on each overlay via ThemeManager.ThemeChanged | Phase 2 themes work without restart via existing event |
| Per-overlay settings only in CustomOptions dict | Typed fields (Opacity, BackgroundOpacity, Scale, etc.) added directly to OverlaySettings | Pattern: VR fields go directly on OverlaySettings (not CustomOptions) since they are core layout |
| All overlays always locked | IsLocked per-overlay, SetAllLocked() for global toggle | Already implemented — Phase 2 just adds visual feedback |

---

## Open Questions

1. **Per-overlay edit bar color picker mechanism**
   - What we know: ThemesTab uses border patches (colored `Border` elements) that open a color picker on click
   - What's unclear: Use `System.Windows.Forms.ColorDialog` (requires WindowsFormsIntegration ref — may not be in project) or a simpler hex TextBox input?
   - Recommendation: Start with hex TextBox (matches existing CustomOptions string storage pattern, zero new dependencies). Can upgrade to color picker in a future iteration.

2. **VR mode detection in BaseOverlayWindow.OnMouseUp**
   - What we know: `OverlayManager` knows VR is active (`IsVRActive`), but `BaseOverlayWindow` only has `DataService` and `Settings` — no reference to `OverlayManager`
   - What's unclear: How should `BaseOverlayWindow` know to write to `VrPosX` instead of `PosX`?
   - Recommendation: Add a static `bool BaseOverlayWindow.IsVRModeActive` field, set by `OverlayManager.StartVR()`/`StopVR()`. Simple and avoids circular references.

3. **Edit mode border rendering approach**
   - What we know: `_outerGrid` in `BaseOverlayWindow` already has layers for background, content, and resize handles
   - What's unclear: Best approach — add a `Border` element as the outermost container vs. manipulate `_outerGrid.Background`
   - Recommendation: Add a dedicated `_editBorder` (`Border` with `BorderBrush = AccentPrimary color`, `BorderThickness=2`, `Background=Transparent`) as the last child of `_outerGrid` (on top via Z-order). Show/hide on lock toggle. This is the cleanest layering.

---

## Validation Architecture

### Test Framework
| Property | Value |
|----------|-------|
| Framework | xUnit 2.9.0 |
| Config file | LMUOverlay.Tests/LMUOverlay.Tests.csproj |
| Quick run command | `dotnet test LMUOverlay\LMUOverlay\LMUOverlay\LMUOverlay.Tests\ --filter "Category=UI" -x` |
| Full suite command | `dotnet test LMUOverlay\LMUOverlay\LMUOverlay\LMUOverlay.Tests\ -x` |

### Phase Requirements → Test Map
| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| UI-01 | Drag position persists to PosX/PosY on MouseUp | unit | `dotnet test --filter "Category=UI&DisplayName~Drag"` | ❌ Wave 0 |
| UI-01 | Snap-to-grid rounds position to 10px boundary | unit | `dotnet test --filter "Category=UI&DisplayName~Snap"` | ❌ Wave 0 |
| UI-02 | Resize persists OverlayWidth/OverlayHeight on StopResize | unit | `dotnet test --filter "Category=UI&DisplayName~Resize"` | ❌ Wave 0 |
| UI-03 | ThemeManager.Load("racing-clair") populates correct colors | unit | `dotnet test --filter "Category=UI&DisplayName~Theme"` | ❌ Wave 0 |
| UI-03 | EnsurePresetThemesExist writes 3 new JSON files | unit | `dotnet test --filter "Category=UI&DisplayName~Preset"` | ❌ Wave 0 |
| UI-04 | VrPosX initialized from PosX on first VR switch | unit | `dotnet test --filter "Category=UI&DisplayName~VrProfile"` | ❌ Wave 0 |
| UI-04 | Drag in VR mode saves to VrPosX not PosX | unit | `dotnet test --filter "Category=UI&DisplayName~VrProfile"` | ❌ Wave 0 |

**Note:** UI tests that involve WPF window instantiation (`Left`, `Top`, `ActualWidth`) are difficult to test in xUnit without STA thread and display. Recommended approach: extract pure logic functions (snap calculation, profile copy logic, color override lookup) into static methods testable without WPF. Visual feedback (borders, edit bar position) is manual-only.

### Sampling Rate
- **Per task commit:** `dotnet test LMUOverlay\LMUOverlay\LMUOverlay\LMUOverlay.Tests\ --filter "Category=UI" -x`
- **Per wave merge:** `dotnet test LMUOverlay\LMUOverlay\LMUOverlay\LMUOverlay.Tests\ -x`
- **Phase gate:** Full suite green (Fuel + UI categories) before `/gsd:verify-work`

### Wave 0 Gaps
- [ ] `LMUOverlay.Tests/UI/SnapGridTests.cs` — covers UI-01 snap math (pure static method, no WPF)
- [ ] `LMUOverlay.Tests/UI/VrProfileTests.cs` — covers UI-04 profile copy logic (pure OverlaySettings logic)
- [ ] `LMUOverlay.Tests/UI/ThemePresetTests.cs` — covers UI-03 JSON writing (file I/O test, no WPF)
- [ ] `LMUOverlay.Tests/UI/ColorOverrideTests.cs` — covers per-overlay color override lookup (pure OverlaySettings/CustomOptions)

---

## Sources

### Primary (HIGH confidence)
- Direct source code read: `BaseOverlayWindow.cs` — drag, resize, lock, theme subscription, OnSettingsChanged full implementation
- Direct source code read: `OverlayManager.cs` — SetAllLocked, ReinitializeOverlays, StartVR/StopVR, all overlay registry
- Direct source code read: `OverlayConfig.cs` — OverlaySettings fields, CustomOptions dict, AppConfig.VREnabled
- Direct source code read: `ThemeManager.cs` — Load, LoadFromFile, SaveCurrentTo, EnsureDefaultThemeExists, ThemesDirectory
- Direct source code read: `ConfigService.cs` — null-coalescing backward compat pattern, Newtonsoft.Json serialization
- Direct source code read: `MainWindow.xaml.cs` — OnToggleLock, OnToggleVR, action bar layout, SelectOverlay settings panel
- Direct source code read: `MainWindow.xaml` — BtnLock, action bar, existing tab layout
- Direct source code read: `ThemesTab.xaml(.cs)` — existing theme selector (ListBox + editor), Initialize pattern
- Direct source code read: `endurance-noir.json` — exact JSON schema for new theme files

### Secondary (MEDIUM confidence)
- `02-CONTEXT.md` — user-verified decisions on lock behavior, theme names, VR fields, snap grid value
- `RECOMMENDATION.md` (Phase 01.1) — WPF confirmed for all Phase 2 work, SkiaSharp deferred

### Tertiary (LOW confidence)
- None — all research grounded in actual source files

---

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — all existing, verified in source
- Architecture patterns: HIGH — patterns traced to exact lines in working code
- Pitfalls: HIGH — derived from actual code flow analysis (IsLocked propagation, ThemeChanged restart, null VR fields)
- Test map: MEDIUM — xUnit framework confirmed; test files don't exist yet (Wave 0 gaps)

**Research date:** 2026-05-19
**Valid until:** Stable — WPF/.NET 8 APIs are frozen; valid indefinitely for this codebase
