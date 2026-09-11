using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Moq;
using OzelYetenekSinavSistemi.Application.DTOs.ExamPeriods;
using OzelYetenekSinavSistemi.Application.Interfaces.Repositories;
using OzelYetenekSinavSistemi.Application.Interfaces.Services;
using OzelYetenekSinavSistemi.Application.Services;
using OzelYetenekSinavSistemi.Infrastructure.Persistence.Repositories;
using OzelYetenekSinavSistemi.Web.Controllers;

namespace OzelYetenekSinavSistemi.Tests.Services;

public sealed class PreferenceOptionDataTablesTests
{
    [Fact]
    public void DataAction_IsGetOnly_AndControllerRequiresSuperAdmin()
    {
        var authorize = typeof(ExamPreferenceOptionController)
            .GetCustomAttribute<AuthorizeAttribute>();
        Assert.Equal("SuperAdminOnly", authorize!.Policy);

        var data = typeof(ExamPreferenceOptionController).GetMethod(nameof(ExamPreferenceOptionController.Data));
        Assert.NotNull(data);
        Assert.NotNull(data!.GetCustomAttribute<HttpGetAttribute>());
        Assert.Null(data.GetCustomAttribute<HttpPostAttribute>());
        Assert.Null(data.GetCustomAttribute<ValidateAntiForgeryTokenAttribute>());
    }

