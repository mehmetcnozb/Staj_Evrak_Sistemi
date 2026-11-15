using StajEvrakSistemi.Models;

namespace StajEvrakSistemi.Services;

public enum LoginStatus
{
    Success,
    InvalidCredentials,
    LockedOut
}

/// <summary>
/// Giris denemesinin sonucu. Basariliysa User, hataliysa kalan deneme sayisi bulunur.
/// </summary>
public record LoginResult(
    LoginStatus Status,
    User? User = null,
    int RemainingAttempts = 0)
{
    public bool IsSuccess => Status == LoginStatus.Success;
}
