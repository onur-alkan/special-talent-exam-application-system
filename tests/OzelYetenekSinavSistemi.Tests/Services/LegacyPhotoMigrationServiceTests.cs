using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using OzelYetenekSinavSistemi.Infrastructure.Configuration;
using OzelYetenekSinavSistemi.Infrastructure.Services;

namespace OzelYetenekSinavSistemi.Tests.Services;

public sealed class LegacyPhotoMigrationServiceTests : IDisposable
{
    private static readonly byte[] JpegHeader = { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10 };

    private readonly string _root;
    private readonly string _webRoot;
    private readonly string _legacyFolder;
    private readonly string _privateFolder;
    private readonly LegacyPhotoMigrationService _sut;

    public LegacyPhotoMigrationServiceTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "oys-mig-" + Guid.NewGuid().ToString("N"));
        _webRoot = Path.Combine(_root, "wwwroot");
        _legacyFolder = Path.Combine(_webRoot, "uploads", "photos");
        _privateFolder = Path.Combine(_root, "App_Data", "private-uploads", "photos");
        Directory.CreateDirectory(_legacyFolder);
        Directory.CreateDirectory(_privateFolder);

        _sut = new LegacyPhotoMigrationService(
            Options.Create(new PhotoUploadOptions
            {
                WebRootPath = _webRoot,
                StorageRootPath = _privateFolder
            }),
            NullLogger<LegacyPhotoMigrationService>.Instance);
    }

    [Fact]
    public async Task Migrate_MovesLegacyFile_AndDeletesSource()
    {
        var fileName = $"{Guid.NewGuid():N}.jpg";
        var source = Path.Combine(_legacyFolder, fileName);
        await File.WriteAllBytesAsync(source, JpegHeader);

        await _sut.MigrateAsync();

        Assert.False(File.Exists(source));
        Assert.True(File.Exists(Path.Combine(_privateFolder, fileName)));
    }

    [Fact]
    public async Task Migrate_SecondRun_DoesNotThrow()
    {
        var fileName = $"{Guid.NewGuid():N}.jpg";
        await File.WriteAllBytesAsync(Path.Combine(_legacyFolder, fileName), JpegHeader);

        await _sut.MigrateAsync();
        await _sut.MigrateAsync();

        Assert.True(File.Exists(Path.Combine(_privateFolder, fileName)));
        Assert.False(File.Exists(Path.Combine(_legacyFolder, fileName)));
    }

    [Fact]
    public async Task Migrate_UnsafeFileName_IsLeftUntouched()
    {
        var unsafeName = "evil.php.jpg";
        var source = Path.Combine(_legacyFolder, unsafeName);
        await File.WriteAllBytesAsync(source, JpegHeader);

        await _sut.MigrateAsync();

        Assert.True(File.Exists(source));
        Assert.False(File.Exists(Path.Combine(_privateFolder, unsafeName)));
    }

    [Fact]
    public async Task Migrate_ExistingDifferentTarget_DoesNotOverwrite()
    {
        var fileName = $"{Guid.NewGuid():N}.jpg";
        var source = Path.Combine(_legacyFolder, fileName);
        var destination = Path.Combine(_privateFolder, fileName);
        await File.WriteAllBytesAsync(source, JpegHeader);
        await File.WriteAllBytesAsync(destination, new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A });

        await _sut.MigrateAsync();

        Assert.True(File.Exists(source));
        Assert.Equal(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }, await File.ReadAllBytesAsync(destination));
    }

    [Fact]
    public async Task Migrate_ExistingSameTarget_DeletesSource()
    {
        var fileName = $"{Guid.NewGuid():N}.jpg";
        var source = Path.Combine(_legacyFolder, fileName);
        var destination = Path.Combine(_privateFolder, fileName);
        await File.WriteAllBytesAsync(source, JpegHeader);
        await File.WriteAllBytesAsync(destination, JpegHeader);

        await _sut.MigrateAsync();

        Assert.False(File.Exists(source));
        Assert.True(File.Exists(destination));
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch { /* ignore */ }
    }
}
