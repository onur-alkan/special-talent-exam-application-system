namespace OzelYetenekSinavSistemi.Tests.Security;

public sealed class PhotoSingleSubmitTests
{
    [Fact]
    public void RegisterForm_HasSingleSubmitProtection()
    {
        var register = ReadProjectFile(
            "src", "OzelYetenekSinavSistemi.Web", "Views", "Account", "Register.cshtml");

        Assert.Contains("data-oys-single-submit", register, StringComparison.Ordinal);
        Assert.Contains("data-oys-submit-button", register, StringComparison.Ordinal);
        Assert.Contains("data-oys-submit-status", register, StringComparison.Ordinal);
        Assert.Contains("oys-submit-spinner", register, StringComparison.Ordinal);
        Assert.Contains("oys-submit-label", register, StringComparison.Ordinal);
        Assert.Contains("aria-live=\"polite\"", register, StringComparison.Ordinal);
        Assert.DoesNotContain("onclick=", register, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("onchange=", register, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ProfileForm_HasSingleSubmitProtection()
    {
        var profile = ReadProjectFile(
            "src", "OzelYetenekSinavSistemi.Web", "Views", "CandidateProfile", "Index.cshtml");

        Assert.Contains("data-oys-single-submit", profile, StringComparison.Ordinal);
        Assert.Contains("data-oys-submit-button", profile, StringComparison.Ordinal);
        Assert.Contains("data-oys-submit-status", profile, StringComparison.Ordinal);
        Assert.Contains("oys-submit-spinner", profile, StringComparison.Ordinal);
        Assert.DoesNotContain("onclick=", profile, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SiteJs_BindsSingleSubmitForms_WithValidationAndBfcacheReset()
    {
        var siteJs = ReadProjectFile(
            "src", "OzelYetenekSinavSistemi.Web", "wwwroot", "js", "site.js");
        var siteCss = ReadProjectFile(
            "src", "OzelYetenekSinavSistemi.Web", "wwwroot", "css", "site-security.css");

        Assert.Contains("function oysBindSingleSubmitForms", siteJs, StringComparison.Ordinal);
        Assert.Contains("form[data-oys-single-submit]", siteJs, StringComparison.Ordinal);
        Assert.Contains("function oysIsFormClientValid", siteJs, StringComparison.Ordinal);
        Assert.Contains("function oysLockSingleSubmitForm", siteJs, StringComparison.Ordinal);
        Assert.Contains("function oysResetSingleSubmitForm", siteJs, StringComparison.Ordinal);
        Assert.Contains("function oysEnsureSubmitOverlay", siteJs, StringComparison.Ordinal);
        Assert.Contains("function oysGetSubmitButtons", siteJs, StringComparison.Ordinal);
        Assert.Contains("Yükleniyor, lütfen bekleyiniz…", siteJs, StringComparison.Ordinal);
        Assert.Contains("Fotoğraf yükleniyor, lütfen bekleyiniz…", siteJs, StringComparison.Ordinal);
        Assert.Contains("aria-busy", siteJs, StringComparison.Ordinal);
        Assert.Contains("oys-is-submitting", siteJs, StringComparison.Ordinal);
        Assert.Contains("data-oys-submit-overlay", siteJs, StringComparison.Ordinal);
        Assert.Contains("stopImmediatePropagation", siteJs, StringComparison.Ordinal);
        Assert.Contains("addEventListener(\"pageshow\"", siteJs, StringComparison.Ordinal);
        Assert.Contains("event.persisted", siteJs, StringComparison.Ordinal);
        Assert.Contains("oysBindSingleSubmitForms()", siteJs, StringComparison.Ordinal);

        var singleSubmitBlock = GetSingleSubmitBlock(siteJs);
        Assert.DoesNotContain("setTimeout(", singleSubmitBlock, StringComparison.Ordinal);
        Assert.DoesNotContain("fieldset", singleSubmitBlock, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("oysWasEnabled", singleSubmitBlock, StringComparison.Ordinal);
        Assert.DoesNotContain("input:not([type=hidden])", singleSubmitBlock, StringComparison.Ordinal);
        Assert.Contains("submitButtons.forEach", singleSubmitBlock, StringComparison.Ordinal);
        Assert.Contains("button[type='submit'], input[type='submit']", singleSubmitBlock, StringComparison.Ordinal);

        Assert.Contains(".oys-submit-overlay", siteCss, StringComparison.Ordinal);
        Assert.Contains("form.oys-is-submitting", siteCss, StringComparison.Ordinal);
        Assert.Contains(".oys-submit-spinner", siteCss, StringComparison.Ordinal);
        Assert.Contains("@keyframes oys-submit-spin", siteCss, StringComparison.Ordinal);
    }

    [Fact]
    public void SiteJs_SingleSubmit_OnlyBlocksDuplicateSubmit_NotFirstSubmit()
    {
        var siteJs = ReadProjectFile(
            "src", "OzelYetenekSinavSistemi.Web", "wwwroot", "js", "site.js");
        var singleSubmitBlock = GetSingleSubmitBlock(siteJs);

        Assert.Contains("if (form.dataset.oysSubmitting === \"true\")", singleSubmitBlock, StringComparison.Ordinal);
        Assert.Contains("e.preventDefault()", singleSubmitBlock, StringComparison.Ordinal);
        Assert.Contains("oysLockSingleSubmitForm(form)", singleSubmitBlock, StringComparison.Ordinal);
        Assert.DoesNotContain("return false", singleSubmitBlock, StringComparison.Ordinal);
    }

    [Fact]
    public void SiteJs_SingleSubmit_PreservesFormFieldSerialization()
    {
        var siteJs = ReadProjectFile(
            "src", "OzelYetenekSinavSistemi.Web", "wwwroot", "js", "site.js");
        var lockStart = siteJs.IndexOf("function oysLockSingleSubmitForm", StringComparison.Ordinal);
        var lockEnd = siteJs.IndexOf("function oysBindSingleSubmitForms", StringComparison.Ordinal);
        Assert.True(lockStart >= 0 && lockEnd > lockStart);
        var lockBlock = siteJs[lockStart..lockEnd];

        Assert.DoesNotContain("querySelectorAll(\"input", lockBlock, StringComparison.Ordinal);
        Assert.DoesNotContain("fieldset", lockBlock, StringComparison.OrdinalIgnoreCase);
        var withoutSubmitDisable = lockBlock.Replace("submitButton.disabled = true", "");
        Assert.DoesNotContain(".disabled = true", withoutSubmitDisable, StringComparison.Ordinal);
    }

    [Fact]
    public void SiteJs_SingleSubmit_PageshowReset_ClearsOverlayAndBusyState()
    {
        var siteJs = ReadProjectFile(
            "src", "OzelYetenekSinavSistemi.Web", "wwwroot", "js", "site.js");
        var singleSubmitBlock = GetSingleSubmitBlock(siteJs);

        Assert.Contains("form.classList.remove(\"oys-is-submitting\")", singleSubmitBlock, StringComparison.Ordinal);
        Assert.Contains("overlay.hidden = true", singleSubmitBlock, StringComparison.Ordinal);
        Assert.Contains("form.removeAttribute(\"aria-busy\")", singleSubmitBlock, StringComparison.Ordinal);
        Assert.Contains("delete form.dataset.oysSubmitting", singleSubmitBlock, StringComparison.Ordinal);
    }

    [Fact]
    public void SiteJs_SingleSubmit_DoesNotBreakPhotoFeedback()
    {
        var siteJs = ReadProjectFile(
            "src", "OzelYetenekSinavSistemi.Web", "wwwroot", "js", "site.js");
        var singleSubmitBlock = GetSingleSubmitBlock(siteJs);

        Assert.Contains("oysBindPhotoFileFeedback", siteJs, StringComparison.Ordinal);
        Assert.Contains("data-oys-photo-status", siteJs, StringComparison.Ordinal);
        Assert.DoesNotContain("input.value = \"\"", singleSubmitBlock, StringComparison.Ordinal);
        Assert.DoesNotContain("input.value = ''", singleSubmitBlock, StringComparison.Ordinal);
    }

    [Fact]
    public void AuthenticationService_UsesKeyedLockForRegister()
    {
        var source = ReadProjectFile(
            "src", "OzelYetenekSinavSistemi.Application", "Services", "AuthenticationService.cs");

        Assert.Contains("IKeyedAsyncLock", source, StringComparison.Ordinal);
        Assert.Contains("PhotoUploadLockKeys.ForRegister", source, StringComparison.Ordinal);
        Assert.Contains("await using var lockHandle", source, StringComparison.Ordinal);
    }

    [Fact]
    public void UserService_UsesKeyedLockForProfile()
    {
        var source = ReadProjectFile(
            "src", "OzelYetenekSinavSistemi.Application", "Services", "UserService.cs");

        Assert.Contains("IKeyedAsyncLock", source, StringComparison.Ordinal);
        Assert.Contains("PhotoUploadLockKeys.ForProfile", source, StringComparison.Ordinal);
        Assert.Contains("await using var lockHandle", source, StringComparison.Ordinal);
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

    private static string GetSingleSubmitBlock(string siteJs)
    {
        var singleSubmitStart = siteJs.IndexOf("function oysEnsureSubmitOverlay", StringComparison.Ordinal);
        Assert.True(singleSubmitStart >= 0, "oysEnsureSubmitOverlay bulunamadı.");
        var singleSubmitEnd = siteJs.IndexOf("function oysBindPhotoFileFeedback", StringComparison.Ordinal);
        Assert.True(singleSubmitEnd > singleSubmitStart, "oysBindPhotoFileFeedback bulunamadı.");
        return siteJs[singleSubmitStart..singleSubmitEnd];
    }
}
