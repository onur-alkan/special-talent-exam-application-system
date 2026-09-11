using System.Text.Encodings.Web;
using OzelYetenekSinavSistemi.Application.Common;
using OzelYetenekSinavSistemi.Application.Interfaces.Services;

namespace OzelYetenekSinavSistemi.Application.Services;

public interface IPasswordResetEmailTemplate
{
    EmailMessage Create(string? firstName, string toEmail, string resetLink);
}

/// <summary>
/// Parola sıfırlama e-postasının HTML ve düz metin gövdesini üretir.
/// Dinamik değerler HTML encode edilir; konu kullanıcı girdisi içermez.
/// </summary>
public sealed class PasswordResetEmailTemplate : IPasswordResetEmailTemplate
{
    public const string Subject = "Parola Sıfırlama";

    public EmailMessage Create(string? firstName, string toEmail, string resetLink)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(toEmail);
        ArgumentException.ThrowIfNullOrWhiteSpace(resetLink);

        var displayName = string.IsNullOrWhiteSpace(firstName) ? null : firstName.Trim();
        var encodedName = displayName is null ? null : HtmlEncoder.Default.Encode(displayName);
        var encodedLink = HtmlEncoder.Default.Encode(resetLink);

        var greetingHtml = encodedName is null
            ? "Merhaba,"
            : $"Merhaba {encodedName},";

        var greetingText = displayName is null
            ? "Merhaba,"
            : $"Merhaba {displayName},";

        var htmlBody =
            $"<p>{greetingHtml}</p>" +
            "<p>Parola sıfırlama talebiniz alındı. Aşağıdaki bağlantı 2 saat boyunca geçerlidir:</p>" +
            $"<p><a href=\"{encodedLink}\">Parolamı Sıfırla</a></p>" +
            "<p>Bu talebi siz yapmadıysanız bu e-postayı yok sayabilirsiniz.</p>";

        var plainText =
            $"{greetingText}\n\n" +
            "Parola sıfırlama talebiniz alındı. Aşağıdaki bağlantı 2 saat boyunca geçerlidir:\n\n" +
            $"{resetLink}\n\n" +
            "Bu talebi siz yapmadıysanız bu e-postayı yok sayabilirsiniz.\n";

        return new EmailMessage
        {
            To = toEmail.Trim(),
            Subject = Subject,
            HtmlBody = htmlBody,
            PlainTextBody = plainText
        };
    }
}
