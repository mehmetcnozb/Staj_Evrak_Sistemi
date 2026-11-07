using System.Globalization;
using StajEvrakSistemi.Models;

namespace StajEvrakSistemi.Core;

public static class InternshipDateValidator
{
    public const int MinimumLeadDays = 30;

    // Kayit akisinin kullanacagi ortak tarih kontrolleri.
    public static IReadOnlyList<string> GetApplicationErrors(
        InternshipApplication application, DateTime submissionDate, AcademicCalendar calendar)
    {
        ArgumentNullException.ThrowIfNull(application);
        ArgumentNullException.ThrowIfNull(calendar);
        var errors = new List<string>();

        if (!IsAcademicYearValid(application.AcademicYear))
            errors.Add("Ogretim yili 2026-2027 gibi ardisik iki yildan olusmali.");
        else if (!MatchesAcademicCalendar(application.AcademicYear, calendar))
            errors.Add($"Bu ogretim yili icin akademik takvim tanimli degil. Desteklenen yil: {calendar.AcademicYear}.");

        if (application.StartDate == default || application.EndDate == default)
        {
            errors.Add("Staj baslangic ve bitis tarihleri girilmeli.");
            return errors;
        }
        if (application.EndDate.Date < application.StartDate.Date)
        {
            errors.Add("Staj bitisi baslangictan once olamaz.");
            return errors;
        }

        if (!IsSubmissionOnTime(submissionDate, application.StartDate))
        {
            string message = $"Basvuru staj baslangicindan en az {MinimumLeadDays} takvim gunu once yapilmali.";
            if (application.StartDate.Date >= DateTime.MinValue.AddDays(MinimumLeadDays))
                message += $" Son teslim tarihi: {GetLatestSubmissionDate(application.StartDate).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}.";
            errors.Add(message);
        }

        // Baska bir yilin dersleri secilen takvimle kontrol edilemez.
        if (MatchesAcademicCalendar(application.AcademicYear, calendar)
            && HasClassPeriodConflict(application.StartDate, application.EndDate, calendar))
            errors.Add("Staj tarihleri akademik takvimdeki guz veya bahar ders donemiyle cakisiyor.");

        return errors;
    }

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
