using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using OzelYetenekSinavSistemi.Application.Common;
using OzelYetenekSinavSistemi.Application.Interfaces.Services;
using OzelYetenekSinavSistemi.Application.ViewModels.UserManagement;
using OzelYetenekSinavSistemi.Domain.Common;
using OzelYetenekSinavSistemi.Web.Infrastructure;

namespace OzelYetenekSinavSistemi.Web.Controllers;

[Authorize(Policy = "SuperAdminOnly")]
public sealed class UserManagementController : Controller
{
    private readonly IUserManagementService _userManagementService;

    public UserManagementController(IUserManagementService userManagementService)
    {
        _userManagementService = userManagementService;
    }

    private string? Ip => HttpContext.GetClientIpString();
    private string CorrelationId => HttpContext.TraceIdentifier;

    [HttpGet]
    public async Task<IActionResult> Index(int page = 1, CancellationToken cancellationToken = default)
    {
        const int pageSize = 50;
        var users = await _userManagementService.GetStaffUsersAsync(User.GetUserId(), page, pageSize, cancellationToken);
        ViewBag.Page = page;
        ViewBag.PageSize = pageSize;
        ViewData["Title"] = "Personel Yönetimi";
        return View(users);
    }

    [HttpGet]
    public IActionResult Create()
    {
        PrepareCreateViewData();
        return View(new CreateStaffUserViewModel());
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateStaffUserViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            PrepareCreateViewData();
            return View(model);
        }

        var result = await _userManagementService.CreateStaffUserAsync(
            model, User.GetUserId(), Ip, CorrelationId, cancellationToken);

        if (!result.Success)
        {
            if (string.Equals(result.ErrorMessage, TurkishIdentityNumber.InvalidMessage, StringComparison.Ordinal))
                ModelState.AddModelError(nameof(CreateStaffUserViewModel.TcNo), result.ErrorMessage!);
            else
                ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "Kayıt başarısız.");
            PrepareCreateViewData();
            return View(model);
        }

        TempData["ToastSuccess"] = "Personel hesabı oluşturuldu.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(Guid id, CancellationToken cancellationToken)
    {
        var result = await _userManagementService.GetStaffUserForEditAsync(id, User.GetUserId(), cancellationToken);
        if (!result.Success || result.Data is null)
        {
            TempData["ToastError"] = result.ErrorMessage ?? "Kullanıcı bulunamadı.";
            return RedirectToAction(nameof(Index));
        }

        PrepareEditViewData();
        return View(result.Data);
    }

    [HttpPost]
    public async Task<IActionResult> Edit(EditStaffUserViewModel model, CancellationToken cancellationToken)
    {
        model.IsSelf = model.Id == User.GetUserId();

        if (!ModelState.IsValid)
        {
            PrepareEditViewData();
            return View(model);
        }

        var result = await _userManagementService.UpdateStaffUserAsync(
            model, User.GetUserId(), Ip, CorrelationId, cancellationToken);

        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "Güncelleme başarısız.");
            PrepareEditViewData();
            return View(model);
        }

        TempData["ToastSuccess"] = "Personel hesabı güncellendi.";
        return RedirectToAction(nameof(Index));
    }

    private void PrepareCreateViewData()
    {
        PopulateRoles();
        ViewData["Title"] = "Personel Oluştur";
    }

    private void PrepareEditViewData()
    {
        PopulateRoles();
        ViewData["Title"] = "Personel Düzenle";
    }

    private void PopulateRoles()
    {
        ViewBag.Roles = new SelectList(new[]
        {
            new { Id = DomainConstants.RoleIds.SuperAdmin, Name = DomainConstants.RoleNames.SuperAdmin },
            new { Id = DomainConstants.RoleIds.ApplicationManager, Name = DomainConstants.RoleNames.ApplicationManager }
        }, "Id", "Name");
    }
}
