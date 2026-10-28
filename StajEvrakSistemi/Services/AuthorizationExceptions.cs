namespace StajEvrakSistemi.Services;

/// <summary>
/// 401: aktif oturum yok, kullanıcı giriş ekranına yönlendirilmeli.
/// </summary>
public class LoginRequiredException : UnauthorizedAccessException
{
    public const int StatusCode = 401;

    public LoginRequiredException(string message) : base(message)
    {
    }
}

/// <summary>
/// 403: kullanıcı giriş yapmış ama rolü ya da kaynak sahipliği bu işleme yetmiyor.
/// </summary>
public class ForbiddenAccessException : UnauthorizedAccessException
{
    public const int StatusCode = 403;

    public ForbiddenAccessException(string message) : base(message)
    {
    }
}
