namespace OzelYetenekSinavSistemi.Tests.Security;

public sealed class AdminDescriptionSecurityTests
{
    [Fact]
    public void EvaluateView_DoesNotUseHtmlRawForAdminDescription()
    {
        var evaluate = ReadProjectFile("Views", "ExamResult", "Evaluate.cshtml");
        Assert.Contains("asp-for=\"AdminDescription\"", evaluate, StringComparison.Ordinal);
        Assert.DoesNotContain("Html.Raw", evaluate, StringComparison.Ordinal);
        Assert.DoesNotContain("@Html.Raw(Model.AdminDescription", evaluate, StringComparison.Ordinal);
    }

    [Fact]
    public void EvaluateView_EncodesAdminDescriptionField()
    {
        var evaluate = ReadProjectFile("Views", "ExamResult", "Evaluate.cshtml");
        Assert.Contains("<textarea asp-for=\"AdminDescription\"", evaluate, StringComparison.Ordinal);
    }

    [Fact]
    public void ExamResultService_HasNoDirectLoggingOfAdminDescription()
    {
        var service = ReadApplicationFile("Services", "ExamResultService.cs");
        Assert.DoesNotContain("_logger", service, StringComparison.Ordinal);
        Assert.DoesNotContain("LogInformation", service, StringComparison.Ordinal);
        Assert.DoesNotContain("LogWarning", service, StringComparison.Ordinal);
    }

    private static string ReadApplicationFile(params string[] parts)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(new[] { dir.FullName, "src", "OzelYetenekSinavSistemi.Application" }.Concat(parts).ToArray());
            if (File.Exists(candidate))
                return File.ReadAllText(candidate);
            dir = dir.Parent;
        }

        throw new FileNotFoundException(string.Join('/', parts));
    }

    private static string ReadProjectFile(params string[] parts)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(new[] { dir.FullName, "src", "OzelYetenekSinavSistemi.Web" }.Concat(parts).ToArray());
            if (File.Exists(candidate))
                return File.ReadAllText(candidate);

            var appCandidate = Path.Combine(new[] { dir.FullName, "src" }.Concat(parts).ToArray());
            if (File.Exists(appCandidate))
                return File.ReadAllText(appCandidate);

            dir = dir.Parent;
        }

        throw new FileNotFoundException(string.Join('/', parts));
    }
}
