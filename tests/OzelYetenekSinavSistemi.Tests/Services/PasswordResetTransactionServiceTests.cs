using System.Collections;
using System.Collections.Concurrent;
using System.Data;
using System.Data.Common;
using Moq;
using OzelYetenekSinavSistemi.Application.Common;
using OzelYetenekSinavSistemi.Application.Interfaces.Infrastructure;
using OzelYetenekSinavSistemi.Domain.Entities;
using OzelYetenekSinavSistemi.Infrastructure.Services;

namespace OzelYetenekSinavSistemi.Tests.Services;

public sealed class PasswordResetTransactionServiceTests
{
    [Fact]
    public async Task Consume_ValidToken_UpdatesPasswordAndMarksTokenUsedTogether()
    {
        var userId = Guid.NewGuid();
        var tokenId = Guid.NewGuid();
        var store = new ResetStore
        {
            Token = new PasswordResetToken
            {
                Id = tokenId,
                UserId = userId,
                TokenHash = "hash-1",
                ExpiresAt = DateTime.UtcNow.AddHours(1),
                UsedAt = null
            },
            UserExists = true,
            PasswordHash = "old-hash"
        };

        var result = await CreateSut(store).ConsumeTokenAndUpdatePasswordAsync("hash-1", "new-hash");

        Assert.True(result.Success);
        Assert.Equal(userId, result.UserId);
        Assert.Equal("new-hash", store.PasswordHash);
        Assert.False(store.MustChangePassword);
        Assert.NotNull(store.Token!.UsedAt);
        Assert.True(store.Committed);
        Assert.False(store.RolledBack);
    }

    [Fact]
    public async Task Consume_UsedToken_DoesNotChangePassword()
    {
        var store = new ResetStore
        {
            Token = new PasswordResetToken
            {
                Id = Guid.NewGuid(),
                UserId = Guid.NewGuid(),
                TokenHash = "hash-used",
                ExpiresAt = DateTime.UtcNow.AddHours(1),
                UsedAt = DateTime.UtcNow.AddMinutes(-1)
            },
            UserExists = true,
            PasswordHash = "old-hash",
            MustChangePassword = true
        };

        var result = await CreateSut(store).ConsumeTokenAndUpdatePasswordAsync("hash-used", "new-hash");

        Assert.False(result.Success);
        Assert.Equal(PasswordResetConsumeStatus.TokenAlreadyUsed, result.Status);
        Assert.Equal("old-hash", store.PasswordHash);
        Assert.True(store.MustChangePassword);
        Assert.True(store.RolledBack);
    }

    [Fact]
    public async Task Consume_ExpiredToken_DoesNotChangePassword()
    {
        var store = new ResetStore
        {
            Token = new PasswordResetToken
            {
                Id = Guid.NewGuid(),
                UserId = Guid.NewGuid(),
                TokenHash = "hash-expired",
                ExpiresAt = DateTime.UtcNow.AddMinutes(-5),
                UsedAt = null
            },
            UserExists = true,
            PasswordHash = "old-hash",
            MustChangePassword = true
        };

        var result = await CreateSut(store).ConsumeTokenAndUpdatePasswordAsync("hash-expired", "new-hash");

        Assert.False(result.Success);
        Assert.Equal(PasswordResetConsumeStatus.TokenExpired, result.Status);
        Assert.Equal("old-hash", store.PasswordHash);
        Assert.Null(store.Token!.UsedAt);
        Assert.True(store.RolledBack);
    }

    [Fact]
    public async Task Consume_TokenUpdateFails_RollsBackPasswordChange()
    {
        var store = new ResetStore
        {
            Token = new PasswordResetToken
            {
                Id = Guid.NewGuid(),
                UserId = Guid.NewGuid(),
                TokenHash = "hash-fail",
                ExpiresAt = DateTime.UtcNow.AddHours(1),
                UsedAt = null
            },
            UserExists = true,
            PasswordHash = "old-hash",
            MustChangePassword = true,
            FailTokenUpdate = true
        };

        var result = await CreateSut(store).ConsumeTokenAndUpdatePasswordAsync("hash-fail", "new-hash");

        Assert.False(result.Success);
        Assert.Equal(PasswordResetConsumeStatus.TokenUpdateFailed, result.Status);
        Assert.Equal("old-hash", store.PasswordHash);
        Assert.True(store.MustChangePassword);
        Assert.Null(store.Token!.UsedAt);
        Assert.True(store.RolledBack);
        Assert.False(store.Committed);
    }

