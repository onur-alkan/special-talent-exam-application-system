using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using OzelYetenekSinavSistemi.Web.Controllers;

namespace OzelYetenekSinavSistemi.Tests.Security;

public sealed class DocumentVerificationRateLimitingTests
{
    private const string PolicyName = "document-verification";

    [Fact]
    public void VerifyGet_HasDocumentVerificationPolicy()
    {
        var method = typeof(DocumentVerificationController).GetMethod(
            nameof(DocumentVerificationController.Verify),
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly);

        Assert.NotNull(method);
        var attribute = method!.GetCustomAttribute<EnableRateLimitingAttribute>();
        Assert.NotNull(attribute);
        Assert.Equal(PolicyName, attribute!.PolicyName);
        Assert.NotNull(method.GetCustomAttribute<HttpGetAttribute>());
    }

    [Fact]
    public void Program_RegistersMatchingDocumentVerificationPolicy()
    {
        var programPath = FindProgramCs();
        var programSource = File.ReadAllText(programPath);

        Assert.Contains($"AddPolicy(\"{PolicyName}\"", programSource, StringComparison.Ordinal);

        var method = typeof(DocumentVerificationController).GetMethod(
            nameof(DocumentVerificationController.Verify),
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly);
        var attribute = method!.GetCustomAttribute<EnableRateLimitingAttribute>();
        Assert.Equal(PolicyName, attribute!.PolicyName);
    }

    private static string FindProgramCs()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "src", "OzelYetenekSinavSistemi.Web", "Program.cs");
            if (File.Exists(candidate))
                return candidate;

            dir = dir.Parent;
        }

        throw new FileNotFoundException("Program.cs bulunamadı.");
    }
}
