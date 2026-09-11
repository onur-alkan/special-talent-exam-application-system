using System.Data;
using Microsoft.Data.SqlClient;
using OzelYetenekSinavSistemi.Application.Interfaces.Infrastructure;

namespace OzelYetenekSinavSistemi.Infrastructure.Persistence;

public sealed class SqlConnectionFactory : IDbConnectionFactory
{
    private readonly string _connectionString;

    public SqlConnectionFactory(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new ArgumentException("Connection string boş olamaz.", nameof(connectionString));
        _connectionString = connectionString;
        DapperTypeHandlers.EnsureRegistered();
    }

    public async Task<IDbConnection> CreateOpenConnectionAsync(CancellationToken cancellationToken = default)
    {
        var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        return connection;
    }
}
