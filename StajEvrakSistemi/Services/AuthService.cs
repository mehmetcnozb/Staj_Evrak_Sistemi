using System.Globalization;
using Microsoft.Data.Sqlite;
using StajEvrakSistemi.Core;
using StajEvrakSistemi.Data;
using StajEvrakSistemi.Models;

namespace StajEvrakSistemi.Services;

/// <summary>
/// Kullanıcı kaydı ve girişi. Üst üste hatalı girişlerde hesabı geçici olarak kilitler.
/// </summary>
public class AuthService
{
    public const int MaxFailedAttempts = 3;
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
    /// </summary>
    public int Register(User user, string password)
    {
        ArgumentNullException.ThrowIfNull(user);

        if (string.IsNullOrEmpty(password) || password.Length < MinPasswordLength)
            throw new ArgumentException($"Şifre en az {MinPasswordLength} karakter olmalıdır.", nameof(password));

        string passwordHash = PasswordHasher.HashPassword(password);
        user.SetPasswordHash(passwordHash);

        if (!user.Validate())
            throw new ArgumentException("Kullanıcı bilgileri geçersiz.", nameof(user));

        using var connection = _database.GetConnection();
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
    /// E-posta veya T.C. kimlik no ile giriş yapar. 3 hatalı denemede hesap 15 dakika kilitlenir.
    /// </summary>
    public LoginResult Login(string emailOrIdentity, string password)
    {
        if (string.IsNullOrWhiteSpace(emailOrIdentity) || string.IsNullOrEmpty(password))
            return new LoginResult(LoginStatus.InvalidCredentials);

        using var connection = _database.GetConnection();

        var found = FindUser(connection, emailOrIdentity.Trim());
        if (found is null)
            return new LoginResult(LoginStatus.InvalidCredentials);

        var (user, passwordHash) = found.Value;
        DateTime now = _utcNow();

        if (user.IsLockedOut(now))
            return new LoginResult(LoginStatus.LockedOut, RemainingLockout: user.LockoutEnd!.Value - now);

        // Kilit süresi dolduysa sayaç sıfırlanır, kullanıcı yeniden 3 hak kazanır.
        if (user.LockoutEnd.HasValue)
        {
            user.ResetLoginAttempts();
            SaveLoginState(connection, user);
        }

        if (!PasswordHasher.VerifyPassword(password, passwordHash))
        {
            user.RecordFailedLogin(now, MaxFailedAttempts, LockoutDuration);
            SaveLoginState(connection, user);

            return user.IsLockedOut(now)
                ? new LoginResult(LoginStatus.LockedOut, RemainingLockout: user.LockoutEnd!.Value - now)
                : new LoginResult(LoginStatus.InvalidCredentials,
                    RemainingAttempts: MaxFailedAttempts - user.FailedLoginAttempts);
        }

        if (user.FailedLoginAttempts != 0 || user.LockoutEnd.HasValue)
        {
            user.ResetLoginAttempts();
            SaveLoginState(connection, user);
        }

        return new LoginResult(LoginStatus.Success, user);
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
            UserRole.Commission => new CommissionMember(identity, fullName, email, phone),
            var role => throw new InvalidOperationException($"Bilinmeyen rol: {role}")
        };

        user.Id = reader.GetInt32(0);
        user.SetPasswordHash(passwordHash);
        user.CreatedAt = FromDbDate(reader.GetString(8));
        user.FailedLoginAttempts = reader.GetInt32(9);
        user.LockoutEnd = reader.IsDBNull(10) ? null : FromDbDate(reader.GetString(10));

        return (user, passwordHash);
    }

    private static void SaveLoginState(SqliteConnection connection, User user)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "UPDATE Users SET FailedLoginAttempts = $attempts, LockoutEnd = $lockoutEnd WHERE Id = $id;";
        command.Parameters.AddWithValue("$attempts", user.FailedLoginAttempts);
        command.Parameters.AddWithValue("$lockoutEnd", user.LockoutEnd.HasValue ? ToDbDate(user.LockoutEnd.Value) : DBNull.Value);
        command.Parameters.AddWithValue("$id", user.Id);
        command.ExecuteNonQuery();
    }

    private static string ToDbDate(DateTime utc) => utc.ToString("o", CultureInfo.InvariantCulture);

    private static DateTime FromDbDate(string value) =>
        DateTime.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
}
