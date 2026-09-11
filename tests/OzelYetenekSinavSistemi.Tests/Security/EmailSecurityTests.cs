using System.Text;
using System.Text.Encodings.Web;
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
using OzelYetenekSinavSistemi.Application.Services;
using OzelYetenekSinavSistemi.Domain.Entities;
using OzelYetenekSinavSistemi.Infrastructure;
using OzelYetenekSinavSistemi.Infrastructure.Configuration;
using OzelYetenekSinavSistemi.Infrastructure.Services;

namespace OzelYetenekSinavSistemi.Tests.Security;

public sealed class EmailSecurityTests
{
    [Theory]
    [InlineData(false, EmailDeliveryMode.File, typeof(DevFileEmailService))]
    [InlineData(false, EmailDeliveryMode.Smtp, typeof(SmtpEmailService))]
    [InlineData(false, EmailDeliveryMode.Disabled, typeof(DisabledEmailService))]
    [InlineData(true, EmailDeliveryMode.Smtp, typeof(SmtpEmailService))]
    public void RegisterEmailService_SelectsExpectedImplementation(
        bool production, EmailDeliveryMode mode, Type expected)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IHostEnvironment>(new FakeHostEnvironment(
            production ? Environments.Production : Environments.Development));
        services.Configure<EmailOptions>(o =>
        {
            o.DeliveryMode = mode;
            o.FromAddress = "no-reply@example.com";
            o.FromName = "ÖYS";
            o.Host = "smtp.example.edu.tr";
            o.Port = 587;
            o.SecurityMode = EmailSecurityMode.StartTls;
            o.Username = "user";
            o.Password = "secret";
            o.DevPickupDirectory = "App_Data/dev-emails";
        });

        DependencyInjection.RegisterEmailService(
            services,
            new EmailOptions
            {
                DeliveryMode = mode,
                Host = "smtp.example.edu.tr",
                Port = 587,
                SecurityMode = EmailSecurityMode.StartTls,
                Username = "user",
                Password = "secret",
                FromAddress = "no-reply@example.com",
                FromName = "ÖYS",
                DevPickupDirectory = "App_Data/dev-emails"
            },
            new FakeHostEnvironment(production ? Environments.Production : Environments.Development));

