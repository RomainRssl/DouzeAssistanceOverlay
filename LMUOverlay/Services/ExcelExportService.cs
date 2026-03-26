using System.IO;
using ClosedXML.Excel;
using LMUOverlay.Models;

namespace LMUOverlay.Services
{
    public class ExcelExportService
    {
        private static readonly string ExportDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "DouzeAssistance", "Exports");

        // ====================================================================
        // EXPORT
        // ====================================================================

        public string Export(List<LapRecord> laps, List<LapTrace> traces, string trackName)
        {
            Directory.CreateDirectory(ExportDir);
            string safe  = string.Join("_", trackName.Split(Path.GetInvalidFileNameChars()));
            string stamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm");
            string path  = Path.Combine(ExportDir, $"Telemetry_{safe}_{stamp}.xlsx");

            using var wb = new XLWorkbook();

            // Sheet 1 – lap summary
            var ws = wb.Worksheets.Add("Résumé");
            ws.Cell(1, 1).Value = "Tour";
            ws.Cell(1, 2).Value = "Temps";
            ws.Cell(1, 3).Value = "S1";
            ws.Cell(1, 4).Value = "S2";
            ws.Cell(1, 5).Value = "S3";
            ws.Cell(1, 6).Value = "Compound";
            ws.Cell(1, 7).Value = "Carburant utilisé";
            ws.Cell(1, 8).Value = "Carburant restant";
            ws.Cell(1, 9).Value = "Horodatage";

            var header = ws.Range(1, 1, 1, 9);
            header.Style.Font.Bold = true;
            header.Style.Fill.BackgroundColor = XLColor.FromHtml("#162020");
            header.Style.Font.FontColor = XLColor.White;

            for (int i = 0; i < laps.Count; i++)
            {
                var lap = laps[i];
                int row = i + 2;
                ws.Cell(row, 1).Value = lap.LapNumber;
                ws.Cell(row, 2).Value = FormatTime(lap.LapTime);
                ws.Cell(row, 3).Value = FormatTime(lap.Sector1);
                ws.Cell(row, 4).Value = FormatTime(lap.Sector2);
                ws.Cell(row, 5).Value = FormatTime(lap.Sector3);
                ws.Cell(row, 6).Value = lap.TireCompound;
                ws.Cell(row, 7).Value = Math.Round(lap.FuelUsed, 2);
                ws.Cell(row, 8).Value = Math.Round(lap.FuelRemaining, 2);
                ws.Cell(row, 9).Value = lap.Timestamp.ToString("HH:mm:ss");
            }
            ws.Columns().AdjustToContents();

            // Sheet per lap trace
            foreach (var trace in traces)
            {
                string sheetName = $"Lap_{trace.LapNumber}";
                var wt = wb.Worksheets.Add(sheetName);
                wt.Cell(1, 1).Value = "TrackPos";
                wt.Cell(1, 2).Value = "Vitesse";
                wt.Cell(1, 3).Value = "Gaz";
                wt.Cell(1, 4).Value = "Frein";
                wt.Cell(1, 5).Value = "Rapport";
                wt.Cell(1, 6).Value = "RPM";
                wt.Cell(1, 7).Value = "Direction";
                wt.Cell(1, 8).Value = "Elapsed";
                wt.Cell(1, 9).Value = "LapTime";
                wt.Cell(1, 10).Value = "Compound";

                var th = wt.Range(1, 1, 1, 10);
                th.Style.Font.Bold = true;
                th.Style.Fill.BackgroundColor = XLColor.FromHtml("#162020");
                th.Style.Font.FontColor = XLColor.White;

                // Meta in row 2
                wt.Cell(2, 9).Value = Math.Round(trace.LapTime, 3);
                wt.Cell(2, 10).Value = trace.Compound;

                for (int i = 0; i < trace.Points.Count; i++)
                {
                    var p = trace.Points[i];
                    int row = i + 2;
                    wt.Cell(row, 1).Value = Math.Round(p.TrackPos, 4);
                    wt.Cell(row, 2).Value = Math.Round(p.Speed, 1);
                    wt.Cell(row, 3).Value = Math.Round(p.Throttle, 3);
                    wt.Cell(row, 4).Value = Math.Round(p.Brake, 3);
                    wt.Cell(row, 5).Value = p.Gear;
                    wt.Cell(row, 6).Value = Math.Round(p.RPM, 0);
                    wt.Cell(row, 7).Value = Math.Round(p.Steering, 3);
                    wt.Cell(row, 8).Value = Math.Round(p.Elapsed, 3);
                }
                wt.Columns(1, 8).AdjustToContents();
            }

            wb.SaveAs(path);
            return path;
        }

