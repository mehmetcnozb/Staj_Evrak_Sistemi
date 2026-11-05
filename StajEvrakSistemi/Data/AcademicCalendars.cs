using StajEvrakSistemi.Models;

namespace StajEvrakSistemi.Data;

public static class AcademicCalendars
{
    // Kocaeli Universitesi 2026-2027 genel onlisans ve lisans ders tarihleri.
    // Kaynak: https://api.kocaeli.edu.tr/api/FileOperation/OpenFile?filePath=1084%2FDocument%2FFiles%2F2026%2F06%2F20260611_d1fdf_akademik-takvims2ilan-senato2026-7-052.pdf
    public static AcademicCalendar Kocaeli2026_2027 { get; } = new(
        academicYear: "2026-2027",
        fallStartDate: new DateTime(2026, 9, 21),
        fallEndDate: new DateTime(2026, 12, 25),
        springStartDate: new DateTime(2027, 2, 8),
        springEndDate: new DateTime(2027, 5, 28));
}
