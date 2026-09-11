using System.Data;
using Dapper;

namespace OzelYetenekSinavSistemi.Infrastructure.Persistence;

/// <summary>
/// Dapper'ın varsayılan olarak desteklemediği <see cref="DateOnly"/> / <see cref="DateOnly?"/>
/// parametre ve okuma eşlemesi.
/// </summary>
internal sealed class DateOnlyTypeHandler : SqlMapper.TypeHandler<DateOnly>
{
    public override void SetValue(IDbDataParameter parameter, DateOnly value)
    {
        parameter.DbType = DbType.Date;
        parameter.Value = value.ToDateTime(TimeOnly.MinValue);
    }

    public override DateOnly Parse(object value) =>
        value switch
        {
            DateOnly dateOnly => dateOnly,
            DateTime dateTime => DateOnly.FromDateTime(dateTime),
            _ => DateOnly.FromDateTime(Convert.ToDateTime(value))
        };
}

internal static class DapperTypeHandlers
{
    private static int _registered;

    public static void EnsureRegistered()
    {
        if (Interlocked.Exchange(ref _registered, 1) == 1)
            return;

        SqlMapper.AddTypeHandler(new DateOnlyTypeHandler());
    }
}
