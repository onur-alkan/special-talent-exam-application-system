using System.Net;
using System.Text;
using OzelYetenekSinavSistemi.Application.Common;
using OzelYetenekSinavSistemi.Application.Interfaces.Services;
using OzelYetenekSinavSistemi.Domain.Enums;

namespace OzelYetenekSinavSistemi.Application.Services;

public sealed class SensitiveDataMaskingService : ISensitiveDataMaskingService
{
    public const string EmptyIdentityDisplay = "-";

    public const int MaxCorrelationIdLength = 64;
    public const int MaxRequestPathLength = 400;
    public const int MaxEventTypeLength = 100;
    public const int MaxDescriptionLength = 2000;
    public const int MaxIpAddressLength = 64;
    public const int DefaultMaxLogLength = 2000;

    public string MaskTcNo(string? tcNo)
    {
        if (string.IsNullOrWhiteSpace(tcNo))
            return string.Empty;

        tcNo = tcNo.Trim();
        if (tcNo.Length < 5)
            return new string('*', tcNo.Length);

        var start = tcNo[..3];
        var end = tcNo[^2..];
        var maskedLength = tcNo.Length - 5;
        return $"{start}{new string('*', maskedLength)}{end}";
    }

    public string MaskIdentityNumber(
        IdentityDocumentType? type,
        string? identityNumber,
        string? legacyTcNo)
    {
        var resolvedType = type;
        if (!resolvedType.HasValue && !string.IsNullOrWhiteSpace(legacyTcNo))
            resolvedType = IdentityDocumentType.TurkishIdentityNumber;

        var raw = !string.IsNullOrWhiteSpace(identityNumber)
            ? identityNumber.Trim()
            : legacyTcNo?.Trim();

        if (string.IsNullOrWhiteSpace(raw))
            return EmptyIdentityDisplay;

        if (resolvedType == IdentityDocumentType.TurkishIdentityNumber)
            return MaskTcNo(raw);

        return MaskForeignOrPassport(raw);
    }

    private static string MaskForeignOrPassport(string value)
    {
        if (value.Length <= 4)
            return new string('*', value.Length);

        var start = value[..2];
        var end = value[^2..];
        return $"{start}{new string('*', value.Length - 4)}{end}";
    }

    public string MaskEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return string.Empty;

        var atIndex = email.IndexOf('@');
        if (atIndex <= 0)
            return "***";

        var local = email[..atIndex];
        var domain = email[atIndex..];
        var visible = local.Length <= 2 ? local[..1] : local[..2];
        return $"{visible}{new string('*', Math.Max(1, local.Length - visible.Length))}{domain}";
    }

    public string MaskPhone(string? phone) => MobilePhoneNumber.Mask(phone);

    public string SanitizeForLog(string? input) =>
        SanitizeForLog(input, DefaultMaxLogLength);

    public string SanitizeForLog(string? input, int maxLength)
    {
        if (string.IsNullOrEmpty(input))
            return string.Empty;

        if (maxLength < 1)
            maxLength = 1;

        var sb = new StringBuilder(Math.Min(input.Length, maxLength));
        foreach (var ch in input)
        {
            if (ch is '\r' or '\n' or '\t')
            {
                sb.Append(' ');
            }
            else if (!char.IsControl(ch))
            {
                sb.Append(ch);
            }

            if (sb.Length >= maxLength)
                break;
        }

        return sb.ToString().Trim();
    }

    public string SanitizeCorrelationId(string? correlationId) =>
        SanitizeForLog(correlationId, MaxCorrelationIdLength);

    public string SanitizeRequestPath(string? requestPath) =>
        SanitizeForLog(requestPath, MaxRequestPathLength);

    public string SanitizeEventType(string? eventType)
    {
        var sanitized = SanitizeForLog(eventType, MaxEventTypeLength);
        return string.IsNullOrEmpty(sanitized) ? "Unknown" : sanitized;
    }

    public string? SanitizeDescription(string? description)
    {
        var sanitized = SanitizeForLog(description, MaxDescriptionLength);
        return string.IsNullOrEmpty(sanitized) ? null : sanitized;
    }

    public string? SanitizeIpAddress(string? ipAddress)
    {
        var sanitized = SanitizeForLog(ipAddress, MaxIpAddressLength);
        if (string.IsNullOrWhiteSpace(sanitized))
            return null;

        // Geçersiz veya aşırı uzun IP loglara yazılmaz.
        if (sanitized.Length > MaxIpAddressLength)
            return null;

        return IPAddress.TryParse(sanitized, out _) ? sanitized : null;
    }
}
