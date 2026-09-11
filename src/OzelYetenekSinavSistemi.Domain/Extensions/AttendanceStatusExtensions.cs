using OzelYetenekSinavSistemi.Domain.Enums;

namespace OzelYetenekSinavSistemi.Domain.Extensions;

public static class AttendanceStatusExtensions
{
    public static string ToDisplayText(this AttendanceStatus status) => status switch
    {
        AttendanceStatus.Attended => "Girdi",
        AttendanceStatus.NotAttended => "Girmedi",
        AttendanceStatus.Cancelled => "İptal",
        AttendanceStatus.Disqualified => "Diskalifiye",
        _ => "-"
    };

    public static string ToDisplayText(this AttendanceStatus? status) =>
        status.HasValue ? status.Value.ToDisplayText() : "-";
}
