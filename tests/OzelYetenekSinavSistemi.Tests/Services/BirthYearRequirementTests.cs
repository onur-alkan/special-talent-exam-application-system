using System.ComponentModel.DataAnnotations;
using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OzelYetenekSinavSistemi.Application.Common;
using OzelYetenekSinavSistemi.Application.Interfaces.Repositories;
using OzelYetenekSinavSistemi.Application.Interfaces.Services;
using OzelYetenekSinavSistemi.Application.Services;
using OzelYetenekSinavSistemi.Application.Validation;
using OzelYetenekSinavSistemi.Application.ViewModels.Account;
using OzelYetenekSinavSistemi.Application.ViewModels.Profile;
using OzelYetenekSinavSistemi.Domain.Entities;
using OzelYetenekSinavSistemi.Domain.Enums;
using OzelYetenekSinavSistemi.Web.Infrastructure;

namespace OzelYetenekSinavSistemi.Tests.Services;

/// <summary>
/// Doğum tarihi (BirthDate) kayıt kuralları + legacy BirthYear sütun koruması.
/// </summary>
public sealed class BirthYearRequirementTests
{
    private static readonly byte[] JpegHeader = PhotoUploadTestSupport.MinimalJpeg;

    [Fact]
    public void BirthDateRules_TryCompose_HandlesLeapYearsAndIncompleteSelection()
    {
        var today = new DateOnly(2026, 7, 23);

        Assert.True(BirthDateRules.TryCompose(15, 8, 2005, today, out var ok, out var err));
        Assert.Equal(new DateOnly(2005, 8, 15), ok);
        Assert.Null(err);

        Assert.True(BirthDateRules.TryCompose(29, 2, 2000, today, out var leap, out _));
        Assert.Equal(new DateOnly(2000, 2, 29), leap);

        Assert.False(BirthDateRules.TryCompose(29, 2, 2001, today, out _, out var invalidLeap));
        Assert.Equal(BirthDateRules.InvalidMessage, invalidLeap);

        Assert.False(BirthDateRules.TryCompose(31, 2, 2005, today, out _, out var invalidDay));
        Assert.Equal(BirthDateRules.InvalidMessage, invalidDay);

        Assert.False(BirthDateRules.TryCompose(null, 1, 2000, today, out _, out var incomplete));
        Assert.Equal(BirthDateRules.RequiredMessage, incomplete);

        Assert.False(BirthDateRules.TryCompose(1, 1, today.Year + 1, today, out _, out var future));
        Assert.Equal(BirthDateRules.FutureMessage, future);

        Assert.True(BirthDateRules.TryCompose(today.Day, today.Month, today.Year, today, out var todayDate, out _));
        Assert.Equal(today, todayDate);
    }

    [Fact]
    public void ProfileBirthDateDisplay_UsesLegacyYearLabelWithoutInventingDayMonth()
    {
        var full = new ProfileViewModel { BirthDate = new DateOnly(2005, 8, 15), BirthYear = 2005 };
        Assert.Equal(BirthDateRules.FullDateLabel, full.BirthDateLabel);
        Assert.Equal("15.08.2005", full.BirthDateDisplay);

        var legacy = new ProfileViewModel { BirthDate = null, BirthYear = 2005 };
        Assert.Equal(BirthDateRules.LegacyYearLabel, legacy.BirthDateLabel);
        Assert.Equal("2005", legacy.BirthDateDisplay);
        Assert.DoesNotContain("01.01", legacy.BirthDateDisplay, StringComparison.Ordinal);

        var empty = new ProfileViewModel();
        Assert.Equal(BirthDateRules.FullDateLabel, empty.BirthDateLabel);
        Assert.Equal(BirthDateRules.UnspecifiedDisplay, empty.BirthDateDisplay);
    }

