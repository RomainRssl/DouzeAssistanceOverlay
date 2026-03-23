using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using LMUOverlay.Models;
using LMUOverlay.Services;

namespace LMUOverlay.Views.Overlays
{
    public class FlagOverlay : BaseOverlayWindow
    {
        private readonly Border _flagBorder;
        private readonly TextBlock _flagText;
        private readonly TextBlock _flagDetail;

        // Hazard panel
        private readonly Border _hazardBorder;
        private readonly TextBlock _hazardArrow;
        private readonly TextBlock _hazardInfo;

        // Debug line (shows raw shared memory values)
        private readonly TextBlock _debugText;

        private int _flashCounter;

        public FlagOverlay(DataService ds, OverlaySettings s) : base(ds, s)
        {
            var outer = new Border { Padding = new Thickness(3) };
            var main = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };

            // ================================================================
            // FLAG BANNER
            // ================================================================
            _flagBorder = new Border
            {
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(20, 8, 20, 8),
                HorizontalAlignment = HorizontalAlignment.Center,
                MinWidth = 140
            };

            var flagStack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
            _flagText = new TextBlock
            {
                FontSize = 22, FontWeight = FontWeights.Bold,
                FontFamily = new FontFamily("Consolas"),
                HorizontalAlignment = HorizontalAlignment.Center
            };
            _flagDetail = new TextBlock
            {
                FontSize = 10, FontFamily = new FontFamily("Consolas"),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 2, 0, 0)
            };
            flagStack.Children.Add(_flagText);
            flagStack.Children.Add(_flagDetail);
            _flagBorder.Child = flagStack;
            main.Children.Add(_flagBorder);

            // ================================================================
            // HAZARD SIDE PANEL (arrow + info)
            // ================================================================
            _hazardBorder = new Border
            {
                CornerRadius = new CornerRadius(4),
                Background = new SolidColorBrush(Color.FromArgb(200, 50, 40, 0)),
                Padding = new Thickness(8, 4, 8, 4),
                Margin = new Thickness(0, 4, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Center,
                Visibility = Visibility.Collapsed
            };
            var hazardStack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
            _hazardArrow = new TextBlock
            {
                FontSize = 24, FontWeight = FontWeights.Bold,
                FontFamily = new FontFamily("Consolas"),
                HorizontalAlignment = HorizontalAlignment.Center,
                Foreground = new SolidColorBrush(Color.FromRgb(255, 235, 59))
            };
            hazardStack.Children.Add(_hazardArrow);
            _hazardInfo = new TextBlock
            {
                FontSize = 9, FontFamily = new FontFamily("Consolas"),
                Foreground = new SolidColorBrush(Color.FromRgb(200, 200, 180)),
                HorizontalAlignment = HorizontalAlignment.Center
            };
            hazardStack.Children.Add(_hazardInfo);
            _hazardBorder.Child = hazardStack;
            main.Children.Add(_hazardBorder);

            // ================================================================
            // DEBUG LINE (small text, shows raw values — remove when confirmed working)
            // ================================================================
            _debugText = new TextBlock
            {
                FontSize = 7, FontFamily = new FontFamily("Consolas"),
                Foreground = new SolidColorBrush(Color.FromRgb(80, 80, 80)),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 3, 0, 0),
                Visibility = Visibility.Visible // DEBUG ON — see raw flag values
            };
            main.Children.Add(_debugText);

