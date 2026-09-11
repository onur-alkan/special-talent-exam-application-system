using OzelYetenekSinavSistemi.Application.Common;

namespace OzelYetenekSinavSistemi.Infrastructure.Services;

/// <summary>
/// JPEG/PNG dosyalarının yapısal bütünlüğünü ve boyutlarını doğrular
/// (yalnızca imza değil; bozuk/truncated ve aşırı piksel dosyaları reddedilir).
/// Yeni görüntü paketi eklemeden çalışır.
/// </summary>
internal static class PhotoImageStructureValidator
{
    public static bool TryValidate(ReadOnlySpan<byte> data, string detectedExtension, out PhotoUploadErrorCode errorCode)
    {
        errorCode = PhotoUploadErrorCode.None;

        if (detectedExtension.Equals(".jpg", StringComparison.OrdinalIgnoreCase)
            || detectedExtension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase))
        {
            return TryValidateJpeg(data, out errorCode);
        }

        if (detectedExtension.Equals(".png", StringComparison.OrdinalIgnoreCase))
        {
            return TryValidatePng(data, out errorCode);
        }

        errorCode = PhotoUploadErrorCode.InvalidSignature;
        return false;
    }

    private static bool TryValidateJpeg(ReadOnlySpan<byte> data, out PhotoUploadErrorCode errorCode)
    {
        errorCode = PhotoUploadErrorCode.InvalidImage;

        if (data.Length < 4 || data[0] != 0xFF || data[1] != 0xD8 || data[2] != 0xFF)
        {
            errorCode = PhotoUploadErrorCode.InvalidSignature;
            return false;
        }

        var i = 2;
        var foundFrame = false;
        long width = 0;
        long height = 0;

        while (i < data.Length - 1)
        {
            if (data[i] != 0xFF)
            {
                i++;
                continue;
            }

            while (i < data.Length && data[i] == 0xFF)
                i++;

            if (i >= data.Length)
                break;

            var marker = data[i++];

            // Standalone markers without length
            if (marker is 0xD8 or 0xD9 or (>= 0xD0 and <= 0xD7) or 0x01)
            {
                if (marker == 0xD9)
                    return foundFrame && IsPixelCountSafe(width, height, out errorCode);

                continue;
            }

            if (i + 1 >= data.Length)
                return false;

            var segmentLength = (data[i] << 8) | data[i + 1];
            if (segmentLength < 2 || i + segmentLength > data.Length)
                return false;

            // SOF0..SOF3, SOF5..SOF7, SOF9..SOF11, SOF13..SOF15 (baseline/progressive frames)
            if (marker is (>= 0xC0 and <= 0xC3) or (>= 0xC5 and <= 0xC7) or (>= 0xC9 and <= 0xCB) or (>= 0xCD and <= 0xCF))
            {
                if (segmentLength < 8)
                    return false;

                height = (data[i + 3] << 8) | data[i + 4];
                width = (data[i + 5] << 8) | data[i + 6];
                if (width <= 0 || height <= 0)
                    return false;

                foundFrame = true;
                if (!IsPixelCountSafe(width, height, out errorCode))
                    return false;
            }

            // SOS: remaining bytes are entropy-coded scan data until EOI
            if (marker == 0xDA)
            {
                i += segmentLength;
                var eoi = FindJpegEoi(data, i);
                if (eoi < 0 || !foundFrame)
                    return false;

                return IsPixelCountSafe(width, height, out errorCode);
            }

            i += segmentLength;
        }

        return false;
    }

    private static int FindJpegEoi(ReadOnlySpan<byte> data, int start)
    {
        for (var i = start; i < data.Length - 1; i++)
        {
            if (data[i] == 0xFF && data[i + 1] == 0xD9)
                return i;
        }

        return -1;
    }

    private static bool TryValidatePng(ReadOnlySpan<byte> data, out PhotoUploadErrorCode errorCode)
    {
        errorCode = PhotoUploadErrorCode.InvalidImage;

        ReadOnlySpan<byte> signature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
        if (data.Length < 33 || !data[..8].SequenceEqual(signature))
        {
            errorCode = data.Length >= 8 && data[..8].SequenceEqual(signature)
                ? PhotoUploadErrorCode.InvalidImage
                : PhotoUploadErrorCode.InvalidSignature;
            return false;
        }

        var offset = 8;
        var sawIhdr = false;
        var sawIend = false;
        long width = 0;
        long height = 0;

        while (offset + 12 <= data.Length)
        {
            var length = ReadBigEndianInt32(data, offset);
            if (length < 0)
                return false;

            var typeOffset = offset + 4;
            var dataOffset = offset + 8;
            var chunkEnd = dataOffset + length + 4; // + CRC
            if (chunkEnd > data.Length)
                return false;

            var type = data.Slice(typeOffset, 4);
            if (!sawIhdr)
            {
                if (!type.SequenceEqual("IHDR"u8) || length != 13)
                    return false;

                width = ReadBigEndianUInt32(data, dataOffset);
                height = ReadBigEndianUInt32(data, dataOffset + 4);
                if (width == 0 || height == 0)
                    return false;

                if (!IsPixelCountSafe(width, height, out errorCode))
                    return false;

                sawIhdr = true;
            }
            else if (type.SequenceEqual("IEND"u8))
            {
                if (length != 0)
                    return false;

                sawIend = true;
                // IEND sonrası veri kabul edilmez
                if (chunkEnd != data.Length)
                    return false;

                break;
            }

            offset = chunkEnd;
        }

        return sawIhdr && sawIend && IsPixelCountSafe(width, height, out errorCode);
    }

    private static bool IsPixelCountSafe(long width, long height, out PhotoUploadErrorCode errorCode)
    {
        errorCode = PhotoUploadErrorCode.None;
        if (width <= 0 || height <= 0)
        {
            errorCode = PhotoUploadErrorCode.InvalidImage;
            return false;
        }

        try
        {
            var pixels = checked(width * height);
            if (pixels > PhotoUploadLimits.MaxTotalPixels)
            {
                errorCode = PhotoUploadErrorCode.InvalidImage;
                return false;
            }
        }
        catch (OverflowException)
        {
            errorCode = PhotoUploadErrorCode.InvalidImage;
            return false;
        }

        return true;
    }

    private static int ReadBigEndianInt32(ReadOnlySpan<byte> data, int offset)
        => (data[offset] << 24) | (data[offset + 1] << 16) | (data[offset + 2] << 8) | data[offset + 3];

    private static long ReadBigEndianUInt32(ReadOnlySpan<byte> data, int offset)
        => ((long)data[offset] << 24) | ((long)data[offset + 1] << 16) | ((long)data[offset + 2] << 8) | data[offset + 3];
}
