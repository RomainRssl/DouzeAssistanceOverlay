using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using LMUOverlay.Models;
using LMUOverlay.Services;
using Microsoft.Win32;

namespace LMUOverlay.Views
{
    public partial class ChronoPanel : UserControl
    {
        // ── Layout constants ─────────────────────────────────────────────────
        private const double ROW_H      = 34;
        private const double CLASS_H    = 28;  // top class header
        private const double CAR_H      = 30;  // car sub-header
        private const double COL_W      = 130; // per-car column width

        // ── Colors ───────────────────────────────────────────────────────────
        private static readonly Color C_BgDark    = Color.FromRgb(0x0B, 0x0B, 0x0B);
        private static readonly Color C_BgRow0    = Color.FromRgb(0x0E, 0x0E, 0x0E);
        private static readonly Color C_BgRow1    = Color.FromRgb(0x11, 0x11, 0x11);
        private static readonly Color C_Border    = Color.FromRgb(0x1E, 0x1E, 0x1E);
        private static readonly Color C_Text      = Color.FromRgb(0xDD, 0xDD, 0xDD);
        private static readonly Color C_Muted     = Color.FromRgb(0x52, 0x52, 0x52);
        private static readonly Color C_Best      = Color.FromRgb(0x22, 0xC5, 0x5E);

        // Class header colors
        private static readonly Dictionary<string, Color> ClassColors = new()
        {
            ["GT3"]      = Color.FromRgb(0xFF, 0x95, 0x00),   // orange
            ["LMP2"]     = Color.FromRgb(0x00, 0x90, 0xFF),   // blue
            ["LMP3"]     = Color.FromRgb(0xA8, 0x55, 0xF7),   // purple
            ["Hypercar"] = Color.FromRgb(0xFF, 0x18, 0x01),   // red
        };

        // ── State ─────────────────────────────────────────────────────────────
        private ChronoSettings _settings = new();
        private bool _suppressSettingChanged;

        public event Action? SettingsChanged;

        // ── Constructor ──────────────────────────────────────────────────────
        public ChronoPanel()
        {
            InitializeComponent();
        }

        // ── Called by MainWindow when this tab becomes visible ────────────────
        public void Initialize(ChronoSettings settings)
        {
            _settings = settings;

            _suppressSettingChanged = true;
            TbGameFolder.Text  = settings.GameFolder;
            TbPlayerName.Text  = settings.PlayerName;
            _suppressSettingChanged = false;

            UpdateResultPath();

            // Auto-load if config is complete
            if (!string.IsNullOrWhiteSpace(settings.GameFolder) &&
                !string.IsNullOrWhiteSpace(settings.PlayerName))
                Refresh();
        }

        // ── Toolbar events ────────────────────────────────────────────────────
        private void OnBrowse(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFolderDialog
            {
                Title = "Sélectionner le dossier racine de Le Mans Ultimate",
                Multiselect = false
            };

            if (dlg.ShowDialog() == true)
            {
                TbGameFolder.Text = dlg.FolderName;
                _settings.GameFolder = dlg.FolderName;
                UpdateResultPath();
            }
        }

        private void OnSettingChanged(object sender, TextChangedEventArgs e)
        {
            if (_suppressSettingChanged) return;
            _settings.GameFolder = TbGameFolder.Text.Trim();
            _settings.PlayerName = TbPlayerName.Text.Trim();
            UpdateResultPath();
            SettingsChanged?.Invoke();
        }

        private void OnRefresh(object sender, RoutedEventArgs e) => Refresh();

        private void OnRightScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            // Sync vertical position of left (frozen) column
            LeftScroll.ScrollToVerticalOffset(RightScroll.VerticalOffset);
        }

        // ── Internal helpers ──────────────────────────────────────────────────
        // The user enters the Results folder directly — use as-is.
        private string ResultsFolder => _settings.GameFolder?.Trim() ?? "";

        private void UpdateResultPath()
        {
            string folder = ResultsFolder;
            TbResultPath.Text = string.IsNullOrWhiteSpace(folder) ? "" : $"→ {folder}";
        }

