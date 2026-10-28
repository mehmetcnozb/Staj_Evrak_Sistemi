using StajEvrakSistemi.Core;

namespace StajEvrakSistemi.Models;

/// <summary>
/// Sistem yöneticisi. Kullanıcıları yönetir, tüm stajları görür ve belgeleri inceler.
/// </summary>
public class Admin : User
{
    public Admin(string identityNumber, string fullName, string email, string? phone = null)
        : base(identityNumber, fullName, email, phone)
    {
    }

    public override UserRole Role => UserRole.Admin;

    public override bool HasPermission(Permission permission) => RolePermissions.Has(Role, permission);

    public override string GetDisplayName() => $"{FullName} - Yönetici";
}
