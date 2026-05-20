using Xunit;
using LMUOverlay.Models;
using Newtonsoft.Json;

namespace LMUOverlay.Tests.TwitchVisual
{
    /// <summary>
    /// TWITCH-V-01: TwitchSettings with the 4 new visual fields survives JSON round-trip.
    /// GREEN: Plan 04-02 added HeaderImagePath, ShowHeader, BackgroundColor, AccentColor.
    /// </summary>
    [Trait("Category", "TwitchVisual")]
    public class TwitchVisualRoundTripTests
    {
        [Fact(DisplayName = "TwitchSettings: 4 visual fields survive JSON round-trip")]
        public void TwitchSettings_VisualFields_SurviveRoundTrip()
        {
            var config = new AppConfig();
            config.Twitch.HeaderImagePath = @"C:\test.png";
            config.Twitch.ShowHeader      = false;
            config.Twitch.BackgroundColor = "#1A1A2E";
            config.Twitch.AccentColor     = "#9146FF";

            string json = JsonConvert.SerializeObject(config);
            var deserialized = JsonConvert.DeserializeObject<AppConfig>(json);

            Assert.NotNull(deserialized);
            Assert.Equal(@"C:\test.png", deserialized!.Twitch.HeaderImagePath);
            Assert.False(deserialized.Twitch.ShowHeader);
            Assert.Equal("#1A1A2E", deserialized.Twitch.BackgroundColor);
            Assert.Equal("#9146FF", deserialized.Twitch.AccentColor);
        }
    }

    /// <summary>
    /// TWITCH-V-02: Old JSON without the new visual fields deserializes to C# defaults.
    /// GREEN: Plan 04-02 added the properties with correct default values.
    /// </summary>
    [Trait("Category", "TwitchVisual")]
    public class TwitchVisualDefaultsTests
    {
        [Fact(DisplayName = "TwitchSettings: old JSON without visual fields deserializes to defaults")]
        public void TwitchSettings_OldJson_DeserializesToDefaults()
        {
            // Simulate a config.json from before Phase 4 (no visual fields)
            string oldJson = "{\"Twitch\":{\"Channel\":\"test\",\"MaxMessages\":20}}";
            var deserialized = JsonConvert.DeserializeObject<AppConfig>(oldJson);

            Assert.NotNull(deserialized);
            Assert.Equal("",   deserialized!.Twitch.HeaderImagePath);
            Assert.True(deserialized.Twitch.ShowHeader);
            Assert.Equal("",   deserialized.Twitch.BackgroundColor);
            Assert.Equal("",   deserialized.Twitch.AccentColor);
        }
    }
}
