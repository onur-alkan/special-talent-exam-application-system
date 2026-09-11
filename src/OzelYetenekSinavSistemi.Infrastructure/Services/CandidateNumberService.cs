using System.Data;
using Dapper;
using OzelYetenekSinavSistemi.Application.Interfaces.Services;

namespace OzelYetenekSinavSistemi.Infrastructure.Services;

/// <summary>
/// Sınav dönemi bazında atomik ve ardışık CandidateNo üretir.
/// Sayaç satırı UPDLOCK/HOLDLOCK ile kilitlenerek eşzamanlı başvurularda çakışma önlenir.
/// </summary>
public sealed class CandidateNumberService : ICandidateNumberService
{
    public async Task<int> GenerateNextAsync(
        IDbConnection connection,
        IDbTransaction transaction,
        Guid examPeriodId,
        CancellationToken cancellationToken = default)
    {
        const string sql = @"
DECLARE @next INT;

IF NOT EXISTS (SELECT 1 FROM dbo.ExamPeriodCounters WITH (UPDLOCK, HOLDLOCK) WHERE ExamPeriodId = @ExamPeriodId)
BEGIN
    INSERT INTO dbo.ExamPeriodCounters (ExamPeriodId, LastCandidateNo, UpdatedDate)
    VALUES (@ExamPeriodId, 0, SYSDATETIME());
END

UPDATE dbo.ExamPeriodCounters WITH (UPDLOCK, HOLDLOCK)
SET @next = LastCandidateNo = LastCandidateNo + 1,
    UpdatedDate = SYSDATETIME()
WHERE ExamPeriodId = @ExamPeriodId;

SELECT @next;";

        return await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(sql, new { ExamPeriodId = examPeriodId }, transaction, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
    }
}
