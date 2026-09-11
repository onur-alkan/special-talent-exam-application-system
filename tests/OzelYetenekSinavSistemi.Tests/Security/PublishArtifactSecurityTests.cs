using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace OzelYetenekSinavSistemi.Tests.Security;

/// <summary>
/// Release publish çıktısı envanteri ve secret taraması.
/// Bulgu mesajlarında secret değerleri yazılmaz; yalnız yol, anahtar ve sınıf.
/// Isolated from other <c>dotnet publish</c> tests via <see cref="DotnetPublishIsolationCollection"/>.
/// </summary>
[Collection(nameof(DotnetPublishIsolationCollection))]
public sealed class PublishArtifactSecurityTests : IClassFixture<PublishArtifactFixture>
{
    private readonly PublishArtifactFixture _fixture;

    public PublishArtifactSecurityTests(PublishArtifactFixture fixture) => _fixture = fixture;

    [Fact]
    public void DevelopmentAppsettings_IsNotPublished()
    {
        Assert.False(File.Exists(Path.Combine(_fixture.PublishDir, "appsettings.Development.json")));
    }

    [Fact]
    public void DevelopmentExample_IsNotPublished()
    {
        Assert.False(File.Exists(Path.Combine(_fixture.PublishDir, "appsettings.Development.json.example")));
    }

    [Fact]
    public void ProductionExample_IsNotPublished()
    {
        Assert.False(File.Exists(Path.Combine(_fixture.PublishDir, "appsettings.Production.example.json")));
    }

    [Fact]
    public void LaunchSettings_IsNotPublished()
    {
        Assert.False(File.Exists(Path.Combine(_fixture.PublishDir, "launchSettings.json")));
        Assert.False(File.Exists(Path.Combine(_fixture.PublishDir, "Properties", "launchSettings.json")));
    }

