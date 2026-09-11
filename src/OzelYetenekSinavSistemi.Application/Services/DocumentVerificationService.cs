using System.Text.RegularExpressions;
using OzelYetenekSinavSistemi.Application.Interfaces.Repositories;
using OzelYetenekSinavSistemi.Application.Interfaces.Services;
using OzelYetenekSinavSistemi.Application.ViewModels.Documents;

namespace OzelYetenekSinavSistemi.Application.Services;

public sealed class DocumentVerificationService : IDocumentVerificationService
{
    private static readonly Regex VerificationCodeFormat =
        new(@"^[0-9A-Fa-f]{32}$", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private readonly IDocumentVerificationRepository _verificationRepository;
    private readonly ICandidateApplicationRepository _applicationRepository;
    private readonly TimeProvider _timeProvider;

    public DocumentVerificationService(
        IDocumentVerificationRepository verificationRepository,
        ICandidateApplicationRepository applicationRepository,
        TimeProvider timeProvider)
    {
        _verificationRepository = verificationRepository;
        _applicationRepository = applicationRepository;
        _timeProvider = timeProvider;
    }

    public async Task<DocumentVerificationResultViewModel> VerifyAsync(string? verificationCode, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(verificationCode))
            return InvalidResult(string.Empty);

        verificationCode = verificationCode.Trim();
        if (!VerificationCodeFormat.IsMatch(verificationCode))
            return InvalidResult(string.Empty);

        verificationCode = verificationCode.ToUpperInvariant();

        var verification = await _verificationRepository.GetByCodeAsync(verificationCode, cancellationToken);
        if (verification is null || !verification.IsValid)
            return InvalidResult(verificationCode);

        var detail = await _applicationRepository.GetDetailByIdAsync(verification.ApplicationId, cancellationToken);
        if (detail is null)
            return InvalidResult(verificationCode);

        // Public doğrulama, sınava giriş belgesi ile aynı zaman kapısına bağlıdır (admin bypass yok).
        if (!ExamEntranceDocumentAccess.IsAvailable(detail.ExamPeriodEndDate, _timeProvider))
            return NotYetAvailableResult();

        return new DocumentVerificationResultViewModel
        {
            IsValid = true,
            ExamTitle = detail.ExamTitle,
            CandidateNo = detail.CandidateNo,
            MaskedCandidateName = MaskName(detail.FirstName, detail.LastName),
            VerificationCode = verificationCode
        };
    }

    private static DocumentVerificationResultViewModel InvalidResult(string code) =>
        new() { IsValid = false, VerificationCode = code };

    private static DocumentVerificationResultViewModel NotYetAvailableResult() =>
        new()
        {
            IsValid = false,
            IsNotYetAvailable = true,
            // Kod ve belge alanlarını döndürmeyerek erken erişimde sızıntıyı önler.
            VerificationCode = string.Empty
        };

    private static string MaskName(string firstName, string lastName)
    {
        static string Mask(string value)
        {
            value = value.Trim();
            if (value.Length <= 1)
                return value + "***";
            return value[..1] + new string('*', Math.Min(4, Math.Max(2, value.Length - 1)));
        }

        return $"{Mask(firstName)} {Mask(lastName)}".Trim();
    }
}
