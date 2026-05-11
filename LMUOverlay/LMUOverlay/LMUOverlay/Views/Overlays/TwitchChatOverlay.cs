using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using LMUOverlay.Helpers;
using LMUOverlay.Models;
using LMUOverlay.Services;

namespace LMUOverlay.Views.Overlays
{
    public class TwitchChatOverlay : BaseOverlayWindow
    {
        private readonly TwitchChatService _chatService;
        private readonly AppConfig         _config;
        private readonly StackPanel        _messagePanel;
        private readonly ScrollViewer      _scrollViewer;
        private readonly TextBlock         _statusText;

        private static readonly Color TwitchPurple = Color.FromRgb(145, 70, 255);

        public TwitchChatOverlay(DataService ds, OverlaySettings s,
                                  TwitchChatService chatService, AppConfig config)
            : base(ds, s)
        {
            _chatService = chatService;
            _config      = config;

            // Largeur ET hauteur libres : pas de Viewbox, le ScrollViewer remplit l'espace
            UseRawResize = true;

            // ── Structure racine ──────────────────────────────────────────────
            var outer = new Border
            {
                Background          = BrushCache.Get(Color.FromArgb(200, ThemeManager.Current.PanelBackground.R, ThemeManager.Current.PanelBackground.G, ThemeManager.Current.PanelBackground.B)),
                CornerRadius        = new CornerRadius(6),
                Padding             = new Thickness(10, 8, 10, 8),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment   = VerticalAlignment.Stretch,
            };

            // Grid à 3 lignes : header (Auto) + séparateur (Auto) + messages (*)
            var root = new Grid();
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            // ── En-tête : titre + statut ──────────────────────────────────────
            var header = new Grid { Margin = new Thickness(0, 0, 0, 5) };
            header.ColumnDefinitions.Add(new ColumnDefinition());
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            header.Children.Add(new TextBlock
            {
                Text              = "TWITCH",
                FontSize          = 10,
                FontFamily        = OverlayHelper.FontSegoeUISB,
                FontWeight        = FontWeights.Bold,
                Foreground        = new SolidColorBrush(TwitchPurple),
                VerticalAlignment = VerticalAlignment.Center,
            });

            _statusText = new TextBlock
            {
                FontSize            = 9,
                FontFamily          = OverlayHelper.FontSegoeUISB,
                VerticalAlignment   = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Right,
            };
            Grid.SetColumn(_statusText, 1);
            header.Children.Add(_statusText);

            Grid.SetRow(header, 0);
            root.Children.Add(header);

            // Séparateur teinté violet Twitch
            var sep = new Border
            {
                Height     = 1,
                Background = new SolidColorBrush(Color.FromArgb(80, 145, 70, 255)),
                Margin     = new Thickness(0, 0, 0, 5),
            };
            Grid.SetRow(sep, 1);
            root.Children.Add(sep);

            // ── Liste des messages ────────────────────────────────────────────
            _messagePanel = new StackPanel
            {
                Orientation         = Orientation.Vertical,
                HorizontalAlignment = HorizontalAlignment.Stretch,
            };
            _scrollViewer = new ScrollViewer
            {
                Content                       = _messagePanel,
                VerticalScrollBarVisibility   = ScrollBarVisibility.Hidden,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                HorizontalAlignment           = HorizontalAlignment.Stretch,
                VerticalAlignment             = VerticalAlignment.Stretch,
            };
            Grid.SetRow(_scrollViewer, 2);
            root.Children.Add(_scrollViewer);

            outer.Child = root;
            Content     = outer;

            // ── Connexion aux événements ──────────────────────────────────────
            _chatService.MessageReceived   += OnMessageReceived;
            _chatService.ConnectionChanged += OnConnectionChanged;

            UpdateStatus();
        }

        // ── Handlers ─────────────────────────────────────────────────────────

        private void OnMessageReceived(TwitchMessage msg) =>
            Dispatcher.Invoke(() => AddMessage(msg));

        private void OnConnectionChanged(bool _) =>
            Dispatcher.Invoke(UpdateStatus);

        // ── Rendu des messages ────────────────────────────────────────────────

        private void AddMessage(TwitchMessage msg)
        {
            int maxMessages = Math.Max(1, _config.Twitch.MaxMessages);

            // Supprimer le plus ancien si on dépasse la limite
            while (_messagePanel.Children.Count >= maxMessages)
                _messagePanel.Children.RemoveAt(0);

            // Parser la couleur du pseudo
            Color nameColor;
            try   { nameColor = (Color)ColorConverter.ConvertFromString(msg.Color)!; }
            catch { nameColor = ThemeManager.Current.TextSecondary; }

            var tb = new TextBlock
            {
                TextWrapping = TextWrapping.Wrap,
                Margin       = new Thickness(0, 1, 0, 1),
                FontSize     = 11,
                FontFamily   = OverlayHelper.FontSegoeUISB,
            };

            // Pseudo en couleur Twitch
            tb.Inlines.Add(new Run(msg.Username + "  ")
            {
                Foreground = BrushCache.Get(nameColor),
                FontWeight = FontWeights.SemiBold,
            });

            // Texte du message
            tb.Inlines.Add(new Run(msg.Text)
            {
                Foreground = BrushCache.Get(ThemeManager.Current.TextPrimary),
            });

            _messagePanel.Children.Add(tb);
            _scrollViewer.ScrollToBottom();
        }

        // ── Statut de connexion ───────────────────────────────────────────────

        private void UpdateStatus()
        {
            string channel = _config.Twitch.Channel;
            var tm = ThemeManager.Current;

            if (string.IsNullOrEmpty(channel))
            {
                _statusText.Text       = "non configuré";
                _statusText.Foreground = BrushCache.Get(tm.TextMuted);
            }
            else if (_chatService.IsConnected)
            {
                _statusText.Text       = $"● #{channel}";
                _statusText.Foreground = BrushCache.Get(tm.StateGood);
            }
            else
            {
                _statusText.Text       = "○ connexion…";
                _statusText.Foreground = BrushCache.Get(tm.StateWarn);
            }
        }

        // ── Overrides ─────────────────────────────────────────────────────────

        public override void UpdateData() { }

        protected override void OnClosed(EventArgs e)
        {
            _chatService.MessageReceived   -= OnMessageReceived;
            _chatService.ConnectionChanged -= OnConnectionChanged;
            base.OnClosed(e);
        }
    }
}
