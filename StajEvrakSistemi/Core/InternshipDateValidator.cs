using System.Globalization;
using StajEvrakSistemi.Models;

namespace StajEvrakSistemi.Core;

public static class InternshipDateValidator
{
    public const int MinimumLeadDays = 30;

    // Ogretim yili 2026-2027 biciminde ve ardisik iki yildan olusmali.
    public static bool IsAcademicYearValid(string? academicYear)
    {
        if (string.IsNullOrWhiteSpace(academicYear))
            return false;

        string value = academicYear.Trim();
        if (value.Length != 9 || value[4] != '-')
            return false;

        bool firstYearParsed = int.TryParse(value.AsSpan(0, 4), NumberStyles.None,
            CultureInfo.InvariantCulture, out int firstYear);
        bool secondYearParsed = int.TryParse(value.AsSpan(5, 4), NumberStyles.None,
            CultureInfo.InvariantCulture, out int secondYear);

        return firstYearParsed && secondYearParsed
            && firstYear >= 1 && secondYear == firstYear + 1;
    }

    // Basvurunun ogretim yili secilen takvimin yiliyla ayni olmali.
    public static bool MatchesAcademicCalendar(string? academicYear, AcademicCalendar calendar)
    {
        ArgumentNullException.ThrowIfNull(calendar);

        return IsAcademicYearValid(academicYear)
            && string.Equals(academicYear!.Trim(), calendar.AcademicYear, StringComparison.Ordinal);
    }

    // Basvuru ile staj baslangici arasinda en az 30 takvim gunu olmali.
    public static bool IsSubmissionOnTime(DateTime submissionDate, DateTime internshipStartDate)
    {
        int daysUntilStart = (internshipStartDate.Date - submissionDate.Date).Days;
        return daysUntilStart >= MinimumLeadDays;
    }

    // Staj baslangicina gore son teslim tarihini hesaplar.
    public static DateTime GetLatestSubmissionDate(DateTime internshipStartDate)
    {
        return internshipStartDate.Date.AddDays(-MinimumLeadDays);
    }

    // Staj araliginin guz veya bahar ders donemiyle kesisip kesismedigini kontrol eder.
    public static bool HasClassPeriodConflict(
        DateTime internshipStartDate,
        DateTime internshipEndDate,
        AcademicCalendar calendar)
    {
        ArgumentNullException.ThrowIfNull(calendar);

        if (internshipEndDate.Date < internshipStartDate.Date)
            throw new ArgumentException("Staj bitisi baslangictan once olamaz.", nameof(internshipEndDate));

        // Sinir gunleri de cakisma kontrolune dahildir.
        bool overlapsFall = internshipStartDate.Date <= calendar.FallEndDate
            && internshipEndDate.Date >= calendar.FallStartDate;

        bool overlapsSpring = internshipStartDate.Date <= calendar.SpringEndDate
            && internshipEndDate.Date >= calendar.SpringStartDate;

        return overlapsFall || overlapsSpring;
    }
}
