namespace OzelYetenekSinavSistemi.Tests.Security;

public sealed class CleanCheckoutConfigurationTests
{
    [Fact]
    public void CleanCheckout_DoesNotRequireLocalDevelopmentJson()
    {
        var webRoot = FindUnder("src", "OzelYetenekSinavSistemi.Web");
        var localDevelopmentJson = Path.Combine(webRoot, "appsettings.Development.json");
        var trackedExample = Path.Combine(webRoot, "appsettings.Development.json.example");

        Assert.True(File.Exists(trackedExample), "Tracked development example must exist in every checkout.");
        if (File.Exists(localDevelopmentJson))
            return;

        var exampleJson = File.ReadAllText(trackedExample);
        Assert.DoesNotContain("\"Password\":", exampleJson, StringComparison.Ordinal);
        Assert.Contains("\"DeliveryMode\": \"File\"", exampleJson, StringComparison.Ordinal);
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
}
