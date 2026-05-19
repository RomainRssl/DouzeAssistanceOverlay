using Xunit;
using LMUOverlay.Models;
using LMUOverlay.Helpers;

namespace LMUOverlay.Tests.UI
{
    // TODO: implement ColorOverrideHelper in 02-02

    /// <summary>
    /// Tests for per-overlay color override lookup via CustomOptions (UI-01/02).
    /// These tests are RED until Plan 02-02 creates ColorOverrideHelper.
    /// </summary>
    [Trait("Category", "UI")]
    public class ColorOverrideTests
    {
        [Fact(DisplayName = "GetColorOverride: absent key returns null")]
        public void GetColorOverride_WhenKeyAbsent_ReturnsNull()
        {
            // Arrange: empty CustomOptions
            var settings = new OverlaySettings();

            // Act
            string? result = ColorOverrideHelper.Get(settings, "ColorBg");

            // Assert: no override stored → null
            Assert.Null(result);
        }

        [Fact(DisplayName = "GetColorOverride: present key returns stored hex string")]
        public void GetColorOverride_WhenKeyPresent_ReturnsHexString()
        {
            // Arrange: custom color stored in CustomOptions
            var settings = new OverlaySettings();
            settings.CustomOptions["ColorBg"] = "#FF0000";

            // Act
            string? result = ColorOverrideHelper.Get(settings, "ColorBg");

            // Assert: stored hex string returned
            Assert.Equal("#FF0000", result);
        }

        [Fact(DisplayName = "SetColorOverride: stores hex string in CustomOptions")]
        public void SetColorOverride_StoresHexInCustomOptions()
        {
            // Arrange
            var settings = new OverlaySettings();

            // Act
            ColorOverrideHelper.Set(settings, "ColorText", "#FFFFFF");

            // Assert: stored under the correct key
            Assert.True(settings.CustomOptions.ContainsKey("ColorText"));
            Assert.Equal("#FFFFFF", settings.CustomOptions["ColorText"]);
        }

        [Fact(DisplayName = "SetColorOverride: overwrites existing key with new value")]
        public void SetColorOverride_Overwrite_UpdatesExistingKey()
        {
            // Arrange: existing override
            var settings = new OverlaySettings();
            settings.CustomOptions["ColorAccent"] = "#0000FF";

            // Act: overwrite with new hex
            ColorOverrideHelper.Set(settings, "ColorAccent", "#00FF00");

            // Assert: key updated to new value
            Assert.Equal("#00FF00", settings.CustomOptions["ColorAccent"]);
        }
    }
}
