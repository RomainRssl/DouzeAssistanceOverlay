using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using LMUOverlay.Helpers;
using LMUOverlay.Models;
using LMUOverlay.Services;
using Microsoft.Win32;

namespace LMUOverlay.Views
{
    public partial class ThemesTab : UserControl
    {
        private AppConfig?    _config;
        private ConfigService? _configService;

        // Working copy of colour values (modified by pickers, applied on Save)
        private Color _bg, _panelBg, _accentPrimary, _accentSecondary;
        private Color _textPrimary, _textSecondary, _textMuted;
        private Color _stateGood, _stateWarn, _stateDanger, _stateBestLap, _border;
        private Color _classHypercar, _classLmp2, _classLmgt, _classGt3;
        private bool  _barGradient, _accentLine, _alertGlow, _roundedBars;

        private bool _dirty;
        private string? _selectedTheme;
        private DispatcherTimer? _statusTimer;

        // Colour patch buttons — one per colour property
        private readonly Dictionary<string, Border> _patches = new();

        public ThemesTab()
        {
            InitializeComponent();
            ThemeManager.ThemeChanged += RefreshPreview;
        }

        // ================================================================
        // Init
        // ================================================================

        public void Initialize(AppConfig config, ConfigService configService)
        {
            _config        = config;
            _configService = configService;
            RefreshThemeList();
            // Select the active theme
            SelectTheme(config.ActiveThemeName);
        }

        // ================================================================
        // Theme list
        // ================================================================

        private void RefreshThemeList()
        {
            ThemeList.Items.Clear();
            foreach (var t in ThemeManager.GetAvailableThemes())
                ThemeList.Items.Add(t);
        }

