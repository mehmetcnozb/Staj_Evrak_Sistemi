namespace StajEvrakSistemi.Models;

public enum FailedLoginReason
{
    UnknownUser,
    WrongPassword,
    LockedOut
}

/// <summary>
/// Başarısız giriş denemesi kaydı (şartname 4.1). Parola hiçbir zaman saklanmaz.
/// </summary>
public sealed record FailedLoginLog(
    int Id,
    int? UserId,
    string Identifier,
    FailedLoginReason Reason,
    DateTime AttemptedAt);
