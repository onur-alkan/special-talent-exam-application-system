namespace OzelYetenekSinavSistemi.Application.Services;

/// <summary>
/// Sınava giriş belgesi erişim zaman kapısı.
/// Başvuru bitiş anı (ExamPeriod.EndDate) itibarıyla belge adaylara açılır.
/// </summary>
public static class ExamEntranceDocumentAccess
{
    public const string NotYetAvailableMessage =
        "Sınava giriş belgesi, başvuru süresi sona erdikten sonra erişime açılacaktır.";

    /// <summary>Herkese açık belge doğrulama sayfasında erken erişim mesajı.</summary>
    public const string VerificationNotYetAvailableMessage =
        "Sınava giriş belgesi henüz erişime açılmamıştır.";

    /// <summary>Aday detayında doğrulama kodu/QR gizlenirken gösterilen açıklama.</summary>
    public const string VerificationDetailsHiddenMessage =
        "Belge doğrulama bilgileri, başvuru süresi sona erdikten sonra görüntülenecektir.";

    /// <summary>
    /// Erişim: <paramref name="currentTime"/> >= <paramref name="applicationEndDateTime"/>.
    /// Tam bitiş anında açık; bir saniye önce kapalıdır.
    /// </summary>
    public static bool IsAvailable(DateTime applicationEndDateTime, DateTime currentTime) =>
        currentTime >= applicationEndDateTime;

    public static bool IsAvailable(DateTime applicationEndDateTime, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        // ExamPeriod.EndDate ve SYSDATETIME()/DateTime.Now ile aynı yerel saat politikası.
        var currentLocal = timeProvider.GetLocalNow().DateTime;
        return IsAvailable(applicationEndDateTime, currentLocal);
    }
}
