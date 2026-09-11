using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using OzelYetenekSinavSistemi.Web.Configuration;
using DataProtectionOptions = OzelYetenekSinavSistemi.Web.Configuration.DataProtectionOptions;

namespace OzelYetenekSinavSistemi.Tests.Security;

public sealed class DataProtectionPersistenceTests
{
    [Fact]
    public void Program_RegistersPersistKeysAndApplicationName()
    {
        var program = ReadProjectFile("src", "OzelYetenekSinavSistemi.Web", "Program.cs");
        Assert.Contains("AddConfiguredDataProtection", program, StringComparison.Ordinal);
        Assert.Contains("DataProtectionOptions", program, StringComparison.Ordinal);
        Assert.Contains("ValidateOnStart()", program, StringComparison.Ordinal);
    }

    [Fact]
    public void Program_CookieSecureHttpOnlySameSite_Unchanged()
    {
        var program = ReadProjectFile("src", "OzelYetenekSinavSistemi.Web", "Program.cs");
        Assert.Equal(2, CountOccurrences(program, "Cookie.SecurePolicy = CookieSecurePolicy.Always"));
        Assert.Equal(2, CountOccurrences(program, "Cookie.HttpOnly = true"));
        Assert.Equal(2, CountOccurrences(program, "Cookie.SameSite = SameSiteMode.Lax"));
        Assert.Contains("OYS.Auth", program, StringComparison.Ordinal);
        Assert.Contains("OYS.Session", program, StringComparison.Ordinal);
    }

