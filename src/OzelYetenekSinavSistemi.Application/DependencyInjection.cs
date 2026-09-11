using Microsoft.Extensions.DependencyInjection;
using OzelYetenekSinavSistemi.Application.Interfaces.Services;
using OzelYetenekSinavSistemi.Application.Services;

namespace OzelYetenekSinavSistemi.Application;

public static class DependencyInjection
{
    /// <summary>
    /// Application katmanı iş servislerini DI konteynerine kaydeder.
    /// </summary>
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<ICountryCatalog, CountryCatalog>();
        services.AddSingleton<ITurkishIdentityNumberValidator, TurkishIdentityNumberValidator>();
        services.AddSingleton<IIdentityDocumentValidator, IdentityDocumentValidator>();
        services.AddScoped<ISensitiveDataMaskingService, SensitiveDataMaskingService>();
        services.AddScoped<IAuthenticationService, AuthenticationService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IPasswordResetService, PasswordResetService>();
        services.AddSingleton<IPasswordResetEmailTemplate, PasswordResetEmailTemplate>();
        services.AddScoped<IExamPeriodService, ExamPeriodService>();
        services.AddScoped<ICandidateApplicationService, CandidateApplicationService>();
        services.AddScoped<IExamResultService, ExamResultService>();
        services.AddScoped<IDocumentService, DocumentService>();
        services.AddScoped<IDocumentVerificationService, DocumentVerificationService>();
        services.AddScoped<ICandidatePhotoService, CandidatePhotoService>();
        services.AddScoped<IUserManagementService, UserManagementService>();

        return services;
    }
}
