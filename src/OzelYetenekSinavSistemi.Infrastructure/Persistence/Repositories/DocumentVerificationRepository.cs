using Dapper;
using OzelYetenekSinavSistemi.Application.Interfaces.Infrastructure;
using OzelYetenekSinavSistemi.Application.Interfaces.Repositories;
using OzelYetenekSinavSistemi.Domain.Entities;

namespace OzelYetenekSinavSistemi.Infrastructure.Persistence.Repositories;

public sealed class DocumentVerificationRepository : GenericRepository<DocumentVerification>, IDocumentVerificationRepository
{
    protected override string TableName => "dbo.DocumentVerifications";

    private const string SelectColumns = "Id, ApplicationId, VerificationCode, IsValid, CreatedDate";

    public DocumentVerificationRepository(IDbConnectionFactory connectionFactory) : base(connectionFactory) { }

    public async Task<DocumentVerification?> GetByCodeAsync(string verificationCode, CancellationToken cancellationToken = default)
    {
        var sql = $"SELECT {SelectColumns} FROM dbo.DocumentVerifications WHERE VerificationCode = @Code;";
        using var connection = await ConnectionFactory.CreateOpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        return await connection.QueryFirstOrDefaultAsync<DocumentVerification>(
            new CommandDefinition(sql, new { Code = verificationCode }, cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    public async Task<DocumentVerification?> GetByApplicationAsync(Guid applicationId, CancellationToken cancellationToken = default)
    {
        var sql = $"SELECT {SelectColumns} FROM dbo.DocumentVerifications WHERE ApplicationId = @ApplicationId;";
        using var connection = await ConnectionFactory.CreateOpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        return await connection.QueryFirstOrDefaultAsync<DocumentVerification>(
            new CommandDefinition(sql, new { ApplicationId = applicationId }, cancellationToken: cancellationToken)).ConfigureAwait(false);
    }
}
