namespace StajEvrakSistemi.Models;

/// <summary>
/// Ek-16 belgesinden okunan alanlar. Alan okuma bu modelin disinda yapilir.
/// Zorunlu ogrenci, tarih, iletisim, yariyil ve isyeri alanlari okunamazsa eslesme verilmez.
/// Ayri gun sayisi, haftalik gun listesi ve muhendis sayisi okunmussa karsilastirmaya eklenir.
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
    string? Fax = null,
    string? Semester = null,
    string? CompanyName = null,
    int? EngineerCount = null);

/// <summary>
/// Basvurudaki bilgiler ile belgeden okunan alanlarin karsilastirma sonucu.
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
    /// Kontrol edilen belge alanlari form ile uyusuyor. Tum belge kurallarini kapsamaz.
    /// </summary>
    public bool IsMatched => Discrepancies.Count == 0;

    public string GetSummary() => IsMatched
        ? $"Basvuru #{ApplicationId}: kontrol edilen belge alanlari form ile uyusuyor."
        : $"Basvuru #{ApplicationId}: belge bilgilerinde {Discrepancies.Count} uyusmazlik - " +
          string.Join(" | ", Discrepancies.Select(d => d.Message));
}
