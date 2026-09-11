using System.Collections.ObjectModel;
using System.Data;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Options;
using OzelYetenekSinavSistemi.Application;
using OzelYetenekSinavSistemi.Application.Common;
using OzelYetenekSinavSistemi.Domain.Common;
using OzelYetenekSinavSistemi.Infrastructure;
using OzelYetenekSinavSistemi.Infrastructure.Persistence;
using OzelYetenekSinavSistemi.Infrastructure.Services;
using OzelYetenekSinavSistemi.Web.Configuration;
using OzelYetenekSinavSistemi.Web.Health;
using OzelYetenekSinavSistemi.Web.Infrastructure;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Serilog;
using Serilog.Sinks.MSSqlServer;

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------------------
// Request body / multipart boyut sınırları (fotoğraf upload: dosya 2 MB, istek 3 MB)
// ---------------------------------------------------------------------------
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = PhotoUploadLimits.MaxMultipartRequestBytes;
});

builder.Services.Configure<IISServerOptions>(options =>
{
    options.MaxRequestBodySize = PhotoUploadLimits.MaxMultipartRequestBytes;
});

builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = PhotoUploadLimits.MaxMultipartRequestBytes;
    options.ValueLengthLimit = checked((int)Math.Min(int.MaxValue, PhotoUploadLimits.MaxMultipartRequestBytes));
    options.MultipartHeadersLengthLimit = 16 * 1024;
    options.MemoryBufferThreshold = PhotoUploadLimits.MemoryBufferThresholdBytes;
});

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? string.Empty;
ProductionReadinessValidator.ValidateConnectionString(builder.Environment, connectionString);

// ---------------------------------------------------------------------------
// Serilog: Konsol + günlük dönen dosya + SQL Server tablosu
// ---------------------------------------------------------------------------
var columnOptions = new ColumnOptions();
// Id kolonu veritabanında UNIQUEIDENTIFIER + DEFAULT NEWSEQUENTIALID() olarak tanımlıdır.
// Sink'in varsayılan INT IDENTITY Id kolonunu INSERT etmemesi için Store listesinden çıkarılır;
// böylece Id değerini veritabanı üretir ve özel şema korunur.
columnOptions.Store.Remove(StandardColumn.Id);
columnOptions.Store.Remove(StandardColumn.Properties);
columnOptions.Store.Add(StandardColumn.LogEvent);
columnOptions.AdditionalColumns = new Collection<SqlColumn>
{
    new() { ColumnName = "UserId", DataType = SqlDbType.NVarChar, DataLength = 64, AllowNull = true },
    new() { ColumnName = "IpAddress", DataType = SqlDbType.NVarChar, DataLength = 64, AllowNull = true },
    new() { ColumnName = "RequestPath", DataType = SqlDbType.NVarChar, DataLength = 400, AllowNull = true },
    new() { ColumnName = "RequestMethod", DataType = SqlDbType.NVarChar, DataLength = 16, AllowNull = true },
    new() { ColumnName = "CorrelationId", DataType = SqlDbType.NVarChar, DataLength = 64, AllowNull = true },
    new() { ColumnName = "EventType", DataType = SqlDbType.NVarChar, DataLength = 100, AllowNull = true }
};

builder.Host.UseSerilog((context, services, configuration) =>
{
    configuration
        .MinimumLevel.Information()
        .MinimumLevel.Override("Microsoft.AspNetCore", Serilog.Events.LogEventLevel.Warning)
        .Enrich.FromLogContext()
        .WriteTo.Console()
        .WriteTo.File(
            path: Path.Combine(context.HostingEnvironment.ContentRootPath, "logs", "log-.txt"),
            rollingInterval: RollingInterval.Day,
            retainedFileCountLimit: 30,
            rollOnFileSizeLimit: true,
            fileSizeLimitBytes: 50 * 1024 * 1024,
            shared: true)
        .WriteTo.MSSqlServer(
            connectionString: connectionString,
            sinkOptions: new MSSqlServerSinkOptions { TableName = "Logs", AutoCreateSqlTable = false },
            columnOptions: columnOptions,
            restrictedToMinimumLevel: Serilog.Events.LogEventLevel.Information);
});

