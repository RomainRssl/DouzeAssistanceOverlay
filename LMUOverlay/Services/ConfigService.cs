using System.IO;
using LMUOverlay.Models;
using Newtonsoft.Json;

namespace LMUOverlay.Services
{
    /// <summary>
    /// Handles loading and saving configuration to a JSON file.
    /// </summary>
    public class ConfigService
    {
        private static readonly string ConfigDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "DouzeAssistance");

        private static readonly string ConfigPath = Path.Combine(ConfigDir, "config.json");

        public AppConfig Load()
        {
            try
            {
                if (File.Exists(ConfigPath))
                {
                    string json = File.ReadAllText(ConfigPath);
                    var cfg = JsonConvert.DeserializeObject<AppConfig>(json) ?? new AppConfig();
                    // Guard against null sub-objects (config.json from older version)
                    cfg.General          ??= new GeneralSettings();
                    cfg.Chrono           ??= new ChronoSettings();
                    cfg.StandingsColumns ??= new StandingsColumnConfig();
                    cfg.StandingsDisplay ??= new StandingsDisplayConfig();
                    cfg.DashboardConfig  ??= new DashboardDisplayConfig();
                    cfg.InputConfig      ??= new InputDisplayConfig();
                    cfg.RelativeConfig   ??= new RelativeConfig();
                    return cfg;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading config: {ex.Message}");
            }

            return new AppConfig();
        }

        public void Save(AppConfig config)
        {
            try
            {
                Directory.CreateDirectory(ConfigDir);
                string json = JsonConvert.SerializeObject(config, Formatting.Indented);
                File.WriteAllText(ConfigPath, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving config: {ex.Message}");
            }
        }
    }
}
