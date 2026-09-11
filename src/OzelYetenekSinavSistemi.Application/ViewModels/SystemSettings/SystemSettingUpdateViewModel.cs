using System.ComponentModel.DataAnnotations;

namespace OzelYetenekSinavSistemi.Application.ViewModels.SystemSettings;

public sealed class SystemSettingUpdateViewModel
{
    [Required(ErrorMessage = "Ayar anahtarını giriniz.")]
    [StringLength(100, ErrorMessage = "Ayar anahtarı en fazla 100 karakter olabilir.")]
    public string SettingKey { get; set; } = string.Empty;

    [Required(ErrorMessage = "Fotoğraf boyutu değerini giriniz.")]
    [Range(100, 10240, ErrorMessage = "Fotoğraf boyutunu 100-10240 KB aralığında giriniz.")]
    [Display(Name = "Değer")]
    public string SettingValue { get; set; } = string.Empty;

    public string? Description { get; set; }

    public DateTime? UpdatedDate { get; set; }
}

public sealed class SystemSettingIndexViewModel
{
    public IReadOnlyList<SystemSettingUpdateViewModel> Settings { get; init; } = [];
}
