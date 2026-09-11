using OzelYetenekSinavSistemi.Application.DTOs.Applications;

namespace OzelYetenekSinavSistemi.Application.Common;

/// <summary>
/// Değerlendirme/sonuç listesi ve Excel/PDF export için ortak puan sıralaması.
/// Puan DESC, eşitlikte aday numarası ASC; puanı olmayanlar sonda.
/// </summary>
public static class CandidateResultOrdering
{
    /// <summary>
    /// DataTables data-order için culture-independent sayısal değer.
    /// Görünen metin Türkçe biçimli kalabilir; bu değer sıralama içindir.
    /// Puan yoksa geçerli puan aralığının altında bir anahtar kullanılır (sahte puan değildir).
    /// </summary>
    public const string MissingScoreSortKey = "-1";

    public static IReadOnlyList<CandidateApplicationDetail> OrderByExamScoreDescending(
        IEnumerable<CandidateApplicationDetail> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);

        return rows
            .OrderByDescending(r => r.ExamScore.HasValue)
            .ThenByDescending(r => r.ExamScore)
            .ThenBy(r => r.CandidateNo)
            .ToList();
    }

    public static string ToDataTablesOrderValue(decimal? examScore) =>
        examScore.HasValue
            ? examScore.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)
            : MissingScoreSortKey;
}
