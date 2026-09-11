namespace OzelYetenekSinavSistemi.Application.DTOs.Applications;

/// <summary>
/// Aday başvurusunun tek transaction içinde oluşturulması için repository'e gönderilen istek.
/// </summary>
public sealed class CandidateApplicationCreationRequest
{
    public required Guid UserId { get; init; }
    public required Guid ExamPeriodId { get; init; }

    /// <summary>Tercih sırasına göre (1. tercih ilk) seçenek Id listesi.</summary>
    public required IReadOnlyList<Guid> OrderedPreferenceOptionIds { get; init; }

    public string? IpAddress { get; init; }
    public string? CorrelationId { get; init; }
}
