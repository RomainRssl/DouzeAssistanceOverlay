using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using LMUOverlay.Helpers;
using LMUOverlay.Models;
using LMUOverlay.Services;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace LMUOverlay.Views.Overlays
{
    public class WebBrowserOverlay : BaseOverlayWindow
    {
        private readonly WebView2 _webView;
        private bool _initialized;

        public WebBrowserOverlay(DataService ds, OverlaySettings s) : base(ds, s)
        {
            UseRawResize = true;

            // CRITICAL: WebView2 is HWND-based (HwndHost). Incompatible with
            // AllowsTransparency=true set by BaseOverlayWindow constructor.
            // MUST override before window is shown (InvalidOperationException if changed after Show()).
            AllowsTransparency = false;
            WindowStyle = WindowStyle.None;   // keep borderless despite AllowsTransparency=false
            var bg = ThemeManager.Current.PanelBackground;
            // Use alpha=220 (same visual weight as TwitchChatOverlay)
            Background = new SolidColorBrush(Color.FromArgb(220, bg.R, bg.G, bg.B));

            _webView = new WebView2
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment   = VerticalAlignment.Stretch,
            };

            // Layout: bandeau on top (WPF), WebView2 fills remaining space
            // WARNING: never place WPF elements overlapping the WebView2 area —
            // the HWND always renders on top of WPF visuals at the same z-layer.
            var root = new Grid();
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            var title = OverlayHelper.MakeTitle("WEB");
            Grid.SetRow(title, 0);
            root.Children.Add(title);

            Grid.SetRow(_webView, 1);
            root.Children.Add(_webView);

            // Wrap in a Border so BaseOverlayWindow can extract background into _bgBorder
            var outer = new Border
            {
                Background   = new SolidColorBrush(Color.FromArgb(220, bg.R, bg.G, bg.B)),
                CornerRadius = new CornerRadius(6),
                Child        = root,
            };
            Content = outer;

            Loaded += OnLoaded;
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            Loaded -= OnLoaded;
            try
            {
                await _webView.EnsureCoreWebView2Async(null);
                _webView.NavigationCompleted += OnNavigationCompleted;
                _initialized = true;
            }
            catch (Exception ex)
            {
                // WebView2 runtime not installed — disable silently
                System.Diagnostics.Debug.WriteLine($"[WebBrowserOverlay] WebView2 init failed: {ex.Message}");
                Settings.IsEnabled = false;
            }
        }

        /// <summary>
        /// Called by MainWindow "Charger" button. Validates URL format then navigates.
        /// Sets IsEnabled=false silently on invalid URL.
        /// </summary>
        public void LoadUrl(string url)
        {
            if (!_initialized || _webView.CoreWebView2 == null)
            {
                System.Diagnostics.Debug.WriteLine("[WebBrowserOverlay] LoadUrl called before initialization");
                return;
            }

            if (!WebBrowserUrlValidator.IsValidWebUrl(url))
            {
                Settings.IsEnabled = false;
                return;
            }

            _webView.CoreWebView2.Navigate(url);
        }

        private void OnNavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            if (!e.IsSuccess)
            {
                // Silent disable — per spec, no error message shown in overlay
                Dispatcher.Invoke(() => Settings.IsEnabled = false);
            }
        }

        public override void UpdateData() { }  // no LMU telemetry dependency

        protected override void OnClosed(EventArgs e)
        {
            _webView.NavigationCompleted -= OnNavigationCompleted;
            _webView.Dispose();
            base.OnClosed(e);
        }
    }
}
