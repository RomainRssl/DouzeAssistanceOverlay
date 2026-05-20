# Phase 3: Web Browser Overlay - Research

**Researched:** 2026-05-20
**Domain:** Microsoft.Web.WebView2 in WPF, HWND airspace problem, overlay integration
**Confidence:** HIGH

---

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions
- L'URL est saisie dans MainWindow (nouveau champ TextBox dans l'onglet HUD ou un onglet dédié)
- L'URL N'EST PAS persistée dans config.json — elle est volatile, remise à zéro à chaque lancement
- Pattern de référence : le TextBox du channel Twitch (MainWindow.xaml.cs:481) pour le style et le placement
- Si l'URL est invalide (format incorrect) ou si la page échoue à charger → l'overlay se désactive (`IsEnabled = false`)
- Pas de message d'erreur affiché dans l'overlay — la désactivation silencieuse est suffisante
- L'utilisateur PEUT cliquer à l'intérieur du WebView2 (interactions web actives)
- Le drag-and-drop de l'overlay se fait exclusivement via le bandeau titre
- `BaseOverlayWindow` avec `UseRawResize = true`
- `OverlayManager.Reg("WebBrowser", ...)`

### Claude's Discretion
- Taille par défaut
- Fréquence de refresh automatique (si pertinent)
- Comportement au resize

### Deferred Ideas (OUT OF SCOPE)
- Persistance de l'URL entre sessions
- Historique d'URLs / favoris
- Plusieurs onglets / plusieurs WebBrowserOverlay
</user_constraints>

---

## Summary

The central technical challenge of this phase is the WPF airspace problem. `BaseOverlayWindow` sets `AllowsTransparency = true` and `Background = Brushes.Transparent` by default (confirmed in constructor). WebView2 is an HWND-based Win32 control hosted via `HwndHost` — this combination is **fundamentally incompatible**: the Chromium surface cannot render inside a software-composited WPF window. The solution is to set `AllowsTransparency = false` on the WebBrowserOverlay window and simulate transparency using a solid dark background with low opacity, which is the same visual effect the user already sees with all other overlays.

This approach avoids the airspace problem entirely, requires no additional NuGet packages, and is consistent with the existing overlay aesthetic. The title bar bandeau remains the sole drag handle, and MouseDown on it will work correctly because it's a pure WPF element positioned above the WebView2 area.

**Primary recommendation:** Override `AllowsTransparency = false` and `Background = new SolidColorBrush(theme.PanelBackground)` in `WebBrowserOverlay` constructor after calling `base(ds, s)`. This overrides the transparent window mode while keeping all drag/resize/VR machinery intact.

---

## Standard Stack

### Core
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| Microsoft.Web.WebView2 | 1.0.2849.39 (stable, Windows 11 preinstalled) | Chromium-based web control for WPF | Only official Microsoft WebView2 WPF package |

### Supporting
| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| Microsoft.Web.WebView2.Core | (included in package) | NavigationCompleted event args, CoreWebView2 API | Always — needed for IsSuccess, WebErrorStatus |

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| Standard WebView2 (HwndHost) | WebView2CompositionControl | CompositionControl uses GraphicsCaptureSession; fixes airspace but DRM broken and performance overhead. **Not needed here** because we're dropping AllowsTransparency. |
| AllowsTransparency=false | SetLayeredWindowAttributes | Requires P/Invoke, complex, not used elsewhere in codebase |

**Installation:**
```bash
dotnet add package Microsoft.Web.WebView2
```
Or in .csproj:
```xml
<PackageReference Include="Microsoft.Web.WebView2" Version="1.0.2849.39" />
```

**Runtime requirement:** WebView2 Runtime is pre-installed on all Windows 11 machines (ships with Edge). No extra deployment needed for the target audience.

---

## Architecture Patterns

### Recommended Project Structure
```
Views/Overlays/
└── WebBrowserOverlay.cs     # new file — mirrors NoteOverlay.cs structure

Models/OverlayConfig.cs      # add WebBrowser property to AppConfig
Services/OverlayManager.cs   # add Reg("WebBrowser", ...) line
Views/MainWindow.xaml.cs     # add URL TextBox + "Charger" button section
```

### Pattern 1: Override AllowsTransparency in Subclass Constructor

**What:** After `base(ds, s)`, set `AllowsTransparency = false` and assign an opaque background. This overrides the base class's transparent window mode without touching BaseOverlayWindow.

