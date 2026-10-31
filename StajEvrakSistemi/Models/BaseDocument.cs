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

    /// <summary>
    /// Belgenin neden doğrulamadan geçmediğini kullanıcıya gösterilecek mesajlarla döndürür
    /// (boş liste = geçerli). Alt sınıflar ezerek ayrıntılı mesaj verir.
    /// </summary>
    public virtual IReadOnlyList<string> GetValidationErrors() =>
        ValidateDocument() ? Array.Empty<string>() : new[] { "Belge doğrulamadan geçemedi." };

    public virtual string GetDisplayName() => DocumentType.ToString();

    /// <summary>
    /// Veritabanından okunan belgenin onay durumunu geri yükler (doğrulama çalıştırılmaz).
    /// </summary>
    internal void RestoreState(bool isApproved, string? rejectionReason)
    {
        IsApproved = isApproved;
        RejectionReason = rejectionReason;
    }

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

    protected bool HasValidFileSize(long maxBytes)
    {
        long size = new FileInfo(FilePath).Length;
        return size > 0 && size <= maxBytes;
    }

    /// <summary>
    /// Dosyanın ilk baytlarının uzantısıyla uyuştuğunu kontrol eder
    /// (ör. uzantısı .pdf olan bir resim dosyası reddedilir).
    /// </summary>
    protected bool HasMatchingFileSignature()
    {
        byte[]? expected = FileFormat switch
        {
            "pdf" => "%PDF-"u8.ToArray(),
            "jpg" or "jpeg" => new byte[] { 0xFF, 0xD8, 0xFF },
            "png" => new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A },
            "docx" => new byte[] { 0x50, 0x4B, 0x03, 0x04 },                                  // ZIP (Office Open XML)
            "doc" => new byte[] { 0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1 },           // OLE2 (eski Word)
            _ => null
        };
        if (expected is null)
            return false;

        using var stream = File.OpenRead(FilePath);
        var header = new byte[expected.Length];
        return stream.Read(header, 0, header.Length) == header.Length && header.SequenceEqual(expected);
    }

    public override string ToString() => $"{GetDisplayName()} - {Path.GetFileName(FilePath)}";
}
