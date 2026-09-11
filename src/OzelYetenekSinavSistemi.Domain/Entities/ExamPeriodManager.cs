namespace OzelYetenekSinavSistemi.Domain.Entities;

public class ExamPeriodManager
{
    public Guid Id { get; set; }
    public Guid ExamPeriodId { get; set; }
    public Guid ManagerUserId { get; set; }
    public Guid AssignedByUserId { get; set; }
    public DateTime AssignedDate { get; set; }
}