    [Fact]
    public void TestAssemblies_AreNotPublished()
    {
        var forbidden = Directory.EnumerateFiles(_fixture.PublishDir, "*", SearchOption.AllDirectories)
            .Select(Relative)
            .Where(p =>
                p.Contains("OzelYetenekSinavSistemi.Tests", StringComparison.OrdinalIgnoreCase)
                || p.Contains("testhost", StringComparison.OrdinalIgnoreCase)
                || p.Contains("coverlet", StringComparison.OrdinalIgnoreCase)
                || p.Contains("TestResults", StringComparison.OrdinalIgnoreCase)
                || p.EndsWith(".Tests.dll", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        Assert.True(forbidden.Length == 0, FormatPathsOnly("Test artifact found", forbidden));
    }

    [Fact]
    public void UserSecrets_AreNotPublished()
    {
        var hits = Directory.EnumerateFiles(_fixture.PublishDir, "*", SearchOption.AllDirectories)
            .Select(Relative)
            .Where(p =>
                p.EndsWith("secrets.json", StringComparison.OrdinalIgnoreCase)
                || p.Contains("UserSecrets", StringComparison.OrdinalIgnoreCase)
                || p.Contains("2fc59eda-c1db-40b2-a3ab-6232ce3294a3", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        Assert.True(hits.Length == 0, FormatPathsOnly("User secrets artifact found", hits));
    }

    [Fact]
    public void CertificateAndKeyFiles_AreNotPublished()
    {
        var hits = Directory.EnumerateFiles(_fixture.PublishDir, "*", SearchOption.AllDirectories)
            .Where(f =>
            {
                var ext = Path.GetExtension(f);
                return ext.Equals(".pfx", StringComparison.OrdinalIgnoreCase)
                       || ext.Equals(".p12", StringComparison.OrdinalIgnoreCase)
                       || ext.Equals(".pem", StringComparison.OrdinalIgnoreCase)
                       || ext.Equals(".key", StringComparison.OrdinalIgnoreCase)
                       || ext.Equals(".cer", StringComparison.OrdinalIgnoreCase);
            })
            .Select(Relative)
            .ToArray();

        Assert.True(hits.Length == 0, FormatPathsOnly("Certificate/key artifact found", hits));
    }

    [Fact]
    public void AuditMarkerFiles_AreNotPublished()
    {
        var hits = Directory.EnumerateFiles(_fixture.PublishDir, "*", SearchOption.AllDirectories)
            .Select(Relative)
            .Where(p => Path.GetFileName(p).StartsWith("_audit_", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        Assert.True(hits.Length == 0, FormatPathsOnly("Audit marker found", hits));
    }

    [Fact]
    public void RequiredRuntimeArtifacts_ArePresent()
    {
        Assert.True(File.Exists(Path.Combine(_fixture.PublishDir, "OzelYetenekSinavSistemi.Web.dll")));
        Assert.True(File.Exists(Path.Combine(_fixture.PublishDir, "OzelYetenekSinavSistemi.Web.deps.json")));
        Assert.True(File.Exists(Path.Combine(_fixture.PublishDir, "OzelYetenekSinavSistemi.Web.runtimeconfig.json")));
        Assert.True(File.Exists(Path.Combine(_fixture.PublishDir, "appsettings.json")));
        Assert.True(Directory.Exists(Path.Combine(_fixture.PublishDir, "wwwroot")));
    }

    [Fact]
    public void ForbiddenWorkspaceResidue_IsAbsent()
    {
        var hits = Directory.EnumerateFileSystemEntries(_fixture.PublishDir, "*", SearchOption.AllDirectories)
            .Select(Relative)
            .Where(p =>
                p.Equals(".git", StringComparison.OrdinalIgnoreCase)
                || p.StartsWith(".git" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                || p.Equals(".vs", StringComparison.OrdinalIgnoreCase)
                || p.StartsWith(".vs" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                || p.Equals("bin", StringComparison.OrdinalIgnoreCase)
                || p.StartsWith("bin" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                || p.Equals("obj", StringComparison.OrdinalIgnoreCase)
                || p.StartsWith("obj" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                || p.Contains("dev-emails", StringComparison.OrdinalIgnoreCase)
                || p.Contains("ValidationHttp", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        Assert.True(hits.Length == 0, FormatPathsOnly("Workspace residue found", hits));
    }

    [Fact]
    public void BaseAppsettings_HasNoRealConnectionStringOrSmtpPassword()
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(_fixture.PublishDir, "appsettings.json")));
        var root = doc.RootElement;

        Assert.True(root.TryGetProperty("ConnectionStrings", out var cs));
        Assert.True(cs.TryGetProperty("DefaultConnection", out var conn));
        Assert.True(string.IsNullOrWhiteSpace(conn.GetString()), "FindingClass=connection_string Key=ConnectionStrings:DefaultConnection");

        if (root.TryGetProperty("Email", out var email))
        {
            if (email.TryGetProperty("Password", out var password))
                Assert.True(string.IsNullOrWhiteSpace(password.GetString()), "FindingClass=smtp_password Key=Email:Password");
            if (email.TryGetProperty("Username", out var username))
                Assert.True(string.IsNullOrWhiteSpace(username.GetString()), "FindingClass=smtp_username Key=Email:Username");
        }
    }

    [Fact]
    public void BaseAppsettings_HasNoProductionSecrets()
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(_fixture.PublishDir, "appsettings.json")));
        var root = doc.RootElement;

        foreach (var key in new[] { "ApiKey", "ClientSecret", "Token", "CaptchaSecret", "RecaptchaSecret" })
        {
            Assert.False(ContainsPropertyName(root, key), $"FindingClass=secret_key Key={key}");
        }

        Assert.Equal("https://localhost:7102", root.GetProperty("PublicUrl").GetProperty("BaseUrl").GetString());
        Assert.Equal("Disabled", root.GetProperty("Email").GetProperty("DeliveryMode").GetString());
    }

    [Fact]
    public void ProductionExample_ExistsInSource_WithPlaceholdersOnly()
    {
        var examplePath = Path.Combine(
            FindRepoRoot(), "src", "OzelYetenekSinavSistemi.Web", "appsettings.Production.example.json");
        Assert.True(File.Exists(examplePath));
        var json = File.ReadAllText(examplePath);
        Assert.Contains("REPLACE_WITH_", json, StringComparison.Ordinal);
        Assert.DoesNotContain("Admin!2345", json, StringComparison.Ordinal);
        Assert.Contains("\"SeedTestData\": false", json, StringComparison.Ordinal);
    }

    [Fact]
    public void PublishSecretScan_FindsNoCriticalSecrets()
    {
        var findings = PublishSecretScanner.Scan(_fixture.PublishDir);
        var critical = findings.Where(f => f.Severity == PublishFindingSeverity.Critical).ToArray();
        Assert.True(
            critical.Length == 0,
            FormatFindings(critical));
    }

    [Fact]
    public void SecretScanFailureMessage_DoesNotEchoSecretValues()
    {
        var samplePath = Path.Combine(Path.GetTempPath(), $"oys-scan-msg-{Guid.NewGuid():N}.json");
        try
        {
            File.WriteAllText(samplePath, """
                {
                  "Email": { "Password": "SuperSecretValue-DoNotEcho-9x" },
                  "ConnectionStrings": {
                    "DefaultConnection": "Server=prod.example;Database=X;User Id=u;Password=SuperSecretValue-DoNotEcho-9x;"
                  }
                }
                """);

            var findings = PublishSecretScanner.ScanFile(samplePath, Path.GetDirectoryName(samplePath)!);
            Assert.Contains(findings, f => f.Severity == PublishFindingSeverity.Critical);

            var message = FormatFindings(findings.Where(f => f.Severity == PublishFindingSeverity.Critical));
            Assert.DoesNotContain("SuperSecretValue-DoNotEcho-9x", message, StringComparison.Ordinal);
            Assert.Contains("FindingClass=", message, StringComparison.Ordinal);
            Assert.Contains("Key=", message, StringComparison.Ordinal);
        }
        finally
        {
            if (File.Exists(samplePath))
                File.Delete(samplePath);
        }
    }

    [Fact]
    public void Csproj_ExcludesSensitiveConfigFromPublish()
    {
        var csproj = File.ReadAllText(Path.Combine(
            FindRepoRoot(), "src", "OzelYetenekSinavSistemi.Web", "OzelYetenekSinavSistemi.Web.csproj"));

        Assert.Contains("appsettings.Development.json", csproj, StringComparison.Ordinal);
        Assert.Contains("appsettings.Development.json.example", csproj, StringComparison.Ordinal);
        Assert.Contains("appsettings.Production.example.json", csproj, StringComparison.Ordinal);
        Assert.Contains("launchSettings.json", csproj, StringComparison.Ordinal);
        Assert.Contains("CopyToPublishDirectory>Never", csproj, StringComparison.Ordinal);
        Assert.DoesNotContain("PreserveNewest", csproj, StringComparison.Ordinal);
    }

    private string Relative(string fullPath) =>
        Path.GetRelativePath(_fixture.PublishDir, fullPath);

    private static string FormatPathsOnly(string title, IEnumerable<string> paths) =>
        title + ": " + string.Join("; ", paths.Select(p => "Path=" + p));

    private static string FormatFindings(IEnumerable<PublishSecretFinding> findings) =>
        string.Join("; ", findings.Select(f =>
            $"Path={f.RelativePath} Key={f.KeyName} FindingClass={f.FindingClass}"));

    private static bool ContainsPropertyName(JsonElement element, string name)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in element.EnumerateObject())
            {
                if (prop.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                    return true;
                if (ContainsPropertyName(prop.Value, name))
                    return true;
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                if (ContainsPropertyName(item, name))
                    return true;
            }
        }

        return false;
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "OzelYetenekSinavSistemi.sln")))
                return dir.FullName;
            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException("Repository root not found.");
    }
}

public sealed class PublishArtifactFixture : IDisposable
{
    public string PublishDir { get; }