// ---------------------------------------------------------------------------
// Reverse proxy + Public URL
// ---------------------------------------------------------------------------
builder.Services.AddOptions<ReverseProxyOptions>()
    .Bind(builder.Configuration.GetSection(ReverseProxyOptions.SectionName))
    .ValidateOnStart();
builder.Services.AddSingleton<IValidateOptions<ReverseProxyOptions>, ReverseProxyOptionsValidator>();

builder.Services.AddOptions<PublicUrlOptions>()
    .Bind(builder.Configuration.GetSection(PublicUrlOptions.SectionName))
    .PostConfigure(o =>
    {
        if (HostingSecurityOptionsValidator.TryNormalizePublicBaseUrl(o.BaseUrl, out var normalized, out _))
            o.BaseUrl = normalized;
    })
    .ValidateOnStart();
builder.Services.AddSingleton<IValidateOptions<PublicUrlOptions>, PublicUrlOptionsValidator>();

builder.Services.AddSingleton<IClientIpAddressAccessor, ClientIpAddressAccessor>();
builder.Services.AddSingleton<IPublicUrlBuilder, PublicUrlBuilder>();
builder.Services.AddSingleton<ICspNonceProvider, CspNonceProvider>();

var reverseProxyOptions = builder.Configuration.GetSection(ReverseProxyOptions.SectionName).Get<ReverseProxyOptions>()
    ?? new ReverseProxyOptions();
var publicUrlOptions = builder.Configuration.GetSection(PublicUrlOptions.SectionName).Get<PublicUrlOptions>()
    ?? new PublicUrlOptions();

HostingSecurityOptionsValidator.Validate(
    builder.Environment,
    builder.Configuration,
    reverseProxyOptions,
    publicUrlOptions);

// ---------------------------------------------------------------------------
// Data Protection — kalıcı key ring (auth cookie; reset token'ları DB hash ile ayrı)
// ---------------------------------------------------------------------------
builder.Services.AddOptions<DataProtectionOptions>()
    .Bind(builder.Configuration.GetSection(DataProtectionOptions.SectionName))
    .ValidateOnStart();
builder.Services.AddSingleton<IValidateOptions<DataProtectionOptions>, DataProtectionOptionsValidator>();

var dataProtectionOptions = builder.Configuration
    .GetSection(DataProtectionOptions.SectionName)
    .Get<DataProtectionOptions>()
    ?? new DataProtectionOptions();

DataProtectionKeyRing.AddConfiguredDataProtection(
    builder.Services,
    builder.Environment,
    dataProtectionOptions);

if (reverseProxyOptions.Enabled)
{
    builder.Services.Configure<ForwardedHeadersOptions>(options =>
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        options.ForwardLimit = Math.Clamp(
            reverseProxyOptions.ForwardLimit,
            ReverseProxyOptions.MinForwardLimit,
            ReverseProxyOptions.MaxForwardLimit);
        options.RequireHeaderSymmetry = reverseProxyOptions.RequireHeaderSymmetry;

        // Varsayılan loopback/özel ağ güvenini kaldır; yalnızca yapılandırılanları ekle.
        options.KnownProxies.Clear();
        options.KnownNetworks.Clear();

        foreach (var ip in HostingSecurityOptionsValidator.ParseKnownProxies(reverseProxyOptions))
            options.KnownProxies.Add(ip);

        foreach (var network in HostingSecurityOptionsValidator.ParseKnownNetworks(reverseProxyOptions))
            options.KnownNetworks.Add(network);
    });
}

// ---------------------------------------------------------------------------
// MVC + global filtreler
// ---------------------------------------------------------------------------
builder.Services.AddControllersWithViews(options =>
{
    // Global anti-forgery doğrulaması
    options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
    TurkishModelBindingMessageConfiguration.Apply(options);
});

