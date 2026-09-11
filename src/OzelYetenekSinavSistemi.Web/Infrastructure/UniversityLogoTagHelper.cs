using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace OzelYetenekSinavSistemi.Web.Infrastructure;

/// <summary>
/// Ürün logosunu yalnızca çalışma zamanı dosyası mevcutsa üretir;
/// aksi durumda erişilebilir metin fallback'i gösterir.
/// </summary>
[HtmlTargetElement("university-logo")]
public sealed class UniversityLogoTagHelper : TagHelper
{
    public const string PublicPath = "/images/branding/oys-logo.png";
    public const string AlternativeText = "Özel Yetenek Sınavları Başvuru Sistemi";

    private readonly IWebHostEnvironment _environment;

    public UniversityLogoTagHelper(IWebHostEnvironment environment)
    {
        _environment = environment;
    }

    public string Variant { get; set; } = "sidebar";

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        var variant = NormalizeVariant(Variant);
        var (width, height) = GetDimensions(variant);
        var cssClass = $"university-logo university-logo--{variant}";
        var webRootPath = _environment.WebRootPath;
        var physicalPath = string.IsNullOrWhiteSpace(webRootPath)
            ? null
            : Path.Combine(webRootPath, "images", "branding", "oys-logo.png");

        if (physicalPath is not null && File.Exists(physicalPath))
        {
            output.TagName = "img";
            output.TagMode = TagMode.SelfClosing;
            output.Attributes.SetAttribute("src", PublicPath);
            output.Attributes.SetAttribute("alt", AlternativeText);
            output.Attributes.SetAttribute("width", width);
            output.Attributes.SetAttribute("height", height);
            output.Attributes.SetAttribute("class", cssClass);
            output.Content.Clear();
            return;
        }

        output.TagName = "div";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.Attributes.SetAttribute("class", $"{cssClass} university-logo-fallback");
        output.Attributes.SetAttribute("role", "img");
        output.Attributes.SetAttribute("aria-label", AlternativeText);
        output.Content.SetContent("ÖYS");
    }

    private static string NormalizeVariant(string? variant) =>
        variant?.ToLowerInvariant() switch
        {
            "auth" => "auth",
            "document" => "document",
            _ => "sidebar"
        };

    private static (int Width, int Height) GetDimensions(string variant) =>
        variant switch
        {
            "auth" => (170, 170),
            "document" => (90, 90),
            _ => (48, 48)
        };
}
