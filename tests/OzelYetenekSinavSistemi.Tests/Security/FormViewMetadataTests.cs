using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Moq;
using OzelYetenekSinavSistemi.Application.Common;
using OzelYetenekSinavSistemi.Application.DTOs.Applications;
using OzelYetenekSinavSistemi.Application.Interfaces.Repositories;
using OzelYetenekSinavSistemi.Application.Interfaces.Services;
using OzelYetenekSinavSistemi.Application.ViewModels.ExamPeriods;
using OzelYetenekSinavSistemi.Application.ViewModels.ExamResults;
using OzelYetenekSinavSistemi.Application.ViewModels.Profile;
using OzelYetenekSinavSistemi.Application.ViewModels.UserManagement;
using OzelYetenekSinavSistemi.Application.Services;
using OzelYetenekSinavSistemi.Domain.Common;
using OzelYetenekSinavSistemi.Domain.Entities;
using OzelYetenekSinavSistemi.Domain.Enums;
using OzelYetenekSinavSistemi.Tests.Services;
using OzelYetenekSinavSistemi.Web.Controllers;

namespace OzelYetenekSinavSistemi.Tests.Security;

public sealed class FormViewMetadataTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid ExamId = Guid.NewGuid();
    private static readonly Guid ApplicationId = Guid.NewGuid();

    [Fact]
    public void ExamPeriod_CreateGet_PreparesTitle()
    {
        var service = new Mock<IExamPeriodService>(MockBehavior.Strict);
        var controller = CreateExamPeriodController(service.Object);

        var result = Assert.IsType<ViewResult>(controller.Create());

        Assert.Equal("Sınav Dönemi Ekle", controller.ViewData["Title"]);
        Assert.IsType<ExamPeriodFormViewModel>(result.Model);
    }

    [Fact]
    public async Task ExamPeriod_CreatePost_InvalidModelState_PreservesTitleAndModel()
    {
        var service = new Mock<IExamPeriodService>(MockBehavior.Strict);
        var controller = CreateExamPeriodController(service.Object);
        var model = new ExamPeriodFormViewModel
        {
            Title = "Gönderilen Başlık",
            StartDate = DateTime.Today,
            EndDate = DateTime.Today.AddDays(10),
            MaxPreferences = 2
        };
        controller.ModelState.AddModelError(nameof(ExamPeriodFormViewModel.Title), "hata");

        var result = Assert.IsType<ViewResult>(await controller.Create(model, CancellationToken.None));

        Assert.Equal("Sınav Dönemi Ekle", controller.ViewData["Title"]);
        Assert.Same(model, result.Model);
        Assert.Equal("Gönderilen Başlık", Assert.IsType<ExamPeriodFormViewModel>(result.Model).Title);
        Assert.False(controller.ModelState.IsValid);
        service.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ExamPeriod_CreatePost_DateOrderError_PreservesTitle()
    {
        var service = new Mock<IExamPeriodService>(MockBehavior.Strict);
        var controller = CreateExamPeriodController(service.Object);
        var model = new ExamPeriodFormViewModel
        {
            Title = "Tarih Hatası",
            StartDate = new DateTime(2026, 7, 20),
            EndDate = new DateTime(2026, 7, 10),
            MaxPreferences = 1
        };

        var validation = model.Validate(new ValidationContext(model)).ToList();
        Assert.Contains(validation, v => v.MemberNames.Contains(nameof(ExamPeriodFormViewModel.EndDate)));
        foreach (var item in validation)
            controller.ModelState.AddModelError(item.MemberNames.First(), item.ErrorMessage ?? "hata");

        var result = Assert.IsType<ViewResult>(await controller.Create(model, CancellationToken.None));

        Assert.Equal("Sınav Dönemi Ekle", controller.ViewData["Title"]);
        Assert.Same(model, result.Model);
        Assert.Equal(new DateTime(2026, 7, 20), Assert.IsType<ExamPeriodFormViewModel>(result.Model).StartDate);
        Assert.Equal(new DateTime(2026, 7, 10), Assert.IsType<ExamPeriodFormViewModel>(result.Model).EndDate);
        service.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ExamPeriod_CreatePost_ServiceFailure_PreservesTitleAndModelState()
    {
        var service = new Mock<IExamPeriodService>();
        service.Setup(s => s.CreateAsync(It.IsAny<ExamPeriodFormViewModel>(), UserId, It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(OperationResult<Guid>.Fail("Bitiş tarihi başlangıç tarihinden sonra olmalıdır."));
        var controller = CreateExamPeriodController(service.Object);
        var model = new ExamPeriodFormViewModel
        {
            Title = "Servis Hatası",
            StartDate = DateTime.Today,
            EndDate = DateTime.Today.AddDays(5)
        };

        var result = Assert.IsType<ViewResult>(await controller.Create(model, CancellationToken.None));

        Assert.Equal("Sınav Dönemi Ekle", controller.ViewData["Title"]);
        Assert.Same(model, result.Model);
        Assert.False(controller.ModelState.IsValid);
        Assert.Contains(
            controller.ModelState.Values.SelectMany(v => v.Errors),
            e => e.ErrorMessage.Contains("Bitiş tarihi", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ExamPeriod_EditPost_InvalidModelState_PreservesTitleAndModel()
    {
        var service = new Mock<IExamPeriodService>(MockBehavior.Strict);
        var controller = CreateExamPeriodController(service.Object, DomainConstants.RoleNames.ApplicationManager);
        var model = new ExamPeriodFormViewModel
        {
            Id = ExamId,
            Title = "Düzenlenen",
            StartDate = DateTime.Today,
            EndDate = DateTime.Today.AddDays(3)
        };
        controller.ModelState.AddModelError(nameof(ExamPeriodFormViewModel.Title), "hata");

        var result = Assert.IsType<ViewResult>(await controller.Edit(model, CancellationToken.None));

        Assert.Equal("Sınav Dönemini Düzenle", controller.ViewData["Title"]);
        Assert.Same(model, result.Model);
        service.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task UserManagement_CreatePost_InvalidModelState_PreservesTitleRolesAndModel()
    {
        var service = new Mock<IUserManagementService>(MockBehavior.Strict);
        var controller = CreateUserManagementController(service.Object);
        var model = new CreateStaffUserViewModel
        {
            FirstName = "Ayşe",
            LastName = "Yılmaz",
            Email = "ayse@example.com",
            TcNo = "12345678901"
        };
        controller.ModelState.AddModelError(nameof(CreateStaffUserViewModel.Password), "zorunlu");

        var result = Assert.IsType<ViewResult>(await controller.Create(model, CancellationToken.None));

        Assert.Equal("Personel Oluştur", controller.ViewData["Title"]);
        Assert.IsType<SelectList>(controller.ViewBag.Roles);
        Assert.Same(model, result.Model);
        Assert.Equal("Ayşe", Assert.IsType<CreateStaffUserViewModel>(result.Model).FirstName);
        service.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task CandidateProfile_Post_InvalidModelState_ReloadsIdentityFromDatabase()
    {
        var userId = UserId;
        var userService = new Mock<IUserService>();
        var years = new Mock<IYgsYearRepository>();
        years.Setup(r => r.GetActiveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<YgsYear> { new() { Id = Guid.NewGuid(), Year = 2024 } });

        var dbProfile = new ProfileViewModel
        {
            Id = userId,
            FirstName = "Ali",
            LastName = "Veli",
            Email = "ali@example.com",
            Identity = new ProfileIdentityDisplayViewModel
            {
                DocumentTypeDisplay = "T.C. Kimlik Kartı",
                IdentityNumberLabel = "T.C. Kimlik Numarası",
                IdentityNumber = "10000000146",
                NationalityDisplay = "Türkiye"
            }
        };
        userService.Setup(s => s.GetProfileAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(OperationResult<ProfileViewModel>.Ok(dbProfile));

        var controller = new CandidateProfileController(
            userService.Object,
            years.Object,
            PhotoUploadTestSupport.CreateAcceptingPhotoService())
        {
            ControllerContext = new ControllerContext { HttpContext = CreateHttpContext(UserId, DomainConstants.RoleNames.Candidate) }
        };
        var posted = new ProfileUpdateViewModel
        {
            Id = userId,
            FirstName = "Ali",
            LastName = "Veli",
            Email = "ali@example.com",
            Phone = "bad"
        };
        controller.ModelState.AddModelError(nameof(ProfileUpdateViewModel.Phone), "hata");

        var result = Assert.IsType<ViewResult>(await controller.Index(posted, Photo: null, CancellationToken.None));

        Assert.Equal("Profilim", controller.ViewData["Title"]);
        Assert.NotNull(controller.ViewBag.YgsYears);
        var model = Assert.IsType<ProfileViewModel>(result.Model);
        Assert.Equal("10000000146", model.Identity.IdentityNumber);
        Assert.Equal("bad", model.Phone);
        userService.Verify(s => s.GetProfileAsync(userId, It.IsAny<CancellationToken>()), Times.Once);
        userService.Verify(s => s.UpdateProfileAsync(It.IsAny<Guid>(), It.IsAny<ProfileUpdateViewModel>(), It.IsAny<PhotoUploadRequest?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExamResult_Save_InvalidModelState_PreservesTitleCandidateAndModel()
    {
        var examPeriod = new Mock<IExamPeriodService>(MockBehavior.Strict);
        var examResult = new Mock<IExamResultService>();
        var excel = new Mock<IExcelExportService>(MockBehavior.Strict);
        var pdf = new Mock<IPdfExportService>(MockBehavior.Strict);
        var audit = new Mock<IAuditService>(MockBehavior.Strict);

        var candidate = new CandidateApplicationDetail
        {
            ApplicationId = ApplicationId,
            FirstName = "Zeynep",
            LastName = "Kaya",
            ExamPeriodId = ExamId
        };
        examResult.Setup(s => s.GetCandidateForEvaluationAsync(ApplicationId, UserId, DomainConstants.RoleNames.SuperAdmin, It.IsAny<CancellationToken>()))
            .ReturnsAsync(OperationResult<CandidateApplicationDetail>.Ok(candidate));

        var controller = new ExamResultController(
            examPeriod.Object, examResult.Object, excel.Object, pdf.Object, audit.Object,
            new CountryCatalog(), new SensitiveDataMaskingService())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = CreateHttpContext(UserId, DomainConstants.RoleNames.SuperAdmin)
            }
        };

        var model = new ExamResultFormViewModel
        {
            ApplicationId = ApplicationId,
            AttendanceStatus = AttendanceStatus.Disqualified,
            ExamScore = 88
        };
        controller.ModelState.AddModelError(nameof(ExamResultFormViewModel.AttendanceStatus), "zorunlu");

        var result = Assert.IsType<ViewResult>(await controller.Save(model, ExamId, CancellationToken.None));

        Assert.Equal("Evaluate", result.ViewName);
        Assert.Equal("Değerlendirme", controller.ViewData["Title"]);
        Assert.Same(candidate, controller.ViewBag.Candidate);
        Assert.Same(model, result.Model);
        Assert.Equal(88, Assert.IsType<ExamResultFormViewModel>(result.Model).ExamScore);
        Assert.Equal(
            AttendanceStatus.Disqualified,
            Assert.IsType<ExamResultFormViewModel>(result.Model).AttendanceStatus);
        examPeriod.VerifyNoOtherCalls();
        excel.VerifyNoOtherCalls();
        pdf.VerifyNoOtherCalls();
        audit.VerifyNoOtherCalls();
    }

    [Fact]
    public void Layouts_DoNotProduceLeadingDashTitleWhenTitleMissing()
    {
        foreach (var relative in new[]
                 {
                     Path.Combine("Shared", "_AdminLayout.cshtml"),
                     Path.Combine("Shared", "_CandidateLayout.cshtml"),
                     Path.Combine("Shared", "_AuthLayout.cshtml")
                 })
        {
            var source = File.ReadAllText(FindUnder("src", "OzelYetenekSinavSistemi.Web", "Views", relative));
            Assert.DoesNotContain("<title>@ViewData[\"Title\"] - ÖYS</title>", source, StringComparison.Ordinal);
            Assert.Contains("_BrowserTabHead", source, StringComparison.Ordinal);
            Assert.DoesNotContain("<title>", source, StringComparison.OrdinalIgnoreCase);
        }

        var head = File.ReadAllText(FindUnder(
            "src", "OzelYetenekSinavSistemi.Web", "Views", "Shared", "_BrowserTabHead.cshtml"));
        Assert.Contains("BrowserTabBranding.FormatDocumentTitle", head, StringComparison.Ordinal);
        Assert.Contains("ÖYS",
            File.ReadAllText(FindUnder(
                "src", "OzelYetenekSinavSistemi.Web", "Infrastructure", "BrowserTabBranding.cs")),
            StringComparison.Ordinal);
    }

    [Fact]
    public void ExamPeriodController_UsesPrepareHelpersOnFailurePaths()
    {
        var source = File.ReadAllText(FindUnder("src", "OzelYetenekSinavSistemi.Web", "Controllers", "ExamPeriodController.cs"));
        Assert.Contains("PrepareCreateViewData()", source, StringComparison.Ordinal);
        Assert.Contains("PrepareEditViewData()", source, StringComparison.Ordinal);
        Assert.Contains("if (!ModelState.IsValid)", source, StringComparison.Ordinal);
        Assert.DoesNotContain("if (!ModelState.IsValid)\n            return View(model);", source, StringComparison.Ordinal);
        Assert.DoesNotContain("if (!ModelState.IsValid)\r\n            return View(model);", source, StringComparison.Ordinal);
    }

    private static ExamPeriodController CreateExamPeriodController(
        IExamPeriodService service,
        string role = DomainConstants.RoleNames.SuperAdmin)
    {
        return new ExamPeriodController(service)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = CreateHttpContext(UserId, role)
            }
        };
    }

    private static UserManagementController CreateUserManagementController(IUserManagementService service)
    {
        return new UserManagementController(service)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = CreateHttpContext(UserId, DomainConstants.RoleNames.SuperAdmin)
            }
        };
    }

    private static DefaultHttpContext CreateHttpContext(Guid userId, string role)
    {
        var context = new DefaultHttpContext();
        var identity = new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Role, role)
        }, authenticationType: "Test");
        context.User = new ClaimsPrincipal(identity);
        return context;
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
}
