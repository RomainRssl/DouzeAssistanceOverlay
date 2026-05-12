using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using LMUOverlay.Helpers;
using LMUOverlay.Models;
using LMUOverlay.Services;

namespace LMUOverlay.Views.Overlays
{
    /// <summary>
    /// Overlay dédié à la distance aux stands.
    ///
    /// Hors pitlane :
    ///   • ENTRÉE — distance jusqu'à la ligne des 60 km/h (apprise ou estimée)
    ///   • BOX    — distance jusqu'au box du joueur (mPitLapDist)
    ///
    /// Dans la pitlane (après avoir franchi le 60 km/h) :
    ///   • BOX seulement — distance restante jusqu'au box pour s'arrêter au bon endroit
    ///   • Rouge < 30 m, orange < 80 m
    ///
    /// Si PitDistanceConfig.AutoShowOnPitRequest == true, l'overlay se masque
    /// automatiquement tant que mPitState == 0.
    /// </summary>
    public class PitDistanceOverlay : BaseOverlayWindow
    {
        private readonly PitDistanceConfig _cfg;

        // ---- Wrapper principal (masqué en mode auto quand aucun pit demandé) ----
        private readonly Border _mainBorder;

        // ---- ENTRÉE row ----
        private readonly TextBlock _entryValue;
        private readonly Border    _entryDot;

        // ---- BOX row ----
        private readonly TextBlock _stallValue;
        private readonly Border    _stallDot;

        // ---- Status (AU STAND / SORTIE) ----
        private readonly TextBlock _statusText;
        private readonly Grid      _distGrid;

        // Seuils hors pitlane
        private const double DIST_DANGER = 150;
        private const double DIST_WARN   = 400;

