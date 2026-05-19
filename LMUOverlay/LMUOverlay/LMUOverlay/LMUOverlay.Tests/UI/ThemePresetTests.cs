using System.IO;
using Xunit;
using LMUOverlay.Helpers;

namespace LMUOverlay.Tests.UI
{
    // TODO: implement ThemeManager.EnsurePresetThemesExistIn(string directory) overload in 02-03

    /// <summary>
    /// Tests for preset theme JSON write logic (UI-03).
    /// These tests are RED until Plan 02-03 adds EnsurePresetThemesExistIn() to ThemeManager.
    /// Uses isolated temp directories to avoid touching %AppData%.
    /// </summary>
    [Trait("Category", "UI")]
    public class ThemePresetTests : IDisposable
    {
        private readonly string _tempDir;

        public ThemePresetTests()
        {
            // Isolated temp directory per test class instance
            _tempDir = Path.Combine(Path.GetTempPath(), "LMUOverlayThemeTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempDir);
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(_tempDir))
                    Directory.Delete(_tempDir, recursive: true);
            }
            catch
            {
                // Best-effort cleanup
            }
        }

        [Fact(DisplayName = "EnsurePresetThemesExistIn: creates racing-clair.json when absent")]
        public void EnsurePresetThemesExist_WritesRacingClair_WhenAbsent()
        {
            ThemeManager.EnsurePresetThemesExistIn(_tempDir);

            Assert.True(File.Exists(Path.Combine(_tempDir, "racing-clair.json")),
                "Expected racing-clair.json to be written to the themes directory.");
        }

        [Fact(DisplayName = "EnsurePresetThemesExistIn: creates bleu-nuit-lmp2.json when absent")]
        public void EnsurePresetThemesExist_WritesBleuNuitLmp2_WhenAbsent()
        {
            ThemeManager.EnsurePresetThemesExistIn(_tempDir);

            Assert.True(File.Exists(Path.Combine(_tempDir, "bleu-nuit-lmp2.json")),
                "Expected bleu-nuit-lmp2.json to be written to the themes directory.");
        }

        [Fact(DisplayName = "EnsurePresetThemesExistIn: creates minimaliste-mono.json when absent")]
        public void EnsurePresetThemesExist_WritesMinimalisteMono_WhenAbsent()
        {
            ThemeManager.EnsurePresetThemesExistIn(_tempDir);

            Assert.True(File.Exists(Path.Combine(_tempDir, "minimaliste-mono.json")),
                "Expected minimaliste-mono.json to be written to the themes directory.");
        }

        [Fact(DisplayName = "EnsurePresetThemesExistIn: does not overwrite existing file with sentinel content")]
        public void EnsurePresetThemesExist_DoesNotOverwrite_WhenFileExists()
        {
            // Write sentinel content before calling EnsurePresetThemesExistIn
            string racingClairPath = Path.Combine(_tempDir, "racing-clair.json");
            File.WriteAllText(racingClairPath, "CUSTOM");

            ThemeManager.EnsurePresetThemesExistIn(_tempDir);

            // Sentinel content must be preserved — no overwrite
            string content = File.ReadAllText(racingClairPath);
            Assert.Equal("CUSTOM", content);
        }
    }
}
