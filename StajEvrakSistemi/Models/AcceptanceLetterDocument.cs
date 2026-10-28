using StajEvrakSistemi.Core;

namespace StajEvrakSistemi.Models;

/// <summary>
/// Kurum Staj Kabul Yazısı. Kurumun antetli, imzalı ve kaşeli yazısı;
/// PDF ya da taranmış resim olarak yüklenir.
/// </summary>
public class AcceptanceLetterDocument : BaseDocument
{
    public const long MaxFileSizeBytes = 5 * 1024 * 1024;

    private static readonly string[] Formats = { "pdf", "jpg", "jpeg", "png" };

    public AcceptanceLetterDocument(int internshipId, string filePath)
        : base(internshipId, filePath)
    {
    }

    public override DocumentType DocumentType => DocumentType.AcceptanceLetter;

    public override IReadOnlyCollection<string> AllowedFormats => Formats;

    public override bool ValidateDocument() =>
        HasAllowedFormat()
        && FileExists()
        && HasValidFileSize(MaxFileSizeBytes)
        && HasMatchingFileSignature();

    public override string GetDisplayName() => "Kurum Staj Kabul Yazısı";
}
