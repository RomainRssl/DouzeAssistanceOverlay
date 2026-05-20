# Phase 4: Twitch Chat Visual Customization - Research

**Researched:** 2026-05-20
**Domain:** WPF code-behind UI — image loading, color picker swatches, conditional layout, config serialization
**Confidence:** HIGH

---

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

**Image header — sélection**
- Bouton "Parcourir" dans la section TwitchChat de MainWindow (même zone que le TextBox Channel)
- Formats acceptés : PNG, JPG, BMP (filtre sur l'OpenFileDialog)
- Chemin de l'image sauvegardé dans `TwitchSettings.HeaderImagePath` (config.json)

**Image header — états d'affichage**
- Image sélectionnée : l'image remplace le TextBlock "TWITCH"
- Aucune image : affiche le texte "TCHAT" (fallback texte)
- Option "Masquer le bandeau" : checkbox/toggle dans les settings — cache entièrement la ligne header (image OU texte, plus le séparateur)
- Ce toggle est sauvegardé dans `TwitchSettings` (config.json)

**Couleurs — ce que l'utilisateur contrôle**
- Couleur de fond : correspond au `Background` de l'`outer Border` (actuellement `PanelBackground` alpha 200)
- Couleur accent : correspond à la barre séparateur (actuellement `#9146FF` alpha 80)
- Les deux couleurs sont indépendamment personnalisables
- Sauvegardées dans `TwitchSettings.BackgroundColor` et `TwitchSettings.AccentColor` (hex strings)

**Color picker UI**
- Roue chromatique (color wheel) dans les settings Twitch de MainWindow
- Décision déléguée au researcher : évaluer Xceed.Wpf.Toolkit ColorPicker (MIT) vs implémentation custom
- Un bouton "Reset" par couleur pour revenir aux valeurs par défaut

**Persistance**
- `TwitchSettings` dans config.json reçoit : `HeaderImagePath`, `ShowHeader`, `BackgroundColor`, `AccentColor`
- Migration forward-compatible : Newtonsoft.Json null → valeur initiale C# par défaut

### Claude's Discretion

- Choix entre Xceed.Wpf.Toolkit ColorPicker (MIT) ou implémentation custom pour la roue chromatique

### Deferred Ideas (OUT OF SCOPE)

- Personnalisation de la police des messages Twitch
- Couleurs par utilisateur/badge Twitch
- Animation ou transition sur le header
</user_constraints>

---

## Summary

Phase 4 is a pure WPF code-behind feature with no new external dependencies required. The project already has `AddColorPicker()` — an 8-swatch preset picker — in MainWindow. The CONTEXT.md asks whether to use Xceed.Wpf.Toolkit or custom code; analysis shows the existing preset swatch pattern fully satisfies the use case (no freeform color needed), and adding Xceed would introduce a NuGet dependency, increase binary size, and require `UseWPF=true` adjustments in the test project. **Recommendation: reuse and extend `AddColorPicker()` with Twitch-specific swatches and a Reset button — no new NuGet package.**

The header image logic is straightforward WPF: `BitmapImage` loaded from a file path, replacing a `TextBlock` in the same Grid cell via conditional visibility. The overlay reads its visuals from `TwitchSettings` at construction time; to apply color changes without restart, keep references to the `outer Border` and `sep Border` and expose an `ApplyVisualSettings()` method callable after settings save.

`TwitchSettings.BackgroundColor` stores full RGBA as 8-digit hex (`#AABBGGRR` format is NOT used in this codebase — `ThemeManager.ParseColor` uses `#AARRGGBB`). The accent color requires an alpha channel; the stored hex must be 8-digit ARGB to round-trip through `ThemeManager.ParseColor`.

**Primary recommendation:** Keep all color picking as preset swatches (reuse `AddColorPicker`), add a "PARCOURIR" button row for the image, add "Masquer le bandeau" toggle, add Reset buttons. No new NuGet package.

---

## Standard Stack

### Core (already in project — no new installs)

| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| System.Windows.Media.Imaging (WPF built-in) | .NET 8 | Load BitmapImage from file path | Already used by existing overlays |
| Microsoft.Win32.OpenFileDialog (WPF built-in) | .NET 8 | File picker for image selection | Standard WPF file dialog |
| Newtonsoft.Json | 13.0.3 | Config serialization/deserialization | Already in project |

### Alternatives Considered

| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| Preset swatch color picker (reuse existing) | Xceed.Wpf.Toolkit ColorPicker | Xceed adds a NuGet dependency, requires test project changes; preset swatches already satisfy the use case (8-10 curated colors) |
| Preset swatch color picker | Custom color wheel drawn in code | More work, maintenance burden; preset swatches are sufficient for end-user overlay customization |

**Installation:** No new packages needed. All required APIs are already in the project.

---

## Architecture Patterns

### Pattern 1: TwitchSettings Model Extension

Add four new properties to `TwitchSettings` in `OverlayConfig.cs`. Newtonsoft.Json deserializes missing keys to the C# default value — zero migration risk for existing config.json.

```csharp
// Source: OverlayConfig.cs existing pattern (TwitchSettings, lines 71-75)
public class TwitchSettings
{
    public string Channel        { get; set; } = "";
    public int    MaxMessages    { get; set; } = 20;
    // NEW — Phase 4
    public string HeaderImagePath { get; set; } = "";
    public bool   ShowHeader      { get; set; } = true;
    public string BackgroundColor { get; set; } = "";   // "" = use theme default
    public string AccentColor     { get; set; } = "";   // "" = use Twitch purple default
}
```

**Default sentinel pattern:** Empty string `""` means "use default" — no special null handling needed. The overlay checks `string.IsNullOrEmpty(BackgroundColor)` to fall back to `PanelBackground`.

### Pattern 2: TwitchChatOverlay Visual Initialization

Store references to mutable visual elements as fields. Apply settings at construction. Expose `ApplyVisualSettings()` for hot-update from MainWindow after settings save.

```csharp
// Source: TwitchChatOverlay.cs — extend constructor, add fields
private readonly Border _outerBorder;
private readonly Border _sepBorder;
private readonly Grid   _headerRow;
private readonly Border _headerContainer;   // row 0 + row 1 together (for hide)
private Image?          _headerImage;
private TextBlock?      _headerText;

// In constructor:
_outerBorder = outer;
_sepBorder   = sep;
ApplyVisualSettings();

public void ApplyVisualSettings()
{
    var tw = _config.Twitch;

    // Background color
    Color bg = string.IsNullOrEmpty(tw.BackgroundColor)
        ? Color.FromArgb(200, ThemeManager.Current.PanelBackground.R,
                              ThemeManager.Current.PanelBackground.G,
                              ThemeManager.Current.PanelBackground.B)
        : ThemeManager.ParseColor(tw.BackgroundColor);
    _outerBorder.Background = BrushCache.Get(bg);

    // Accent color
    Color accent = string.IsNullOrEmpty(tw.AccentColor)
        ? Color.FromArgb(80, 145, 70, 255)
        : ThemeManager.ParseColor(tw.AccentColor);
    _sepBorder.Background = BrushCache.Get(accent);

    // Header visibility
    _headerContainer.Visibility = tw.ShowHeader ? Visibility.Visible : Visibility.Collapsed;

    // Header content: image vs text
    if (!string.IsNullOrEmpty(tw.HeaderImagePath) && File.Exists(tw.HeaderImagePath))
    {
        _headerText.Visibility  = Visibility.Collapsed;
        _headerImage.Visibility = Visibility.Visible;
        var bmp = new BitmapImage();
        bmp.BeginInit();
        bmp.UriSource    = new Uri(tw.HeaderImagePath, UriKind.Absolute);
        bmp.CacheOption  = BitmapCacheOption.OnLoad;
        bmp.EndInit();
        _headerImage.Source = bmp;
    }
    else
    {
        _headerImage.Visibility = Visibility.Collapsed;
        _headerText.Visibility  = Visibility.Visible;
    }
}
```

**Note:** `BitmapCacheOption.OnLoad` closes the file handle immediately after load — safe for overlay use.

### Pattern 3: MainWindow Settings Panel — Image Picker Row

Follow the existing code-behind pattern exactly (no XAML/MVVM). File row: label (60px) | path display (stretch) | PARCOURIR button (Auto).

```csharp
// Source: MainWindow.xaml.cs lines 468-528 (channelRow pattern)
var imageRow = new Grid { Margin = new Thickness(0, 4, 0, 4) };
imageRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(60) });
imageRow.ColumnDefinitions.Add(new ColumnDefinition());
imageRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

var pathLabel = new TextBlock { Text = "Image", ... };
var pathDisplay = new TextBlock
{
    Text = string.IsNullOrEmpty(_config.Twitch.HeaderImagePath) ? "(aucune)" : Path.GetFileName(_config.Twitch.HeaderImagePath),
    ...
};
var browseBtn = new Button { Content = "PARCOURIR", ... };
browseBtn.Click += (_, _) =>
{
    var dlg = new Microsoft.Win32.OpenFileDialog
    {
        Filter = "Images|*.png;*.jpg;*.jpeg;*.bmp",
        Title  = "Choisir une image de bandeau"
    };
    if (dlg.ShowDialog() == true)
    {
        _config.Twitch.HeaderImagePath = dlg.FileName;
        pathDisplay.Text = Path.GetFileName(dlg.FileName);
        _configService.Save(_config);
        _overlayManager.GetOverlay<TwitchChatOverlay>("TwitchChat")?.ApplyVisualSettings();
    }
};
```

### Pattern 4: Color Picker with Reset — Twitch Swatches

Reuse `AddColorPicker()` but add a Reset button inline. Since `AddColorPicker` creates a fixed row, add a separate Reset button row or modify to support it. Simplest: add a dedicated `AddColorPickerWithReset()` helper.

```csharp
// Twitch-specific presets (Twitch purple + neutral tones)
string[] twitchPresets = {
    "#9146FF",  // Twitch purple
    "#6441A5",  // Twitch dark purple
    "#1E1F2E",  // near-black
    "#0C0E12",  // current PanelBackground
    "#1A1A2E",  // dark navy
    "#16213E",  // dark blue
    "#0F3460",  // midnight blue
    "#FFFFFF",  // white
};
```

**Reset default values:**
- Background: `""` (empty — triggers theme fallback in `ApplyVisualSettings`)
- Accent: `""` (empty — triggers `#9146FF` alpha 80 fallback)

### Pattern 5: Alpha in AccentColor Storage

`ThemeManager.ParseColor` handles 8-digit ARGB (`#AARRGGBB`). The accent color needs alpha. Store as 8-digit hex string.

```csharp
// Default accent: alpha=80, R=145, G=70, B=255
const string DefaultAccentHex = "#509146FF";  // 80 decimal = 0x50
// The separator currently uses: Color.FromArgb(80, 145, 70, 255)
// = #509146FF in #AARRGGBB format
```

**Picker constraint:** The preset swatches show the RGB hue (no alpha swatch). Alpha is fixed at the default 80 (31%) for accent. User picks the hue; alpha is not user-configurable (out of scope). Store as 6-digit RGB from picker → apply with hardcoded alpha when rendering.

Alternatively: store BackgroundColor as 8-digit to include the 200 alpha, and AccentColor as 8-digit with 80 alpha, but apply the user-chosen RGB while preserving fixed alpha internally. The simpler approach: store as 6-digit RGB, apply fixed alpha at render time.

**Decision for planner:** Store BackgroundColor as 6-digit RGB + apply alpha 200 at render. Store AccentColor as 6-digit RGB + apply alpha 80 at render. This way `AddColorPicker` presets (6-digit) work without modification.

```csharp
// Render-time alpha application (in ApplyVisualSettings):
Color rawBg = ThemeManager.ParseColor(tw.BackgroundColor);  // RGB only
Color bg    = Color.FromArgb(200, rawBg.R, rawBg.G, rawBg.B);

Color rawAcc = ThemeManager.ParseColor(tw.AccentColor);     // RGB only
Color accent = Color.FromArgb(80, rawAcc.R, rawAcc.G, rawAcc.B);
```

### Pattern 6: GetOverlay for Hot-Update

```csharp
// Source: MainWindow.xaml.cs BlindSpotOverlay pattern (line 335)
_overlayManager.GetOverlay<TwitchChatOverlay>("TwitchChat")?.ApplyVisualSettings();
```

Call this after every settings change (image pick, color pick, reset, toggle) so changes take effect without overlay restart.

### Anti-Patterns to Avoid

- **Using `BitmapCacheOption.Default` or no CacheOption:** Leaves the file handle open, preventing the user from moving/deleting the file.
- **Storing full ARGB hex in AddColorPicker presets:** The existing `AddColorPicker` uses 6-digit presets only; `InputDisplayConfig.ParseColor` (RGB only) is used in the picker. Don't mix with `ThemeManager.ParseColor` (ARGB) in the same swatch loop.
- **Rebuilding TwitchChatOverlay on settings change:** Too costly. Use `ApplyVisualSettings()` on the live instance.
- **Using XAML/binding for the new controls:** All MainWindow settings controls are code-behind. Keep consistent.
- **Setting BitmapImage.UriSource with `UriKind.Relative`:** Must be `UriKind.Absolute` for a filesystem path from `OpenFileDialog.FileName`.

---

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| File picker dialog | Custom file dialog | `Microsoft.Win32.OpenFileDialog` | Built into WPF, handles all Win32 file system concerns |
| Image loading from disk | Manual file stream + decoder | `BitmapImage` with `UriSource` + `BitmapCacheOption.OnLoad` | Handles all image format decoding, EXIF, format detection |
| Color parsing | Custom hex parser | `ThemeManager.ParseColor` (ARGB) or `InputDisplayConfig.ParseColor` (RGB) | Both already in codebase, tested |
| Brush caching | Raw `new SolidColorBrush()` | `BrushCache.Get()` | Frozen brushes for thread safety and WPF optimization |

**Key insight:** This phase uses zero new libraries. All needed primitives (`OpenFileDialog`, `BitmapImage`, `ThemeManager.ParseColor`, `BrushCache`) are already in the project.

---

## Common Pitfalls

### Pitfall 1: BrushCache With Modified Frozen Brush

**What goes wrong:** `ApplyVisualSettings()` calls `BrushCache.Get(color)` — the brush is frozen. Assigning to `_outerBorder.Background` works fine (assigns a new brush reference). But if code tries to *modify* a cached brush (e.g., `((SolidColorBrush)_outerBorder.Background).Color = ...`) it throws `InvalidOperationException: Cannot modify a frozen object`.
**How to avoid:** Always call `BrushCache.Get(newColor)` to get a new (or cached) frozen brush and assign it to the property. Never mutate the existing brush on the border.

### Pitfall 2: BitmapImage URI Kind

**What goes wrong:** `new Uri(path)` without specifying `UriKind.Absolute` may fail or load incorrectly for absolute paths returned by `OpenFileDialog.FileName`.
**How to avoid:** Always use `new Uri(path, UriKind.Absolute)`.

### Pitfall 3: File-Not-Found on Overlay Startup

**What goes wrong:** The user has a `HeaderImagePath` saved in config, but moves or deletes the image file. The overlay constructor calls `ApplyVisualSettings()`, `File.Exists()` returns false, but without the guard the `BitmapImage` would throw.
**How to avoid:** Always guard with `!string.IsNullOrEmpty(path) && File.Exists(path)` before loading. Fall back to text "TCHAT" silently.

### Pitfall 4: `_configService.Save()` Not Called After Color Change

**What goes wrong:** Color swatch clicked → `set(hex)` callback updates `_config.Twitch.BackgroundColor` → `ApplyVisualSettings()` called → overlay updates → but config not saved → restart loses the setting.
**How to avoid:** In every swatch `MouseLeftButtonDown` callback (or in a dedicated color-change handler), call `_configService.Save(_config)` then `ApplyVisualSettings()`. The existing `AddColorPicker` does NOT call save — the caller's `set` lambda must do it.

### Pitfall 5: ShowHeader Toggle Hiding Separator

**What goes wrong:** Setting only the header Grid row to `Collapsed` still leaves the separator `Border` (row 1) visible, creating an orphaned line.
**How to avoid:** Wrap both header (row 0) and separator (row 1) in a single container, or set both to `Collapsed` when `ShowHeader=false`. Simpler: keep header and sep as separate elements but set both in `ApplyVisualSettings()`:
```csharp
_headerGrid.Visibility = tw.ShowHeader ? Visibility.Visible : Visibility.Collapsed;
_sepBorder.Visibility  = tw.ShowHeader ? Visibility.Visible : Visibility.Collapsed;
```

### Pitfall 6: Test Project Cannot Use `OpenFileDialog` or `BitmapImage`

**What goes wrong:** Test project has `UseWPF=false` (confirmed in `LMUOverlay.Tests.csproj`). Any test that directly instantiates `OpenFileDialog` or `BitmapImage` will fail to compile.
**How to avoid:** Do not test WPF UI construction. Test only: (1) `TwitchSettings` JSON round-trip, (2) `ApplyVisualSettings` pure logic via testable helper (similar to `WebBrowserUrlValidator`), (3) default sentinel values. All image/dialog code lives in MainWindow and TwitchChatOverlay — not testable without WPF harness; accept this as manual-verify scope.

---

## Code Examples

### TwitchSettings JSON Round-Trip (testable)

```csharp
// Source: WebBrowserTests.cs pattern (Phase 3)
[Fact]
public void TwitchSettings_NewFields_SurviveRoundTrip()
{
    var config = new AppConfig();
    config.Twitch.HeaderImagePath = "C:\\test.png";
    config.Twitch.ShowHeader      = false;
    config.Twitch.BackgroundColor = "#1A1A2E";
    config.Twitch.AccentColor     = "#9146FF";

    string json       = JsonConvert.SerializeObject(config);
    var    deserialized = JsonConvert.DeserializeObject<AppConfig>(json)!;

    Assert.Equal("C:\\test.png", deserialized.Twitch.HeaderImagePath);
    Assert.False(deserialized.Twitch.ShowHeader);
    Assert.Equal("#1A1A2E", deserialized.Twitch.BackgroundColor);
    Assert.Equal("#9146FF", deserialized.Twitch.AccentColor);
}

[Fact]
public void TwitchSettings_OldJsonWithoutNewFields_UsesDefaults()
{
    string oldJson = "{\"Twitch\":{\"Channel\":\"test\",\"MaxMessages\":20}}";
    var config = JsonConvert.DeserializeObject<AppConfig>(oldJson)!;

    Assert.Equal("",   config.Twitch.HeaderImagePath);  // default
    Assert.True(config.Twitch.ShowHeader);               // default true
    Assert.Equal("",   config.Twitch.BackgroundColor);   // default
    Assert.Equal("",   config.Twitch.AccentColor);       // default
}
```

### Color Default Sentinel Logic (testable, pure C#)

```csharp
// Source: pattern from TwitchChatOverlay ApplyVisualSettings — extractable to pure helper
public static Color ResolveBackgroundColor(string stored, Color themeDefault)
    => string.IsNullOrEmpty(stored)
        ? Color.FromArgb(200, themeDefault.R, themeDefault.G, themeDefault.B)
        : ApplyAlpha(ThemeManager.ParseColor(stored), 200);

public static Color ResolveAccentColor(string stored)
    => string.IsNullOrEmpty(stored)
        ? Color.FromArgb(80, 145, 70, 255)
        : ApplyAlpha(ThemeManager.ParseColor(stored), 80);

private static Color ApplyAlpha(Color rgb, byte alpha)
    => Color.FromArgb(alpha, rgb.R, rgb.G, rgb.B);
```

**Note:** `Color` is `System.Windows.Media.Color` — WPF type, not testable without `UseWPF=true`. For unit tests, extract the logic to operate on byte tuples or just test the JSON round-trip.

### AddColorPicker With Reset Pattern

```csharp
// In MainWindow TwitchChat section — BackgroundColor picker
AddSep();
Add(new TextBlock { Text = "COULEURS", ... });

// Reuse AddColorPicker with Twitch-themed swatches
AddColorPicker("Fond", 
    string.IsNullOrEmpty(_config.Twitch.BackgroundColor) ? "#0C0E12" : _config.Twitch.BackgroundColor,
    v =>
    {
        _config.Twitch.BackgroundColor = v;
        _configService.Save(_config);
        _overlayManager.GetOverlay<TwitchChatOverlay>("TwitchChat")?.ApplyVisualSettings();
    });

// Reset button for background
var resetBgBtn = new Button { Content = "Reset fond", ... };
resetBgBtn.Click += (_, _) =>
{
    _config.Twitch.BackgroundColor = "";
    _configService.Save(_config);
    _overlayManager.GetOverlay<TwitchChatOverlay>("TwitchChat")?.ApplyVisualSettings();
};
Add(resetBgBtn);
```

---

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| New NuGet for color picker | Reuse existing preset swatch system | Research 2026-05-20 | Zero new dependencies |
| 8-digit ARGB hex stored | 6-digit RGB stored + alpha applied at render | Research 2026-05-20 | Simpler, compatible with existing `AddColorPicker` |

---

## Open Questions

1. **`GetOverlay<T>()` availability on OverlayManager**
   - What we know: Used in MainWindow for `BlindSpotOverlay` (line 335) — pattern exists.
   - What's unclear: Whether `GetOverlay<TwitchChatOverlay>("TwitchChat")` key matches exactly (confirmed key is "TwitchChat" from `_allOverlays` line 66).
   - Recommendation: Verify key string at implementation time; confirmed as "TwitchChat".

2. **Image scaling in the header Grid**
   - What we know: The header Grid column 0 is stretch, column 1 is Auto (status text). The image must respect the column width.
   - What's unclear: Whether `Stretch` or `Uniform` is better for the image `Stretch` property.
   - Recommendation: Use `Stretch=Uniform`, `MaxHeight=18` (same height as the current "TWITCH" TextBlock at FontSize 10). This preserves aspect ratio and stays visually consistent.

---

## Validation Architecture

### Test Framework

| Property | Value |
|----------|-------|
| Framework | xUnit 2.9.0 |
| Config file | none (implicit discovery) |
| Quick run command | `dotnet test LMUOverlay\LMUOverlay\LMUOverlay\LMUOverlay.Tests\ --filter "Category=TwitchVisual" --no-build` |
| Full suite command | `dotnet test LMUOverlay\LMUOverlay\LMUOverlay\LMUOverlay.Tests\ --no-build` |

### Phase Requirements → Test Map

| ID | Behavior | Test Type | Automated Command | File Exists? |
|----|----------|-----------|-------------------|-------------|
| TWITCH-V-01 | TwitchSettings new fields survive JSON round-trip | unit | `dotnet test --filter "FullyQualifiedName~TwitchVisualConfigTests"` | Wave 0 |
| TWITCH-V-02 | Old config.json without new fields deserializes to defaults | unit | `dotnet test --filter "FullyQualifiedName~TwitchVisualConfigTests"` | Wave 0 |
| TWITCH-V-03 | Image picker button appears, file filter correct | manual | n/a — WPF dialog, no-headless | manual |
| TWITCH-V-04 | Header hides when ShowHeader=false | manual | n/a — WPF rendering | manual |
| TWITCH-V-05 | Background and accent colors apply live without restart | manual | n/a — WPF rendering | manual |
| TWITCH-V-06 | Reset restores default colors | manual | n/a — WPF rendering | manual |
| TWITCH-V-07 | Missing image file at startup falls back to "TCHAT" text | manual | n/a — WPF rendering | manual |

### Sampling Rate

- **Per task commit:** `dotnet test LMUOverlay\LMUOverlay\LMUOverlay\LMUOverlay.Tests\ --filter "Category=TwitchVisual" --no-build`
- **Per wave merge:** `dotnet test LMUOverlay\LMUOverlay\LMUOverlay\LMUOverlay.Tests\ --no-build`
- **Phase gate:** Full suite green + manual verification checklist before `/gsd:verify-work`

### Wave 0 Gaps

- [ ] `LMUOverlay.Tests\TwitchVisual\TwitchVisualConfigTests.cs` — covers TWITCH-V-01 and TWITCH-V-02 (JSON round-trip)

---

## Sources

### Primary (HIGH confidence)

- `TwitchChatOverlay.cs` (full file read) — exact field structure, current colors, constructor pattern
- `OverlayConfig.cs` lines 69-75, 869-931 — TwitchSettings, ThrottleColor pattern, ParseColor
- `MainWindow.xaml.cs` lines 452-534, 670-721 — TwitchChat section, AddColorPicker implementation
- `LMUOverlay.Tests.csproj` — UseWPF=false confirmed, test project constraints
- `WebBrowserTests.cs` — TDD stub pattern (RED stubs, JSON round-trip, manual-only WPF verification)
- `LMUOverlay.csproj` — no Xceed, no external color picker dependency
- `ThemeManager.cs` line 477 — ParseColor handles 8-digit ARGB

### Secondary (MEDIUM confidence)

- `BrushCache.cs` — frozen brush pattern, thread safety
- `BlindSpotOverlay` reference in MainWindow (line 335) — GetOverlay hot-update pattern
- `InputGraphOverlay.cs` lines 177-322 — ParseColor read-per-tick, no live-update callback needed

### Tertiary (LOW confidence)

- None

---

## Metadata

**Confidence breakdown:**
- Model changes (TwitchSettings): HIGH — existing field pattern (ThrottleColor) directly mirrors what's needed
- TwitchChatOverlay visual logic: HIGH — full file read, patterns verified
- MainWindow settings panel: HIGH — AddColorPicker and AddToggle fully read, no new helpers required
- Test architecture: HIGH — test project constraints confirmed via .csproj

**Research date:** 2026-05-20
**Valid until:** 2026-07-20 (stable WPF/xUnit stack, no fast-moving dependencies)
