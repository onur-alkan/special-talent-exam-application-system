using System.Reflection;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using OzelYetenekSinavSistemi.Application.Common;
using OzelYetenekSinavSistemi.Infrastructure.Configuration;
using OzelYetenekSinavSistemi.Infrastructure.Services;
using OzelYetenekSinavSistemi.Web.Controllers;
using OzelYetenekSinavSistemi.Web.Infrastructure;

namespace OzelYetenekSinavSistemi.Tests.Security;

public sealed class PhotoUploadLimitsTests
{
    [Fact]
    public void Limits_AreAligned_FileSmallerThanMultipart()
    {
        Assert.Equal(2 * 1024 * 1024, PhotoUploadLimits.MaxFileBytes);
        Assert.Equal(3 * 1024 * 1024, PhotoUploadLimits.MaxMultipartRequestBytes);
        Assert.Equal(64 * 1024, PhotoUploadLimits.MemoryBufferThresholdBytes);
        Assert.True(PhotoUploadLimits.MaxMultipartRequestBytes > PhotoUploadLimits.MaxFileBytes);
    }

    [Fact]
    public void Options_DefaultMaxBytes_EqualsFileLimit()
    {
        Assert.Equal(PhotoUploadLimits.MaxFileBytes, new PhotoUploadOptions().MaxBytes);
    }

