namespace OzelYetenekSinavSistemi.Domain.Entities;

public class DocumentVerification
{
    public Guid Id { get; set; }
    public Guid ApplicationId { get; set; }
    public string VerificationCode { get; set; } = string.Empty;
    public bool IsValid { get; set; } = true;
    public DateTime CreatedDate { get; set; }
}