    [Fact]
    public void Repository_SearchUsesOffsetFetchAndParameterizedLike_WithoutStringConcatSearch()
    {
        var source = File.ReadAllText(FindUnder(
            "src", "OzelYetenekSinavSistemi.Infrastructure", "Persistence",
            "Repositories", "ExamPreferenceOptionRepository.cs"));

        Assert.Contains("SearchForDataTablesAsync", source, StringComparison.Ordinal);
        Assert.Contains("OFFSET @Start ROWS FETCH NEXT @Length ROWS ONLY", source, StringComparison.Ordinal);
        Assert.Contains("PreferenceName LIKE @SearchPattern", source, StringComparison.Ordinal);
        Assert.Contains("EscapeLikePattern", source, StringComparison.Ordinal);
        Assert.Contains("NormalizePageLength", source, StringComparison.Ordinal);
        Assert.Contains("ResolveOrderByClause", source, StringComparison.Ordinal);
        Assert.Contains("COUNT_BIG(1)", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ToList().Skip", source, StringComparison.Ordinal);
        Assert.DoesNotContain("AsEnumerable()", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ORDER BY \" + search", source, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(10, 10)]
    [InlineData(25, 25)]
    [InlineData(50, 50)]
    [InlineData(100, 100)]
    [InlineData(0, 10)]
    [InlineData(7, 10)]
    [InlineData(1000, 10)]
    [InlineData(-5, 10)]
    public void NormalizePageLength_AllowsOnlyConfiguredSizes(int input, int expected)
    {
        var method = typeof(ExamPreferenceOptionRepository)
            .GetMethod("NormalizePageLength", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);
        var actual = (int)method!.Invoke(null, new object[] { input })!;
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData(0, "asc", "DisplayOrder ASC, Id ASC")]
    [InlineData(1, "desc", "PreferenceName DESC, Id ASC")]
    [InlineData(2, "ASC", "IsActive ASC, Id ASC")]
    [InlineData(99, "weird", "DisplayOrder ASC, Id ASC")]
    [InlineData(3, "desc", "DisplayOrder DESC, Id ASC")]
    public void ResolveOrderByClause_UsesWhitelist(int column, string dir, string expected)
    {
        var method = typeof(ExamPreferenceOptionRepository)
            .GetMethod("ResolveOrderByClause", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);
        var actual = (string)method!.Invoke(null, new object[] { column, dir })!;
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void EscapeLikePattern_EscapesWildcards()
    {
        var method = typeof(ExamPreferenceOptionRepository)
            .GetMethod("EscapeLikePattern", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);
        var actual = (string)method!.Invoke(null, new object[] { @"a%b_c\d[e" })!;
        Assert.Equal(@"a\%b\_c\\d\[e", actual);
    }

    [Fact]
    public async Task Service_SearchMapsPagedResult_AndDoesNotLeakExceptionDetails()
    {
        var options = new Mock<IExamPreferenceOptionRepository>();
        options.Setup(r => r.SearchForDataTablesAsync(It.IsAny<PreferenceOptionDataTablesQuery>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("secret-db-detail"));

        var service = new ExamPeriodService(
            Mock.Of<IExamPeriodRepository>(),
            options.Object,
            Mock.Of<IExamPeriodManagerRepository>(),
            Mock.Of<IUserRepository>(),
            Mock.Of<IAuditService>());

        var response = await service.SearchPreferenceOptionsForDataTablesAsync(new PreferenceOptionDataTablesQuery
        {
            ExamPeriodId = Guid.NewGuid(),
            Draw = 7,
            Start = 0,
            Length = 10
        });

        Assert.Equal(7, response.Draw);
        Assert.Equal(0, response.RecordsTotal);
        Assert.NotNull(response.Error);
        Assert.DoesNotContain("secret-db-detail", response.Error!, StringComparison.Ordinal);
        Assert.Contains("hata", response.Error!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Service_SearchReturnsDrawAndCountsFromRepository()
    {
        var examId = Guid.NewGuid();
        var rowId = Guid.NewGuid();
        var options = new Mock<IExamPreferenceOptionRepository>();
        options.Setup(r => r.SearchForDataTablesAsync(
                It.Is<PreferenceOptionDataTablesQuery>(q =>
                    q.ExamPeriodId == examId && q.Draw == 3 && q.Start == 10 && q.Length == 10),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PreferenceOptionPagedResult
            {
                RecordsTotal = 25,
                RecordsFiltered = 12,
                Rows =
                [
                    new PreferenceOptionTableRowDto
                    {
                        Id = rowId,
                        PreferenceName = "Resim",
                        DisplayOrder = 2,
                        IsActive = true
                    }
                ]
            });

        var service = new ExamPeriodService(
            Mock.Of<IExamPeriodRepository>(),
            options.Object,
            Mock.Of<IExamPeriodManagerRepository>(),
            Mock.Of<IUserRepository>(),
            Mock.Of<IAuditService>());

        var response = await service.SearchPreferenceOptionsForDataTablesAsync(new PreferenceOptionDataTablesQuery
        {
            ExamPeriodId = examId,
            Draw = 3,
            Start = 10,
            Length = 10
        });

        Assert.Equal(3, response.Draw);
        Assert.Equal(25, response.RecordsTotal);
        Assert.Equal(12, response.RecordsFiltered);
        Assert.Single(response.Data);
        Assert.Equal("Resim", response.Data[0].PreferenceName);
        Assert.Null(response.Error);
    }

    [Fact]
    public void SiteJs_ConfiguresServerSideAjaxWithTurkishProcessingAndDebounce()
    {
        var siteJs = File.ReadAllText(FindUnder(
            "src", "OzelYetenekSinavSistemi.Web", "wwwroot", "js", "site.js"));

        Assert.Contains("data-oys-server-side", siteJs, StringComparison.Ordinal);
        Assert.Contains("options.serverSide = true", siteJs, StringComparison.Ordinal);
        Assert.Contains("options.processing = true", siteJs, StringComparison.Ordinal);
        Assert.Contains("searchDelay", siteJs, StringComparison.Ordinal);
        Assert.Contains("processing: \"İşleniyor...\"", siteJs, StringComparison.Ordinal);
        Assert.Contains("zeroRecords: \"Eşleşen kayıt bulunamadı\"", siteJs, StringComparison.Ordinal);
        Assert.Contains("oysBuildPreferenceOptionsServerColumns", siteJs, StringComparison.Ordinal);
        Assert.DoesNotContain("innerHTML", siteJs, StringComparison.Ordinal);
        Assert.DoesNotContain("ajax.reload", siteJs, StringComparison.Ordinal); // mutations redirect; reload via page
    }

    [Fact]
    public void ManagePage_DoesNotEmbedPreferenceRowsInHtmlSkeleton()
    {
        var manage = File.ReadAllText(FindUnder(
            "src", "OzelYetenekSinavSistemi.Web", "Views",
            "ExamPreferenceOption", "Manage.cshtml"));
        var controller = File.ReadAllText(FindUnder(
            "src", "OzelYetenekSinavSistemi.Web", "Controllers",
            "ExamPreferenceOptionController.cs"));

        Assert.Contains("ExistingOptions = Array.Empty", controller, StringComparison.Ordinal);
        Assert.Contains("GetNextPreferenceDisplayOrderAsync", controller, StringComparison.Ordinal);
        Assert.DoesNotContain("GetPreferenceOptionsAsync(examPeriodId", controller, StringComparison.Ordinal);
        Assert.Contains("Url.Action(\"Data\"", manage, StringComparison.Ordinal);
        Assert.DoesNotContain("@option.PreferenceName", manage, StringComparison.Ordinal);
        Assert.DoesNotContain("@option.DisplayOrder", manage, StringComparison.Ordinal);
    }

    private static string FindUnder(params string[] parts)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(new[] { dir.FullName }.Concat(parts).ToArray());
            if (File.Exists(candidate))
                return candidate;
            dir = dir.Parent;
        }

        throw new FileNotFoundException(string.Join(Path.DirectorySeparatorChar, parts));
    }
}
