using Microsoft.Extensions.Logging;
using OzelYetenekSinavSistemi.Application.Interfaces.Repositories;
using OzelYetenekSinavSistemi.Application.Interfaces.Services;
using OzelYetenekSinavSistemi.Domain.Entities;

namespace OzelYetenekSinavSistemi.Infrastructure.Services;

public sealed class AuditService : IAuditService
{
    private readonly IAuditLogRepository _auditLogRepository;
    private readonly ISensitiveDataMaskingService _masking;
    private readonly ILogger<AuditService> _logger;

    public AuditService(
        IAuditLogRepository auditLogRepository,
        ISensitiveDataMaskingService masking,
        ILogger<AuditService> logger)
    {
        _auditLogRepository = auditLogRepository;
        _masking = masking;
        _logger = logger;
    }

    public async Task LogAsync(
        string eventType,
        string? description,
        Guid? userId = null,
        string? ipAddress = null,
        string? requestPath = null,
        string? correlationId = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var entry = new AuditLog
            {
                UserId = userId,
                EventType = _masking.SanitizeEventType(eventType),
                Description = _masking.SanitizeDescription(description),
                IpAddress = _masking.SanitizeIpAddress(ipAddress),
                RequestPath = NullIfEmpty(_masking.SanitizeRequestPath(requestPath)),
                CorrelationId = NullIfEmpty(_masking.SanitizeCorrelationId(correlationId)),
                CreatedDate = DateTime.UtcNow
            };

            await _auditLogRepository.AddAsync(entry, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // Audit yazımı ana akışı bozmamalıdır. Hassas alanlar loglanmaz.
            _logger.LogError(ex, "Audit kaydı yazılamadı. EventType={EventType}", _masking.SanitizeEventType(eventType));
        }
    }

    private static string? NullIfEmpty(string value) =>
        string.IsNullOrEmpty(value) ? null : value;
}
