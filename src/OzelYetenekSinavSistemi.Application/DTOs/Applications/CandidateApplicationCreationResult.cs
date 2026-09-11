namespace OzelYetenekSinavSistemi.Application.DTOs.Applications;

public sealed class CandidateApplicationCreationResult
{
    public required Guid ApplicationId { get; init; }
    public required int CandidateNo { get; init; }
    public required string VerificationCode { get; init; }
}
