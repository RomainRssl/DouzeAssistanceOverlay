using System.Net.WebSockets;
using System.Text;
using LMUOverlay.Models;

namespace LMUOverlay.Services
{
    /// <summary>
    /// Connexion anonyme en lecture seule au chat Twitch via IRC-over-WebSocket.
    /// Aucune clé API ni compte Twitch requis pour les chaînes publiques.
    /// </summary>
    public class TwitchChatService : IDisposable
    {
        // ── Events ────────────────────────────────────────────────────────────
        public event Action<TwitchMessage>? MessageReceived;
        public event Action<bool>?          ConnectionChanged; // true = connecté

        // ── State ─────────────────────────────────────────────────────────────
        public bool   IsConnected { get; private set; }
        public string Channel     { get; private set; } = "";

        private ClientWebSocket?         _ws;
        private CancellationTokenSource? _cts;
        private volatile bool            _shouldReconnect;
        private volatile bool            _disposed;

        // Couleurs Twitch par défaut (quand l'utilisateur n'a pas choisi de couleur)
        private static readonly string[] DefaultColors =
        {
            "#FF4500", "#2E8B57", "#DAA520", "#FF69B4", "#5F9EA0",
            "#1E90FF", "#FF7F50", "#9ACD32", "#8A2BE2", "#00FF7F"
        };

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>
        /// Démarre la connexion au canal Twitch indiqué.
        /// Si déjà connecté au même canal, ne fait rien.
        /// Si changement de canal, déconnecte et reconnecte.
        /// </summary>
        public void Connect(string channel)
        {
            if (string.IsNullOrWhiteSpace(channel)) return;

            string normalized = channel.TrimStart('#').ToLowerInvariant().Trim();
            if (normalized == Channel && IsConnected) return;

            Channel          = normalized;
            _shouldReconnect = true;

            // Annule la connexion existante → ConnectLoop redémarre
            _cts?.Cancel();

            Task.Run(ConnectLoop);
        }

        /// <summary>Déconnecte et arrête la reconnexion automatique.</summary>
        public void Disconnect()
        {
            _shouldReconnect = false;
            Channel          = "";
            _cts?.Cancel();
        }

        // ── Boucle de connexion ───────────────────────────────────────────────

        private async Task ConnectLoop()
        {
            while (_shouldReconnect && !_disposed)
            {
                try
                {
                    await ConnectOnce();
                }
                catch (OperationCanceledException) { /* déconnexion intentionnelle */ }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[TwitchChat] Erreur: {ex.Message}");
                }

                SetConnected(false);

                if (_shouldReconnect && !_disposed)
                    await Task.Delay(5000); // 5s avant nouvelle tentative
            }
        }

        private async Task ConnectOnce()
        {
            // Nettoyage de la connexion précédente
            _cts?.Cancel();
            try { _ws?.Dispose(); } catch { }

            _cts = new CancellationTokenSource();
            _ws  = new ClientWebSocket();

            await _ws.ConnectAsync(new Uri("wss://irc-ws.chat.twitch.tv:443"), _cts.Token);

            // Handshake IRC anonyme (justinfan = utilisateur anonyme officiel Twitch)
            int rand = Random.Shared.Next(10000, 99999);
            await Send("PASS SCHMOOPIIE");
            await Send($"NICK justinfan{rand}");
            await Send("CAP REQ :twitch.tv/tags"); // active les métadonnées (couleur, display-name…)
            await Send($"JOIN #{Channel}");

            SetConnected(true);

            // Boucle de réception
            var buffer = new byte[8192];
            var sb     = new StringBuilder();

            while (_ws.State == WebSocketState.Open && !_cts.Token.IsCancellationRequested)
            {
                WebSocketReceiveResult result;
                try
                {
                    result = await _ws.ReceiveAsync(new ArraySegment<byte>(buffer), _cts.Token);
                }
                catch (OperationCanceledException) { break; }

                if (result.MessageType == WebSocketMessageType.Close) break;

                sb.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
                if (!result.EndOfMessage) continue;

                string raw = sb.ToString();
                sb.Clear();

                foreach (var line in raw.Split('\n', StringSplitOptions.RemoveEmptyEntries))
                    ProcessLine(line.TrimEnd('\r'));
            }
        }

        private async Task Send(string message)
        {
            if (_ws?.State != WebSocketState.Open) return;
            try
            {
                var bytes = Encoding.UTF8.GetBytes(message + "\r\n");
                await _ws.SendAsync(new ArraySegment<byte>(bytes),
                    WebSocketMessageType.Text, true,
                    _cts?.Token ?? CancellationToken.None);
            }
            catch { /* ignore — la boucle de réception détectera la déconnexion */ }
        }

        // ── Parsing IRC ───────────────────────────────────────────────────────

        private void ProcessLine(string line)
        {
            if (string.IsNullOrEmpty(line)) return;

            // Répondre aux PING pour maintenir la connexion vivante
            if (line.StartsWith("PING"))
            {
                string target = line.Length > 5 ? line[5..] : ":tmi.twitch.tv";
                _ = Send($"PONG {target}");
                return;
            }

            if (!line.Contains("PRIVMSG")) return;

            // ── Extraire les tags (@key=val;key=val…) ─────────────────────
            string tags      = "";
            string remainder = line;

            if (line.StartsWith("@"))
            {
                int sp = line.IndexOf(' ');
                if (sp < 0) return;
                tags      = line[1..sp];
                remainder = line[(sp + 1)..];
            }

            // ── Extraire le texte du message ──────────────────────────────
            int privIdx = remainder.IndexOf("PRIVMSG");
            if (privIdx < 0) return;

            string afterPrivmsg = remainder[(privIdx + 7)..].TrimStart();
            int colonIdx = afterPrivmsg.IndexOf(':');
            if (colonIdx < 0) return;

            string messageText = afterPrivmsg[(colonIdx + 1)..].Trim();
            if (string.IsNullOrEmpty(messageText)) return;

            // ── Parser les valeurs des tags ───────────────────────────────
            string displayName = "";
            string color       = "";

            foreach (var tag in tags.Split(';'))
            {
                if (tag.StartsWith("display-name="))
                    displayName = tag[13..];
                else if (tag.StartsWith("color="))
                    color = tag[6..];
            }

            // Fallback : extraire le pseudo depuis le préfixe IRC ":user!user@…"
            if (string.IsNullOrEmpty(displayName))
            {
                int colon = remainder.IndexOf(':');
                int bang   = remainder.IndexOf('!');
                if (colon >= 0 && bang > colon)
                    displayName = remainder[(colon + 1)..bang];
            }

            if (string.IsNullOrEmpty(displayName)) displayName = "?";

            // Couleur par défaut déterministe (même logique que Twitch)
            if (string.IsNullOrEmpty(color))
                color = DefaultColors[Math.Abs(displayName.GetHashCode()) % DefaultColors.Length];

            MessageReceived?.Invoke(new TwitchMessage
            {
                Username = displayName,
                Color    = color,
                Text     = messageText,
                Time     = DateTime.Now,
            });
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private void SetConnected(bool connected)
        {
            if (IsConnected == connected) return;
            IsConnected = connected;
            ConnectionChanged?.Invoke(connected);
        }

        // ── Dispose ───────────────────────────────────────────────────────────

        public void Dispose()
        {
            _disposed        = true;
            _shouldReconnect = false;
            _cts?.Cancel();
            try { _ws?.Dispose(); } catch { }
        }
    }
}
