using System.Globalization;
using Microsoft.Data.Sqlite;
using StajEvrakSistemi.Core;
using StajEvrakSistemi.Data;
using StajEvrakSistemi.Models;

namespace StajEvrakSistemi.Services;

/// <summary>
/// Ogrenci ve iletisim bilgilerini veri setiyle, form alanlarini okunmus belgeyle karsilastirir.
/// Belge dogrulamasi yalnizca son yuklenen belgenin alanlari birlikte uyusursa kaydedilir.
/// </summary>
public class Ek16MatcherService
{
    private static readonly CultureInfo Turkish = CultureInfo.GetCultureInfo("tr-TR");

    private const string SelectApplications = """
        SELECT i.Id, i.StudentId, u.StudentNumber, u.IdentityNumber, u.FullName, i.Department, i.AcademicYear, u.IsForeign,
               i.StartDate, i.EndDate, i.WorkDays, i.Address, i.Phone, i.Email, i.Fax
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
    /// Basvuruyu veri setiyle karsilastirir. Tek basina belge dogrulamasi vermez.
    /// </summary>
    public Ek16ComparisonResult CompareWithEk16(User user, int applicationId)
    {
        using var connection = _database.GetConnection();
        using var transaction = connection.BeginTransaction();

        var application = LoadAuthorizedApplication(connection, transaction, user, applicationId);

        var result = Compare(connection, transaction, application);
        if (!result.IsMatched)
            InvalidateVerification(connection, transaction, applicationId);

        transaction.Commit();
        return result;
    }

    /// <summary>
    /// Yonetici icin tum basvurulari veri setiyle karsilastirir. Belge dogrulamasi vermez.
    /// </summary>
    public IReadOnlyList<Ek16ComparisonResult> CompareAllApplicationsWithEk16(User admin)
    {
        using var connection = _database.GetConnection();
        using var transaction = connection.BeginTransaction();

        EnsureActor(connection, transaction, admin);
        if (admin.Role != UserRole.Admin || !admin.HasPermission(Permission.ViewAllInternships))
            throw new ForbiddenAccessException("Toplu karsilastirmayi yalnizca yonetici yapabilir.");

        var results = LoadApplications(connection, transaction, applicationId: null)
            .Select(application => Compare(connection, transaction, application))
            .ToList();

        foreach (var result in results)
            if (!result.IsMatched)
                InvalidateVerification(connection, transaction, result.ApplicationId);

        transaction.Commit();
        return results;
    }

    /// <summary>
    /// Okunmus ogrenci, staj ve iletisim alanlariyla formu karsilastirir. Bu on kontrol veri kaydetmez.
    /// </summary>
    /// <exception cref="KeyNotFoundException">Basvuru bulunamadi.</exception>
    public ScannedDocumentComparisonResult CompareFormWithScannedDocument(
        User user, int applicationId, ScannedDocumentFields scanned)
    {
        ArgumentNullException.ThrowIfNull(scanned);

        using var connection = _database.GetConnection();
        using var transaction = connection.BeginTransaction();

        var app = LoadAuthorizedApplication(connection, transaction, user, applicationId);
        var discrepancies = GetScannedDiscrepancies(app, scanned);
        discrepancies.AddRange(ContactDocumentValidator.Compare(GetApplicationDetails(app), scanned));
        var internship = GetInternshipComparison(app, scanned);
        discrepancies.AddRange(internship.Discrepancies);

        return new ScannedDocumentComparisonResult(applicationId, discrepancies.Distinct().ToArray(), _utcNow(),
            internship.CalculatedWorkDays, internship.DocumentCalculatedWorkDays);
    }

    /// <summary>
    /// Son belgenin ogrenci bilgilerini basvuru sahibi ve veri setiyle birlikte dogrular.
    /// Staj tarihleri, gun ve iletisim bilgilerini de basvuruyla karsilastirir.
    /// Okunmus alanlarin belirtilen belgeden alinmasi, belge okuyucunun sorumlulugundadir.
    /// </summary>
    /// <exception cref="KeyNotFoundException">Basvuru veya belge bulunamadi.</exception>
    public Ek16ComparisonResult CompareScannedDocumentWithDataset(
        User user, int applicationId, int documentId, ScannedDocumentFields scanned)
    {
        ArgumentNullException.ThrowIfNull(scanned);

        using var connection = _database.GetConnection();
        using var transaction = connection.BeginTransaction();

        var app = LoadAuthorizedApplication(connection, transaction, user, applicationId);
        EnsureLatestDocument(connection, transaction, applicationId, documentId);

        // Referans kayit belgedeki baska bir kisiye gore degil, basvuru sahibine gore secilir.
        var reference = Compare(connection, transaction, app);
        var internship = GetInternshipComparison(app, scanned);
        var discrepancies = reference.Discrepancies.Concat(GetScannedDiscrepancies(app, scanned))
            .Concat(internship.Discrepancies)
            .Concat(ContactDocumentValidator.Compare(GetApplicationDetails(app), scanned)).Distinct().ToArray();
        var result = new Ek16ComparisonResult(app.Id, app.StudentId, app.StudentNumber, app.FullName,
            reference.MatchedRecord, discrepancies, reference.ComparedAt, documentId,
            internship.CalculatedWorkDays, internship.DocumentCalculatedWorkDays);
        SaveVerification(connection, transaction, result, documentId);

        transaction.Commit();
        return result;
    }

    private static InternshipComparisonResult GetInternshipComparison(ApplicationInfo app, ScannedDocumentFields scanned) =>
        InternshipDocumentValidator.Compare(GetApplicationDetails(app), scanned);

    private static InternshipApplication GetApplicationDetails(ApplicationInfo app) => new()
    {
        StartDate = app.StartDate, EndDate = app.EndDate, WorkDays = app.WorkDays.ToList(),
        Address = app.Address, Phone = app.Phone, Email = app.Email, Fax = app.Fax
    };

    private static void EnsureActor(SqliteConnection connection, SqliteTransaction transaction, User user)
    {
        if (user is null)
            throw new LoginRequiredException("Karsilastirma icin giris yapmalisiniz.");
        UserGuard.EnsureUserInDatabase(connection, transaction, user);
    }

    private static ApplicationInfo LoadAuthorizedApplication(
        SqliteConnection connection, SqliteTransaction transaction, User user, int applicationId)
    {
        EnsureActor(connection, transaction, user);
        var app = LoadApplications(connection, transaction, applicationId).SingleOrDefault()
            ?? throw new KeyNotFoundException("Karsilastirilacak basvuru bulunamadi.");
        if (!user.HasPermission(Permission.ViewAllInternships)
            && !(user.HasPermission(Permission.ViewOwnInternships) && app.StudentId == user.Id))
            throw new ForbiddenAccessException("Yalnizca kendi basvurunuzun bilgilerini karsilastirabilirsiniz.");
        return app;
    }

    private static void EnsureLatestDocument(
        SqliteConnection connection, SqliteTransaction transaction, int applicationId, int documentId)
    {
        using var find = connection.CreateCommand();
        find.Transaction = transaction;
        find.CommandText = "SELECT InternshipId FROM Documents WHERE Id = $id AND DocumentType = 'Ek16';";
        find.Parameters.AddWithValue("$id", documentId);
        object? owner = find.ExecuteScalar();
        if (owner is null or DBNull)
            throw new KeyNotFoundException("Karsilastirilacak Ek-16 belgesi bulunamadi.");
        if (Convert.ToInt32(owner, CultureInfo.InvariantCulture) != applicationId)
            throw new ForbiddenAccessException("Belge secilen basvuruya ait degil.");

        using var latest = connection.CreateCommand();
        latest.Transaction = transaction;
        latest.CommandText = "SELECT MAX(Id) FROM Documents WHERE InternshipId = $id AND DocumentType = 'Ek16';";
        latest.Parameters.AddWithValue("$id", applicationId);
        if (Convert.ToInt32(latest.ExecuteScalar(), CultureInfo.InvariantCulture) != documentId)
            throw new InvalidOperationException("Karsilastirma son yuklenen Ek-16 belgesine ait olmali.");
    }

    private static List<Ek16Discrepancy> GetScannedDiscrepancies(ApplicationInfo app, ScannedDocumentFields scanned)
    {
        var errors = GetApplicationValidationErrors(app);
        CompareWithScanned(errors, Ek16Field.StudentNumber, "Ogrenci numarasi", app.StudentNumber, scanned.StudentNumber, false);
        CompareWithScanned(errors, Ek16Field.IdentityNumber, "Kimlik numarasi", app.IdentityNumber, scanned.IdentityNumber, false);
        CompareWithScanned(errors, Ek16Field.FullName, "Ad soyad", app.FullName, scanned.FullName, true);
        CompareWithScanned(errors, Ek16Field.Department, "Bolum", app.Department, scanned.Department, true);
        CompareWithScanned(errors, Ek16Field.AcademicYear, "Ogretim yili", app.AcademicYear, scanned.AcademicYear, false);
        if (!string.IsNullOrWhiteSpace(scanned.IdentityNumber))
            CheckIdentity(errors, scanned.IdentityNumber.Trim(), app.IsForeign, "Belgedeki");
        if (!string.IsNullOrWhiteSpace(scanned.StudentNumber) && !FormatValidator.IsValidStudentNumber(scanned.StudentNumber))
            errors.Add(new(Ek16Field.StudentNumber, scanned.StudentNumber, null, "Belgedeki ogrenci numarasinin bicimi gecersiz."));
        return errors;
    }

    private static List<Ek16Discrepancy> GetApplicationValidationErrors(ApplicationInfo app)
    {
        var errors = new List<Ek16Discrepancy>();
        errors.AddRange(ContactDocumentValidator.GetApplicationErrors(GetApplicationDetails(app)));
        CheckIdentity(errors, app.IdentityNumber?.Trim(), app.IsForeign, "Kayitli");
        if (!FormatValidator.IsValidStudentNumber(app.StudentNumber))
            errors.Add(new(Ek16Field.StudentNumber, app.StudentNumber, null, "Kayitli ogrenci numarasinin bicimi gecersiz."));
        if (!InternshipDateValidator.IsAcademicYearValid(app.AcademicYear))
            errors.Add(new(Ek16Field.AcademicYear, app.AcademicYear, null, "Kayitli ogretim yilinin bicimi gecersiz."));
        return errors;
    }

    private static void CheckIdentity(List<Ek16Discrepancy> errors, string? identity, bool isForeign, string source)
    {
        if (!IdentityNumberValidator.IsValidForNationality(identity, isForeign, validateChecksum: true))
            errors.Add(new(Ek16Field.IdentityNumber, identity, null, isForeign
                ? $"{source} yabanci kimlik numarasi 11 rakamdan olusmali ve 99 ile baslamali."
                : $"{source} T.C. kimlik numarasinin bicimi veya kontrol haneleri gecersiz."));
    }

    private Ek16ComparisonResult Compare(
        SqliteConnection connection, SqliteTransaction transaction, ApplicationInfo app, string source = "başvuruda")
    {
        DateTime now = _utcNow();
        var official = FindBestRecord(connection, transaction, app);
        var discrepancies = GetApplicationValidationErrors(app);

        if (official is null)
        {
            var notFound = new Ek16Discrepancy(
                Ek16Field.Record,
                $"{app.StudentNumber ?? "-"} / {app.IdentityNumber ?? "-"}",
                null,
                $"Öğrenci Ek-16 staj listesinde bulunamadı (öğrenci no: {app.StudentNumber ?? "yok"}, " +
                $"kimlik no: {app.IdentityNumber ?? "yok"}).");

            discrepancies.Add(notFound);
            return new Ek16ComparisonResult(app.Id, app.StudentId, app.StudentNumber, app.FullName, null, discrepancies, now);
        }

        discrepancies.AddRange(ContactDocumentValidator.CompareWithReference(GetApplicationDetails(app), official));
        CheckIdentity(discrepancies, official.IdentityNumber, official.IsForeign, "Veri setindeki");
        if (app.IsForeign != official.IsForeign)
            discrepancies.Add(new(Ek16Field.Nationality, app.IsForeign ? "Yabanci" : "T.C.", official.Nationality,
                "Kayitli uyruk secimi veri setindeki uyrukla uyusmuyor."));

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

    private static void SaveVerification(
        SqliteConnection connection, SqliteTransaction transaction, Ek16ComparisonResult result, int documentId)
    {
        if (!result.IsMatched)
        {
            InvalidateVerification(connection, transaction, result.ApplicationId);
            return;
        }
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            UPDATE Internships SET Ek16Verified = $verified, Ek16VerifiedAt = $verifiedAt, NetWorkDays = $days
            WHERE Id = $id;
            """;
        command.Parameters.AddWithValue("$verified", 1);
        command.Parameters.AddWithValue("$verifiedAt", result.ComparedAt.ToString("o", CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("$id", result.ApplicationId);
        command.Parameters.AddWithValue("$days", (double)result.CalculatedWorkDays!.Value);
        command.ExecuteNonQuery();

        using var document = connection.CreateCommand();
        document.Transaction = transaction;
        document.CommandText = """
            UPDATE Documents SET StudentFieldsVerified = 1, StudentFieldsVerifiedAt = $at,
                InternshipFieldsVerified = 1, InternshipFieldsVerifiedAt = $at,
                ContactFieldsVerified = 1, ContactFieldsVerifiedAt = $at
            WHERE Id = $documentId AND InternshipId = $applicationId;
            """;
        document.Parameters.AddWithValue("$at", result.ComparedAt.ToString("o", CultureInfo.InvariantCulture));
        document.Parameters.AddWithValue("$documentId", documentId);
        document.Parameters.AddWithValue("$applicationId", result.ApplicationId);
        document.ExecuteNonQuery();
    }

    private static void InvalidateVerification(SqliteConnection connection, SqliteTransaction transaction, int applicationId)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            UPDATE Internships SET Ek16Verified = 0, Ek16VerifiedAt = NULL WHERE Id = $id;
            UPDATE Documents SET StudentFieldsVerified = 0, StudentFieldsVerifiedAt = NULL,
                InternshipFieldsVerified = 0, InternshipFieldsVerifiedAt = NULL,
                ContactFieldsVerified = 0, ContactFieldsVerifiedAt = NULL, IsApproved = 0
            WHERE InternshipId = $id;
            """;
        command.Parameters.AddWithValue("$id", applicationId);
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
            string? Str(int index) => reader.IsDBNull(index) ? null : reader.GetString(index);
            DateTime Date(int index) => !reader.IsDBNull(index)
                && DateTime.TryParseExact(reader.GetString(index), "yyyy-MM-dd", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out var value) ? value : default;
            var days = reader.IsDBNull(10) ? Array.Empty<DayOfWeek>() : reader.GetString(10).Split(',')
                .Select(value => Enum.TryParse<DayOfWeek>(value.Trim(), true, out var day) && Enum.IsDefined(day)
                    ? day : (DayOfWeek)(-1)).ToArray();
            applications.Add(new ApplicationInfo(
                reader.GetInt32(0),
                reader.GetInt32(1),
                reader.IsDBNull(2) ? null : reader.GetString(2),
                reader.IsDBNull(3) ? null : reader.GetString(3),
                reader.GetString(4),
                reader.IsDBNull(5) ? null : reader.GetString(5),
                reader.GetString(6), reader.GetInt32(7) == 1, Date(8), Date(9), days,
                Str(11), Str(12), Str(13), Str(14)));
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
        string AcademicYear,
        bool IsForeign,
        DateTime StartDate,
        DateTime EndDate,
        IReadOnlyList<DayOfWeek> WorkDays,
        string? Address, string? Phone, string? Email, string? Fax);
}