    [Fact]
    public void Options_RejectsAboveSecurityCeiling()
    {
        var errors = new List<string>();
        PhotoUploadOptionsValidator.ValidateCore(
            new PhotoUploadOptions { MaxBytes = PhotoUploadLimits.MaxFileBytes + 1 },
            errors);
        Assert.Contains(errors, e => e.Contains("en fazla", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Options_AllowsLowerValue()
    {
        var errors = new List<string>();
        PhotoUploadOptionsValidator.ValidateCore(
            new PhotoUploadOptions { MaxBytes = 100 * 1024 },
            errors);
        Assert.Empty(errors);
    }

    [Fact]
    public void Options_RejectsTooSmall()
    {
        var errors = new List<string>();
        PhotoUploadOptionsValidator.ValidateCore(
            new PhotoUploadOptions { MaxBytes = 10 },
            errors);
        Assert.Contains(errors, e => e.Contains("en az", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Program_ConfiguresKestrelIisAndFormOptions()
    {
        var program = File.ReadAllText(FindUnder("src", "OzelYetenekSinavSistemi.Web", "Program.cs"));
        Assert.Contains("ConfigureKestrel", program, StringComparison.Ordinal);
        Assert.Contains("MaxRequestBodySize = PhotoUploadLimits.MaxMultipartRequestBytes", program, StringComparison.Ordinal);
        Assert.Contains("IISServerOptions", program, StringComparison.Ordinal);
        Assert.Contains("FormOptions", program, StringComparison.Ordinal);
        Assert.Contains("MultipartBodyLengthLimit", program, StringComparison.Ordinal);
        Assert.Contains("MemoryBufferThreshold", program, StringComparison.Ordinal);
        Assert.DoesNotContain("MaxRequestBodySize = null", program, StringComparison.Ordinal);
        Assert.DoesNotContain("AllowSynchronousIO = true", program, StringComparison.Ordinal);
    }

    [Fact]
    public void WebConfig_HasThreeMegabyteRequestLimit()
    {
        var webConfig = File.ReadAllText(FindUnder("src", "OzelYetenekSinavSistemi.Web", "web.config"));
        Assert.Contains("maxAllowedContentLength=\"3145728\"", webConfig, StringComparison.Ordinal);
    }

    [Fact]
    public void Controllers_DoNotCopyIFormFileToMemoryStream()
    {
        foreach (var relative in new[]
                 {
                     Path.Combine("Controllers", "AccountController.cs"),
                     Path.Combine("Controllers", "CandidateProfileController.cs")
                 })
        {
            var source = File.ReadAllText(FindUnder("src", "OzelYetenekSinavSistemi.Web", relative));
            Assert.DoesNotContain("new MemoryStream()", source, StringComparison.Ordinal);
            Assert.DoesNotContain("CopyTo(memory)", source, StringComparison.Ordinal);
            Assert.Contains("PhotoUploadFormMapper.ToRequest", source, StringComparison.Ordinal);
            Assert.Contains("RequestSizeLimit", source, StringComparison.Ordinal);
            Assert.Contains("RequestFormLimits", source, StringComparison.Ordinal);
        }

        var mapper = File.ReadAllText(FindUnder(
            "src", "OzelYetenekSinavSistemi.Web", "Infrastructure", "PhotoUploadFormMapper.cs"));
        Assert.Contains("OpenReadStream()", mapper, StringComparison.Ordinal);
        Assert.DoesNotContain("CopyTo", mapper, StringComparison.Ordinal);
    }

    [Fact]
    public void UploadActions_HaveMatchingSizeAttributes()
    {
        AssertHasLimits(typeof(AccountController), nameof(AccountController.Register));
        AssertHasLimits(typeof(CandidateProfileController), nameof(CandidateProfileController.Index));
    }

    [Fact]
    public async Task SavePhoto_ClaimedLengthUnderLimit_ActualOverLimit_FailsWithoutFile()
    {
        var storage = Path.Combine(Path.GetTempPath(), "oys-upload-limit-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(storage);
        try
        {
            const int maxBytes = 64;
            var sut = new PhotoUploadService(Options.Create(new PhotoUploadOptions
            {
                StorageRootPath = storage,
                MaxBytes = maxBytes
            }));

            var oversized = new byte[maxBytes + 16];
            oversized[0] = 0xFF;
            oversized[1] = 0xD8;
            oversized[2] = 0xFF;

            var result = await sut.SavePhotoAsync(new PhotoUploadRequest
            {
                Content = new MemoryStream(oversized),
                FileName = "photo.jpg",
                ContentType = "image/jpeg",
                Length = 8 // yanlış bildirilen uzunluk
            });

            Assert.False(result.Success);
            Assert.Empty(Directory.EnumerateFiles(storage));
        }
        finally
        {
            if (Directory.Exists(storage))
                Directory.Delete(storage, recursive: true);
        }
    }

    [Fact]
    public void FormOptionsDefaults_MatchLimits()
    {
        var options = new FormOptions
        {
            MultipartBodyLengthLimit = PhotoUploadLimits.MaxMultipartRequestBytes,
            MemoryBufferThreshold = PhotoUploadLimits.MemoryBufferThresholdBytes
        };
        Assert.Equal(PhotoUploadLimits.MaxMultipartRequestBytes, options.MultipartBodyLengthLimit);
        Assert.Equal(PhotoUploadLimits.MemoryBufferThresholdBytes, options.MemoryBufferThreshold);
    }

    private static void AssertHasLimits(Type controller, string methodName)
    {
        var method = controller.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
            .Where(m => m.Name == methodName && m.GetCustomAttribute<HttpPostAttribute>() is not null)
            .Single();

        var size = method.GetCustomAttribute<RequestSizeLimitAttribute>();
        Assert.NotNull(size);

        var form = method.GetCustomAttribute<RequestFormLimitsAttribute>();
        Assert.NotNull(form);
        Assert.Equal(PhotoUploadLimits.MaxMultipartRequestBytes, form!.MultipartBodyLengthLimit);
        Assert.Equal(PhotoUploadLimits.MemoryBufferThresholdBytes, form.MemoryBufferThreshold);
    }

    private static string FindUnder(params string[] parts)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var path = Path.Combine(new[] { dir.FullName }.Concat(parts).ToArray());
            if (File.Exists(path) || Directory.Exists(path))
                return path;
            dir = dir.Parent;
        }

        throw new FileNotFoundException(string.Join(Path.DirectorySeparatorChar, parts));
    }
}
