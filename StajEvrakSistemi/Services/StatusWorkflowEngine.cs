using System.Globalization;
using Microsoft.Data.Sqlite;
using StajEvrakSistemi.Core;
using StajEvrakSistemi.Data;
using StajEvrakSistemi.Models;

namespace StajEvrakSistemi.Services;

/// <summary>
/// Staj başvurusunun statü akışını yönetir:
/// Yüklendi -> İnceleniyor -> Tamamlandı / Reddedildi / Revizyon Gerekli -> Yüklendi ...
/// Her geçiş yetki ve iş kuralı kontrolünden geçer ve StatusTransitionLogs tablosuna yazılır.
/// </summary>
public class StatusWorkflowEngine
{
    // Geçerli geçişler. Reddedildi ve Tamamlandı son statülerdir.
    private static readonly IReadOnlyDictionary<ApplicationStatus, ApplicationStatus[]> Transitions =
        new Dictionary<ApplicationStatus, ApplicationStatus[]>
        {
            [ApplicationStatus.Uploaded] = new[] { ApplicationStatus.UnderReview },
            [ApplicationStatus.UnderReview] = new[]
            {
                ApplicationStatus.RevisionRequired, ApplicationStatus.Rejected, ApplicationStatus.Completed
            },
            [ApplicationStatus.RevisionRequired] = new[] { ApplicationStatus.Uploaded },
            [ApplicationStatus.Rejected] = Array.Empty<ApplicationStatus>(),
            [ApplicationStatus.Completed] = Array.Empty<ApplicationStatus>()
        };

    // Hedef statüye geçmek için gereken izin.
    private static readonly IReadOnlyDictionary<ApplicationStatus, Permission> RequiredPermissions =
        new Dictionary<ApplicationStatus, Permission>
        {
            [ApplicationStatus.Uploaded] = Permission.UploadDocument,
            [ApplicationStatus.UnderReview] = Permission.ReviewDocument,
            [ApplicationStatus.RevisionRequired] = Permission.ReviewDocument,
            [ApplicationStatus.Rejected] = Permission.ReviewDocument,
            [ApplicationStatus.Completed] = Permission.ApproveInternship
        };

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
    /// Ret ve revizyon talebinde gerekçe yazılması zorunludur.
    /// </summary>
    public static bool RequiresNote(ApplicationStatus targetStatus) =>
        targetStatus is ApplicationStatus.RevisionRequired or ApplicationStatus.Rejected;

    public static string GetDisplayName(ApplicationStatus status) => status switch
    {
        ApplicationStatus.Uploaded => "Yüklendi",
        ApplicationStatus.UnderReview => "İnceleniyor",
        ApplicationStatus.RevisionRequired => "Revizyon Gerekli",
        ApplicationStatus.Rejected => "Reddedildi",
        ApplicationStatus.Completed => "Tamamlandı",
        _ => status.ToString()
    };

    /// <summary>
    /// Başvurunun statüsünü değiştirir ve geçişi kaydeder.
    /// Kontrol sırası: giriş (401) -> rol izni ve sahiplik (403) -> akış kuralı -> gerekçe -> Ek-16 onayı.
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

        if (newStatus == ApplicationStatus.Completed && !application.Ek16Verified)
            throw new InvalidOperationException(
                "Başvuru Ek-16 listesiyle doğrulanmadan 'Tamamlandı' statüsüne alınamaz.");

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

    // Bellekte oluşturulmuş bir kullanıcı nesnesiyle yetki atlatılamasın diye rol veritabanından da doğrulanır.
    private static void EnsureUserInDatabase(SqliteConnection connection, SqliteTransaction transaction, User user)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT Role FROM Users WHERE Id = $id;";
        command.Parameters.AddWithValue("$id", user.Id);

        if (command.ExecuteScalar() as string != user.Role.ToString())
            throw new ForbiddenAccessException("403 Yetkisiz Erişim: Kullanıcı kaydı doğrulanamadı.");
    }

    private static ApplicationInfo? LoadApplication(SqliteConnection connection, SqliteTransaction transaction, int applicationId)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT Status, StudentId, Ek16Verified FROM Internships WHERE Id = $id;";
        command.Parameters.AddWithValue("$id", applicationId);

        using var reader = command.ExecuteReader();
        if (!reader.Read())
            return null;

        if (!Enum.TryParse<ApplicationStatus>(reader.GetString(0), out var status))
            throw new InvalidOperationException($"#{applicationId} başvurusunda bilinmeyen statü: {reader.GetString(0)}");

        return new ApplicationInfo(status, reader.GetInt32(1), reader.GetInt32(2) == 1);
    }

    private sealed record ApplicationInfo(ApplicationStatus Status, int StudentId, bool Ek16Verified);
}