    [Fact]
    public async Task Consume_ConcurrentRequests_OnlyOneSucceeds()
    {
        var userId = Guid.NewGuid();
        var store = new ResetStore
        {
            Token = new PasswordResetToken
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                TokenHash = "hash-race",
                ExpiresAt = DateTime.UtcNow.AddHours(1),
                UsedAt = null
            },
            UserExists = true,
            PasswordHash = "old-hash",
            HoldLockAfterSelectMs = 80
        };

        var sut = CreateSut(store);
        var task1 = Task.Run(() => sut.ConsumeTokenAndUpdatePasswordAsync("hash-race", "new-hash-1"));
        var task2 = Task.Run(() => sut.ConsumeTokenAndUpdatePasswordAsync("hash-race", "new-hash-2"));

        var results = await Task.WhenAll(task1, task2);

        Assert.Equal(1, results.Count(r => r.Success));
        Assert.Equal(1, results.Count(r => r.Status == PasswordResetConsumeStatus.TokenAlreadyUsed));
        Assert.NotNull(store.Token!.UsedAt);
        Assert.False(store.MustChangePassword);
        Assert.True(store.PasswordHash is "new-hash-1" or "new-hash-2");
    }

    private static PasswordResetTransactionService CreateSut(ResetStore store)
    {
        var factory = new Mock<IDbConnectionFactory>();
        factory.Setup(f => f.CreateOpenConnectionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new ResetFakeConnection(store));
        return new PasswordResetTransactionService(factory.Object);
    }

    private sealed class ResetStore
    {
        private readonly object _gate = new();
        private object? _lockOwner;

        public PasswordResetToken? Token { get; set; }
        public bool UserExists { get; set; }
        public string PasswordHash { get; set; } = string.Empty;
        public bool MustChangePassword { get; set; } = true;
        public bool FailTokenUpdate { get; set; }
        public int HoldLockAfterSelectMs { get; set; }
        public bool Committed { get; set; }
        public bool RolledBack { get; set; }

        public ConcurrentDictionary<object, PendingState> Pending { get; } = new();

        public object Gate => _gate;

        public void AcquireRowLock(object owner)
        {
            lock (_gate)
            {
                while (_lockOwner is not null && !ReferenceEquals(_lockOwner, owner))
                    Monitor.Wait(_gate);

                _lockOwner = owner;
            }
        }

        public void ReleaseRowLock(object owner)
        {
            lock (_gate)
            {
                if (!ReferenceEquals(_lockOwner, owner))
                    return;

                _lockOwner = null;
                Monitor.PulseAll(_gate);
            }
        }

        public sealed class PendingState
        {
            public string? PasswordHash { get; set; }
            public bool? MustChangePassword { get; set; }
            public DateTime? TokenUsedAt { get; set; }
        }
    }