builder.Services.AddOptions<Microsoft.AspNetCore.Mvc.MvcViewOptions>()
    .Configure<TimeProvider>((options, timeProvider) =>
    {
        options.ClientModelValidatorProviders.Add(new TurkishNumericClientModelValidatorProvider());
        options.ClientModelValidatorProviders.Add(new TurkishIdentityNumberClientModelValidatorProvider());
        options.ClientModelValidatorProviders.Add(new BirthYearClientModelValidatorProvider(timeProvider));
        options.ClientModelValidatorProviders.Add(new BirthDateClientModelValidatorProvider(timeProvider));
        options.ClientModelValidatorProviders.Add(new MobilePhoneNumberClientModelValidatorProvider());
        options.ClientModelValidatorProviders.Add(new InputTextClientModelValidatorProvider());
    });


builder.Services.AddHttpContextAccessor();

builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.Name = "OYS.Session";
});

// ---------------------------------------------------------------------------
// Cookie Authentication
// ---------------------------------------------------------------------------
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromHours(2);
        options.SlidingExpiration = true;
        options.Cookie.Name = "OYS.Auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.EventsType = typeof(ActiveUserCookieAuthenticationEvents);
    });

builder.Services.AddScoped<ActiveUserCookieAuthenticationEvents>();

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("SuperAdminOnly", p => p.RequireRole(DomainConstants.RoleNames.SuperAdmin));
    options.AddPolicy("AdminArea", p => p.RequireRole(DomainConstants.RoleNames.SuperAdmin, DomainConstants.RoleNames.ApplicationManager));
    options.AddPolicy("CandidateOnly", p => p.RequireRole(DomainConstants.RoleNames.Candidate));
});

// ---------------------------------------------------------------------------
// Rate limiting — partition key güvenilir istemci IP (IClientIpAddressAccessor)
// ---------------------------------------------------------------------------
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.AddPolicy("login", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.GetClientIpPartitionKey(),
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));

    options.AddPolicy("password-reset-request", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.GetClientIpPartitionKey(),
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(15),
                QueueLimit = 0,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst
            }));

    options.AddPolicy("password-reset-submit", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.GetClientIpPartitionKey(),
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(15),
                QueueLimit = 0,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst
            }));

    options.AddPolicy("document-verification", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.GetClientIpPartitionKey(),
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 30,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst
            }));
});

// ---------------------------------------------------------------------------
// Uygulama ve altyapı servisleri
// ---------------------------------------------------------------------------
builder.Services.AddApplicationServices();
builder.Services.AddInfrastructure(
    builder.Configuration,
    connectionString,
    builder.Environment.WebRootPath,
    builder.Environment.ContentRootPath,
    builder.Environment);

var readyTimeout = TimeSpan.FromSeconds(5);
builder.Services.AddHealthChecks()
    .AddCheck<SqlSelectOneHealthCheck>("sql", tags: new[] { HealthCheckTags.Ready }, timeout: readyTimeout)
    .AddCheck<PhotoStorageHealthCheck>("photo_storage", tags: new[] { HealthCheckTags.Ready }, timeout: readyTimeout)
    .AddCheck<DataProtectionKeysHealthCheck>("dataprotection_keys", tags: new[] { HealthCheckTags.Ready }, timeout: readyTimeout)
    .AddCheck<CriticalConfigurationHealthCheck>("configuration", tags: new[] { HealthCheckTags.Ready }, timeout: readyTimeout);

var app = builder.Build();

