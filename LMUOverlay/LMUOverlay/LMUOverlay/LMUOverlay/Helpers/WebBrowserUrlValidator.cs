namespace LMUOverlay.Helpers
{
    /// <summary>
    /// Pure static URL validation — no WPF dependency, unit-testable.
    /// </summary>
    public static class WebBrowserUrlValidator
    {
        /// <summary>
        /// Returns true only for absolute http:// or https:// URLs.
        /// Malformed strings, empty strings, ftp://, javascript: all return false.
        /// </summary>
        public static bool IsValidWebUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return false;
            return Uri.TryCreate(url, UriKind.Absolute, out var uri)
                && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
        }
    }
}
