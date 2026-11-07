namespace StajEvrakSistemi.Models;

/// <summary>
/// Ek-16 belgesinden okunan alanlar. Alan okuma bu modelin disinda yapilir.
/// Baslangic, bitis ve zorunlu iletisim alanlari okunamazsa karsilastirma basarisiz olur.
/// Ayri gun sayisi ve haftalik gun listesi belgede varsa karsilastirmaya eklenir.
/// </summary>
public sealed record ScannedDocumentFields(
    string? StudentNumber,
    string? IdentityNumber,
    string? FullName,
    string? Department,
    string? AcademicYear,
    DateTime? StartDate = null,
    DateTime? EndDate = null,
    decimal? DeclaredWorkDays = null,
    IReadOnlyList<DayOfWeek>? WorkDays = null,
    string? Address = null,
    string? Phone = null,
    string? Email = null,
    string? Fax = null);

/// <summary>
/// Basvurudaki bilgiler ile belgedeki ogrenci, staj ve iletisim alanlarinin karsilastirma sonucu.
/// </summary>
public class ScannedDocumentComparisonResult
{
    public ScannedDocumentComparisonResult(
        int applicationId, IReadOnlyList<Ek16Discrepancy> discrepancies, DateTime comparedAt,
        decimal? calculatedWorkDays = null, decimal? documentCalculatedWorkDays = null)
    {
        ApplicationId = applicationId;
        Discrepancies = discrepancies;
        ComparedAt = comparedAt;
        CalculatedWorkDays = calculatedWorkDays;
        DocumentCalculatedWorkDays = documentCalculatedWorkDays;
    }

    public int ApplicationId { get; }

    public IReadOnlyList<Ek16Discrepancy> Discrepancies { get; }

    public DateTime ComparedAt { get; }

    public decimal? CalculatedWorkDays { get; }

    public decimal? DocumentCalculatedWorkDays { get; }

    /// <summary>
    /// Ogrenci, staj ve iletisim alanlari form ile uyusuyor. Tum belge kurallarini kapsamaz.
    /// </summary>
    public bool IsMatched => Discrepancies.Count == 0;

    public string GetSummary() => IsMatched
        ? $"Basvuru #{ApplicationId}: belgedeki ogrenci, staj ve iletisim bilgileri form ile uyusuyor."
        : $"Basvuru #{ApplicationId}: belge bilgilerinde {Discrepancies.Count} uyusmazlik - " +
          string.Join(" | ", Discrepancies.Select(d => d.Message));
}
