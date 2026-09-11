using OzelYetenekSinavSistemi.Application.Common;
using OzelYetenekSinavSistemi.Application.DTOs.Applications;
using OzelYetenekSinavSistemi.Application.Interfaces.Repositories;
using OzelYetenekSinavSistemi.Application.Interfaces.Services;
using OzelYetenekSinavSistemi.Application.ViewModels.Documents;
using OzelYetenekSinavSistemi.Domain.Common;

namespace OzelYetenekSinavSistemi.Application.Services;

public sealed class DocumentService : IDocumentService
{
    private readonly ICandidateApplicationRepository _applicationRepository;
    private readonly IExamPeriodManagerRepository _managerRepository;
    private readonly IQrCodeService _qrCodeService;
    private readonly ISensitiveDataMaskingService _masking;
    private readonly ICountryCatalog _countryCatalog;
    private readonly TimeProvider _timeProvider;

    public DocumentService(
        ICandidateApplicationRepository applicationRepository,
        IExamPeriodManagerRepository managerRepository,
        IQrCodeService qrCodeService,
        ISensitiveDataMaskingService masking,
        ICountryCatalog countryCatalog,
        TimeProvider timeProvider)
    {
        _applicationRepository = applicationRepository;
        _managerRepository = managerRepository;
        _qrCodeService = qrCodeService;
        _masking = masking;
        _countryCatalog = countryCatalog;
        _timeProvider = timeProvider;
    }

    public async Task<OperationResult<ExamEntranceDocumentViewModel>> GetEntranceDocumentAsync(
        Guid applicationId,
        Guid currentUserId,
        string role,
        string verificationBaseUrl,
        CancellationToken cancellationToken = default)
    {
        var detail = await _applicationRepository.GetDetailByIdAsync(applicationId, cancellationToken);
        if (detail is null)
            return OperationResult<ExamEntranceDocumentViewModel>.Fail("Başvuru bulunamadı.");

        if (!await IsAuthorizedAsync(detail, currentUserId, role, cancellationToken).ConfigureAwait(false))
            return OperationResult<ExamEntranceDocumentViewModel>.Fail("Bu belgeye erişim yetkiniz yok.");

        // Aday için başvuru bitiş kapısı; SuperAdmin / atanmış yönetici önizlemesi etkilenmez.
        if (string.Equals(role, DomainConstants.RoleNames.Candidate, StringComparison.Ordinal)
            && !ExamEntranceDocumentAccess.IsAvailable(detail.ExamPeriodEndDate, _timeProvider))
        {
            return OperationResult<ExamEntranceDocumentViewModel>.Fail(
                ExamEntranceDocumentAccess.NotYetAvailableMessage);
        }

        var (verificationUrl, qrDataUri) = BuildVerificationArtifacts(detail.VerificationCode, verificationBaseUrl);
        var identity = BuildMaskedIdentity(detail);

        var vm = new ExamEntranceDocumentViewModel
        {
            ApplicationId = detail.ApplicationId,
            ExamTitle = detail.ExamTitle,
            PhotoPath = detail.PhotoPath,
            Identity = identity,
            FullName = detail.FullName,
            CandidateNo = detail.CandidateNo,
            Preferences = detail.Preferences,
            RegistrationDate = detail.RegistrationDate,
            VerificationCode = detail.VerificationCode,
            QrCodeDataUri = qrDataUri,
            VerificationUrl = verificationUrl
        };

        return OperationResult<ExamEntranceDocumentViewModel>.Ok(vm);
    }

    public async Task<OperationResult<CandidateResultDocumentViewModel>> GetResultDocumentAsync(
        Guid applicationId,
        Guid currentUserId,
        string role,
        string verificationBaseUrl,
        CancellationToken cancellationToken = default)
    {
        var detail = await _applicationRepository.GetDetailByIdAsync(applicationId, cancellationToken);
        if (detail is null)
            return OperationResult<CandidateResultDocumentViewModel>.Fail("Başvuru bulunamadı.");

        if (!await IsAuthorizedAsync(detail, currentUserId, role, cancellationToken).ConfigureAwait(false))
            return OperationResult<CandidateResultDocumentViewModel>.Fail("Bu belgeye erişim yetkiniz yok.");

        if (!detail.AttendanceStatus.HasValue)
            return OperationResult<CandidateResultDocumentViewModel>.Fail("Sınav sonucu henüz açıklanmamış veya değerlendirilmemiş.");

        var (verificationUrl, qrDataUri) = BuildVerificationArtifacts(detail.VerificationCode, verificationBaseUrl);

        string? visibleDescription = null;
        if (detail.IsDescriptionVisibleToCandidate && !string.IsNullOrWhiteSpace(detail.AdminDescription))
            visibleDescription = detail.AdminDescription;

        var identity = BuildMaskedIdentity(detail);

        var vm = new CandidateResultDocumentViewModel
        {
            ApplicationId = detail.ApplicationId,
            ExamTitle = detail.ExamTitle,
            PhotoPath = detail.PhotoPath,
            Identity = identity,
            FullName = detail.FullName,
            CandidateNo = detail.CandidateNo,
            Preferences = detail.Preferences,
            AttendanceStatus = detail.AttendanceStatus.Value,
            ExamScore = detail.ExamScore,
            CandidateVisibleDescription = visibleDescription,
            EvaluatedDate = detail.EvaluatedDate,
            VerificationCode = detail.VerificationCode,
            VerificationUrl = verificationUrl,
            QrCodeDataUri = qrDataUri
        };

        return OperationResult<CandidateResultDocumentViewModel>.Ok(vm);
    }

    private DocumentIdentityViewModel BuildMaskedIdentity(CandidateApplicationDetail detail)
    {
        var info = IdentityDisplayBuilder.BuildFromApplicationDetail(
            detail, _countryCatalog, maskIdentity: true, _masking);
        return DocumentIdentityMapper.Map(info);
    }

    private async Task<bool> IsAuthorizedAsync(
        CandidateApplicationDetail detail,
        Guid currentUserId,
        string role,
        CancellationToken cancellationToken)
    {
        return role switch
        {
            DomainConstants.RoleNames.SuperAdmin => true,
            DomainConstants.RoleNames.Candidate => detail.UserId == currentUserId,
            DomainConstants.RoleNames.ApplicationManager =>
                await _managerRepository.IsManagerOfExamPeriodAsync(currentUserId, detail.ExamPeriodId, cancellationToken)
                    .ConfigureAwait(false),
            _ => false
        };
    }

    private (string VerificationUrl, string QrCodeDataUri) BuildVerificationArtifacts(
        string verificationCode,
        string verificationBaseUrl)
    {
        var separator = verificationBaseUrl.Contains('?') ? "&" : "?";
        var verificationUrl = $"{verificationBaseUrl}{separator}code={Uri.EscapeDataString(verificationCode)}";
        var qrDataUri = _qrCodeService.GenerateBase64PngDataUri(verificationUrl);
        return (verificationUrl, qrDataUri);
    }
}