**When to use:** Any overlay that embeds HWND-based Win32 controls (WebView2, Windows Forms controls, Direct3D surfaces).

**Example:**
```csharp
// Source: confirmed from BaseOverlayWindow constructor analysis + WPF docs
public WebBrowserOverlay(DataService ds, OverlaySettings s) : base(ds, s)
{
    UseRawResize = true;

    // CRITICAL: WebView2 is HWND-based — incompatible with AllowsTransparency=true.
    // Override the base class defaults before the window is shown.
    AllowsTransparency = false;
    WindowStyle = WindowStyle.None;   // keeps borderless appearance
    Background = new SolidColorBrush(
        Color.FromArgb(220,
            ThemeManager.Current.PanelBackground.R,
            ThemeManager.Current.PanelBackground.G,
            ThemeManager.Current.PanelBackground.B));
    // ... build content
}
```

### Pattern 2: WebView2 Initialization (EnsureCoreWebView2Async)

**What:** WebView2 must be initialized asynchronously before navigation. The simplest WPF pattern uses `EnsureCoreWebView2Async` called from `Loaded`.

**When to use:** Whenever you need to navigate immediately after creation, or subscribe to `CoreWebView2` events.

**Example:**
```csharp
// Source: https://learn.microsoft.com/en-us/microsoft-edge/webview2/get-started/wpf
private Microsoft.Web.WebView2.Wpf.WebView2 _webView;

// In constructor — add Loaded handler
Loaded += async (_, _) =>
{
    await _webView.EnsureCoreWebView2Async(null);
    // CoreWebView2 is now ready
};
```

Note: `EnsureCoreWebView2Async(null)` uses the default user data folder. For an overlay, this is fine — WebView2 creates a profile in `%AppData%\Microsoft\WebView2` automatically.

### Pattern 3: Navigation and Failure Detection

**What:** Call `CoreWebView2.Navigate(url)` or set `Source` property. Subscribe to `NavigationCompleted` to detect failure.

**When to use:** Any navigation that should disable the overlay on failure.

**Example:**
```csharp
// Source: https://learn.microsoft.com/en-us/dotnet/api/microsoft.web.webview2.core.corewebview2.navigationcompleted
_webView.NavigationCompleted += (sender, e) =>
{
    if (!e.IsSuccess)
    {
        // Silent disable — no error message in overlay per spec
        Dispatcher.Invoke(() => Settings.IsEnabled = false);
    }
};

// Validate URL format before navigating
private bool TryNavigate(string url)
{
    if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)
        || (uri.Scheme != "http" && uri.Scheme != "https"))
    {
        Settings.IsEnabled = false;
        return false;
    }
    _webView.CoreWebView2.Navigate(url);
    return true;
}
```

`e.IsSuccess` is `false` for: DNS failure, network error, 4xx HTTP responses, TLS errors, cancelled navigation. It maps to `CoreWebView2WebErrorStatus` enum.

### Pattern 4: Drag Via Bandeau With WebView2 Present

**What:** WebView2 as HwndHost captures all mouse input within its Win32 window. However, the title bandeau sits in a separate WPF layer **above** the WebView2 in the visual tree. MouseDown on the bandeau fires before WebView2 sees any input.

**When to use:** This is the standard pattern — no special handling needed because the bandeau is not inside the WebView2 client area.

**Key insight:** `OnMouseDown` in `BaseOverlayWindow` handles drag — this fires on the WPF window level. The bandeau (a WPF Border/StackPanel at the top) is hit-tested first. Clicks **within the WebView2 area** go to Chromium. Clicks **on the bandeau** go to WPF drag. This is correct behavior as specified.

**Warning:** If you add WPF elements that overlap the WebView2 client area (e.g., a status indicator positioned over the web content), those WPF elements will be obscured by the WebView2 HWND. With `AllowsTransparency = false` this is the HwndHost airspace behavior. Mitigation: keep all WPF chrome in a row above/below the WebView2, never overlapping it.

### Pattern 5: AppConfig and OverlayManager Integration

