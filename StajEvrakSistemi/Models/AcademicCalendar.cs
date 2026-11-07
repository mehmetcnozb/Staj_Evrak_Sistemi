using StajEvrakSistemi.Core;

namespace StajEvrakSistemi.Models;

public sealed class AcademicCalendar
{
    public string AcademicYear { get; }
    public DateTime AcademicYearStartDate { get; }
    public DateTime AcademicYearEndDate { get; }
    public DateTime FallStartDate { get; }
    public DateTime FallEndDate { get; }
    public DateTime SpringStartDate { get; }
    public DateTime SpringEndDate { get; }

    public AcademicCalendar(
        string academicYear,
        DateTime fallStartDate,
        DateTime fallEndDate,
        DateTime springStartDate,
        DateTime springEndDate,
        DateTime? academicYearStartDate = null,
        DateTime? academicYearEndDate = null)
    {
        if (!InternshipDateValidator.IsAcademicYearValid(academicYear))
            throw new ArgumentException("Ogretim yili 2026-2027 gibi ardisik iki yildan olusmali.", nameof(academicYear));

        if (fallEndDate.Date < fallStartDate.Date)
            throw new ArgumentException("Guz donemi bitisi baslangictan once olamaz.", nameof(fallEndDate));

        if (springEndDate.Date < springStartDate.Date)
            throw new ArgumentException("Bahar donemi bitisi baslangictan once olamaz.", nameof(springEndDate));

        if (springStartDate.Date <= fallEndDate.Date)
            throw new ArgumentException("Bahar donemi guz donemi bittikten sonra baslamali.", nameof(springStartDate));

        if (academicYearStartDate.HasValue != academicYearEndDate.HasValue)
            throw new ArgumentException("Ogretim yili sinirlari birlikte verilmeli.");

        int firstYear = int.Parse(academicYear.Trim().AsSpan(0, 4), System.Globalization.CultureInfo.InvariantCulture);
        // Sinir verilmezse uygulamada 1 Eylul - 31 Agustos araligi kullanilir.
        DateTime yearStart = academicYearStartDate?.Date ?? new DateTime(firstYear, 9, 1);
        DateTime yearEnd = academicYearEndDate?.Date ?? new DateTime(firstYear + 1, 8, 31);
        if (yearStart.Year != firstYear || yearEnd.Year != firstYear + 1 || yearEnd < yearStart)
            throw new ArgumentException("Ogretim yili sinirlari yil bilgisiyle uyumlu olmali.");
        if (fallStartDate.Date < yearStart || springEndDate.Date > yearEnd)
            throw new ArgumentException("Ders donemleri ogretim yili araligi icinde olmali.");

        AcademicYear = academicYear.Trim();
        AcademicYearStartDate = yearStart;
        AcademicYearEndDate = yearEnd;
        FallStartDate = fallStartDate.Date;
        FallEndDate = fallEndDate.Date;
        SpringStartDate = springStartDate.Date;
        SpringEndDate = springEndDate.Date;
    }
}
