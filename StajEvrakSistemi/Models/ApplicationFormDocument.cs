using StajEvrakSistemi.Core;

namespace StajEvrakSistemi.Models;

/// <summary>
/// Staj Başvuru Formu (Ek-1). Doldurulup imzalandıktan sonra taranarak
/// PDF ya da resim olarak yüklenir.
/// </summary>
public class ApplicationFormDocument : BaseDocument
{
    public const long MaxFileSizeBytes = 5 * 1024 * 1024;

    private static readonly string[] Formats = { "pdf", "jpg", "jpeg", "png" };

    public ApplicationFormDocument(int internshipId, string filePath)
        : base(internshipId, filePath)
    {
    }

    public override DocumentType DocumentType => DocumentType.InternshipApplicationForm;

    public override IReadOnlyCollection<string> AllowedFormats => Formats;

    public override bool ValidateDocument() =>
        HasAllowedFormat()
        && FileExists()
        && HasValidFileSize(MaxFileSizeBytes)
        && HasMatchingFileSignature();

    public override string GetDisplayName() => "Staj Başvuru Formu (Ek-1)";
}
