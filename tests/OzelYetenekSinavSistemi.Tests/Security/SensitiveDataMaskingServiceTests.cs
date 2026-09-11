using Microsoft.AspNetCore.Http;
using OzelYetenekSinavSistemi.Application.Services;
using OzelYetenekSinavSistemi.Infrastructure.Configuration;
using OzelYetenekSinavSistemi.Infrastructure.Services;

namespace OzelYetenekSinavSistemi.Tests.Security;

public sealed class SensitiveDataMaskingServiceTests
{
    private readonly SensitiveDataMaskingService _sut = new();

    [Fact]
    public void MaskTcNo_ShowsFirstThreeAndLastTwo()
    {
        var masked = _sut.MaskTcNo("12345678901");
        Assert.Equal("123******01", masked);
        Assert.DoesNotContain("45678", masked);
    }

    [Fact]
    public void MaskTcNo_Null_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, _sut.MaskTcNo(null));
    }

    [Fact]
    public void MaskEmail_HidesLocalPart()
    {
        var masked = _sut.MaskEmail("candidate@example.com");
        Assert.StartsWith("ca", masked);
        Assert.EndsWith("@example.com", masked);
        Assert.Contains("*", masked);
    }

    [Fact]
    public void MaskPhone_HidesMiddleDigits()
    {
        var masked = _sut.MaskPhone("+905551234567");
        Assert.Equal("+90******4567", masked);
        Assert.DoesNotContain("555123", masked, StringComparison.Ordinal);
    }

    [Fact]
    public void SanitizeForLog_RemovesCrLfAndTab_IntoSingleLine()
    {
        var input = "Kullanici\r\nEnjekte\tedilen satir";
        var sanitized = _sut.SanitizeForLog(input);
        Assert.DoesNotContain("\r", sanitized);
        Assert.DoesNotContain("\n", sanitized);
        Assert.DoesNotContain("\t", sanitized);
        Assert.Contains("Kullanici", sanitized);
        Assert.Contains("Enjekte", sanitized);
    }

    [Fact]
    public void SanitizeForLog_RemovesControlChars()
    {
        var input = "abc\u0000def\u0007";
        var sanitized = _sut.SanitizeForLog(input);
        Assert.DoesNotContain('\u0000', sanitized);
        Assert.DoesNotContain('\u0007', sanitized);
        Assert.Contains("abc", sanitized);
        Assert.Contains("def", sanitized);
    }

    [Fact]
    public void SanitizeRequestPath_TruncatesTo400()
    {
        var longPath = "/" + new string('a', 500);
        var sanitized = _sut.SanitizeRequestPath(longPath);
        Assert.True(sanitized.Length <= 400);
    }

    [Fact]
    public void SanitizeCorrelationId_TruncatesTo64()
    {
        var longId = new string('b', 100);
        var sanitized = _sut.SanitizeCorrelationId(longId);
        Assert.Equal(64, sanitized.Length);
    }

    [Fact]
    public void SanitizeIpAddress_Invalid_ReturnsNull()
    {
        Assert.Null(_sut.SanitizeIpAddress("not-an-ip"));
        Assert.Null(_sut.SanitizeIpAddress(new string('1', 80)));
        Assert.Equal("127.0.0.1", _sut.SanitizeIpAddress("127.0.0.1"));
    }

    [Fact]
    public void SanitizeEventType_Empty_ReturnsUnknown()
    {
        Assert.Equal("Unknown", _sut.SanitizeEventType("   "));
        Assert.True(_sut.SanitizeEventType(new string('x', 150)).Length <= 100);
    }

    [Fact]
    public void SanitizeDescription_Respects2000Limit()
    {
        var description = new string('d', 2500);
        var sanitized = _sut.SanitizeDescription(description);
        Assert.NotNull(sanitized);
        Assert.True(sanitized!.Length <= 2000);
    }

    [Fact]
    public void ResetPasswordPath_ExcludesQueryStringToken()
    {
        var context = new DefaultHttpContext();
        context.Request.Path = "/Account/ResetPassword";
        context.Request.QueryString = new QueryString("?token=SUPER-SECRET-RESET-TOKEN");

        var path = _sut.SanitizeRequestPath(context.Request.Path.Value);

        Assert.Equal("/Account/ResetPassword", path);
        Assert.DoesNotContain("token", path, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SUPER-SECRET", path, StringComparison.Ordinal);
        Assert.DoesNotContain(context.Request.QueryString.Value ?? string.Empty, path);
    }
}

public sealed class DataRetentionOptionsTests
{
    private readonly DataRetentionOptionsValidator _validator = new();

    [Fact]
    public void ValidDefaults_Succeed()
    {
        var result = _validator.Validate(null, new DataRetentionOptions());
        Assert.True(result.Succeeded);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(DataRetentionOptions.MaxRetentionDays + 1)]
    public void InvalidSerilogDays_Fails(int days)
    {
        var options = new DataRetentionOptions { SerilogSqlDays = days };
        Assert.False(_validator.Validate(null, options).Succeeded);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(DataRetentionOptions.MaxBatchSize + 1)]
    public void InvalidBatchSize_Fails(int batch)
    {
        var options = new DataRetentionOptions { BatchSize = batch };
        Assert.False(_validator.Validate(null, options).Succeeded);
    }
}

public sealed class DataRetentionCleanupRuleTests
{
    [Fact]
    public void ActiveUnexpiredToken_IsNotDeleted()
    {
        var now = DateTime.UtcNow;
        var cutoff = now.AddDays(-7);
        Assert.False(DataRetentionCleanupService.ShouldDeletePasswordResetToken(
            usedAt: null,
            expiresAt: now.AddHours(1),
            now: now,
            cutoff: cutoff));
    }

    [Fact]
    public void OldUsedToken_IsDeleted()
    {
        var now = DateTime.UtcNow;
        var cutoff = now.AddDays(-7);
        Assert.True(DataRetentionCleanupService.ShouldDeletePasswordResetToken(
            usedAt: now.AddDays(-10),
            expiresAt: now.AddDays(-9),
            now: now,
            cutoff: cutoff));
    }

    [Fact]
    public void OldExpiredToken_IsDeleted()
    {
        var now = DateTime.UtcNow;
        var cutoff = now.AddDays(-7);
        Assert.True(DataRetentionCleanupService.ShouldDeletePasswordResetToken(
            usedAt: null,
            expiresAt: now.AddDays(-10),
            now: now,
            cutoff: cutoff));
    }

    [Fact]
    public void RecentlyExpiredButWithinRetention_IsNotDeleted()
    {
        var now = DateTime.UtcNow;
        var cutoff = now.AddDays(-7);
        Assert.False(DataRetentionCleanupService.ShouldDeletePasswordResetToken(
            usedAt: null,
            expiresAt: now.AddDays(-1),
            now: now,
            cutoff: cutoff));
    }
}
