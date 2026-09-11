using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Moq;
using OzelYetenekSinavSistemi.Web.Infrastructure;

namespace OzelYetenekSinavSistemi.Tests.Security;

public sealed class UniversityLogoTests
{
    [Fact]
    public void MissingLogo_RendersAccessibleFallbackWithoutImg()
    {
        using var webRoot = new TemporaryDirectory();
        var output = RenderLogo(webRoot.Path, "sidebar");

        Assert.Equal("div", output.TagName);
        Assert.Equal("img", output.Attributes["role"].Value);
        Assert.Equal("Özel Yetenek Sınavları Başvuru Sistemi", output.Attributes["aria-label"].Value);
        Assert.Contains("university-logo-fallback", output.Attributes["class"].Value?.ToString());
        Assert.Equal("ÖYS", WebUtility.HtmlDecode(output.Content.GetContent()));
        Assert.False(output.Attributes.ContainsName("src"));
        Assert.False(output.Attributes.ContainsName("alt"));
    }

    [Fact]
    public void ExistingLogo_RendersSelfHostedImageWithMeaningfulAltAndDimensions()
    {
        using var webRoot = new TemporaryDirectory();
        var imageDirectory = Path.Combine(webRoot.Path, "images", "branding");
        Directory.CreateDirectory(imageDirectory);
        File.WriteAllBytes(Path.Combine(imageDirectory, "oys-logo.png"), [0x89, 0x50, 0x4E, 0x47]);

        var output = RenderLogo(webRoot.Path, "document");

        Assert.Equal("img", output.TagName);
        Assert.Equal(TagMode.SelfClosing, output.TagMode);
        Assert.Equal("/images/branding/oys-logo.png", output.Attributes["src"].Value);
        Assert.Equal("Özel Yetenek Sınavları Başvuru Sistemi", output.Attributes["alt"].Value);
        Assert.Equal("90", output.Attributes["width"].Value?.ToString());
        Assert.Equal("90", output.Attributes["height"].Value?.ToString());
        Assert.DoesNotContain("http://", output.Attributes["src"].Value?.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("https://", output.Attributes["src"].Value?.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RuntimeLogoViews_UseServerSideHelperWithoutInlineFallbackScriptOrExternalUrl()
    {
        var viewsRoot = FindUnder("src", "OzelYetenekSinavSistemi.Web", "Views");
        var compactLogoViews = new[]
        {
            Path.Combine("Shared", "_AdminSidebar.cshtml"),
            Path.Combine("Shared", "_CandidateSidebar.cshtml")
        };

        foreach (var relativePath in compactLogoViews)
        {
            var source = File.ReadAllText(Path.Combine(viewsRoot, relativePath));
            Assert.Contains("<university-logo ", source, StringComparison.Ordinal);
        }

        foreach (var file in Directory.EnumerateFiles(viewsRoot, "*.cshtml", SearchOption.AllDirectories))
        {
            var source = File.ReadAllText(file);
            Assert.DoesNotContain("onerror=", source, StringComparison.OrdinalIgnoreCase);

            foreach (var line in source.Split('\n').Where(line =>
                         line.Contains("logo", StringComparison.OrdinalIgnoreCase)))
            {
                Assert.DoesNotContain("http://", line, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("https://", line, StringComparison.OrdinalIgnoreCase);
            }
        }
    }

    [Fact]
    public void LogoRendering_DoesNotChangeCsp()
    {
        var nonce = Convert.ToBase64String(Guid.NewGuid().ToByteArray());
        var csp = SecurityHeadersMiddleware.BuildContentSecurityPolicy(nonce);

        Assert.Equal("style-src 'self' 'unsafe-inline'", ExtractDirective(csp, "style-src"));
        Assert.Equal("font-src 'self' data:", ExtractDirective(csp, "font-src"));
        Assert.DoesNotContain("unsafe-eval", csp, StringComparison.OrdinalIgnoreCase);
    }

    private static TagHelperOutput RenderLogo(string webRootPath, string variant)
    {
        var environment = new Mock<IWebHostEnvironment>();
        environment.SetupGet(x => x.WebRootPath).Returns(webRootPath);

        var helper = new UniversityLogoTagHelper(environment.Object) { Variant = variant };
        var context = new TagHelperContext(
            tagName: "university-logo",
            allAttributes: new TagHelperAttributeList
            {
                new("variant", variant)
            },
            items: new Dictionary<object, object>(),
            uniqueId: Guid.NewGuid().ToString("N"));
        var output = new TagHelperOutput(
            "university-logo",
            new TagHelperAttributeList(),
            (_, _) => Task.FromResult<TagHelperContent>(new DefaultTagHelperContent()));

        helper.Process(context, output);
        return output;
    }

    private static string ExtractDirective(string csp, string name)
    {
        var start = csp.IndexOf(name + " ", StringComparison.Ordinal);
        Assert.True(start >= 0);
        var end = csp.IndexOf(';', start);
        return end > start ? csp[start..end] : csp[start..];
    }

    private static string FindUnder(params string[] parts)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var path = Path.Combine(new[] { dir.FullName }.Concat(parts).ToArray());
            if (Directory.Exists(path) || File.Exists(path))
                return path;
            dir = dir.Parent;
        }

        throw new FileNotFoundException(string.Join(Path.DirectorySeparatorChar, parts));
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"oys-logo-tests-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path))
                Directory.Delete(Path, recursive: true);
        }
    }
}
