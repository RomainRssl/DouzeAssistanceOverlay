using System.IO;
using System.Xml.Linq;

namespace LMUOverlay.Services
{
    public class ChronoEntry
    {
        public string TrackName   { get; set; } = "";
        public string CarClass    { get; set; } = "";  // "GT3", "LMP2", "LMP3", "Hypercar"
        public string CarName     { get; set; } = "";
        public double BestLapSec  { get; set; }
    }

    /// <summary>
    /// Parses LMU (rFactor2) result XML files.
    /// Real format: &lt;rFactorXML&gt; &gt; &lt;RaceResults&gt; &gt; sessions &gt; &lt;Driver&gt; blocks.
    /// </summary>
    public static class ChronoService
    {
        private static readonly string[] ClassOrder = { "GT3", "LMP2", "LMP3", "Hypercar" };

        // ── Class normalisation ──────────────────────────────────────────────
        public static string NormalizeClass(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return "Other";
            string u = raw.Trim().ToUpperInvariant();

            if (u.Contains("GT3"))  return "GT3";
            if (u.Contains("LMP2")) return "LMP2";
            if (u.Contains("LMP3")) return "LMP3";
            if (u.Contains("HYPERCAR") || u.Contains("LMH") || u.Contains("LMDH"))
                return "Hypercar";

            return raw.Trim();
        }

        public static IReadOnlyList<string> GetClassOrder() => ClassOrder;

        // ── Main entry point ─────────────────────────────────────────────────
        // resultsFolder is used AS-IS (no path manipulation).
        public static List<ChronoEntry> LoadResults(string resultsFolder, string playerName)
        {
            var raw = new List<ChronoEntry>();

            if (!Directory.Exists(resultsFolder) || string.IsNullOrWhiteSpace(playerName))
                return raw;

            string[] files;
            try { files = Directory.GetFiles(resultsFolder, "*.xml", SearchOption.TopDirectoryOnly); }
            catch { return raw; }

            foreach (string file in files)
            {
                try { ParseFile(file, playerName, raw); }
                catch { /* skip malformed / locked files */ }
            }

            // Keep only the personal best per (track, car)
            return raw
                .GroupBy(e => (e.TrackName, e.CarName))
                .Select(g => g.MinBy(e => e.BestLapSec)!)
                .ToList();
        }

        // ── Parse one LMU result XML ─────────────────────────────────────────
        private static void ParseFile(string path, string playerName, List<ChronoEntry> output)
        {
            XDocument doc = XDocument.Load(path);
            XElement root = doc.Root ?? throw new InvalidOperationException("Empty XML");

            // Real LMU format: <rFactorXML> wraps <RaceResults>
            XElement results = root.Name.LocalName == "rFactorXML"
                ? (root.Element("RaceResults") ?? root)
                : root;

            // TrackCourse is the specific layout (includes variants like "Bahrain Outer Circuit").
            // TrackVenue is the generic venue name — use as fallback only.
            string trackName =
                ((string?)results.Element("TrackCourse") ??
                 (string?)results.Element("TrackVenue") ??
                 (string?)results.Element("TrackName") ??
                 Path.GetFileNameWithoutExtension(path)).Trim();

            // <Driver> elements can appear inside any session sub-element
            // (Practice1, Qualify, Race1, etc.) — search the whole document.
            foreach (XElement driver in results.Descendants("Driver"))
            {
                string name = ((string?)driver.Element("Name") ?? "").Trim();
                if (!NameMatches(name, playerName)) continue;

                // Car info
                string carName =
                    ((string?)driver.Element("CarType") ??       // LMU primary field
                     (string?)driver.Element("VehicleName") ??
                     (string?)driver.Element("Vehicle") ?? "Unknown").Trim();

                string carClassRaw =
                    ((string?)driver.Element("CarClass") ??
                     (string?)driver.Element("VehicleClass") ??
                     (string?)driver.Element("Class") ?? "").Trim();
                string carClass = NormalizeClass(carClassRaw);

                // Best lap: try <BestLapTime> first, then compute from <Lap> elements
                double best = ParseBestLapField(driver);
                if (best <= 0)
                    best = ComputeBestFromLaps(driver);
                if (best <= 0) continue;

                output.Add(new ChronoEntry
                {
                    TrackName  = trackName,
                    CarClass   = carClass,
                    CarName    = carName,
                    BestLapSec = best
                });
            }
        }

        private static bool NameMatches(string driverName, string playerName)
        {
            if (string.IsNullOrEmpty(driverName)) return false;
            return string.Equals(driverName, playerName, StringComparison.OrdinalIgnoreCase)
                || driverName.Contains(playerName, StringComparison.OrdinalIgnoreCase);
        }

        // <BestLapTime>99.6029</BestLapTime> — value in seconds
        private static double ParseBestLapField(XElement driver)
        {
            foreach (string tag in new[] { "BestLapTime", "BestLap", "BestTime" })
            {
                var el = driver.Element(tag);
                if (el == null) continue;
                if (double.TryParse(el.Value,
                    System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out double t) && t > 0)
                    return t;
            }
            return -1;
        }

        // <Lap num="2" ...>99.6029</Lap>  — text content is seconds; "--.----" = invalid
        private static double ComputeBestFromLaps(XElement driver)
        {
            double best = double.MaxValue;
            bool found = false;

            foreach (XElement lap in driver.Elements("Lap"))
            {
                string txt = (lap.Value ?? "").Trim();
                if (txt.StartsWith("-")) continue; // invalid/DNF lap

                if (double.TryParse(txt,
                    System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out double t) && t > 0 && t < best)
                {
                    best = t;
                    found = true;
                }
            }

            return found ? best : -1;
        }

        // ── Lap time formatter ────────────────────────────────────────────────
        public static string FormatLap(double seconds)
        {
            if (seconds <= 0) return "";
            var ts = TimeSpan.FromSeconds(seconds);
            return $"{(int)ts.TotalMinutes}:{ts.Seconds:D2}.{ts.Milliseconds:D3}";
        }
    }
}