    [Fact]
    public void Appsettings_HasSecretFreeDataProtectionDefaults()
    {
        var json = ReadProjectFile("src", "OzelYetenekSinavSistemi.Web", "appsettings.json");
        Assert.Contains("\"DataProtection\"", json, StringComparison.Ordinal);
        Assert.Contains("\"ApplicationName\": \"OzelYetenekSinavSistemi\"", json, StringComparison.Ordinal);
        Assert.Contains("\"KeysPath\": \"\"", json, StringComparison.Ordinal);
        Assert.DoesNotContain(":\\\\", json, StringComparison.Ordinal);
        Assert.DoesNotContain("C:\\", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GitIgnore_ExcludesDataProtectionKeysDirectory()
    {
        var gitIgnore = ReadProjectFile(".gitignore");
        Assert.Contains("**/App_Data/DataProtection-Keys/", gitIgnore, StringComparison.Ordinal);
    }

    [Fact]
    public void ResolveApplicationName_UsesConfiguredOrDefault()
    {
        Assert.Equal(
            "OzelYetenekSinavSistemi",
            DataProtectionKeyRing.ResolveApplicationName(new DataProtectionOptions { ApplicationName = "  " }));
        Assert.Equal(
            "CampusApp",
            DataProtectionKeyRing.ResolveApplicationName(new DataProtectionOptions { ApplicationName = " CampusApp " }));
    }

    [Fact]
    public void Development_EmptyKeysPath_UsesAppDataOutsideWebRoot()
    {
        using var root = new TempContentRoot();
        var env = new FakeWebHostEnvironment(Environments.Development, root.Path);
        var keys = DataProtectionKeyRing.ResolveKeysDirectory(env, new DataProtectionOptions { KeysPath = "" });

        Assert.Equal(
            Path.GetFullPath(Path.Combine(root.Path, DataProtectionOptions.DevelopmentRelativeKeysPath)),
            keys);
        Assert.DoesNotContain(
            Path.DirectorySeparatorChar + "wwwroot" + Path.DirectorySeparatorChar,
            keys + Path.DirectorySeparatorChar,
            StringComparison.OrdinalIgnoreCase);
        DataProtectionKeyRing.EnsureOutsideWebRoot(env, keys);
    }

    [Fact]
    public void Production_EmptyKeysPath_FailsFastWithoutLeakingPathDetails()
    {
        using var root = new TempContentRoot();
        var env = new FakeWebHostEnvironment(Environments.Production, root.Path);
        var ex = Assert.Throws<InvalidOperationException>(() =>
            DataProtectionKeyRing.ResolveKeysDirectory(env, new DataProtectionOptions { KeysPath = null }));

        Assert.Contains("KeysPath", ex.Message, StringComparison.Ordinal);
        Assert.Contains("zorunlu", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(root.Path, ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("wwwroot", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Production_ValidKeysPath_PreparesDirectoryAndStartsProtector()
    {
        using var root = new TempContentRoot();
        var keysPath = Path.Combine(root.Path, "dp-keys");
        var env = new FakeWebHostEnvironment(Environments.Production, root.Path);
        var options = new DataProtectionOptions
        {
            ApplicationName = "OzelYetenekSinavSistemi",
            KeysPath = keysPath,
            ProtectKeysAtRest = false
        };

        var resolved = DataProtectionKeyRing.ValidatePrepareAndResolve(env, options);
        Assert.True(Directory.Exists(resolved));

        using var provider = BuildProvider(env, options);
        var protector = provider.GetRequiredService<IDataProtectionProvider>().CreateProtector("oys-test");
        var payload = protector.Protect("restart-roundtrip");
        Assert.Equal("restart-roundtrip", protector.Unprotect(payload));
    }

    [Fact]
    public void KeysPath_UnderWebRoot_IsRejected()
    {
        using var root = new TempContentRoot();
        var env = new FakeWebHostEnvironment(Environments.Production, root.Path);
        var underWeb = Path.Combine(root.Path, "wwwroot", "keys");

        var ex = Assert.Throws<InvalidOperationException>(() =>
            DataProtectionKeyRing.ResolveKeysDirectory(
                env,
                new DataProtectionOptions { KeysPath = underWeb }));

        Assert.Contains("wwwroot", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SameKeysPathAndApplicationName_SecondProviderCanUnprotect()
    {
        using var root = new TempContentRoot();
        var keysPath = Path.Combine(root.Path, "shared-keys");
        var env = new FakeWebHostEnvironment(Environments.Production, root.Path);
        var options = new DataProtectionOptions
        {
            ApplicationName = "OzelYetenekSinavSistemi",
            KeysPath = keysPath,
            ProtectKeysAtRest = false
        };

        string protectedPayload;
        using (var first = BuildProvider(env, options))
        {
            protectedPayload = first.GetRequiredService<IDataProtectionProvider>()
                .CreateProtector("oys-auth-sim")
                .Protect("cookie-payload");
        }

        using var second = BuildProvider(env, options);
        var plain = second.GetRequiredService<IDataProtectionProvider>()
            .CreateProtector("oys-auth-sim")
            .Unprotect(protectedPayload);
        Assert.Equal("cookie-payload", plain);
        Assert.NotEmpty(Directory.EnumerateFiles(keysPath, "key-*.xml"));
    }

    [Fact]
    public void DifferentApplicationName_CannotUnprotect()
    {
        using var root = new TempContentRoot();
        var keysPath = Path.Combine(root.Path, "shared-keys");
        var env = new FakeWebHostEnvironment(Environments.Production, root.Path);

        string protectedPayload;
        using (var first = BuildProvider(env, new DataProtectionOptions
        {
            ApplicationName = "AppA",
            KeysPath = keysPath,
            ProtectKeysAtRest = false
        }))
        {
            protectedPayload = first.GetRequiredService<IDataProtectionProvider>()
                .CreateProtector("purpose")
                .Protect("secret");
        }

        using var second = BuildProvider(env, new DataProtectionOptions
        {
            ApplicationName = "AppB",
            KeysPath = keysPath,
            ProtectKeysAtRest = false
        });

        Assert.ThrowsAny<Exception>(() =>
            second.GetRequiredService<IDataProtectionProvider>()
                .CreateProtector("purpose")
                .Unprotect(protectedPayload));
    }

    [Fact]
    public void Development_ProtectKeysAtRest_DoesNotRequireDpapiCall()
    {
        using var root = new TempContentRoot();
        var env = new FakeWebHostEnvironment(Environments.Development, root.Path);
        var options = new DataProtectionOptions
        {
            ApplicationName = "OzelYetenekSinavSistemi",
            KeysPath = Path.Combine(root.Path, "dev-keys"),
            ProtectKeysAtRest = true,
            ProtectToLocalMachine = false
        };

        using var provider = BuildProvider(env, options);
        var protector = provider.GetRequiredService<IDataProtectionProvider>().CreateProtector("dev");
        Assert.Equal("x", protector.Unprotect(protector.Protect("x")));
    }

    [Fact]
    public void OptionsValidator_RejectsProtectToLocalMachineWithoutAtRest()
    {
        var result = new DataProtectionOptionsValidator().Validate(
            null,
            new DataProtectionOptions { ProtectKeysAtRest = false, ProtectToLocalMachine = true });

        Assert.True(result.Failed);
    }

    [Fact]
    public void PasswordReset_DoesNotUseIDataProtectionProvider()
    {
        var source = ReadProjectFile(
            "src", "OzelYetenekSinavSistemi.Application", "Services", "PasswordResetService.cs");
        Assert.DoesNotContain("IDataProtectionProvider", source, StringComparison.Ordinal);
        Assert.DoesNotContain("IDataProtector", source, StringComparison.Ordinal);
        Assert.Contains("ComputeHash", source, StringComparison.Ordinal);
    }

    [Fact]
    public void DeploymentDoc_DocumentsSingleServerAndWebFarm()
    {
        var doc = ReadProjectFile("docs", "DATA-PROTECTION-DEPLOYMENT.md");
        Assert.Contains("ProtectKeysWithDpapi", doc, StringComparison.Ordinal);
        Assert.Contains("ProtectToLocalMachine", doc, StringComparison.Ordinal);
        Assert.Contains("web farm", doc, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("DataProtection__KeysPath", doc, StringComparison.Ordinal);
        Assert.Contains("Everyone", doc, StringComparison.Ordinal);
    }

    [Fact]
    public void WindowsProduction_ProtectKeysAtRest_AppliesDpapiConfigurationPath()
    {
        if (!OperatingSystem.IsWindows())
            return;

        using var root = new TempContentRoot();
        var env = new FakeWebHostEnvironment(Environments.Production, root.Path);
        var options = new DataProtectionOptions
        {
            ApplicationName = "OzelYetenekSinavSistemi",
            KeysPath = Path.Combine(root.Path, "win-keys"),
            ProtectKeysAtRest = true,
            ProtectToLocalMachine = false
        };

        using var provider = BuildProvider(env, options);
        var protector = provider.GetRequiredService<IDataProtectionProvider>().CreateProtector("win-dpapi");
        Assert.Equal("ok", protector.Unprotect(protector.Protect("ok")));
    }

    private static ServiceProvider BuildProvider(IHostEnvironment environment, DataProtectionOptions options)
    {
        var services = new ServiceCollection();
        DataProtectionKeyRing.AddConfiguredDataProtection(services, environment, options);
        return services.BuildServiceProvider();
    }

    private static int CountOccurrences(string source, string value)
    {
        var count = 0;
        var index = 0;
        while ((index = source.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += value.Length;
        }

        return count;
    }

    private static string ReadProjectFile(params string[] parts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(new[] { directory.FullName }.Concat(parts).ToArray());
            if (File.Exists(candidate))
                return File.ReadAllText(candidate);
            directory = directory.Parent;
        }

        throw new FileNotFoundException(string.Join(Path.DirectorySeparatorChar, parts));
    }

    private sealed class TempContentRoot : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "oys-dp-" + Guid.NewGuid().ToString("N"));

        public TempContentRoot()
        {
            Directory.CreateDirectory(Path);
            Directory.CreateDirectory(System.IO.Path.Combine(Path, "wwwroot"));
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(Path))
                    Directory.Delete(Path, recursive: true);
            }
            catch
            {
                // best-effort cleanup
            }
        }
    }

    private sealed class FakeWebHostEnvironment : IWebHostEnvironment
    {
        public FakeWebHostEnvironment(string environmentName, string contentRoot)
        {
            EnvironmentName = environmentName;
            ContentRootPath = contentRoot;
            WebRootPath = System.IO.Path.Combine(contentRoot, "wwwroot");
            ApplicationName = "OzelYetenekSinavSistemi.Web";
            ContentRootFileProvider = new PhysicalFileProvider(contentRoot);
            WebRootFileProvider = new PhysicalFileProvider(WebRootPath);
        }

        public string EnvironmentName { get; set; }
        public string ApplicationName { get; set; }
        public string ContentRootPath { get; set; }
        public IFileProvider ContentRootFileProvider { get; set; }
        public string WebRootPath { get; set; }
        public IFileProvider WebRootFileProvider { get; set; }
    }
}
