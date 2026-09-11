using System.Data;

namespace OzelYetenekSinavSistemi.Application.Interfaces.Infrastructure;

/// <summary>
/// Dapper için SQL bağlantısı üreten fabrika soyutlaması.
/// </summary>
public interface IDbConnectionFactory
{
    Task<IDbConnection> CreateOpenConnectionAsync(CancellationToken cancellationToken = default);
}
