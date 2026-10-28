using StajEvrakSistemi.Core;

namespace StajEvrakSistemi.Models;

/// <summary>
/// SGK Müstehaklık ve Sigorta Belgesi. e-Devlet'ten barkodlu PDF olarak alınır,
/// bu yüzden yalnızca PDF kabul edilir.
/// </summary>
public class InsuranceDocument : BaseDocument
{
    public const long MaxFileSizeBytes = 2 * 1024 * 1024;

    private static readonly string[] Formats = { "pdf" };

    public InsuranceDocument(int internshipId, string filePath)
        : base(internshipId, filePath)
    {
    }

    public override DocumentType DocumentType => DocumentType.InsuranceDocument;

    public override IReadOnlyCollection<string> AllowedFormats => Formats;

    public override bool ValidateDocument() =>
        HasAllowedFormat()
        && FileExists()
        && HasValidFileSize(MaxFileSizeBytes)
        && HasMatchingFileSignature();

    public override string GetDisplayName() => "SGK Müstehaklık ve Sigorta Belgesi";
}
