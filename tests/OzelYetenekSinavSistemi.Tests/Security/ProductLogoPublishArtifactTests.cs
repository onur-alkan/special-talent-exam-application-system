using System.Diagnostics;
using System.Text;
using OzelYetenekSinavSistemi.Web.Infrastructure;

namespace OzelYetenekSinavSistemi.Tests.Security;

/// <summary>
/// Release publish must include the neutral product logo.
/// Isolated from other <c>dotnet publish</c> tests via <see cref="DotnetPublishIsolationCollection"/>.
/// </summary>
[Collection(nameof(DotnetPublishIsolationCollection))]
public sealed class ProductLogoPublishArtifactTests
{
    [Fact]
    public async Task ReleasePublishOutput_IncludesProductLogo()
    {
        var project = FindUnder("src", "OzelYetenekSinavSistemi.Web", "OzelYetenekSinavSistemi.Web.csproj");
        var publishDir = Path.Combine(Path.GetTempPath(), $"oys-logo-publish-{Guid.NewGuid():N}");

        try
        {
            Directory.CreateDirectory(publishDir);

            var psi = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"publish \"{project}\" -c Release -o \"{publishDir}\" --verbosity quiet",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };

            using var process = Process.Start(psi)
                ?? throw new InvalidOperationException("Failed to start dotnet publish process.");

            var stdoutTask = process.StandardOutput.ReadToEndAsync();
            var stderrTask = process.StandardError.ReadToEndAsync();

            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromMilliseconds(180_000));
            try
            {
                await process.WaitForExitAsync(timeoutCts.Token);
            }
            catch (OperationCanceledException)
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch
                {
                    // best-effort
                }

                var partialOut = stdoutTask.IsCompletedSuccessfully ? await stdoutTask : "(stdout incomplete)";
                var partialErr = stderrTask.IsCompletedSuccessfully ? await stderrTask : "(stderr incomplete)";
                throw new TimeoutException(
                    "dotnet publish timed out after 180000 ms."
                    + Environment.NewLine + "--- stdout ---" + Environment.NewLine + Truncate(partialOut)
                    + Environment.NewLine + "--- stderr ---" + Environment.NewLine + Truncate(partialErr));
            }

            var stdout = await stdoutTask;
            var stderr = await stderrTask;

            Assert.True(
                process.HasExited,
                "dotnet publish process did not report HasExited=true after WaitForExitAsync.");

            Assert.True(
                process.ExitCode == 0,
                "dotnet publish failed."
                + Environment.NewLine + $"ExitCode={process.ExitCode}"
                + Environment.NewLine + "--- stdout ---" + Environment.NewLine + Truncate(stdout)
                + Environment.NewLine + "--- stderr ---" + Environment.NewLine + Truncate(stderr));

            var published = Path.Combine(
                publishDir,
                "wwwroot",
                "images",
                "branding",
                "oys-logo.png");

            Assert.True(
                File.Exists(published),
                "Product logo was not copied to Release publish output."
                + Environment.NewLine + $"Expected: {published}"
                + Environment.NewLine + $"RelativeWebPath: {OfficialUniversityBranding.RelativeWebPath}"
                + Environment.NewLine + "--- stdout ---" + Environment.NewLine + Truncate(stdout)
                + Environment.NewLine + "--- stderr ---" + Environment.NewLine + Truncate(stderr));

            Assert.True(new FileInfo(published).Length > 0, "Published product logo file is empty.");
        }
        finally
        {
            if (Directory.Exists(publishDir))
                Directory.Delete(publishDir, recursive: true);
        }
    }

    private static string Truncate(string text, int maxChars = 4000)
    {
        if (string.IsNullOrEmpty(text))
            return "(empty)";
        return text.Length <= maxChars
            ? text
            : text[..maxChars] + $"{Environment.NewLine}... (truncated, {text.Length} chars total)";
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
