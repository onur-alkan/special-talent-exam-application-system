namespace OzelYetenekSinavSistemi.Application.Interfaces.Repositories;

/// <summary>
/// Yalnızca temel CRUD işlemlerini içeren generic repository sözleşmesi.
/// Karmaşık iş kuralları burada tutulmaz; özel repository'lerde ele alınır.
/// </summary>
public interface IGenericRepository<T> where T : class
{
    Task<T?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<T>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<Guid> AddAsync(T entity, CancellationToken cancellationToken = default);

    Task<bool> UpdateAsync(T entity, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
