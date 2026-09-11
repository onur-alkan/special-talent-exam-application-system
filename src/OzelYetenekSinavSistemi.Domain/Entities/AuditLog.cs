namespace OzelYetenekSinavSistemi.Domain.Entities;

public class AuditLog
{
    public Guid Id { get; set; }
    public Guid? UserId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? IpAddress { get; set; }
    public string? RequestPath { get; set; }
    public string? CorrelationId { get; set; }
    public DateTime CreatedDate { get; set; }
}
