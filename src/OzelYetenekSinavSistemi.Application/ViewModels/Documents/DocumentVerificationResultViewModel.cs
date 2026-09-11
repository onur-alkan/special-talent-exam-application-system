namespace OzelYetenekSinavSistemi.Application.ViewModels.Documents;

public sealed class DocumentVerificationResultViewModel
{
    public bool IsValid { get; init; }

    /// <summary>
    /// Kod geçerli olsa bile başvuru bitişinden önce doğrulama henüz açılmamış.
    /// Belge alanları doldurulmaz; IsValid false kalır.
    /// </summary>
    public bool IsNotYetAvailable { get; init; }

    public string? ExamTitle { get; init; }
    public int? CandidateNo { get; init; }
    public string? MaskedCandidateName { get; init; }
    public string VerificationCode { get; init; } = string.Empty;
}
