using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Moq;
using OzelYetenekSinavSistemi.Application.Common;
using OzelYetenekSinavSistemi.Application.Interfaces.Repositories;
using OzelYetenekSinavSistemi.Application.Interfaces.Services;
using OzelYetenekSinavSistemi.Application.Services;
using OzelYetenekSinavSistemi.Application.ViewModels.Account;
using OzelYetenekSinavSistemi.Domain.Entities;
using OzelYetenekSinavSistemi.Domain.Enums;
using OzelYetenekSinavSistemi.Tests.Services;
using OzelYetenekSinavSistemi.Web.Controllers;
using OzelYetenekSinavSistemi.Web.Infrastructure;

namespace OzelYetenekSinavSistemi.Tests.Security;

public sealed class LoginIdentityUiTests
{
    private const string LoginFailureMessage = "E-posta/T.C. Kimlik Numarası veya parola hatalı.";

    [Fact]
    public void LoginMarkup_HasEmailOrTurkishIdentityField()
    {
        var login = ReadProjectFile("src", "OzelYetenekSinavSistemi.Web", "Views", "Account", "Login.cshtml");

        Assert.Contains("asp-for=\"LoginIdentifier\"", login, StringComparison.Ordinal);
        Assert.Contains("label asp-for=\"LoginIdentifier\"", login, StringComparison.Ordinal);
        Assert.Contains("label asp-for=\"LoginIdentifier\" class=\"sr-only\"", login, StringComparison.Ordinal);
        Assert.Contains("E-posta adresi veya T.C. kimlik numarası</label>", login, StringComparison.Ordinal);
        Assert.Contains("placeholder=\"E-posta adresi veya T.C. kimlik numarası\"", login, StringComparison.Ordinal);
        Assert.DoesNotContain("ornek@eposta.com", login, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("11 haneli", login, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Yabancı uyruklu adaylar e-posta adresleriyle giriş yapmalıdır.", login, StringComparison.Ordinal);
        Assert.Contains("data-oys-login-identifier", login, StringComparison.Ordinal);
        Assert.Contains("autocomplete=\"username\"", login, StringComparison.Ordinal);
        Assert.Contains("maxlength=\"254\"", login, StringComparison.Ordinal);
        Assert.DoesNotContain("inputmode=\"numeric\"", login, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("asp-for=\"TcNo\"", login, StringComparison.Ordinal);
        Assert.DoesNotContain("onclick=", login, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void LoginMarkup_CandidateApplicationCtaIsProminentBeforeLoginForm()
    {
        var login = ReadProjectFile("src", "OzelYetenekSinavSistemi.Web", "Views", "Account", "Login.cshtml");
        var css = ReadProjectFile("src", "OzelYetenekSinavSistemi.Web", "wwwroot", "css", "site-security.css");

        Assert.Contains("Aday Başvurusu Oluştur", login, StringComparison.Ordinal);
        Assert.Contains("Hesabınız varsa giriş yapın", login, StringComparison.Ordinal);
        Assert.Contains("Giriş Yap", login, StringComparison.Ordinal);
        Assert.Contains("Parolamı Unuttum", login, StringComparison.Ordinal);
        Assert.Contains("site-login-forgot", login, StringComparison.Ordinal);
        Assert.Contains("asp-action=\"ForgotPassword\"", login, StringComparison.Ordinal);
        Assert.Contains("site-login-register", login, StringComparison.Ordinal);
        Assert.Contains("btn-primary", login, StringComparison.Ordinal);
        Assert.Contains("asp-action=\"Register\"", login, StringComparison.Ordinal);
        Assert.Contains("class=\"btn btn-white block full-width site-login-submit\"", login, StringComparison.Ordinal);
        Assert.Contains("_ValidationScriptsPartial", login, StringComparison.Ordinal);
        Assert.Contains("js-captcha-refresh", login, StringComparison.Ordinal);
        Assert.Contains("data-oys-captcha-refresh", login, StringComparison.Ordinal);
        Assert.Contains("data-captcha-url=", login, StringComparison.Ordinal);
        Assert.Contains("data-oys-captcha-image", login, StringComparison.Ordinal);
        Assert.Contains("data-oys-captcha-input", login, StringComparison.Ordinal);
        Assert.Contains("type=\"button\"", login, StringComparison.Ordinal);
        Assert.Contains("aria-label=\"Güvenlik kodunu yenile\"", login, StringComparison.Ordinal);
        Assert.Contains("asp-for=\"CaptchaInput\"", login, StringComparison.Ordinal);
        Assert.DoesNotContain("İlk kez mi başvuruyorsunuz?", login, StringComparison.Ordinal);
        Assert.DoesNotContain("Yeni adaylar kayıt oluşturarak başvuruya başlayabilir.", login, StringComparison.Ordinal);
        Assert.DoesNotContain("style=", login, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<script>", login, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("onclick=", login, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("href=\"/Account/Register\"", login, StringComparison.Ordinal);
        Assert.DoesNotContain("Aday Kaydı Oluştur", login, StringComparison.Ordinal);

        var ctaIndex = login.IndexOf("Aday Başvurusu Oluştur", StringComparison.Ordinal);
        var formIndex = login.IndexOf("asp-action=\"Login\"", StringComparison.Ordinal);
        var passwordLabelIndex = login.IndexOf("label asp-for=\"Password\"", StringComparison.Ordinal);
        var passwordInputIndex = login.IndexOf("asp-for=\"Password\"", passwordLabelIndex + 1, StringComparison.Ordinal);
        var forgotIndex = login.IndexOf("Parolamı Unuttum", StringComparison.Ordinal);
        var captchaIndex = login.IndexOf("asp-for=\"CaptchaInput\"", StringComparison.Ordinal);
        var submitIndex = login.IndexOf("site-login-submit", StringComparison.Ordinal);
        Assert.True(ctaIndex >= 0 && formIndex > ctaIndex, "CTA login formundan önce render edilmelidir.");
        Assert.True(forgotIndex > passwordInputIndex && forgotIndex < captchaIndex,
            "Parolamı Unuttum parola inputundan sonra ve CAPTCHA öncesinde olmalıdır.");
        Assert.True(submitIndex > captchaIndex, "Giriş Yap butonu CAPTCHA sonrasında korunmalıdır.");

        Assert.Contains(".site-login-register", css, StringComparison.Ordinal);
        Assert.Contains(".site-login-forgot", css, StringComparison.Ordinal);
        Assert.Contains(".site-login-forgot-row", css, StringComparison.Ordinal);
        Assert.Contains(".site-login-title", css, StringComparison.Ordinal);
        Assert.Contains(".site-login-label", css, StringComparison.Ordinal);
        Assert.Contains("max-width: 480px", css, StringComparison.Ordinal);
        Assert.Contains("min-height: 44px", css, StringComparison.Ordinal);
        Assert.DoesNotContain("outline: none", css, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("RememberMe", login, StringComparison.Ordinal);
        Assert.DoesNotContain("Beni hatırla", login, StringComparison.Ordinal);
    }

    [Fact]
    public void SiteJs_CaptchaRefreshUsesDelegatedSafeHandler()
    {
        var siteJs = ReadProjectFile("src", "OzelYetenekSinavSistemi.Web", "wwwroot", "js", "site.js");
        var block = ExtractBetween(siteJs, "function oysBindCaptchaRefresh", "function oysInitSelect2");

        Assert.Contains("window.oysCaptchaRefreshBound", block, StringComparison.Ordinal);
        Assert.Contains("document.addEventListener(\"click\"", block, StringComparison.Ordinal);
        Assert.Contains("closest(\".js-captcha-refresh, [data-oys-captcha-refresh]\")", block, StringComparison.Ordinal);
        Assert.Contains("preventDefault()", block, StringComparison.Ordinal);
        Assert.Contains("oysIsSafeCaptchaUrl", block, StringComparison.Ordinal);
        Assert.Contains("oysClearCaptchaInput", block, StringComparison.Ordinal);
        Assert.Contains("oysSnapshotLoginFormFieldValues", block, StringComparison.Ordinal);
        Assert.Contains("oysRestoreLoginFormFieldValues", block, StringComparison.Ordinal);
        Assert.Contains("aria-busy", block, StringComparison.Ordinal);
        Assert.Contains("Date.now()", block, StringComparison.Ordinal);
        Assert.DoesNotContain("innerHTML", block, StringComparison.Ordinal);
        Assert.DoesNotContain("eval(", block, StringComparison.Ordinal);

        // Load-time bind must exist outside initialize so loginidentifier .rules() hataları CAPTCHA'yı öldürmesin.
        Assert.Contains("\noysBindCaptchaRefresh();\n", siteJs.Replace("\r\n", "\n"), StringComparison.Ordinal);
        Assert.Contains("oysEnsureUnobtrusiveFormValidator", siteJs, StringComparison.Ordinal);
        Assert.Contains("unobtrusive.parse(form)", siteJs, StringComparison.Ordinal);
    }

    [Fact]
    public void ForgotPasswordMarkup_HasEmailOrTurkishIdentityField()
    {
        var view = ReadProjectFile("src", "OzelYetenekSinavSistemi.Web", "Views", "Account", "ForgotPassword.cshtml");

        Assert.Contains("asp-for=\"TcNoOrEmail\"", view, StringComparison.Ordinal);
        Assert.Contains("label asp-for=\"TcNoOrEmail\"", view, StringComparison.Ordinal);
        Assert.Contains("label asp-for=\"TcNoOrEmail\" class=\"sr-only\"", view, StringComparison.Ordinal);
        Assert.Contains("E-posta adresi veya T.C. kimlik numarası</label>", view, StringComparison.Ordinal);
        Assert.Contains("placeholder=\"E-posta adresi veya T.C. kimlik numarası\"", view, StringComparison.Ordinal);
        Assert.DoesNotContain("ornek@eposta.com", view, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("11 haneli", view, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("data-oys-login-identifier", view, StringComparison.Ordinal);
        Assert.Contains("site-forgot-page", view, StringComparison.Ordinal);
        Assert.Contains("site-forgot-input", view, StringComparison.Ordinal);
        Assert.Contains("asp-action=\"ForgotPassword\"", view, StringComparison.Ordinal);
        Assert.Contains("method=\"post\"", view, StringComparison.Ordinal);
        Assert.Contains("type=\"submit\"", view, StringComparison.Ordinal);
        Assert.Contains("asp-action=\"Login\"", view, StringComparison.Ordinal);
        Assert.Contains("Girişe Dön", view, StringComparison.Ordinal);
        Assert.Contains("Yabancı uyruklu adaylar kayıt sırasında kullandıkları e-posta adresini girmelidir.", view, StringComparison.Ordinal);
        Assert.DoesNotContain("onclick=", view, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SiteJs_HasLoginIdentifierValidation()
    {
        var siteJs = ReadProjectFile("src", "OzelYetenekSinavSistemi.Web", "wwwroot", "js", "site.js");
        var block = ExtractBetween(siteJs, "function oysBindLoginIdentifierValidation", "function oysInitializePage");

        Assert.Contains("function oysBindLoginIdentifierValidation", siteJs, StringComparison.Ordinal);
        Assert.Contains("[data-oys-login-identifier]", block, StringComparison.Ordinal);
        Assert.Contains("loginidentifier", block, StringComparison.Ordinal);
        Assert.Contains("oysEnsureUnobtrusiveFormValidator", block, StringComparison.Ordinal);
        Assert.DoesNotContain("$form.validate();", block, StringComparison.Ordinal);
        Assert.Contains("oysBindLoginIdentifierValidation()", siteJs, StringComparison.Ordinal);
        Assert.Contains("oysIsValidTurkishIdentityNumber", siteJs, StringComparison.Ordinal);
        Assert.DoesNotContain("$input.valid();", block, StringComparison.Ordinal);
        Assert.DoesNotContain("oysLoginIdentifierPageshowBound", block, StringComparison.Ordinal);
        Assert.Contains("function oysBindFormValidationStatePolicy", siteJs, StringComparison.Ordinal);
        Assert.Contains("oysApplyFormValidationStatePolicy();", siteJs, StringComparison.Ordinal);
        Assert.DoesNotContain("setTimeout(", block, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("ali@example.com", true)]
    [InlineData("10000000146", true)]
    [InlineData("99999999999", false)]
    [InlineData("AB1234567", false)]
    [InlineData("", false)]
    public void LoginIdentifierHelper_ValidatesAcceptedIdentifiers(string value, bool expected)
    {
        Assert.Equal(expected, LoginIdentifierHelper.IsValidLoginIdentifier(value));
    }

    [Fact]
    public async Task LoginPost_ValidTurkishIdentity_ReachesAuthenticationService()
    {
        var auth = new Mock<IAuthenticationService>();
        auth.Setup(a => a.ValidateCredentialsAsync(
                "10000000146",
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(OperationResult<User>.Fail(LoginFailureMessage));

        var controller = CreateController(auth.Object);
        SetupCaptcha(controller, "1234");

        await controller.Login(new LoginViewModel
        {
            LoginIdentifier = "10000000146",
            Password = "Passw0rd!",
            CaptchaInput = "1234"
        }, CancellationToken.None);

        auth.Verify(a => a.ValidateCredentialsAsync(
            "10000000146",
            "Passw0rd!",
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task LoginPost_ForeignIdentity_DoesNotReachAuthenticationService()
    {
        var auth = new Mock<IAuthenticationService>(MockBehavior.Strict);
        var controller = CreateController(auth.Object);
        SetupCaptcha(controller, "1234");

        var result = await controller.Login(new LoginViewModel
        {
            LoginIdentifier = "99999999999",
            Password = "Passw0rd!",
            CaptchaInput = "1234"
        }, CancellationToken.None);

        Assert.IsType<ViewResult>(result);
        auth.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task LoginPost_PassportNumber_DoesNotReachAuthenticationService()
    {
        var auth = new Mock<IAuthenticationService>(MockBehavior.Strict);
        var controller = CreateController(auth.Object);
        SetupCaptcha(controller, "1234");

        var result = await controller.Login(new LoginViewModel
        {
            LoginIdentifier = "AB1234567",
            Password = "Passw0rd!",
            CaptchaInput = "1234"
        }, CancellationToken.None);

        Assert.IsType<ViewResult>(result);
        auth.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task LoginPost_InvalidIdentifier_UsesGeneralFailureMessage()
    {
        var auth = new Mock<IAuthenticationService>(MockBehavior.Strict);
        var controller = CreateController(auth.Object);
        SetupCaptcha(controller, "1234");

        var result = await controller.Login(new LoginViewModel
        {
            LoginIdentifier = "99999999999",
            Password = "Passw0rd!",
            CaptchaInput = "1234"
        }, CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        Assert.Contains(view.ViewData.ModelState![string.Empty]!.Errors, e => e.ErrorMessage == LoginFailureMessage);
    }

    [Fact]
    public async Task ForgotPasswordPost_InvalidIdentifier_StillReturnsGeneralSuccessWithoutServiceCall()
    {
        var reset = new Mock<IPasswordResetService>(MockBehavior.Strict);
        var controller = CreateController(Mock.Of<IAuthenticationService>(), reset.Object);

        var result = await controller.ForgotPassword(
            new ForgotPasswordViewModel { TcNoOrEmail = "99999999999" },
            CancellationToken.None);

        Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(LoginIdentifierHelper.ForgotPasswordSuccessMessage, controller.TempData["ToastInfo"]);
        reset.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ForgotPasswordPost_ValidEmail_CallsPasswordResetService()
    {
        var reset = new Mock<IPasswordResetService>();
        reset.Setup(s => s.RequestResetAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(OperationResult.Ok());

        var controller = CreateController(Mock.Of<IAuthenticationService>(), reset.Object);

        await controller.ForgotPassword(
            new ForgotPasswordViewModel { TcNoOrEmail = "foreign@example.com" },
            CancellationToken.None);

        reset.Verify(s => s.RequestResetAsync(
            "foreign@example.com",
            It.IsAny<string>(),
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            It.IsAny<CancellationToken>()), Times.Once);
        Assert.Equal(LoginIdentifierHelper.ForgotPasswordSuccessMessage, controller.TempData["ToastInfo"]);
    }

    private static AccountController CreateController(
        IAuthenticationService auth,
        IPasswordResetService? reset = null)
    {
        var captcha = new Mock<ICaptchaService>();
        captcha.Setup(c => c.Validate(It.IsAny<string>(), It.IsAny<string>())).Returns(true);

        var httpContext = CreateHttpContextWithSession();
        var controller = new AccountController(
            auth,
            Mock.Of<IUserService>(),
            reset ?? Mock.Of<IPasswordResetService>(),
            captcha.Object,
            Mock.Of<IYgsYearRepository>(),
            Mock.Of<IPublicUrlBuilder>(),
            new CountryCatalog(),
            new IdentityDocumentValidator(TimeProvider.System),
            PhotoUploadTestSupport.CreateAcceptingPhotoService())
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext },
            TempData = new TempDataDictionary(httpContext, Mock.Of<ITempDataProvider>())
        };

        return controller;
    }

    private static DefaultHttpContext CreateHttpContextWithSession()
    {
        var context = new DefaultHttpContext();
        var storage = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        var session = new Mock<ISession>();
        session.Setup(s => s.Set(It.IsAny<string>(), It.IsAny<byte[]>()))
            .Callback<string, byte[]>((key, value) => storage[key] = value);
        session.Setup(s => s.TryGetValue(
        It.IsAny<string>(),
        out It.Ref<byte[]?>.IsAny))
    .Returns((string key, out byte[]? value) =>
    {
        if (storage.TryGetValue(key, out var bytes))
        {
            value = bytes;
            return true;
        }

        value = null;
        return false;
    });
        session.Setup(s => s.Remove(It.IsAny<string>()))
            .Callback<string>(key => storage.Remove(key));
        context.Session = session.Object;
        return context;
    }

    private static void SetupCaptcha(AccountController controller, string code)
    {
        controller.HttpContext.Session.SetString("LoginCaptchaCode", code);
    }

    private static string ExtractBetween(string source, string start, string end)
    {
        var startIndex = source.IndexOf(start, StringComparison.Ordinal);
        var endIndex = source.IndexOf(end, startIndex, StringComparison.Ordinal);
        Assert.True(startIndex >= 0 && endIndex > startIndex);
        return source.Substring(startIndex, endIndex - startIndex);
    }

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
