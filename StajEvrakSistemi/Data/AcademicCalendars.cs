using StajEvrakSistemi.Core;
using StajEvrakSistemi.Models;

namespace StajEvrakSistemi.Data;

public static class AcademicCalendars
{
    // Ogretim yili siniri proje icin secilen uygulama kuralidir.
    public static AcademicCalendar Current { get; } = new(
        academicYear: "2026-2027",
        academicYearStartDate: new DateTime(2026, 9, 1),
        academicYearEndDate: new DateTime(2027, 8, 31));

    public static AcademicCalendar GetForAcademicYear(string? academicYear)
    {
        if (!InternshipDateValidator.IsAcademicYearValid(academicYear))
            throw new ArgumentException("Ogretim yili ardisik iki yildan olusmali.", nameof(academicYear));

        string value = academicYear!.Trim();
        return value == Current.AcademicYear ? Current : new AcademicCalendar(value);
    }
}
