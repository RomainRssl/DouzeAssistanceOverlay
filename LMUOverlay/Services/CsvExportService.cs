using System.IO;
using System.Text;
using LMUOverlay.Models;

namespace LMUOverlay.Services
{
    public class CsvExportService
    {
        private static readonly string ExportDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "DouzeAssistance", "Exports");

        public string ExportLapHistory(List<LapRecord> laps, string trackName = "")
        {
            Directory.CreateDirectory(ExportDir);

            string safeName = string.IsNullOrEmpty(trackName) ? "session" :
                new string(trackName.Where(c => !Path.GetInvalidFileNameChars().Contains(c)).ToArray());
            string filename = $"{safeName}_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.csv";
            string path = Path.Combine(ExportDir, filename);

            var sb = new StringBuilder();
            sb.AppendLine("Lap,LapTime,Sector1,Sector2,Sector3,FuelUsed,FuelRemaining,Compound,Timestamp");

            foreach (var lap in laps)
            {
                sb.AppendLine(string.Join(",",
                    lap.LapNumber,
                    lap.LapTime.ToString("F3"),
                    lap.Sector1.ToString("F3"),
                    lap.Sector2.ToString("F3"),
                    lap.Sector3.ToString("F3"),
                    lap.FuelUsed.ToString("F3"),
                    lap.FuelRemaining.ToString("F1"),
                    $"\"{lap.TireCompound}\"",
                    lap.Timestamp.ToString("o")));
            }

            File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
            return path;
        }

        public string ExportTelemetrySnapshot(InputData input, TireData[] tires, FuelData fuel, WeatherData weather)
        {
            Directory.CreateDirectory(ExportDir);
            string path = Path.Combine(ExportDir, $"telemetry_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.csv");

            var sb = new StringBuilder();
            sb.AppendLine("Metric,Value");
            sb.AppendLine($"Speed,{input.Speed:F1}");
            sb.AppendLine($"RPM,{input.RPM:F0}");
            sb.AppendLine($"Gear,{input.Gear}");
            sb.AppendLine($"Throttle,{input.Throttle:F3}");
            sb.AppendLine($"Brake,{input.Brake:F3}");
            sb.AppendLine($"Steering,{input.Steering:F3}");
            sb.AppendLine($"Fuel,{fuel.CurrentFuel:F1}");
            sb.AppendLine($"FuelPerLap,{fuel.FuelPerLap:F3}");
            sb.AppendLine($"AmbientTemp,{weather.AmbientTemp:F1}");
            sb.AppendLine($"TrackTemp,{weather.TrackTemp:F1}");
            sb.AppendLine($"Rain,{weather.Raining:F2}");

            if (tires.Length >= 4)
            {
                string[] names = { "FL", "FR", "RL", "RR" };
                for (int i = 0; i < 4; i++)
                {
                    sb.AppendLine($"Tire{names[i]}_TempInner,{tires[i].Temperature[0]:F1}");
                    sb.AppendLine($"Tire{names[i]}_TempMid,{tires[i].Temperature[1]:F1}");
                    sb.AppendLine($"Tire{names[i]}_TempOuter,{tires[i].Temperature[2]:F1}");
                    sb.AppendLine($"Tire{names[i]}_Wear,{tires[i].Wear:F3}");
                    sb.AppendLine($"Tire{names[i]}_Pressure,{tires[i].Pressure:F1}");
                    sb.AppendLine($"Tire{names[i]}_BrakeTemp,{tires[i].BrakeTemp:F1}");
                }
            }

            File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
            return path;
        }

        public static string GetExportDir() => ExportDir;
    }
}
