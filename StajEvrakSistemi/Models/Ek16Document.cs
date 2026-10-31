using StajEvrakSistemi.Core;

namespace StajEvrakSistemi.Models;

/// <summary>
/// Öğrencinin yüklediği Ek-16 staj belgesi. Şartname 4.4: Word (.doc/.docx), PDF, JPEG ve PNG
/// kabul edilir, diğer formatlar reddedilir ve kullanıcıya uyarı verilir.
/// </summary>
public class Ek16Document : BaseDocument
{
    public const long MaxFileSizeBytes = 10 * 1024 * 1024;

    private static readonly string[] Formats = { "doc", "docx", "pdf", "jpg", "jpeg", "png" };

    public Ek16Document(int internshipId, string filePath)
        : base(internshipId, filePath)
    {
    }

    public override DocumentType DocumentType => DocumentType.Ek16;

    public override IReadOnlyCollection<string> AllowedFormats => Formats;

    public override bool ValidateDocument() => GetValidationErrors().Count == 0;

    public override IReadOnlyList<string> GetValidationErrors()
    {
        var errors = new List<string>();

        if (string.IsNullOrEmpty(FileFormat))
        {
            errors.Add("Dosyanın uzantısı yok. Kabul edilen formatlar: Word (.doc/.docx), PDF, JPEG, PNG.");
            return errors;
        }

        bool formatAllowed = HasAllowedFormat();
        if (!formatAllowed)
            errors.Add($"'.{FileFormat}' formatı kabul edilmiyor. Kabul edilen formatlar: Word (.doc/.docx), PDF, JPEG, PNG.");

        if (!FileExists())
        {
            errors.Add("Dosya bulunamadı.");
            return errors;
        }

        long size = new FileInfo(FilePath).Length;
        if (size == 0)
            errors.Add("Dosya boş.");
        else if (size > MaxFileSizeBytes)
            errors.Add($"Dosya çok büyük (en fazla {MaxFileSizeBytes / 1024 / 1024} MB).");
        else if (formatAllowed && !HasMatchingFileSignature())
            errors.Add($"Dosya içeriği '.{FileFormat}' uzantısıyla uyuşmuyor.");

        return errors;
    }

    public override string GetDisplayName() => "Ek-16 Staj Belgesi";
}