    [Fact]
    public async Task RegisterCandidate_ValidBirthDate_PersistsDateAndLegacyYear()
    {
        var yearId = Guid.NewGuid();
        User? saved = null;
        var sut = CreateAuthenticationService(yearId, out var users, out var photos);
        users.Setup(r => r.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .Callback<User, CancellationToken>((user, _) => saved = user)
            .ReturnsAsync(Guid.NewGuid());

        var birthDate = new DateOnly(2001, 6, 15);
        var result = await sut.RegisterCandidateAsync(
            ValidRegister(yearId, birthDate), ValidPhoto(), null, null);

        Assert.True(result.Success);
        Assert.NotNull(saved);
        Assert.Equal(birthDate, saved!.BirthDate);
        Assert.Equal((short)2001, saved.BirthYear);
        photos.Verify(
            p => p.SavePhotoAsync(It.IsAny<PhotoUploadRequest>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RegisterCandidate_FutureBirthDate_IsRejectedBeforePhotoAndRepository()
    {
        var yearId = Guid.NewGuid();
        var sut = CreateAuthenticationService(yearId, out var users, out var photos);
        var tomorrow = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(1));

        var result = await sut.RegisterCandidateAsync(
            ValidRegister(yearId, tomorrow), ValidPhoto(), null, null);

        Assert.False(result.Success);
        Assert.Contains(BirthDateRules.FutureMessage, result.ValidationErrors);
        users.Verify(r => r.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
        photos.Verify(
            p => p.SavePhotoAsync(It.IsAny<PhotoUploadRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task RegisterCandidate_TodayBirthDate_IsAccepted()
    {
        var yearId = Guid.NewGuid();
        User? saved = null;
        var sut = CreateAuthenticationService(yearId, out var users, out var photos);
        users.Setup(r => r.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .Callback<User, CancellationToken>((user, _) => saved = user)
            .ReturnsAsync(Guid.NewGuid());

        var today = DateOnly.FromDateTime(DateTime.UtcNow.Date);
        var result = await sut.RegisterCandidateAsync(
            ValidRegister(yearId, today), ValidPhoto(), null, null);

        Assert.True(result.Success);
        Assert.Equal(today, saved!.BirthDate);
        Assert.Equal((short)today.Year, saved.BirthYear);
    }

    [Fact]
    public async Task RegisterCandidate_LeapDay2000_IsAccepted()
    {
        var yearId = Guid.NewGuid();
        User? saved = null;
        var sut = CreateAuthenticationService(yearId, out var users, out var photos);
        users.Setup(r => r.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .Callback<User, CancellationToken>((user, _) => saved = user)
            .ReturnsAsync(Guid.NewGuid());

        var leap = new DateOnly(2000, 2, 29);
        var result = await sut.RegisterCandidateAsync(
            ValidRegister(yearId, leap), ValidPhoto(), null, null);

        Assert.True(result.Success);
        Assert.Equal(leap, saved!.BirthDate);
    }

    [Fact]
    public async Task RegisterCandidate_MissingBirthDate_IsRejected()
    {
        var yearId = Guid.NewGuid();
        var sut = CreateAuthenticationService(yearId, out var users, out var photos);
        var model = ValidRegister(yearId, new DateOnly(2000, 6, 15));
        model.BirthDate = null;

        var result = await sut.RegisterCandidateAsync(model, ValidPhoto(), null, null);

        Assert.False(result.Success);
        Assert.Contains(BirthDateRules.RequiredMessage, result.ValidationErrors);
        users.Verify(r => r.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetProfile_MapsBirthDate_AndAllowsLegacyYearOnly()
    {
        var users = new Mock<IUserRepository>();
        users.SetupSequence(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User
            {
                Id = Guid.NewGuid(),
                BirthDate = new DateOnly(1999, 3, 10),
                BirthYear = 1999
            })
            .ReturnsAsync(new User { Id = Guid.NewGuid(), BirthYear = 1999, BirthDate = null })
            .ReturnsAsync(new User { Id = Guid.NewGuid(), BirthYear = null, BirthDate = null });
        var sut = CreateUserService(users.Object);

        var withDate = await sut.GetProfileAsync(Guid.NewGuid());
        var yearOnly = await sut.GetProfileAsync(Guid.NewGuid());
        var empty = await sut.GetProfileAsync(Guid.NewGuid());

        Assert.True(withDate.Success);
        Assert.Equal(new DateOnly(1999, 3, 10), withDate.Data!.BirthDate);
        Assert.Equal("10.03.1999", withDate.Data.BirthDateDisplay);
        Assert.Equal(BirthDateRules.FullDateLabel, withDate.Data.BirthDateLabel);
        Assert.True(yearOnly.Success);
        Assert.Null(yearOnly.Data!.BirthDate);
        Assert.Equal((short)1999, yearOnly.Data.BirthYear);
        Assert.Equal("1999", yearOnly.Data.BirthDateDisplay);
        Assert.Equal(BirthDateRules.LegacyYearLabel, yearOnly.Data.BirthDateLabel);
        Assert.True(empty.Success);
        Assert.Equal(BirthDateRules.UnspecifiedDisplay, empty.Data!.BirthDateDisplay);
        Assert.Equal(BirthDateRules.FullDateLabel, empty.Data.BirthDateLabel);
    }

    [Fact]
    public async Task UpdateProfile_DoesNotChangePostedBirthFields()
    {
        var userId = Guid.NewGuid();
        var persisted = new User
        {
            Id = userId,
            FirstName = "Ali",
            LastName = "Veli",
            Email = "ali@example.com",
            BirthYear = 1990,
            BirthDate = new DateOnly(1990, 5, 1)
        };
        var users = new Mock<IUserRepository>();
        users.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(persisted);
        users.Setup(r => r.UpdateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var sut = CreateUserService(users.Object);

        var result = await sut.UpdateProfileAsync(userId, new ProfileUpdateViewModel
        {
            FirstName = "Ali",
            LastName = "Veli",
            Email = "ali@example.com"
        }, photo: null);

        Assert.True(result.Success);
        Assert.Equal((short)1990, persisted.BirthYear);
        Assert.Equal(new DateOnly(1990, 5, 1), persisted.BirthDate);
        users.Verify(
            r => r.UpdateAsync(
                It.Is<User>(u => u.BirthYear == 1990 && u.BirthDate == new DateOnly(1990, 5, 1)),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public void Migration003_BirthYear_IsIdempotentNullableSmallInt_WithCheckConstraint()
    {
        var migration = ReadProjectFile("database", "migrations", "003_AddUsersBirthYear.sql");

        Assert.Contains("COL_LENGTH(N'dbo.Users', N'BirthYear') IS NULL", migration, StringComparison.Ordinal);
        Assert.Contains("ADD BirthYear SMALLINT NULL", migration, StringComparison.Ordinal);
        Assert.Contains("IF NOT EXISTS", migration, StringComparison.Ordinal);
        Assert.Contains("CK_Users_BirthYear", migration, StringComparison.Ordinal);
        Assert.Contains("BirthYear IS NULL", migration, StringComparison.Ordinal);
        Assert.Contains("BirthYear BETWEEN 1900 AND 2100", migration, StringComparison.Ordinal);
        Assert.DoesNotContain("IDENTITY", migration, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DELETE FROM dbo.Users", migration, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Migration006_BirthDate_IsNullableDate_WithoutRewritingExistingYears()
    {
        var migration = ReadProjectFile("database", "migrations", "006_AddApplicantBirthDate.sql");

        Assert.Contains("COL_LENGTH(N'dbo.Users', N'BirthDate') IS NULL", migration, StringComparison.Ordinal);
        Assert.Contains("ADD BirthDate DATE NULL", migration, StringComparison.Ordinal);
        Assert.DoesNotContain("UPDATE", migration, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DELETE FROM dbo.Users", migration, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("BirthYear =", migration, StringComparison.Ordinal);
        Assert.DoesNotContain("DROP COLUMN", migration, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RepositoryAndSchema_IncludeBirthDateDateAndLegacyBirthYear()
    {
        var schema = ReadProjectFile("database", "OzelYetenekSinavSistemi.sql");
        var repository = ReadProjectFile(
            "src", "OzelYetenekSinavSistemi.Infrastructure", "Persistence", "Repositories", "UserRepository.cs");
        var genericRepository = ReadProjectFile(
            "src", "OzelYetenekSinavSistemi.Infrastructure", "Persistence", "Repositories", "GenericRepository.cs");

        Assert.Contains("BirthYear         SMALLINT      NULL", schema, StringComparison.Ordinal);
        Assert.Contains("BirthDate         DATE          NULL", schema, StringComparison.Ordinal);
        Assert.Contains("CK_Users_BirthYear", schema, StringComparison.Ordinal);
        Assert.Contains("BirthDate", repository, StringComparison.Ordinal);
        Assert.Contains("BirthYear", repository, StringComparison.Ordinal);
        Assert.Contains("IdentityDocumentType", repository, StringComparison.Ordinal);
        Assert.Contains("\"@\" + c", genericRepository, StringComparison.Ordinal);
        Assert.Contains("new CommandDefinition(sql, entity", genericRepository, StringComparison.Ordinal);
    }

    [Fact]
    public void RegisterAndProfileViews_ExposeBirthDateSemantics()
    {
        var register = ReadProjectFile(
            "src", "OzelYetenekSinavSistemi.Web", "Views", "Account", "Register.cshtml");
        var profile = ReadProjectFile(
            "src", "OzelYetenekSinavSistemi.Web", "Views", "CandidateProfile", "Index.cshtml");
        var adminDetail = ReadProjectFile(
            "src", "OzelYetenekSinavSistemi.Web", "Views", "ExamResult", "Evaluate.cshtml");
        var validationScripts = ReadProjectFile(
            "src", "OzelYetenekSinavSistemi.Web", "Views", "Shared", "_ValidationScriptsPartial.cshtml");
        var program = ReadProjectFile("src", "OzelYetenekSinavSistemi.Web", "Program.cs");
        var siteJs = ReadProjectFile("src", "OzelYetenekSinavSistemi.Web", "wwwroot", "js", "site.js");

        Assert.Contains("novalidate", register, StringComparison.Ordinal);
        Assert.Contains("asp-for=\"BirthDateDay\"", register, StringComparison.Ordinal);
        Assert.Contains("asp-for=\"BirthDateMonth\"", register, StringComparison.Ordinal);
        Assert.Contains("asp-for=\"BirthDateYear\"", register, StringComparison.Ordinal);
        Assert.Contains("asp-validation-for=\"BirthDate\"", register, StringComparison.Ordinal);
        Assert.Contains("data-oys-birth-date-root", register, StringComparison.Ordinal);
        Assert.DoesNotContain("data-oys-birth-date\"", register, StringComparison.Ordinal);
        Assert.DoesNotContain("mm/dd/yyyy", register, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("autocomplete=\"bday-day\"", register, StringComparison.Ordinal);
        Assert.Contains("autocomplete=\"bday-month\"", register, StringComparison.Ordinal);
        Assert.Contains("autocomplete=\"bday-year\"", register, StringComparison.Ordinal);
        Assert.Contains("Doğum tarihinizi gün, ay ve yıl olarak seçiniz.", register, StringComparison.Ordinal);
        Assert.Contains("BirthDateRules.TurkishMonthNames", register, StringComparison.Ordinal);
        Assert.Contains("Ocak", ReadProjectFile(
            "src", "OzelYetenekSinavSistemi.Application", "Validation", "BirthDateRules.cs"), StringComparison.Ordinal);
        Assert.Contains("Şubat", ReadProjectFile(
            "src", "OzelYetenekSinavSistemi.Application", "Validation", "BirthDateRules.cs"), StringComparison.Ordinal);
        Assert.DoesNotContain("asp-for=\"BirthYear\"", register, StringComparison.Ordinal);
        Assert.Contains("asp-for=\"YgsScore\"", register, StringComparison.Ordinal);
        Assert.DoesNotContain("DateTime.UtcNow", register, StringComparison.Ordinal);
        Assert.DoesNotContain("DateTime.Now", register, StringComparison.Ordinal);
        Assert.Contains("_ValidationScriptsPartial", register, StringComparison.Ordinal);
        Assert.Contains("jquery.validate", validationScripts, StringComparison.Ordinal);
        Assert.Contains("jquery.validate.unobtrusive", validationScripts, StringComparison.Ordinal);
        Assert.Contains("BirthDateClientModelValidatorProvider", program, StringComparison.Ordinal);
        Assert.Contains("oysIsValidBirthDate", siteJs, StringComparison.Ordinal);
        Assert.Contains("oysBindBirthDateParts", siteJs, StringComparison.Ordinal);
        Assert.Contains("addMethod(\"birthdateparts\"", siteJs, StringComparison.Ordinal);
        Assert.Contains("BirthDateLabel", profile, StringComparison.Ordinal);
        Assert.Contains("Belirtilmemiş", ReadProjectFile(
            "src", "OzelYetenekSinavSistemi.Application", "Validation", "BirthDateRules.cs"), StringComparison.Ordinal);
        Assert.Contains("BirthDateDisplay", profile, StringComparison.Ordinal);
        Assert.Contains("disabled", profile, StringComparison.Ordinal);
        Assert.Contains("Doğum Tarihi", adminDetail, StringComparison.Ordinal);
        Assert.DoesNotContain("Doğum Yılı", adminDetail, StringComparison.Ordinal);
    }

    [Fact]
    public void RegisterViewModel_BirthDate_HasTurkishRequiredMessage()
    {
        var model = new RegisterViewModel();
        var results = model.Validate(new ValidationContext(model)).ToList();

        Assert.Contains(results, r =>
            r.MemberNames.Contains(nameof(RegisterViewModel.BirthDate))
            && r.ErrorMessage == BirthDateRules.RequiredMessage);
    }

    [Fact]
    public void BirthDateClientValidator_EmitsMaxAndUnobtrusiveAttributes()
    {
        var fixedNow = new DateTimeOffset(2026, 7, 17, 12, 0, 0, TimeSpan.Zero);
        var time = new FakeTimeProvider(fixedNow);
        var expectedMax = "2026-07-17";

        var attributes = RenderBirthDateClientAttributes(time);

        Assert.Equal("true", attributes["data-val"]);
        Assert.Equal(BirthDateRules.FutureMessage, attributes["data-val-birthdate"]);
        Assert.Equal(expectedMax, attributes["max"]);
        Assert.Equal(BirthDateRules.FutureMessage, attributes["data-msg-max"]);
        Assert.DoesNotContain(
            attributes.Values,
            v => v.Contains("The value", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(
            attributes.Values,
            v => v.Contains("is not valid", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void BirthDateClientValidator_DoesNotOverwriteExistingAttributes()
    {
        var time = new FakeTimeProvider(new DateTimeOffset(2026, 7, 17, 12, 0, 0, TimeSpan.Zero));
        var metadataProvider = new EmptyModelMetadataProvider();
        var metadata = metadataProvider.GetMetadataForProperty(
            typeof(RegisterViewModel), nameof(RegisterViewModel.BirthDate));
        var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["data-msg-max"] = "Mevcut mesaj korunmalı.",
            ["max"] = "2099-01-01"
        };
        var clientContext = new ClientModelValidationContext(
            new ActionContext(), metadata, metadataProvider, attributes);

        new BirthDateClientValidator(time).AddValidation(clientContext);

        Assert.Equal("Mevcut mesaj korunmalı.", attributes["data-msg-max"]);
        Assert.Equal("2099-01-01", attributes["max"]);
        Assert.Equal(BirthDateRules.FutureMessage, attributes["data-val-birthdate"]);
    }

    [Fact]
    public void YgsScore_DoesNotReceiveBirthDateClientValidator()
    {
        var services = new ServiceCollection();
        services.AddMvcCore().AddDataAnnotations();
        using var sp = services.BuildServiceProvider();
        var metadataProvider = sp.GetRequiredService<IModelMetadataProvider>();
        var ygsMetadata = metadataProvider.GetMetadataForProperty(
            typeof(RegisterViewModel), nameof(RegisterViewModel.YgsScore));

        Assert.DoesNotContain(ygsMetadata.ValidatorMetadata, static m => m is BirthDateAttribute);

        var context = new ClientValidatorProviderContext(ygsMetadata, new List<ClientValidatorItem>());
        new BirthDateClientModelValidatorProvider(TimeProvider.System).CreateValidators(context);

        Assert.Empty(context.Results);
    }

    [Fact]
    public void RegisterHtml_DoesNotContainEnglishModelBindingPhrases()
    {
        var register = ReadProjectFile(
            "src", "OzelYetenekSinavSistemi.Web", "Views", "Account", "Register.cshtml");
        var siteJs = ReadProjectFile("src", "OzelYetenekSinavSistemi.Web", "wwwroot", "js", "site.js");

        Assert.DoesNotContain("Please enter a value", register, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("The value", register, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("is not valid", siteJs, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("oysBirthDatePartsValidationResult", siteJs, StringComparison.Ordinal);
        Assert.Contains(BirthDateRules.InvalidMessage, siteJs, StringComparison.Ordinal);
        Assert.Contains(BirthDateRules.FutureMessage, siteJs, StringComparison.Ordinal);
        Assert.Contains(BirthDateRules.RequiredMessage, siteJs, StringComparison.Ordinal);
    }

    private static IDictionary<string, string> RenderBirthDateClientAttributes(TimeProvider time)
    {
        var metadataProvider = new EmptyModelMetadataProvider();
        var metadata = metadataProvider.GetMetadataForProperty(
            typeof(RegisterViewModel), nameof(RegisterViewModel.BirthDate));
        var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var clientContext = new ClientModelValidationContext(
            new ActionContext(), metadata, metadataProvider, attributes);
        new BirthDateClientValidator(time).AddValidation(clientContext);
        return attributes;
    }

    [Fact]
    public void BirthDateClientModelValidatorProvider_CreatesValidator_WhenAttributePresent()
    {
        var services = new ServiceCollection();
        services.AddMvcCore().AddDataAnnotations();
        using var sp = services.BuildServiceProvider();
        var metadataProvider = sp.GetRequiredService<IModelMetadataProvider>();
        var modelMetadata = metadataProvider.GetMetadataForProperty(
            typeof(RegisterViewModel), nameof(RegisterViewModel.BirthDate));

        Assert.Contains(modelMetadata.ValidatorMetadata, static m => m is BirthDateAttribute);
        Assert.DoesNotContain(modelMetadata.ValidatorMetadata, static m => m is RequiredAttribute);

        var context = new ClientValidatorProviderContext(modelMetadata, new List<ClientValidatorItem>());
        new BirthDateClientModelValidatorProvider(TimeProvider.System).CreateValidators(context);

        Assert.Single(context.Results);
        Assert.IsType<BirthDateClientValidator>(context.Results[0].Validator);
    }

    [Fact]
    public void BirthDateYearSelect_EmitsTurkishPartsClientValidationMessage()
    {
        var register = ReadProjectFile(
            "src", "OzelYetenekSinavSistemi.Web", "Views", "Account", "Register.cshtml");

        Assert.Contains("data-val-birthdateparts=", register, StringComparison.Ordinal);
        Assert.Contains(BirthDateRules.RequiredMessage, register, StringComparison.Ordinal);
        Assert.Contains("data-oys-birth-year", register, StringComparison.Ordinal);
    }

    [Fact]
    public void RegisterViewModel_BirthDateAttribute_RejectsFuture_AcceptsToday()
    {
        var attr = new BirthDateAttribute();
        var today = DateOnly.FromDateTime(DateTime.UtcNow.Date);
        var context = new ValidationContext(new object());

        Assert.Equal(ValidationResult.Success, attr.GetValidationResult(today, context));
        Assert.Equal(
            BirthDateRules.FutureMessage,
            attr.GetValidationResult(today.AddDays(1), context)!.ErrorMessage);
    }

    private sealed class FakeTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _utcNow;

        public FakeTimeProvider(DateTimeOffset utcNow) => _utcNow = utcNow;

        public override DateTimeOffset GetUtcNow() => _utcNow;
    }

    [Fact]
    public void IdentityVerificationContract_ExistsWithoutProductionRegistration()
    {
        var contract = ReadProjectFile(
            "src", "OzelYetenekSinavSistemi.Application", "Interfaces", "Services", "IIdentityVerificationService.cs");
        var registrations = ReadProjectFile(
            "src", "OzelYetenekSinavSistemi.Application", "DependencyInjection.cs");

        Assert.Contains("VerifyAsync(", contract, StringComparison.Ordinal);
        Assert.Contains("int birthYear", contract, StringComparison.Ordinal);
        Assert.DoesNotContain("IIdentityVerificationService,", registrations, StringComparison.Ordinal);
    }

    [Fact]
    public void SiteJs_IdentityTypeChange_DoesNotClearBirthDate()
    {
        var siteJs = ReadProjectFile("src", "OzelYetenekSinavSistemi.Web", "wwwroot", "js", "site.js");
        var snippet = GetIdentityTypeChangeHandlerSnippet(siteJs);

        Assert.Contains("oysClearIdentityNumberClientError(form, identityInput)", snippet, StringComparison.Ordinal);
        Assert.DoesNotContain("BirthDate", snippet, StringComparison.Ordinal);
        Assert.DoesNotContain("birth-date", snippet, StringComparison.OrdinalIgnoreCase);
    }

    private static string GetIdentityTypeChangeHandlerSnippet(string siteJs)
    {
        const string marker = "var previousType = oysGetIdentityDocumentTypeValue(form);";
        var start = siteJs.IndexOf(marker, StringComparison.Ordinal);
        Assert.True(start >= 0);
        return siteJs.Substring(start, Math.Min(500, siteJs.Length - start));
    }

    private static AuthenticationService CreateAuthenticationService(
        Guid yearId,
        out Mock<IUserRepository> users,
        out Mock<IPhotoUploadService> photos)
    {
        users = new Mock<IUserRepository>();
        photos = new Mock<IPhotoUploadService>();
        var years = new Mock<IYgsYearRepository>();
        var passwords = new Mock<IPasswordService>();
        var audits = new Mock<IAuditService>();
        var masking = new Mock<ISensitiveDataMaskingService>();

        years.Setup(r => r.GetByIdAsync(yearId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new YgsYear { Id = yearId });
        users.Setup(r => r.IdentityExistsAsync(It.IsAny<IdentityDocumentType>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        users.Setup(r => r.TcNoExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        users.Setup(r => r.EmailExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
        passwords.Setup(p => p.Hash(It.IsAny<string>())).Returns("hash");
        masking.Setup(m => m.MaskTcNo(It.IsAny<string>())).Returns("***********");
        photos.Setup(p => p.SavePhotoAsync(It.IsAny<PhotoUploadRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(PhotoUploadResult.Ok($"/uploads/photos/{Guid.NewGuid():N}.jpg"));
        users.Setup(r => r.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Guid.NewGuid());
        audits.Setup(a => a.LogAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<string?>(),
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        return new AuthenticationService(
            users.Object,
            years.Object,
            passwords.Object,
            new IdentityDocumentValidator(TimeProvider.System),
            photos.Object,
            audits.Object,
            masking.Object,
            PhotoUploadTestSupport.SharedLock,
            TimeProvider.System);
    }

    private static UserService CreateUserService(IUserRepository users)
    {
        var audit = new Mock<IAuditService>();
        audit.Setup(a => a.LogAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<string?>(),
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        return new UserService(
            users,
            Mock.Of<IYgsYearRepository>(),
            Mock.Of<IPhotoUploadService>(),
            Mock.Of<IPasswordService>(),
            audit.Object,
            new CountryCatalog(),
            NullLogger<UserService>.Instance,
            PhotoUploadTestSupport.SharedLock);
    }

    private static RegisterViewModel ValidRegister(Guid yearId, DateOnly birthDate) => new()
    {
        FirstName = "Ali",
        LastName = "Veli",
        IdentityDocumentType = IdentityDocumentType.TurkishIdentityNumber,
        IdentityNumber = "10000000146",
        NationalityCountryCode = "TR",
        BirthDate = birthDate,
        Email = "ali@example.com",
        Phone = "05321234567",
        Password = "Passw0rd!",
        ConfirmPassword = "Passw0rd!",
        YgsScore = 250,
        YgsYearId = yearId
    };

    private static PhotoUploadRequest ValidPhoto() => new()
    {
        Content = new MemoryStream(JpegHeader),
        FileName = "photo.jpg",
        ContentType = "image/jpeg",
        Length = JpegHeader.Length
    };

    private static string ReadProjectFile(params string[] parts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(new[] { directory.FullName }.Concat(parts).ToArray());
            if (File.Exists(candidate))
                return File.ReadAllText(candidate);
            directory = directory.Parent;
        }

        throw new FileNotFoundException(string.Join(Path.DirectorySeparatorChar, parts));
    }
}
