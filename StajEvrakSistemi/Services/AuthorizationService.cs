using StajEvrakSistemi.Core;

namespace StajEvrakSistemi.Services;

/// <summary>
/// Rol bazlı yetkilendirme katmanı. Her kontrol önce aktif oturumu (401),
/// sonra rol iznini ve gerekiyorsa kaynak sahipliğini (403) denetler.
/// Authorize* metotları sonuç döndürür, Ensure* metotları izin yoksa istisna fırlatır.
/// </summary>
public class AuthorizationService
{
    private readonly SessionManager _sessions;

    public AuthorizationService(SessionManager sessions)
    {
        _sessions = sessions ?? throw new ArgumentNullException(nameof(sessions));
    }

    /// <summary>
    /// Aktif oturum var mı? Yoksa giriş ekranına yönlendirme sonucu döner.
    /// </summary>
    public AuthorizationResult RequireLogin() =>
        _sessions.CurrentSession is null ? AuthorizationResult.LoginRequired : AuthorizationResult.Allowed;

    /// <summary>
    /// Giriş yapmış kullanıcının rolü bu izne sahip mi?
    /// </summary>
    public bool HasPermission(Permission permission) =>
        _sessions.CurrentSession is { } session && RolePermissions.Has(session.Role, permission);

    public AuthorizationResult Authorize(Permission permission)
    {
        var session = _sessions.CurrentSession;
        if (session is null)
            return AuthorizationResult.LoginRequired;

        return RolePermissions.Has(session.Role, permission)
            ? AuthorizationResult.Allowed
            : AuthorizationResult.Forbidden(
                $"{RolePermissions.ToRoleName(session.Role)} rolü '{permission}' işlemini yapamaz.");
    }

    /// <summary>
    /// Ekran bazlı kontrol, ör. yönetici paneli için AuthorizeRole(UserRole.Admin).
    /// </summary>
    public AuthorizationResult AuthorizeRole(params UserRole[] allowedRoles)
    {
        var session = _sessions.CurrentSession;
        if (session is null)
            return AuthorizationResult.LoginRequired;

        if (allowedRoles.Contains(session.Role))
            return AuthorizationResult.Allowed;

        string required = string.Join(", ", allowedRoles.Select(RolePermissions.ToRoleName));
        return AuthorizationResult.Forbidden(
            $"Bu ekran yalnızca {required} için açık; rolünüz {RolePermissions.ToRoleName(session.Role)}.");
    }

    /// <summary>
    /// Staj başvurusu ve evraklarını görüntüleme. Öğrenci yalnızca kendi kaydını,
    /// yönetici ve komisyon tüm kayıtları görebilir.
    /// </summary>
    public AuthorizationResult AuthorizeInternshipView(int ownerStudentId)
    {
        var session = _sessions.CurrentSession;
        if (session is null)
            return AuthorizationResult.LoginRequired;

        if (RolePermissions.Has(session.Role, Permission.ViewAllInternships))
            return AuthorizationResult.Allowed;

        if (RolePermissions.Has(session.Role, Permission.ViewOwnInternships))
            return IsOwner(session, ownerStudentId)
                ? AuthorizationResult.Allowed
                : AuthorizationResult.Forbidden("Yalnızca kendi staj kayıtlarınızı görüntüleyebilirsiniz.");

        return AuthorizationResult.Forbidden("Staj kayıtlarını görüntüleme yetkiniz yok.");
    }

    /// <summary>
    /// Evrak yükleme. Yalnızca yükleme izni olan rol (öğrenci) ve yalnızca kendi stajına.
    /// </summary>
    public AuthorizationResult AuthorizeDocumentUpload(int ownerStudentId)
    {
        var result = Authorize(Permission.UploadDocument);
        if (!result.IsAllowed)
            return result;

        return IsOwner(_sessions.CurrentSession!, ownerStudentId)
            ? AuthorizationResult.Allowed
            : AuthorizationResult.Forbidden("Yalnızca kendi staj başvurunuza evrak yükleyebilirsiniz.");
    }

    public void EnsureLoggedIn() => RequireLogin().ThrowIfDenied();

    public void EnsureAuthorized(Permission permission) => Authorize(permission).ThrowIfDenied();

    public void EnsureRole(params UserRole[] allowedRoles) => AuthorizeRole(allowedRoles).ThrowIfDenied();

    public void EnsureCanViewInternship(int ownerStudentId) =>
        AuthorizeInternshipView(ownerStudentId).ThrowIfDenied();

    public void EnsureCanUploadDocument(int ownerStudentId) =>
        AuthorizeDocumentUpload(ownerStudentId).ThrowIfDenied();

    private static bool IsOwner(UserSession session, int ownerStudentId) =>
        ownerStudentId > 0 && session.UserId == ownerStudentId;
}
