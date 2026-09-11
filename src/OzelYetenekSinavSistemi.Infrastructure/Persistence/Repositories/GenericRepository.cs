using System.Data;
using System.Reflection;
using Dapper;
using OzelYetenekSinavSistemi.Application.Interfaces.Infrastructure;
using OzelYetenekSinavSistemi.Application.Interfaces.Repositories;

namespace OzelYetenekSinavSistemi.Infrastructure.Persistence.Repositories;

/// <summary>
/// Dapper tabanlı, yalnızca temel CRUD işlemleri içeren generic repository.
/// Kolon adları açıkça yazılır (SELECT * kullanılmaz), tüm sorgular parametriktir.
/// </summary>
public abstract class GenericRepository<T> : IGenericRepository<T> where T : class
{
    protected readonly IDbConnectionFactory ConnectionFactory;
    protected abstract string TableName { get; }

    private static readonly PropertyInfo IdProperty =
        typeof(T).GetProperty("Id", BindingFlags.Public | BindingFlags.Instance)
        ?? throw new InvalidOperationException($"{typeof(T).Name} tipinde 'Id' property'si bulunamadı.");

    protected GenericRepository(IDbConnectionFactory connectionFactory)
    {
        ConnectionFactory = connectionFactory;
    }

    protected string AllColumnsList => string.Join(", ", EntityMetadata.GetAllColumns(typeof(T)));

    public virtual async Task<T?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var sql = $"SELECT {AllColumnsList} FROM {TableName} WHERE Id = @Id;";
        using var connection = await ConnectionFactory.CreateOpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        return await connection.QueryFirstOrDefaultAsync<T>(
            new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    public virtual async Task<IReadOnlyList<T>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var sql = $"SELECT {AllColumnsList} FROM {TableName};";
        using var connection = await ConnectionFactory.CreateOpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        var rows = await connection.QueryAsync<T>(
            new CommandDefinition(sql, cancellationToken: cancellationToken)).ConfigureAwait(false);
        return rows.ToList();
    }

    public virtual async Task<Guid> AddAsync(T entity, CancellationToken cancellationToken = default)
    {
        ApplyAutoTimestamps(entity);

        var columns = EntityMetadata.GetWritableColumns(typeof(T));
        var columnList = string.Join(", ", columns);
        var valueList = string.Join(", ", columns.Select(c => "@" + c));

        var sql = $"INSERT INTO {TableName} ({columnList}) OUTPUT INSERTED.Id VALUES ({valueList});";
        using var connection = await ConnectionFactory.CreateOpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        var id = await connection.ExecuteScalarAsync<Guid>(
            new CommandDefinition(sql, entity, cancellationToken: cancellationToken)).ConfigureAwait(false);

        IdProperty.SetValue(entity, id);
        return id;
    }

    public virtual async Task<bool> UpdateAsync(T entity, CancellationToken cancellationToken = default)
    {
        var columns = EntityMetadata.GetWritableColumns(typeof(T));
        var setList = string.Join(", ", columns.Select(c => $"{c} = @{c}"));

        var sql = $"UPDATE {TableName} SET {setList} WHERE Id = @Id;";
        using var connection = await ConnectionFactory.CreateOpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        var affected = await connection.ExecuteAsync(
            new CommandDefinition(sql, entity, cancellationToken: cancellationToken)).ConfigureAwait(false);
        return affected > 0;
    }

    public virtual async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var sql = $"DELETE FROM {TableName} WHERE Id = @Id;";
        using var connection = await ConnectionFactory.CreateOpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        var affected = await connection.ExecuteAsync(
            new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken)).ConfigureAwait(false);
        return affected > 0;
    }

    /// <summary>
    /// CreatedDate / AssignedDate gibi oluşturma zaman damgalarını, atanmamışsa UtcNow ile doldurur.
    /// </summary>
    private static void ApplyAutoTimestamps(T entity)
    {
        foreach (var name in new[] { "CreatedDate", "AssignedDate" })
        {
            var prop = typeof(T).GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
            if (prop is null || !prop.CanWrite)
                continue;

            if (prop.PropertyType == typeof(DateTime))
            {
                var current = (DateTime)(prop.GetValue(entity) ?? default(DateTime));
                if (current == default)
                    prop.SetValue(entity, DateTime.UtcNow);
            }
        }
    }
}
