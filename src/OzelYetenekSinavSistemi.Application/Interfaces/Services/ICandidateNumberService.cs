using System.Data;

namespace OzelYetenekSinavSistemi.Application.Interfaces.Services;

/// <summary>
/// Sınav dönemi bazında atomik ve ardışık CandidateNo üretimi.
/// Var olan bir transaction içinde çağrılmalıdır (UPDLOCK/HOLDLOCK ile).
/// </summary>
public interface ICandidateNumberService
{
    Task<int> GenerateNextAsync(
        IDbConnection connection,
        IDbTransaction transaction,
        Guid examPeriodId,
        CancellationToken cancellationToken = default);
}
