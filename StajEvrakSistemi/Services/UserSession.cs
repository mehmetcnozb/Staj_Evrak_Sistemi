using StajEvrakSistemi.Core;

namespace StajEvrakSistemi.Services;

/// <summary>
/// Giriş yapmış kullanıcının oturum bilgileri. Zamanlar UTC'dir.
/// </summary>
public sealed record UserSession(
    int UserId,
    UserRole Role,
    string SessionToken,
    DateTime LoginTime,
    DateTime? ExpiresAt = null)
{
    public bool IsExpired(DateTime nowUtc) => ExpiresAt.HasValue && nowUtc >= ExpiresAt.Value;
}
