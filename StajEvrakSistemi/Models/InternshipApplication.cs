using StajEvrakSistemi.Core;

namespace StajEvrakSistemi.Models;

/// <summary>
/// Öğrencinin sisteme girdiği staj başvuru bilgileri (şartname 6: adres, telefon, e-posta, faks, tarihler...).
/// Bu bilgiler kaydedilir ve yüklenen Ek-16 belgesinden okunan değerlerle karşılaştırılır.
/// Alan biçimi doğrulamaları (adres sırası, +90 telefon, tarih/gün hesabı...) ayrı doğrulama katmanının işidir;
/// bu sınıf yalnızca veriyi taşır.
/// </summary>
public class InternshipApplication
{
    public int Id { get; internal set; }

    public int StudentId { get; internal set; }

    /// <summary>
    /// Öğretim yılı, ör. "2025-2026".
    /// </summary>
    public string AcademicYear { get; set; } = string.Empty;

    /// <summary>
    /// Yarıyıl, ör. "Bahar" / "Güz" (şartname 6.3).
    /// </summary>
    public string? Semester { get; set; }

    public DateTime StartDate { get; set; }

    public DateTime EndDate { get; set; }

    public string CompanyName { get; set; } = string.Empty;

    public string? Department { get; set; }

    public string? Address { get; set; }

    public string? Phone { get; set; }

    public string? Email { get; set; }

    /// <summary>
    /// Faks boş bırakılabilir (nullable).
    /// </summary>
    public string? Fax { get; set; }

    /// <summary>
    /// Öğrencinin staj yapacağı haftalık günler.
    /// </summary>
    public IList<DayOfWeek> WorkDays { get; set; } = new List<DayOfWeek>();

    public int EngineerCount { get; set; }

    public string? GoogleFormUrl { get; set; }

    public ApplicationStatus Status { get; internal set; } = ApplicationStatus.Uploaded;

    public bool Ek16Verified { get; internal set; }

    public DateTime? CreatedAt { get; internal set; }
}
