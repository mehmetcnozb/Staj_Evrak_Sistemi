using StajEvrakSistemi.Core;

namespace StajEvrakSistemi.Models;

public sealed class AcademicCalendar
{
    public string AcademicYear { get; }
    public DateTime AcademicYearStartDate { get; }
    public DateTime AcademicYearEndDate { get; }

    public AcademicCalendar(
        string academicYear,
        DateTime? academicYearStartDate = null,
        DateTime? academicYearEndDate = null)
    {
        if (!InternshipDateValidator.IsAcademicYearValid(academicYear))
            throw new ArgumentException("Ogretim yili 2026-2027 gibi ardisik iki yildan olusmali.", nameof(academicYear));

        if (academicYearStartDate.HasValue != academicYearEndDate.HasValue)
            throw new ArgumentException("Ogretim yili sinirlari birlikte verilmeli.");

        int firstYear = int.Parse(academicYear.Trim().AsSpan(0, 4), System.Globalization.CultureInfo.InvariantCulture);
        // Sinir verilmezse uygulamada 1 Eylul - 31 Agustos araligi kullanilir.
        DateTime yearStart = academicYearStartDate?.Date ?? new DateTime(firstYear, 9, 1);
        DateTime yearEnd = academicYearEndDate?.Date ?? new DateTime(firstYear + 1, 8, 31);
        if (yearStart.Year != firstYear || yearEnd.Year != firstYear + 1 || yearEnd < yearStart)
            throw new ArgumentException("Ogretim yili sinirlari yil bilgisiyle uyumlu olmali.");

        AcademicYear = academicYear.Trim();
        AcademicYearStartDate = yearStart;
        AcademicYearEndDate = yearEnd;
    }
}
