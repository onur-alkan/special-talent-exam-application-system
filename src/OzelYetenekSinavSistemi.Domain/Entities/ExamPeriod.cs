namespace OzelYetenekSinavSistemi.Domain.Entities;

public class ExamPeriod
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public int MaxPreferences { get; set; } = 1;
    public bool IsActive { get; set; } = true;
    public bool IsClosed { get; set; }
    public bool IsDeleted { get; set; }
    public Guid CreatedByUserId { get; set; }
    public DateTime CreatedDate { get; set; }
    public DateTime? UpdatedDate { get; set; }
}
