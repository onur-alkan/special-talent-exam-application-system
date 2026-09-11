using OzelYetenekSinavSistemi.Application.Interfaces.Services;

namespace OzelYetenekSinavSistemi.Tests.Validation.Admin;

/// <summary>
/// Test host'ta gerçek captcha doğrulama akışını korur; yalnızca üretilen kod deterministiktir.
/// </summary>
internal sealed class IntegrationTestCaptchaService : ICaptchaService
{
    internal const string FixedCode = "TST01";

    public CaptchaChallenge Generate() =>
        new(FixedCode, "<svg xmlns='http://www.w3.org/2000/svg' width='10' height='10'></svg>"u8.ToArray(), "image/svg+xml");

    public bool Validate(string? expectedCode, string? userInput) =>
        !string.IsNullOrWhiteSpace(expectedCode)
        && !string.IsNullOrWhiteSpace(userInput)
        && string.Equals(expectedCode.Trim(), userInput.Trim(), StringComparison.OrdinalIgnoreCase);
}
