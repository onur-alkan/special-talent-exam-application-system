using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OzelYetenekSinavSistemi.Application.Interfaces.Repositories;
using OzelYetenekSinavSistemi.Infrastructure.Persistence.Repositories;

namespace OzelYetenekSinavSistemi.Web.Controllers;

[Authorize(Policy = "SuperAdminOnly")]
public sealed class AuditLogController : Controller
{
    private readonly IAuditLogRepository _auditLogRepository;

    public AuditLogController(IAuditLogRepository auditLogRepository)
    {
        _auditLogRepository = auditLogRepository;
    }

    [HttpGet]
    public async Task<IActionResult> Index(int page = 1, int pageSize = 50, CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, AuditLogRepository.MinTake, AuditLogRepository.MaxTake);
        var skip = (page - 1) * pageSize;

        var logs = await _auditLogRepository.GetRecentAsync(pageSize, skip, cancellationToken);
        ViewData["Title"] = "Sistem Logları";
        ViewBag.Page = page;
        ViewBag.PageSize = pageSize;
        return View(logs);
    }
}
