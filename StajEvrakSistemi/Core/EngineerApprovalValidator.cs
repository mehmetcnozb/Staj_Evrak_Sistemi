using System.Globalization;
using System.Text;
using StajEvrakSistemi.Models;

namespace StajEvrakSistemi.Core;

public static class EngineerApprovalValidator
{
    public static bool RequiresGraduationDepartment(int engineerCount) => engineerCount is >= 1 and < 5;

    public static bool IsValidEngineerTitle(string? title)
    {
        if (string.IsNullOrWhiteSpace(title))
            return false;

        // Turkce harfleri sadelestirerek unvandaki muhendis ifadesini kontrol eder.
        string normalized = new string(title.Trim().Normalize(NormalizationForm.FormD)
            .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark).ToArray())
            .Replace('\u0131', 'i').ToLowerInvariant();

        return normalized is "muhendis" or "muhendisi"
            || normalized.EndsWith(" muhendis", StringComparison.Ordinal)
            || normalized.EndsWith(" muhendisi", StringComparison.Ordinal);
    }

    public static IReadOnlyList<string> GetEngineerErrors(InternshipApplication application)
    {
        ArgumentNullException.ThrowIfNull(application);
        var errors = new List<string>();

        if (application.EngineerCount < 1)
        {
            errors.Add("Isyerinde en az bir muhendis bulunmali.");
            return errors;
        }

        if (string.IsNullOrWhiteSpace(application.ResponsibleEngineerName))
            errors.Add("Sorumlu muhendisin adi soyadi girilmeli.");
        if (!IsValidEngineerTitle(application.ResponsibleEngineerTitle))
            errors.Add("Sorumlu muhendisin unvani muhendis veya muhendisi ifadesiyle bitmeli.");
        if (string.IsNullOrWhiteSpace(application.EngineerRegistrationNumber))
            errors.Add("Sorumlu muhendisin oda sicil numarasi girilmeli.");
        if (RequiresGraduationDepartment(application.EngineerCount)
            && string.IsNullOrWhiteSpace(application.EngineerGraduationDepartment))
            errors.Add("Muhendis sayisi 5'in altindaysa sorumlu muhendisin mezun oldugu bolum girilmeli.");

        return errors;
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
