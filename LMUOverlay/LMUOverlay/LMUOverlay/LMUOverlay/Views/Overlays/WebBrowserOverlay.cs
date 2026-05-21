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
        private readonly WebView2   _webView;
        private readonly TextBlock  _statusBar;
        private bool   _initialized;
        private bool   _initFailed;
        private string _pendingUrl = "";

        public WebBrowserOverlay(DataService ds, OverlaySettings s) : base(ds, s)
        {
            UseRawResize = true;

            // CRITICAL: WebView2 is HWND-based (HwndHost). Incompatible with
            // AllowsTransparency=true set by BaseOverlayWindow constructor.
            // MUST override before window is shown (InvalidOperationException if changed after Show()).
            AllowsTransparency = false;
            WindowStyle = WindowStyle.None;
            var bg = ThemeManager.Current.PanelBackground;
            Background = new SolidColorBrush(Color.FromArgb(220, bg.R, bg.G, bg.B));

            _webView = new WebView2
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment   = VerticalAlignment.Stretch,
            };

            // Status bar sits ABOVE the WebView2 row — safe from HWND z-order issue.
            // Hidden by default, shown only during loading or on error.
            _statusBar = new TextBlock
            {
                FontFamily  = new System.Windows.Media.FontFamily("Segoe UI"),
                FontSize    = 10,
                Foreground  = new SolidColorBrush(Color.FromRgb(163, 163, 163)),
                Background  = new SolidColorBrush(Color.FromArgb(180, 20, 20, 20)),
                Padding     = new Thickness(8, 4, 8, 4),
                TextWrapping = TextWrapping.NoWrap,
                Visibility  = Visibility.Collapsed,
            };

            // Layout: title | status bar (optional) | WebView2
            // WARNING: never place WPF elements overlapping the WebView2 area —
            // the HWND always renders on top of WPF visuals at the same z-layer.
            var root = new Grid();
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            var title = OverlayHelper.MakeTitle("WEB");
            Grid.SetRow(title, 0);
            root.Children.Add(title);

            Grid.SetRow(_statusBar, 1);
            root.Children.Add(_statusBar);

            Grid.SetRow(_webView, 2);
            root.Children.Add(_webView);

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
                SetStatus("Initialisation WebView2...");
                await _webView.EnsureCoreWebView2Async(null);
                _webView.NavigationCompleted += OnNavigationCompleted;
                _initialized = true;
                HideStatus();

                if (!string.IsNullOrEmpty(_pendingUrl))
                {
                    _webView.CoreWebView2.Navigate(_pendingUrl);
                    _pendingUrl = "";
                }
            }
            catch (Exception ex)
            {
                _initFailed = true;
                SetStatus($"WebView2 indisponible : {ex.Message} — Installer le runtime sur https://aka.ms/webview2");
            }
        }

        public void LoadUrl(string url)
        {
            if (_initFailed)
            {
                SetStatus("WebView2 indisponible — voir le message d'erreur dans l'overlay");
                return;
            }

            if (!WebBrowserUrlValidator.IsValidWebUrl(url))
            {
                SetStatus("URL invalide — doit commencer par http:// ou https://");
                return;
            }

            if (!_initialized)
            {
                _pendingUrl = url;
                SetStatus("Initialisation en cours, l'URL sera chargee automatiquement...");
                return;
            }

            SetStatus("Chargement...");
            _webView.CoreWebView2.Navigate(url);
        }

        private void OnNavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                if (e.IsSuccess)
                    HideStatus();
                else
                    SetStatus($"Erreur de navigation (code {e.WebErrorStatus})");
            });
        }

        private void SetStatus(string msg)
            => Dispatcher.Invoke(() => { _statusBar.Text = msg; _statusBar.Visibility = Visibility.Visible; });

        private void HideStatus()
            => Dispatcher.Invoke(() => _statusBar.Visibility = Visibility.Collapsed);

        public override void UpdateData() { }  // no LMU telemetry dependency

        protected override void OnClosed(EventArgs e)
        {
            _webView.NavigationCompleted -= OnNavigationCompleted;
            _webView.Dispose();
            base.OnClosed(e);
        }
    }
}
