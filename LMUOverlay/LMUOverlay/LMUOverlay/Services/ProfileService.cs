using System.IO;
using LMUOverlay.Models;
using Newtonsoft.Json;

namespace LMUOverlay.Services
{
    public class ProfileService
    {
        private static readonly string ProfileDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "DouzeAssistance", "Profiles");

        public List<string> GetAvailableProfiles()
        {
            if (!Directory.Exists(ProfileDir))
                return new List<string> { "Default" };

            var profiles = Directory.GetFiles(ProfileDir, "*.json")
                .Select(f => Path.GetFileNameWithoutExtension(f))
                .ToList();

            if (!profiles.Contains("Default"))
                profiles.Insert(0, "Default");

            return profiles;
        }

        public void SaveProfile(AppConfig config, string profileName)
        {
            Directory.CreateDirectory(ProfileDir);
            string safeName = SanitizeFileName(profileName);
            string path = Path.Combine(ProfileDir, $"{safeName}.json");

            string json = JsonConvert.SerializeObject(config, Formatting.Indented);
            File.WriteAllText(path, json);
        }

        public AppConfig? LoadProfile(string profileName)
        {
            string path = Path.Combine(ProfileDir, $"{SanitizeFileName(profileName)}.json");
            if (!File.Exists(path)) return null;

            try
            {
                string json = File.ReadAllText(path);
                return JsonConvert.DeserializeObject<AppConfig>(json);
            }
            catch
            {
                return null;
            }
        }

        public void DeleteProfile(string profileName)
        {
            if (profileName == "Default") return;
            string path = Path.Combine(ProfileDir, $"{SanitizeFileName(profileName)}.json");
            if (File.Exists(path))
                File.Delete(path);
        }

        /// <summary>
        /// Creates a profile name from the current track name.
        /// </summary>
        public static string TrackToProfileName(string trackName)
        {
            if (string.IsNullOrWhiteSpace(trackName)) return "Default";
            return SanitizeFileName(trackName);
        }

        private static string SanitizeFileName(string name)
        {
            var invalid = Path.GetInvalidFileNameChars();
            return new string(name.Where(c => !invalid.Contains(c)).ToArray()).Trim();
        }
    }
}
