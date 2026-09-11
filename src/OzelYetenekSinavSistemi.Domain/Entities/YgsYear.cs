namespace OzelYetenekSinavSistemi.Domain.Entities;

public class YgsYear
{
    public Guid Id { get; set; }
    public int Year { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedDate { get; set; }
}
