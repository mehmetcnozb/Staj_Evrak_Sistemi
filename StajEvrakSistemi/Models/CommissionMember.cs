using StajEvrakSistemi.Core;

namespace StajEvrakSistemi.Models;

/// <summary>
/// Staj komisyonu üyesi. Belgeleri inceler ve stajı onaylar.
/// </summary>
public class CommissionMember : User
{
    public CommissionMember(string identityNumber, string fullName, string email, string? phone = null)
        : base(identityNumber, fullName, email, phone)
    {
    }

    public override UserRole Role => UserRole.Commission;

    public override bool HasPermission(Permission permission) => RolePermissions.Has(Role, permission);

    public override string GetDisplayName() => $"{FullName} - Komisyon Üyesi";
}
