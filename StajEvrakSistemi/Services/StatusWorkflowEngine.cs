using System.Globalization;
using Microsoft.Data.Sqlite;
using StajEvrakSistemi.Core;
using StajEvrakSistemi.Data;
using StajEvrakSistemi.Models;

namespace StajEvrakSistemi.Services;

/// <summary>
/// Staj başvurusunun statü akışını yönetir (şartname bölüm 5):
/// Yüklendi → İnceleniyor → Hatalı/Eksik → (düzeltilip yeniden yüklenir) → Onaylandı → Tamamlandı.
/// Her geçiş yetki ve iş kuralı kontrolünden geçer ve StatusTransitionLogs tablosuna yazılır.
/// </summary>
public class StatusWorkflowEngine
{
    // Geçerli geçişler. Tamamlandı son statüdür.
    private static readonly IReadOnlyDictionary<ApplicationStatus, ApplicationStatus[]> Transitions =
        new Dictionary<ApplicationStatus, ApplicationStatus[]>
        {
            [ApplicationStatus.Uploaded] = new[] { ApplicationStatus.UnderReview },
            [ApplicationStatus.UnderReview] = new[] { ApplicationStatus.FaultyOrMissing, ApplicationStatus.Approved },
            [ApplicationStatus.FaultyOrMissing] = new[] { ApplicationStatus.Uploaded },
            [ApplicationStatus.Approved] = new[] { ApplicationStatus.Completed },
            [ApplicationStatus.Completed] = Array.Empty<ApplicationStatus>()
        };

    // Hedef statüye geçmek için gereken izin.
    private static readonly IReadOnlyDictionary<ApplicationStatus, Permission> RequiredPermissions =
        new Dictionary<ApplicationStatus, Permission>
        {
            [ApplicationStatus.Uploaded] = Permission.UploadDocument,
            [ApplicationStatus.UnderReview] = Permission.ReviewDocument,
            [ApplicationStatus.FaultyOrMissing] = Permission.ReviewDocument,
            [ApplicationStatus.Approved] = Permission.ApproveInternship,
            [ApplicationStatus.Completed] = Permission.ApproveInternship
        };

    // Öğrencinin başvurusunu ve evraklarını değiştirebildiği / silebildiği statüler.
    private static readonly HashSet<ApplicationStatus> StudentEditableStatuses = new()
    {
        ApplicationStatus.Uploaded,
        ApplicationStatus.FaultyOrMissing
    };

    private enum ModificationKind
    {
        Update,
        Delete
    }

    private readonly DatabaseManager _database;
    private readonly Func<DateTime> _utcNow;