        private void Refresh()
        {
            CircuitList.Children.Clear();
            DataTable.Children.Clear();
            TbStatus.Text = "Chargement…";
            EmptyState.Visibility = Visibility.Collapsed;

            string folder = ResultsFolder;
            string player = _settings.PlayerName;

            if (string.IsNullOrWhiteSpace(folder))
            {
                ShowEmpty("Entrez le dossier du jeu.");
                return;
            }
            if (!Directory.Exists(folder))
            {
                ShowEmpty($"Dossier introuvable :\n{folder}");
                return;
            }
            if (string.IsNullOrWhiteSpace(player))
            {
                ShowEmpty("Entrez votre nom in-game.");
                return;
            }

            var entries = ChronoService.LoadResults(folder, player);

            if (entries.Count == 0)
            {
                ShowEmpty($"Aucun résultat trouvé pour « {player} »\ndans {folder}");
                return;
            }

            BuildTable(entries);

            int fileCount = 0;
            try { fileCount = Directory.GetFiles(folder, "*.xml").Length; } catch { }
            TbStatus.Text = $"{entries.Count} entrée(s) — {fileCount} fichier(s) XML analysé(s)";
        }

        private void ShowEmpty(string hint)
        {
            TbEmptyHint.Text = hint;
            EmptyState.Visibility = Visibility.Visible;
            TbStatus.Text = "";
        }

        // ── Table builder ─────────────────────────────────────────────────────
        private void BuildTable(List<ChronoEntry> entries)
        {
            // Unique tracks (sorted alphabetically)
            var tracks = entries.Select(e => e.TrackName).Distinct()
                                .OrderBy(t => t).ToList();

            // Cars grouped by class, columns ordered: GT3, LMP2, LMP3, Hypercar, others
            var classOrder = ChronoService.GetClassOrder().ToList();
            var allClasses = entries.Select(e => e.CarClass).Distinct()
                                    .OrderBy(c =>
                                    {
                                        int idx = classOrder.IndexOf(c);
                                        return idx < 0 ? 99 : idx;
                                    }).ToList();

            // Per class: list of unique car names (sorted)
            var carsByClass = allClasses.ToDictionary(
                cls => cls,
                cls => entries.Where(e => e.CarClass == cls)
                              .Select(e => e.CarName)
                              .Distinct().OrderBy(n => n).ToList());

            // Flat list of (class, carName) columns
            var columns = allClasses
                .SelectMany(cls => carsByClass[cls].Select(car => (cls, car)))
                .ToList();

            // Best time lookup
            var bestMap = entries.ToDictionary(e => (e.TrackName, e.CarName), e => e.BestLapSec);

            // Best overall time per track (for green highlight)
            var bestPerTrack = tracks.ToDictionary(
                t => t,
                t => entries.Where(e => e.TrackName == t).Min(e => e.BestLapSec));

            // ── Build right-side header (class row + car row) ──────────────
            var headerGrid = BuildHeaderGrid(allClasses, carsByClass, columns);
            DataTable.Children.Add(headerGrid);

            // ── Build data rows ────────────────────────────────────────────
            for (int ri = 0; ri < tracks.Count; ri++)
            {
                string track = tracks[ri];
                var dataRow  = BuildDataRow(track, columns, bestMap, bestPerTrack[track], ri);
                DataTable.Children.Add(dataRow);

                // Left panel: circuit name cell
                var circuitCell = MakeCircuitCell(track, ri);
                CircuitList.Children.Add(circuitCell);
            }
        }