        // ====================================================================
        // IMPORT
        // ====================================================================

        public (List<LapRecord> laps, List<LapTrace> traces) Import(string path)
        {
            var laps   = new List<LapRecord>();
            var traces = new List<LapTrace>();

            using var wb = new XLWorkbook(path);

            // Sheet "Résumé"
            if (wb.TryGetWorksheet("Résumé", out var ws))
            {
                int row = 2;
                while (!ws.Cell(row, 1).IsEmpty())
                {
                    laps.Add(new LapRecord
                    {
                        LapNumber      = ws.Cell(row, 1).GetValue<int>(),
                        LapTime        = ParseTime(ws.Cell(row, 2).GetString()),
                        Sector1        = ParseTime(ws.Cell(row, 3).GetString()),
                        Sector2        = ParseTime(ws.Cell(row, 4).GetString()),
                        Sector3        = ParseTime(ws.Cell(row, 5).GetString()),
                        TireCompound   = ws.Cell(row, 6).GetString(),
                        FuelUsed       = ws.Cell(row, 7).GetValue<double>(),
                        FuelRemaining  = ws.Cell(row, 8).GetValue<double>()
                    });
                    row++;
                }
            }

            // Lap sheets
            foreach (var wt in wb.Worksheets.Where(s => s.Name.StartsWith("Lap_")))
            {
                double lapTime = 0;
                string compound = "";
                if (!wt.Cell(2, 9).IsEmpty()) lapTime  = wt.Cell(2, 9).GetValue<double>();
                if (!wt.Cell(2, 10).IsEmpty()) compound = wt.Cell(2, 10).GetString();

                var trace = new LapTrace
                {
                    LapNumber = int.TryParse(wt.Name[4..], out var n) ? n : 0,
                    LapTime   = lapTime,
                    Compound  = compound
                };

                int row = 2;
                while (!wt.Cell(row, 1).IsEmpty())
                {
                    trace.Points.Add(new TelemetryPoint
                    {
                        TrackPos = wt.Cell(row, 1).GetValue<double>(),
                        Speed    = wt.Cell(row, 2).GetValue<double>(),
                        Throttle = wt.Cell(row, 3).GetValue<double>(),
                        Brake    = wt.Cell(row, 4).GetValue<double>(),
                        Gear     = wt.Cell(row, 5).GetValue<int>(),
                        RPM      = wt.Cell(row, 6).GetValue<double>(),
                        Steering = wt.Cell(row, 7).GetValue<double>(),
                        Elapsed  = wt.Cell(row, 8).GetValue<double>()
                    });
                    row++;
                }
                traces.Add(trace);
            }

            return (laps, traces);
        }

        // ====================================================================
        // HELPERS
        // ====================================================================

        private static string FormatTime(double s)
        {
            if (s <= 0) return "-";
            int m = (int)(s / 60);
            double sec = s - m * 60;
            return $"{m}:{sec:00.000}";
        }

        private static double ParseTime(string t)
        {
            if (string.IsNullOrEmpty(t) || t == "-") return 0;
            var parts = t.Split(':');
            if (parts.Length == 2 && int.TryParse(parts[0], out int m) &&
                double.TryParse(parts[1], System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out double s))
                return m * 60 + s;
            return 0;
        }
    }
}
