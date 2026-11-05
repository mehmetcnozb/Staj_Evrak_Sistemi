using StajEvrakSistemi.Models;

namespace StajEvrakSistemi.Core;

public static class InternshipDateValidator
{
    public const int MinimumLeadDays = 30;

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
