namespace StajEvrakSistemi.Core;

/// <summary>
/// Hangi rolün hangi işlemleri yapabileceğini tanımlayan tek merkezi tablo.
/// Hem User alt sınıfları hem de AuthorizationService bu tabloyu kullanır.
/// </summary>
public static class RolePermissions
{
    public const string RoleStudent = "ROLE_STUDENT";
    public const string RoleAdmin = "ROLE_ADMIN";

    private static readonly IReadOnlyDictionary<UserRole, IReadOnlySet<Permission>> Map =
        new Dictionary<UserRole, IReadOnlySet<Permission>>
        {
            [UserRole.Student] = new HashSet<Permission>
            {
                Permission.UploadDocument,
                Permission.ViewOwnInternships
            },
            [UserRole.Admin] = new HashSet<Permission>
            {
                Permission.ViewAllInternships,
                Permission.ReviewDocument,
                Permission.ApproveInternship,
                Permission.ManageUsers,
                Permission.ImportDataset
            }
        };

    public static bool Has(UserRole role, Permission permission) =>
        Map.TryGetValue(role, out var permissions) && permissions.Contains(permission);

    public static string ToRoleName(UserRole role) => role switch
    {
        UserRole.Student => RoleStudent,
        UserRole.Admin => RoleAdmin,
        _ => throw new ArgumentOutOfRangeException(nameof(role), role, null)
    };
}
