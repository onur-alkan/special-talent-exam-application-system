using System.Collections;
using System.Data;
using System.Data.Common;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using OzelYetenekSinavSistemi.Application.Interfaces.Infrastructure;
using OzelYetenekSinavSistemi.Application.Interfaces.Services;
using OzelYetenekSinavSistemi.Infrastructure.Configuration;
using OzelYetenekSinavSistemi.Infrastructure.Persistence;

namespace OzelYetenekSinavSistemi.Tests.Persistence;

public sealed class DatabaseSeederTests
{
    [Fact]
    public async Task SeedAsync_CreatesSuperAdmin_WithoutLoggingSensitiveData()
    {
        var store = new SeedStore();
        var logger = new CapturingLogger<DatabaseSeeder>();
        var options = new SeedOptions
        {
            SuperAdminPassword = "ComplexPass!23",
            SuperAdminTcNo = "11111111110",
            SuperAdminEmail = "admin@example.com",
            SeedTestData = false
        };

        var factory = new Mock<IDbConnectionFactory>();
        factory.Setup(f => f.CreateOpenConnectionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new SeedFakeConnection(store));
        var passwordService = new Mock<IPasswordService>();
        passwordService.Setup(p => p.Hash(It.IsAny<string>())).Returns("hashed-password");

        var sut = new DatabaseSeeder(factory.Object, passwordService.Object, Options.Create(options), logger);
        await sut.SeedAsync();

        Assert.True(store.SuperAdminExists);
        var joined = string.Join('\n', logger.Messages);
        Assert.Contains("Varsayılan SuperAdmin kullanıcısı oluşturuldu", joined, StringComparison.Ordinal);
        Assert.DoesNotContain("11111111110", joined);
        Assert.DoesNotContain("admin@example.com", joined, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ComplexPass!23", joined, StringComparison.Ordinal);
        Assert.DoesNotContain("hashed-password", joined, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SeedAsync_SuperAdminMissing_EmptyPassword_Throws()
    {
        var store = new SeedStore();
        var sut = CreateSut(store, new SeedOptions { SuperAdminPassword = string.Empty, SeedTestData = false });

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => sut.SeedAsync());

        Assert.Contains("Seed:SuperAdminPassword", ex.Message, StringComparison.Ordinal);
        Assert.False(store.SuperAdminExists);
        Assert.DoesNotContain(store.ExecutedInserts, sql => sql.Contains("INSERT INTO dbo.Users", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task SeedAsync_SuperAdminExists_EmptyPassword_DoesNotThrow()
    {
        var store = new SeedStore { SuperAdminExists = true };
        var passwordService = new Mock<IPasswordService>(MockBehavior.Strict);
        var sut = CreateSut(store, new SeedOptions { SuperAdminPassword = string.Empty, SeedTestData = false }, passwordService);

        await sut.SeedAsync();

        passwordService.Verify(p => p.Hash(It.IsAny<string>()), Times.Never);
        Assert.DoesNotContain(store.ExecutedInserts, sql => sql.Contains("dbo.Users", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task SeedAsync_SeedTestDataFalse_DoesNotCreateTestExamPeriod()
    {
        var store = new SeedStore { SuperAdminExists = true };
        var sut = CreateSut(store, new SeedOptions { SuperAdminPassword = string.Empty, SeedTestData = false });

        await sut.SeedAsync();

        Assert.False(store.ExamPeriodExists);
        Assert.DoesNotContain(store.ExecutedInserts, sql => sql.Contains("dbo.ExamPeriods", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task SeedAsync_SeedTestDataTrue_CreatesTestExamPeriodIdempotently()
    {
        var store = new SeedStore { SuperAdminExists = true };
        var sut = CreateSut(store, new SeedOptions { SuperAdminPassword = string.Empty, SeedTestData = true });

        await sut.SeedAsync();
        Assert.True(store.ExamPeriodExists);
        Assert.Contains(store.ExecutedInserts, sql => sql.Contains("dbo.ExamPeriods", StringComparison.OrdinalIgnoreCase));

        var insertCountAfterFirst = store.ExecutedInserts.Count(sql => sql.Contains("dbo.ExamPeriods", StringComparison.OrdinalIgnoreCase));

        await sut.SeedAsync();
        var insertCountAfterSecond = store.ExecutedInserts.Count(sql => sql.Contains("dbo.ExamPeriods", StringComparison.OrdinalIgnoreCase));

        Assert.Equal(insertCountAfterFirst, insertCountAfterSecond);
        Assert.True(store.ExamPeriodExists);
    }

    private static DatabaseSeeder CreateSut(
        SeedStore store,
        SeedOptions options,
        Mock<IPasswordService>? passwordService = null)
    {
        var factory = new Mock<IDbConnectionFactory>();
        factory.Setup(f => f.CreateOpenConnectionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new SeedFakeConnection(store));

        passwordService ??= new Mock<IPasswordService>();
        passwordService.Setup(p => p.Hash(It.IsAny<string>())).Returns("hashed-password");

        return new DatabaseSeeder(
            factory.Object,
            passwordService.Object,
            Options.Create(options),
            NullLogger<DatabaseSeeder>.Instance);
    }

    private sealed class SeedStore
    {
        public bool SuperAdminExists { get; set; }
        public bool ExamPeriodExists { get; set; }
        public List<string> ExecutedInserts { get; } = new();
    }

    /// <summary>
    /// Dapper'ın IDbConnection üzerinden çalıştığı minimal sahte bağlantı.
    /// Yalnızca seed SQL'lerindeki EXISTS kontrolü ve INSERT yollarını simüle eder.
    /// </summary>
#pragma warning disable CS8765 // Fake DbConnection/DbCommand overrides intentionally accept null assignment from ADO.NET.
    private sealed class SeedFakeConnection : DbConnection
    {
        private readonly SeedStore _store;
        private ConnectionState _state = ConnectionState.Open;

        public SeedFakeConnection(SeedStore store) => _store = store;

        public override string ConnectionString
        {
            get => "SeedFake";
            set { }
        }
        public override string Database => "SeedFake";
        public override string DataSource => "SeedFake";
        public override string ServerVersion => "1.0";
        public override ConnectionState State => _state;

        public override void ChangeDatabase(string databaseName) { }
        public override void Close() => _state = ConnectionState.Closed;
        public override void Open() => _state = ConnectionState.Open;
        protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel) =>
            throw new NotSupportedException();
        protected override DbCommand CreateDbCommand() => new SeedFakeCommand(_store);

        private sealed class SeedFakeCommand : DbCommand
        {
            private readonly SeedStore _store;
            private readonly SeedFakeParameterCollection _parameters = new();
            private string _commandText = string.Empty;

            public SeedFakeCommand(SeedStore store) => _store = store;

            public override string CommandText
            {
                get => _commandText;
                set => _commandText = value ?? string.Empty;
            }
            public override int CommandTimeout { get; set; }
            public override CommandType CommandType { get; set; } = CommandType.Text;
            public override bool DesignTimeVisible { get; set; }
            public override UpdateRowSource UpdatedRowSource { get; set; }
            protected override DbConnection? DbConnection { get; set; }
            protected override DbParameterCollection DbParameterCollection => _parameters;
            protected override DbTransaction? DbTransaction { get; set; }

            public override void Cancel() { }
            public override int ExecuteNonQuery()
            {
                var sql = CommandText ?? string.Empty;

                if (sql.Contains("INSERT INTO dbo.Roles", StringComparison.OrdinalIgnoreCase)
                    || sql.Contains("INSERT INTO dbo.Users", StringComparison.OrdinalIgnoreCase)
                    || sql.Contains("INSERT INTO dbo.ExamPeriods", StringComparison.OrdinalIgnoreCase))
                {
                    _store.ExecutedInserts.Add(sql);

                    if (sql.Contains("INSERT INTO dbo.Users", StringComparison.OrdinalIgnoreCase))
                        _store.SuperAdminExists = true;

                    if (sql.Contains("INSERT INTO dbo.ExamPeriods", StringComparison.OrdinalIgnoreCase))
                        _store.ExamPeriodExists = true;
                }

                return 1;
            }

            public override object? ExecuteScalar()
            {
                var sql = CommandText ?? string.Empty;

                if (sql.Contains("dbo.Users", StringComparison.OrdinalIgnoreCase))
                    return _store.SuperAdminExists ? 1 : 0;

                if (sql.Contains("dbo.ExamPeriods", StringComparison.OrdinalIgnoreCase))
                    return _store.ExamPeriodExists ? 1 : 0;

                return 0;
            }

            public override void Prepare() { }
            protected override DbParameter CreateDbParameter() => new SeedFakeParameter();
            protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior) =>
                throw new NotSupportedException();
        }

        private sealed class SeedFakeParameter : DbParameter
        {
            private string _parameterName = string.Empty;
            private string _sourceColumn = string.Empty;

            public override DbType DbType { get; set; }
            public override ParameterDirection Direction { get; set; } = ParameterDirection.Input;
            public override bool IsNullable { get; set; }
            public override string ParameterName
            {
                get => _parameterName;
                set => _parameterName = value ?? string.Empty;
            }
            public override int Size { get; set; }
            public override string SourceColumn
            {
                get => _sourceColumn;
                set => _sourceColumn = value ?? string.Empty;
            }
            public override bool SourceColumnNullMapping { get; set; }
            public override object? Value { get; set; }
            public override void ResetDbType() { }
        }

        private sealed class SeedFakeParameterCollection : DbParameterCollection
        {
            private readonly List<DbParameter> _items = new();

            public override int Count => _items.Count;
            public override object SyncRoot => ((ICollection)_items).SyncRoot;

            public override int Add(object value)
            {
                _items.Add((DbParameter)value);
                return _items.Count - 1;
            }

            public override void AddRange(Array values)
            {
                foreach (var value in values)
                    Add(value!);
            }

            public override void Clear() => _items.Clear();
            public override bool Contains(object value) => _items.Contains((DbParameter)value);
            public override bool Contains(string value) => IndexOf(value) >= 0;
            public override void CopyTo(Array array, int index) => ((ICollection)_items).CopyTo(array, index);
            public override IEnumerator GetEnumerator() => _items.GetEnumerator();
            public override int IndexOf(object value) => _items.IndexOf((DbParameter)value);
            public override int IndexOf(string parameterName) =>
                _items.FindIndex(p => string.Equals(p.ParameterName, parameterName, StringComparison.OrdinalIgnoreCase));
            public override void Insert(int index, object value) => _items.Insert(index, (DbParameter)value);
            public override void Remove(object value) => _items.Remove((DbParameter)value);
            public override void RemoveAt(int index) => _items.RemoveAt(index);
            public override void RemoveAt(string parameterName)
            {
                var index = IndexOf(parameterName);
                if (index >= 0)
                    RemoveAt(index);
            }

            protected override DbParameter GetParameter(int index) => _items[index];
            protected override DbParameter GetParameter(string parameterName)
            {
                var index = IndexOf(parameterName);
                if (index < 0)
                    throw new IndexOutOfRangeException(parameterName);
                return _items[index];
            }

            protected override void SetParameter(int index, DbParameter value) => _items[index] = value;
            protected override void SetParameter(string parameterName, DbParameter value)
            {
                var index = IndexOf(parameterName);
                if (index < 0)
                    Add(value);
                else
                    _items[index] = value;
            }
        }
    }
#pragma warning restore CS8765

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public List<string> Messages { get; } = new();

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            Messages.Add(formatter(state, exception));
        }
    }
}