        public PitDistanceOverlay(DataService ds, OverlaySettings s, PitDistanceConfig cfg) : base(ds, s)
        {
            _cfg = cfg;
            var tm = ThemeManager.Current;

            _mainBorder = OverlayHelper.MakeBorder();
            var root = new StackPanel();

            // ── Titre ─────────────────────────────────────────────────────────
            root.Children.Add(OverlayHelper.MakeTitle("STANDS"));

            // ── Grille des distances ───────────────────────────────────────────
            _distGrid = new Grid { Margin = new Thickness(4, 2, 4, 6) };
            _distGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10) }); // dot
            _distGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(56) }); // label
            _distGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // value
            _distGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // row 0 — ENTRÉE
            _distGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(4) }); // row 1 — spacer
            _distGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // row 2 — BOX

            // Row 0 — ENTRÉE
            _entryDot = MakeDot(tm.TextMuted);
            Grid.SetRow(_entryDot, 0); Grid.SetColumn(_entryDot, 0);
            _distGrid.Children.Add(_entryDot);

            var entryLabel = MakeLabel("ENTRÉE", tm);
            Grid.SetRow(entryLabel, 0); Grid.SetColumn(entryLabel, 1);
            _distGrid.Children.Add(entryLabel);

            _entryValue = MakeValue(tm);
            Grid.SetRow(_entryValue, 0); Grid.SetColumn(_entryValue, 2);
            _distGrid.Children.Add(_entryValue);

            // Row 2 — BOX
            _stallDot = MakeDot(tm.TextMuted);
            Grid.SetRow(_stallDot, 2); Grid.SetColumn(_stallDot, 0);
            _distGrid.Children.Add(_stallDot);

            var stallLabel = MakeLabel("BOX", tm);
            Grid.SetRow(stallLabel, 2); Grid.SetColumn(stallLabel, 1);
            _distGrid.Children.Add(stallLabel);

            _stallValue = MakeValue(tm);
            Grid.SetRow(_stallValue, 2); Grid.SetColumn(_stallValue, 2);
            _distGrid.Children.Add(_stallValue);

            root.Children.Add(_distGrid);

            // ── Status (AU STAND / SORTIE) ─────────────────────────────────────
            _statusText = new TextBlock
            {
                FontSize            = 15,
                FontWeight          = FontWeights.Bold,
                FontFamily          = OverlayHelper.FontConsolas,
                Foreground          = BrushCache.Get(tm.StateGood),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin              = new Thickness(8, 2, 8, 6),
                Visibility          = Visibility.Collapsed
            };
            root.Children.Add(_statusText);

            _mainBorder.Child = root;
            Content = _mainBorder;
        }

        // ── Factories ────────────────────────────────────────────────────────

        private static Border MakeDot(Color color)
            => new Border
            {
                Width             = 6,
                Height            = 6,
                CornerRadius      = new CornerRadius(3),
                Background        = BrushCache.Get(color),
                VerticalAlignment = VerticalAlignment.Center,
                Margin            = new Thickness(0, 0, 4, 0)
            };

        private static TextBlock MakeLabel(string text, ThemeManager tm)
            => new TextBlock
            {
                Text              = text,
                FontSize          = 11,
                FontFamily        = OverlayHelper.FontConsolas,
                Foreground        = BrushCache.Get(tm.TextMuted),
                VerticalAlignment = VerticalAlignment.Center,
                Margin            = new Thickness(2, 0, 6, 0)
            };

        private static TextBlock MakeValue(ThemeManager tm)
            => new TextBlock
            {
                FontSize            = 20,
                FontWeight          = FontWeights.Bold,
                FontFamily          = OverlayHelper.FontConsolas,
                Foreground          = BrushCache.Get(tm.TextPrimary),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment   = VerticalAlignment.Center,
                Text                = "---"
            };

        // ── Update loop ──────────────────────────────────────────────────────

        public override void UpdateData()
        {
            var tm = ThemeManager.Current;
            var (entryDist, stallDist, pitState, inPits) = DataService.GetPitDistanceData();

            // ── Mode auto : se masquer si aucun pit n'est actif ───────────────
            if (_cfg.AutoShowOnPitRequest && pitState == 0 && !inPits)
            {
                _mainBorder.Visibility = Visibility.Collapsed;
                return;
            }
            _mainBorder.Visibility = Visibility.Visible;

            // ── Arrêté au stand ou en sortie → statut texte ───────────────────
            if ((inPits && pitState == 3) || pitState == 4)
            {
                _distGrid.Visibility   = Visibility.Collapsed;
                _statusText.Visibility = Visibility.Visible;
                (string txt, Color col) = pitState == 4
                    ? ("🚀 SORTIE",   tm.ClassLmp2)
                    : ("🔧 AU STAND", tm.StateGood);
                _statusText.Text       = txt;
                _statusText.Foreground = BrushCache.Get(col);
                return;
            }

            // ── Dans la pitlane (approche du box) → BOX seulement ────────────
            if (inPits)
            {
                _distGrid.Visibility   = Visibility.Visible;
                _statusText.Visibility = Visibility.Collapsed;

                // Masquer la ligne ENTRÉE et son spacer (inutiles en pitlane)
                _distGrid.RowDefinitions[0].Height = new GridLength(0);
                _distGrid.RowDefinitions[1].Height = new GridLength(0);

                if (stallDist > 0)
                {
                    _stallValue.Text       = FormatDist(stallDist);
                    var col                = PitLaneColor(stallDist, tm);
                    _stallValue.Foreground = BrushCache.Get(col);
                    _stallDot.Background   = BrushCache.Get(col);
                }
                else
                {
                    _stallValue.Text       = "---";
                    _stallValue.Foreground = BrushCache.Get(tm.TextMuted);
                    _stallDot.Background   = BrushCache.Get(tm.TextMuted);
                }
                return;
            }

            // ── Hors pitlane : affichage normal ENTRÉE + BOX ──────────────────
            _distGrid.Visibility   = Visibility.Visible;
            _statusText.Visibility = Visibility.Collapsed;

            // Rétablir les lignes ENTRÉE et spacer
            _distGrid.RowDefinitions[0].Height = GridLength.Auto;
            _distGrid.RowDefinitions[1].Height = new GridLength(4);

            // ENTRÉE
            if (entryDist > 0)
            {
                _entryValue.Text       = FormatDist(entryDist);
                var entryColor         = DistColor(entryDist, tm);
                _entryValue.Foreground = BrushCache.Get(entryColor);
                _entryDot.Background   = BrushCache.Get(entryColor);
            }
            else
            {
                _entryValue.Text       = "---";
                _entryValue.Foreground = BrushCache.Get(tm.TextMuted);
                _entryDot.Background   = BrushCache.Get(tm.TextMuted);
            }

            // BOX
            if (stallDist > 0)
            {
                _stallValue.Text       = FormatDist(stallDist);
                var stallColor         = DistColor(stallDist, tm);
                _stallValue.Foreground = BrushCache.Get(stallColor);
                _stallDot.Background   = BrushCache.Get(stallColor);
            }
            else
            {
                _stallValue.Text       = "---";
                _stallValue.Foreground = BrushCache.Get(tm.TextMuted);
                _stallDot.Background   = BrushCache.Get(tm.TextMuted);
            }
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        private static string FormatDist(double m)
        {
            if (m >= 1000) return $"{m / 1000:F2} km";
            if (m >= 100)  return $"{m:F0} m";
            return $"{m:F1} m";
        }

        // Couleurs pour l'approche depuis la piste (longues distances)
        private static Color DistColor(double m, ThemeManager tm)
            => m <= DIST_DANGER ? tm.StateDanger
             : m <= DIST_WARN   ? tm.StateWarn
             : tm.TextSecondary;

        // Couleurs pour l'approche dans la pitlane (courtes distances)
        private static Color PitLaneColor(double m, ThemeManager tm)
            => m <= 30 ? tm.StateDanger
             : m <= 80 ? tm.StateWarn
             : tm.TextSecondary;
    }
}
