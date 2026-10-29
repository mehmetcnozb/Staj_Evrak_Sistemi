namespace StajEvrakSistemi.Models;

/// <summary>
/// Yüklenen (taranmış) Ek-16 belgesinden okunan alan değerleri. Alan okuma işlemi (OCR / form
/// ayrıştırma) bu sınıfın dışında yapılır; okunamayan alan null bırakılır.
/// </summary>
public sealed record ScannedDocumentFields(
    string? StudentNumber,
    string? IdentityNumber,
    string? FullName,
    string? Department,
    string? AcademicYear);

/// <summary>
/// Başvuruda girilen bilgiler ile taranmış Ek-16 belgesindeki bilgilerin karşılaştırma sonucu.
/// </summary>
public class ScannedDocumentComparisonResult
{
    public ScannedDocumentComparisonResult(
        int applicationId, IReadOnlyList<Ek16Discrepancy> discrepancies, DateTime comparedAt)
    {
        ApplicationId = applicationId;
        Discrepancies = discrepancies;
        ComparedAt = comparedAt;
    }

    public int ApplicationId { get; }

    public IReadOnlyList<Ek16Discrepancy> Discrepancies { get; }

    public DateTime ComparedAt { get; }

    /// <summary>
    /// Belgedeki tüm alanlar başvuruyla uyuşuyor: belge teslime hazır.
    /// </summary>
    public bool IsReadyForSubmission => Discrepancies.Count == 0;

    public string GetSummary() => IsReadyForSubmission
        ? $"Başvuru #{ApplicationId}: Ek-16 belgesi başvuru bilgileriyle uyuşuyor, teslime hazır."
        : $"Başvuru #{ApplicationId}: Teslime hazır değil, {Discrepancies.Count} uyuşmazlık - " +
          string.Join(" | ", Discrepancies.Select(d => d.Message));
}
