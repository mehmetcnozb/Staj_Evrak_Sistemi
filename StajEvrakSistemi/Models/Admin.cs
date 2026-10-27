using StajEvrakSistemi.Core;

namespace StajEvrakSistemi.Models;

/// <summary>
/// Sistem yöneticisi. Kullanıcıları yönetir, tüm stajları görür ve belgeleri inceler.
/// </summary>
public class Admin : User
{
    private static readonly HashSet<Permission> Permissions = new()
    {
        Permission.ViewAllInternships,
        Permission.ReviewDocument,
        Permission.ManageUsers
    };

    public Admin(string identityNumber, string fullName, string email, string? phone = null)
        : base(identityNumber, fullName, email, phone)
    {
    }

    public override UserRole Role => UserRole.Admin;

    public override bool HasPermission(Permission permission) => Permissions.Contains(permission);

    public override string GetDisplayName() => $"{FullName} - Yönetici";
}
