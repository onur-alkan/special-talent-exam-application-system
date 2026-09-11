using Microsoft.Extensions.Options;
using OzelYetenekSinavSistemi.Application.Common;
using OzelYetenekSinavSistemi.Infrastructure.Configuration;
using OzelYetenekSinavSistemi.Infrastructure.Services;
using OzelYetenekSinavSistemi.Tests.Services;

namespace OzelYetenekSinavSistemi.Tests.Security;

public sealed class PhotoUploadServiceTests : IDisposable
{
    private static readonly byte[] ValidJpeg = PhotoUploadTestSupport.MinimalJpeg;
    private static readonly byte[] ValidPng = PhotoUploadTestSupport.MinimalPng;

    private readonly string _root;
    private readonly string _webRoot;
    private readonly string _storageRoot;
    private readonly PhotoUploadService _sut;
    private readonly long _maxBytes = 2 * 1024 * 1024;

    public PhotoUploadServiceTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "oys-tests-" + Guid.NewGuid().ToString("N"));
        _webRoot = Path.Combine(_root, "wwwroot");
        _storageRoot = Path.Combine(_root, "App_Data", "private-uploads", "photos");
        Directory.CreateDirectory(_webRoot);
        Directory.CreateDirectory(_storageRoot);
        var options = Options.Create(new PhotoUploadOptions
        {
            WebRootPath = _webRoot,
            StorageRootPath = _storageRoot,
            MaxBytes = _maxBytes
        });
        _sut = new PhotoUploadService(options);
    }

    private static PhotoUploadRequest Request(byte[] content, string fileName, string contentType, long? length = null) => new()
    {
        Content = new MemoryStream(content),
        FileName = fileName,
        ContentType = contentType,
        Length = length ?? content.Length
    };

    [Fact]
    public async Task SavePhoto_StoresUnderPrivateStorage_NotWwwroot()
    {
        var result = await _sut.SavePhotoAsync(Request(ValidJpeg, "photo.jpg", "image/jpeg"));

        Assert.True(result.Success);
        Assert.StartsWith("/uploads/photos/", result.StoredPath);
        var fileName = Path.GetFileName(result.StoredPath);
        Assert.True(File.Exists(Path.Combine(_storageRoot, fileName!)));
        Assert.False(File.Exists(Path.Combine(_webRoot, "uploads", "photos", fileName!)));
    }

    [Fact]
    public async Task SavePhoto_ValidPng_Succeeds()
    {
        var result = await _sut.SavePhotoAsync(Request(ValidPng, "photo.png", "image/png"));

        Assert.True(result.Success);
        Assert.EndsWith(".png", result.StoredPath);
    }

    [Fact]
    public async Task SavePhoto_InvalidExtension_Fails()
    {
        var result = await _sut.SavePhotoAsync(Request(ValidJpeg, "photo.gif", "image/gif"));
        Assert.False(result.Success);
    }

    [Fact]
    public async Task SavePhoto_MagicBytesMismatch_FailsAndCreatesNoFile()
    {
        var fakeContent = new byte[] { 0x25, 0x50, 0x44, 0x46 };
        var result = await _sut.SavePhotoAsync(Request(fakeContent, "photo.jpg", "image/jpeg"));

        Assert.False(result.Success);
        Assert.Empty(Directory.EnumerateFiles(_storageRoot));
    }

    [Fact]
    public async Task SavePhoto_DoubleExtension_Fails()
    {
        var result = await _sut.SavePhotoAsync(Request(ValidJpeg, "photo.php.jpg", "image/jpeg"));
        Assert.False(result.Success);
    }

    [Fact]
    public async Task SavePhoto_PathTraversalFileName_StaysInsidePrivateFolder()
    {
        var result = await _sut.SavePhotoAsync(Request(ValidJpeg, "../../../evil.jpg", "image/jpeg"));

        Assert.True(result.Success);
        Assert.StartsWith("/uploads/photos/", result.StoredPath);
        Assert.DoesNotContain("..", result.StoredPath!);
        Assert.True(File.Exists(Path.Combine(_storageRoot, Path.GetFileName(result.StoredPath)!)));
    }

    [Fact]
    public async Task SavePhoto_ClaimedLengthSmall_ButActualExceedsMax_Fails()
    {
        var maxBytes = ValidJpeg.Length;
        var sut = new PhotoUploadService(Options.Create(new PhotoUploadOptions
        {
            StorageRootPath = _storageRoot,
            MaxBytes = maxBytes
        }));
        var content = PhotoUploadTestSupport.CreateJpegOfLength(maxBytes + 8);

        var result = await sut.SavePhotoAsync(Request(content, "photo.jpg", "image/jpeg", length: 16));

        Assert.False(result.Success);
        Assert.Empty(Directory.EnumerateFiles(_storageRoot));
    }

    [Fact]
    public async Task SavePhoto_ExactMaxBytes_Succeeds()
    {
        var maxBytes = ValidJpeg.Length;
        var sut = new PhotoUploadService(Options.Create(new PhotoUploadOptions
        {
            StorageRootPath = _storageRoot,
            MaxBytes = maxBytes
        }));

        var result = await sut.SavePhotoAsync(Request(ValidJpeg, "photo.jpg", "image/jpeg"));
        Assert.True(result.Success);
    }

    [Fact]
    public async Task SavePhoto_NonSeekableStream_Succeeds()
    {
        await using var nonSeekable = new NonSeekableStream(new MemoryStream(ValidJpeg));
        var request = new PhotoUploadRequest
        {
            Content = nonSeekable,
            FileName = "photo.jpg",
            ContentType = "image/jpeg",
            Length = ValidJpeg.Length
        };

        var result = await _sut.SavePhotoAsync(request);
        Assert.True(result.Success);
    }

    [Fact]
    public async Task ReadPhoto_ValidJpegAndPng_Succeeds()
    {
        var jpeg = await _sut.SavePhotoAsync(Request(ValidJpeg, "photo.jpg", "image/jpeg"));
        var png = await _sut.SavePhotoAsync(Request(ValidPng, "photo.png", "image/png"));

        var jpegRead = await _sut.ReadPhotoAsync(jpeg.StoredPath);
        var pngRead = await _sut.ReadPhotoAsync(png.StoredPath);

        Assert.True(jpegRead.Success);
        Assert.Equal("image/jpeg", jpegRead.Data!.ContentType);
        Assert.True(pngRead.Success);
        Assert.Equal("image/png", pngRead.Data!.ContentType);
    }

    [Theory]
    [InlineData("/uploads/photos/../secret.jpg")]
    [InlineData("C:/Windows/win.ini")]
    [InlineData("/uploads/photos/not-a-guid.jpg")]
    [InlineData("/other/aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa.jpg")]
    public async Task ReadPhoto_InvalidLogicalPath_Fails(string path)
    {
        var result = await _sut.ReadPhotoAsync(path);
        Assert.False(result.Success);
    }

    [Fact]
    public async Task ReadPhoto_OutsidePrivateFolder_Fails()
    {
        var outside = Path.Combine(_root, "outside.jpg");
        await File.WriteAllBytesAsync(outside, ValidJpeg);
        var fakeLogical = $"/uploads/photos/{Guid.NewGuid():N}.jpg";

        var result = await _sut.ReadPhotoAsync(fakeLogical);
        Assert.False(result.Success);
        Assert.True(File.Exists(outside));
    }

    [Fact]
    public async Task DeletePhoto_PathTraversal_IsRejected()
    {
        Assert.False(await _sut.DeletePhotoAsync("/uploads/photos/../../../Windows/win.ini"));
    }

    [Fact]
    public async Task DeletePhoto_OutsidePrivateFolder_IsNotDeleted()
    {
        var outsideName = $"{Guid.NewGuid():N}.jpg";
        var outside = Path.Combine(_root, outsideName);
        await File.WriteAllBytesAsync(outside, ValidJpeg);

        var deleted = await _sut.DeletePhotoAsync($"/uploads/photos/{outsideName}");

        Assert.True(deleted);
        Assert.True(File.Exists(outside));
    }

    [Fact]
    public async Task DeletePhoto_MissingFile_IsIdempotent()
    {
        Assert.True(await _sut.DeletePhotoAsync($"/uploads/photos/{Guid.NewGuid():N}.jpg"));
    }

    [Fact]
    public async Task DeletePhoto_ValidGeneratedFile_Deletes()
    {
        var save = await _sut.SavePhotoAsync(Request(ValidJpeg, "photo.jpg", "image/jpeg"));
        var physical = Path.Combine(_storageRoot, Path.GetFileName(save.StoredPath)!);
        Assert.True(File.Exists(physical));

        Assert.True(await _sut.DeletePhotoAsync(save.StoredPath));
        Assert.False(File.Exists(physical));
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch { /* ignore */ }
    }

    private sealed class NonSeekableStream : Stream
    {
        private readonly Stream _inner;
        public NonSeekableStream(Stream inner) => _inner = inner;
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }
        public override void Flush() => _inner.Flush();
        public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            _inner.ReadAsync(buffer, cancellationToken);
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        protected override void Dispose(bool disposing)
        {
            if (disposing) _inner.Dispose();
            base.Dispose(disposing);
        }
    }
}
