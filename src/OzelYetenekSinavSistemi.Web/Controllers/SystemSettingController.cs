using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OzelYetenekSinavSistemi.Application.Interfaces.Repositories;
using OzelYetenekSinavSistemi.Application.Validation;
using OzelYetenekSinavSistemi.Application.ViewModels.SystemSettings;
using OzelYetenekSinavSistemi.Web.Infrastructure;

namespace OzelYetenekSinavSistemi.Web.Controllers;

[Authorize(Policy = "SuperAdminOnly")]
public sealed class SystemSettingController : Controller
{
    private readonly ISystemSettingRepository _systemSettingRepository;

    public SystemSettingController(ISystemSettingRepository systemSettingRepository)
    {
        _systemSettingRepository = systemSettingRepository;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var settings = await _systemSettingRepository.GetAllAsync(cancellationToken);
        ViewData["Title"] = "Sistem Ayarları";
        return View(new SystemSettingIndexViewModel
        {
            Settings = settings.Select(s => new SystemSettingUpdateViewModel
            {
                SettingKey = s.SettingKey,
                SettingValue = s.SettingValue ?? string.Empty,
                Description = s.Description,
                UpdatedDate = s.UpdatedDate
            }).ToList()
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Update(SystemSettingUpdateViewModel model, CancellationToken cancellationToken)
    {
        if (!SystemSettingValidator.TryValidateKey(model.SettingKey, out var normalizedKey, out var keyError))
        {
            TempData["ToastError"] = keyError;
            return RedirectToAction(nameof(Index));
        }

        if (!SystemSettingValidator.TryValidateValue(normalizedKey!, model.SettingValue, out var normalizedValue, out var valueError))
        {
            TempData["ToastError"] = valueError;
            return RedirectToAction(nameof(Index));
        }

        var success = await _systemSettingRepository.UpdateValueAsync(
            normalizedKey!,
            normalizedValue!,
            User.GetUserId(),
            cancellationToken);

        TempData[success ? "ToastSuccess" : "ToastError"] =
            success ? "Ayar güncellendi." : "Ayar güncellenemedi.";
        return RedirectToAction(nameof(Index));
    }
}
