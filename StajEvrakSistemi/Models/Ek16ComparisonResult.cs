namespace StajEvrakSistemi.Models;

/// <summary>
/// Karşılaştırılan alan. Record: öğrenci Ek-16 listesinde hiç bulunamadı.
/// </summary>
public enum Ek16Field
{
    Record,
    StudentNumber,
    IdentityNumber,
    FullName,
    Department,
    AcademicYear
}

/// <summary>
/// Başvurudaki bilgi ile resmi Ek-16 listesindeki bilgi arasındaki tek bir uyuşmazlık.
/// </summary>
public sealed record Ek16Discrepancy(Ek16Field Field, string? ApplicationValue, string? OfficialValue, string Message)
{
    public override string ToString() => Message;
}

/// <summary>
/// Bir staj başvurusunun resmi Ek-16 staj listesiyle karşılaştırılma sonucu.
/// </summary>
public class Ek16ComparisonResult
{
    public Ek16ComparisonResult(
        int applicationId,
        int studentId,
        string? studentNumber,
        string fullName,
        OfficialStudentRecord? matchedRecord,
        IReadOnlyList<Ek16Discrepancy> discrepancies,
        DateTime comparedAt)
    {
        ApplicationId = applicationId;
        StudentId = studentId;
        StudentNumber = studentNumber;
        FullName = fullName;
        MatchedRecord = matchedRecord;
        Discrepancies = discrepancies;
        ComparedAt = comparedAt;
    }

    public int ApplicationId { get; }

    public int StudentId { get; }

    public string? StudentNumber { get; }

    public string FullName { get; }

    /// <summary>
    /// Ek-16 listesinde öğrenciye karşılık gelen kayıt; hiç bulunamadıysa null.
    /// </summary>
    public OfficialStudentRecord? MatchedRecord { get; }

    public IReadOnlyList<Ek16Discrepancy> Discrepancies { get; }

    public DateTime ComparedAt { get; }

    /// <summary>
    /// Listede kayıt bulundu ve hiçbir alanda uyuşmazlık yok.
    /// </summary>
    public bool IsMatched => MatchedRecord is not null && Discrepancies.Count == 0;

    public string GetSummary() => IsMatched
        ? $"Başvuru #{ApplicationId} ({FullName}): Ek-16 ile tam eşleşti."
        : $"Başvuru #{ApplicationId} ({FullName}): {Discrepancies.Count} uyuşmazlık - " +
          string.Join(" | ", Discrepancies.Select(d => d.Message));
}
