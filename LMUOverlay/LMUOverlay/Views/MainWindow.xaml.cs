using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using LMUOverlay.Models;
using LMUOverlay.Services;
using LMUOverlay.Views.Overlays;

namespace LMUOverlay.Views
{
    public partial class MainWindow : Window
    {
        private readonly AppConfig _config;
        private readonly ConfigService _configService;
        private readonly OverlayManager _overlayManager;
        private readonly ProfileService _profileService = new();
        private readonly CsvExportService _csvExportService = new();
        private bool _isLocked;

        private readonly List<(string Key, string Name, OverlaySettings Settings)> _allOverlays;
        private HotkeyService? _hotkeyService;

        public MainWindow()
        {
            _configService = new ConfigService();
            _config = _configService.Load();
            InitializeComponent();

            // Set window icon from embedded resource
            try
            {
                var iconUri = new Uri("pack://application:,,,/Resources/app.ico", UriKind.Absolute);
                Icon = new System.Windows.Media.Imaging.BitmapImage(iconUri);
            }
            catch { /* icon not critical */ }

            _overlayManager = new OverlayManager(_config);
            _overlayManager.ConnectionChanged += OnConnectionChanged;

            _allOverlays = new()
            {
                ("ProximityRadar",      "RADAR",        _config.ProximityRadar),
                ("StandingsOverall",    "STANDINGS",    _config.StandingsOverall),
                ("StandingsRelative",   "RELATIVE",     _config.StandingsRelative),
                ("TrackMap",            "TRACKMAP",     _config.TrackMap),
                ("InputGraph",          "INPUTS",       _config.InputGraph),
                ("GapTimer",            "GAP",          _config.GapTimer),
                ("RelativeAheadBehind", "AHEAD/BEHIND", _config.RelativeAheadBehind),
                ("Weather",             "WEATHER",      _config.Weather),
                ("Flags",               "FLAG",         _config.Flags),
                ("TireInfo",            "TYREAPP",      _config.TireInfo),
                ("FuelStrategy",         "STRATÉGIE",    _config.FuelInfo),
                ("DeltaTime",           "DELTA",        _config.DeltaTime),
                ("Damage",              "DAMAGE",       _config.Damage),
                ("LapHistory",          "LAPTIMELOG",   _config.LapHistory),
                ("LapGraph",            "LAPTIMEGRAPH", _config.LapGraph),
                ("GForce",              "GFORCE",       _config.GForce),
                ("Dashboard",           "DASHBOARD",    _config.Dashboard),
                ("BlindSpot",           "ANGLES MORTS", _config.BlindSpot),
                ("Rejoin",              "RETOUR PISTE", _config.Rejoin),
                ("Note",               "NOTE",          _config.Note),
                ("Compteur",           "COMPTEUR",      _config.Compteur),
            };

            BuildSidebar();
            UpdateConnectionUI(false);
            _overlayManager.Initialize();
            VoicePanel.Initialize(_config.General, _overlayManager.VoiceService,
                                  _configService, _config);
            ClassementPanel.Initialize(_config.General, new ClassementService());
            _hotkeyService = new HotkeyService(_overlayManager);
            _hotkeyService.Initialize(_allOverlays.Select(o => (o.Key, o.Settings)));
            UpdateHideMenusButton();   // initialise le libellé ON/OFF depuis la config

            if (_allOverlays.Count > 0)
                SelectOverlay(_allOverlays[0].Key);

            // Afficher la version courante (lire AssemblyInformationalVersion = tag <Version> du .csproj)
            var ver = Assembly.GetExecutingAssembly()
                .GetCustomAttribute<System.Reflection.AssemblyInformationalVersionAttribute>()
                ?.InformationalVersion?.Split('+')[0] ?? "1.0.0";
            VersionText.Text = $"v{ver}";

            // Vérifier les mises à jour 5 secondes après le démarrage (silencieux)
            var updateTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
            updateTimer.Tick += (s, e) =>
            {
                updateTimer.Stop();
                UpdateService.CheckForUpdates(silent: true);
            };
            updateTimer.Start();
        }

        private void OnCheckUpdates(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            UpdateService.CheckForUpdates(silent: false);
        }

        // ================================================================
        // SIDEBAR — Overlay list
        // ================================================================

        // Flat toggle helper — uses new accent green palette
        private static readonly SolidColorBrush _btnActive   = new(Color.FromRgb(22, 101, 52));   // AccentGreenDark
        private static readonly SolidColorBrush _btnInactive = new(Color.FromRgb(28, 28, 28));    // BgButton
        private static readonly SolidColorBrush _fgActive    = new(Color.FromRgb(34, 197, 94));   // AccentGreen
        private static readonly SolidColorBrush _fgInactive  = new(Color.FromRgb(163, 163, 163)); // TextSecondary

        private static void SetActive(Button btn, bool active)
        {
            btn.Background = active ? _btnActive   : _btnInactive;
            btn.Foreground = active ? _fgActive    : _fgInactive;
        }

        private void BuildSidebar()
        {
            foreach (var (key, name, settings) in _allOverlays)
            {
                var row = new Grid
                {
                    Cursor = System.Windows.Input.Cursors.Hand,
                    Background = Brushes.Transparent
                };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(32) });
                row.ColumnDefinitions.Add(new ColumnDefinition());

