using StajEvrakSistemi.Models;

namespace StajEvrakSistemi.Services;

public enum LoginStatus
{
    Success,
    InvalidCredentials,
    LockedOut
}

/// <summary>
/// Giriş denemesinin sonucu. Başarılıysa User dolu, kilitliyse kalan süre dolu gelir.
/// </summary>
public record LoginResult(
    LoginStatus Status,
    User? User = null,
    TimeSpan? RemainingLockout = null,
    int RemainingAttempts = 0)
{
    public bool IsSuccess => Status == LoginStatus.Success;
}