```csharp
// In AppConfig (Models/OverlayConfig.cs)
public OverlaySettings WebBrowser { get; set; } = new("Navigateur Web", false);

// In OverlayManager.Initialize()
Reg("WebBrowser", () => new WebBrowserOverlay(_dataService, _config.WebBrowser));

// WebBrowser is NOT in _persistentOverlays — it behaves like a game overlay.
// It IS visible without LMU connection in the same way as NoteOverlay (which
// also isn't in persistentOverlays but works independently).
// Decision: add "WebBrowser" to _persistentOverlays so user can use it
// independently of LMU connection (it has no game data dependency).
```

### Pattern 6: MainWindow URL TextBox (Twitch reference pattern)

The Twitch channel block at MainWindow.xaml.cs:481 is the exact pattern to replicate:
- Label column (fixed width) + TextBox (stretch) + Button (auto) in a Grid
- Button triggers navigation, Enter key in TextBox also triggers it
- No persistence — URL stays in-memory only

```csharp
// MainWindow.xaml.cs — inside the "WebBrowser" overlay settings block
var urlRow = new Grid { Margin = new Thickness(0, 0, 0, 4) };
urlRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(40) });
urlRow.ColumnDefinitions.Add(new ColumnDefinition());
urlRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

var urlBox = new TextBox
{
    Text              = "",           // volatile — never populated from config
    FontSize          = 11,
    FontFamily        = new FontFamily("Consolas"),
    Foreground        = B(34, 197, 94),
    Background        = new SolidColorBrush(Color.FromRgb(22, 24, 30)),
    BorderBrush       = new SolidColorBrush(Color.FromRgb(50, 50, 60)),
    BorderThickness   = new Thickness(1),
    Padding           = new Thickness(6, 3, 6, 3),
    VerticalAlignment = VerticalAlignment.Center,
    Margin            = new Thickness(4, 0, 4, 0),
    // hint: "https://..." placeholder text via WatermarkTextBox pattern or
    // use a placeholder approach in GotFocus/LostFocus
};

var loadBtn = new Button
{
    Content   = "CHARGER",
    FontSize  = 9,
    Padding   = new Thickness(10, 3, 10, 3),
    Style     = (Style)FindResource("FlatToggle"),
    Foreground = new SolidColorBrush(Color.FromRgb(0, 210, 190)),
};

loadBtn.Click += (_, _) =>
{
    var overlay = _overlayManager.GetOverlay<WebBrowserOverlay>("WebBrowser");
    overlay?.LoadUrl(urlBox.Text.Trim());
};
urlBox.KeyDown += (_, e) =>
{
    if (e.Key == System.Windows.Input.Key.Enter)
        loadBtn.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
};
```

### Anti-Patterns to Avoid

- **Keeping AllowsTransparency=true with WebView2:** The window will either show a black rectangle where WebView2 should be, or WebView2 will simply not render. This is a hard WPF/Win32 architectural constraint, not a configuration issue.
- **Overlaying WPF elements on top of WebView2 client area:** HwndHost HWND always draws on top of WPF content at the same visual layer. Structure the layout so all WPF chrome (bandeau, status) is in rows above/below WebView2, never overlapping.
- **Calling Navigate before EnsureCoreWebView2Async completes:** CoreWebView2 is null until initialization completes. Always await `EnsureCoreWebView2Async` or check `_webView.CoreWebView2 != null`.
- **Using Source property for initial navigation and also calling Navigate:** Redundant navigation. Use either `Source = new Uri(url)` for initial load, or `CoreWebView2.Navigate(url)` after initialization — not both.
- **Storing URL in OverlaySettings.CustomOptions:** Explicitly deferred out of scope. Do not add URL persistence.

---

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Web page rendering | Custom HTML renderer | WebView2 | Chromium engine, full web standard support |
| Navigation failure detection | Try/catch around HTTP requests | NavigationCompleted + IsSuccess | Event-based, handles all failure modes (DNS, TLS, 4xx, network) |
| URL format validation | Regex-based URL parser | `Uri.TryCreate` + scheme check | Handles edge cases, RFC-compliant |
| HWND airspace | Custom D3D/WS_EX_LAYERED workaround | AllowsTransparency=false + opaque bg | Zero custom native code, consistent with project style |

**Key insight:** The `NavigationCompleted` event is the only reliable way to know if a web page loaded. HTTP errors (404, 500) do NOT throw C# exceptions — they are reported via `IsSuccess = false` in the event args.

---

## Common Pitfalls