    public PublishArtifactFixture()
    {
        var repoRoot = FindRepoRoot();
        var project = Path.Combine(repoRoot, "src", "OzelYetenekSinavSistemi.Web", "OzelYetenekSinavSistemi.Web.csproj");
        PublishDir = Path.Combine(Path.GetTempPath(), $"oys-publish-audit-{Guid.NewGuid():N}");

        try
        {
            Directory.CreateDirectory(PublishDir);
            var psi = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"publish \"{project}\" -c Release -o \"{PublishDir}\" --verbosity quiet",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = repoRoot
            };

            using var process = Process.Start(psi)!;
            var stdoutTask = process.StandardOutput.ReadToEndAsync();
            var stderrTask = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(300_000))
            {
                try { process.Kill(entireProcessTree: true); } catch { /* ignore */ }
                throw new TimeoutException("dotnet publish timed out.");
            }

            var stdout = stdoutTask.GetAwaiter().GetResult();
            var stderr = stderrTask.GetAwaiter().GetResult();

            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException(
                    $"dotnet publish failed. ExitCode={process.ExitCode}."
                    + Environment.NewLine + "--- stdout ---" + Environment.NewLine + TruncateForMessage(stdout)
                    + Environment.NewLine + "--- stderr ---" + Environment.NewLine + TruncateForMessage(stderr));
            }
        }
        catch
        {
            try
            {
                if (Directory.Exists(PublishDir))
                    Directory.Delete(PublishDir, recursive: true);
            }
            catch
            {
                // ignore cleanup failure
            }

            throw;
        }
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(PublishDir))
                Directory.Delete(PublishDir, recursive: true);
        }
        catch
        {
            // best-effort cleanup
        }
    }

    private static string TruncateForMessage(string text, int maxChars = 4000)
    {
        if (string.IsNullOrEmpty(text))
            return "(empty)";
        return text.Length <= maxChars
            ? text
            : text[..maxChars] + $"{Environment.NewLine}... (truncated, {text.Length} chars total)";
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "OzelYetenekSinavSistemi.sln")))
                return dir.FullName;
            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException("Repository root not found.");
    }
}