        private Grid BuildHeaderGrid(
            List<string> classes,
            Dictionary<string, List<string>> carsByClass,
            List<(string cls, string car)> columns)
        {
            int totalCols = columns.Count;

            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(CLASS_H) });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(CAR_H) });
            for (int c = 0; c < totalCols; c++)
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(COL_W) });

            // Row 0 — class headers (merged per class)
            int colIdx = 0;
            foreach (var cls in classes)
            {
                int span = carsByClass[cls].Count;
                Color classCol = ClassColors.TryGetValue(cls, out var cc) ? cc : C_Muted;

                var classCell = new Border
                {
                    Background = new SolidColorBrush(Color.FromRgb(0x12, 0x12, 0x12)),
                    BorderBrush = new SolidColorBrush(C_Border),
                    BorderThickness = new Thickness(0, 0, 1, 1),
                    Child = new TextBlock
                    {
                        Text = cls.ToUpperInvariant(),
                        FontSize = 12, FontWeight = FontWeights.Bold,
                        FontFamily = new FontFamily("Segoe UI Semibold"),
                        Foreground = new SolidColorBrush(classCol),
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center
                    }
                };
                Grid.SetRow(classCell, 0);
                Grid.SetColumn(classCell, colIdx);
                Grid.SetColumnSpan(classCell, span);
                grid.Children.Add(classCell);
                colIdx += span;
            }

            // Row 1 — car sub-headers
            for (int c = 0; c < columns.Count; c++)
            {
                string carName = columns[c].car;
                // Shorten to ~14 chars to fit column
                string display = carName.Length > 14 ? carName[..14] + "…" : carName;

                var carCell = new Border
                {
                    Background = new SolidColorBrush(Color.FromRgb(0x16, 0x16, 0x16)),
                    BorderBrush = new SolidColorBrush(C_Border),
                    BorderThickness = new Thickness(0, 0, 1, 1),
                    ToolTip = carName,
                    Child = new TextBlock
                    {
                        Text = display,
                        FontSize = 10, FontWeight = FontWeights.SemiBold,
                        FontFamily = new FontFamily("Segoe UI Semibold"),
                        Foreground = new SolidColorBrush(C_Muted),
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center,
                        TextAlignment = TextAlignment.Center,
                        TextWrapping = TextWrapping.Wrap,
                        Margin = new Thickness(2, 0, 2, 0)
                    }
                };
                Grid.SetRow(carCell, 1);
                Grid.SetColumn(carCell, c);
                grid.Children.Add(carCell);
            }

            return grid;
        }

        private Grid BuildDataRow(
            string track,
            List<(string cls, string car)> columns,
            Dictionary<(string, string), double> bestMap,
            double trackBest,
            int rowIndex)
        {
            var grid = new Grid { Height = ROW_H };
            for (int c = 0; c < columns.Count; c++)
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(COL_W) });

            Color rowBg = rowIndex % 2 == 0 ? C_BgRow0 : C_BgRow1;

            for (int c = 0; c < columns.Count; c++)
            {
                var (cls, car) = columns[c];
                bool hasTime = bestMap.TryGetValue((track, car), out double lapSec);
                bool isBest  = hasTime && Math.Abs(lapSec - trackBest) < 0.001;

                Color fg = isBest ? C_Best : C_Text;
                Color bg = isBest ? Color.FromRgb(0x0A, 0x22, 0x14) : rowBg;

                var cell = new Border
                {
                    Background = new SolidColorBrush(bg),
                    BorderBrush = new SolidColorBrush(C_Border),
                    BorderThickness = new Thickness(0, 0, 1, 1),
                    Child = new TextBlock
                    {
                        Text = hasTime ? ChronoService.FormatLap(lapSec) : "",
                        FontSize = 13, FontFamily = new FontFamily("Consolas"),
                        Foreground = new SolidColorBrush(fg),
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center
                    }
                };
                Grid.SetColumn(cell, c);
                grid.Children.Add(cell);
            }

            return grid;
        }

        private Border MakeCircuitCell(string track, int rowIndex)
        {
            Color rowBg = rowIndex % 2 == 0 ? C_BgRow0 : C_BgRow1;

            return new Border
            {
                Height = ROW_H,
                Background = new SolidColorBrush(rowBg),
                BorderBrush = new SolidColorBrush(C_Border),
                BorderThickness = new Thickness(0, 0, 1, 1),
                Child = new TextBlock
                {
                    Text = track,
                    FontSize = 12,
                    FontFamily = new FontFamily("Segoe UI"),
                    Foreground = new SolidColorBrush(C_Text),
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(10, 0, 6, 0),
                    TextWrapping = TextWrapping.NoWrap
                }
            };
        }
    }
}