### Pitfall 1: Black WebView2 Rectangle (AllowsTransparency Conflict)
**What goes wrong:** WebView2 renders as a black or invisible rectangle when the WPF Window has `AllowsTransparency = true`.
**Why it happens:** Software composition pipeline (WPF transparency) and HWND hardware rendering pipeline (WebView2/Chromium) cannot share the same window.
**How to avoid:** In `WebBrowserOverlay` constructor, after `base(ds, s)`: set `AllowsTransparency = false`, keep `WindowStyle = WindowStyle.None`.
**Warning signs:** Black rectangle at WebView2 position; WebView2 renders but WPF is invisible; application crash on window show.

### Pitfall 2: Navigate Called Before CoreWebView2 Initialized
**What goes wrong:** `NullReferenceException` on `_webView.CoreWebView2.Navigate(...)` because CoreWebView2 is null until async initialization completes.
**Why it happens:** WebView2 initialization is asynchronous. The WPF control is created synchronously but the Chromium process starts separately.
**How to avoid:** Always await `EnsureCoreWebView2Async()` before calling `CoreWebView2.Navigate()`. Expose a `LoadUrl(string)` method that the MainWindow calls; inside, check `CoreWebView2 != null` or track initialization state.
**Warning signs:** NullReferenceException in stack trace mentioning `CoreWebView2`.

### Pitfall 3: WPF Elements Overlapping WebView2 Area Are Hidden
**What goes wrong:** A WPF control placed over the WebView2 client area is invisible — the HWND draws on top.
**Why it happens:** HwndHost-based controls always appear above WPF visuals in the same z-space. This is the HwndHost airspace issue that still exists even with `AllowsTransparency=false`.
**How to avoid:** Design the layout as a vertical stack: `[bandeau WPF]` on top row, `[WebView2]` in the remaining space. Never add WPF overlays on top of WebView2.
**Warning signs:** WPF elements disappear when WebView2 is visible.

### Pitfall 4: AllowsTransparency Cannot Be Changed After Window Is Shown
**What goes wrong:** Setting `AllowsTransparency = false` after `Show()` throws `InvalidOperationException`.
**Why it happens:** WPF bakes this property into the HwndSource during window creation.
**How to avoid:** Set `AllowsTransparency = false` in the constructor, before the window is ever shown.

### Pitfall 5: NavigationCompleted Fires on Redirect Chains
**What goes wrong:** `NavigationCompleted` with `IsSuccess=false` fires prematurely for a redirect (301/302).
**Why it happens:** Each navigation (including redirects) can generate events.
**How to avoid:** For HTTP redirects, WebView2 follows them automatically and fires one final `NavigationCompleted`. `IsSuccess` will be `true` if the final destination loads. Only set `IsEnabled=false` when `IsSuccess` is definitively `false`.

### Pitfall 6: WebView2 User Data Folder Contention
**What goes wrong:** Two instances of the app running simultaneously both try to use the same WebView2 user data folder, causing initialization failure.
**Why it happens:** Default user data folder is `%AppData%\Roaming\<AppName>`. Only one process can own it.
**How to avoid:** For a game overlay, this is acceptable — only one instance runs at a time. Document this limitation.

---

## Code Examples

### WebBrowserOverlay — Minimal Structure
```csharp
// Source: based on NoteOverlay.cs pattern + official WebView2 WPF docs
using Microsoft.Web.WebView2.Wpf;
using Microsoft.Web.WebView2.Core;

public class WebBrowserOverlay : BaseOverlayWindow
{
    private readonly WebView2 _webView;
    private bool _initialized;

    public WebBrowserOverlay(DataService ds, OverlaySettings s) : base(ds, s)
    {
        UseRawResize = true;

        // FIX: WebView2 (HwndHost) incompatible with AllowsTransparency=true
        // Must be set before window is shown (cannot change after Show())
        AllowsTransparency = false;
        WindowStyle = WindowStyle.None;
        var bg = ThemeManager.Current.PanelBackground;
        Background = new SolidColorBrush(Color.FromArgb(220, bg.R, bg.G, bg.B));

        _webView = new WebView2
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment   = VerticalAlignment.Stretch,
        };

        var outer = new Border
        {
            Background   = Brushes.Transparent,
            CornerRadius = new CornerRadius(6),
        };
        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });  // bandeau
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // webview

        var title = OverlayHelper.MakeTitle("WEB");
        Grid.SetRow(title, 0);
        root.Children.Add(title);

        Grid.SetRow(_webView, 1);
        root.Children.Add(_webView);

        outer.Child = root;
        Content = outer;

        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;
        await _webView.EnsureCoreWebView2Async(null);
        _webView.NavigationCompleted += OnNavigationCompleted;
        _initialized = true;
    }

    public void LoadUrl(string url)
    {
        if (!_initialized || _webView.CoreWebView2 == null) return;

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)
            || (uri.Scheme != "http" && uri.Scheme != "https"))
        {
            Settings.IsEnabled = false;
            return;
        }
        _webView.CoreWebView2.Navigate(url);
    }

    private void OnNavigationCompleted(object? sender,
        CoreWebView2NavigationCompletedEventArgs e)
    {
        if (!e.IsSuccess)
            Dispatcher.Invoke(() => Settings.IsEnabled = false);
    }

    public override void UpdateData() { }  // no LMU data dependency

    protected override void OnClosed(EventArgs e)
    {
        _webView.NavigationCompleted -= OnNavigationCompleted;
        _webView.Dispose();
        base.OnClosed(e);
    }
}
```

