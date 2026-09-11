namespace OzelYetenekSinavSistemi.Application.Validation;

public static class ExamResultValidationMessages
{
    public const string InvalidAttendanceStatus = "Geçerli bir sınava girme durumu seçiniz.";

    public const string ScoreNotAllowed =
        "Sınava girmedi, iptal veya diskalifiye durumundaki aday için puan girilemez.";
}
