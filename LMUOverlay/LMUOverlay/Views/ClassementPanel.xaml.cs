using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using LMUOverlay.Models;
using LMUOverlay.Services;

namespace LMUOverlay.Views
{
    public partial class ClassementPanel : UserControl
    {
        private static readonly string[] ClassOrder =
            ["HYPERCAR", "GT3", "LMP2", "LMP3", "LMP2_ELMS", "GTE"];

        private static readonly Dictionary<string, string> ClassColors = new(StringComparer.OrdinalIgnoreCase)
        {
            ["GT3"]       = "#00cc88",
            ["HYPERCAR"]  = "#ff4466",
            ["LMP2"]      = "#44aaff",
            ["LMP3"]      = "#cc66ff",
            ["LMP2_ELMS"] = "#2288ff",
            ["GTE"]       = "#ffaa00",
        };

        private GeneralSettings?  _settings;
        private ClassementService? _svc;
        private HashSet<string> _activeClasses = [];
        private Dictionary<string, Dictionary<string, List<LapEntry>>>? _data;

        public void Initialize(GeneralSettings settings, ClassementService svc)
        {
            _settings       = settings;
            _svc            = svc;
            TbPrenom.Text   = settings.LeaderboardPrenom;
            TbNom.Text      = settings.LeaderboardNom;
            TbDiscord.Text  = settings.LeaderboardDiscord;

            // Show hint if identity not yet configured
            if (string.IsNullOrWhiteSpace(settings.LeaderboardPrenom) ||
                string.IsNullOrWhiteSpace(settings.LeaderboardNom))
                ShowState("config");
        }

        public async Task LoadAsync()
        {
            if (_settings == null) return;

            if (string.IsNullOrWhiteSpace(_settings.LeaderboardPrenom) ||
                string.IsNullOrWhiteSpace(_settings.LeaderboardNom))
            {
                ShowState("config");
                return;
            }

            ShowState("loading");
            StatusText.Visibility = Visibility.Collapsed;

            _data = await _svc!.FetchAsync();

            if (_data == null)
            {
                ShowState("error");
                ErrorText.Text = "Impossible de joindre le serveur.";
                return;
            }

            if (_data.Count == 0)
            {
                ShowState("empty");
                return;
            }

            BuildFilters();
            Render();
            ShowState("data");
        }

        private void BuildFilters()
        {
            if (_data == null) return;

            var presentClasses = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var byClass in _data.Values)
                foreach (var cls in byClass.Keys)
                    presentClasses.Add(cls);

            if (_activeClasses.Count == 0)
                _activeClasses = new HashSet<string>(presentClasses, StringComparer.OrdinalIgnoreCase);

            FilterPanel.Children.Clear();

            var ordered = ClassOrder.Where(c => presentClasses.Contains(c))
                .Concat(presentClasses.Where(c => !ClassOrder.Contains(c, StringComparer.OrdinalIgnoreCase)))
                .ToList();

            foreach (string cls in ordered)
            {
                bool active = _activeClasses.Contains(cls);
                string hex  = ClassColors.GetValueOrDefault(cls, "#aaaaaa");
                var color   = (Color)ColorConverter.ConvertFromString(hex);

                var btn = new Button
                {
                    Content         = cls,
                    FontFamily      = new FontFamily("Consolas"),
                    FontSize        = 9,
                    Padding         = new Thickness(10, 3, 10, 3),
                    Margin          = new Thickness(0, 0, 4, 0),
                    Cursor          = System.Windows.Input.Cursors.Hand,
                    Tag             = cls,
                    Opacity         = active ? 1.0 : 0.35,
                    Background      = new SolidColorBrush(Color.FromRgb(0x1a, 0x2a, 0x2a)),
                    BorderBrush     = new SolidColorBrush(color),
                    BorderThickness = new Thickness(1),
                    Foreground      = new SolidColorBrush(color),
                };
                btn.Click += OnFilterClick;
                FilterPanel.Children.Add(btn);
            }
        }

