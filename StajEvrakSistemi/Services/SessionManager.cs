using System.Security.Cryptography;
using System.Text;
using StajEvrakSistemi.Models;

namespace StajEvrakSistemi.Services;

/// <summary>
/// Uygulamadaki aktif oturumu yönetir: başlatır, geçerliliğini doğrular ve sonlandırır.
/// Süresi dolan oturum ilk kontrolde otomatik olarak kapatılır.
/// </summary>
public class SessionManager
{
    public static readonly TimeSpan DefaultSessionDuration = TimeSpan.FromMinutes(30);

    private const int TokenSizeBytes = 32;

    private readonly TimeSpan _sessionDuration;
    private readonly Func<DateTime> _utcNow;
    private UserSession? _current;

    public SessionManager(TimeSpan? sessionDuration = null, Func<DateTime>? utcNow = null)
    {
        _sessionDuration = sessionDuration ?? DefaultSessionDuration;
        if (_sessionDuration <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(sessionDuration), "Oturum süresi pozitif olmalıdır.");

        _utcNow = utcNow ?? (() => DateTime.UtcNow);
    }

    /// <summary>
    /// Geçerli oturum; oturum yoksa veya süresi dolmuşsa null.
    /// </summary>
    public UserSession? CurrentSession => IsSessionValid() ? _current : null;

    public bool IsAuthenticated => IsSessionValid();

    /// <summary>
    /// Başarılı girişten sonra yeni oturum açar. Varsa önceki oturum kapanır.
    /// </summary>
    public UserSession StartSession(User user)
    {
        ArgumentNullException.ThrowIfNull(user);
        if (user.Id <= 0)
            throw new ArgumentException("Oturum yalnızca kayıtlı bir kullanıcı için açılabilir.", nameof(user));

        DateTime now = _utcNow();
        _current = new UserSession(user.Id, user.Role, GenerateToken(), now, now.Add(_sessionDuration));
        return _current;
    }

    public bool IsSessionValid()
    {
        if (_current is null)
            return false;

        if (_current.IsExpired(_utcNow()))
        {
            _current = null;
            return false;
        }

        return true;
    }

    /// <summary>
    /// Verilen token'ın aktif oturuma ait ve süresinin dolmamış olduğunu doğrular.
    /// </summary>
    public bool ValidateSession(string sessionToken)
    {
        if (string.IsNullOrEmpty(sessionToken) || !IsSessionValid())
            return false;

        // Süre farkından bilgi sızmasın diye sabit zamanlı karşılaştırma.
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(sessionToken),
            Encoding.UTF8.GetBytes(_current!.SessionToken));
    }

    /// <summary>
    /// Aktif oturumu kapatır. Kapatılacak oturum varsa true döner.
    /// </summary>
    public bool Logout()
    {
        bool hadSession = _current is not null;
        _current = null;
        return hadSession;
    }

    private static string GenerateToken() =>
        Convert.ToHexString(RandomNumberGenerator.GetBytes(TokenSizeBytes));
}
