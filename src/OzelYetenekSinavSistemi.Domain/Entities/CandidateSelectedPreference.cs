namespace OzelYetenekSinavSistemi.Domain.Entities;

public class CandidateSelectedPreference
{
    public Guid Id { get; set; }
    public Guid ApplicationId { get; set; }
    public Guid PreferenceOptionId { get; set; }

    /// <summary>
    /// 1. Tercih, 2. Tercih vb. sıra alanı; sayısal sıra olduğundan int kalır.
    /// </summary>
    public int PreferenceOrder { get; set; }
}
