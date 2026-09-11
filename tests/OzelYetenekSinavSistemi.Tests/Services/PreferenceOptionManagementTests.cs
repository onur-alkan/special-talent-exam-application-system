using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using OzelYetenekSinavSistemi.Application.Common;
using OzelYetenekSinavSistemi.Application.DTOs.ExamPeriods;
using OzelYetenekSinavSistemi.Application.Interfaces.Repositories;
using OzelYetenekSinavSistemi.Application.Interfaces.Services;
using OzelYetenekSinavSistemi.Application.Services;
using OzelYetenekSinavSistemi.Application.ViewModels.ExamPeriods;
using OzelYetenekSinavSistemi.Domain.Common;
using OzelYetenekSinavSistemi.Domain.Entities;
using OzelYetenekSinavSistemi.Web.Controllers;

namespace OzelYetenekSinavSistemi.Tests.Services;

public sealed class PreferenceOptionManagementTests
{
    private static readonly Guid ActorId = Guid.NewGuid();
    private static readonly Guid OptionId = Guid.NewGuid();
    private static readonly Guid ExamId = Guid.NewGuid();
    private static readonly Guid OtherExamId = Guid.NewGuid();

    [Fact]
    public async Task Update_TrimsName_AndUsesOptionIdWithoutPostedPeriodForMutation()
    {
        var fixture = CreateService();
        PreferenceOptionWriteRequest? captured = null;
        fixture.Options
            .Setup(r => r.UpdateAtomicAsync(It.IsAny<PreferenceOptionWriteRequest>(), It.IsAny<CancellationToken>()))
            .Callback<PreferenceOptionWriteRequest, CancellationToken>((request, _) => captured = request)
            .ReturnsAsync(PreferenceOptionWriteResult.Ok(OptionId, ExamId));

        var result = await fixture.Service.UpdatePreferenceOptionAsync(new PreferenceOptionFormViewModel
        {
            Id = OptionId,
            ExamPeriodId = OtherExamId,
            PreferenceName = "  Resim Bölümü  ",
            DisplayOrder = 2,
            IsActive = false
        }, ActorId, "127.0.0.1", "corr", CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(ExamId, result.Data);
        Assert.NotNull(captured);
        Assert.Equal("Resim Bölümü", captured!.PreferenceName);
        Assert.Equal(OptionId, captured.OptionId);
        Assert.Null(captured.ExamPeriodId);
        Assert.Equal(2, captured.DisplayOrder);
        Assert.False(captured.IsActive);
        Assert.Equal(ActorId, captured.ActorUserId);
    }

    [Theory]
    [InlineData("", 1, "Tercih adını giriniz.")]
    [InlineData("   ", 1, "Tercih adını giriniz.")]
    [InlineData("Geçerli", 0, "Görünüm sırasını 1 veya daha büyük giriniz.")]
    [InlineData("Geçerli", -1, "Görünüm sırasını 1 veya daha büyük giriniz.")]
    public async Task Update_InvalidInput_IsRejectedWithoutRepositoryWrite(
        string name,
        int order,
        string expectedMessage)
    {
        var fixture = CreateService();

        var result = await fixture.Service.UpdatePreferenceOptionAsync(new PreferenceOptionFormViewModel
        {
            Id = OptionId,
            ExamPeriodId = ExamId,
            PreferenceName = name,
            DisplayOrder = order
        }, ActorId, cancellationToken: CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(expectedMessage, result.ErrorMessage);
        fixture.Options.Verify(
            r => r.UpdateAtomicAsync(It.IsAny<PreferenceOptionWriteRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Update_NameOver100Characters_IsRejected()
    {
        var fixture = CreateService();

        var result = await fixture.Service.UpdatePreferenceOptionAsync(new PreferenceOptionFormViewModel
        {
            Id = OptionId,
            ExamPeriodId = ExamId,
            PreferenceName = new string('A', 101),
            DisplayOrder = 1
        }, ActorId, cancellationToken: CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("Tercih adı en fazla 100 karakter olabilir.", result.ErrorMessage);
        fixture.Options.Verify(
            r => r.UpdateAtomicAsync(It.IsAny<PreferenceOptionWriteRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Theory]
    [InlineData(PreferenceOptionWriteStatus.DuplicateName, "Bu tercih adı bu sınav döneminde zaten kullanılıyor.")]
    [InlineData(PreferenceOptionWriteStatus.DuplicateDisplayOrder, "Bu görünüm sırası bu sınav döneminde zaten kullanılıyor.")]
    public async Task Update_DuplicateWithinPeriod_ReturnsFriendlyMessage(
        PreferenceOptionWriteStatus status,
        string expectedMessage)
    {
        var fixture = CreateService();
        fixture.Options
            .Setup(r => r.UpdateAtomicAsync(It.IsAny<PreferenceOptionWriteRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(PreferenceOptionWriteResult.Fail(status, ExamId));

        var result = await fixture.Service.UpdatePreferenceOptionAsync(ValidModel(), ActorId);

        Assert.False(result.Success);
        Assert.Equal(expectedMessage, result.ErrorMessage);
        Assert.DoesNotContain("SQL", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Add_SameNameAndOrderInDifferentPeriods_CanSucceed()
    {
        var fixture = CreateService();
        fixture.Options
            .Setup(r => r.AddAtomicAsync(It.IsAny<PreferenceOptionWriteRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PreferenceOptionWriteRequest request, CancellationToken _) =>
                PreferenceOptionWriteResult.Ok(Guid.NewGuid(), request.ExamPeriodId!.Value));

        var first = await fixture.Service.AddPreferenceOptionAsync(new PreferenceOptionFormViewModel
        {
            ExamPeriodId = ExamId,
            PreferenceName = "Müzik",
            DisplayOrder = 1
        }, ActorId);
        var second = await fixture.Service.AddPreferenceOptionAsync(new PreferenceOptionFormViewModel
        {
            ExamPeriodId = OtherExamId,
            PreferenceName = "Müzik",
            DisplayOrder = 1
        }, ActorId);

        Assert.True(first.Success);
        Assert.True(second.Success);
        Assert.Equal(ExamId, first.Data);
        Assert.Equal(OtherExamId, second.Data);
    }

    [Fact]
    public async Task ApplicationManager_UpdateIsRejectedByAtomicAuthorization()
    {
        var fixture = CreateService();
        fixture.Options
            .Setup(r => r.UpdateAtomicAsync(It.IsAny<PreferenceOptionWriteRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(PreferenceOptionWriteResult.Fail(PreferenceOptionWriteStatus.ActorNotAuthorized));

        var result = await fixture.Service.UpdatePreferenceOptionAsync(ValidModel(), ActorId);

        Assert.False(result.Success);
        Assert.Equal("Bu işlem için yetkiniz yok.", result.ErrorMessage);
    }

    [Fact]
    public async Task UsedPreference_CanBeDeactivated()
    {
        var fixture = CreateService();
        fixture.Options
            .Setup(r => r.SetActiveAtomicAsync(
                It.Is<PreferenceOptionWriteRequest>(request =>
                    request.OptionId == OptionId && !request.IsActive),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(PreferenceOptionWriteResult.Ok(OptionId, ExamId));

        var result = await fixture.Service.SetPreferenceOptionActiveAsync(
            OptionId, false, ActorId, cancellationToken: CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(ExamId, result.Data);
    }

    [Fact]
    public async Task UsedPreference_CannotBeDeleted_ReturnsRequiredMessage()
    {
        var fixture = CreateService();
        fixture.Options
            .Setup(r => r.DeleteAtomicAsync(It.IsAny<PreferenceOptionWriteRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(PreferenceOptionWriteResult.Fail(PreferenceOptionWriteStatus.InUse, ExamId));

        var result = await fixture.Service.DeletePreferenceOptionAsync(
            OptionId, ActorId, cancellationToken: CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(
            "Bu tercih daha önce başvurularda kullanıldığı için silinemez. Pasif hâle getirebilirsiniz.",
            result.ErrorMessage);
    }

    [Fact]
    public async Task UnusedPreference_CanBeDeleted()
    {
        var fixture = CreateService();
        fixture.Options
            .Setup(r => r.DeleteAtomicAsync(It.IsAny<PreferenceOptionWriteRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(PreferenceOptionWriteResult.Ok(OptionId, ExamId));

        var result = await fixture.Service.DeletePreferenceOptionAsync(
            OptionId, ActorId, cancellationToken: CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(ExamId, result.Data);
    }

    [Fact]
    public async Task SuperAdmin_ControllerCanUpdate_AndRedirectUsesResolvedPeriod()
    {
        var service = new Mock<IExamPeriodService>();
        service.Setup(s => s.GetPreferenceOptionAsync(OptionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ExamPreferenceOption
            {
                Id = OptionId,
                ExamPeriodId = ExamId,
                PreferenceName = "Eski",
                DisplayOrder = 1
            });
        service.Setup(s => s.UpdatePreferenceOptionAsync(
                It.Is<PreferenceOptionFormViewModel>(m => m.ExamPeriodId == ExamId),
                ActorId,
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(OperationResult<Guid>.Ok(ExamId));
        var controller = CreateController(service.Object, DomainConstants.RoleNames.SuperAdmin);

        var result = Assert.IsType<RedirectToActionResult>(await controller.Edit(new PreferenceOptionFormViewModel
        {
            Id = OptionId,
            ExamPeriodId = OtherExamId,
            PreferenceName = "Yeni",
            DisplayOrder = 2,
            IsActive = true
        }, CancellationToken.None));

        Assert.Equal(nameof(ExamPreferenceOptionController.Manage), result.ActionName);
        Assert.Equal(ExamId, result.RouteValues!["examPeriodId"]);
        Assert.Equal("Tercih seçeneği güncellendi.", controller.TempData["ToastSuccess"]);
    }

    [Fact]
    public void AddForm_PostsFlatPreferenceOptionFields_MatchingAddActionBinding()
    {
        var manage = File.ReadAllText(FindUnder(
            "src", "OzelYetenekSinavSistemi.Web", "Views",
            "ExamPreferenceOption", "Manage.cshtml"));
        Assert.Contains("_PreferenceOptionAddForm", manage, StringComparison.Ordinal);
        Assert.DoesNotContain("asp-for=\"AddForm.ExamPeriodId\"", manage, StringComparison.Ordinal);

        var addForm = File.ReadAllText(FindUnder(
            "src", "OzelYetenekSinavSistemi.Web", "Views",
            "ExamPreferenceOption", "_PreferenceOptionAddForm.cshtml"));
        Assert.Contains("asp-action=\"Add\"", addForm, StringComparison.Ordinal);
        Assert.Contains("asp-for=\"ExamPeriodId\"", addForm, StringComparison.Ordinal);
        Assert.Contains("asp-for=\"PreferenceName\"", addForm, StringComparison.Ordinal);
        Assert.DoesNotContain("asp-for=\"AddForm.", addForm, StringComparison.Ordinal);
    }

    [Fact]
    public void MutationActions_ArePostAndRequireAntiforgery_AndControllerIsSuperAdminOnly()
    {
        var authorize = typeof(ExamPreferenceOptionController)
            .GetCustomAttribute<AuthorizeAttribute>();
        Assert.NotNull(authorize);
        Assert.Equal("SuperAdminOnly", authorize!.Policy);

        foreach (var action in new[] { "Add", "SetActive", "Delete" })
        {
            var method = typeof(ExamPreferenceOptionController).GetMethod(action);
            Assert.NotNull(method);
            Assert.NotNull(method!.GetCustomAttribute<HttpPostAttribute>());
            Assert.NotNull(method.GetCustomAttribute<ValidateAntiForgeryTokenAttribute>());
            Assert.Null(method.GetCustomAttribute<HttpGetAttribute>());
        }

        var editPost = typeof(ExamPreferenceOptionController)
            .GetMethods()
            .Single(m => m.Name == "Edit"
                         && m.GetCustomAttribute<HttpPostAttribute>() is not null);
        Assert.NotNull(editPost.GetCustomAttribute<ValidateAntiForgeryTokenAttribute>());

        var editGet = typeof(ExamPreferenceOptionController)
            .GetMethods()
            .Single(m => m.Name == "Edit"
                         && m.GetCustomAttribute<HttpGetAttribute>() is not null);
        Assert.Null(editGet.GetCustomAttribute<HttpPostAttribute>());

        foreach (var action in new[] { "Index", "Manage", "Data" })
        {
            var method = typeof(ExamPreferenceOptionController).GetMethod(action);
            Assert.NotNull(method);
            Assert.NotNull(method!.GetCustomAttribute<HttpGetAttribute>());
            Assert.Null(method.GetCustomAttribute<HttpPostAttribute>());
        }
    }

    [Fact]
    public void CandidateQueries_ExcludeInactiveFromNewChoices_ButKeepHistoricalDetails()
    {
        var optionRepository = File.ReadAllText(FindUnder(
            "src", "OzelYetenekSinavSistemi.Infrastructure", "Persistence",
            "Repositories", "ExamPreferenceOptionRepository.cs"));
        Assert.Contains("AND IsActive = 1", optionRepository, StringComparison.Ordinal);

        var applicationRepository = File.ReadAllText(FindUnder(
            "src", "OzelYetenekSinavSistemi.Infrastructure", "Persistence",
            "Repositories", "CandidateApplicationRepository.cs"));
        Assert.Contains("INNER JOIN dbo.ExamPreferenceOptions", applicationRepository, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "INNER JOIN dbo.ExamPreferenceOptions epo ON epo.Id = csp.PreferenceOptionId AND epo.IsActive = 1",
            applicationRepository,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Delete_IsAtomicUsageCheckAndAuditCommitsOnlyAfterSuccessfulMutation()
    {
        var source = File.ReadAllText(FindUnder(
            "src", "OzelYetenekSinavSistemi.Infrastructure", "Persistence",
            "Repositories", "ExamPreferenceOptionRepository.cs"));

        Assert.Contains("BeginTransactionAsync", source, StringComparison.Ordinal);
        Assert.Contains("CandidateSelectedPreferences WITH (UPDLOCK, HOLDLOCK)", source, StringComparison.Ordinal);
        Assert.Contains("PreferenceOptionWriteStatus.InUse", source, StringComparison.Ordinal);
        Assert.Contains("ex.Number == 547", source, StringComparison.Ordinal);
        Assert.Contains("\"PreferenceOptionDeleted\"", source, StringComparison.Ordinal);

        var deleteIndex = source.IndexOf("DELETE FROM dbo.ExamPreferenceOptions", StringComparison.Ordinal);
        var auditIndex = source.IndexOf("\"PreferenceOptionDeleted\"", deleteIndex, StringComparison.Ordinal);
        var commitIndex = source.IndexOf("CommitAsync", auditIndex, StringComparison.Ordinal);
        Assert.True(deleteIndex >= 0 && auditIndex > deleteIndex && commitIndex > auditIndex);
    }

    [Fact]
    public void Toggle_AuditsOnlyInsideSuccessfulAtomicTransaction()
    {
        var source = File.ReadAllText(FindUnder(
            "src", "OzelYetenekSinavSistemi.Infrastructure", "Persistence",
            "Repositories", "ExamPreferenceOptionRepository.cs"));

        Assert.Contains("\"PreferenceOptionActivated\"", source, StringComparison.Ordinal);
        Assert.Contains("\"PreferenceOptionDeactivated\"", source, StringComparison.Ordinal);
        Assert.Contains("if (!await InsertAuditAsync", source, StringComparison.Ordinal);
        Assert.Contains("RollbackAndFailAsync", source, StringComparison.Ordinal);
    }

    [Fact]
    public void DatabaseConstraints_AreScopedToExamPeriod()
    {
        var schema = File.ReadAllText(FindUnder("database", "OzelYetenekSinavSistemi.sql"));
        Assert.Contains(
            "UQ_ExamPreferenceOptions_Period_Name UNIQUE (ExamPeriodId, PreferenceName)",
            schema,
            StringComparison.Ordinal);
        Assert.Contains(
            "UQ_ExamPreferenceOptions_Period_Order UNIQUE (ExamPeriodId, DisplayOrder)",
            schema,
            StringComparison.Ordinal);
    }

    [Fact]
    public void ManageView_HasTopAddFormAndServerSideDataTableWithoutEmbeddedRows()
    {
        var manage = File.ReadAllText(FindUnder(
            "src", "OzelYetenekSinavSistemi.Web", "Views",
            "ExamPreferenceOption", "Manage.cshtml"));
        var addForm = File.ReadAllText(FindUnder(
            "src", "OzelYetenekSinavSistemi.Web", "Views",
            "ExamPreferenceOption", "_PreferenceOptionAddForm.cshtml"));
        var edit = File.ReadAllText(FindUnder(
            "src", "OzelYetenekSinavSistemi.Web", "Views",
            "ExamPreferenceOption", "Edit.cshtml"));
        var siteJs = File.ReadAllText(FindUnder(
            "src", "OzelYetenekSinavSistemi.Web", "wwwroot", "js", "site.js"));

        Assert.Contains("Yeni Tercih Seçeneği Ekle", manage, StringComparison.Ordinal);
        Assert.Contains("Tüm Tercih Seçenekleri", manage, StringComparison.Ordinal);
        Assert.Contains("js-datatable", manage, StringComparison.Ordinal);
        Assert.Contains("data-oys-server-side=\"true\"", manage, StringComparison.Ordinal);
        Assert.Contains("data-ajax-url=", manage, StringComparison.Ordinal);
        Assert.Contains("data-search-placeholder=\"Tabloda ara\"", manage, StringComparison.Ordinal);
        Assert.Contains("data-oys-length-menu=\"10,25,50,100\"", manage, StringComparison.Ordinal);
        Assert.Contains("data-page-length=\"10\"", manage, StringComparison.Ordinal);
        Assert.Contains("data-search-delay=\"400\"", manage, StringComparison.Ordinal);
        Assert.DoesNotContain("data-length-menu=", manage, StringComparison.Ordinal);
        Assert.DoesNotContain("@foreach (var option in Model.ExistingOptions)", manage, StringComparison.Ordinal);
        Assert.Contains("<tbody></tbody>", manage, StringComparison.Ordinal);
        Assert.Contains("data-edit-url-template=", manage, StringComparison.Ordinal);
        Assert.Contains("data-delete-url=", manage, StringComparison.Ordinal);
        Assert.Contains("Url.Action(\"Delete\"", manage, StringComparison.Ordinal);
        Assert.Contains("options.serverSide = true", siteJs, StringComparison.Ordinal);
        Assert.Contains("options.processing = true", siteJs, StringComparison.Ordinal);
        Assert.Contains("oysEnhancePreferenceOptionsRow", siteJs, StringComparison.Ordinal);
        Assert.Contains("data-confirm", siteJs, StringComparison.Ordinal);
        Assert.Contains("badge badge-primary", siteJs, StringComparison.Ordinal);
        Assert.Contains("badge badge-secondary", siteJs, StringComparison.Ordinal);
        Assert.Contains("table-responsive", manage, StringComparison.Ordinal);
        Assert.DoesNotContain("asp-action=\"SetActive\"", manage, StringComparison.Ordinal);
        Assert.DoesNotContain("<input asp-for=\"PreferenceName\"", manage, StringComparison.Ordinal);
        Assert.Contains("Yeni Kayıt Ekle", addForm, StringComparison.Ordinal);
        Assert.Contains("Vazgeç", addForm, StringComparison.Ordinal);
        Assert.Contains("asp-for=\"IsActive\"", addForm, StringComparison.Ordinal);
        Assert.Contains("asp-action=\"Edit\"", edit, StringComparison.Ordinal);
        Assert.DoesNotContain("onclick=", manage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ExamPeriodDetails_ManageLinkTargetsCurrentExamDirectly()
    {
        var details = File.ReadAllText(FindUnder(
            "src", "OzelYetenekSinavSistemi.Web", "Views",
            "ExamPeriod", "Details.cshtml"));
        Assert.Contains("asp-controller=\"ExamPreferenceOption\"", details, StringComparison.Ordinal);
        Assert.Contains("asp-action=\"Manage\"", details, StringComparison.Ordinal);
        Assert.Contains("asp-route-examPeriodId=\"@Model.Id\"", details, StringComparison.Ordinal);
    }

    private static PreferenceOptionFormViewModel ValidModel() =>
        new()
        {
            Id = OptionId,
            ExamPeriodId = ExamId,
            PreferenceName = "Müzik",
            DisplayOrder = 1,
            IsActive = true
        };

    private static ServiceFixture CreateService()
    {
        var periods = new Mock<IExamPeriodRepository>(MockBehavior.Strict);
        var options = new Mock<IExamPreferenceOptionRepository>(MockBehavior.Strict);
        var managers = new Mock<IExamPeriodManagerRepository>(MockBehavior.Strict);
        var users = new Mock<IUserRepository>(MockBehavior.Strict);
        var audit = new Mock<IAuditService>(MockBehavior.Strict);
        var service = new ExamPeriodService(
            periods.Object, options.Object, managers.Object, users.Object, audit.Object);
        return new ServiceFixture(service, options);
    }

    private static ExamPreferenceOptionController CreateController(
        IExamPeriodService service,
        string role)
    {
        var http = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, ActorId.ToString()),
                new Claim(ClaimTypes.Role, role)
            }, "Test")),
            TraceIdentifier = "test-correlation"
        };
        return new ExamPreferenceOptionController(service)
        {
            ControllerContext = new ControllerContext { HttpContext = http },
            TempData = new Microsoft.AspNetCore.Mvc.ViewFeatures.TempDataDictionary(
                http,
                Mock.Of<Microsoft.AspNetCore.Mvc.ViewFeatures.ITempDataProvider>())
        };
    }

    private static string FindUnder(params string[] parts)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var path = Path.Combine(new[] { dir.FullName }.Concat(parts).ToArray());
            if (Directory.Exists(path) || File.Exists(path))
                return path;
            dir = dir.Parent;
        }

        throw new FileNotFoundException(string.Join(Path.DirectorySeparatorChar, parts));
    }

    private sealed record ServiceFixture(
        ExamPeriodService Service,
        Mock<IExamPreferenceOptionRepository> Options);
}
