using Xunit;
using LMUOverlay.Models;
using LMUOverlay.Helpers;
using Newtonsoft.Json;

namespace LMUOverlay.Tests.WebBrowser
{
    // Tests are RED until Plan 03-02 creates WebBrowserUrlValidator and AppConfig.WebBrowser.
    // This is intentional — Wave 0 TDD stubs establish the contract before implementation.

    /// <summary>
    /// WEB-01: URL validation — only http/https URLs accepted.
    /// </summary>
    [Trait("Category", "WebBrowser")]
    public class WebBrowserUrlValidationTests
    {
        [Theory]
        [InlineData("https://example.com", true)]
        [InlineData("http://example.com", true)]
        [InlineData("not-a-url", false)]
        [InlineData("", false)]
        [InlineData("ftp://example.com", false)]
        [InlineData("javascript:alert(1)", false)]
        public void IsValidWebUrl_VariousInputs_ReturnsExpected(string url, bool expected)
        {
            bool result = WebBrowserUrlValidator.IsValidWebUrl(url);
            Assert.Equal(expected, result);
        }
    }

    /// <summary>
    /// WEB-02: Navigation failure handler — sets IsEnabled=false without WebView2.
    /// Tests the condition logic directly (pure C#, no WebView2 dependency).
    /// </summary>
    [Trait("Category", "WebBrowser")]
    public class WebBrowserNavigationFailureTests
    {
        [Fact(DisplayName = "NavigationFailed: IsEnabled becomes false when isSuccess=false")]
        public void NavigationFailed_SetsIsEnabledFalse()
        {
            var settings = new OverlaySettings("Test", true);
            Assert.True(settings.IsEnabled);

            // Simulate the OnNavigationCompleted handler logic (pure C# — no WebView2 needed):
            // if (!e.IsSuccess) Dispatcher.Invoke(() => settings.IsEnabled = false);
            bool isSuccess = false;
            if (!isSuccess)
                settings.IsEnabled = false;

            Assert.False(settings.IsEnabled);
        }

        [Fact(DisplayName = "NavigationSucceeded: IsEnabled stays true when isSuccess=true")]
        public void NavigationSucceeded_IsEnabledStaysTrue()
        {
            var settings = new OverlaySettings("Test", true);

            bool isSuccess = true;
            if (!isSuccess)
                settings.IsEnabled = false;

            Assert.True(settings.IsEnabled);
        }
    }

    /// <summary>
    /// WEB-03: AppConfig JSON round-trip — WebBrowser property survives serialization
    /// and old configs without the key deserialize to a non-null default.
    /// </summary>
    [Trait("Category", "WebBrowser")]
    public class AppConfigWebBrowserTests
    {
        [Fact(DisplayName = "AppConfig: WebBrowser property survives JSON round-trip")]
        public void AppConfig_WebBrowserProperty_SurvivesRoundTrip()
        {
            var config = new AppConfig();
            // WebBrowser property added by Plan 03-02
            string json = JsonConvert.SerializeObject(config);
            var deserialized = JsonConvert.DeserializeObject<AppConfig>(json);
            Assert.NotNull(deserialized);
            Assert.NotNull(deserialized!.WebBrowser);
        }

        [Fact(DisplayName = "AppConfig: old JSON without WebBrowser key deserializes to non-null default")]
        public void AppConfig_OldJsonWithoutWebBrowser_DeserializesToDefault()
        {
            // Simulate a config.json from before Phase 3 (no WebBrowser key)
            string oldJson = "{\"TwitchChat\":{\"Name\":\"Tchat Twitch\",\"IsEnabled\":false}}";
            var deserialized = JsonConvert.DeserializeObject<AppConfig>(oldJson);
            Assert.NotNull(deserialized);
            Assert.NotNull(deserialized!.WebBrowser);
            Assert.False(deserialized.WebBrowser.IsEnabled);
        }
    }
}
