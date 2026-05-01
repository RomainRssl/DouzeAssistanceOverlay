using System.Net.Http;
using Newtonsoft.Json;

namespace LMUOverlay.Services
{
    public record LapEntry(
        string Username,
        string CarName,
        double LapTime,
        double? Sector1,
        double? Sector2,
        double? Sector3);

    public class ClassementService
    {
        private const string LeaderboardUrl = "http://82.165.167.165:3000/leaderboard";

        private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(10) };

        public async Task<Dictionary<string, Dictionary<string, List<LapEntry>>>?> FetchAsync()
        {
            try
            {
                string json = await _http.GetStringAsync(LeaderboardUrl).ConfigureAwait(false);

                var raw = JsonConvert.DeserializeObject<
                    Dictionary<string, Dictionary<string, List<RawLap>>>>(json);

                if (raw == null) return null;

                var result = new Dictionary<string, Dictionary<string, List<LapEntry>>>(StringComparer.OrdinalIgnoreCase);
                foreach (var (circuit, byClass) in raw)
                {
                    result[circuit] = new Dictionary<string, List<LapEntry>>(StringComparer.OrdinalIgnoreCase);
                    foreach (var (cls, laps) in byClass)
                    {
                        result[circuit][cls] = laps
                            .Select(l => new LapEntry(
                                l.Username ?? "",
                                l.CarName  ?? "",
                                l.LapTime,
                                l.Sector1,
                                l.Sector2,
                                l.Sector3))
                            .ToList();
                    }
                }
                return result;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Classement] Fetch failed: {ex.Message}");
                return null;
            }
        }

        private class RawLap
        {
            [JsonProperty("username")] public string? Username { get; set; }
            [JsonProperty("carName")]  public string? CarName  { get; set; }
            [JsonProperty("lapTime")]  public double  LapTime  { get; set; }
            [JsonProperty("sector1")]  public double? Sector1  { get; set; }
            [JsonProperty("sector2")]  public double? Sector2  { get; set; }
            [JsonProperty("sector3")]  public double? Sector3  { get; set; }
        }
    }
}
