using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.Extensions.DependencyInjection;
using OzelYetenekSinavSistemi.Application.Validation;
using OzelYetenekSinavSistemi.Application.ViewModels.Account;
using OzelYetenekSinavSistemi.Web.Infrastructure;

namespace OzelYetenekSinavSistemi.Tests.Validation;

public sealed class ClientValidationTests
{
    [Fact]
    public void HumanNameClientValidator_EmitsDataValHumanName()
    {
        var attributes = RenderClientAttributes<RegisterViewModel>(nameof(RegisterViewModel.FirstName));
        Assert.True(attributes.ContainsKey("data-val"));
        Assert.True(attributes.ContainsKey("data-val-humanname"));
        Assert.Contains("harf", attributes["data-val-humanname"], StringComparison.Ordinal);
    }

    [Fact]
    public void MeaningfulTitleClientValidator_EmitsDataValMeaningfulTitle()
    {
        var attributes = RenderClientAttributes<Application.ViewModels.ExamPeriods.ExamPeriodFormViewModel>(
            nameof(Application.ViewModels.ExamPeriods.ExamPeriodFormViewModel.Title));
        Assert.True(attributes.ContainsKey("data-val-meaningfultitle"));
    }

    [Fact]
    public void MeaningfulTextClientValidator_EmitsOptionalAndMultilineParams()
    {
        var attributes = RenderClientAttributes<RegisterViewModel>(nameof(RegisterViewModel.Address));
        Assert.True(attributes.ContainsKey("data-val-meaningfultext"));
        Assert.Equal("true", attributes["data-val-meaningfultext-optional"]);
        Assert.Equal("true", attributes["data-val-meaningfultext-multiline"]);
    }

    [Fact]
    public void RegisterView_IncludesValidationScriptsAndInputTextJs()
    {
        var register = ReadProjectFile("Views", "Account", "Register.cshtml");
        var partial = ReadProjectFile("Views", "Shared", "_ValidationScriptsPartial.cshtml");
        var authLayout = ReadProjectFile("Views", "Shared", "_AuthLayout.cshtml");

        Assert.Contains("_ValidationScriptsPartial", register, StringComparison.Ordinal);
        Assert.Contains("jquery.validate", partial, StringComparison.Ordinal);
        Assert.Contains("input-text-validation.js", authLayout, StringComparison.Ordinal);
        Assert.Contains("asp-validation-for=\"FirstName\"", register, StringComparison.Ordinal);
    }

    [Fact]
    public void SiteJs_RegistersInputTextUnobtrusiveAdapters()
    {
        var siteJs = ReadProjectFile("wwwroot", "js", "site.js");
        Assert.Contains("oysBindInputTextValidation", siteJs, StringComparison.Ordinal);
        Assert.Contains("humanname", siteJs, StringComparison.Ordinal);
        Assert.Contains("meaningfultitle", siteJs, StringComparison.Ordinal);
        Assert.Contains("meaningfultext", siteJs, StringComparison.Ordinal);
    }

    [Fact]
    public void InputTextValidationJs_MirrorsServerRuleNames()
    {
        var js = ReadProjectFile("wwwroot", "js", "input-text-validation.js");
        Assert.Contains("isValidHumanName", js, StringComparison.Ordinal);
        Assert.Contains("isValidMeaningfulTitle", js, StringComparison.Ordinal);
        Assert.Contains("isValidOptionalMeaningfulText", js, StringComparison.Ordinal);
        Assert.Contains("\\u202E", js, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("--------", false)]
    [InlineData("Jean-Pierre", true)]
    [InlineData("Ömer Faruk", true)]
    [InlineData("O\u2019Connor", true)]
    public void HumanName_ParityCases_MatchServerRules(string value, bool expected) =>
        Assert.Equal(expected, InputTextRules.IsValidHumanName(value, out _));

    [Fact]
    public void PreferenceOptionFormViewModel_EmitsMeaningfulTitleClientMetadata()
    {
        var attributes = RenderClientAttributes<Application.ViewModels.ExamPeriods.PreferenceOptionFormViewModel>(
            nameof(Application.ViewModels.ExamPeriods.PreferenceOptionFormViewModel.PreferenceName));
        Assert.True(attributes.ContainsKey("data-val-meaningfultitle"));
        Assert.True(attributes.ContainsKey("data-val"));
    }

    [Fact]
    public void PreferenceManageView_UsesAspForAndValidationScripts()
    {
        var manage = ReadProjectFile("Views", "ExamPreferenceOption", "Manage.cshtml");
        var addForm = ReadProjectFile("Views", "ExamPreferenceOption", "_PreferenceOptionAddForm.cshtml");
        var edit = ReadProjectFile("Views", "ExamPreferenceOption", "Edit.cshtml");

        Assert.Contains("_ValidationScriptsPartial", manage, StringComparison.Ordinal);
        Assert.Contains("_PreferenceOptionAddForm", manage, StringComparison.Ordinal);
        Assert.Contains("js-datatable", manage, StringComparison.Ordinal);
        Assert.Contains("Yeni Tercih Seçeneği Ekle", manage, StringComparison.Ordinal);
        Assert.Contains("Tüm Tercih Seçenekleri", manage, StringComparison.Ordinal);
        Assert.Contains("asp-for=\"PreferenceName\"", addForm, StringComparison.Ordinal);
        Assert.Contains("asp-for=\"ExamPeriodId\"", addForm, StringComparison.Ordinal);
        Assert.Contains("asp-for=\"PreferenceName\"", edit, StringComparison.Ordinal);
        Assert.DoesNotContain("asp-for=\"AddForm.", addForm, StringComparison.Ordinal);
    }

    [Fact]
    public void SystemSettingView_UsesAspForRangeValidation()
    {
        var index = ReadProjectFile("Views", "SystemSetting", "Index.cshtml");
        var row = ReadProjectFile("Views", "SystemSetting", "_SystemSettingRow.cshtml");

        Assert.Contains("_ValidationScriptsPartial", index, StringComparison.Ordinal);
        Assert.Contains("asp-for=\"SettingValue\"", row, StringComparison.Ordinal);
        Assert.DoesNotContain("name=\"settingValue\"", row, StringComparison.OrdinalIgnoreCase);
    }

    private static IDictionary<string, string> RenderClientAttributes<TModel>(string propertyName)
    {
        var services = new ServiceCollection();
        services.AddMvcCore().AddDataAnnotations();
        services.AddSingleton<IClientModelValidatorProvider, InputTextClientModelValidatorProvider>();
        using var sp = services.BuildServiceProvider();

        var metadataProvider = sp.GetRequiredService<IModelMetadataProvider>();
        var metadata = metadataProvider.GetMetadataForProperty(typeof(TModel), propertyName);
        var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var clientContext = new ClientModelValidationContext(
            new ActionContext(), metadata, metadataProvider, attributes);

        var providerContext = new ClientValidatorProviderContext(metadata, new List<ClientValidatorItem>());
        foreach (var provider in sp.GetServices<IClientModelValidatorProvider>())
            provider.CreateValidators(providerContext);

        foreach (var item in providerContext.Results)
            item.Validator?.AddValidation(clientContext);

        return attributes;
    }

    private static string ReadProjectFile(params string[] parts)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(new[] { dir.FullName, "src", "OzelYetenekSinavSistemi.Web" }.Concat(parts).ToArray());
            if (File.Exists(candidate))
                return File.ReadAllText(candidate);
            dir = dir.Parent;
        }

        throw new FileNotFoundException(string.Join('/', parts));
    }
}
