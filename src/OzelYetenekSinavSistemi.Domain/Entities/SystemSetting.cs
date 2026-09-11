namespace OzelYetenekSinavSistemi.Domain.Entities;

public class SystemSetting
{
    public Guid Id { get; set; }
    public string SettingKey { get; set; } = string.Empty;
    public string? SettingValue { get; set; }
    public string? Description { get; set; }
    public DateTime? UpdatedDate { get; set; }
    public Guid? UpdatedByUserId { get; set; }
}