public enum PublishFindingSeverity
{
    Info,
    Critical
}

public sealed record PublishSecretFinding(
    string RelativePath,
    string KeyName,
    string FindingClass,
    PublishFindingSeverity Severity);

public static class PublishSecretScanner
{
    private static readonly string[] ScanExtensions =
    [
        ".json", ".config", ".xml", ".txt", ".md", ".ps1", ".cmd", ".bat",
        ".yml", ".yaml", ".pfx", ".p12", ".pem", ".key", ".cer"
    ];

    private static readonly HashSet<string> SafeEmptyOrLocalKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "AllowedHosts",
        "BaseUrl",
        "FromAddress",
        "FromName",
        "SuperAdminEmail",
        "SuperAdminFirstName",
        "SuperAdminLastName",
        "SuperAdminTcNo",
        "ApplicationName",
        "DeliveryMode",
        "KeysPath"
    };

    public static IReadOnlyList<PublishSecretFinding> Scan(string publishDir)
    {
        var findings = new List<PublishSecretFinding>();
        foreach (var file in Directory.EnumerateFiles(publishDir, "*", SearchOption.AllDirectories))
        {
            var ext = Path.GetExtension(file);
            if (!ScanExtensions.Any(e => e.Equals(ext, StringComparison.OrdinalIgnoreCase)))
                continue;

            findings.AddRange(ScanFile(file, publishDir));
        }

        return findings;
    }

    public static IReadOnlyList<PublishSecretFinding> ScanFile(string filePath, string rootDir)
    {
        var relative = Path.GetRelativePath(rootDir, filePath);
        var ext = Path.GetExtension(filePath);
        var findings = new List<PublishSecretFinding>();

        if (ext.Equals(".pfx", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".p12", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".pem", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".key", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".cer", StringComparison.OrdinalIgnoreCase))
        {
            findings.Add(new PublishSecretFinding(relative, "(file)", "certificate_or_key_file", PublishFindingSeverity.Critical));
            return findings;
        }

        string text;
        try
        {
            text = File.ReadAllText(filePath, Encoding.UTF8);
        }
        catch
        {
            findings.Add(new PublishSecretFinding(relative, "(file)", "unreadable_sensitive_candidate", PublishFindingSeverity.Critical));
            return findings;
        }

        if (Regex.IsMatch(text, @"-----BEGIN ([A-Z ]+)?PRIVATE KEY-----", RegexOptions.IgnoreCase))
            findings.Add(new PublishSecretFinding(relative, "(content)", "private_key_header", PublishFindingSeverity.Critical));

        if (ext.Equals(".json", StringComparison.OrdinalIgnoreCase))
            findings.AddRange(ScanJson(relative, text));

        // Generic credential markers in non-deps config-like files.
        var fileName = Path.GetFileName(filePath);
        if (!fileName.EndsWith(".deps.json", StringComparison.OrdinalIgnoreCase)
            && !fileName.EndsWith(".runtimeconfig.json", StringComparison.OrdinalIgnoreCase))
        {
            if (Regex.IsMatch(text, @"Password\s*=\s*[^;\s""]+", RegexOptions.IgnoreCase)
                && !relative.Equals("appsettings.json", StringComparison.OrdinalIgnoreCase))
            {
                findings.Add(new PublishSecretFinding(relative, "Password", "inline_password_assignment", PublishFindingSeverity.Critical));
            }

            if (Regex.IsMatch(text, @"ClientSecret\s*[:=]\s*\S+", RegexOptions.IgnoreCase))
                findings.Add(new PublishSecretFinding(relative, "ClientSecret", "client_secret", PublishFindingSeverity.Critical));

            if (Regex.IsMatch(text, @"ApiKey\s*[:=]\s*\S+", RegexOptions.IgnoreCase))
                findings.Add(new PublishSecretFinding(relative, "ApiKey", "api_key", PublishFindingSeverity.Critical));
        }

        return findings;
    }

    private static IEnumerable<PublishSecretFinding> ScanJson(string relativePath, string text)
    {
        var findings = new List<PublishSecretFinding>();
        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(text);
        }
        catch (JsonException)
        {
            return findings;
        }

        using (doc)
        {
            Walk(doc.RootElement, relativePath, "", findings);
        }

        return findings;
    }

    private static void Walk(
        JsonElement element,
        string relativePath,
        string path,
        List<PublishSecretFinding> findings)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in element.EnumerateObject())
            {
                var next = string.IsNullOrEmpty(path) ? prop.Name : path + ":" + prop.Name;
                ClassifyProperty(relativePath, next, prop.Name, prop.Value, findings);
                Walk(prop.Value, relativePath, next, findings);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            var i = 0;
            foreach (var item in element.EnumerateArray())
            {
                Walk(item, relativePath, path + "[" + i + "]", findings);
                i++;
            }
        }
    }

    private static void ClassifyProperty(
        string relativePath,
        string fullKey,
        string name,
        JsonElement value,
        List<PublishSecretFinding> findings)
    {
        if (value.ValueKind is not (JsonValueKind.String or JsonValueKind.Number))
            return;

        var raw = value.ValueKind == JsonValueKind.String ? value.GetString() ?? "" : value.ToString();
        if (string.IsNullOrWhiteSpace(raw))
            return;

        if (name.Equals("DefaultConnection", StringComparison.OrdinalIgnoreCase)
            || fullKey.EndsWith("ConnectionStrings:DefaultConnection", StringComparison.OrdinalIgnoreCase))
        {
            if (LooksLikeConnectionString(raw))
            {
                findings.Add(new PublishSecretFinding(
                    relativePath, fullKey, "connection_string", PublishFindingSeverity.Critical));
            }

            return;
        }

        if (name.Equals("Password", StringComparison.OrdinalIgnoreCase)
            || name.Equals("ClientSecret", StringComparison.OrdinalIgnoreCase)
            || name.Equals("ApiKey", StringComparison.OrdinalIgnoreCase)
            || name.Equals("AccessToken", StringComparison.OrdinalIgnoreCase)
            || name.Equals("CaptchaSecret", StringComparison.OrdinalIgnoreCase)
            || name.Equals("RecaptchaSecret", StringComparison.OrdinalIgnoreCase)
            || name.Equals("SuperAdminPassword", StringComparison.OrdinalIgnoreCase))
        {
            findings.Add(new PublishSecretFinding(relativePath, fullKey, "secret_value", PublishFindingSeverity.Critical));
            return;
        }

        if (name.Equals("Username", StringComparison.OrdinalIgnoreCase)
            && fullKey.Contains("Email", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(raw))
        {
            findings.Add(new PublishSecretFinding(relativePath, fullKey, "smtp_username", PublishFindingSeverity.Critical));
            return;
        }

        // Non-localhost absolute production-looking hosts in publish base config.
        if (relativePath.Equals("appsettings.json", StringComparison.OrdinalIgnoreCase)
            && name.Equals("BaseUrl", StringComparison.OrdinalIgnoreCase)
            && Uri.TryCreate(raw, UriKind.Absolute, out var uri)
            && !IsLoopbackHost(uri.Host))
        {
            findings.Add(new PublishSecretFinding(relativePath, fullKey, "non_localhost_public_url", PublishFindingSeverity.Critical));
        }

        _ = SafeEmptyOrLocalKeys;
    }

    private static bool LooksLikeConnectionString(string value) =>
        value.Contains("Server=", StringComparison.OrdinalIgnoreCase)
        || value.Contains("Data Source=", StringComparison.OrdinalIgnoreCase)
        || value.Contains("Password=", StringComparison.OrdinalIgnoreCase)
        || value.Contains("User ID=", StringComparison.OrdinalIgnoreCase)
        || value.Contains("User Id=", StringComparison.OrdinalIgnoreCase);

    private static bool IsLoopbackHost(string host) =>
        host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
        || host.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase)
        || host.Equals("::1", StringComparison.OrdinalIgnoreCase);
}