// Production: private storage ve Data Protection erişim probe (secretsiz fail-fast)
if (app.Environment.IsProduction())
{
    using var readinessScope = app.Services.CreateScope();
    var photoOptions = readinessScope.ServiceProvider
        .GetRequiredService<Microsoft.Extensions.Options.IOptions<OzelYetenekSinavSistemi.Infrastructure.Configuration.PhotoUploadOptions>>()
        .Value;
    ProductionReadinessValidator.EnsurePrivatePhotoStorageAccessible(photoOptions, app.Environment);

    var dpOptions = app.Configuration
        .GetSection(DataProtectionOptions.SectionName)
        .Get<DataProtectionOptions>()
        ?? new DataProtectionOptions();
    var keysDirectory = DataProtectionKeyRing.ResolveKeysDirectory(app.Environment, dpOptions);
    ProductionReadinessValidator.EnsureDataProtectionKeysAccessible(keysDirectory);
}

// ---------------------------------------------------------------------------
// Middleware pipeline
// ---------------------------------------------------------------------------
// Forwarded headers (yalnızca Enabled=true): HSTS / HTTPS redirection öncesi.
if (reverseProxyOptions.Enabled)
{
    app.UseForwardedHeaders();
}

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseStatusCodePagesWithReExecute("/Home/Status", "?code={0}");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseMiddleware<SecurityHeadersMiddleware>();
app.UseMiddleware<BlockPublicPhotoUploadsMiddleware>();
app.UseStaticFiles();

app.UseRouting();
app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();
app.UseSession();

app.UseMiddleware<SerilogEnrichmentMiddleware>();
app.UseSerilogRequestLogging(options =>
{
    // Path query string içermez; body/header/cookie/QueryString eklenmez.
    options.MessageTemplate = "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000} ms";
    options.GetLevel = (httpContext, elapsed, ex) =>
        ex is not null || httpContext.Response.StatusCode >= 500
            ? Serilog.Events.LogEventLevel.Error
            : Serilog.Events.LogEventLevel.Information;
    options.EnrichDiagnosticContext = static (diagnosticContext, httpContext) =>
    {
        // Yalnızca path (query yok). Hassas alanlar eklenmez.
        diagnosticContext.Set("RequestPath", httpContext.Request.Path.Value ?? string.Empty);
    };
});

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = static _ => false,
    ResponseWriter = HealthCheckResponseWriter.WriteLiveAsync,
    ResultStatusCodes =
    {
        [HealthStatus.Healthy] = StatusCodes.Status200OK,
        [HealthStatus.Degraded] = StatusCodes.Status200OK,
        [HealthStatus.Unhealthy] = StatusCodes.Status503ServiceUnavailable
    }
});

app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = static check => check.Tags.Contains(HealthCheckTags.Ready),
    ResponseWriter = HealthCheckResponseWriter.WriteReadyAsync,
    ResultStatusCodes =
    {
        [HealthStatus.Healthy] = StatusCodes.Status200OK,
        [HealthStatus.Degraded] = StatusCodes.Status503ServiceUnavailable,
        [HealthStatus.Unhealthy] = StatusCodes.Status503ServiceUnavailable
    }
});

// ---------------------------------------------------------------------------
// Veritabanı seed (idempotent) + legacy fotoğraf migration
// ---------------------------------------------------------------------------
using (var scope = app.Services.CreateScope())
{
    try
    {
        var schema = scope.ServiceProvider.GetRequiredService<SchemaValidationService>();
        await schema.ValidateAsync();
    }
    catch (Exception ex)
    {
        Log.Fatal(ex, "Veritabanı şema doğrulaması başarısız. Uygulama başlatılamıyor.");
        throw;
    }

    try
    {
        var migration = scope.ServiceProvider.GetRequiredService<LegacyPhotoMigrationService>();
        await migration.MigrateAsync();
    }
    catch (Exception ex)
    {
        Log.Warning(ex, "Legacy fotoğraf migration çalıştırılamadı.");
    }

    try
    {
        var seeder = scope.ServiceProvider.GetRequiredService<DatabaseSeeder>();
        await seeder.SeedAsync();
    }
    catch (Exception ex)
    {
        Log.Warning(ex, "Veritabanı seed işlemi çalıştırılamadı. SQL scriptinin çalıştırıldığından ve bağlantının doğru olduğundan emin olun.");
    }
}

app.Run();

public partial class Program { }
