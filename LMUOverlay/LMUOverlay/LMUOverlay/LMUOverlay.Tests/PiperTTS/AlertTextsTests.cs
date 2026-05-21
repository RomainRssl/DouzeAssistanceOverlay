using Xunit;
using LMUOverlay.Models;
using LMUOverlay.Services;
using Newtonsoft.Json;

namespace LMUOverlay.Tests.PiperTTS
{
    /// <summary>
    /// PIPER-01: GeneralSettings.AlertTexts dictionary survives JSON round-trip.
    /// </summary>
    [Trait("Category", "PiperTTS")]
    public class AlertTextsRoundTripTests
    {
        [Fact(DisplayName = "AlertTexts: Dictionary survives JSON round-trip")]
        public void AlertTexts_Dictionary_SurvivesJsonRoundTrip()
        {
            var config = new AppConfig();
            config.General.AlertTexts["BlueFlagWarning"] = "Drapeau bleu, laisse passer";
            config.General.AlertTexts["GreenFlag"]       = "Go go go";

            var json = JsonConvert.SerializeObject(config);
            var restored = JsonConvert.DeserializeObject<AppConfig>(json)!;

            Assert.NotNull(restored.General.AlertTexts);
            Assert.Equal("Drapeau bleu, laisse passer", restored.General.AlertTexts["BlueFlagWarning"]);
            Assert.Equal("Go go go", restored.General.AlertTexts["GreenFlag"]);
        }
    }

    /// <summary>
    /// PIPER-02: Old config.json without AlertTexts key deserializes to empty dict (not null).
    /// </summary>
    [Trait("Category", "PiperTTS")]
    public class AlertTextsLegacyConfigTests
    {
        [Fact(DisplayName = "GeneralSettings: old JSON without AlertTexts deserializes to empty dict")]
        public void GeneralSettings_OldJsonWithoutAlertTexts_DeserializesToEmptyDict()
        {
            // JSON with no AlertTexts key — simulates old config
            const string legacyJson = "{\"VoiceEnabled\":false,\"VoiceVolume\":80}";
            var settings = JsonConvert.DeserializeObject<GeneralSettings>(legacyJson)!;

            Assert.NotNull(settings.AlertTexts);
            Assert.Empty(settings.AlertTexts);
        }
    }

    /// <summary>
    /// PIPER-03: GetAlertText returns stored value when key is present.
    /// </summary>
    [Trait("Category", "PiperTTS")]
    public class GetAlertTextFromDictTests
    {
        [Fact(DisplayName = "GetAlertText: key present in dict returns stored value")]
        public void GetAlertText_KeyPresentInDict_ReturnsStoredValue()
        {
            var settings = new GeneralSettings();
            settings.AlertTexts["RedFlag"] = "STOP maintenant";

            var result = settings.GetAlertText("RedFlag", "Drapeau rouge, arrêt immédiat");

            Assert.Equal("STOP maintenant", result);
        }
    }

    /// <summary>
    /// PIPER-04: GetAlertText returns fallback when key is absent.
    /// </summary>
    [Trait("Category", "PiperTTS")]
    public class GetAlertTextFallbackTests
    {
        [Fact(DisplayName = "GetAlertText: key absent from dict returns fallback")]
        public void GetAlertText_KeyAbsentFromDict_ReturnsFallback()
        {
            var settings = new GeneralSettings();

            var result = settings.GetAlertText("GreenFlag", "Drapeau vert, go");

            Assert.Equal("Drapeau vert, go", result);
        }
    }

    /// <summary>
    /// PIPER-05: GetAlertText returns fallback when stored value is empty/whitespace.
    /// </summary>
    [Trait("Category", "PiperTTS")]
    public class GetAlertTextEmptyStringTests
    {
        [Fact(DisplayName = "GetAlertText: empty string value returns fallback")]
        public void GetAlertText_EmptyStringValue_ReturnsFallback()
        {
            var settings = new GeneralSettings();
            settings.AlertTexts["SpotterClear"] = "   "; // whitespace only

            var result = settings.GetAlertText("SpotterClear", "Dégagé");

            Assert.Equal("Dégagé", result);
        }
    }

    /// <summary>
    /// PIPER-06: VoiceService.EnsureDefaultAlertTexts populates all 23 expected keys.
    /// </summary>
    [Trait("Category", "PiperTTS")]
    public class DefaultAlertTexts23KeysTests
    {
        [Fact(DisplayName = "EnsureDefaultAlertTexts: populates all 23 keys")]
        public void EnsureDefaultAlertTexts_PopulatesAll23Keys()
        {
            var config = new AppConfig();
            // Start with empty AlertTexts
            Assert.Empty(config.General.AlertTexts);

            VoiceService.EnsureDefaultAlertTexts(config.General);

            Assert.Equal(23, config.General.AlertTexts.Count);
        }
    }
}
