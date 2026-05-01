using System.Net.Http;
using System.Text;
using LMUOverlay.Models;
using Newtonsoft.Json;

namespace LMUOverlay.Services
{
    public class LeaderboardService
    {
        private const string SubmitUrl = "http://82.165.167.165:3000/submit";

        private static readonly HttpClient _http = new()
        {
            Timeout = TimeSpan.FromSeconds(8)
        };

        private readonly GeneralSettings _settings;

        public LeaderboardService(GeneralSettings settings)
        {
            _settings = settings;

            if (string.IsNullOrEmpty(_settings.LeaderboardToken))
                _settings.LeaderboardToken = Guid.NewGuid().ToString("N");
        }

        public void SubmitLap(
            string circuit,
            string carClass,
            string carName,
            double lapTime,
            double sector1,
            double sector2,
            double sector3,
            string appVersion)
        {
            if (!_settings.SendToLeaderboard)                             return;
            if (string.IsNullOrWhiteSpace(_settings.LeaderboardPrenom))  return;
            if (string.IsNullOrWhiteSpace(_settings.LeaderboardNom))     return;
            if (lapTime <= 0)                                             return;

            string prenom  = _settings.LeaderboardPrenom;
            string nom     = _settings.LeaderboardNom;
            string discord = _settings.LeaderboardDiscord;
            string token   = _settings.LeaderboardToken;

            Task.Run(async () =>
            {
                try
                {
                    var payload = new
                    {
                        prenom,
                        nom,
                        discord,
                        token,
                        circuit,
                        carClass,
                        carName,
                        lapTime,
                        sector1 = sector1 > 0 ? sector1 : (double?)null,
                        sector2 = sector2 > 0 ? sector2 : (double?)null,
                        sector3 = sector3 > 0 ? sector3 : (double?)null,
                        version = appVersion
                    };

                    var json    = JsonConvert.SerializeObject(payload);
                    var content = new StringContent(json, Encoding.UTF8, "application/json");

                    using var resp = await _http.PostAsync(SubmitUrl, content).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[Leaderboard] Envoi échoué : {ex.Message}");
                }
            }).ContinueWith(t =>
            {
                if (t.IsFaulted)
                    System.Diagnostics.Debug.WriteLine($"[Leaderboard] Exception non capturée : {t.Exception?.Message}");
            }, TaskContinuationOptions.OnlyOnFaulted);
        }
    }
}
