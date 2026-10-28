namespace StajEvrakSistemi.Services;

public enum AuthorizationStatus
{
    Allowed = 200,
    LoginRequired = 401,
    Forbidden = 403
}

/// <summary>
/// Yetki kontrolünün sonucu. Arayüz bu sonuca göre işlemi yapar,
/// giriş ekranına yönlendirir ya da 403 uyarısı gösterir.
/// </summary>
public sealed record AuthorizationResult(AuthorizationStatus Status, string Message)
{
    public static readonly AuthorizationResult Allowed =
        new(AuthorizationStatus.Allowed, "İzin verildi.");

    public static readonly AuthorizationResult LoginRequired =
        new(AuthorizationStatus.LoginRequired, "Bu işlem için giriş yapmalısınız. Lütfen giriş ekranına gidin.");

    public static AuthorizationResult Forbidden(string message) =>
        new(AuthorizationStatus.Forbidden, $"403 Yetkisiz Erişim: {message}");

    public bool IsAllowed => Status == AuthorizationStatus.Allowed;

    public int StatusCode => (int)Status;

    /// <summary>
    /// İzin yoksa durumuna uygun istisnayı fırlatır.
    /// </summary>
    public void ThrowIfDenied()
    {
        switch (Status)
        {
            case AuthorizationStatus.LoginRequired:
                throw new LoginRequiredException(Message);
            case AuthorizationStatus.Forbidden:
                throw new ForbiddenAccessException(Message);
        }
    }
}