        using var sp = services.BuildServiceProvider();
        Assert.IsType(expected, sp.GetRequiredService<IEmailService>());
    }

    [Theory]
    [InlineData(EmailDeliveryMode.File)]
    [InlineData(EmailDeliveryMode.Disabled)]
    public void Production_NonSmtp_FailsValidation(EmailDeliveryMode mode)
    {
        var errors = new List<string>();
        EmailOptionsValidator.ValidateCore(
            new EmailOptions { DeliveryMode = mode, FromAddress = "a@b.com", FromName = "ÖYS" },
            isProduction: true,
            errors);
        Assert.Contains(errors, e => e.Contains("Smtp", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Production_MissingHost_Fails()
    {
        var errors = new List<string>();
        EmailOptionsValidator.ValidateCore(ValidSmtp(o => o.Host = ""), true, errors);
        Assert.Contains(errors, e => e.Contains("Host", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Production_InvalidPort_Fails()
    {
        var errors = new List<string>();
        EmailOptionsValidator.ValidateCore(ValidSmtp(o => o.Port = 0), true, errors);
        Assert.Contains(errors, e => e.Contains("Port", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void UsernameWithoutPassword_Fails()
    {
        var errors = new List<string>();
        EmailOptionsValidator.ValidateCore(ValidSmtp(o =>
        {
            o.Username = "u";
            o.Password = "";
        }), true, errors);
        Assert.Contains(errors, e => e.Contains("Password", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void FromNameWithCrLf_Fails()
    {
        var errors = new List<string>();
        EmailOptionsValidator.ValidateCore(ValidSmtp(o => o.FromName = "A\r\nB"), false, errors);
        Assert.Contains(errors, e => e.Contains("FromName", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Appsettings_DoNotContainSmtpPassword()
    {
        var root = FindUnder("src", "OzelYetenekSinavSistemi.Web");
        var basePath = Path.Combine(root, "appsettings.json");
        var developmentExamplePath = Path.Combine(root, "appsettings.Development.json.example");

        Assert.True(File.Exists(developmentExamplePath), "Tracked development template is required for reproducible tests.");

        foreach (var path in new[] { basePath, developmentExamplePath })
        {
            var json = File.ReadAllText(path);
            Assert.DoesNotContain("\"Password\":", json, StringComparison.Ordinal);
            Assert.Contains("DeliveryMode", json, StringComparison.Ordinal);
        }

        var baseJson = File.ReadAllText(basePath);
        Assert.Contains("\"DeliveryMode\": \"Disabled\"", baseJson, StringComparison.Ordinal);

        var developmentExampleJson = File.ReadAllText(developmentExamplePath);
        Assert.Contains("\"DeliveryMode\": \"File\"", developmentExampleJson, StringComparison.Ordinal);

        var options = new ConfigurationBuilder()
            .AddJsonFile(basePath)
            .Build()
            .GetSection(EmailOptions.SectionName)
            .Get<EmailOptions>();
        Assert.Equal("Özel Yetenek Sınavları Başvuru Sistemi", options!.FromName);
    }

    [Fact]
    public void Template_EncodesHostileFirstNameAndLink()
    {
        var template = new PasswordResetEmailTemplate();
        var link = "https://example.test/reset?x=\"><img src=x onerror=alert(1)>&token=abc";
        var msg = template.Create("</p><script>alert(1)</script>", "user@example.com", link);

        Assert.Equal(PasswordResetEmailTemplate.Subject, msg.Subject);
        Assert.DoesNotContain("<script>", msg.HtmlBody, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(HtmlEncoder.Default.Encode("</p><script>alert(1)</script>"), msg.HtmlBody, StringComparison.Ordinal);
        Assert.Contains(HtmlEncoder.Default.Encode(link), msg.HtmlBody, StringComparison.Ordinal);
        Assert.Contains("&quot;", msg.HtmlBody, StringComparison.Ordinal);
        Assert.Contains("&amp;", msg.HtmlBody, StringComparison.Ordinal);
        Assert.Contains(link, msg.PlainTextBody, StringComparison.Ordinal);
        Assert.Contains("Parola Sıfırlama", msg.Subject, StringComparison.Ordinal);
        Assert.Contains("2 saat", msg.HtmlBody, StringComparison.Ordinal);
        Assert.Contains("2 saat", msg.PlainTextBody, StringComparison.Ordinal);
    }

    [Fact]
    public void Template_EmptyName_UsesGenericGreeting()
    {
        var msg = new PasswordResetEmailTemplate().Create("  ", "a@b.com", "https://localhost/reset");
        Assert.StartsWith("<p>Merhaba,</p>", msg.HtmlBody, StringComparison.Ordinal);
        Assert.StartsWith("Merhaba,", msg.PlainTextBody, StringComparison.Ordinal);
    }

    [Fact]
    public void Template_Source_DoesNotUseHtmlRaw()
    {
        var source = File.ReadAllText(FindUnder(
            "src", "OzelYetenekSinavSistemi.Application", "Services", "PasswordResetEmailTemplate.cs"));
        Assert.Contains("HtmlEncoder.Default", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Html.Raw", source, StringComparison.Ordinal);
    }

    [Fact]
    public void HeaderGuard_RejectsCrLfInSubjectAndRecipient()
    {
        Assert.Throws<ArgumentException>(() => EmailHeaderGuard.ValidateMessage(new EmailMessage
        {
            To = "a@b.com",
            Subject = "Hi\r\nBcc: evil@x.com",
            HtmlBody = "<p>x</p>",
            PlainTextBody = "x"
        }));

        Assert.Throws<ArgumentException>(() => EmailHeaderGuard.ValidateMessage(new EmailMessage
        {
            To = "a@b.com\nbad@c.com",
            Subject = "Hi",
            HtmlBody = "<p>x</p>",
            PlainTextBody = "x"
        }));
    }

    [Fact]
    public async Task RequestReset_SmtpFailure_ReturnsOk_InvalidatesToken_AuditsSafely()
    {
        var userId = Guid.NewGuid();
        var tokenId = Guid.NewGuid();
        var userRepo = new Mock<IUserRepository>();
        var tokenRepo = new Mock<IPasswordResetTokenRepository>();
        var email = new Mock<IEmailService>();
        var audit = new Mock<IAuditService>();

        userRepo.Setup(r => r.GetByTcNoOrEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User
            {
                Id = userId,
                IsActive = true,
                Email = "user@example.com",
                FirstName = "Ali",
                TcNo = "11111111110"
            });
        tokenRepo.Setup(r => r.AddAsync(It.IsAny<PasswordResetToken>(), It.IsAny<CancellationToken>()))
            .Callback<PasswordResetToken, CancellationToken>((t, _) => t.Id = tokenId)
            .ReturnsAsync(tokenId);
        email.Setup(e => e.SendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("SMTP AUTH failed for user@example.com token=SECRET"));

        var sut = new PasswordResetService(
            userRepo.Object,
            tokenRepo.Object,
            Mock.Of<IPasswordResetTransactionService>(),
            Mock.Of<IPasswordService>(),
            email.Object,
            new PasswordResetEmailTemplate(),
            audit.Object,
            NullLogger<PasswordResetService>.Instance);

        var result = await sut.RequestResetAsync(
            "11111111110",
            "https://basvuru.example.edu.tr/Account/ResetPassword",
            "127.0.0.1",
            "corr-1");

        Assert.True(result.Success);
        tokenRepo.Verify(r => r.MarkAsUsedAsync(tokenId, It.IsAny<CancellationToken>()), Times.Once);
        audit.Verify(a => a.LogAsync(
            "EmailDeliveryFailed",
            "PasswordResetSmtpSendFailed",
            userId,
            "127.0.0.1",
            null,
            "corr-1",
            It.IsAny<CancellationToken>()), Times.Once);
        audit.Verify(a => a.LogAsync(
            "PasswordResetRequested",
            It.IsAny<string>(),
            It.IsAny<Guid?>(),
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            It.IsAny<CancellationToken>()), Times.Never);

        // Exception / e-posta / TC / token audit description'a yazılmamalı
        audit.Verify(a => a.LogAsync(
            It.IsAny<string>(),
            It.Is<string>(d => d.Contains("SECRET", StringComparison.Ordinal)
                               || d.Contains("user@example.com", StringComparison.Ordinal)
                               || d.Contains("11111111110", StringComparison.Ordinal)
                               || d.Contains("failed for", StringComparison.OrdinalIgnoreCase)),
            It.IsAny<Guid?>(),
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RequestReset_Success_WritesPasswordResetRequested_AfterSend()
    {
        var userId = Guid.NewGuid();
        var userRepo = new Mock<IUserRepository>();
        var tokenRepo = new Mock<IPasswordResetTokenRepository>();
        var email = new Mock<IEmailService>();
        var audit = new Mock<IAuditService>();
        var sequence = new List<string>();

        userRepo.Setup(r => r.GetByTcNoOrEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = userId, IsActive = true, Email = "a@b.com", FirstName = "Ayşe" });
        tokenRepo.Setup(r => r.AddAsync(It.IsAny<PasswordResetToken>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Guid.NewGuid());
        email.Setup(e => e.SendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()))
            .Callback(() => sequence.Add("send"))
            .Returns(Task.CompletedTask);
        audit.Setup(a => a.LogAsync("PasswordResetRequested", It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Callback(() => sequence.Add("audit"))
            .Returns(Task.CompletedTask);

        var sut = new PasswordResetService(
            userRepo.Object, tokenRepo.Object, Mock.Of<IPasswordResetTransactionService>(),
            Mock.Of<IPasswordService>(), email.Object, new PasswordResetEmailTemplate(),
            audit.Object, NullLogger<PasswordResetService>.Instance);

        await sut.RequestResetAsync("a@b.com", "https://localhost/Account/ResetPassword", null, "c1");

        Assert.Equal(new[] { "send", "audit" }, sequence);
        email.Verify(e => e.SendAsync(
            It.Is<EmailMessage>(m =>
                m.Subject == "Parola Sıfırlama"
                && m.PlainTextBody.Contains("https://localhost/Account/ResetPassword?token=", StringComparison.Ordinal)
                && m.HtmlBody.Contains("href=", StringComparison.Ordinal)),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RequestReset_UnknownUser_SameGenericSuccess()
    {
        var userRepo = new Mock<IUserRepository>();
        userRepo.Setup(r => r.GetByTcNoOrEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);
        var email = new Mock<IEmailService>();
        var sut = new PasswordResetService(
            userRepo.Object, Mock.Of<IPasswordResetTokenRepository>(),
            Mock.Of<IPasswordResetTransactionService>(), Mock.Of<IPasswordService>(),
            email.Object, new PasswordResetEmailTemplate(), Mock.Of<IAuditService>(),
            NullLogger<PasswordResetService>.Instance);

        var result = await sut.RequestResetAsync("unknown@x.com", "https://localhost/r", null);
        Assert.True(result.Success);
        email.Verify(e => e.SendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DevFileEmail_WritesUtf8RandomFileOutsideWebRoot()
    {
        var contentRoot = Path.Combine(Path.GetTempPath(), "oys-email-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(contentRoot);
        try
        {
            var env = new FakeHostEnvironment(Environments.Development) { ContentRootPath = contentRoot };
            var options = Options.Create(new EmailOptions
            {
                DeliveryMode = EmailDeliveryMode.File,
                DevPickupDirectory = "App_Data/dev-emails",
                FromAddress = "no-reply@example.com",
                FromName = "ÖYS"
            });
            var sut = new DevFileEmailService(options, env, NullLogger<DevFileEmailService>.Instance);
            var token = "tok_SECRET_VALUE";
            var link = $"https://localhost/Account/ResetPassword?token={token}";
            var message = new PasswordResetEmailTemplate().Create("Ayşe", "aday@example.com", link);

            await sut.SendAsync(message);

            var dir = Path.Combine(contentRoot, "App_Data", "dev-emails");
            var files = Directory.GetFiles(dir);
            Assert.Single(files);
            var name = Path.GetFileName(files[0]);
            Assert.DoesNotContain("aday", name, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(token, name, StringComparison.Ordinal);
            Assert.Matches("^[0-9a-fA-F]{32}\\.eml\\.html$", name);

            var bytes = await File.ReadAllBytesAsync(files[0]);
            var text = Encoding.UTF8.GetString(bytes);
            Assert.Contains("Parola", text, StringComparison.Ordinal);
            Assert.Contains("2 saat", text, StringComparison.Ordinal);
            Assert.Contains("Ay", text, StringComparison.Ordinal); // Ayşe HTML body
            Assert.Contains(token, text, StringComparison.Ordinal); // içerikte olabilir; dosya adında değil
            Assert.StartsWith(Path.GetFullPath(contentRoot), files[0], StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(
                $"{Path.DirectorySeparatorChar}wwwroot{Path.DirectorySeparatorChar}",
                files[0],
                StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            if (Directory.Exists(contentRoot))
                Directory.Delete(contentRoot, recursive: true);
        }
    }

    [Fact]
    public void DevFileEmail_ProductionCtor_Throws()
    {
        Assert.Throws<InvalidOperationException>(() =>
            new DevFileEmailService(
                Options.Create(new EmailOptions { FromAddress = "a@b.com", FromName = "X" }),
                new FakeHostEnvironment(Environments.Production),
                NullLogger<DevFileEmailService>.Instance));
    }

    [Fact]
    public void SmtpSource_HasNoCertificateBypassOrPlaintextFallback()
    {
        var source = File.ReadAllText(FindUnder(
            "src", "OzelYetenekSinavSistemi.Infrastructure", "Services", "SmtpEmailService.cs"));
        Assert.DoesNotContain("client.ServerCertificateValidationCallback", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ServerCertificateValidationCallback =", source, StringComparison.Ordinal);
        Assert.DoesNotContain("SecureSocketOptions.None", source, StringComparison.Ordinal);
        Assert.DoesNotContain("SecureSocketOptions.Auto", source, StringComparison.Ordinal);
        Assert.Contains("SecureSocketOptions.StartTls", source, StringComparison.Ordinal);
        Assert.Contains("SecureSocketOptions.SslOnConnect", source, StringComparison.Ordinal);
    }

    [Fact]
    public void PasswordResetService_DoesNotLogTokenOrLink()
    {
        var source = File.ReadAllText(FindUnder(
            "src", "OzelYetenekSinavSistemi.Application", "Services", "PasswordResetService.cs"));
        Assert.DoesNotContain("{ResetLink}", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("{Token}", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("LogInformation(resetLink", source, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("EmailDeliveryFailed", source, StringComparison.Ordinal);
        Assert.Contains("MarkAsUsedAsync", source, StringComparison.Ordinal);
        Assert.Contains("Uri.EscapeDataString(plainToken)", source, StringComparison.Ordinal);
    }

    private static EmailOptions ValidSmtp(Action<EmailOptions>? mutate = null)
    {
        var o = new EmailOptions
        {
            DeliveryMode = EmailDeliveryMode.Smtp,
            Host = "smtp.example.edu.tr",
            Port = 587,
            SecurityMode = EmailSecurityMode.StartTls,
            Username = "u",
            Password = "p",
            FromAddress = "no-reply@example.com",
            FromName = "ÖYS",
            ConnectionTimeoutSeconds = 15,
            SendTimeoutSeconds = 30
        };
        mutate?.Invoke(o);
        return o;
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

    private sealed class FakeHostEnvironment : IHostEnvironment
    {
        public FakeHostEnvironment(string name) => EnvironmentName = name;
        public string EnvironmentName { get; set; }
        public string ApplicationName { get; set; } = "Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