    public StatusWorkflowEngine(DatabaseManager database, Func<DateTime>? utcNow = null)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
        _utcNow = utcNow ?? (() => DateTime.UtcNow);
    }

    /// <summary>
    /// Akış kurallarına göre currentStatus'tan targetStatus'a geçilebilir mi?
    /// </summary>
    public bool CanTransition(ApplicationStatus currentStatus, ApplicationStatus targetStatus) =>
        Transitions.TryGetValue(currentStatus, out var targets) && targets.Contains(targetStatus);

    public IReadOnlyList<ApplicationStatus> GetAllowedTransitions(ApplicationStatus currentStatus) =>
        Transitions.TryGetValue(currentStatus, out var targets) ? targets : Array.Empty<ApplicationStatus>();

    public static bool IsFinal(ApplicationStatus status) => Transitions[status].Length == 0;

    /// <summary>
    /// Hatalı/Eksik işaretlerken hangi alanların hatalı veya eksik olduğunu belirten gerekçe yazılması zorunludur.
    /// </summary>
    public static bool RequiresNote(ApplicationStatus targetStatus) =>
        targetStatus is ApplicationStatus.FaultyOrMissing;

    public static string GetDisplayName(ApplicationStatus status) => status switch
    {
        ApplicationStatus.Uploaded => "Yüklendi",
        ApplicationStatus.UnderReview => "İnceleniyor",
        ApplicationStatus.FaultyOrMissing => "Hatalı / Eksik",
        ApplicationStatus.Approved => "Onaylandı",
        ApplicationStatus.Completed => "Tamamlandı",
        _ => status.ToString()
    };

    /// <summary>
    /// Başvuru ve evrakları bu statüdeyken bu kullanıcı tarafından güncellenebilir/silinebilir mi?
    /// Öğrenci (ROLE_STUDENT) yalnızca Yüklendi ve Hatalı/Eksik statüsünde değişiklik yapabilir;
    /// İnceleniyor, Onaylandı ve Tamamlandı statülerinde başvuru öğrenci için kilitlidir.
    /// Admin (ROLE_ADMIN) Tamamlandı dahil her statüde güncelleyebilir ve silebilir (hocanın 3. cevabı).
    /// </summary>
    public bool CanModifyOrDelete(ApplicationStatus currentStatus, User currentUser)
    {
        if (currentUser is null)
            return false;

        return currentUser.Role switch
        {
            UserRole.Student => StudentEditableStatuses.Contains(currentStatus),
            UserRole.Admin => true,
            _ => false
        };
    }

    /// <summary>
    /// Başvuru veya evrakı üzerinde güncelleme yapılmadan önce çağrılır; izin yoksa istisna fırlatır.
    /// </summary>
    /// <exception cref="LoginRequiredException">Kullanıcı yok.</exception>
    /// <exception cref="ForbiddenAccessException">Başkasının başvurusu ya da içerik değiştiremeyen rol.</exception>
    /// <exception cref="KeyNotFoundException">Başvuru bulunamadı.</exception>
    /// <exception cref="InvalidOperationException">Başvurunun statüsü güncellemeye izin vermiyor.</exception>
    public void ValidateUpdateOperation(int applicationId, User currentUser) =>
        ValidateModification(applicationId, currentUser, ModificationKind.Update);

    /// <summary>
    /// Başvuru veya evrakı silinmeden önce çağrılır; izin yoksa istisna fırlatır.
    /// </summary>
    /// <exception cref="LoginRequiredException">Kullanıcı yok.</exception>
    /// <exception cref="ForbiddenAccessException">Başkasının başvurusu ya da içerik değiştiremeyen rol.</exception>
    /// <exception cref="KeyNotFoundException">Başvuru bulunamadı.</exception>
    /// <exception cref="InvalidOperationException">Başvurunun statüsü silmeye izin vermiyor.</exception>
    public void ValidateDeleteOperation(int applicationId, User currentUser) =>
        ValidateModification(applicationId, currentUser, ModificationKind.Delete);

    /// <summary>
    /// Başvurunun statüsünü değiştirir ve geçişi kaydeder.
    /// Kontrol sirasi: giris -> yetki ve sahiplik -> akis kurali -> gerekce -> onay ve tamamlanma kontrolleri.
    /// </summary>
    /// <exception cref="LoginRequiredException">Kullanıcı yok.</exception>
    /// <exception cref="ForbiddenAccessException">Rol ya da sahiplik bu geçişe yetmiyor.</exception>
    /// <exception cref="KeyNotFoundException">Başvuru bulunamadı.</exception>
    /// <exception cref="InvalidOperationException">Geçiş akış kurallarına ya da iş kurallarına aykırı.</exception>
    /// <exception cref="ArgumentException">Zorunlu gerekçe yazılmamış.</exception>
    public StatusTransitionLog ChangeStatus(int applicationId, ApplicationStatus newStatus, User currentUser, string note)
    {
        if (currentUser is null)
            throw new LoginRequiredException("Statü değiştirmek için giriş yapmalısınız.");

        using var connection = _database.GetConnection();
        using var transaction = connection.BeginTransaction();

        EnsureUserInDatabase(connection, transaction, currentUser);

        var application = LoadApplication(connection, transaction, applicationId)
            ?? throw new KeyNotFoundException($"#{applicationId} numaralı staj başvurusu bulunamadı.");

        EnsureAuthorized(currentUser, newStatus, application);

        if (!CanTransition(application.Status, newStatus))
        {
            var allowed = GetAllowedTransitions(application.Status);
            string hint = allowed.Count == 0
                ? $"'{GetDisplayName(application.Status)}' son statüdür, değiştirilemez."
                : $"İzin verilen: {string.Join(", ", allowed.Select(GetDisplayName))}.";
            throw new InvalidOperationException(
                $"'{GetDisplayName(application.Status)}' statüsünden '{GetDisplayName(newStatus)}' statüsüne geçilemez. {hint}");
        }

        if (RequiresNote(newStatus) && string.IsNullOrWhiteSpace(note))
            throw new ArgumentException($"'{GetDisplayName(newStatus)}' için gerekçe yazılmalıdır.", nameof(note));

        int? approvedDocumentId = null;
        if (newStatus is ApplicationStatus.Approved or ApplicationStatus.Completed)
            approvedDocumentId = EnsureReadyForApproval(connection, transaction, applicationId, application);

        DateTime now = _utcNow();

        using (var update = connection.CreateCommand())
        {
            update.Transaction = transaction;
            // Statü okunduktan sonra başka biri değiştirdiyse güncelleme yapılmaz.
            update.CommandText = "UPDATE Internships SET Status = $new WHERE Id = $id AND Status = $old;";
            update.Parameters.AddWithValue("$new", newStatus.ToString());
            update.Parameters.AddWithValue("$id", applicationId);
            update.Parameters.AddWithValue("$old", application.Status.ToString());

            if (update.ExecuteNonQuery() == 0)
                throw new InvalidOperationException("Başvurunun statüsü bu sırada değişti; lütfen yeniden deneyin.");
        }

        if (approvedDocumentId.HasValue)
        {
            using var approveDocument = connection.CreateCommand();
            approveDocument.Transaction = transaction;
            approveDocument.CommandText = "UPDATE Documents SET IsApproved = 1, RejectionReason = NULL WHERE Id = $id;";
            approveDocument.Parameters.AddWithValue("$id", approvedDocumentId.Value);
            approveDocument.ExecuteNonQuery();
        }

        var log = new StatusTransitionLog(applicationId, application.Status, newStatus, currentUser.Id, now, note);

        using (var insert = connection.CreateCommand())
        {
            insert.Transaction = transaction;
            insert.CommandText = """
                INSERT INTO StatusTransitionLogs (ApplicationId, OldStatus, NewStatus, ChangedByUserId, ChangedAt, Note)
                VALUES ($applicationId, $old, $new, $userId, $changedAt, $note);
                SELECT last_insert_rowid();
                """;
            insert.Parameters.AddWithValue("$applicationId", applicationId);
            insert.Parameters.AddWithValue("$old", log.OldStatus.ToString());
            insert.Parameters.AddWithValue("$new", log.NewStatus.ToString());
            insert.Parameters.AddWithValue("$userId", currentUser.Id);
            insert.Parameters.AddWithValue("$changedAt", now.ToString("o", CultureInfo.InvariantCulture));
            insert.Parameters.AddWithValue("$note", (object?)log.Note ?? DBNull.Value);
            log.Id = Convert.ToInt32(insert.ExecuteScalar(), CultureInfo.InvariantCulture);
        }

        transaction.Commit();
        return log;
    }

    /// <summary>
    /// Başvurunun statü geçmişi, eskiden yeniye.
    /// </summary>
    public IReadOnlyList<StatusTransitionLog> GetHistory(int applicationId)
    {
        using var connection = _database.GetConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT Id, ApplicationId, OldStatus, NewStatus, ChangedByUserId, ChangedAt, Note
            FROM StatusTransitionLogs
            WHERE ApplicationId = $id
            ORDER BY Id;
            """;
        command.Parameters.AddWithValue("$id", applicationId);

        var history = new List<StatusTransitionLog>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            history.Add(new StatusTransitionLog(
                reader.GetInt32(1),
                Enum.Parse<ApplicationStatus>(reader.GetString(2)),
                Enum.Parse<ApplicationStatus>(reader.GetString(3)),
                reader.IsDBNull(4) ? null : reader.GetInt32(4),
                DateTime.Parse(reader.GetString(5), CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal),
                reader.IsDBNull(6) ? null : reader.GetString(6))
            {
                Id = reader.GetInt32(0)
            });
        }
        return history;
    }

    private void ValidateModification(int applicationId, User currentUser, ModificationKind kind)
    {
        string action = kind == ModificationKind.Update ? "güncellenemez" : "silinemez";

        if (currentUser is null)
            throw new LoginRequiredException("Başvuru üzerinde değişiklik yapmak için giriş yapmalısınız.");

        using var connection = _database.GetConnection();
        using var transaction = connection.BeginTransaction();

        EnsureUserInDatabase(connection, transaction, currentUser);

        var application = LoadApplication(connection, transaction, applicationId)
            ?? throw new KeyNotFoundException($"#{applicationId} numaralı staj başvurusu bulunamadı.");

        if (currentUser.Role == UserRole.Student && application.StudentId != currentUser.Id)
            throw new ForbiddenAccessException(
                "403 Yetkisiz Erişim: Yalnızca kendi staj başvurunuz ve evraklarınız üzerinde değişiklik yapabilirsiniz.");

        if (CanModifyOrDelete(application.Status, currentUser))
            return;

        string statusName = GetDisplayName(application.Status);
        throw new InvalidOperationException(application.Status switch
        {
            ApplicationStatus.Completed =>
                $"'{statusName}' statüsündeki başvuru ve evrakları öğrenci tarafından {action}; " +
                "değişiklik için yönetici ile iletişime geçin.",
            ApplicationStatus.UnderReview or ApplicationStatus.Approved =>
                $"Başvuru '{statusName}' statüsündeyken başvuru ve evrakları {action}. " +
                "Değişiklik yalnızca 'Yüklendi' veya 'Hatalı / Eksik' statüsünde yapılabilir.",
            _ =>
                $"'{statusName}' statüsündeki başvuru ve evrakları {action}."
        });
    }

    private static void EnsureAuthorized(User user, ApplicationStatus newStatus, ApplicationInfo application)
    {
        var permission = RequiredPermissions[newStatus];
        if (!user.HasPermission(permission))
            throw new ForbiddenAccessException(
                $"403 Yetkisiz Erişim: {RolePermissions.ToRoleName(user.Role)} rolü başvuruyu " +
                $"'{GetDisplayName(newStatus)}' statüsüne alamaz.");

        // Öğrenci yalnızca kendi başvurusunu yeniden yükleyebilir.
        if (newStatus == ApplicationStatus.Uploaded && application.StudentId != user.Id)
            throw new ForbiddenAccessException(
                "403 Yetkisiz Erişim: Yalnızca kendi staj başvurunuzu yeniden yükleyebilirsiniz.");
    }

    private static void EnsureUserInDatabase(SqliteConnection connection, SqliteTransaction transaction, User user) =>
        UserGuard.EnsureUserInDatabase(connection, transaction, user);

    private static int EnsureReadyForApproval(
        SqliteConnection connection, SqliteTransaction transaction, int applicationId, ApplicationInfo application)
    {
        if (!application.Ek16Verified)
            throw new InvalidOperationException("Basvuru Ek-16 veri setiyle yeniden dogrulanmali.");

        var details = new InternshipApplication
        {
            AcademicYear = application.AcademicYear,
            Semester = application.Semester,
            StartDate = application.StartDate,
            EndDate = application.EndDate,
            CompanyName = application.CompanyName ?? string.Empty,
            Address = application.Address, Phone = application.Phone, Email = application.Email, Fax = application.Fax,
            EngineerCount = application.EngineerCount,
            EngineerGraduationDepartment = application.EngineerGraduationDepartment
        };
        var errors = EngineerApprovalValidator.GetEngineerErrors(details).ToList();
        errors.AddRange(FormatValidator.GetApplicationErrors(details));
        var calendar = InternshipDateValidator.IsAcademicYearValid(details.AcademicYear)
            ? AcademicCalendars.GetForAcademicYear(details.AcademicYear) : AcademicCalendars.Current;
        errors.AddRange(InternshipDateValidator.GetApplicationErrors(details, calendar));

        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            SELECT Id, FilePath, StudentApprovalPresent, AdvisorApprovalPresent, EmployerFieldsComplete,
                   EmployerStampPresent, EmployerSignaturePresent, ApprovalReviewedByUserId, ApprovalReviewedAt, UploadedAt,
                   StudentFieldsVerified, StudentFieldsVerifiedAt, InternshipFieldsVerified, InternshipFieldsVerifiedAt,
                   ContactFieldsVerified, ContactFieldsVerifiedAt, ApplicationFieldsVerified, ApplicationFieldsVerifiedAt
            FROM Documents WHERE InternshipId = $id AND DocumentType = 'Ek16' ORDER BY Id DESC LIMIT 1;
            """;
        command.Parameters.AddWithValue("$id", applicationId);
        using var reader = command.ExecuteReader();
        if (!reader.Read())
            throw new InvalidOperationException("Onay icin yuklenmis bir Ek-16 belgesi gerekli.");

        int documentId = reader.GetInt32(0);
        if (reader.GetInt32(10) != 1 || reader.IsDBNull(11))
            errors.Add("Son belgenin ogrenci bilgileri basvuru ve veri setiyle karsilastirilmali.");
        if (reader.GetInt32(12) != 1 || reader.IsDBNull(13))
            errors.Add("Son belgenin staj tarihleri ve gun bilgileri basvuruyla karsilastirilmali.");
        if (reader.GetInt32(14) != 1 || reader.IsDBNull(15))
            errors.Add("Son belgenin iletisim bilgileri basvuruyla karsilastirilmali.");
        if (reader.GetInt32(16) != 1 || reader.IsDBNull(17))
            errors.Add("Son belgenin yariyil ve isyeri bilgileri basvuruyla karsilastirilmali.");
        bool? Flag(int index) => reader.IsDBNull(index) ? null : reader.GetInt32(index) == 1;
        var fields = new DocumentApprovalFields(Flag(2), Flag(3), Flag(4), Flag(5), Flag(6));
        errors.AddRange(EngineerApprovalValidator.GetDocumentApprovalErrors(fields));
        if (reader.IsDBNull(7) || reader.IsDBNull(8))
            errors.Add("Son yuklenen belge icin yonetici incelemesi kaydedilmeli.");

        // Onay gunu yerine son belgenin gercek teslim zamani kullanilir.
        DateTime? uploadedAt = !reader.IsDBNull(9)
            && DateTime.TryParse(reader.GetString(9), CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var timestamp)
            ? timestamp : null;
        errors.AddRange(InternshipDateValidator.GetDocumentDeliveryErrors(uploadedAt, application.StartDate));

        var document = new Ek16Document(applicationId, reader.GetString(1));
        errors.AddRange(document.GetValidationErrors());
        if (errors.Count > 0)
            throw new InvalidOperationException(string.Join(" ", errors));

        return documentId;
    }

    private static ApplicationInfo? LoadApplication(SqliteConnection connection, SqliteTransaction transaction, int applicationId)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            SELECT Status, StudentId, Ek16Verified, EngineerCount, EngineerGraduationDepartment,
                   AcademicYear, StartDate, EndDate, Semester, CompanyName, Address, Phone, Email, Fax
            FROM Internships WHERE Id = $id;
            """;
        command.Parameters.AddWithValue("$id", applicationId);

        using var reader = command.ExecuteReader();
        if (!reader.Read())
            return null;

        if (!Enum.TryParse<ApplicationStatus>(reader.GetString(0), out var status))
            throw new InvalidOperationException($"#{applicationId} başvurusunda bilinmeyen statü: {reader.GetString(0)}");

        string? Str(int index) => reader.IsDBNull(index) ? null : reader.GetString(index);
        DateTime Date(int index) => DateTime.TryParseExact(Str(index), "yyyy-MM-dd", CultureInfo.InvariantCulture,
            DateTimeStyles.None, out var value) ? value : default;
        return new ApplicationInfo(status, reader.GetInt32(1), reader.GetInt32(2) == 1, reader.GetInt32(3),
            Str(4), Str(5) ?? string.Empty, Date(6), Date(7), Str(8), Str(9), Str(10), Str(11), Str(12), Str(13));
    }

    private sealed record ApplicationInfo(ApplicationStatus Status, int StudentId, bool Ek16Verified, int EngineerCount,
        string? EngineerGraduationDepartment, string AcademicYear, DateTime StartDate, DateTime EndDate,
        string? Semester, string? CompanyName, string? Address, string? Phone, string? Email, string? Fax);
}