        private void OnThemeSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ThemeList.SelectedItem is string name)
            {
                _selectedTheme = name;
                LoadThemeIntoEditor(name);
            }
        }

        private void SelectTheme(string name)
        {
            for (int i = 0; i < ThemeList.Items.Count; i++)
            {
                if (ThemeList.Items[i]?.ToString() == name)
                {
                    ThemeList.SelectedIndex = i;
                    return;
                }
            }
            if (ThemeList.Items.Count > 0)
                ThemeList.SelectedIndex = 0;
        }

        // ================================================================
        // Editor build
        // ================================================================

        private void LoadThemeIntoEditor(string themeName)
        {
            // Load theme into ThemeManager so we can read its values
            ThemeManager.Load(themeName);
            CopyCurrentThemeToWorkingCopy();
            BuildEditor();
            RefreshPreview();
            _dirty = false;
        }

        private void CopyCurrentThemeToWorkingCopy()
        {
            var tm = ThemeManager.Current;
            _bg            = tm.Background;
            _panelBg       = tm.PanelBackground;
            _accentPrimary  = tm.AccentPrimary;
            _accentSecondary= tm.AccentSecondary;
            _textPrimary    = tm.TextPrimary;
            _textSecondary  = tm.TextSecondary;
            _textMuted      = tm.TextMuted;
            _stateGood      = tm.StateGood;
            _stateWarn      = tm.StateWarn;
            _stateDanger    = tm.StateDanger;
            _stateBestLap   = tm.StateBestLap;
            _border         = tm.Border;
            _classHypercar  = tm.ClassHypercar;
            _classLmp2      = tm.ClassLmp2;
            _classLmgt      = tm.ClassLmgt;
            _classGt3       = tm.ClassGt3;
            _barGradient    = tm.BarGradient;
            _accentLine     = tm.AccentLine;
            _alertGlow      = tm.AlertGlow;
            _roundedBars    = tm.RoundedBars;
        }

        private void BuildEditor()
        {
            EditorPanel.Children.Clear();
            _patches.Clear();

            var tm = ThemeManager.Current;

            // Title
            EditorPanel.Children.Add(MakeTitle(tm.Name));
            EditorPanel.Children.Add(MakeSep());

            // Colours section
            EditorPanel.Children.Add(MakeSectionHeader("COULEURS"));
            AddColorRow("Fond global",        "bg",            ref _bg);
            AddColorRow("Fond panel",         "panelBg",       ref _panelBg);
            AddColorRow("Accent principal",   "accentPrimary", ref _accentPrimary);
            AddColorRow("Accent secondaire",  "accentSecondary", ref _accentSecondary);
            EditorPanel.Children.Add(MakeSep());

            EditorPanel.Children.Add(MakeSectionHeader("TEXTE"));
            AddColorRow("Texte principal",    "textPrimary",   ref _textPrimary);
            AddColorRow("Texte secondaire",   "textSecondary", ref _textSecondary);
            AddColorRow("Texte estompé",      "textMuted",     ref _textMuted);
            AddColorRow("Bordure",            "border",        ref _border);
            EditorPanel.Children.Add(MakeSep());

            EditorPanel.Children.Add(MakeSectionHeader("ÉTATS"));
            AddColorRow("Bon / Gain",         "stateGood",     ref _stateGood);
            AddColorRow("Avertissement",      "stateWarn",     ref _stateWarn);
            AddColorRow("Danger",             "stateDanger",   ref _stateDanger);
            AddColorRow("Meilleur tour",      "stateBestLap",  ref _stateBestLap);
            EditorPanel.Children.Add(MakeSep());

            EditorPanel.Children.Add(MakeSectionHeader("CLASSES"));
            AddColorRow("Hypercar / LMH",     "classHypercar", ref _classHypercar);
            AddColorRow("LMP2",               "classLmp2",     ref _classLmp2);
            AddColorRow("LMGT / GTE",         "classLmgt",     ref _classLmgt);
            AddColorRow("GT3",                "classGt3",      ref _classGt3);
            EditorPanel.Children.Add(MakeSep());

            EditorPanel.Children.Add(MakeSectionHeader("EFFETS"));
            AddCheckRow("Dégradé barre",      _barGradient,  v => { _barGradient  = v; MarkDirty(); });
            AddCheckRow("Ligne accent",       _accentLine,   v => { _accentLine   = v; MarkDirty(); });
            AddCheckRow("Lueur alerte",       _alertGlow,    v => { _alertGlow    = v; MarkDirty(); });
            AddCheckRow("Barres arrondies",   _roundedBars,  v => { _roundedBars  = v; MarkDirty(); });
            EditorPanel.Children.Add(new Border { Height = 16 });
        }

        // ================================================================
        // Color picker rows
        // ================================================================

        private void AddColorRow(string label, string key, ref Color color)
        {
            var currentColor = color; // capture
            var patch = new Border
            {
                Width = 22, Height = 22,
                CornerRadius = new CornerRadius(3),
                Background = BrushCache.Get(currentColor),
                BorderBrush = new SolidColorBrush(Color.FromArgb(80, 255, 255, 255)),
                BorderThickness = new Thickness(1),
                Cursor = System.Windows.Input.Cursors.Hand,
                ToolTip = $"Cliquer pour choisir la couleur ({ThemeManager.ColorToHex(currentColor)})"
            };
            _patches[key] = patch;

            var hexText = new TextBlock
            {
                Text = ThemeManager.ColorToHex(currentColor),
                FontFamily = new FontFamily("Consolas"),
                FontSize = 10,
                Foreground = new SolidColorBrush(Color.FromRgb(130, 140, 150)),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(8, 0, 0, 0)
            };

            patch.MouseLeftButtonDown += (_, _) =>
            {
                var dlg = new System.Windows.Forms.ColorDialog
                {
                    Color = System.Drawing.Color.FromArgb(
                        _patches.TryGetValue(key, out var p) && p.Background is SolidColorBrush sb
                            ? sb.Color.R : currentColor.R,
                        _patches.TryGetValue(key, out var p2) && p2.Background is SolidColorBrush sb2
                            ? sb2.Color.G : currentColor.G,
                        _patches.TryGetValue(key, out var p3) && p3.Background is SolidColorBrush sb3
                            ? sb3.Color.B : currentColor.B),
                    FullOpen = true
                };
                if (dlg.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;

                var chosen = Color.FromRgb(dlg.Color.R, dlg.Color.G, dlg.Color.B);
                SetColorField(key, chosen);
                patch.Background  = BrushCache.Get(chosen);
                patch.ToolTip     = $"Cliquer pour choisir ({ThemeManager.ColorToHex(chosen)})";
                hexText.Text = ThemeManager.ColorToHex(chosen);
                MarkDirty();
                ApplyWorkingCopyLive();
            };

            var row = new Grid { Margin = new Thickness(0, 3, 0, 3) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(170) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition());

            var lbl = new TextBlock
            {
                Text = label, FontSize = 11, FontFamily = new FontFamily("Segoe UI"),
                Foreground = new SolidColorBrush(Color.FromRgb(180, 180, 180)),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(lbl,     0); row.Children.Add(lbl);
            Grid.SetColumn(patch,   1); row.Children.Add(patch);
            Grid.SetColumn(hexText, 2); row.Children.Add(hexText);

            EditorPanel.Children.Add(row);
        }

        private void SetColorField(string key, Color c)
        {
            switch (key)
            {
                case "bg":             _bg            = c; break;
                case "panelBg":        _panelBg       = c; break;
                case "accentPrimary":  _accentPrimary  = c; break;
                case "accentSecondary":_accentSecondary= c; break;
                case "textPrimary":    _textPrimary    = c; break;
                case "textSecondary":  _textSecondary  = c; break;
                case "textMuted":      _textMuted      = c; break;
                case "stateGood":      _stateGood      = c; break;
                case "stateWarn":      _stateWarn      = c; break;
                case "stateDanger":    _stateDanger    = c; break;
                case "stateBestLap":   _stateBestLap   = c; break;
                case "border":         _border         = c; break;
                case "classHypercar":  _classHypercar  = c; break;
                case "classLmp2":      _classLmp2      = c; break;
                case "classLmgt":      _classLmgt      = c; break;
                case "classGt3":       _classGt3       = c; break;
            }
        }

        // ================================================================
        // Checkbox rows
        // ================================================================

        private void AddCheckRow(string label, bool value, Action<bool> setter)
        {
            var cb = new CheckBox
            {
                Content = label,
                IsChecked = value,
                FontSize = 11, FontFamily = new FontFamily("Segoe UI"),
                Foreground = new SolidColorBrush(Color.FromRgb(180, 180, 180)),
                Margin = new Thickness(0, 3, 0, 3)
            };
            cb.Checked   += (_, _) => setter(true);
            cb.Unchecked += (_, _) => setter(false);
            EditorPanel.Children.Add(cb);
        }

        // ================================================================
        // Live preview
        // ================================================================

        private void ApplyWorkingCopyLive()
        {
            ThemeManager.ApplyLive(tm =>
            {
                tm.Background       = _bg;
                tm.PanelBackground  = _panelBg;
                tm.AccentPrimary    = _accentPrimary;
                tm.AccentSecondary  = _accentSecondary;
                tm.TextPrimary      = _textPrimary;
                tm.TextSecondary    = _textSecondary;
                tm.TextMuted        = _textMuted;
                tm.StateGood        = _stateGood;
                tm.StateWarn        = _stateWarn;
                tm.StateDanger      = _stateDanger;
                tm.StateBestLap     = _stateBestLap;
                tm.Border           = _border;
                tm.ClassHypercar    = _classHypercar;
                tm.ClassLmp2        = _classLmp2;
                tm.ClassLmgt        = _classLmgt;
                tm.ClassGt3         = _classGt3;
                tm.BarGradient      = _barGradient;
                tm.AccentLine       = _accentLine;
                tm.AlertGlow        = _alertGlow;
                tm.RoundedBars      = _roundedBars;
            });
        }

        private void RefreshPreview()
        {
            var tm = ThemeManager.Current;
            PreviewBorder.Background = BrushCache.Get(tm.PanelBackground);
            PreviewLabel.Foreground  = BrushCache.Get(tm.TextMuted);
            PreviewLabel.FontFamily  = tm.MonoFont;
            PreviewValue.Foreground  = BrushCache.Get(tm.TextPrimary);
            PreviewValue.FontFamily  = tm.MonoFont;
            PreviewBarBg.Background  = BrushCache.Get(tm.Border);
            PreviewBarFill.Background= BrushCache.Get(tm.AccentPrimary);
            PreviewGood.Foreground   = BrushCache.Get(tm.StateGood);
            PreviewWarn.Foreground   = BrushCache.Get(tm.StateWarn);
            PreviewDanger.Foreground = BrushCache.Get(tm.StateDanger);
            PreviewGood.FontFamily   = tm.MonoFont;
            PreviewWarn.FontFamily   = tm.MonoFont;
            PreviewDanger.FontFamily = tm.MonoFont;
        }

        // ================================================================
        // Action handlers — theme list
        // ================================================================

        private void OnNew(object sender, RoutedEventArgs e)
        {
            var name = PromptName("Nom du nouveau thème", "mon-theme");
            if (string.IsNullOrWhiteSpace(name)) return;
            string path = Path.Combine(ThemeManager.ThemesDirectory, name + ".json");
            if (File.Exists(path)) { ShowStatus("Ce nom existe déjà.", error: true); return; }
            ThemeManager.SaveCurrentTo(path);
            RefreshThemeList();
            SelectTheme(name);
        }

        private void OnDuplicate(object sender, RoutedEventArgs e)
        {
            if (_selectedTheme == null) return;
            var name = PromptName("Nom de la copie", _selectedTheme + "-copie");
            if (string.IsNullOrWhiteSpace(name)) return;
            string src  = Path.Combine(ThemeManager.ThemesDirectory, _selectedTheme + ".json");
            string dest = Path.Combine(ThemeManager.ThemesDirectory, name + ".json");
            if (!File.Exists(src)) { ShowStatus("Fichier source introuvable.", error: true); return; }
            File.Copy(src, dest, overwrite: false);
            RefreshThemeList();
            SelectTheme(name);
        }

        private void OnDelete(object sender, RoutedEventArgs e)
        {
            if (_selectedTheme == null) return;
            if (MessageBox.Show($"Supprimer le thème « {_selectedTheme} » ?",
                "Confirmer", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                return;
            string path = Path.Combine(ThemeManager.ThemesDirectory, _selectedTheme + ".json");
            if (File.Exists(path)) File.Delete(path);
            RefreshThemeList();
            if (ThemeList.Items.Count > 0) ThemeList.SelectedIndex = 0;
        }

        private void OnImport(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Filter = "Thème JSON (*.json)|*.json",
                Title  = "Importer un thème"
            };
            if (dlg.ShowDialog() != true) return;
            string dest = Path.Combine(ThemeManager.ThemesDirectory,
                Path.GetFileName(dlg.FileName));
            File.Copy(dlg.FileName, dest, overwrite: true);
            RefreshThemeList();
            SelectTheme(Path.GetFileNameWithoutExtension(dlg.FileName));
        }

        private void OnExportFile(object sender, RoutedEventArgs e)
        {
            if (_selectedTheme == null) return;
            var dlg = new SaveFileDialog
            {
                FileName = _selectedTheme + ".json",
                Filter   = "Thème JSON (*.json)|*.json",
                Title    = "Exporter le thème"
            };
            if (dlg.ShowDialog() != true) return;
            string src = Path.Combine(ThemeManager.ThemesDirectory, _selectedTheme + ".json");
            File.Copy(src, dlg.FileName, overwrite: true);
            ShowStatus("Thème exporté.");
        }

        // ================================================================
        // Action handlers — editor
        // ================================================================

        private void OnSave(object sender, RoutedEventArgs e)
        {
            if (_selectedTheme == null) return;
            ApplyWorkingCopyLive();
            string path = Path.Combine(ThemeManager.ThemesDirectory, _selectedTheme + ".json");
            ThemeManager.SaveCurrentTo(path);
            if (_config != null)
            {
                _config.ActiveThemeName = _selectedTheme;
                _configService?.Save(_config);
            }
            _dirty = false;
            ShowStatus("Thème sauvegardé ✓");
        }

        private void OnCancel(object sender, RoutedEventArgs e)
        {
            if (_selectedTheme == null) return;
            LoadThemeIntoEditor(_selectedTheme);
        }

        private void OnReset(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("Réinitialiser aux valeurs par défaut « Endurance Noir » ?",
                "Confirmer", MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
            ThemeManager.Load("endurance-noir");
            CopyCurrentThemeToWorkingCopy();
            BuildEditor();
            RefreshPreview();
            MarkDirty();
        }

        // ================================================================
        // Helpers
        // ================================================================

        private void MarkDirty() => _dirty = true;

        private void ShowStatus(string msg, bool error = false)
        {
            StatusMsg.Text       = msg;
            StatusMsg.Foreground = new SolidColorBrush(error
                ? Color.FromRgb(239, 68, 68) : Color.FromRgb(34, 197, 94));
            StatusMsg.Visibility = Visibility.Visible;

            _statusTimer?.Stop();
            _statusTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
            _statusTimer.Tick += (_, _) =>
            {
                StatusMsg.Visibility = Visibility.Collapsed;
                _statusTimer?.Stop();
            };
            _statusTimer.Start();
        }

        private static string? PromptName(string prompt, string defaultValue)
        {
            var win = new Window
            {
                Title  = prompt, Width  = 360, Height = 130,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Background = new SolidColorBrush(Color.FromRgb(15, 17, 20)),
                ResizeMode = ResizeMode.NoResize
            };
            if (Application.Current.MainWindow != null)
                win.Owner = Application.Current.MainWindow;

            var sp = new StackPanel { Margin = new Thickness(16) };
            sp.Children.Add(new TextBlock
            {
                Text = prompt, FontSize = 11, FontFamily = new FontFamily("Segoe UI"),
                Foreground = new SolidColorBrush(Color.FromRgb(200, 200, 200)),
                Margin = new Thickness(0, 0, 0, 8)
            });
            var tb = new TextBox
            {
                Text = defaultValue, FontFamily = new FontFamily("Consolas"),
                FontSize = 11, Padding = new Thickness(6, 4, 6, 4),
                Background = new SolidColorBrush(Color.FromRgb(20, 22, 28)),
                Foreground = new SolidColorBrush(Color.FromRgb(220, 220, 220)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(50, 55, 65)),
                CaretBrush = new SolidColorBrush(Color.FromRgb(34, 197, 94))
            };
            sp.Children.Add(tb);
            var btnRow = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 10, 0, 0) };
            string? result = null;
            var ok = new Button
            {
                Content = "OK", Width = 64, Height = 26, Margin = new Thickness(0, 0, 6, 0),
                Background = new SolidColorBrush(Color.FromRgb(21, 128, 61)),
                Foreground = new SolidColorBrush(Color.FromRgb(220, 255, 220)),
                BorderThickness = new Thickness(0)
            };
            ok.Click += (_, _) => { result = tb.Text.Trim(); win.Close(); };
            var cancel = new Button
            {
                Content = "Annuler", Width = 64, Height = 26,
                Background = new SolidColorBrush(Color.FromRgb(30, 32, 38)),
                Foreground = new SolidColorBrush(Color.FromRgb(140, 140, 140)),
                BorderThickness = new Thickness(0)
            };
            cancel.Click += (_, _) => win.Close();
            btnRow.Children.Add(ok);
            btnRow.Children.Add(cancel);
            sp.Children.Add(btnRow);
            win.Content = sp;
            tb.SelectAll();
            win.ShowDialog();
            return result;
        }

        private static UIElement MakeTitle(string text) => new TextBlock
        {
            Text = text, FontSize = 16, FontWeight = FontWeights.Bold,
            FontFamily = new FontFamily("Segoe UI Semibold"),
            Foreground = new SolidColorBrush(Color.FromRgb(245, 245, 245)),
            Margin = new Thickness(0, 0, 0, 4)
        };

        private static UIElement MakeSectionHeader(string text) => new TextBlock
        {
            Text = text, FontSize = 9, FontWeight = FontWeights.Bold,
            FontFamily = new FontFamily("Segoe UI Semibold"),
            Foreground = new SolidColorBrush(Color.FromRgb(34, 197, 94)),
            Margin = new Thickness(0, 8, 0, 4)
        };

        private static Border MakeSep() => new Border
        {
            Height = 1, Background = new SolidColorBrush(Color.FromRgb(28, 30, 36)),
            Margin = new Thickness(0, 6, 0, 6)
        };
    }
}
