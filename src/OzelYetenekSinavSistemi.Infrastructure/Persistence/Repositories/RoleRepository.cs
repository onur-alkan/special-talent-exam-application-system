using Dapper;
using OzelYetenekSinavSistemi.Application.Interfaces.Infrastructure;
using OzelYetenekSinavSistemi.Application.Interfaces.Repositories;
using OzelYetenekSinavSistemi.Domain.Entities;

namespace OzelYetenekSinavSistemi.Infrastructure.Persistence.Repositories;

public sealed class RoleRepository : GenericRepository<Role>, IRoleRepository
{
    protected override string TableName => "dbo.Roles";

    public RoleRepository(IDbConnectionFactory connectionFactory) : base(connectionFactory) { }

    public async Task<Role?> GetByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT Id, Name, Description, CreatedDate FROM dbo.Roles WHERE Name = @Name;";
        using var connection = await ConnectionFactory.CreateOpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        return await connection.QueryFirstOrDefaultAsync<Role>(
            new CommandDefinition(sql, new { Name = name }, cancellationToken: cancellationToken)).ConfigureAwait(false);
    }
}
