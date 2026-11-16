using System.Globalization;
using Microsoft.Data.Sqlite;
using StajEvrakSistemi.Core;
using StajEvrakSistemi.Data;
using StajEvrakSistemi.Models;

namespace StajEvrakSistemi.Services;

/// <summary>
/// Kullanici kaydi ve girisi. Bes hatali denemede hesabi kilitler ve girisleri loglar.
/// </summary>
public class AuthService
{
    /// <summary>
    /// Şartname 4.1: 5 hatalı denemede sistem kilitlenir.
    /// </summary>
    public const int MaxFailedAttempts = 5;

    private const string SelectUserColumns =
        "Id, IdentityNumber, StudentNumber, FullName, Email, Phone, PasswordHash, Role, CreatedAt, FailedLoginAttempts, IsForeign";

    private readonly DatabaseManager _database;
    private readonly Func<DateTime> _utcNow;

    public AuthService(DatabaseManager database, Func<DateTime>? utcNow = null)
    {
        _database = database;
        _utcNow = utcNow ?? (() => DateTime.UtcNow);
    }

    /// <summary>
    /// Şifreyi hashleyip kullanıcıyı veritabanına kaydeder ve oluşan Id'yi döndürür.
    /// Öğrenciler hesaplarını kendileri açabilir. Yönetici hesabı yalnızca öğretim görevlisi
    /// e-postasıyla açılır (bkz. <see cref="FacultyEmailPolicy"/>).
    /// </summary>
    /// <exception cref="ArgumentException">Yönetici e-postası öğretim görevlisi adresi değil.</exception>
    public int Register(User user, string password, User? createdBy = null)
    {
        ArgumentNullException.ThrowIfNull(user);

        if (string.IsNullOrEmpty(password))
            throw new ArgumentException("Sifre bos olamaz.", nameof(password));

        using var connection = _database.GetConnection();
        EnsureCanRegister(connection, user, createdBy);

        if (!IdentityNumberValidator.IsValidForNationality(user.IdentityNumber, user.IsForeign, validateChecksum: true))
            throw new ArgumentException(user.IsForeign
                ? "Yabanci kimlik numarasi 11 rakamdan olusmali ve 99 ile baslamali."
                : "T.C. kimlik numarasinin bicimi veya kontrol haneleri gecersiz.", nameof(user));

        string passwordHash = PasswordHasher.HashPassword(password);
        user.SetPasswordHash(passwordHash);

        if (!user.Validate())
            throw new ArgumentException("Kullanıcı bilgileri geçersiz.", nameof(user));

        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO Users (IdentityNumber, StudentNumber, FullName, Email, Phone, PasswordHash, Role, CreatedAt, IsForeign)
            VALUES ($identity, $studentNumber, $fullName, $email, $phone, $hash, $role, $createdAt, $isForeign);
            SELECT last_insert_rowid();
            """;
        command.Parameters.AddWithValue("$identity", user.IdentityNumber);
        command.Parameters.AddWithValue("$studentNumber", (user as Student)?.StudentNumber ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("$fullName", user.FullName);
        command.Parameters.AddWithValue("$email", user.Email);
        command.Parameters.AddWithValue("$phone", user.Phone ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("$hash", passwordHash);
        command.Parameters.AddWithValue("$role", user.Role.ToString());
        command.Parameters.AddWithValue("$createdAt", ToDbDate(user.CreatedAt));
        command.Parameters.AddWithValue("$isForeign", user.IsForeign ? 1 : 0);

        try
        {
            user.Id = Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture);
            return user.Id;
        }
        catch (SqliteException ex) when (ex.SqliteExtendedErrorCode == 2067 || ex.SqliteExtendedErrorCode == 1555)
        {
            // SQLITE_CONSTRAINT_UNIQUE / SQLITE_CONSTRAINT_PRIMARYKEY
            throw new InvalidOperationException(
                "Bu T.C. kimlik no, e-posta veya öğrenci numarası ile zaten bir kayıt var.", ex);
        }
    }

    /// <summary>
    /// E-posta, kimlik veya ogrenci numarasi ile giris yapar. Bes hatali denemede hesap kilitlenir.
    /// Başarısız her deneme FailedLoginLogs tablosuna yazılır.
    /// </summary>
    public LoginResult Login(string emailOrIdentity, string password) =>
        Login(emailOrIdentity, password, adminOnly: false);

    /// <summary>
    /// Yönetici girişi. Yalnızca yönetici hesabı ve yalnızca e-posta ile giriş yapılır.
    /// Kilit ve başarısız giriş kaydı öğrenci girişiyle aynıdır.
    /// </summary>
    public LoginResult LoginAdmin(string email, string password) =>
        Login(email, password, adminOnly: true);

    /// <summary>
    /// Bütün yönetici hesaplarını siler. Başvurular ve belgeler kalır, yöneticiye bağlı alanlar boşalır.
    /// </summary>
    public int DeleteAllAdmins()
    {
        using var connection = _database.GetConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM Users WHERE Role = 'Admin';";
        return command.ExecuteNonQuery();
    }

    private LoginResult Login(string emailOrIdentity, string password, bool adminOnly)
    {
        if (string.IsNullOrWhiteSpace(emailOrIdentity) || string.IsNullOrEmpty(password))
            return new LoginResult(LoginStatus.InvalidCredentials);

        using var connection = _database.GetConnection();
        string identifier = emailOrIdentity.Trim();
        DateTime now = _utcNow();

        var found = FindUser(connection, identifier, adminOnly);
        if (found is null)
        {
            LogFailedLogin(connection, null, null, identifier, FailedLoginReason.UnknownUser, now);
            return new LoginResult(LoginStatus.InvalidCredentials);
        }

        var (user, passwordHash) = found.Value;

        if (user.IsLockedOut(MaxFailedAttempts))
        {
            LogFailedLogin(connection, null, user.Id, identifier, FailedLoginReason.LockedOut, now);
            return new LoginResult(LoginStatus.LockedOut);
        }

        if (!PasswordHasher.VerifyPassword(password, passwordHash))
        {
            using var transaction = connection.BeginTransaction();
            user.RecordFailedLogin();
            SaveLoginState(connection, transaction, user);
            LogFailedLogin(connection, transaction, user.Id, identifier, FailedLoginReason.WrongPassword, now);
            transaction.Commit();

            return user.IsLockedOut(MaxFailedAttempts)
                ? new LoginResult(LoginStatus.LockedOut)
                : new LoginResult(LoginStatus.InvalidCredentials,
                    RemainingAttempts: MaxFailedAttempts - user.FailedLoginAttempts);
        }

        if (user.FailedLoginAttempts != 0)
        {
            user.ResetLoginAttempts();
            SaveLoginState(connection, null, user);
        }

        return new LoginResult(LoginStatus.Success, user);
    }

    /// <summary>
    /// Id'ye göre kullanıcıyı yükler (ör. oturumdaki UserId'den kullanıcı nesnesine geçmek için).
    /// </summary>
    public User? GetUser(int userId)
    {
        using var connection = _database.GetConnection();
        using var command = connection.CreateCommand();
        command.CommandText = $"SELECT {SelectUserColumns} FROM Users WHERE Id = $id;";
        command.Parameters.AddWithValue("$id", userId);

        using var reader = command.ExecuteReader();
        return reader.Read() ? MapUser(reader).User : null;
    }

    /// <summary>
    /// Başarısız giriş kayıtlarını yeniden eskiye sıralı döndürür. Yalnızca kullanıcı yönetimi yetkisi olanlar görebilir.
    /// </summary>
    public IReadOnlyList<FailedLoginLog> GetFailedLogins(User requester, int? userId = null, int limit = 100)
    {
        if (requester is null)
            throw new LoginRequiredException("Giriş kayıtlarını görmek için giriş yapmalısınız.");
        if (!requester.HasPermission(Permission.ManageUsers))
            throw new ForbiddenAccessException("403 Yetkisiz Erişim: Giriş kayıtlarını yalnızca yönetici görebilir.");

        using var connection = _database.GetConnection();
        UserGuard.EnsureUserInDatabase(connection, null, requester);

        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT Id, UserId, Identifier, Reason, AttemptedAt
            FROM FailedLoginLogs
            WHERE ($userId IS NULL OR UserId = $userId)
            ORDER BY Id DESC
            LIMIT $limit;
            """;
        command.Parameters.AddWithValue("$userId", (object?)userId ?? DBNull.Value);
        command.Parameters.AddWithValue("$limit", Math.Clamp(limit, 1, 1000));

        var logs = new List<FailedLoginLog>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            logs.Add(new FailedLoginLog(
                reader.GetInt32(0),
                reader.IsDBNull(1) ? null : reader.GetInt32(1),
                reader.GetString(2),
                Enum.Parse<FailedLoginReason>(reader.GetString(3)),
                FromDbDate(reader.GetString(4))));
        }
        return logs;
    }

    private static void EnsureCanRegister(SqliteConnection connection, User user, User? createdBy)
    {
        if (user.Role == UserRole.Student)
            return;

        // Yönetici hesabı, kim açarsa açsın, öğretim görevlisi e-postası ister.
        if (!FacultyEmailPolicy.IsFacultyEmail(user.Email))
            throw new ArgumentException(FacultyEmailPolicy.Requirement, nameof(user));

        if (createdBy is null)
            return;

        UserGuard.EnsureUserInDatabase(connection, null, createdBy);
        if (!createdBy.HasPermission(Permission.ManageUsers))
            throw new ForbiddenAccessException("403 Yetkisiz Erişim: Kullanıcı oluşturma yetkiniz yok.");
    }

    private static (User User, string PasswordHash)? FindUser(SqliteConnection connection, string identifier, bool adminOnly)
    {
        using var command = connection.CreateCommand();
        // Aynı değer bir kimlik numarası ve bir öğrenci numarasıysa mevcut kimlik girişini koru.
        command.CommandText = adminOnly
            ? $"SELECT {SelectUserColumns} FROM Users WHERE Role = 'Admin' AND Email = $email LIMIT 1;"
            : $"""
            SELECT {SelectUserColumns} FROM Users
            WHERE Email = $email OR IdentityNumber = $identifier
                OR (Role = 'Student' AND StudentNumber = $identifier)
            ORDER BY CASE WHEN Email = $email THEN 0 WHEN IdentityNumber = $identifier THEN 1 ELSE 2 END
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("$email", identifier.ToLowerInvariant());
        command.Parameters.AddWithValue("$identifier", identifier);

        using var reader = command.ExecuteReader();
        return reader.Read() ? MapUser(reader) : null;
    }

    private static (User User, string PasswordHash) MapUser(SqliteDataReader reader)
    {
        string identity = reader.GetString(1);
        string? studentNumber = reader.IsDBNull(2) ? null : reader.GetString(2);
        string fullName = reader.GetString(3);
        string email = reader.GetString(4);
        string? phone = reader.IsDBNull(5) ? null : reader.GetString(5);
        string passwordHash = reader.GetString(6);

        User user = Enum.Parse<UserRole>(reader.GetString(7)) switch
        {
            UserRole.Student => new Student(identity, studentNumber ?? string.Empty, fullName, email, phone),
            UserRole.Admin => new Admin(identity, fullName, email, phone),
            var role => throw new InvalidOperationException($"Bilinmeyen rol: {role}")
        };

        user.Id = reader.GetInt32(0);
        user.SetPasswordHash(passwordHash);
        user.CreatedAt = FromDbDate(reader.GetString(8));
        user.FailedLoginAttempts = reader.GetInt32(9);
        user.IsForeign = reader.GetInt32(10) == 1;

        return (user, passwordHash);
    }

    private static void SaveLoginState(SqliteConnection connection, SqliteTransaction? transaction, User user)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "UPDATE Users SET FailedLoginAttempts = $attempts WHERE Id = $id;";
        command.Parameters.AddWithValue("$attempts", user.FailedLoginAttempts);
        command.Parameters.AddWithValue("$id", user.Id);
        command.ExecuteNonQuery();
    }

    private static void LogFailedLogin(
        SqliteConnection connection, SqliteTransaction? transaction, int? userId, string identifier,
        FailedLoginReason reason, DateTime now)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO FailedLoginLogs (UserId, Identifier, Reason, AttemptedAt)
            VALUES ($userId, $identifier, $reason, $attemptedAt);
            """;
        command.Parameters.AddWithValue("$userId", (object?)userId ?? DBNull.Value);
        command.Parameters.AddWithValue("$identifier", identifier.Length > 200 ? identifier[..200] : identifier);
        command.Parameters.AddWithValue("$reason", reason.ToString());
        command.Parameters.AddWithValue("$attemptedAt", ToDbDate(now));
        command.ExecuteNonQuery();
    }

    private static string ToDbDate(DateTime utc) => utc.ToString("o", CultureInfo.InvariantCulture);

    private static DateTime FromDbDate(string value) =>
        DateTime.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
}
