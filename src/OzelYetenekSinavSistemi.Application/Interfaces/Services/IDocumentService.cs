using OzelYetenekSinavSistemi.Application.Common;
using OzelYetenekSinavSistemi.Application.ViewModels.Documents;

namespace OzelYetenekSinavSistemi.Application.Interfaces.Services;

public interface IDocumentService
{
    /// <summary>
    /// Sınava giriş belgesini oluşturur. Sahiplik kontrolü uygulanır:
    /// Candidate yalnızca kendi belgesine, SuperAdmin/atanmış yönetici erişebilir.
    /// </summary>
    Task<OperationResult<ExamEntranceDocumentViewModel>> GetEntranceDocumentAsync(
        Guid applicationId,
        Guid currentUserId,
        string role,
        string verificationBaseUrl,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sınav sonuç belgesini oluşturur. Yetkilendirme giriş belgesi ile aynıdır.
    /// Yalnızca değerlendirme sonucu mevcutsa belge üretilir.
    /// </summary>
    Task<OperationResult<CandidateResultDocumentViewModel>> GetResultDocumentAsync(
        Guid applicationId,
        Guid currentUserId,
        string role,
        string verificationBaseUrl,
        CancellationToken cancellationToken = default);
}