                // Checkbox
                var cb = new CheckBox
                {
                    IsChecked = settings.IsEnabled,
                    Style = (Style)FindResource("SidebarCheckBox"),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 0, 0, 0)
                };
                string capturedKey = key;
                cb.Checked   += (s, e) => { settings.IsEnabled = true;  _overlayManager.RefreshOverlayVisibility(); UpdateFooter(); };
                cb.Unchecked += (s, e) => { settings.IsEnabled = false; _overlayManager.RefreshOverlayVisibility(); UpdateFooter(); };
                settings.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName == nameof(OverlaySettings.IsEnabled))
                        cb.IsChecked = settings.IsEnabled;
                };
                Grid.SetColumn(cb, 0);
                row.Children.Add(cb);

                // Label
                var label = new TextBlock
                {
                    Text = name,
                    FontSize = 10, FontWeight = FontWeights.SemiBold,
                    FontFamily = new FontFamily("Segoe UI Semibold"),
                    Foreground = new SolidColorBrush(settings.IsEnabled ? Color.FromRgb(220, 220, 220) : Color.FromRgb(82, 82, 82)),
                    VerticalAlignment = VerticalAlignment.Center,
                    Padding = new Thickness(4, 8, 4, 8)
                };
                settings.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName == nameof(OverlaySettings.IsEnabled))
                        label.Foreground = new SolidColorBrush(settings.IsEnabled ? Color.FromRgb(220, 220, 220) : Color.FromRgb(82, 82, 82));
                };
                label.MouseLeftButtonDown += (s, e) => SelectOverlay(capturedKey);
                row.MouseLeftButtonDown   += (s, e) => SelectOverlay(capturedKey);

                Grid.SetColumn(label, 1);
                row.Children.Add(label);
                SidebarPanel.Children.Add(row);

                // Separator
                SidebarPanel.Children.Add(new Border
                {
                    Height = 1,
                    Background = new SolidColorBrush(Color.FromRgb(22, 22, 22)),
                    Margin = new Thickness(0)
                });
            }
        }

        // ================================================================
        // RIGHT PANEL — Settings for selected overlay
        // ================================================================

        private void SelectOverlay(string key)
        {
            SettingsPanel.Children.Clear();
            var entry = _allOverlays.Find(o => o.Key == key);
            if (entry == default) return;
            var s = entry.Settings;

            // Highlight sidebar
            // Each overlay = 2 children (Grid row + separator), so stride = 2
            for (int i = 0; i < _allOverlays.Count; i++)
            {
                int childIdx = i * 2;
                if (childIdx < SidebarPanel.Children.Count && SidebarPanel.Children[childIdx] is Grid g)
                    g.Background = _allOverlays[i].Key == key
                        ? new SolidColorBrush(Color.FromArgb(50, 34, 197, 94))
                        : Brushes.Transparent;
            }

            // Title
            Add(new TextBlock
            {
                Text = entry.Name, FontSize = 16, FontWeight = FontWeights.Bold,
                FontFamily = new FontFamily("Segoe UI Semibold"),
                Foreground = B(245, 245, 245), Margin = new Thickness(0, 0, 0, 2)
            });
            Add(new Border { Height = 2, Background = new SolidColorBrush(Color.FromRgb(34, 197, 94)), Margin = new Thickness(0, 0, 0, 14), HorizontalAlignment = HorizontalAlignment.Left, Width = 32 });

            // Enabled
            AddToggle("Enabled", s.IsEnabled, v => { s.IsEnabled = v; _overlayManager.RefreshOverlayVisibility(); });
            AddSep();

            // Opacity
            AddSlider("Opacité", s.Opacity, 0.1, 1.0, v => s.Opacity = v, "P0");

            // Scale
            AddSlider("Taille", s.Scale, 0.5, 3.0, v => s.Scale = v, "F2");
            AddSep();

            // Position
            var posRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 4) };
            posRow.Children.Add(MakeBtn("Center", () => { s.PosX = SystemParameters.PrimaryScreenWidth / 2; s.PosY = SystemParameters.PrimaryScreenHeight / 2; }));
            posRow.Children.Add(MakeBtn("Reset Pos", () => { s.PosX = 100; s.PosY = 100; }));
            posRow.Children.Add(MakeBtn("Reset Size", () => { s.OverlayWidth = 0; s.OverlayHeight = 0; }));
            Add(posRow);

            // Arrow buttons for fine positioning
            Add(MakeArrowButtons(s));

            // Screen selector
            Add(MakeScreenSelector(s));

            // Lock
            AddToggle("Lock Position", s.IsLocked, v => s.IsLocked = v);
            AddSep();

            // Hotkey
            AddHotkeyCapture(s, () =>
            {
                _hotkeyService?.RefreshBindings(_allOverlays.Select(o => (o.Key, o.Settings)));
                _configService.Save(_config);
            });

            // ---- Overlay-specific config toggles ----
            if (key == "StandingsRelative")
            {
                AddSep();
                AddSlider("Ahead", _config.RelativeConfig.AheadCount, 1, 15,
                    v => _config.RelativeConfig.AheadCount = (int)v, "F0");
                AddSlider("Behind", _config.RelativeConfig.BehindCount, 1, 15,
                    v => _config.RelativeConfig.BehindCount = (int)v, "F0");
            }
            if (key == "StandingsOverall")
            {
                AddSep();
                AddSlider("My class", _config.StandingsColumns.MaxEntriesPerClass, 3, 30,
                    v => _config.StandingsColumns.MaxEntriesPerClass = (int)v, "F0");
                AddSlider("Other cls", _config.StandingsColumns.OtherClassCount, 1, 10,
                    v => _config.StandingsColumns.OtherClassCount = (int)v, "F0");
                AddToggle("Session info", _config.StandingsColumns.ShowSessionInfo,
                    v => _config.StandingsColumns.ShowSessionInfo = v);
                AddSep();
                AddToggles("STRUCTURE", new (string, string)[]
                {
                    ("Driver",    "Pilote"),
                    ("CarNumber", "Numéro de course"),
                    ("CarName",   "Nom voiture"),
                    ("ClassBar",  "Barre de classe"),
                    ("PitStops",  "Arrêts pit"),
                    ("Indicator", "Indicateur pit/flag"),
                }, _config.StandingsColumns);
                AddSep();
                AddToggles("ÉCARTS & TEMPS", new (string, string)[]
                {
                    ("GapToNext",   "Écart devant"),
                    ("GapToLeader", "Écart leader"),
                    ("Delta",       "Delta (dernier - meilleur)"),
                    ("BestLap",     "Meilleur tour"),
                    ("LastLap",     "Dernier tour"),
                }, _config.StandingsColumns);
                AddSep();
                AddToggles("COURSE & RELAIS", new (string, string)[]
                {
                    ("TotalLaps",   "Tours totaux"),
                    ("LapProgress", "Progression %"),
                    ("StintLaps",   "Tours de relais"),
                    ("StintTime",   "Temps de relais"),
                }, _config.StandingsColumns);
                AddSep();
                AddToggles("TECHNIQUE", new (string, string)[]
                {
                    ("TireCompound", "Pneus"),
                    ("SectorStatus", "Secteurs (indicateurs)"),
                    ("Damage",       "Dégâts %"),
                    ("Speed",        "Vitesse"),
                    ("Penalties",    "Pénalités"),
                }, _config.StandingsColumns);
            }
            if (key == "BlindSpot")
            {
                AddSep();
                double curScale = _config.BlindSpot.CustomOptions.TryGetValue("Scale", out var sv) ? Convert.ToDouble(sv) : 1.0;
                double curGap   = _config.BlindSpot.CustomOptions.TryGetValue("Gap",   out var gv) ? Convert.ToDouble(gv) : 10;

                AddSlider("Taille des spots", curScale, 0.4, 4.0, v =>
                {
                    _config.BlindSpot.CustomOptions["Scale"] = v;
                    double g = _config.BlindSpot.CustomOptions.TryGetValue("Gap", out var g2) ? Convert.ToDouble(g2) : 10;
                    _overlayManager.GetOverlay<BlindSpotOverlay>("BlindSpot")?.UpdatePanelLayout(v, g);
                }, "F2");

                AddSlider("Écartement", curGap, 0, 6000, v =>
                {
                    _config.BlindSpot.CustomOptions["Gap"] = v;
                    double sc = _config.BlindSpot.CustomOptions.TryGetValue("Scale", out var s2) ? Convert.ToDouble(s2) : 1.0;
                    _overlayManager.GetOverlay<BlindSpotOverlay>("BlindSpot")?.UpdatePanelLayout(sc, v);
                }, "F0");
            }
            if (key == "TrackMap")
            {
                AddSep();

                double curOutline = _config.TrackMap.CustomOptions.TryGetValue("OutlineThickness", out var otv) ? Convert.ToDouble(otv) : 20.0;
                double curCenter  = _config.TrackMap.CustomOptions.TryGetValue("CenterThickness",  out var ctv) ? Convert.ToDouble(ctv) : 10.0;
                string curOutlineColor = _config.TrackMap.CustomOptions.TryGetValue("OutlineColor", out var ocv) ? ocv?.ToString() ?? "#000000" : "#000000";
                string curCenterColor  = _config.TrackMap.CustomOptions.TryGetValue("CenterColor",  out var ccv) ? ccv?.ToString() ?? "#FFFFFF" : "#FFFFFF";

                TrackMapUpdateColors();

                AddSlider("Épaisseur contour", curOutline, 4, 40, v =>
                {
                    _config.TrackMap.CustomOptions["OutlineThickness"] = v;
                    double c = _config.TrackMap.CustomOptions.TryGetValue("CenterThickness", out var c2) ? Convert.ToDouble(c2) : 10.0;
                    _overlayManager.GetOverlay<TrackMapOverlay>("TrackMap")?.UpdateThickness(v, c);
                }, "F0");

                AddSlider("Épaisseur centre", curCenter, 2, 36, v =>
                {
                    _config.TrackMap.CustomOptions["CenterThickness"] = v;
                    double o = _config.TrackMap.CustomOptions.TryGetValue("OutlineThickness", out var o2) ? Convert.ToDouble(o2) : 20.0;
                    _overlayManager.GetOverlay<TrackMapOverlay>("TrackMap")?.UpdateThickness(o, v);
                }, "F0");

                AddSep();

                AddColorPicker("Couleur contour", curOutlineColor, v =>
                {
                    _config.TrackMap.CustomOptions["OutlineColor"] = v;
                    TrackMapUpdateColors();
                });
                AddColorPicker("Couleur centre", curCenterColor, v =>
                {
                    _config.TrackMap.CustomOptions["CenterColor"] = v;
                    TrackMapUpdateColors();
                });

                void TrackMapUpdateColors()
                {
                    var overlay = _overlayManager.GetOverlay<TrackMapOverlay>("TrackMap");
                    if (overlay == null) return;
                    string oc = _config.TrackMap.CustomOptions.TryGetValue("OutlineColor", out var o2) ? o2?.ToString() ?? "#000000" : "#000000";
                    string cc = _config.TrackMap.CustomOptions.TryGetValue("CenterColor",  out var c2) ? c2?.ToString() ?? "#FFFFFF" : "#FFFFFF";
                    try
                    {
                        var outlineC = (Color)ColorConverter.ConvertFromString(oc)!;
                        var centerC  = (Color)ColorConverter.ConvertFromString(cc)!;
                        overlay.UpdateColors(outlineC, centerC);
                    }
                    catch { }
                }
            }
            if (key == "Dashboard")
            {
                AddSep();
                AddToggles("DASHBOARD ELEMENTS", DashboardDisplayConfig.AllItems, _config.DashboardConfig);
            }
            if (key == "InputGraph")
            {
                AddSep();
                AddToggles("INPUT ELEMENTS", InputDisplayConfig.AllItems, _config.InputConfig);
                AddSep();

                // Line thickness
                AddSlider("Épaisseur trait", _config.InputConfig.LineThickness, 0.5, 5.0,
                    v => _config.InputConfig.LineThickness = v, "F1");

                // Trail brake alert
                AddToggle("Alerte Trail Brake", _config.InputConfig.TrailBrakeAlert,
                    v => _config.InputConfig.TrailBrakeAlert = v);

                AddSep();
                // Color options
                AddColorPicker("Couleur Gaz", _config.InputConfig.ThrottleColor,
                    v => _config.InputConfig.ThrottleColor = v);
                AddColorPicker("Couleur Frein", _config.InputConfig.BrakeColor,
                    v => _config.InputConfig.BrakeColor = v);
                AddColorPicker("Couleur Embrayage", _config.InputConfig.ClutchColor,
                    v => _config.InputConfig.ClutchColor = v);
                AddColorPicker("Couleur Direction", _config.InputConfig.SteeringColor,
                    v => _config.InputConfig.SteeringColor = v);
            }
            if (key == "ProximityRadar")
            {
                AddSep();

                bool curColorClass = _config.ProximityRadar.CustomOptions.TryGetValue("ColorByClass",  out var cbv) && Convert.ToBoolean(cbv);
                bool curShowPos    = !_config.ProximityRadar.CustomOptions.TryGetValue("ShowPosition", out var spv) || Convert.ToBoolean(spv);

                AddToggle("Couleur par classe", curColorClass, v =>
                {
                    _config.ProximityRadar.CustomOptions["ColorByClass"] = v;
                });

                AddToggle("Afficher position", curShowPos, v =>
                {
                    _config.ProximityRadar.CustomOptions["ShowPosition"] = v;
                });
            }
        }

        // ================================================================
        // UI BUILDERS
        // ================================================================

        private void Add(UIElement el) => SettingsPanel.Children.Add(el);
        private void AddSep() => Add(new Border { Height = 1, Background = B(30, 30, 30), Margin = new Thickness(0, 8, 0, 8) });

        private void AddSlider(string label, double val, double min, double max, Action<double> set, string fmt)
        {
            var r = new Grid { Margin = new Thickness(0, 4, 0, 4) };
            r.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) });
            r.ColumnDefinitions.Add(new ColumnDefinition());
            r.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(50) });

            r.Children.Add(new TextBlock { Text = label, FontSize = 11, FontFamily = new FontFamily("Segoe UI"), Foreground = B(163, 163, 163), VerticalAlignment = VerticalAlignment.Center });

            var vt = new TextBlock { Text = fmt == "P0" ? $"{val * 100:F0}%" : val.ToString(fmt), FontSize = 11, FontFamily = new FontFamily("Consolas"), Foreground = B(34, 197, 94), HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(vt, 2); r.Children.Add(vt);

            var sl = new Slider { Minimum = min, Maximum = max, Value = val, Style = (Style)FindResource("ModernSlider"), VerticalAlignment = VerticalAlignment.Center };
            sl.ValueChanged += (s, e) => { set(e.NewValue); vt.Text = fmt == "P0" ? $"{e.NewValue * 100:F0}%" : e.NewValue.ToString(fmt); };
            Grid.SetColumn(sl, 1); r.Children.Add(sl);
            Add(r);
        }

        private void AddToggle(string label, bool val, Action<bool> set)
        {
            var r = new Grid { Margin = new Thickness(0, 4, 0, 4) };
            r.ColumnDefinitions.Add(new ColumnDefinition());
            r.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            r.Children.Add(new TextBlock { Text = label, FontSize = 11, FontFamily = new FontFamily("Segoe UI"), Foreground = B(163, 163, 163), VerticalAlignment = VerticalAlignment.Center });
            var t = new CheckBox { IsChecked = val, Style = (Style)FindResource("ToggleSwitchStyle") };
            t.Checked += (s, e) => set(true); t.Unchecked += (s, e) => set(false);
            Grid.SetColumn(t, 1); r.Children.Add(t);
            Add(r);
        }

        private void AddColorPicker(string label, string currentHex, Action<string> set)
        {
            var r = new Grid { Margin = new Thickness(0, 2, 0, 2) };
            r.ColumnDefinitions.Add(new ColumnDefinition());
            r.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            r.Children.Add(new TextBlock
            {
                Text = label, FontSize = 10,
                FontFamily = new FontFamily("Consolas"),
                Foreground = B(150, 180, 180),
                VerticalAlignment = VerticalAlignment.Center
            });

            var colorsPanel = new StackPanel { Orientation = Orientation.Horizontal };
            string[] presets = { "#4CD964", "#FF3B30", "#007AFF", "#FF9500", "#FFCC00", "#AF52DE", "#FFFFFF", "#5AC8FA" };

            foreach (var hex in presets)
            {
                var color = InputDisplayConfig.ParseColor(hex);
                var swatch = new Border
                {
                    Width = 18, Height = 18,
                    CornerRadius = new CornerRadius(3),
                    Background = new SolidColorBrush(color),
                    Margin = new Thickness(1),
                    Cursor = System.Windows.Input.Cursors.Hand,
                    BorderThickness = new Thickness(hex.Equals(currentHex, StringComparison.OrdinalIgnoreCase) ? 2 : 0),
                    BorderBrush = Brushes.White
                };

                string capturedHex = hex;
                swatch.MouseLeftButtonDown += (s, e) =>
                {
                    set(capturedHex);
                    // Update all swatches borders
                    foreach (var child in colorsPanel.Children)
                    {
                        if (child is Border b)
                        {
                            b.BorderThickness = new Thickness(0);
                        }
                    }
                    ((Border)s).BorderThickness = new Thickness(2);
                };
                colorsPanel.Children.Add(swatch);
            }

            Grid.SetColumn(colorsPanel, 1);
            r.Children.Add(colorsPanel);
            Add(r);
        }

        /// <summary>
        /// Adds a hotkey capture row in the overlay settings panel.
        /// Shows the current binding (or "Non assigné") + CAPTURER + ✕ buttons.
        /// </summary>
        private void AddHotkeyCapture(OverlaySettings settings, Action onChanged)
        {
            var row = new Grid { Margin = new Thickness(0, 4, 0, 4) };
            row.ColumnDefinitions.Add(new ColumnDefinition());                         // label
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // binding display
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // clear btn
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // capture btn

            row.Children.Add(new TextBlock
            {
                Text = "Raccourci", FontSize = 11,
                FontFamily = new FontFamily("Segoe UI"),
                Foreground = B(163, 163, 163),
                VerticalAlignment = VerticalAlignment.Center
            });

            // Current binding label
            var bindingLabel = new TextBlock
            {
                Text              = settings.Hotkey.IsEmpty ? "Non assigné" : settings.Hotkey.Display,
                FontSize          = 10,
                FontFamily        = new FontFamily("Consolas"),
                Foreground        = settings.Hotkey.IsEmpty ? B(82, 82, 82) : B(34, 197, 94),
                VerticalAlignment = VerticalAlignment.Center,
                Margin            = new Thickness(0, 0, 6, 0),
            };
            Grid.SetColumn(bindingLabel, 1);
            row.Children.Add(bindingLabel);

            // Clear (✕) button
            var clearBtn = new Button
            {
                Content         = "✕",
                FontSize        = 9,
                Padding         = new Thickness(5, 3, 5, 3),
                Margin          = new Thickness(0, 0, 4, 0),
                Visibility      = settings.Hotkey.IsEmpty ? Visibility.Collapsed : Visibility.Visible,
                Style           = (Style)FindResource("FlatToggle"),
                Foreground      = B(180, 60, 60),
            };
            Grid.SetColumn(clearBtn, 2);
            row.Children.Add(clearBtn);

            // Capture button
            var captureBtn = new Button
            {
                Content  = "CAPTURER",
                FontSize = 9,
                Padding  = new Thickness(10, 3, 10, 3),
                Style    = (Style)FindResource("FlatToggle"),
            };
            Grid.SetColumn(captureBtn, 3);
            row.Children.Add(captureBtn);

            // ── Capture flow ──
            captureBtn.Click += (_, _) =>
            {
                captureBtn.Content    = "En attente…";
                captureBtn.Foreground = B(34, 197, 94);
                captureBtn.IsEnabled  = false;

                _hotkeyService?.StartCapture(binding =>
                {
                    // UI update on dispatcher (callback already on UI thread via Dispatcher.Invoke in service)
                    settings.Hotkey = binding;

                    bindingLabel.Text       = binding.Display;
                    bindingLabel.Foreground = B(34, 197, 94);
                    clearBtn.Visibility     = Visibility.Visible;

                    captureBtn.Content    = "CAPTURER";
                    captureBtn.Foreground = B(163, 163, 163);
                    captureBtn.IsEnabled  = true;

                    onChanged();
                });
            };

            // ── Clear flow ──
            clearBtn.Click += (_, _) =>
            {
                _hotkeyService?.CancelCapture();
                settings.Hotkey = new Models.HotkeyBinding();

                bindingLabel.Text       = "Non assigné";
                bindingLabel.Foreground = B(82, 82, 82);
                clearBtn.Visibility     = Visibility.Collapsed;

                captureBtn.Content    = "CAPTURER";
                captureBtn.Foreground = B(163, 163, 163);
                captureBtn.IsEnabled  = true;

                onChanged();
            };

            Add(row);
        }

        private void AddToggles(string title, (string Key, string Label)[] items, object cfg)
        {
            Add(new TextBlock { Text = title, FontSize = 10, FontWeight = FontWeights.SemiBold, FontFamily = new FontFamily("Segoe UI Semibold"), Foreground = B(82, 82, 82), Margin = new Thickness(0, 4, 0, 6) });
            var ct = cfg.GetType();
            foreach (var (key, label) in items)
            {
                var prop = ct.GetProperty(key);
                bool on = prop != null && (bool)(prop.GetValue(cfg) ?? false);
                var r = new Grid { Margin = new Thickness(0, 3, 0, 3) };
                r.ColumnDefinitions.Add(new ColumnDefinition());
                r.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                r.Children.Add(new TextBlock { Text = label, FontSize = 11, FontFamily = new FontFamily("Segoe UI"), Foreground = B(163, 163, 163), VerticalAlignment = VerticalAlignment.Center });
                var t = new CheckBox { IsChecked = on, Style = (Style)FindResource("ToggleSwitchStyle") };
                string ck = key;
                t.Checked += (s, e) => ct.GetProperty(ck)?.SetValue(cfg, true);
                t.Unchecked += (s, e) => ct.GetProperty(ck)?.SetValue(cfg, false);
                Grid.SetColumn(t, 1); r.Children.Add(t);
                Add(r);
            }
        }

        private Button MakeBtn(string label, Action click)
        {
            var b = new Button { Content = label, Style = (Style)FindResource("SecondaryButton"), Margin = new Thickness(0, 0, 6, 0), FontSize = 10 };
            b.Click += (s, e) => click();
            return b;
        }

        private UIElement MakeArrowButtons(OverlaySettings s)
        {
            double step = 10;

            // ── Arrow grid ────────────────────────────────────────────────────
            var grid = new Grid { HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 2, 0, 2) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(36) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(36) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(36) });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(28) });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(28) });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(28) });

            var up    = ArrowBtn("▲", () => s.PosY -= step);
            var left  = ArrowBtn("◄", () => s.PosX -= step);
            var right = ArrowBtn("►", () => s.PosX += step);
            var down  = ArrowBtn("▼", () => s.PosY += step);

            Grid.SetColumn(up,    1); Grid.SetRow(up,    0); grid.Children.Add(up);
            Grid.SetColumn(left,  0); Grid.SetRow(left,  1); grid.Children.Add(left);
            Grid.SetColumn(right, 2); Grid.SetRow(right, 1); grid.Children.Add(right);
            Grid.SetColumn(down,  1); Grid.SetRow(down,  2); grid.Children.Add(down);

            // ── Step slider ───────────────────────────────────────────────────
            var stepRow = new Grid { Margin = new Thickness(0, 2, 0, 4) };
            stepRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) });
            stepRow.ColumnDefinitions.Add(new ColumnDefinition());
            stepRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(50) });

            stepRow.Children.Add(new TextBlock
            {
                Text = "Pas (px)", FontSize = 11, FontFamily = new FontFamily("Consolas"),
                Foreground = B(150, 180, 180), VerticalAlignment = VerticalAlignment.Center
            });

            var stepVal = new TextBlock
            {
                Text = "10", FontSize = 11, FontFamily = new FontFamily("Consolas"),
                Foreground = B(76, 217, 100),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(stepVal, 2);
            stepRow.Children.Add(stepVal);

            var stepSlider = new Slider
            {
                Minimum = 1, Maximum = 500, Value = 10,
                Style = (Style)FindResource("ModernSlider"),
                VerticalAlignment = VerticalAlignment.Center
            };
            stepSlider.ValueChanged += (_, e) =>
            {
                step = Math.Round(e.NewValue);
                stepVal.Text = ((int)step).ToString();
            };
            Grid.SetColumn(stepSlider, 1);
            stepRow.Children.Add(stepSlider);

            // ── Wrapper ───────────────────────────────────────────────────────
            var wrapper = new StackPanel { Orientation = Orientation.Vertical };
            wrapper.Children.Add(grid);
            wrapper.Children.Add(stepRow);
            return wrapper;
        }

        private Button ArrowBtn(string symbol, Action click)
        {
            var b = new Button
            {
                Content = symbol, FontSize = 14, FontFamily = new FontFamily("Consolas"),
                Width = 32, Height = 28,
                Background = new SolidColorBrush(Color.FromRgb(28, 28, 28)),
                Foreground = B(163, 163, 163),
                BorderBrush = B(42, 42, 42), BorderThickness = new Thickness(1),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Cursor = System.Windows.Input.Cursors.Hand
            };
            b.Click += (s, e) => click();
            return b;
        }

        private Grid MakeScreenSelector(OverlaySettings settings)
        {
            var r = new Grid { Margin = new Thickness(0, 4, 0, 4) };
            r.ColumnDefinitions.Add(new ColumnDefinition());
            r.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            r.Children.Add(new TextBlock { Text = "Écran", FontSize = 11, FontFamily = new FontFamily("Segoe UI"), Foreground = B(163, 163, 163), VerticalAlignment = VerticalAlignment.Center });

            var screens = Helpers.ScreenInfo.GetAllScreens();

            // DPI scale factor: Win32 rcWork returns physical pixels, WPF Left/Top uses DIPs.
            // We must convert: WPF_DIP = physical_px / dpiScale
            double dpiScale = 1.0;
            var ps = PresentationSource.FromVisual(this);
            if (ps?.CompositionTarget != null)
                dpiScale = ps.CompositionTarget.TransformToDevice.M11;

            var combo = new ComboBox { Width = 130, Background = new SolidColorBrush(Color.FromRgb(22, 22, 22)), Foreground = B(220, 220, 220), BorderBrush = B(42, 42, 42) };
            for (int i = 0; i < screens.Count; i++) combo.Items.Add(screens[i].ToString());

            // Restore selection to the screen the overlay is currently on (compare in WPF DIPs)
            int currentScreenIdx = 0;
            for (int i = 0; i < screens.Count; i++)
            {
                double screenLeftDip  = screens[i].Left  / dpiScale;
                double screenRightDip = screenLeftDip + screens[i].Width / dpiScale;
                if (settings.PosX >= screenLeftDip && settings.PosX < screenRightDip)
                {
                    currentScreenIdx = i;
                    break;
                }
            }

            // Set index BEFORE attaching the handler to avoid a spurious position reset
            combo.SelectedIndex = currentScreenIdx;

            combo.SelectionChanged += (s, e) =>
            {
                if (combo.SelectedIndex >= 0 && combo.SelectedIndex < screens.Count)
                {
                    // Convert physical-pixel screen origin to WPF DIPs before writing to settings
                    settings.PosX = screens[combo.SelectedIndex].Left  / dpiScale + 100;
                    settings.PosY = screens[combo.SelectedIndex].Top   / dpiScale + 100;
                }
            };

            Grid.SetColumn(combo, 1); r.Children.Add(combo);
            return r;
        }

        // ================================================================
        // CONNECTION
        // ================================================================

        private void OnConnect(object s, RoutedEventArgs e)
        {
            if (_overlayManager.IsConnected) _overlayManager.Disconnect();
            else _overlayManager.Connect();
        }

        private void OnConnectionChanged(object? s, bool c) => Dispatcher.Invoke(() => UpdateConnectionUI(c));

        private void UpdateConnectionUI(bool c)
        {
            StatusDot.Fill    = c ? B(34, 197, 94) : B(239, 68, 68);
            StatusText.Text   = c ? "Connecté" : "Déconnecté";
            StatusText.Foreground = c ? B(34, 197, 94) : B(163, 163, 163);
            BtnConnect.Content = c ? "DÉCONNECTER" : "CONNECTER";
            ShmDot.Fill       = c ? B(34, 197, 94) : B(239, 68, 68);

            // FORCER AFFICHAGE : uniquement disponible quand déconnecté
            BtnForceDisplay.IsEnabled = !c;
            if (c)
            {
                // Réinitialiser l'état visuel à OFF lors de la connexion
                BtnForceDisplay.Content = "FORCER : OFF";
                SetActive(BtnForceDisplay, false);
            }

            UpdateFooter();
        }

        private void UpdateFooter()
        {
            int count = _allOverlays.Count(o => o.Settings.IsEnabled);
            ActiveCountText.Text = $"{count}/{_allOverlays.Count} active";
        }

        // ================================================================
        // ACTION BAR
        // ================================================================

        private void OnShowAll(object s, RoutedEventArgs e)
        {
            // Coche toutes les cases — l'affichage réel respecte HideInMenus / connexion
            _overlayManager.ShowAll();
            UpdateFooter();
        }

        private void OnHideAll(object s, RoutedEventArgs e) { _overlayManager.HideAll(); UpdateFooter(); }

        private void OnToggleLock(object s, RoutedEventArgs e)
        {
            _isLocked = !_isLocked;
            _overlayManager.SetAllLocked(_isLocked);
            BtnLock.Content = _isLocked ? "🔓 UNLOCK" : "🔒 LOCK";
            SetActive(BtnLock, _isLocked);
        }

        private void OnToggleHideMenus(object s, RoutedEventArgs e)
        {
            _config.General.HideInMenus = !_config.General.HideInMenus;
            UpdateHideMenusButton();
        }

        private void UpdateHideMenusButton()
        {
            bool on = _config.General.HideInMenus;
            BtnHideMenus.Content = on ? "MENUS: MASQUÉS" : "MASQUER MENUS";
            SetActive(BtnHideMenus, on);
        }

        private void OnToggleForceDisplay(object s, RoutedEventArgs e)
        {
            bool newState = !_overlayManager.ForceDisplay;
            _overlayManager.SetForceDisplay(newState);
            BtnForceDisplay.Content = newState ? "FORCER: ON" : "FORCER : OFF";
            SetActive(BtnForceDisplay, newState);
        }

        private void OnToggleVR(object s, RoutedEventArgs e)
        {
            if (_overlayManager.IsVRActive)
            {
                _overlayManager.StopVR();
                SetActive(BtnVR, false);
            }
            else
            {
                if (_overlayManager.StartVR(out string err)) SetActive(BtnVR, true);
                else MessageBox.Show($"VR Error:\n{err}", "VR", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void OnSave(object s, RoutedEventArgs e)
        {
            _configService.Save(_config);
            string track = _overlayManager.DataService.GetTrackName();
            if (!string.IsNullOrEmpty(track))
                _profileService.SaveProfile(_config, ProfileService.TrackToProfileName(track));

            // Toast confirmation
            SaveStatus.Visibility = Visibility.Visible;
            var timer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(3)
            };
            timer.Tick += (_, _) => { SaveStatus.Visibility = Visibility.Collapsed; timer.Stop(); };
            timer.Start();
        }

        private void OnExport(object s, RoutedEventArgs e)
        {
            var laps = _overlayManager.DataService.GetLapHistory();
            if (laps.Count == 0) { MessageBox.Show("Aucun tour enregistré.", "Export"); return; }
            string path = _csvExportService.ExportLapHistory(laps, _overlayManager.DataService.GetTrackName());
            MessageBox.Show($"Exporté:\n{path}", "Export CSV", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        // ================================================================
        // SECTION TABS: HUD / TELEMETRY
        // ================================================================

        private static readonly SolidColorBrush _tabUnderlineActive   = new(Color.FromRgb(34, 197, 94));
        private static readonly SolidColorBrush _tabUnderlineInactive = new(Color.FromArgb(0, 0, 0, 0));
        private static readonly SolidColorBrush _tabFgActive          = new(Color.FromRgb(245, 245, 245));
        private static readonly SolidColorBrush _tabFgInactive        = new(Color.FromRgb(82, 82, 82));

        private void ShowTab(string tab)
        {
            HudGrid.Visibility         = tab == "hud"         ? Visibility.Visible : Visibility.Collapsed;
            TelPanel.Visibility        = tab == "telemetry"   ? Visibility.Visible : Visibility.Collapsed;
            VoicePanel.Visibility      = tab == "audio"       ? Visibility.Visible : Visibility.Collapsed;
            ChronoPanel.Visibility     = tab == "chrono"      ? Visibility.Visible : Visibility.Collapsed;
            ClassementPanel.Visibility = tab == "leaderboard" ? Visibility.Visible : Visibility.Collapsed;
            HudActions.Visibility      = tab == "hud"         ? Visibility.Visible : Visibility.Collapsed;

            TabHud.BorderBrush          = tab == "hud"         ? _tabUnderlineActive : _tabUnderlineInactive;
            TabHud.Foreground           = tab == "hud"         ? _tabFgActive        : _tabFgInactive;
            TabTelemetry.BorderBrush    = tab == "telemetry"   ? _tabUnderlineActive : _tabUnderlineInactive;
            TabTelemetry.Foreground     = tab == "telemetry"   ? _tabFgActive        : _tabFgInactive;
            TabAudio.BorderBrush        = tab == "audio"       ? _tabUnderlineActive : _tabUnderlineInactive;
            TabAudio.Foreground         = tab == "audio"       ? _tabFgActive        : _tabFgInactive;
            TabChrono.BorderBrush       = tab == "chrono"      ? _tabUnderlineActive : _tabUnderlineInactive;
            TabChrono.Foreground        = tab == "chrono"      ? _tabFgActive        : _tabFgInactive;
            TabLeaderboard.BorderBrush  = tab == "leaderboard" ? _tabUnderlineActive : _tabUnderlineInactive;
            TabLeaderboard.Foreground   = tab == "leaderboard" ? _tabFgActive        : _tabFgInactive;
        }

        private void OnTabAudio(object s, RoutedEventArgs e) => ShowTab("audio");

        private bool _chronoInitialized;
        private void OnTabChrono(object s, RoutedEventArgs e)
        {
            ShowTab("chrono");
            ChronoPanel.Initialize(_config.Chrono);
            if (!_chronoInitialized)
            {
                _chronoInitialized = true;
                ChronoPanel.SettingsChanged += () => _configService.Save(_config);
            }
        }

        private void OnTabLeaderboard(object s, RoutedEventArgs e)
        {
            ShowTab("leaderboard");
            _ = ClassementPanel.LoadAsync();
        }

        private void OnTabHud(object s, RoutedEventArgs e) => ShowTab("hud");

        private void OnTabTelemetry(object s, RoutedEventArgs e)
        {
            ShowTab("telemetry");
            string track = _overlayManager.DataService.GetTrackName();
            TelPanel.Refresh(_overlayManager.DataService, track);
        }


        // ================================================================
        // CLOSE
        // ================================================================

        protected override void OnClosed(EventArgs e)
        {
            _hotkeyService?.Dispose();
            _configService.Save(_config);
            _overlayManager.Dispose();
            base.OnClosed(e);
        }

        private static SolidColorBrush B(byte r, byte g, byte b) => new(Color.FromRgb(r, g, b));
    }
}