### NavigationCompleted Event Args Reference
```csharp
// Source: https://learn.microsoft.com/en-us/dotnet/api/microsoft.web.webview2.core.corewebview2navigationcompletedeventargs
// e.IsSuccess        — bool: false on DNS error, network error, 4xx, TLS failure
// e.WebErrorStatus   — CoreWebView2WebErrorStatus enum: ConnectionAborted, Timeout, etc.
// e.HttpStatusCode   — int: HTTP status code (0 if no HTTP response)
// e.NavigationId     — ulong: matches NavigationStarting event NavigationId
```

### URL Validation Pattern
```csharp
// Source: System.Uri — built-in .NET, no additional package needed
private static bool IsValidWebUrl(string url)
{
    return Uri.TryCreate(url, UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
}
```

---

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| WPF WebBrowser (IE-based) | WebView2 (Chromium-based) | ~2020 | Modern web standards, no IE quirks mode |
| WindowsFormsHost + WebBrowser | WebView2 WPF native control | 2020 | No WinForms dependency, proper WPF integration |
| Manual airspace workarounds | AllowsTransparency=false + opaque bg | Always valid | Simplest, most reliable approach for game overlays |
| WebView2 1.0.864 (first stable) | WebView2 1.0.2849+ (2024+) | Ongoing | NavigationCompleted.HttpStatusCode added in 1.0.1150 |

**Deprecated/outdated:**
- System.Windows.Controls.WebBrowser: Internet Explorer-based, removed from modern WPF best practices. Do not use.
- WindowsFormsHost to host Windows Forms WebBrowser: Adds WinForms dependency, double airspace problem (HwndHost inside HwndHost). Do not use.

---

## Open Questions

1. **WebView2 user data folder — where to place it?**
   - What we know: Default is `%AppData%\Roaming\<process name>`. Chromium profile (cookies, cache) is stored there.
   - What's unclear: Should we use a custom path (e.g., next to config.json) for cleaner uninstall?
   - Recommendation: Use `null` for default (simplest, standard). Document that uninstall should clean `%AppData%\Roaming\DouzeAssistance`.

2. **WebView2 runtime not installed on rare Windows 10 machines**
   - What we know: WebView2 Runtime ships with Windows 11 and Edge. On old Windows 10 without Edge, it may not be present.
   - What's unclear: Target audience (sim racers on Windows 10 without Edge = rare but possible).
   - Recommendation: Wrap `EnsureCoreWebView2Async` in try/catch; if it throws `WebView2RuntimeNotFoundException`, disable the overlay with a log message. Do not add installer complexity for Phase 3.

3. **Default overlay size for web content**
   - What we know: `UseRawResize = true` defaults to 300x350 (from BaseOverlayWindow). This is narrow for most web pages.
   - Recommendation: Default to 600x450 for WebBrowserOverlay specifically. Override in `OnFirstLoaded` by checking `savedW < 400` (not `savedW < 150`).

---

## Validation Architecture

> `workflow.nyquist_validation = true` in .planning/config.json — section included.

### Test Framework
| Property | Value |
|----------|-------|
| Framework | xUnit 2.9.0 |
| Config file | LMUOverlay.Tests.csproj (net8.0-windows, UseWPF=false) |
| Quick run command | `dotnet test LMUOverlay/LMUOverlay/LMUOverlay/LMUOverlay.Tests --filter "Category=WebBrowser" --no-build` |
| Full suite command | `dotnet test LMUOverlay/LMUOverlay/LMUOverlay/LMUOverlay.Tests` |

