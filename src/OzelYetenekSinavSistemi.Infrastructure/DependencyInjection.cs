using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using OzelYetenekSinavSistemi.Application.Common;
using OzelYetenekSinavSistemi.Application.Interfaces.Infrastructure;
using OzelYetenekSinavSistemi.Application.Interfaces.Repositories;
using OzelYetenekSinavSistemi.Application.Interfaces.Services;
using OzelYetenekSinavSistemi.Infrastructure.Configuration;
using OzelYetenekSinavSistemi.Infrastructure.Persistence;
using OzelYetenekSinavSistemi.Infrastructure.Persistence.Repositories;
using OzelYetenekSinavSistemi.Infrastructure.Services;

namespace OzelYetenekSinavSistemi.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        string connectionString,
        string webRootPath,
        string contentRootPath,
        IHostEnvironment environment)
    {
        var privatePhotoStorage = Path.Combine(contentRootPath, "App_Data", "private-uploads", "photos");

        // Options
        services.AddOptions<PhotoUploadOptions>()
            .Bind(configuration.GetSection(PhotoUploadOptions.SectionName))
            .PostConfigure(o =>
            {
                o.WebRootPath = webRootPath;
                if (string.IsNullOrWhiteSpace(o.StorageRootPath))
                    o.StorageRootPath = privatePhotoStorage;
                else if (!Path.IsPathRooted(o.StorageRootPath))
                    o.StorageRootPath = Path.GetFullPath(Path.Combine(contentRootPath, o.StorageRootPath));
                else
                    o.StorageRootPath = Path.GetFullPath(o.StorageRootPath);

                // Güvenlik tavanı: yapılandırma ile 2 MB aşılamaz.
                if (o.MaxBytes > PhotoUploadLimits.MaxFileBytes)
                    o.MaxBytes = PhotoUploadLimits.MaxFileBytes;
                if (o.MaxBytes <= 0)
                    o.MaxBytes = PhotoUploadLimits.MaxFileBytes;
            })
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<PhotoUploadOptions>, PhotoUploadOptionsValidator>();

        services.AddOptions<EmailOptions>()
            .Bind(configuration.GetSection(EmailOptions.SectionName))
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<EmailOptions>, EmailOptionsValidator>();

        services.AddOptions<SeedOptions>()
            .Bind(configuration.GetSection(SeedOptions.SectionName))
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<SeedOptions>, SeedOptionsValidator>();

        services.AddOptions<DataRetentionOptions>()
            .Bind(configuration.GetSection(DataRetentionOptions.SectionName))
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<DataRetentionOptions>, DataRetentionOptionsValidator>();

        // Erken yapılandırma doğrulaması (ilk e-posta talebinden önce fail).
        var emailOptions = configuration.GetSection(EmailOptions.SectionName).Get<EmailOptions>() ?? new EmailOptions();
        var emailErrors = new List<string>();
        EmailOptionsValidator.ValidateCore(emailOptions, environment.IsProduction(), emailErrors);
        if (emailErrors.Count > 0)
            throw new InvalidOperationException(string.Join(" ", emailErrors));

        // Bağlantı fabrikası
        services.AddSingleton<IDbConnectionFactory>(_ => new SqlConnectionFactory(connectionString));

        // Repositories
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IRoleRepository, RoleRepository>();
        services.AddScoped<IYgsYearRepository, YgsYearRepository>();
        services.AddScoped<IExamPeriodRepository, ExamPeriodRepository>();
        services.AddScoped<IExamPreferenceOptionRepository, ExamPreferenceOptionRepository>();
        services.AddScoped<IExamPeriodManagerRepository, ExamPeriodManagerRepository>();
        services.AddScoped<ICandidateApplicationRepository, CandidateApplicationRepository>();
        services.AddScoped<ICandidateExamResultRepository, CandidateExamResultRepository>();
        services.AddScoped<IPasswordResetTokenRepository, PasswordResetTokenRepository>();
        services.AddScoped<IAuditLogRepository, AuditLogRepository>();
        services.AddScoped<ISystemSettingRepository, SystemSettingRepository>();
        services.AddScoped<IDocumentVerificationRepository, DocumentVerificationRepository>();

        // Altyapı servisleri
        services.AddSingleton<IKeyedAsyncLock, KeyedAsyncLock>();
        services.AddSingleton<IPasswordService, PasswordService>();
        services.AddSingleton<ICaptchaService, CaptchaService>();
        services.AddSingleton<IQrCodeService, QrCodeService>();
        services.AddSingleton<IExcelExportService, ExcelExportService>();
        services.AddSingleton<IPdfExportService>(_ => new PdfExportService(webRootPath));
        services.AddSingleton<ICandidateNumberService, CandidateNumberService>();
        services.AddScoped<IPhotoUploadService, PhotoUploadService>();
        RegisterEmailService(services, emailOptions, environment);
        services.AddScoped<IAuditService, AuditService>();
        services.AddScoped<IPasswordResetTransactionService, PasswordResetTransactionService>();
        services.AddScoped<IDataRetentionCleanupService, DataRetentionCleanupService>();
        services.AddScoped<LegacyPhotoMigrationService>();
        services.AddScoped<SchemaValidationService>();
        services.AddHostedService<DataRetentionCleanupHostedService>();

        // Seeder
        services.AddScoped<DatabaseSeeder>();

        return services;
    }

    internal static void RegisterEmailService(
        IServiceCollection services,
        EmailOptions emailOptions,
        IHostEnvironment environment)
    {
        if (environment.IsProduction())
        {
            if (emailOptions.DeliveryMode != EmailDeliveryMode.Smtp)
                throw new InvalidOperationException("Production ortamında Email:DeliveryMode Smtp olmalıdır.");

            services.AddScoped<IEmailService, SmtpEmailService>();
            return;
        }

        switch (emailOptions.DeliveryMode)
        {
            case EmailDeliveryMode.File:
                services.AddScoped<IEmailService, DevFileEmailService>();
                break;
            case EmailDeliveryMode.Smtp:
                services.AddScoped<IEmailService, SmtpEmailService>();
                break;
            case EmailDeliveryMode.Disabled:
                services.AddScoped<IEmailService, DisabledEmailService>();
                break;
            default:
                throw new InvalidOperationException($"Email:DeliveryMode desteklenmiyor: {emailOptions.DeliveryMode}");
        }
    }
}
