using System.Globalization;
using Microsoft.Data.Sqlite;
using StajEvrakSistemi.Core;
using StajEvrakSistemi.Data;
using StajEvrakSistemi.Models;

namespace StajEvrakSistemi.Services;

/// <summary>
/// Kullanıcı kaydı ve girişi. Üst üste hatalı girişlerde hesabı geçici olarak kilitler ve
/// başarısız girişleri loglar.
/// </summary>
public class AuthService
{
    /// <summary>
    /// Şartname 4.1: 5 hatalı denemede sistem kilitlenir.
    /// </summary>
    public const int MaxFailedAttempts = 5;
    public static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);
    public const int MinPasswordLength = 8;

    private const string SelectUserColumns =
        "Id, IdentityNumber, StudentNumber, FullName, Email, Phone, PasswordHash, Role, CreatedAt, FailedLoginAttempts, LockoutEnd";

    private readonly DatabaseManager _database;
    private readonly Func<DateTime> _utcNow;

    public AuthService(DatabaseManager database, Func<DateTime>? utcNow = null)
    {
        _database = database;
        _utcNow = utcNow ?? (() => DateTime.UtcNow);
    }

    /// <summary>
    /// Şifreyi hashleyip kullanıcıyı veritabanına kaydeder ve oluşan Id'yi döndürür.
    /// Öğrenciler hesaplarını kendileri açabilir. Yönetici hesabını yalnızca bir yönetici (createdBy)
    /// oluşturabilir; sistemde hiç yönetici yoksa ilk yönetici kendiliğinden oluşturulabilir.
    /// </summary>
    /// <exception cref="ForbiddenAccessException">Öğrenci dışı bir hesap yetkisiz biri tarafından açılmaya çalışılıyor.</exception>
    public int Register(User user, string password, User? createdBy = null)
    {
        ArgumentNullException.ThrowIfNull(user);

        if (string.IsNullOrEmpty(password) || password.Length < MinPasswordLength)
            throw new ArgumentException($"Şifre en az {MinPasswordLength} karakter olmalıdır.", nameof(password));

        using var connection = _database.GetConnection();
        EnsureCanRegister(connection, user, createdBy);

        string passwordHash = PasswordHasher.HashPassword(password);
        user.SetPasswordHash(passwordHash);

        if (!user.Validate())
            throw new ArgumentException("Kullanıcı bilgileri geçersiz.", nameof(user));

        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO Users (IdentityNumber, StudentNumber, FullName, Email, Phone, PasswordHash, Role, CreatedAt)
            VALUES ($identity, $studentNumber, $fullName, $email, $phone, $hash, $role, $createdAt);
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
    /// E-posta veya T.C. kimlik no ile giriş yapar. 5 hatalı denemede hesap 15 dakika kilitlenir.
    /// Başarısız her deneme FailedLoginLogs tablosuna yazılır.
    /// </summary>
    public LoginResult Login(string emailOrIdentity, string password)
    {
        if (string.IsNullOrWhiteSpace(emailOrIdentity) || string.IsNullOrEmpty(password))
            return new LoginResult(LoginStatus.InvalidCredentials);

        using var connection = _database.GetConnection();
        string identifier = emailOrIdentity.Trim();
        DateTime now = _utcNow();

        var found = FindUser(connection, identifier);
        if (found is null)
        {
            LogFailedLogin(connection, null, null, identifier, FailedLoginReason.UnknownUser, now);
            return new LoginResult(LoginStatus.InvalidCredentials);
        }

        var (user, passwordHash) = found.Value;

        if (user.IsLockedOut(now))
        {
            LogFailedLogin(connection, null, user.Id, identifier, FailedLoginReason.LockedOut, now);
            return new LoginResult(LoginStatus.LockedOut, RemainingLockout: user.LockoutEnd!.Value - now);
        }

        // Kilit süresi dolduysa sayaç sıfırlanır, kullanıcı yeniden hak kazanır.
        if (user.LockoutEnd.HasValue)
        {
            user.ResetLoginAttempts();
            SaveLoginState(connection, null, user);
        }

        if (!PasswordHasher.VerifyPassword(password, passwordHash))
        {
            using var transaction = connection.BeginTransaction();
            user.RecordFailedLogin(now, MaxFailedAttempts, LockoutDuration);
            SaveLoginState(connection, transaction, user);
            LogFailedLogin(connection, transaction, user.Id, identifier, FailedLoginReason.WrongPassword, now);
            transaction.Commit();

            return user.IsLockedOut(now)
                ? new LoginResult(LoginStatus.LockedOut, RemainingLockout: user.LockoutEnd!.Value - now)
                : new LoginResult(LoginStatus.InvalidCredentials,
                    RemainingAttempts: MaxFailedAttempts - user.FailedLoginAttempts);
        }

        if (user.FailedLoginAttempts != 0 || user.LockoutEnd.HasValue)
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

        // Sistemde hiç yönetici yoksa ilk yönetici hesabı açılabilir.
        if (user.Role == UserRole.Admin && CountAdmins(connection) == 0)
            return;

        if (createdBy is null)
            throw new ForbiddenAccessException(
                "403 Yetkisiz Erişim: Yalnızca öğrenci hesabı kendi kendine açılabilir; yönetici hesabını yalnızca bir yönetici oluşturabilir.");

        UserGuard.EnsureUserInDatabase(connection, null, createdBy);
        if (!createdBy.HasPermission(Permission.ManageUsers))
            throw new ForbiddenAccessException("403 Yetkisiz Erişim: Kullanıcı oluşturma yetkiniz yok.");
    }

    private static long CountAdmins(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM Users WHERE Role = 'Admin';";
        return (long)command.ExecuteScalar()!;
    }

    private static (User User, string PasswordHash)? FindUser(SqliteConnection connection, string emailOrIdentity)
    {
        using var command = connection.CreateCommand();
        command.CommandText = $"SELECT {SelectUserColumns} FROM Users WHERE Email = $email OR IdentityNumber = $identity LIMIT 1;";
        command.Parameters.AddWithValue("$email", emailOrIdentity.ToLowerInvariant());
        command.Parameters.AddWithValue("$identity", emailOrIdentity);

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
        user.LockoutEnd = reader.IsDBNull(10) ? null : FromDbDate(reader.GetString(10));

        return (user, passwordHash);
    }

    private static void SaveLoginState(SqliteConnection connection, SqliteTransaction? transaction, User user)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "UPDATE Users SET FailedLoginAttempts = $attempts, LockoutEnd = $lockoutEnd WHERE Id = $id;";
        command.Parameters.AddWithValue("$attempts", user.FailedLoginAttempts);
        command.Parameters.AddWithValue("$lockoutEnd", user.LockoutEnd.HasValue ? ToDbDate(user.LockoutEnd.Value) : DBNull.Value);
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
