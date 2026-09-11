using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using OzelYetenekSinavSistemi.Application.Common;
using OzelYetenekSinavSistemi.Application.Interfaces.Repositories;
using OzelYetenekSinavSistemi.Application.Interfaces.Services;
using OzelYetenekSinavSistemi.Application.ViewModels.Account;
using OzelYetenekSinavSistemi.Application.ViewModels.Documents;
using OzelYetenekSinavSistemi.Application.Services;
using OzelYetenekSinavSistemi.Domain.Common;
using OzelYetenekSinavSistemi.Web.Configuration;
using OzelYetenekSinavSistemi.Web.Controllers;
using OzelYetenekSinavSistemi.Web.Infrastructure;
using OzelYetenekSinavSistemi.Tests.Services;

namespace OzelYetenekSinavSistemi.Tests.Security;

public sealed class PublicUrlBuilderTests
{
    [Fact]
    public void BuildPasswordResetBaseUrl_UsesConfiguredHttpsBase()
    {
        var builder = CreateBuilder("https://basvuru.example.edu.tr");
        Assert.Equal("https://basvuru.example.edu.tr/Account/ResetPassword", builder.BuildPasswordResetBaseUrl());
    }

    [Fact]
    public void BuildDocumentVerificationBaseUrl_UsesConfiguredHttpsBase()
    {
        var builder = CreateBuilder("https://basvuru.example.edu.tr/");
        Assert.Equal("https://basvuru.example.edu.tr/DocumentVerification/Verify", builder.BuildDocumentVerificationBaseUrl());
    }

    [Fact]
    public void BuildPath_PreservesBasePath()
    {
        var builder = CreateBuilder("https://basvuru.example.edu.tr/app");
        Assert.Equal("https://basvuru.example.edu.tr/app/Account/ResetPassword", builder.BuildPasswordResetBaseUrl());
    }

    [Theory]
    [InlineData("http://basvuru.example.edu.tr")]
    [InlineData("https://user:pass@basvuru.example.edu.tr")]
    [InlineData("https://basvuru.example.edu.tr?x=1")]
    [InlineData("not-a-url")]
    [InlineData("")]
    public void TryNormalize_RejectsInvalid(string value)
    {
        Assert.False(HostingSecurityOptionsValidator.TryNormalizePublicBaseUrl(value, out _, out var error));
        Assert.False(string.IsNullOrWhiteSpace(error));
    }

    [Fact]
    public void HostMatch_IgnoresPortOnPublicUrl()
    {
        Assert.True(HostingSecurityOptionsValidator.HostMatchesAllowed(
            "basvuru.example.edu.tr",
            new[] { "basvuru.example.edu.tr" }));

        Assert.True(HostingSecurityOptionsValidator.TryNormalizePublicBaseUrl(
            "https://basvuru.example.edu.tr:8443", out var normalized, out _));
        Assert.Contains("8443", normalized, StringComparison.Ordinal);
        Assert.True(Uri.TryCreate(normalized, UriKind.Absolute, out var uri));
        Assert.Equal("basvuru.example.edu.tr", uri.Host);
    }

    private static PublicUrlBuilder CreateBuilder(string baseUrl) =>
        new(Options.Create(new PublicUrlOptions { BaseUrl = baseUrl }));
}

