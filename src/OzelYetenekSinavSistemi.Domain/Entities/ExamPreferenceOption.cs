namespace OzelYetenekSinavSistemi.Domain.Entities;

public class ExamPreferenceOption
{
    public Guid Id { get; set; }
    public Guid ExamPeriodId { get; set; }
    public string PreferenceName { get; set; } = string.Empty;
    public int DisplayOrder { get; set; } = 1;
    public bool IsActive { get; set; } = true;
}