#pragma warning disable CS8765
    private sealed class ResetFakeConnection : DbConnection
    {
        private readonly ResetStore _store;
        private ConnectionState _state = ConnectionState.Open;

        public ResetFakeConnection(ResetStore store) => _store = store;

        public override string ConnectionString
        {
            get => "ResetFake";
            set { }
        }

        public override string Database => "ResetFake";
        public override string DataSource => "ResetFake";
        public override string ServerVersion => "1.0";
        public override ConnectionState State => _state;

        public override void ChangeDatabase(string databaseName) { }
        public override void Close() => _state = ConnectionState.Closed;
        public override void Open() => _state = ConnectionState.Open;

        protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel) =>
            new ResetFakeTransaction(this, _store);

        protected override DbCommand CreateDbCommand() => new ResetFakeCommand(_store, this);

        private sealed class ResetFakeTransaction : DbTransaction
        {
            private readonly ResetStore _store;
            private bool _completed;

            public ResetFakeTransaction(ResetFakeConnection connection, ResetStore store)
            {
                DbConnection = connection;
                _store = store;
                _store.Pending[this] = new ResetStore.PendingState();
            }

            public override IsolationLevel IsolationLevel => IsolationLevel.ReadCommitted;
            protected override DbConnection DbConnection { get; }

            public override void Commit()
            {
                lock (_store.Gate)
                {
                    if (_completed)
                        return;

                    if (_store.Pending.TryRemove(this, out var pending))
                    {
                        if (pending.PasswordHash is not null)
                            _store.PasswordHash = pending.PasswordHash;
                        if (pending.MustChangePassword is not null)
                            _store.MustChangePassword = pending.MustChangePassword.Value;
                        if (pending.TokenUsedAt is not null && _store.Token is not null)
                            _store.Token.UsedAt = pending.TokenUsedAt;
                    }

                    _store.Committed = true;
                    _completed = true;
                }

                _store.ReleaseRowLock(this);
            }

            public override void Rollback()
            {
                lock (_store.Gate)
                {
                    if (_completed)
                        return;

                    _store.Pending.TryRemove(this, out _);
                    _store.RolledBack = true;
                    _completed = true;
                }

                _store.ReleaseRowLock(this);
            }

            protected override void Dispose(bool disposing)
            {
                if (disposing && !_completed)
                    Rollback();
                base.Dispose(disposing);
            }
        }

        private sealed class ResetFakeCommand : DbCommand
        {
            private readonly ResetStore _store;
            private readonly ResetFakeConnection _connection;
            private readonly ResetFakeParameterCollection _parameters = new();
            private string _commandText = string.Empty;

            public ResetFakeCommand(ResetStore store, ResetFakeConnection connection)
            {
                _store = store;
                _connection = connection;
            }

            public override string CommandText
            {
                get => _commandText;
                set => _commandText = value ?? string.Empty;
            }

            public override int CommandTimeout { get; set; }
            public override CommandType CommandType { get; set; } = CommandType.Text;
            public override bool DesignTimeVisible { get; set; }
            public override UpdateRowSource UpdatedRowSource { get; set; }
            protected override DbConnection? DbConnection
            {
                get => _connection;
                set { }
            }

            protected override DbParameterCollection DbParameterCollection => _parameters;
            protected override DbTransaction? DbTransaction { get; set; }

            public override void Cancel() { }
            public override void Prepare() { }
            protected override DbParameter CreateDbParameter() => new ResetFakeParameter();

            public override int ExecuteNonQuery()
            {
                var tx = (ResetFakeTransaction?)DbTransaction
                    ?? throw new InvalidOperationException("Transaction required.");
                var sql = CommandText;
                var pending = _store.Pending[tx];

                if (sql.Contains("UPDATE dbo.Users", StringComparison.OrdinalIgnoreCase))
                {
                    var userId = (Guid)GetRequiredParameter("UserId");
                    if (!_store.UserExists || _store.Token?.UserId != userId)
                        return 0;

                    pending.PasswordHash = (string)GetRequiredParameter("PasswordHash");
                    pending.MustChangePassword = false;
                    return 1;
                }

                if (sql.Contains("UPDATE dbo.PasswordResetTokens", StringComparison.OrdinalIgnoreCase))
                {
                    if (_store.FailTokenUpdate)
                        return 0;

                    var id = (Guid)GetRequiredParameter("Id");
                    if (_store.Token is null || _store.Token.Id != id)
                        return 0;

                    if (_store.Token.UsedAt is not null)
                        return 0;

                    pending.TokenUsedAt = DateTime.UtcNow;
                    return 1;
                }

                return 0;
            }

            public override object? ExecuteScalar() => null;

            protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior)
            {
                var tx = (ResetFakeTransaction?)DbTransaction
                    ?? throw new InvalidOperationException("Transaction required.");
                var sql = CommandText;

                if (!sql.Contains("FROM dbo.PasswordResetTokens", StringComparison.OrdinalIgnoreCase))
                    return new ResetFakeDataReader(null);

                _store.AcquireRowLock(tx);

                if (_store.HoldLockAfterSelectMs > 0)
                    Thread.Sleep(_store.HoldLockAfterSelectMs);

                var tokenHash = (string)GetRequiredParameter("TokenHash");
                PasswordResetToken? token = null;

                lock (_store.Gate)
                {
                    if (_store.Token is not null
                        && string.Equals(_store.Token.TokenHash, tokenHash, StringComparison.Ordinal))
                    {
                        token = CloneToken(_store.Token);
                    }
                }

                return new ResetFakeDataReader(token);
            }

            private object GetRequiredParameter(string name)
            {
                var parameter = _parameters.Find(name)
                    ?? throw new IndexOutOfRangeException(name);
                return parameter.Value ?? throw new InvalidOperationException($"Parameter '{name}' value is null.");
            }

            private static PasswordResetToken CloneToken(PasswordResetToken source) => new()
            {
                Id = source.Id,
                UserId = source.UserId,
                TokenHash = source.TokenHash,
                ExpiresAt = source.ExpiresAt,
                UsedAt = source.UsedAt,
                CreatedDate = source.CreatedDate,
                CreatedIpAddress = source.CreatedIpAddress
            };
        }

        private sealed class ResetFakeDataReader : DbDataReader
        {
            private readonly PasswordResetToken? _token;
            private int _row = -1;

            public ResetFakeDataReader(PasswordResetToken? token) => _token = token;

            public override bool HasRows => _token is not null;
            public override int FieldCount => 7;
            public override bool IsClosed => false;
            public override int RecordsAffected => 0;
            public override int Depth => 0;

            public override string GetName(int ordinal) => ordinal switch
            {
                0 => "Id",
                1 => "UserId",
                2 => "TokenHash",
                3 => "ExpiresAt",
                4 => "UsedAt",
                5 => "CreatedDate",
                6 => "CreatedIpAddress",
                _ => throw new IndexOutOfRangeException()
            };

            public override int GetOrdinal(string name) => name.ToUpperInvariant() switch
            {
                "ID" => 0,
                "USERID" => 1,
                "TOKENHASH" => 2,
                "EXPIRESAT" => 3,
                "USEDAT" => 4,
                "CREATEDDATE" => 5,
                "CREATEDIPADDRESS" => 6,
                _ => throw new IndexOutOfRangeException(name)
            };

            public override bool Read()
            {
                if (_token is null || _row >= 0)
                    return false;
                _row = 0;
                return true;
            }

            public override bool NextResult() => false;
            public override bool IsDBNull(int ordinal) => GetValue(ordinal) is DBNull;

            public override object GetValue(int ordinal)
            {
                if (_token is null || _row != 0)
                    throw new InvalidOperationException();

                return ordinal switch
                {
                    0 => _token.Id,
                    1 => _token.UserId,
                    2 => _token.TokenHash,
                    3 => _token.ExpiresAt,
                    4 => (object?)_token.UsedAt ?? DBNull.Value,
                    5 => _token.CreatedDate,
                    6 => (object?)_token.CreatedIpAddress ?? DBNull.Value,
                    _ => throw new IndexOutOfRangeException()
                };
            }

            public override string GetDataTypeName(int ordinal) => GetFieldType(ordinal).Name;
            public override Type GetFieldType(int ordinal) => ordinal switch
            {
                0 or 1 => typeof(Guid),
                2 or 6 => typeof(string),
                3 or 4 or 5 => typeof(DateTime),
                _ => throw new IndexOutOfRangeException()
            };

            public override int GetValues(object[] values)
            {
                var count = Math.Min(values.Length, FieldCount);
                for (var i = 0; i < count; i++)
                    values[i] = GetValue(i);
                return count;
            }

            public override object this[int ordinal] => GetValue(ordinal);
            public override object this[string name] => GetValue(GetOrdinal(name));

            public override bool GetBoolean(int ordinal) => (bool)GetValue(ordinal);
            public override byte GetByte(int ordinal) => (byte)GetValue(ordinal);
            public override long GetBytes(int ordinal, long dataOffset, byte[]? buffer, int bufferOffset, int length) =>
                throw new NotSupportedException();
            public override char GetChar(int ordinal) => (char)GetValue(ordinal);
            public override long GetChars(int ordinal, long dataOffset, char[]? buffer, int bufferOffset, int length) =>
                throw new NotSupportedException();
            public override Guid GetGuid(int ordinal) => (Guid)GetValue(ordinal);
            public override short GetInt16(int ordinal) => (short)GetValue(ordinal);
            public override int GetInt32(int ordinal) => (int)GetValue(ordinal);
            public override long GetInt64(int ordinal) => (long)GetValue(ordinal);
            public override DateTime GetDateTime(int ordinal) => (DateTime)GetValue(ordinal);
            public override string GetString(int ordinal) => (string)GetValue(ordinal);
            public override decimal GetDecimal(int ordinal) => (decimal)GetValue(ordinal);
            public override double GetDouble(int ordinal) => (double)GetValue(ordinal);
            public override float GetFloat(int ordinal) => (float)GetValue(ordinal);
            public override IEnumerator GetEnumerator() => throw new NotSupportedException();
        }

        private sealed class ResetFakeParameter : DbParameter
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

        private sealed class ResetFakeParameterCollection : DbParameterCollection
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
            public override int IndexOf(string parameterName)
            {
                var normalized = Normalize(parameterName);
                return _items.FindIndex(p => string.Equals(Normalize(p.ParameterName), normalized, StringComparison.OrdinalIgnoreCase));
            }

            public DbParameter? Find(string parameterName)
            {
                var index = IndexOf(parameterName);
                return index >= 0 ? _items[index] : null;
            }

            private static string Normalize(string name) => name.TrimStart('@');

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
}
