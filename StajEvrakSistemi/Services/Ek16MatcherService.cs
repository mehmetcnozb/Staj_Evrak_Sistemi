using System.Globalization;
using Microsoft.Data.Sqlite;
using StajEvrakSistemi.Core;
using StajEvrakSistemi.Data;
using StajEvrakSistemi.Models;

namespace StajEvrakSistemi.Services;

/// <summary>
/// Staj başvurularındaki öğrenci bilgilerini, Admin'in içe aktardığı resmi Ek-16 staj
/// listesiyle (OfficialStudentRecords) karşılaştırır. Tam eşleşen başvurunun Ek-16
/// doğrulaması onaylanır, aksi halde uyuşmazlıklar raporlanır ve onay kaldırılır.
/// </summary>
public class Ek16MatcherService
{
    private static readonly CultureInfo Turkish = CultureInfo.GetCultureInfo("tr-TR");

    private const string SelectApplications = """
        SELECT i.Id, i.StudentId, u.StudentNumber, u.IdentityNumber, u.FullName, i.Department, i.AcademicYear
        FROM Internships i
        JOIN Users u ON u.Id = i.StudentId
        """;

    private readonly DatabaseManager _database;
    private readonly Func<DateTime> _utcNow;

    public Ek16MatcherService(DatabaseManager database, Func<DateTime>? utcNow = null)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
        _utcNow = utcNow ?? (() => DateTime.UtcNow);
    }

    /// <summary>
    /// Tek bir başvuruyu Ek-16 listesiyle karşılaştırır ve doğrulama durumunu kaydeder.
    /// </summary>
    public Ek16ComparisonResult CompareWithEk16(int applicationId)
    {
        using var connection = _database.GetConnection();
        using var transaction = connection.BeginTransaction();

        var application = LoadApplications(connection, transaction, applicationId).SingleOrDefault()
            ?? throw new KeyNotFoundException($"#{applicationId} numaralı staj başvurusu bulunamadı.");

        var result = Compare(connection, transaction, application);
        SaveVerification(connection, transaction, result);

        transaction.Commit();
        return result;
    }

    /// <summary>
    /// Tüm başvuruları Ek-16 listesiyle karşılaştırır ve her birinin doğrulama durumunu kaydeder.
    /// </summary>
    public IReadOnlyList<Ek16ComparisonResult> CompareAllApplicationsWithEk16()
    {
        using var connection = _database.GetConnection();
        using var transaction = connection.BeginTransaction();

        var results = LoadApplications(connection, transaction, applicationId: null)
            .Select(application => Compare(connection, transaction, application))
            .ToList();

        foreach (var result in results)
            SaveVerification(connection, transaction, result);

        transaction.Commit();
        return results;
    }

    /// <summary>
    /// Başvuruya girilen bilgileri (öğrenci no, kimlik no, ad soyad, bölüm, öğretim yılı), yüklenen
    /// Ek-16 belgesinden okunan değerlerle karşılaştırır ve belgenin teslime hazır olup olmadığını döndürür.
    /// Hiçbir şey kaydedilmez; yalnızca karşılaştırma sonucu verilir.
    /// </summary>
    /// <exception cref="KeyNotFoundException">Başvuru bulunamadı.</exception>
    public ScannedDocumentComparisonResult CompareFormWithScannedDocument(int applicationId, ScannedDocumentFields scanned)
    {
        ArgumentNullException.ThrowIfNull(scanned);

        using var connection = _database.GetConnection();
        using var transaction = connection.BeginTransaction();

        var app = LoadApplications(connection, transaction, applicationId).SingleOrDefault()
            ?? throw new KeyNotFoundException($"#{applicationId} numaralı staj başvurusu bulunamadı.");

        var discrepancies = new List<Ek16Discrepancy>();
        CompareWithScanned(discrepancies, Ek16Field.StudentNumber, "Öğrenci numarası", app.StudentNumber, scanned.StudentNumber, false);
        CompareWithScanned(discrepancies, Ek16Field.IdentityNumber, "T.C./Yabancı kimlik numarası", app.IdentityNumber, scanned.IdentityNumber, false);
        CompareWithScanned(discrepancies, Ek16Field.FullName, "Ad soyad", app.FullName, scanned.FullName, true);
        CompareWithScanned(discrepancies, Ek16Field.Department, "Bölüm", app.Department, scanned.Department, true);
        CompareWithScanned(discrepancies, Ek16Field.AcademicYear, "Öğretim yılı (dönem)", app.AcademicYear, scanned.AcademicYear, false);

        return new ScannedDocumentComparisonResult(applicationId, discrepancies, _utcNow());
    }

    /// <summary>
    /// Yüklenen Ek-16 belgesinden okunan değerleri, Admin'in yüklediği veri setindeki kayıtla karşılaştırır
    /// (hoca: "sisteme yüklenen belgeler ile test veri setindeki bilgiler karşılaştırılacaktır").
    /// Sonuç başvurunun Ek-16 doğrulama durumuna da yazılır: tam eşleşme onaylanır, uyuşmazlıkta onay kalkar.
    /// </summary>
    /// <exception cref="KeyNotFoundException">Başvuru bulunamadı.</exception>
    public Ek16ComparisonResult CompareScannedDocumentWithDataset(int applicationId, ScannedDocumentFields scanned)
    {
        ArgumentNullException.ThrowIfNull(scanned);

        using var connection = _database.GetConnection();
        using var transaction = connection.BeginTransaction();

        var app = LoadApplications(connection, transaction, applicationId).SingleOrDefault()
            ?? throw new KeyNotFoundException($"#{applicationId} numaralı staj başvurusu bulunamadı.");

        // Karşılaştırmanın "sol tarafı" artık başvuru değil, belgeden okunan değerlerdir.
        var fromDocument = app with
        {
            StudentNumber = scanned.StudentNumber?.Trim(),
            IdentityNumber = scanned.IdentityNumber?.Trim(),
            FullName = scanned.FullName?.Trim() ?? string.Empty,
            Department = scanned.Department?.Trim(),
            AcademicYear = scanned.AcademicYear?.Trim() ?? string.Empty
        };

        var result = Compare(connection, transaction, fromDocument, "Ek-16 belgesinde");
        SaveVerification(connection, transaction, result);

        transaction.Commit();
        return result;
    }

    private Ek16ComparisonResult Compare(
        SqliteConnection connection, SqliteTransaction transaction, ApplicationInfo app, string source = "başvuruda")
    {
        DateTime now = _utcNow();
        var official = FindBestRecord(connection, transaction, app);

        if (official is null)
        {
            var notFound = new Ek16Discrepancy(
                Ek16Field.Record,
                $"{app.StudentNumber ?? "-"} / {app.IdentityNumber ?? "-"}",
                null,
                $"Öğrenci Ek-16 staj listesinde bulunamadı (öğrenci no: {app.StudentNumber ?? "yok"}, " +
                $"kimlik no: {app.IdentityNumber ?? "yok"}).");

            return new Ek16ComparisonResult(app.Id, app.StudentId, app.StudentNumber, app.FullName, null, new[] { notFound }, now);
        }

        var discrepancies = new List<Ek16Discrepancy>();

        CompareExact(discrepancies, Ek16Field.StudentNumber, "Öğrenci numarası", app.StudentNumber, official.StudentNumber, source);
        CompareExact(discrepancies, Ek16Field.IdentityNumber, "T.C./Yabancı kimlik numarası", app.IdentityNumber, official.IdentityNumber, source);
        CompareText(discrepancies, Ek16Field.FullName, "Ad soyad", app.FullName, official.FullName, source);
        CompareText(discrepancies, Ek16Field.Department, "Bölüm", app.Department, official.Department, source);
        CompareExact(discrepancies, Ek16Field.AcademicYear, "Öğretim yılı (dönem)", app.AcademicYear, official.AcademicYear, source);

        return new Ek16ComparisonResult(app.Id, app.StudentId, app.StudentNumber, app.FullName, official, discrepancies, now);
    }

    /// <summary>
    /// Öğrenci no veya kimlik no ile eşleşen Ek-16 kayıtları arasından en uygununu seçer:
    /// iki numarası da tutan ve aynı öğretim yılına ait kayıt önceliklidir.
    /// </summary>
    private static OfficialStudentRecord? FindBestRecord(SqliteConnection connection, SqliteTransaction transaction, ApplicationInfo app)
    {
        if (app.StudentNumber is null && app.IdentityNumber is null)
            return null;

        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            SELECT Id, StudentNumber, IdentityNumber, FullName, Nationality, Department, AcademicYear, BirthDate, Address,
                   PreviousInternshipDays, Email, Phone, HealthAidStatus, ImportedAt, ImportedBy
            FROM OfficialStudentRecords
            WHERE StudentNumber = $studentNumber OR IdentityNumber = $identity
            ORDER BY Id;
            """;
        command.Parameters.AddWithValue("$studentNumber", (object?)app.StudentNumber ?? DBNull.Value);
        command.Parameters.AddWithValue("$identity", (object?)app.IdentityNumber ?? DBNull.Value);

        var candidates = new List<OfficialStudentRecord>();
        using (var reader = command.ExecuteReader())
        {
            while (reader.Read())
                candidates.Add(MapRecord(reader));
        }

        return candidates
            .OrderByDescending(r =>
                (r.StudentNumber == app.StudentNumber ? 4 : 0)
                + (r.IdentityNumber == app.IdentityNumber ? 4 : 0)
                + (r.AcademicYear == app.AcademicYear ? 2 : 0))
            .FirstOrDefault();
    }

    // source: karşılaştırmanın sol tarafını anlatan ifade ("başvuruda" ya da "Ek-16 belgesinde").
    private static void CompareExact(
        List<Ek16Discrepancy> discrepancies, Ek16Field field, string label, string? applicationValue, string officialValue,
        string source = "başvuruda")
    {
        string? value = applicationValue?.Trim();
        if (string.IsNullOrEmpty(value))
            discrepancies.Add(new Ek16Discrepancy(field, null, officialValue,
                $"{label} {source} yok; Ek-16 listesinde '{officialValue}'."));
        else if (value != officialValue)
            discrepancies.Add(new Ek16Discrepancy(field, value, officialValue,
                $"{label} uyuşmuyor: {source} '{value}', Ek-16 listesinde '{officialValue}'."));
    }

    // Ad soyad ve bölüm büyük/küçük harf ve fazla boşluk farkı gözetmeden karşılaştırılır.
    private static void CompareText(
        List<Ek16Discrepancy> discrepancies, Ek16Field field, string label, string? applicationValue, string officialValue,
        string source = "başvuruda")
    {
        if (string.IsNullOrWhiteSpace(applicationValue))
            discrepancies.Add(new Ek16Discrepancy(field, null, officialValue,
                $"{label} {source} yok; Ek-16 listesinde '{officialValue}'."));
        else if (NormalizeText(applicationValue) != NormalizeText(officialValue))
            discrepancies.Add(new Ek16Discrepancy(field, applicationValue.Trim(), officialValue,
                $"{label} uyuşmuyor: {source} '{applicationValue.Trim()}', Ek-16 listesinde '{officialValue}'."));
    }

    // Belgeden okunamayan alan da uyuşmazlık sayılır: belge okunaklı değilse teslime hazır olmaz.
    private static void CompareWithScanned(
        List<Ek16Discrepancy> discrepancies, Ek16Field field, string label,
        string? applicationValue, string? scannedValue, bool ignoreCaseAndSpacing)
    {
        string? application = applicationValue?.Trim();
        string? scanned = scannedValue?.Trim();

        if (string.IsNullOrEmpty(scanned))
        {
            discrepancies.Add(new Ek16Discrepancy(field, application, null,
                $"{label} Ek-16 belgesinden okunamadı."));
            return;
        }

        if (string.IsNullOrEmpty(application))
        {
            discrepancies.Add(new Ek16Discrepancy(field, null, scanned,
                $"{label} başvuruda girilmemiş; Ek-16 belgesinde '{scanned}'."));
            return;
        }

        bool same = ignoreCaseAndSpacing
            ? NormalizeText(application) == NormalizeText(scanned)
            : application == scanned;

        if (!same)
            discrepancies.Add(new Ek16Discrepancy(field, application, scanned,
                $"{label} uyuşmuyor: başvuruda '{application}', Ek-16 belgesinde '{scanned}'."));
    }

    private static string NormalizeText(string value) =>
        string.Join(' ', value.Split(' ', StringSplitOptions.RemoveEmptyEntries)).ToUpper(Turkish);

    private static void SaveVerification(SqliteConnection connection, SqliteTransaction transaction, Ek16ComparisonResult result)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "UPDATE Internships SET Ek16Verified = $verified, Ek16VerifiedAt = $verifiedAt WHERE Id = $id;";
        command.Parameters.AddWithValue("$verified", result.IsMatched ? 1 : 0);
        command.Parameters.AddWithValue("$verifiedAt",
            result.IsMatched ? result.ComparedAt.ToString("o", CultureInfo.InvariantCulture) : DBNull.Value);
        command.Parameters.AddWithValue("$id", result.ApplicationId);
        command.ExecuteNonQuery();
    }

    private static List<ApplicationInfo> LoadApplications(
        SqliteConnection connection, SqliteTransaction transaction, int? applicationId)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = applicationId is null
            ? $"{SelectApplications} ORDER BY i.Id;"
            : $"{SelectApplications} WHERE i.Id = $id;";
        if (applicationId is not null)
            command.Parameters.AddWithValue("$id", applicationId.Value);

        var applications = new List<ApplicationInfo>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            applications.Add(new ApplicationInfo(
                reader.GetInt32(0),
                reader.GetInt32(1),
                reader.IsDBNull(2) ? null : reader.GetString(2),
                reader.IsDBNull(3) ? null : reader.GetString(3),
                reader.GetString(4),
                reader.IsDBNull(5) ? null : reader.GetString(5),
                reader.GetString(6)));
        }
        return applications;
    }

    private static OfficialStudentRecord MapRecord(SqliteDataReader reader) =>
        new(studentNumber: reader.GetString(1),
            identityNumber: reader.GetString(2),
            fullName: reader.GetString(3),
            nationality: reader.GetString(4),
            department: reader.GetString(5),
            academicYear: reader.GetString(6),
            birthDate: reader.IsDBNull(7) ? null : DateTime.ParseExact(reader.GetString(7), "yyyy-MM-dd", CultureInfo.InvariantCulture),
            address: reader.IsDBNull(8) ? null : reader.GetString(8),
            previousInternshipDays: reader.IsDBNull(9) ? null : reader.GetInt32(9),
            email: reader.IsDBNull(10) ? null : reader.GetString(10),
            phone: reader.IsDBNull(11) ? null : reader.GetString(11),
            healthAidStatus: reader.IsDBNull(12) ? null : reader.GetString(12))
        {
            Id = reader.GetInt32(0),
            ImportedAt = DateTime.Parse(reader.GetString(13), CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal),
            ImportedByUserId = reader.IsDBNull(14) ? null : reader.GetInt32(14)
        };

    private sealed record ApplicationInfo(
        int Id,
        int StudentId,
        string? StudentNumber,
        string? IdentityNumber,
        string FullName,
        string? Department,
        string AcademicYear);
}
