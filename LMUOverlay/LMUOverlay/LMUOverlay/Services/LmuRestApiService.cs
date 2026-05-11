using System.Net.Http;
using System.Threading;
using Newtonsoft.Json.Linq;

namespace LMUOverlay.Services
{
    /// <summary>
    /// Polls the LMU REST API (localhost:6397) to retrieve virtual energy data.
    /// Virtual energy (VE) is a regulated energy budget per stint — completely
    /// separate from mBatteryChargeFraction (physical battery SOC).
    /// Endpoint: GET /rest/garage/UIScreen/RepairAndRefuel
    /// JSON: { "fuelInfo": { "currentVirtualEnergy": 850, "maxVirtualEnergy": 1000 } }
    /// </summary>
    public class LmuRestApiService : IDisposable
    {
        private readonly HttpClient _http;
        private Timer? _pollTimer;

        // Virtual Energy (absolute values, same unit as the game)
        public double CurrentVirtualEnergy { get; private set; } = -1;
        public double MaxVirtualEnergy     { get; private set; } = -1;

        /// <summary>VE as a percentage 0–100. -1 if not available.</summary>
        public double VirtualEnergyPct =>
            MaxVirtualEnergy > 0
                ? Math.Clamp(CurrentVirtualEnergy / MaxVirtualEnergy * 100.0, 0, 100)
                : -1;

        /// <summary>True when the REST API returned valid VE data at least once.</summary>
        public bool IsAvailable { get; private set; }

        public LmuRestApiService()
        {
            _http = new HttpClient
            {
                BaseAddress = new Uri("http://localhost:6397/"),
                Timeout     = TimeSpan.FromSeconds(1)
            };
            // Poll every 1 second — VE changes slowly, no need for higher frequency
            _pollTimer = new Timer(Poll, null, TimeSpan.Zero, TimeSpan.FromSeconds(1));
        }

        private async void Poll(object? state)
        {
            try
            {
                var json = await _http.GetStringAsync("rest/garage/UIScreen/RepairAndRefuel");
                var root = JObject.Parse(json);

                if (root["fuelInfo"] is JObject fi)
                {
                    double cur = fi["currentVirtualEnergy"]?.Value<double>() ?? -1;
                    double max = fi["maxVirtualEnergy"]?.Value<double>()     ?? -1;

                    if (cur >= 0 && max > 0)
                    {
                        CurrentVirtualEnergy = cur;
                        MaxVirtualEnergy     = max;
                        IsAvailable          = true;
                    }
                    else
                    {
                        IsAvailable = false;
                    }
                }
            }
            catch
            {
                // REST API not reachable (LMU not running, or car has no VE)
                IsAvailable = false;
            }
        }

        public void Dispose()
        {
            _pollTimer?.Dispose();
            _pollTimer = null;
            _http.Dispose();
        }
    }
}
