using System.Globalization;
using StajEvrakSistemi.Models;

namespace StajEvrakSistemi.Core;

public static class ContactDocumentValidator
{
    private static readonly CultureInfo Turkish = CultureInfo.GetCultureInfo("tr-TR");

    public static IReadOnlyList<Ek16Discrepancy> GetApplicationErrors(InternshipApplication application)
    {
        ArgumentNullException.ThrowIfNull(application);
        var errors = new List<Ek16Discrepancy>();
        CheckFormat(errors, Ek16Field.Address, application.Address, "Basvurudaki", isApplication: true);
        CheckFormat(errors, Ek16Field.Phone, application.Phone, "Basvurudaki", isApplication: true);
        CheckFormat(errors, Ek16Field.Email, application.Email, "Basvurudaki", isApplication: true);
        CheckFormat(errors, Ek16Field.Fax, application.Fax, "Basvurudaki", isApplication: true);
        return errors;
    }

    public static IReadOnlyList<Ek16Discrepancy> Compare(InternshipApplication application, ScannedDocumentFields scanned)
    {
        ArgumentNullException.ThrowIfNull(scanned);
        var errors = GetApplicationErrors(application).ToList();
        foreach (var (field, form, document) in new[]
        {
            (Ek16Field.Address, application.Address, scanned.Address),
            (Ek16Field.Phone, application.Phone, scanned.Phone),
            (Ek16Field.Email, application.Email, scanned.Email),
            (Ek16Field.Fax, application.Fax, scanned.Fax)
        })
        {
            CheckFormat(errors, field, document, "Belgedeki");
            CompareValue(errors, field, form, document, "belge");
        }
        return errors;
    }

    public static IReadOnlyList<Ek16Discrepancy> CompareWithReference(
        InternshipApplication application, OfficialStudentRecord reference)
    {
        ArgumentNullException.ThrowIfNull(application);
        ArgumentNullException.ThrowIfNull(reference);
        var errors = new List<Ek16Discrepancy>();
        foreach (var (field, form, official) in new[]
        {
            (Ek16Field.Address, application.Address, reference.Address),
            (Ek16Field.Phone, application.Phone, reference.Phone),
            (Ek16Field.Email, application.Email, reference.Email)
        })
        {
            // Eski veri setlerinde bulunmayan alan icin bir deger uretmeyiz.
            if (string.IsNullOrWhiteSpace(official))
                continue;
            CheckFormat(errors, field, official, "Veri setindeki");
            CompareValue(errors, field, form, official, "veri seti");
        }
        return errors;
    }

    private static void CheckFormat(List<Ek16Discrepancy> errors, Ek16Field field, string? value, string source,
        bool isApplication = false)
    {
        bool valid = field switch
        {
            Ek16Field.Address => FormatValidator.IsValidAddress(value),
            Ek16Field.Phone => FormatValidator.IsValidTurkishPhone(value),
            Ek16Field.Email => FormatValidator.IsValidEmail(value),
            Ek16Field.Fax => FormatValidator.IsValidOptionalFax(value),
            _ => false
        };
        if (valid)
            return;
        string rule = field switch
        {
            Ek16Field.Address => "adres mahalle, cadde/sokak, bina no, daire no ve ilce/il sirasiyla tam olmali",
            Ek16Field.Phone => "telefon zorunlu ve +90 xxx xxx xx xx biciminde olmali",
            Ek16Field.Email => "e-posta zorunlu ve gecerli bicimde olmali",
            _ => "faks bos birakilabilir; girilmisse +90 xxx xxx xx xx biciminde olmali"
        };
        errors.Add(new(field, isApplication ? value : null, isApplication ? null : value, $"{source} {rule}."));
    }

    private static void CompareValue(List<Ek16Discrepancy> errors, Ek16Field field,
        string? application, string? other, string source)
    {
        if (Normalize(field, application) != Normalize(field, other))
            errors.Add(new(field, application, other, $"{Label(field)} form ile {source} arasinda uyusmuyor."));
    }

    private static string Normalize(Ek16Field field, string? value)
    {
        string text = value?.Trim() ?? string.Empty;
        if (field == Ek16Field.Email)
            return text.ToUpperInvariant();
        if (field == Ek16Field.Address)
            return string.Join(' ', text.Replace(',', ' ').Split((char[]?)null,
                StringSplitOptions.RemoveEmptyEntries)).ToUpper(Turkish);
        return text;
    }

    private static string Label(Ek16Field field) => field switch
    {
        Ek16Field.Address => "Adres", Ek16Field.Phone => "Telefon", Ek16Field.Email => "E-posta", _ => "Faks"
    };
}
