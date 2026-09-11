using System.Data;
using Dapper;
using Microsoft.Data.SqlClient;
using OzelYetenekSinavSistemi.Infrastructure.Persistence;

namespace OzelYetenekSinavSistemi.Tests.Persistence;

public sealed class DapperDateOnlyTypeHandlerTests
{
    public DapperDateOnlyTypeHandlerTests()
    {
        DapperTypeHandlers.EnsureRegistered();
    }

    [Fact]
    public void DateOnlyHandler_IsRegistered_AndSerializesToDbDate()
    {
        Assert.True(SqlMapper.HasTypeHandler(typeof(DateOnly)));

        var handler = new DateOnlyTypeHandler();
        IDbDataParameter parameter = new SqlParameter();
        var value = new DateOnly(2030, 6, 15);

        handler.SetValue(parameter, value);

        Assert.Equal(DbType.Date, parameter.DbType);
        Assert.Equal(new DateTime(2030, 6, 15), parameter.Value);
    }

    [Fact]
    public void DateOnlyHandler_ParsesDateTimeAndDateOnly()
    {
        var handler = new DateOnlyTypeHandler();

        Assert.Equal(new DateOnly(2030, 6, 15), handler.Parse(new DateTime(2030, 6, 15)));
        Assert.Equal(new DateOnly(2030, 6, 15), handler.Parse(new DateOnly(2030, 6, 15)));
    }

    [Fact]
    public void EnsureRegistered_IsIdempotent()
    {
        DapperTypeHandlers.EnsureRegistered();
        DapperTypeHandlers.EnsureRegistered();
        Assert.True(SqlMapper.HasTypeHandler(typeof(DateOnly)));
    }
}
