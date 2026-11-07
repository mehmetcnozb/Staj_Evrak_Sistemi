using System.Globalization;
using StajEvrakSistemi.Data;
using StajEvrakSistemi.Models;

namespace StajEvrakSistemi.Core;

public static class InternshipDocumentValidator
{
    public static InternshipComparisonResult Compare(InternshipApplication application, ScannedDocumentFields scanned)
    {
        ArgumentNullException.ThrowIfNull(application);
        ArgumentNullException.ThrowIfNull(scanned);
        var errors = new List<Ek16Discrepancy>();

        CheckDate(errors, Ek16Field.StartDate, "Staj baslangici", application.StartDate, scanned.StartDate);
        CheckDate(errors, Ek16Field.EndDate, "Staj bitisi", application.EndDate, scanned.EndDate);
        bool formRangeValid = IsValidRange(application.StartDate, application.EndDate);
        bool documentRangeValid = scanned.StartDate.HasValue && scanned.EndDate.HasValue
            && IsValidRange(scanned.StartDate.Value, scanned.EndDate.Value);
        if (!formRangeValid)
            errors.Add(new(Ek16Field.EndDate, Date(application.EndDate), null, "Basvurunun tarih araligi gecersiz."));
        if (scanned.StartDate.HasValue && scanned.EndDate.HasValue && !documentRangeValid)
            errors.Add(new(Ek16Field.EndDate, null, Date(scanned.EndDate.Value), "Belgenin tarih araligi gecersiz."));

        var formDays = application.WorkDays?.ToHashSet() ?? new HashSet<DayOfWeek>();
        bool formDaysValid = IsValidDays(formDays);
        if (!formDaysValid)
            errors.Add(new(Ek16Field.WorkDays, Days(formDays), null, "Basvuruda gecerli haftalik calisma gunleri secilmeli."));

        // Belgede ayri bir gun secimi yoksa ogrencinin formda sectigi gunler kullanilir.
        var documentDays = scanned.WorkDays?.ToHashSet() ?? formDays;
        bool documentDaysValid = IsValidDays(documentDays);
        if (scanned.WorkDays is not null)
        {
            if (!documentDaysValid)
                errors.Add(new(Ek16Field.WorkDays, Days(formDays), Days(documentDays), "Belgedeki haftalik calisma gunleri gecersiz."));
            else if (formDaysValid && !formDays.SetEquals(documentDays))
                errors.Add(new(Ek16Field.WorkDays, Days(formDays), Days(documentDays), "Form ve belgedeki haftalik calisma gunleri uyusmuyor."));
        }

        decimal? calculated = formRangeValid && formDaysValid
            ? Calculate(errors, application.StartDate, application.EndDate, formDays, "Basvuru") : null;
        decimal? documentCalculated = documentRangeValid && documentDaysValid
            ? Calculate(errors, scanned.StartDate!.Value, scanned.EndDate!.Value, documentDays, "Belge") : null;
        if (calculated.HasValue && documentCalculated.HasValue && calculated.Value != documentCalculated.Value)
            errors.Add(new(Ek16Field.NetWorkDays, Number(calculated.Value), Number(documentCalculated.Value),
                "Form ve belgenin tarih/gun bilgilerinden hesaplanan net staj gunleri uyusmuyor."));

        // PDF bu sayi icin ayri bir zorunlu belge alani tanimlamiyor. Okunmussa karsilastirilir.
        if (scanned.DeclaredWorkDays.HasValue)
        {
            decimal? expected = calculated ?? documentCalculated;
            if (scanned.DeclaredWorkDays.Value < 0)
                errors.Add(new(Ek16Field.NetWorkDays, expected.HasValue ? Number(expected.Value) : null,
                    Number(scanned.DeclaredWorkDays.Value), "Belgedeki staj gunu negatif olamaz."));
            else if (expected.HasValue && !InternshipDayCalculator.MatchesDeclaredDays(expected.Value, scanned.DeclaredWorkDays.Value))
                errors.Add(new(Ek16Field.NetWorkDays, Number(expected.Value), Number(scanned.DeclaredWorkDays.Value),
                    "Belgede belirtilen staj gunu hesaplanan net gunle uyusmuyor."));
        }

        return new InternshipComparisonResult(errors.ToArray(), calculated, documentCalculated);
    }

    private static bool IsValidRange(DateTime start, DateTime end) =>
        start != default && end != default && end.Date >= start.Date;

    private static bool IsValidDays(HashSet<DayOfWeek> days) => days.Count > 0 && days.All(day => Enum.IsDefined(day));

    private static void CheckDate(List<Ek16Discrepancy> errors, Ek16Field field, string label, DateTime form, DateTime? document)
    {
        if (!document.HasValue || document.Value == default)
            errors.Add(new(field, Date(form), null, $"{label} belgeden okunamadi."));
        else if (form == default || form.Date != document.Value.Date)
            errors.Add(new(field, Date(form), Date(document.Value), $"{label} form ve belgede uyusmuyor."));
    }

    private static decimal? Calculate(List<Ek16Discrepancy> errors, DateTime start, DateTime end, HashSet<DayOfWeek> days, string source)
    {
        try
        {
            var holidays = TurkishPublicHolidays.GetForRange(start, end);
            return InternshipDayCalculator.CalculateWorkDays(start, end, days, holidays);
        }
        catch (ArgumentException ex)
        {
            errors.Add(new(Ek16Field.NetWorkDays, null, null, $"{source} icin net gun hesaplanamadi: {ex.Message}"));
            return null;
        }
    }

    private static string? Date(DateTime value) => value == default ? null : value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    private static string Number(decimal value) => value.ToString(CultureInfo.InvariantCulture);
    private static string Days(HashSet<DayOfWeek> days) => string.Join(", ", days.OrderBy(day => day).Select(day => day switch
    {
        DayOfWeek.Monday => "Pazartesi", DayOfWeek.Tuesday => "Sali", DayOfWeek.Wednesday => "Carsamba",
        DayOfWeek.Thursday => "Persembe", DayOfWeek.Friday => "Cuma", DayOfWeek.Saturday => "Cumartesi",
        DayOfWeek.Sunday => "Pazar", _ => "Gecersiz gun"
    }));
}
