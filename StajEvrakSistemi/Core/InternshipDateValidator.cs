using System.Globalization;
using StajEvrakSistemi.Models;

namespace StajEvrakSistemi.Core;

public static class InternshipDateValidator
{
    public const int MinimumLeadDays = 30;
    private static readonly TimeZoneInfo TurkeyTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Istanbul");

    // Sistem UTC zamanini Turkiye'deki takvim tarihine cevirir.
    public static DateTime ToTurkeyDate(DateTime utcTime) => TimeZoneInfo.ConvertTimeFromUtc(
        DateTime.SpecifyKind(utcTime, DateTimeKind.Utc), TurkeyTimeZone).Date;

    public static IReadOnlyList<string> GetDocumentDeliveryErrors(DateTime? uploadedAtUtc, DateTime internshipStartDate)
    {
        if (!uploadedAtUtc.HasValue)
            return new[] { "Belgenin kayitli teslim tarihi bulunamadi veya okunamadi." };
        if (internshipStartDate == default)
            return new[] { "Staj baslangic tarihi girilmeli." };

        return GetLeadTimeErrors(ToTurkeyDate(uploadedAtUtc.Value), internshipStartDate);
    }

    // Kayit akisinin kullanacagi ortak tarih kontrolleri.
    public static IReadOnlyList<string> GetApplicationErrors(
        InternshipApplication application, AcademicCalendar calendar)
    {
        ArgumentNullException.ThrowIfNull(application);
        ArgumentNullException.ThrowIfNull(calendar);
        var errors = new List<string>();

        if (!IsAcademicYearValid(application.AcademicYear))
            errors.Add("Ogretim yili 2026-2027 gibi ardisik iki yildan olusmali.");
        else if (!MatchesAcademicCalendar(application.AcademicYear, calendar))
            errors.Add($"Bu ogretim yili icin akademik takvim tanimli degil. Desteklenen yil: {calendar.AcademicYear}.");

        if (NormalizeSemester(application.Semester) is null)
            errors.Add("Teslim edilen yariyil Guz veya Bahar olarak girilmeli.");

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

        if (MatchesAcademicCalendar(application.AcademicYear, calendar)
            && !IsWithinAcademicYear(application.StartDate, application.EndDate, calendar))
            errors.Add($"Staj tarihleri {calendar.AcademicYearStartDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}"
                + $" ile {calendar.AcademicYearEndDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)} arasinda olmali.");

        return errors;
    }

    public static string? NormalizeSemester(string? semester)
    {
        string? value = semester?.Trim();
        if (string.Equals(value, "Guz", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "G\u00fcz", StringComparison.OrdinalIgnoreCase))
            return "Guz";
        if (string.Equals(value, "Bahar", StringComparison.OrdinalIgnoreCase))
            return "Bahar";
        return null;
    }

    // Stajin iki ucu da secilen ogretim yili icinde olmali. Sinir gunleri dahildir.
    public static bool IsWithinAcademicYear(DateTime startDate, DateTime endDate, AcademicCalendar calendar)
    {
        ArgumentNullException.ThrowIfNull(calendar);
        return startDate.Date <= endDate.Date
            && startDate.Date >= calendar.AcademicYearStartDate
            && endDate.Date <= calendar.AcademicYearEndDate;
    }

    private static IReadOnlyList<string> GetLeadTimeErrors(DateTime submissionDate, DateTime internshipStartDate)
    {
        if (IsSubmissionOnTime(submissionDate, internshipStartDate))
            return Array.Empty<string>();

        string message = $"Teslim staj baslangicindan en az {MinimumLeadDays} takvim gunu once yapilmali.";
        if (internshipStartDate.Date >= DateTime.MinValue.AddDays(MinimumLeadDays))
            message += $" Son teslim tarihi: {GetLatestSubmissionDate(internshipStartDate).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}.";
        return new[] { message };
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

    // Belgenin teslimi ile staj baslangici arasinda en az 30 takvim gunu olmali.
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
}
