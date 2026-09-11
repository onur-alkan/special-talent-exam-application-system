using OzelYetenekSinavSistemi.Domain.Entities;

namespace OzelYetenekSinavSistemi.Application.DTOs.Applications;

/// <summary>
/// Aday başvuru formunu doldurmak için gereken veriler.
/// </summary>
public sealed class ApplicationFormData
{
    public required ExamPeriod ExamPeriod { get; init; }
    public required IReadOnlyList<ExamPreferenceOption> Options { get; init; }

    /// <summary>Mevcut başvuru varsa seçili tercih Id'leri (sıralı); güncelleme senaryosu.</summary>
    public IReadOnlyList<Guid> ExistingSelectedOptionIds { get; init; } = Array.Empty<Guid>();

    public Guid? ExistingApplicationId { get; init; }
    public bool AlreadyApplied => ExistingApplicationId.HasValue;
}
