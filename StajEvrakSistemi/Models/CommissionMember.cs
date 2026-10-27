using StajEvrakSistemi.Core;

namespace StajEvrakSistemi.Models;

/// <summary>
/// Staj komisyonu üyesi. Belgeleri inceler ve stajı onaylar.
/// </summary>
public class CommissionMember : User
{
    private static readonly HashSet<Permission> Permissions = new()
    {
        Permission.ViewAllInternships,
        Permission.ReviewDocument,
        Permission.ApproveInternship
    };

    public CommissionMember(string identityNumber, string fullName, string email, string? phone = null)
        : base(identityNumber, fullName, email, phone)
    {
    }

    public override UserRole Role => UserRole.Commission;

    public override bool HasPermission(Permission permission) => Permissions.Contains(permission);

    public override string GetDisplayName() => $"{FullName} - Komisyon Üyesi";
}
