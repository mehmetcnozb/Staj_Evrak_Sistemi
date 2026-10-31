using System.Collections.Concurrent;
using System.Security.Cryptography;
using StajEvrakSistemi.Models;

namespace StajEvrakSistemi.Services;

/// <summary>
/// Giriş yapmış kullanıcıların oturumlarını yönetir: başlatır, token ile doğrular ve sonlandırır.
/// Aynı anda birden fazla kullanıcının oturumu açık olabilir (web sitesi); her istek kendi oturum
/// token'ıyla gelir. Bir kullanıcı yeniden giriş yaparsa önceki oturumu kapanır.
/// Süresi dolan oturum ilk kontrolde otomatik olarak kapatılır.
/// </summary>
public class SessionManager
{
    public static readonly TimeSpan DefaultSessionDuration = TimeSpan.FromMinutes(30);

    private const int TokenSizeBytes = 32;

    private readonly ConcurrentDictionary<string, UserSession> _sessions = new();
    private readonly TimeSpan _sessionDuration;
    private readonly Func<DateTime> _utcNow;

    public SessionManager(TimeSpan? sessionDuration = null, Func<DateTime>? utcNow = null)
    {
        _sessionDuration = sessionDuration ?? DefaultSessionDuration;
        if (_sessionDuration <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(sessionDuration), "Oturum süresi pozitif olmalıdır.");

        _utcNow = utcNow ?? (() => DateTime.UtcNow);
    }

    /// <summary>
    /// Şu an açık (süresi dolmamış) oturum sayısı.
    /// </summary>
    public int ActiveSessionCount
    {
        get
        {
            DateTime now = _utcNow();
            return _sessions.Values.Count(s => !s.IsExpired(now));
        }
    }

    /// <summary>
    /// Başarılı girişten sonra yeni oturum açar. Aynı kullanıcının varsa önceki oturumu kapanır.
    /// </summary>
    public UserSession StartSession(User user)
    {
        ArgumentNullException.ThrowIfNull(user);
        if (user.Id <= 0)
            throw new ArgumentException("Oturum yalnızca kayıtlı bir kullanıcı için açılabilir.", nameof(user));

        foreach (var existing in _sessions.Where(s => s.Value.UserId == user.Id).ToList())
            _sessions.TryRemove(existing.Key, out _);

        DateTime now = _utcNow();
        var session = new UserSession(user.Id, user.Role, GenerateToken(), now, now.Add(_sessionDuration));
        _sessions[session.SessionToken] = session;
        return session;
    }

    /// <summary>
    /// Token'a ait geçerli oturumu döndürür; token yoksa, bilinmiyorsa veya süresi dolmuşsa null.
    /// </summary>
    public UserSession? GetSession(string? sessionToken)
    {
        if (string.IsNullOrEmpty(sessionToken) || !_sessions.TryGetValue(sessionToken, out var session))
            return null;

        if (session.IsExpired(_utcNow()))
        {
            _sessions.TryRemove(sessionToken, out _);
            return null;
        }

        return session;
    }

    public bool IsSessionValid(string? sessionToken) => GetSession(sessionToken) is not null;

    /// <summary>
    /// Verilen token'ın açık ve süresi dolmamış bir oturuma ait olduğunu doğrular.
    /// </summary>
    public bool ValidateSession(string? sessionToken) => IsSessionValid(sessionToken);

    /// <summary>
    /// Token'a ait oturumu kapatır (çıkış). Kapatılacak oturum varsa true döner.
    /// </summary>
    public bool Logout(string? sessionToken) =>
        !string.IsNullOrEmpty(sessionToken) && _sessions.TryRemove(sessionToken, out _);

    private static string GenerateToken() =>
        Convert.ToHexString(RandomNumberGenerator.GetBytes(TokenSizeBytes));
}