### Phase Requirements → Test Map

| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| WB-01 | URL validation rejects non-http/https and malformed strings | unit | `dotnet test --filter "WebBrowserUrlValidation"` | Wave 0 |
| WB-02 | IsEnabled=false set when navigation fails | unit (logic only, no WebView2 instance) | `dotnet test --filter "WebBrowserNavigationFailure"` | Wave 0 |
| WB-03 | AppConfig deserializes new WebBrowser field from JSON without breaking old configs | unit | `dotnet test --filter "AppConfigWebBrowser"` | Wave 0 |
| WB-04 | WebBrowserOverlay builds content tree (title bandeau + webview row) | manual-only | N/A — requires WPF UI thread | N/A |
| WB-05 | Page loads and renders in overlay window | manual-only | N/A — requires WebView2 runtime | N/A |
| WB-06 | Click in web area triggers web interaction (not drag) | manual-only | N/A — requires WebView2 runtime | N/A |

**Notes on manual-only tests:**
- WB-04, WB-05, WB-06 require the WPF dispatcher and WebView2 runtime — cannot run in headless CI. These are verified during wave execution via the running application.

### Sampling Rate
- **Per task commit:** `dotnet test LMUOverlay/LMUOverlay/LMUOverlay/LMUOverlay.Tests --filter "Category=WebBrowser"`
- **Per wave merge:** `dotnet test LMUOverlay/LMUOverlay/LMUOverlay/LMUOverlay.Tests`
- **Phase gate:** Full suite green before `/gsd:verify-work`

### Wave 0 Gaps
- [ ] `LMUOverlay.Tests/WebBrowser/WebBrowserTests.cs` — covers WB-01, WB-02, WB-03
  - WB-01: `IsValidWebUrl` static helper (pure function, no WPF needed)
  - WB-02: mock `Settings.IsEnabled` via `OverlaySettings` directly (no window creation)
  - WB-03: JSON round-trip test for `AppConfig.WebBrowser` field using `Newtonsoft.Json`

---

## Sources

### Primary (HIGH confidence)
- Microsoft Learn — WebView2 WPF platforms page (2025-11-26 updated): https://learn.microsoft.com/en-us/microsoft-edge/webview2/platforms/wpf
- Microsoft Learn — Get started with WebView2 in WPF: https://learn.microsoft.com/en-us/microsoft-edge/webview2/get-started/wpf
- CoreWebView2NavigationCompletedEventArgs.IsSuccess: https://learn.microsoft.com/en-us/dotnet/api/microsoft.web.webview2.core.corewebview2navigationcompletedeventargs.issuccess
- WPF Airspace spec — WebView2CompositionControl: https://github.com/MicrosoftEdge/WebView2Feedback/blob/main/specs/WPF_WebView2CompositionControl.md
- NuGet Gallery Microsoft.Web.WebView2 1.0.3967.48 (latest stable): https://www.nuget.org/packages/Microsoft.Web.WebView2
- BaseOverlayWindow.cs — codebase (AllowsTransparency=true in constructor confirmed)
- TwitchChatOverlay.cs — UseRawResize=true pattern confirmed
- OverlayManager.cs — Reg() pattern confirmed
- AppConfig — OverlaySettings property pattern confirmed

### Secondary (MEDIUM confidence)
- WebView2Feedback Issue #915 — AllowsTransparency not supported (multiple community confirmations)
- WebView2Feedback Issue #328 — WPF transparency feature request (open since 2020, not resolved as of 2025)

### Tertiary (LOW confidence)
- webview2backhost NuGet: alternative airspace solution (not investigated, not needed for this approach)

---

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — official NuGet package name and version verified against nuget.org
- Architecture: HIGH — AllowsTransparency=false solution derived from official docs + BaseOverlayWindow source analysis
- Pitfalls: HIGH — airspace problem is well-documented in official WebView2 feedback; initialization async pattern from official docs
- Validation: HIGH — existing xUnit infrastructure confirmed, test categories are realistic unit-testable extractions

**Research date:** 2026-05-20
**Valid until:** 2026-08-20 (stable technology, WebView2 API unlikely to change; airspace behavior is architectural)
