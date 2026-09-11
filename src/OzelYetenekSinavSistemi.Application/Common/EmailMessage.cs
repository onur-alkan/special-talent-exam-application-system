namespace OzelYetenekSinavSistemi.Application.Common;

/// <summary>Güvenli e-posta gönderim modeli. Servis HTML üretmez; yalnızca iletir.</summary>
public sealed class EmailMessage
{
    public required string To { get; init; }
    public required string Subject { get; init; }
    public required string HtmlBody { get; init; }
    public required string PlainTextBody { get; init; }
}
