using System.ComponentModel.DataAnnotations;

namespace OzelYetenekSinavSistemi.Infrastructure.Configuration;

/// <summary>
/// Log ve denetim kayıtları için yapılandırılabilir saklama politikası.
/// Secret içermez; yalnızca sayısal politika değerleri.
/// </summary>
public sealed class DataRetentionOptions
{
    public const string SectionName = "DataRetention";

    public const int MaxRetentionDays = 3650;
    public const int MaxCleanupIntervalHours = 168;
    public const int MinBatchSize = 1;
    public const int MaxBatchSize = 5000;
    public const int DefaultStartupDelaySeconds = 120;

    [Range(1, MaxRetentionDays)]
    public int SerilogSqlDays { get; set; } = 90;

    [Range(1, MaxRetentionDays)]
    public int AuditLogDays { get; set; } = 365;

    [Range(1, MaxRetentionDays)]
    public int PasswordResetTokenDays { get; set; } = 7;

    [Range(1, MaxCleanupIntervalHours)]
    public int CleanupIntervalHours { get; set; } = 24;

    public bool EnableCleanup { get; set; } = true;

    [Range(MinBatchSize, MaxBatchSize)]
    public int BatchSize { get; set; } = 1000;

    /// <summary>İlk temizliğin startup'ı engellememesi için gecikme (saniye).</summary>
    [Range(0, 3600)]
    public int StartupDelaySeconds { get; set; } = DefaultStartupDelaySeconds;
}

/// <summary>
/// DataRetention seçeneklerini doğrular; Production'da geçersiz yapılandırma startup hatası üretir.
/// </summary>
public sealed class DataRetentionOptionsValidator : Microsoft.Extensions.Options.IValidateOptions<DataRetentionOptions>
{
    public Microsoft.Extensions.Options.ValidateOptionsResult Validate(string? name, DataRetentionOptions options)
    {
        var errors = new List<string>();

        if (options.SerilogSqlDays < 1 || options.SerilogSqlDays > DataRetentionOptions.MaxRetentionDays)
            errors.Add($"DataRetention:SerilogSqlDays 1-{DataRetentionOptions.MaxRetentionDays} aralığında olmalıdır.");

        if (options.AuditLogDays < 1 || options.AuditLogDays > DataRetentionOptions.MaxRetentionDays)
            errors.Add($"DataRetention:AuditLogDays 1-{DataRetentionOptions.MaxRetentionDays} aralığında olmalıdır.");

        if (options.PasswordResetTokenDays < 1 || options.PasswordResetTokenDays > DataRetentionOptions.MaxRetentionDays)
            errors.Add($"DataRetention:PasswordResetTokenDays 1-{DataRetentionOptions.MaxRetentionDays} aralığında olmalıdır.");

        if (options.CleanupIntervalHours < 1 || options.CleanupIntervalHours > DataRetentionOptions.MaxCleanupIntervalHours)
            errors.Add($"DataRetention:CleanupIntervalHours 1-{DataRetentionOptions.MaxCleanupIntervalHours} aralığında olmalıdır.");

        if (options.BatchSize < DataRetentionOptions.MinBatchSize || options.BatchSize > DataRetentionOptions.MaxBatchSize)
            errors.Add($"DataRetention:BatchSize {DataRetentionOptions.MinBatchSize}-{DataRetentionOptions.MaxBatchSize} aralığında olmalıdır.");

        if (options.StartupDelaySeconds < 0 || options.StartupDelaySeconds > 3600)
            errors.Add("DataRetention:StartupDelaySeconds 0-3600 aralığında olmalıdır.");

        return errors.Count > 0
            ? Microsoft.Extensions.Options.ValidateOptionsResult.Fail(errors)
            : Microsoft.Extensions.Options.ValidateOptionsResult.Success;
    }
}
