using StajEvrakSistemi.Models;

namespace StajEvrakSistemi.Services;

/// <summary>
/// Okunamayan satır. CSV'de dosyadaki satır numarası, JSON'da dizideki sıra numarası (1'den başlar).
/// </summary>
public sealed record DatasetRowError(int RowNumber, string Message);

/// <summary>
/// ParseCsv / ParseJson sonucu: geçerli kayıtlar ve okunamayan satırlar.
/// </summary>
public sealed record DatasetParseResult(
    IReadOnlyList<OfficialStudentRecord> Records,
    IReadOnlyList<DatasetRowError> Errors)
{
    public int TotalRows => Records.Count + Errors.Count;
}

/// <summary>
/// Veritabanına aktarım özeti.
/// </summary>
public sealed record DatasetImportResult(
    int TotalRows,
    int Imported,
    int DuplicatesInFile,
    int DuplicatesInDatabase,
    IReadOnlyList<DatasetRowError> Errors)
{
    public int Skipped => DuplicatesInFile + DuplicatesInDatabase + Errors.Count;
}
