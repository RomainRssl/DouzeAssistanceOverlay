using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
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

            // Largeur fixe + hauteur auto : pas de Viewbox, le texte wrappera correctement
            UseWidthOnlyResize = true;

            // ── Structure racine ──────────────────────────────────────────────
            var outer = new Border
            {
                Background          = new SolidColorBrush(Color.FromArgb(200, 10, 12, 18)),
                CornerRadius        = new CornerRadius(6),
                Padding             = new Thickness(10, 8, 10, 8),
                HorizontalAlignment = HorizontalAlignment.Stretch,
            };

            var root = new StackPanel();

            // ── En-tête : titre + statut ──────────────────────────────────────
            var header = new Grid { Margin = new Thickness(0, 0, 0, 5) };
            header.ColumnDefinitions.Add(new ColumnDefinition());
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            header.Children.Add(new TextBlock
            {
                Text              = "TWITCH",
                FontSize          = 10,
                FontFamily        = new FontFamily("Segoe UI"),
                FontWeight        = FontWeights.Bold,
                Foreground        = new SolidColorBrush(TwitchPurple),
                VerticalAlignment = VerticalAlignment.Center,
            });

            _statusText = new TextBlock
            {
                FontSize            = 9,
                FontFamily          = new FontFamily("Segoe UI"),
                VerticalAlignment   = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Right,
            };
            Grid.SetColumn(_statusText, 1);
            header.Children.Add(_statusText);
            root.Children.Add(header);

            // Séparateur teinté violet Twitch
            root.Children.Add(new Border
            {
                Height     = 1,
                Background = new SolidColorBrush(Color.FromArgb(80, 145, 70, 255)),
                Margin     = new Thickness(0, 0, 0, 5),
            });

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
                MaxHeight                     = 400,
            };
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
            catch { nameColor = Color.FromRgb(200, 200, 200); }

            var tb = new TextBlock
            {
                TextWrapping = TextWrapping.Wrap,
                Margin       = new Thickness(0, 1, 0, 1),
                FontSize     = 11,
                FontFamily   = new FontFamily("Segoe UI"),
            };

            // Pseudo en couleur Twitch
            tb.Inlines.Add(new Run(msg.Username + "  ")
            {
                Foreground = new SolidColorBrush(nameColor),
                FontWeight = FontWeights.SemiBold,
            });

            // Texte du message en blanc
            tb.Inlines.Add(new Run(msg.Text)
            {
                Foreground = new SolidColorBrush(Color.FromRgb(225, 225, 225)),
            });

            _messagePanel.Children.Add(tb);
            _scrollViewer.ScrollToBottom();
        }

        // ── Statut de connexion ───────────────────────────────────────────────

        private void UpdateStatus()
        {
            string channel = _config.Twitch.Channel;

            if (string.IsNullOrEmpty(channel))
            {
                _statusText.Text       = "non configuré";
                _statusText.Foreground = new SolidColorBrush(Color.FromRgb(100, 60, 60));
            }
            else if (_chatService.IsConnected)
            {
                _statusText.Text       = $"● #{channel}";
                _statusText.Foreground = new SolidColorBrush(Color.FromRgb(34, 197, 94));
            }
            else
            {
                _statusText.Text       = "○ connexion…";
                _statusText.Foreground = new SolidColorBrush(Color.FromRgb(200, 160, 0));
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
