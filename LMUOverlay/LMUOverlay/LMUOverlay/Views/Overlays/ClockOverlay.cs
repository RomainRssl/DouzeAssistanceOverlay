using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using LMUOverlay.Helpers;
using LMUOverlay.Models;
using LMUOverlay.Services;

namespace LMUOverlay.Views.Overlays
{
    /// <summary>
    /// Displays the current Windows system time, updated every second.
    /// Does not depend on game data — runs regardless of LMU connection state.
    /// </summary>
    public class ClockOverlay : BaseOverlayWindow
    {
        private readonly TextBlock _timeText;
        private readonly DispatcherTimer _clockTimer;

        public ClockOverlay(DataService ds, OverlaySettings s) : base(ds, s)
        {
            var tm0 = ThemeManager.Current;
            var outer = new Border
            {
                Background    = BrushCache.Get(Color.FromArgb(200, tm0.PanelBackground.R, tm0.PanelBackground.G, tm0.PanelBackground.B)),
                CornerRadius  = new CornerRadius(6),
                Padding       = new Thickness(16, 8, 16, 8)
            };

            _timeText = new TextBlock
            {
                FontSize            = 30,
                FontFamily          = OverlayHelper.FontConsolas,
                FontWeight          = FontWeights.Bold,
                Foreground          = BrushCache.Get(tm0.TextPrimary),
                HorizontalAlignment = HorizontalAlignment.Center,
                Text                = DateTime.Now.ToString("HH:mm:ss")
            };

            outer.Child = _timeText;
            Content = outer;

            // Update every second, independent of LMU connection
            _clockTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _clockTimer.Tick += (_, _) => _timeText.Text = DateTime.Now.ToString("HH:mm:ss");
            _clockTimer.Start();
        }

        /// <summary>
        /// The clock uses its own DispatcherTimer — nothing to do in the game update loop.
        /// </summary>
        public override void UpdateData() { }

        protected override void OnClosed(EventArgs e)
        {
            _clockTimer.Stop();
            base.OnClosed(e);
        }
    }
}