public sealed class HostingSecurityValidationTests
{
    [Fact]
    public void Production_AllowedHostsWildcard_Fails()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            HostingSecurityOptionsValidator.Validate(
                new FakeHostEnvironment(Environments.Production),
                Config(("AllowedHosts", "*")),
                new ReverseProxyOptions { Enabled = false },
                new PublicUrlOptions { BaseUrl = "https://basvuru.example.edu.tr" }));

        Assert.Contains("AllowedHosts", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Password", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Connection", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Production_MissingPublicUrl_Fails()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            HostingSecurityOptionsValidator.Validate(
                new FakeHostEnvironment(Environments.Production),
                Config(("AllowedHosts", "basvuru.example.edu.tr")),
                new ReverseProxyOptions(),
                new PublicUrlOptions { BaseUrl = "" }));

        Assert.Contains("PublicUrl", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Production_HttpBaseUrl_Fails()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            HostingSecurityOptionsValidator.Validate(
                new FakeHostEnvironment(Environments.Production),
                Config(("AllowedHosts", "basvuru.example.edu.tr")),
                new ReverseProxyOptions(),
                new PublicUrlOptions { BaseUrl = "http://basvuru.example.edu.tr" }));

        Assert.Contains("https", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Production_PublicHostNotInAllowedHosts_Fails()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            HostingSecurityOptionsValidator.Validate(
                new FakeHostEnvironment(Environments.Production),
                Config(("AllowedHosts", "other.example.edu.tr")),
                new ReverseProxyOptions(),
                new PublicUrlOptions { BaseUrl = "https://basvuru.example.edu.tr" }));

        Assert.Contains("AllowedHosts", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Production_ProxyEnabledWithoutTrusted_Fails()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            HostingSecurityOptionsValidator.Validate(
                new FakeHostEnvironment(Environments.Production),
                Config(("AllowedHosts", "basvuru.example.edu.tr")),
                new ReverseProxyOptions { Enabled = true },
                new PublicUrlOptions { BaseUrl = "https://basvuru.example.edu.tr" }));

        Assert.Contains("KnownProxies", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void InvalidProxyIp_Fails()
    {
        var errors = new List<string>();
        HostingSecurityOptionsValidator.ParseKnownProxies(
            new ReverseProxyOptions { KnownProxies = new[] { "not-an-ip" } }, errors);
        Assert.Contains(errors, e => e.Contains("KnownProxies", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void InvalidCidr_Fails()
    {
        var errors = new List<string>();
        HostingSecurityOptionsValidator.ParseKnownNetworks(
            new ReverseProxyOptions { KnownNetworks = new[] { "10.0.0.0" } }, errors);
        Assert.Contains(errors, e => e.Contains("KnownNetworks", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void UnrestrictedCidr_Fails()
    {
        var errors = new List<string>();
        HostingSecurityOptionsValidator.ParseKnownNetworks(
            new ReverseProxyOptions { KnownNetworks = new[] { "0.0.0.0/0" } }, errors);
        Assert.Contains(errors, e => e.Contains("tüm interneti", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Development_LocalhostHttps_Accepted()
    {
        HostingSecurityOptionsValidator.Validate(
            new FakeHostEnvironment(Environments.Development),
            Config(("AllowedHosts", "localhost;127.0.0.1")),
            new ReverseProxyOptions { Enabled = false },
            new PublicUrlOptions { BaseUrl = "https://localhost:7102" });
    }

    private static IConfiguration Config(params (string Key, string? Value)[] pairs) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(pairs.ToDictionary(p => p.Key, p => p.Value))
            .Build();

    private sealed class FakeHostEnvironment : IHostEnvironment
    {
        public FakeHostEnvironment(string environmentName) => EnvironmentName = environmentName;
        public string EnvironmentName { get; set; }
        public string ApplicationName { get; set; } = "Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}

public sealed class ClientIpAddressAccessorTests
{
    [Fact]
    public void PartitionKey_NullIp_UsesUnknown()
    {
        var accessor = new ClientIpAddressAccessor();
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = null;
        Assert.Equal(ClientIpAddressAccessor.UnknownPartitionKey, accessor.GetPartitionKey(context));
    }

    [Fact]
    public void Normalize_MapsIpv4MappedIpv6()
    {
        var mapped = IPAddress.Parse("::ffff:203.0.113.10");
        var normalized = ClientIpAddressAccessor.Normalize(mapped);
        Assert.Equal(IPAddress.Parse("203.0.113.10"), normalized);
    }

    [Fact]
    public void PartitionKey_SameIp_SameKey()
    {
        var accessor = new ClientIpAddressAccessor();
        var a = new DefaultHttpContext();
        a.Connection.RemoteIpAddress = IPAddress.Parse("203.0.113.5");
        var b = new DefaultHttpContext();
        b.Connection.RemoteIpAddress = IPAddress.Parse("203.0.113.5");
        Assert.Equal(accessor.GetPartitionKey(a), accessor.GetPartitionKey(b));
    }

    [Fact]
    public void PartitionKey_DifferentIps_Differ()
    {
        var accessor = new ClientIpAddressAccessor();
        var a = new DefaultHttpContext();
        a.Connection.RemoteIpAddress = IPAddress.Parse("203.0.113.5");
        var b = new DefaultHttpContext();
        b.Connection.RemoteIpAddress = IPAddress.Parse("198.51.100.7");
        Assert.NotEqual(accessor.GetPartitionKey(a), accessor.GetPartitionKey(b));
    }

    [Fact]
    public void Accessor_DoesNotParseXForwardedForItself()
    {
        var accessor = new ClientIpAddressAccessor();
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse("10.0.0.1");
        context.Request.Headers["X-Forwarded-For"] = "203.0.113.9";
        Assert.Equal("10.0.0.1", accessor.GetClientIpString(context));
    }
}

public sealed class ForwardedHeadersTrustTests
{
    [Fact]
    public async Task ProxyDisabled_ForgedXff_DoesNotChangeRemoteIp()
    {
        // Middleware yok: RemoteIp değişmez (Program Enabled=false senaryosu).
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse("198.51.100.20");
        context.Request.Headers["X-Forwarded-For"] = "203.0.113.9";

        var accessor = new ClientIpAddressAccessor();
        Assert.Equal("198.51.100.20", accessor.GetPartitionKey(context));
        await Task.CompletedTask;
    }

    [Fact]
    public async Task UntrustedSource_IgnoresXForwardedFor()
    {
        var context = await RunForwardedAsync(
            remoteIp: "198.51.100.20",
            knownProxy: "10.10.0.5",
            xff: "203.0.113.9",
            forwardLimit: 1);

        Assert.Equal(IPAddress.Parse("198.51.100.20"), context.Connection.RemoteIpAddress);
        Assert.Equal("198.51.100.20", new ClientIpAddressAccessor().GetPartitionKey(context));
    }

    [Fact]
    public async Task TrustedProxy_AppliesXForwardedFor()
    {
        var context = await RunForwardedAsync(
            remoteIp: "10.10.0.5",
            knownProxy: "10.10.0.5",
            xff: "203.0.113.9",
            forwardLimit: 1,
            xfp: "https");

        Assert.Equal(IPAddress.Parse("203.0.113.9"), context.Connection.RemoteIpAddress);
        Assert.Equal("203.0.113.9", new ClientIpAddressAccessor().GetPartitionKey(context));
    }

    [Fact]
    public async Task ForwardLimitOne_OnlyProcessesAllowedHop()
    {
        var context = await RunForwardedAsync(
            remoteIp: "10.10.0.5",
            knownProxy: "10.10.0.5",
            xff: "203.0.113.9, 198.51.100.7",
            forwardLimit: 1,
            xfp: "https, https");

        // ForwardLimit=1: zincirdeki en sağdaki (en yakın) hop işlenir.
        Assert.Equal(IPAddress.Parse("198.51.100.7"), context.Connection.RemoteIpAddress);
    }

    [Fact]
    public async Task TrustedProxy_AppliesXForwardedProto()
    {
        var context = await RunForwardedAsync(
            remoteIp: "10.10.0.5",
            knownProxy: "10.10.0.5",
            xff: "203.0.113.9",
            forwardLimit: 1,
            xfp: "https");

        Assert.Equal("https", context.Request.Scheme);
    }

    [Fact]
    public async Task UntrustedSource_DoesNotApplyXForwardedProto()
    {
        var context = await RunForwardedAsync(
            remoteIp: "198.51.100.20",
            knownProxy: "10.10.0.5",
            xff: "203.0.113.9",
            forwardLimit: 1,
            xfp: "https",
            initialScheme: "http");

        Assert.Equal("http", context.Request.Scheme);
    }

    private static async Task<DefaultHttpContext> RunForwardedAsync(
        string remoteIp,
        string knownProxy,
        string xff,
        int forwardLimit,
        string? xfp = null,
        string initialScheme = "http")
    {
        var options = new Microsoft.AspNetCore.Builder.ForwardedHeadersOptions
        {
            ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
            ForwardLimit = forwardLimit,
            RequireHeaderSymmetry = true
        };
        options.KnownProxies.Clear();
        options.KnownNetworks.Clear();
        options.KnownProxies.Add(IPAddress.Parse(knownProxy));

        var middleware = new ForwardedHeadersMiddleware(
            _ => Task.CompletedTask,
            NullLoggerFactory.Instance,
            Options.Create(options));

        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse(remoteIp);
        context.Request.Scheme = initialScheme;
        context.Request.Headers["X-Forwarded-For"] = xff;
        if (xfp is not null)
            context.Request.Headers["X-Forwarded-Proto"] = xfp;

        await middleware.Invoke(context);
        return context;
    }
}

public sealed class AbsoluteUrlSecurityControllerTests
{
    [Fact]
    public async Task ForgotPassword_IgnoresHostileHostAndUsesPublicUrl()
    {
        var reset = new Mock<IPasswordResetService>();
        string? capturedBase = null;
        reset.Setup(s => s.RequestResetAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, string?, string?, CancellationToken>((_, url, _, _, _) => capturedBase = url)
            .ReturnsAsync(OperationResult.Ok());

        var controller = CreateAccountController(reset.Object);
        controller.HttpContext.Request.Scheme = "http";
        controller.HttpContext.Request.Host = new HostString("evil.example");
        controller.HttpContext.Request.Headers["X-Forwarded-Host"] = "evil.example";
        controller.HttpContext.Request.Headers["X-Forwarded-Proto"] = "http";

        // RedirectToAction UrlHelper gerektirmez; yalnızca reset URL'sini doğrula.
        try
        {
            await controller.ForgotPassword(new ForgotPasswordViewModel { TcNoOrEmail = "10000000146" }, CancellationToken.None);
        }
        catch (InvalidOperationException)
        {
            // Unit test DI'sında IUrlHelperFactory yok; reset çağrısı öncesinde URL üretilmiş olmalı.
        }

        Assert.Equal("https://basvuru.example.edu.tr/Account/ResetPassword", capturedBase);
        Assert.DoesNotContain("evil.example", capturedBase!, StringComparison.OrdinalIgnoreCase);
        Assert.StartsWith("https://", capturedBase!, StringComparison.OrdinalIgnoreCase);
        reset.Verify(s => s.RequestResetAsync(
            "10000000146",
            "https://basvuru.example.edu.tr/Account/ResetPassword",
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task EntranceDocument_UsesPublicUrlForVerification()
    {
        string? captured = null;
        var documents = new Mock<IDocumentService>();
        documents.Setup(d => d.GetEntranceDocumentAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<Guid, Guid, string, string, CancellationToken>((_, _, _, url, _) => captured = url)
            .ReturnsAsync(OperationResult<ExamEntranceDocumentViewModel>.Fail("x"));

        var controller = CreateExamDocumentController(documents.Object);
        controller.HttpContext.Request.Host = new HostString("evil.example");
        controller.HttpContext.Request.Scheme = "http";

        await controller.View(Guid.NewGuid(), CancellationToken.None);

        Assert.Equal("https://basvuru.example.edu.tr/DocumentVerification/Verify", captured);
    }

    [Fact]
    public async Task ResultDocument_UsesPublicUrlForVerification()
    {
        string? captured = null;
        var documents = new Mock<IDocumentService>();
        documents.Setup(d => d.GetResultDocumentAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<Guid, Guid, string, string, CancellationToken>((_, _, _, url, _) => captured = url)
            .ReturnsAsync(OperationResult<CandidateResultDocumentViewModel>.Fail("x"));

        var controller = CreateExamDocumentController(documents.Object);
        controller.HttpContext.Request.Host = new HostString("evil.example");
        controller.HttpContext.Request.Scheme = "http";

        await controller.Result(Guid.NewGuid(), CancellationToken.None);

        Assert.Equal("https://basvuru.example.edu.tr/DocumentVerification/Verify", captured);
    }

    [Fact]
    public void PasswordResetService_UrlEncodesToken()
    {
        var source = File.ReadAllText(FindUnder("src", "OzelYetenekSinavSistemi.Application", "Services", "PasswordResetService.cs"));
        Assert.Contains("Uri.EscapeDataString(plainToken)", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Request.Host", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Request.Scheme", source, StringComparison.Ordinal);
    }

    [Fact]
    public void DocumentService_UrlEncodesVerificationCode()
    {
        var source = File.ReadAllText(FindUnder("src", "OzelYetenekSinavSistemi.Application", "Services", "DocumentService.cs"));
        Assert.Contains("Uri.EscapeDataString(verificationCode)", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Program_DoesNotEnableXForwardedHost_AndClearsDefaultTrust()
    {
        var program = File.ReadAllText(FindUnder("src", "OzelYetenekSinavSistemi.Web", "Program.cs"));
        Assert.Contains("XForwardedFor", program, StringComparison.Ordinal);
        Assert.Contains("XForwardedProto", program, StringComparison.Ordinal);
        Assert.DoesNotContain("XForwardedHost", program, StringComparison.Ordinal);
        Assert.Contains("UseForwardedHeaders", program, StringComparison.Ordinal);
        Assert.Contains("KnownProxies.Clear()", program, StringComparison.Ordinal);
        Assert.Contains("KnownNetworks.Clear()", program, StringComparison.Ordinal);
        Assert.Contains("GetClientIpPartitionKey()", program, StringComparison.Ordinal);

        var useForwarded = program.IndexOf("UseForwardedHeaders", StringComparison.Ordinal);
        var useHsts = program.IndexOf("UseHsts", StringComparison.Ordinal);
        var useHttps = program.IndexOf("UseHttpsRedirection", StringComparison.Ordinal);
        Assert.True(useForwarded > 0 && useHsts > useForwarded && useHttps > useForwarded);
    }

    [Fact]
    public void SerilogEnrichment_UsesAccessor_NotRawHeaders()
    {
        var source = File.ReadAllText(FindUnder("src", "OzelYetenekSinavSistemi.Web", "Infrastructure", "SerilogEnrichmentMiddleware.cs"));
        Assert.Contains("IClientIpAddressAccessor", source, StringComparison.Ordinal);
        Assert.DoesNotContain("X-Forwarded-For", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Headers[", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Appsettings_HasSecureDefaults()
    {
        var json = File.ReadAllText(FindUnder("src", "OzelYetenekSinavSistemi.Web", "appsettings.json"));
        Assert.Contains("\"AllowedHosts\": \"localhost;127.0.0.1\"", json, StringComparison.Ordinal);
        Assert.DoesNotContain("\"AllowedHosts\": \"*\"", json, StringComparison.Ordinal);
        Assert.Contains("\"PublicUrl\"", json, StringComparison.Ordinal);
        Assert.Contains("\"ReverseProxy\"", json, StringComparison.Ordinal);
        Assert.Contains("\"Enabled\": false", json, StringComparison.Ordinal);
        Assert.Contains("https://localhost:7102", json, StringComparison.Ordinal);
    }

    private static AccountController CreateAccountController(IPasswordResetService reset)
    {
        var publicUrl = new PublicUrlBuilder(Options.Create(new PublicUrlOptions
        {
            BaseUrl = "https://basvuru.example.edu.tr"
        }));

        var http = new DefaultHttpContext
        {
            RequestServices = new ServiceCollection()
                .AddSingleton<IClientIpAddressAccessor, ClientIpAddressAccessor>()
                .BuildServiceProvider()
        };

        var controller = new AccountController(
            Mock.Of<IAuthenticationService>(),
            Mock.Of<IUserService>(),
            reset,
            Mock.Of<ICaptchaService>(),
            Mock.Of<IYgsYearRepository>(),
            publicUrl,
            new CountryCatalog(),
            new IdentityDocumentValidator(TimeProvider.System),
            PhotoUploadTestSupport.CreateAcceptingPhotoService())
        {
            ControllerContext = new ControllerContext { HttpContext = http },
            TempData = new TempDataDictionary(http, Mock.Of<ITempDataProvider>())
        };
        return controller;
    }

    private static ExamDocumentController CreateExamDocumentController(IDocumentService documents)
    {
        var publicUrl = new PublicUrlBuilder(Options.Create(new PublicUrlOptions
        {
            BaseUrl = "https://basvuru.example.edu.tr"
        }));

        var identity = new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
            new Claim(ClaimTypes.Role, DomainConstants.RoleNames.Candidate)
        }, "Test");

        var http = new DefaultHttpContext { User = new ClaimsPrincipal(identity) };

        return new ExamDocumentController(documents, publicUrl)
        {
            ControllerContext = new ControllerContext { HttpContext = http },
            TempData = new TempDataDictionary(http, Mock.Of<ITempDataProvider>())
        };
    }

    private static string FindUnder(params string[] parts)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var path = Path.Combine(new[] { dir.FullName }.Concat(parts).ToArray());
            if (File.Exists(path))
                return path;
            dir = dir.Parent;
        }

        throw new FileNotFoundException(string.Join(Path.DirectorySeparatorChar, parts));
    }
}
