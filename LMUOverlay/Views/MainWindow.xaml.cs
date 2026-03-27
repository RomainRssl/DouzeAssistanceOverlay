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
                ("TrackLimits",         "TRACKLIMITS",  _config.TrackLimits),
                ("BlindSpot",           "ANGLES MORTS", _config.BlindSpot),
                ("Rejoin",              "RETOUR PISTE", _config.Rejoin),
            };

            BuildSidebar();
            UpdateConnectionUI(false);
            _overlayManager.Initialize();

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

        // Flat toggle helper
        private static readonly SolidColorBrush _btnActive   = new(Color.FromRgb(46, 90, 46));
        private static readonly SolidColorBrush _btnInactive = new(Color.FromRgb(42, 42, 42));
        private static readonly SolidColorBrush _fgActive    = new(Color.FromRgb(255, 255, 255));
        private static readonly SolidColorBrush _fgInactive  = new(Color.FromRgb(200, 200, 200));

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
                    Foreground = new SolidColorBrush(settings.IsEnabled ? Color.FromRgb(210, 210, 210) : Color.FromRgb(100, 100, 100)),
                    VerticalAlignment = VerticalAlignment.Center,
                    Padding = new Thickness(4, 6, 4, 6)
                };
                settings.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName == nameof(OverlaySettings.IsEnabled))
                        label.Foreground = new SolidColorBrush(settings.IsEnabled ? Color.FromRgb(210, 210, 210) : Color.FromRgb(100, 100, 100));
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
                    Background = new SolidColorBrush(Color.FromRgb(38, 38, 38)),
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
                        ? new SolidColorBrush(Color.FromArgb(60, 46, 90, 46))
                        : Brushes.Transparent;
            }

            // Title
            Add(new TextBlock
            {
                Text = entry.Name, FontSize = 18, FontWeight = FontWeights.Bold,
                FontFamily = new FontFamily("Consolas"),
                Foreground = B(230, 240, 240), Margin = new Thickness(0, 0, 0, 8)
            });

            // Enabled
            AddToggle("Enabled", s.IsEnabled, v => { s.IsEnabled = v; _overlayManager.RefreshOverlayVisibility(); });
            AddSep();

            // Opacity
            AddSlider("Opacité", s.Opacity, 0.1, 1.0, v => s.Opacity = v, "P0");
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
        }

        // ================================================================
        // UI BUILDERS
        // ================================================================

        private void Add(UIElement el) => SettingsPanel.Children.Add(el);
        private void AddSep() => Add(new Border { Height = 1, Background = B(36, 68, 68), Margin = new Thickness(0, 6, 0, 6) });

        private void AddSlider(string label, double val, double min, double max, Action<double> set, string fmt)
        {
            var r = new Grid { Margin = new Thickness(0, 2, 0, 2) };
            r.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) });
            r.ColumnDefinitions.Add(new ColumnDefinition());
            r.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(50) });

            r.Children.Add(new TextBlock { Text = label, FontSize = 11, FontFamily = new FontFamily("Consolas"), Foreground = B(150, 180, 180), VerticalAlignment = VerticalAlignment.Center });

            var vt = new TextBlock { Text = fmt == "P0" ? $"{val * 100:F0}%" : val.ToString(fmt), FontSize = 11, FontFamily = new FontFamily("Consolas"), Foreground = B(76, 217, 100), HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(vt, 2); r.Children.Add(vt);

            var sl = new Slider { Minimum = min, Maximum = max, Value = val, Style = (Style)FindResource("ModernSlider"), VerticalAlignment = VerticalAlignment.Center };
            sl.ValueChanged += (s, e) => { set(e.NewValue); vt.Text = fmt == "P0" ? $"{e.NewValue * 100:F0}%" : e.NewValue.ToString(fmt); };
            Grid.SetColumn(sl, 1); r.Children.Add(sl);
            Add(r);
        }

        private void AddToggle(string label, bool val, Action<bool> set)
        {
            var r = new Grid { Margin = new Thickness(0, 2, 0, 2) };
            r.ColumnDefinitions.Add(new ColumnDefinition());
            r.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            r.Children.Add(new TextBlock { Text = label, FontSize = 11, FontFamily = new FontFamily("Consolas"), Foreground = B(150, 180, 180), VerticalAlignment = VerticalAlignment.Center });
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

        private void AddToggles(string title, (string Key, string Label)[] items, object cfg)
        {
            Add(new TextBlock { Text = title, FontSize = 12, FontWeight = FontWeights.Bold, FontFamily = new FontFamily("Consolas"), Foreground = B(200, 220, 220), Margin = new Thickness(0, 0, 0, 4) });
            var ct = cfg.GetType();
            foreach (var (key, label) in items)
            {
                var prop = ct.GetProperty(key);
                bool on = prop != null && (bool)(prop.GetValue(cfg) ?? false);
                var r = new Grid { Margin = new Thickness(0, 1, 0, 1) };
                r.ColumnDefinitions.Add(new ColumnDefinition());
                r.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                r.Children.Add(new TextBlock { Text = label, FontSize = 10, FontFamily = new FontFamily("Consolas"), Foreground = B(130, 160, 160), VerticalAlignment = VerticalAlignment.Center });
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

        private Grid MakeArrowButtons(OverlaySettings s)
        {
            const double step = 10;
            var grid = new Grid { HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 2, 0, 4) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(36) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(36) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(36) });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(28) });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(28) });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(28) });

            var up = ArrowBtn("▲", () => s.PosY -= step);
            Grid.SetColumn(up, 1); Grid.SetRow(up, 0);
            grid.Children.Add(up);

            var left = ArrowBtn("◄", () => s.PosX -= step);
            Grid.SetColumn(left, 0); Grid.SetRow(left, 1);
            grid.Children.Add(left);

            var right = ArrowBtn("►", () => s.PosX += step);
            Grid.SetColumn(right, 2); Grid.SetRow(right, 1);
            grid.Children.Add(right);

            var down = ArrowBtn("▼", () => s.PosY += step);
            Grid.SetColumn(down, 1); Grid.SetRow(down, 2);
            grid.Children.Add(down);

            return grid;
        }

        private Button ArrowBtn(string symbol, Action click)
        {
            var b = new Button
            {
                Content = symbol, FontSize = 14, FontFamily = new FontFamily("Consolas"),
                Width = 32, Height = 26,
                Background = new SolidColorBrush(Color.FromRgb(24, 52, 55)),
                Foreground = B(200, 220, 220),
                BorderBrush = B(36, 68, 68), BorderThickness = new Thickness(1),
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
            r.Children.Add(new TextBlock { Text = "Screen", FontSize = 11, FontFamily = new FontFamily("Consolas"), Foreground = B(150, 180, 180), VerticalAlignment = VerticalAlignment.Center });
            var screens = Helpers.ScreenInfo.GetAllScreens();
            var combo = new ComboBox { Width = 130, Background = new SolidColorBrush(Color.FromRgb(24, 52, 55)), Foreground = B(200, 200, 200), BorderBrush = B(36, 68, 68) };
            for (int i = 0; i < screens.Count; i++) combo.Items.Add(screens[i].ToString());
            combo.SelectedIndex = 0;
            combo.SelectionChanged += (s, e) =>
            {
                if (combo.SelectedIndex >= 0 && combo.SelectedIndex < screens.Count)
                {
                    settings.PosX = screens[combo.SelectedIndex].Left + 100;
                    settings.PosY = screens[combo.SelectedIndex].Top + 100;
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
            StatusDot.Fill = c ? B(76, 217, 100) : B(255, 59, 48);
            StatusText.Text = c ? "Connected" : "Disconnected";
            StatusText.Foreground = c ? B(76, 217, 100) : B(255, 59, 48);
            BtnConnect.Content = c ? "DISCONNECT" : "CONNECT";
            ShmDot.Fill = c ? B(76, 217, 100) : B(255, 59, 48);
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
            if (_config.General.HideInMenus && !_overlayManager.IsOnTrack())
            {
                var result = MessageBox.Show(
                    "Le mode \"Masquer dans les menus\" est activé.\n" +
                    "Les overlays sont actuellement masqués car vous n'êtes pas en piste.\n\n" +
                    "Voulez-vous forcer l'affichage ?",
                    "Douze Assistance",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    _config.General.HideInMenus = false;
                    BtnHideMenus.Content = "📺 MASQUER MENUS: OFF";
                    _overlayManager.ShowAll();
                }
            }
            else
            {
                _overlayManager.ShowAll();
            }
            UpdateFooter();
        }
        private void OnHideAll(object s, RoutedEventArgs e) { _overlayManager.HideAll(); UpdateFooter(); }

        private void OnToggleLock(object s, RoutedEventArgs e)
        {
            _isLocked = !_isLocked;
            _overlayManager.SetAllLocked(_isLocked);
            BtnLock.Content = _isLocked ? "UNLOCK HUD" : "LOCK HUD";
            SetActive(BtnLock, _isLocked);
        }

        private void OnToggleHideMenus(object s, RoutedEventArgs e)
        {
            _config.General.HideInMenus = !_config.General.HideInMenus;
            SetActive(BtnHideMenus, _config.General.HideInMenus);
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

        private static readonly SolidColorBrush _tabActive   = new(Color.FromRgb(46, 90, 46));
        private static readonly SolidColorBrush _tabInactive = new(Color.FromArgb(0, 0, 0, 0));

        private void OnTabHud(object s, RoutedEventArgs e)
        {
            HudGrid.Visibility    = Visibility.Visible;
            TelPanel.Visibility   = Visibility.Collapsed;
            HudActions.Visibility = Visibility.Visible;
            TabHud.Background       = _tabActive;
            TabHud.Foreground       = Brushes.White;
            TabTelemetry.Background = _tabInactive;
            TabTelemetry.Foreground = new SolidColorBrush(Color.FromRgb(102, 102, 102));
        }

        private void OnTabTelemetry(object s, RoutedEventArgs e)
        {
            HudGrid.Visibility    = Visibility.Collapsed;
            TelPanel.Visibility   = Visibility.Visible;
            HudActions.Visibility = Visibility.Collapsed;
            TabTelemetry.Background = _tabActive;
            TabTelemetry.Foreground = Brushes.White;
            TabHud.Background       = _tabInactive;
            TabHud.Foreground       = new SolidColorBrush(Color.FromRgb(102, 102, 102));

            string track = _overlayManager.DataService.GetTrackName();
            TelPanel.Refresh(_overlayManager.DataService, track);
        }

        // ================================================================
        // CLOSE
        // ================================================================

        protected override void OnClosed(EventArgs e)
        {
            _configService.Save(_config);
            _overlayManager.Dispose();
            base.OnClosed(e);
        }

        private static SolidColorBrush B(byte r, byte g, byte b) => new(Color.FromRgb(r, g, b));
    }
}