            outer.Child = main;
            Content = outer;
        }

        public override void UpdateData()
        {
            byte pFlag = DataService.GetCurrentFlag();
            sbyte yState = DataService.GetYellowFlagState();
            byte gPhase = DataService.GetGamePhase();
            sbyte[] sFlags = DataService.GetSectorFlags();

            // Debug line
            string sf = sFlags != null && sFlags.Length >= 3
                ? $"{sFlags[0]},{sFlags[1]},{sFlags[2]}" : "?";

            // ================================================================
            // FLAG STATE DETECTION — strict priority order
            // ================================================================
            string state;

            // Suppress flags when player is in the pits
            var playerScoring = DataService.GetPlayerInPits();

            if (playerScoring)
                state = "NONE";
            else if (gPhase == 8)
                state = "CHECKERED";
            else if (gPhase == 7)
                state = "RED";
            else if (pFlag == 6)
                state = "BLUE";
            else if (gPhase == 6 || yState > 0)
                state = "YELLOW";
            else if (HasSectorYellow(sFlags))
                state = "YELLOW_LOCAL";
            else if (gPhase >= 3)
                state = "GREEN";
            else
                state = "NONE";

            // Debug: hidden (sector flags confirmed: 1=yellow, 11=green)
            _debugText.Visibility = Visibility.Collapsed;
            _debugText.Text = $"gp:{gPhase} pf:{pFlag} ys:{yState} sf:[{sf}] → {state}";

            // ================================================================
            // FLAG DISPLAY
            // ================================================================
            switch (state)
            {
                case "GREEN":
                case "NONE":
                    _flagBorder.Visibility = Visibility.Collapsed;
                    _hazardBorder.Visibility = Visibility.Collapsed;
                    _flashCounter = 0;
                    break;

                case "YELLOW":
                    _flagBorder.Visibility = Visibility.Visible;
                    string yDetail = yState switch
                    {
                        1 => "En attente",
                        2 => "Pit fermé",
                        3 => "Pit lead lap",
                        4 => "Pit ouvert",
                        5 => "Dernier tour",
                        6 => "Reprise",
                        _ => "Safety Car"
                    };
                    Flag("JAUNE", yDetail,
                        Color.FromRgb(255, 204, 0), Color.FromRgb(30, 30, 30));
                    break;

                case "YELLOW_LOCAL":
                    _flagBorder.Visibility = Visibility.Visible;
                    var sectorList = GetYellowSectors(sFlags);
                    // Show only the sector(s) as main text, e.g. "S2" or "S1 + S3"
                    string sMain = sectorList.Count > 0
                        ? string.Join(" + ", sectorList) : "LOCAL";
                    Flag(sMain, "",
                        Color.FromRgb(255, 204, 0), Color.FromRgb(30, 30, 30));
                    break;

                case "BLUE":
                    _flagBorder.Visibility = Visibility.Visible;
                    Flag("BLEU", "Laissez passer",
                        Color.FromRgb(0, 122, 255), Colors.White);
                    break;

                case "RED":
                    _flagBorder.Visibility = Visibility.Visible;
                    Flag("ROUGE", "Session arrêtée",
                        Color.FromRgb(220, 38, 38), Colors.White);
                    break;

                case "CHECKERED":
                    _flagBorder.Visibility = Visibility.Visible;
                    Flag("DAMIER", "Fin de course",
                        Color.FromRgb(230, 237, 243), Color.FromRgb(30, 30, 30));
                    break;

                default:
                    _flagBorder.Visibility = Visibility.Collapsed;
                    _hazardBorder.Visibility = Visibility.Collapsed;
                    return;
            }

            // ================================================================
            // HAZARD DETECTION (only during yellow, and only if player is moving)
            // ================================================================
            bool isYellow = state == "YELLOW" || state == "YELLOW_LOCAL";
            double playerSpeed = DataService.GetPlayerSpeed();
            bool playerIsMoving = playerSpeed > 15; // > 54 km/h

            if (isYellow && playerIsMoving)
            {
                var hazards = DataService.GetNearbyHazards(500);
                if (hazards.Count > 0)
                {
                    var h = hazards[0];
                    _hazardBorder.Visibility = Visibility.Visible;

                    switch (h.Side)
                    {
                        case HazardSide.Left:
                            _hazardArrow.Text = "◄◄◄ GAUCHE";
                            _hazardArrow.Foreground = Br(255, 235, 59);
                            break;
                        case HazardSide.Right:
                            _hazardArrow.Text = "DROITE ►►►";
                            _hazardArrow.Foreground = Br(255, 235, 59);
                            break;
                        case HazardSide.Center:
                            _flashCounter++;
                            bool cOn = (_flashCounter / 3) % 2 == 0;
                            _hazardArrow.Text = "⚠ MILIEU ⚠";
                            _hazardArrow.Foreground = new SolidColorBrush(
                                cOn ? Color.FromRgb(255, 59, 48) : Color.FromRgb(200, 150, 0));
                            break;
                        default:
                            _hazardArrow.Text = "⚠ DANGER";
                            _hazardArrow.Foreground = Br(255, 204, 0);
                            break;
                    }

                    string dName = OverlayHelper.FormatName(h.DriverName);
                    int speedKmh = (int)(h.Speed * 3.6);
                    _hazardInfo.Text = $"{h.Distance:F0}m — S{h.Sector} — {speedKmh}km/h — {dName}";

                    if (h.Distance < 100)
                    {
                        _flashCounter++;
                        bool flash = (_flashCounter / 2) % 2 == 0;
                        _hazardBorder.Background = new SolidColorBrush(
                            flash ? Color.FromArgb(220, 200, 60, 0) : Color.FromArgb(180, 50, 40, 0));
                    }
                    else
                    {
                        _hazardBorder.Background = new SolidColorBrush(Color.FromArgb(200, 50, 40, 0));
                    }
                }
                else
                {
                    _hazardBorder.Visibility = Visibility.Collapsed;
                }
            }
            else
            {
                _hazardBorder.Visibility = Visibility.Collapsed;
                _flashCounter = 0;
            }
        }

        // ================================================================
        // HELPERS
        // ================================================================

        private static bool HasSectorYellow(sbyte[]? sf)
        {
            // LMU observed: 0=no data, 1=YELLOW, 2=GREEN
            // No incident: [2,2,2] or [0,0,0] (before session)
            // Yellow S1:   [1,2,2]
            if (sf == null || sf.Length < 3) return false;
            return sf[0] == 1 || sf[1] == 1 || sf[2] == 1;
        }

        private static List<string> GetYellowSectors(sbyte[]? sf)
        {
            var sectors = new List<string>();
            if (sf == null || sf.Length < 3) return sectors;
            if (sf[0] == 1) sectors.Add("S1");
            if (sf[1] == 1) sectors.Add("S2");
            if (sf[2] == 1) sectors.Add("S3");
            return sectors;
        }

        private void Flag(string text, string detail, Color bg, Color fg)
        {
            _flagBorder.Background = new SolidColorBrush(bg);
            _flagText.Text = text;
            _flagText.Foreground = new SolidColorBrush(fg);
            _flagDetail.Text = detail;
            _flagDetail.Foreground = new SolidColorBrush(
                Color.FromArgb(180, fg.R, fg.G, fg.B));
        }

        private static SolidColorBrush Br(byte r, byte g, byte b)
            => new(Color.FromRgb(r, g, b));
    }
}
