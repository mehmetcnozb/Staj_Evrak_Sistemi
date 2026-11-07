using System.Globalization;
using StajEvrakSistemi.Models;

namespace StajEvrakSistemi.Core;

public static class ApplicationDocumentValidator
{
    private static readonly CultureInfo Turkish = CultureInfo.GetCultureInfo("tr-TR");

    public static IReadOnlyList<Ek16Discrepancy> GetApplicationErrors(InternshipApplication application)
    {
        ArgumentNullException.ThrowIfNull(application);
        var errors = new List<Ek16Discrepancy>();
        if (InternshipDateValidator.NormalizeSemester(application.Semester) is null)
            errors.Add(new(Ek16Field.Semester, application.Semester, null, "Basvurunun yariyili Guz veya Bahar olmali."));
        if (string.IsNullOrWhiteSpace(application.CompanyName))
            errors.Add(new(Ek16Field.CompanyName, application.CompanyName, null, "Basvuruda isyeri adi bulunmali."));
        foreach (string error in EngineerApprovalValidator.GetEngineerErrors(application))
            errors.Add(new(Ek16Field.EngineerCount, Number(application.EngineerCount), null, error));
        return errors;
    }

    public static IReadOnlyList<Ek16Discrepancy> Compare(InternshipApplication application, ScannedDocumentFields scanned)
    {
        ArgumentNullException.ThrowIfNull(scanned);
        var errors = GetApplicationErrors(application).ToList();
        string? formSemester = InternshipDateValidator.NormalizeSemester(application.Semester);
        string? documentSemester = InternshipDateValidator.NormalizeSemester(scanned.Semester);
        if (documentSemester is null)
            errors.Add(new(Ek16Field.Semester, formSemester, scanned.Semester,
                "Belgenin teslim edildigi yariyil okunamadi veya Guz/Bahar biciminde degil."));
        else if (formSemester is not null && formSemester != documentSemester)
            errors.Add(new(Ek16Field.Semester, formSemester, documentSemester, "Form ve belgedeki yariyil uyusmuyor."));

        if (string.IsNullOrWhiteSpace(scanned.CompanyName))
            errors.Add(new(Ek16Field.CompanyName, application.CompanyName, null, "Isyeri adi belgeden okunamadi."));
        else if (!string.IsNullOrWhiteSpace(application.CompanyName)
            && NormalizeName(application.CompanyName) != NormalizeName(scanned.CompanyName))
            errors.Add(new(Ek16Field.CompanyName, application.CompanyName, scanned.CompanyName,
                "Form ve belgedeki isyeri adi uyusmuyor."));

        // Ayri bir sayi okunmussa denetlenir. Belgede ek bir zorunlu sayi alani tanimlamayiz.
        if (scanned.EngineerCount.HasValue)
        {
            if (scanned.EngineerCount.Value < 1)
                errors.Add(new(Ek16Field.EngineerCount, Number(application.EngineerCount), Number(scanned.EngineerCount.Value),
                    "Belgedeki muhendis sayisi en az bir olmali."));
            else if (scanned.EngineerCount.Value != application.EngineerCount)
                errors.Add(new(Ek16Field.EngineerCount, Number(application.EngineerCount), Number(scanned.EngineerCount.Value),
                    "Form ve belgedeki muhendis sayisi uyusmuyor."));
        }
        return errors;
    }

    private static string NormalizeName(string value) =>
        string.Join(' ', value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).ToUpper(Turkish);

    private static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);
}
