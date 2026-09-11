namespace OzelYetenekSinavSistemi.Web.Infrastructure;

/// <summary>
/// Tarayıcı sekmesi başlığı ve kompakt favicon standardı (public portfolio edition).
/// </summary>
public static class BrowserTabBranding
{
    public const string InstitutionalSuffix = "ÖYS";
    public const string DefaultTitle = "Özel Yetenek Sınavları Başvuru Sistemi";
    public const string FaviconRelativeWebPath = "images/branding/oys-favicon.png";
    public const string FaviconPublicPath = "/images/branding/oys-favicon.png";
    public const string FaviconContentType = "image/png";
    public const string FaviconAlternativeText = "Özel Yetenek Sınavları Başvuru Sistemi";

    public static string FormatDocumentTitle(string? pageTitle)
    {
        var title = pageTitle?.Trim();
        if (string.IsNullOrWhiteSpace(title))
            return DefaultTitle;

        if (string.Equals(title, DefaultTitle, StringComparison.Ordinal)
            || string.Equals(title, InstitutionalSuffix, StringComparison.Ordinal))
            return title;

        var composedSuffix = " - " + InstitutionalSuffix;
        if (title.EndsWith(composedSuffix, StringComparison.Ordinal))
            return title;

        if (title.EndsWith(InstitutionalSuffix, StringComparison.Ordinal))
            return title;

        if (title.Contains(composedSuffix, StringComparison.Ordinal))
            return title;

        return title + composedSuffix;
    }
}
