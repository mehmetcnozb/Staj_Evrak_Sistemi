using StajEvrakSistemi.Core;

namespace StajEvrakSistemi.Models;

/// <summary>
/// Staj yapan öğrenci. Yalnızca kendi stajlarını görür ve belge yükler.
/// </summary>
public class Student : User
{
    private static readonly HashSet<Permission> Permissions = new()
    {
        Permission.UploadDocument,
        Permission.ViewOwnInternships
    };

    private string _studentNumber = string.Empty;

    public Student(string identityNumber, string studentNumber, string fullName, string email, string? phone = null)
        : base(identityNumber, fullName, email, phone)
    {
        StudentNumber = studentNumber;
    }

    public string StudentNumber
    {
        get => _studentNumber;
        set => _studentNumber = Require(value, nameof(StudentNumber));
    }

    public override UserRole Role => UserRole.Student;

    public override bool HasPermission(Permission permission) => Permissions.Contains(permission);

    public override bool Validate() =>
        base.Validate() && StudentNumber.All(char.IsAsciiLetterOrDigit);

    public override string GetDisplayName() => $"{FullName} ({StudentNumber}) - Öğrenci";
}