        private void OnFilterClick(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn || btn.Tag is not string cls) return;
            if (_activeClasses.Contains(cls)) _activeClasses.Remove(cls);
            else _activeClasses.Add(cls);
            btn.Opacity = _activeClasses.Contains(cls) ? 1.0 : 0.35;
            Render();
        }

        private void Render()
        {
            if (_data == null) return;

            RankingsPanel.Children.Clear();
            int totalCircuits = 0;
            var allPilots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var (circuit, byClass) in _data.OrderBy(k => k.Key))
            {
                var visClasses = ClassOrder
                    .Where(c => byClass.ContainsKey(c) && _activeClasses.Contains(c))
                    .Concat(byClass.Keys
                        .Where(c => !ClassOrder.Contains(c, StringComparer.OrdinalIgnoreCase)
                                 && _activeClasses.Contains(c)))
                    .ToList();

                if (visClasses.Count == 0) continue;
                totalCircuits++;

                var header = new Border
                {
                    Background      = new SolidColorBrush(Color.FromRgb(0x0e, 0x0e, 0x0e)),
                    BorderBrush     = new SolidColorBrush(Color.FromRgb(0x1e, 0x1e, 0x1e)),
                    BorderThickness = new Thickness(0, 0, 0, 1),
                    Padding         = new Thickness(0, 10, 0, 4),
                    Margin          = new Thickness(0, 16, 0, 0),
                };
                header.Child = new TextBlock
                {
                    Text       = circuit.ToUpperInvariant(),
                    FontSize   = 11,
                    FontWeight = FontWeights.Bold,
                    FontFamily = new FontFamily("Consolas"),
                    Foreground = new SolidColorBrush(Color.FromRgb(0x22, 0xC5, 0x5E)),
                };
                RankingsPanel.Children.Add(header);

                foreach (string cls in visClasses)
                {
                    var entries  = byClass[cls];
                    string hex   = ClassColors.GetValueOrDefault(cls, "#aaaaaa");
                    var clsColor = (Color)ColorConverter.ConvertFromString(hex);

                    RankingsPanel.Children.Add(new TextBlock
                    {
                        Text       = cls.ToUpperInvariant(),
                        FontSize   = 9,
                        FontFamily = new FontFamily("Consolas"),
                        FontWeight = FontWeights.Bold,
                        Foreground = new SolidColorBrush(clsColor),
                        Margin     = new Thickness(0, 10, 0, 4),
                    });

                    double bestTime = entries[0].LapTime;
                    int rank = 0;
                    foreach (var e in entries.Take(5))
                    {
                        rank++;
                        allPilots.Add(e.Username);
                        RankingsPanel.Children.Add(BuildEntryRow(e, rank, bestTime));
                    }
                }
            }

            int totalPilots = allPilots.Count;
            StatsText.Text = totalCircuits > 0
                ? $"{totalCircuits} circuit{(totalCircuits > 1 ? "s" : "")} · {totalPilots} pilote{(totalPilots > 1 ? "s" : "")}"
                : "";

            if (RankingsPanel.Children.Count == 0)
                ShowState("empty");
        }

        private static UIElement BuildEntryRow(LapEntry e, int rank, double best)
        {
            string timeStr = FormatTime(e.LapTime);
            string gapStr  = rank == 1 ? "" : $"+{(e.LapTime - best):F3}";
            string sectors = (e.Sector1.HasValue && e.Sector2.HasValue && e.Sector3.HasValue)
                ? $"{FormatTime(e.Sector1.Value)}  {FormatTime(e.Sector2.Value)}  {FormatTime(e.Sector3.Value)}"
                : "";

            var grid = new Grid { Margin = new Thickness(0, 2, 0, 2) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(22) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(180) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(90) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(70) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var details = new Grid();
            details.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            details.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            Grid.SetColumn(details, 4);

            var rankTb = new TextBlock
            {
                Text              = $"{rank}.",
                FontSize          = 10,
                FontFamily        = new FontFamily("Consolas"),
                Foreground        = new SolidColorBrush(Color.FromRgb(0x52, 0x52, 0x52)),
                VerticalAlignment = VerticalAlignment.Center,
            };
            Grid.SetColumn(rankTb, 0);

            var nameTb = new TextBlock
            {
                Text              = e.Username,
                FontSize          = 11,
                FontFamily        = new FontFamily("Consolas"),
                Foreground        = new SolidColorBrush(Color.FromRgb(0x22, 0xC5, 0x5E)),
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming      = TextTrimming.CharacterEllipsis,
            };
            Grid.SetColumn(nameTb, 1);

            var timeTb = new TextBlock
            {
                Text              = timeStr,
                FontSize          = 12,
                FontWeight        = FontWeights.Bold,
                FontFamily        = new FontFamily("Consolas"),
                Foreground        = new SolidColorBrush(Color.FromRgb(0xF5, 0xF5, 0xF5)),
                VerticalAlignment = VerticalAlignment.Center,
            };
            Grid.SetColumn(timeTb, 2);

            var gapTb = new TextBlock
            {
                Text              = gapStr,
                FontSize          = 10,
                FontFamily        = new FontFamily("Consolas"),
                Foreground        = new SolidColorBrush(Color.FromRgb(0x52, 0x52, 0x52)),
                VerticalAlignment = VerticalAlignment.Center,
            };
            Grid.SetColumn(gapTb, 3);

            if (!string.IsNullOrEmpty(e.CarName))
            {
                var carTb = new TextBlock
                {
                    Text         = e.CarName,
                    FontSize     = 9,
                    FontFamily   = new FontFamily("Consolas"),
                    Foreground   = new SolidColorBrush(Color.FromRgb(0x38, 0x38, 0x38)),
                    TextTrimming = TextTrimming.CharacterEllipsis,
                };
                Grid.SetRow(carTb, 0);
                details.Children.Add(carTb);
            }

            if (!string.IsNullOrEmpty(sectors))
            {
                var secTb = new TextBlock
                {
                    Text       = sectors,
                    FontSize   = 9,
                    FontFamily = new FontFamily("Consolas"),
                    Foreground = new SolidColorBrush(Color.FromRgb(0x38, 0x38, 0x38)),
                };
                Grid.SetRow(secTb, 1);
                details.Children.Add(secTb);
            }

            grid.Children.Add(rankTb);
            grid.Children.Add(nameTb);
            grid.Children.Add(timeTb);
            grid.Children.Add(gapTb);
            grid.Children.Add(details);

            return new Border
            {
                Child           = grid,
                Background      = new SolidColorBrush(Color.FromRgb(0x0e, 0x0e, 0x0e)),
                BorderBrush     = new SolidColorBrush(Color.FromRgb(0x1a, 0x1a, 0x1a)),
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding         = new Thickness(6, 5, 6, 5),
            };
        }

        private static string FormatTime(double sec)
        {
            if (sec <= 0) return "—";
            int m      = (int)(sec / 60);
            double rem = sec % 60;
            return m > 0 ? $"{m}:{rem:00.000}" : rem.ToString("0.000");
        }

        private void ShowState(string state)
        {
            LoadingText.Visibility      = state == "loading" ? Visibility.Visible : Visibility.Collapsed;
            ErrorPanel.Visibility       = state == "error"   ? Visibility.Visible : Visibility.Collapsed;
            EmptyPanel.Visibility       = state == "empty"   ? Visibility.Visible : Visibility.Collapsed;
            ConfigNeededText.Visibility = state == "config"  ? Visibility.Visible : Visibility.Collapsed;
            RankingsScroll.Visibility   = state == "data"    ? Visibility.Visible : Visibility.Collapsed;
        }

        private void OnRefresh(object sender, RoutedEventArgs e) => _ = LoadAsync();

        private void OnSave(object sender, RoutedEventArgs e)
        {
            if (_settings == null) return;
            _settings.LeaderboardPrenom  = TbPrenom.Text.Trim();
            _settings.LeaderboardNom     = TbNom.Text.Trim();
            _settings.LeaderboardDiscord = TbDiscord.Text.Trim();

            StatusText.Text       = "✓ Sauvegardé";
            StatusText.Visibility = Visibility.Visible;
            _ = LoadAsync();
        }
    }
}
