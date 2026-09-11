using System.Collections.Concurrent;
using System.Reflection;

namespace OzelYetenekSinavSistemi.Infrastructure.Persistence;

/// <summary>
/// Generic repository için entity kolon meta verisini reflection ile üretir ve önbelleğe alır.
/// Kolon adları property adlarından türetilir; böylece SELECT * yerine açık kolon listeleri kullanılır.
/// </summary>
internal static class EntityMetadata
{
    private static readonly ConcurrentDictionary<Type, string[]> ColumnCache = new();

    /// <summary>Id dışındaki, yazılabilir tüm kolonları döner.</summary>
    public static string[] GetWritableColumns(Type type) =>
        ColumnCache.GetOrAdd(type, static t => t
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p is { CanRead: true, CanWrite: true })
            .Select(p => p.Name)
            .Where(name => !string.Equals(name, "Id", StringComparison.Ordinal))
            .ToArray());

    public static string[] GetAllColumns(Type type)
    {
        var writable = GetWritableColumns(type);
        var all = new string[writable.Length + 1];
        all[0] = "Id";
        Array.Copy(writable, 0, all, 1, writable.Length);
        return all;
    }
}
