using OzelYetenekSinavSistemi.Web.Controllers;

namespace OzelYetenekSinavSistemi.Tests.Security;

public sealed class CandidatePhotoViewRouteTests
{
    [Fact]
    public void CandidateProfileView_UsesCandidatePhotoProfileAction()
    {
        var view = FindView("CandidateProfile", "Index.cshtml");
        var content = File.ReadAllText(view);

        Assert.Contains("CandidatePhoto", content, StringComparison.Ordinal);
        Assert.Contains("Profile", content, StringComparison.Ordinal);
        Assert.DoesNotContain("Url.Content(Model.PhotoPath)", content, StringComparison.Ordinal);
        Assert.DoesNotContain("asp-for=\"PhotoPath\"", content, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("ExamDocument", "View.cshtml")]
    [InlineData("ExamDocument", "Result.cshtml")]
    public void DocumentViews_UseCandidatePhotoApplicationAction(string folder, string file)
    {
        var view = FindView(folder, file);
        var content = File.ReadAllText(view);

        Assert.Contains("CandidatePhoto", content, StringComparison.Ordinal);
        Assert.Contains("Application", content, StringComparison.Ordinal);
        Assert.Contains("applicationId", content, StringComparison.Ordinal);
        Assert.DoesNotContain("Url.Content(Model.PhotoPath)", content, StringComparison.Ordinal);
    }

    [Fact]
    public void CandidatePhotoController_Exists()
    {
        Assert.Equal("CandidatePhoto", nameof(CandidatePhotoController).Replace("Controller", string.Empty));
    }

    private static string FindView(string folder, string file)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "src", "OzelYetenekSinavSistemi.Web", "Views", folder, file);
            if (File.Exists(candidate))
                return candidate;
            dir = dir.Parent;
        }

        throw new FileNotFoundException($"{folder}/{file} bulunamadı.");
    }
}
