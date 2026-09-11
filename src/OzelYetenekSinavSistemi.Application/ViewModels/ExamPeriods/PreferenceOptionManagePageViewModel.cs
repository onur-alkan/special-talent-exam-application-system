using OzelYetenekSinavSistemi.Domain.Entities;

namespace OzelYetenekSinavSistemi.Application.ViewModels.ExamPeriods;

public sealed class PreferenceOptionManagePageViewModel
{
    public Guid ExamPeriodId { get; init; }

    public string ExamPeriodTitle { get; init; } = string.Empty;

    public ExamPeriod? ExamPeriod { get; init; }

    /// <summary>Salt okunur liste satırları (DataTables).</summary>
    public IReadOnlyList<PreferenceOptionListItemViewModel> ExistingOptions { get; init; } = [];

    /// <summary>Geriye uyumluluk: Options alias.</summary>
    public IReadOnlyList<PreferenceOptionListItemViewModel> Options => ExistingOptions;

    public PreferenceOptionFormViewModel AddForm { get; set; } = new();
}

public sealed class PreferenceOptionListItemViewModel
{
    public Guid Id { get; init; }
    public Guid ExamPeriodId { get; init; }
    public string PreferenceName { get; init; } = string.Empty;
    public int DisplayOrder { get; init; }
    public bool IsActive { get; init; }
}
