using StajEvrakSistemi.Core;

namespace StajEvrakSistemi.Services;

/// <summary>
/// Rol bazlı yetkilendirme katmanı. Her kontrol, isteği yapan kullanıcının oturum token'ını alır ve önce
/// oturumu (401), sonra rol iznini ve gerekiyorsa kaynak sahipliğini (403) denetler.
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
    public AuthorizationResult RequireLogin(string? sessionToken) =>
        _sessions.GetSession(sessionToken) is null ? AuthorizationResult.LoginRequired : AuthorizationResult.Allowed;

    /// <summary>
    /// Giriş yapmış kullanıcının rolü bu izne sahip mi?
    /// </summary>
    public bool HasPermission(string? sessionToken, Permission permission) =>
        _sessions.GetSession(sessionToken) is { } session && RolePermissions.Has(session.Role, permission);

    public AuthorizationResult Authorize(string? sessionToken, Permission permission)
    {
        var session = _sessions.GetSession(sessionToken);
        if (session is null)
            return AuthorizationResult.LoginRequired;

        return RolePermissions.Has(session.Role, permission)
            ? AuthorizationResult.Allowed
            : AuthorizationResult.Forbidden(
                $"{RolePermissions.ToRoleName(session.Role)} rolü '{permission}' işlemini yapamaz.");
    }

    /// <summary>
    /// Ekran bazlı kontrol, ör. admin paneli için AuthorizeRole(token, UserRole.Admin).
    /// Yetkisiz istek 403 döner (şartname 4.2); arayüz bunu login sayfasına yönlendirmeyle karşılar.
    /// </summary>
    public AuthorizationResult AuthorizeRole(string? sessionToken, params UserRole[] allowedRoles)
    {
        var session = _sessions.GetSession(sessionToken);
        if (session is null)
            return AuthorizationResult.LoginRequired;

        if (allowedRoles.Contains(session.Role))
            return AuthorizationResult.Allowed;

        string required = string.Join(", ", allowedRoles.Select(RolePermissions.ToRoleName));
        return AuthorizationResult.Forbidden(
            $"Bu ekran yalnızca {required} için açık; rolünüz {RolePermissions.ToRoleName(session.Role)}.");
    }

    /// <summary>
    /// Staj başvurusu ve evraklarını görüntüleme. Öğrenci yalnızca kendi kaydını, yönetici tüm kayıtları görebilir.
    /// </summary>
    public AuthorizationResult AuthorizeInternshipView(string? sessionToken, int ownerStudentId)
    {
        var session = _sessions.GetSession(sessionToken);
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
    public AuthorizationResult AuthorizeDocumentUpload(string? sessionToken, int ownerStudentId)
    {
        var result = Authorize(sessionToken, Permission.UploadDocument);
        if (!result.IsAllowed)
            return result;

        return IsOwner(_sessions.GetSession(sessionToken)!, ownerStudentId)
            ? AuthorizationResult.Allowed
            : AuthorizationResult.Forbidden("Yalnızca kendi staj başvurunuza evrak yükleyebilirsiniz.");
    }

    public void EnsureLoggedIn(string? sessionToken) => RequireLogin(sessionToken).ThrowIfDenied();

    public void EnsureAuthorized(string? sessionToken, Permission permission) =>
        Authorize(sessionToken, permission).ThrowIfDenied();

    public void EnsureRole(string? sessionToken, params UserRole[] allowedRoles) =>
        AuthorizeRole(sessionToken, allowedRoles).ThrowIfDenied();

    public void EnsureCanViewInternship(string? sessionToken, int ownerStudentId) =>
        AuthorizeInternshipView(sessionToken, ownerStudentId).ThrowIfDenied();

    public void EnsureCanUploadDocument(string? sessionToken, int ownerStudentId) =>
        AuthorizeDocumentUpload(sessionToken, ownerStudentId).ThrowIfDenied();

    private static bool IsOwner(UserSession session, int ownerStudentId) =>
        ownerStudentId > 0 && session.UserId == ownerStudentId;
}
