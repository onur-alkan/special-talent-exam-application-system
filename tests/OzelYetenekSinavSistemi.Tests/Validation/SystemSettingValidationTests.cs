using Moq;
using OzelYetenekSinavSistemi.Application.Interfaces.Repositories;
using OzelYetenekSinavSistemi.Application.Validation;

namespace OzelYetenekSinavSistemi.Tests.Validation;

public sealed class SystemSettingValidationTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ValidateKey_RejectsEmpty(string? key)
    {
        Assert.False(SystemSettingValidator.TryValidateKey(key, out _, out var error));
        Assert.Equal("Ayar anahtarını giriniz.", error);
    }

    [Theory]
    [InlineData("UnknownKey")]
    [InlineData("MaxPhotoSizeKb\u200B")]
    [InlineData("'; DROP TABLE--")]
    public void ValidateKey_RejectsUnknownOrUnsafe(string key)
    {
        Assert.False(SystemSettingValidator.TryValidateKey(key, out _, out var error));
        Assert.False(string.IsNullOrWhiteSpace(error));
    }

    [Fact]
    public void ValidateKey_AcceptsKnownKey()
    {
        Assert.True(SystemSettingValidator.TryValidateKey("MaxPhotoSizeKb", out var key, out _));
        Assert.Equal("MaxPhotoSizeKb", key);
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("50")]
    [InlineData("99999")]
    public void ValidateValue_RejectsInvalidPhotoSize(string value)
    {
        Assert.False(SystemSettingValidator.TryValidateValue(
            SystemSettingValidator.MaxPhotoSizeKbKey, value, out _, out var error));
        Assert.Contains("KB", error, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidateValue_AcceptsValidPhotoSize()
    {
        Assert.True(SystemSettingValidator.TryValidateValue(
            SystemSettingValidator.MaxPhotoSizeKbKey, "2048", out var normalized, out _));
        Assert.Equal("2048", normalized);
    }

    [Fact]
    public async Task Repository_UpdateUnknownKey_DoesNotInsert()
    {
        var repo = new Mock<ISystemSettingRepository>(MockBehavior.Strict);
        repo.Setup(r => r.UpdateValueAsync("UnknownKey", "1", It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var updated = await repo.Object.UpdateValueAsync("UnknownKey", "1", Guid.NewGuid());
        Assert.False(updated);
    }
}
