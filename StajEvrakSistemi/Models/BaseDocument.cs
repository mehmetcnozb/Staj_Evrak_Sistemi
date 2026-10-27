using StajEvrakSistemi.Core;

namespace StajEvrakSistemi.Models;

/// <summary>
/// Sisteme yüklenen tüm staj belgelerinin (Staj Başvuru Formu, Sigorta Belgesi vb.)
/// ortak temel sınıfı. Her belge türü kendi doğrulama kurallarını uygular.
/// </summary>
public abstract class BaseDocument
{
    private string _filePath = string.Empty;

    protected BaseDocument(int internshipId, string filePath)
    {
        if (internshipId <= 0)
            throw new ArgumentOutOfRangeException(nameof(internshipId), "Geçerli bir staj kaydı gerekli.");

        InternshipId = internshipId;
        FilePath = filePath;
        UploadedAt = DateTime.UtcNow;
    }

    public int Id { get; internal set; }

    public int InternshipId { get; }

    public string FilePath
    {
        get => _filePath;
        set
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("Dosya yolu boş olamaz.", nameof(FilePath));
            _filePath = value.Trim();
        }
    }

    /// <summary>
    /// Dosya uzantısından türetilir (ör. "pdf").
    /// </summary>
    public string FileFormat => Path.GetExtension(FilePath).TrimStart('.').ToLowerInvariant();

    public DateTime UploadedAt { get; internal set; }

    // Onay durumu yalnızca Approve / Reject ile değiştirilebilir.
    public bool IsApproved { get; private set; }

    public string? RejectionReason { get; private set; }

    public bool IsRejected => !IsApproved && RejectionReason is not null;

    public bool IsPending => !IsApproved && RejectionReason is null;

    public abstract DocumentType DocumentType { get; }

    /// <summary>
    /// Bu belge türü için kabul edilen dosya formatları (ör. "pdf").
    /// </summary>
    public abstract IReadOnlyCollection<string> AllowedFormats { get; }

    /// <summary>
    /// Belgenin türüne özgü kurallara uyup uymadığını kontrol eder.
    /// </summary>
    public abstract bool ValidateDocument();

    public virtual string GetDisplayName() => DocumentType.ToString();

    public void Approve()
    {
        if (!ValidateDocument())
            throw new InvalidOperationException("Doğrulamadan geçmeyen belge onaylanamaz.");

        IsApproved = true;
        RejectionReason = null;
    }

    public void Reject(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("Ret gerekçesi belirtilmelidir.", nameof(reason));

        IsApproved = false;
        RejectionReason = reason.Trim();
    }

    // Alt sınıfların ValidateDocument içinde kullanabileceği ortak kontroller.
    protected bool HasAllowedFormat() =>
        AllowedFormats.Contains(FileFormat, StringComparer.OrdinalIgnoreCase);

    protected virtual bool FileExists() => File.Exists(FilePath);

    public override string ToString() => $"{GetDisplayName()} - {Path.GetFileName(FilePath)}";
}
