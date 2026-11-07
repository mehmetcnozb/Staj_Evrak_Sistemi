using StajEvrakSistemi.Models;

namespace StajEvrakSistemi.Core;

public static class EngineerApprovalValidator
{
    public static bool ShouldAskGraduationDepartment(int engineerCount) => engineerCount is >= 1 and < 5;

    public static IReadOnlyList<string> GetEngineerErrors(InternshipApplication application)
    {
        ArgumentNullException.ThrowIfNull(application);
        var errors = new List<string>();

        if (application.EngineerCount < 1)
        {
            errors.Add("Isyerinde en az bir muhendis bulunmali.");
            return errors;
        }

        return errors;
    }

    // Bu uyari onayi engellemez. Arayuz bolum bilgisini sorarken kullanabilir.
    public static IReadOnlyList<string> GetEngineerWarnings(InternshipApplication application)
    {
        ArgumentNullException.ThrowIfNull(application);
        if (ShouldAskGraduationDepartment(application.EngineerCount)
            && string.IsNullOrWhiteSpace(application.EngineerGraduationDepartment))
            return new[] { "Muhendis sayisi 5'in altinda. Muhendisin mezun oldugu bolumu belirtiniz." };
        return Array.Empty<string>();
    }

    public static IReadOnlyList<string> GetDocumentApprovalErrors(DocumentApprovalFields fields)
    {
        ArgumentNullException.ThrowIfNull(fields);
        var errors = new List<string>();
        CheckField(errors, fields.StudentApprovalPresent, false, "Ogrenci onay alani");
        CheckField(errors, fields.AdvisorApprovalPresent, false, "Staj danismani onay alani");
        CheckField(errors, fields.EmployerFieldsComplete, true, "Isveren bolumu");
        CheckField(errors, fields.EmployerStampPresent, true, "Isveren kasesi");
        CheckField(errors, fields.EmployerSignaturePresent, true, "Isveren imzasi");
        return errors;
    }

    private static void CheckField(List<string> errors, bool? present, bool expected, string label)
    {
        if (!present.HasValue)
            errors.Add($"{label} henuz incelenmedi.");
        else if (present.Value != expected)
            errors.Add(expected ? $"{label} dolu olmali." : $"{label} bos olmali.");
    }
}
