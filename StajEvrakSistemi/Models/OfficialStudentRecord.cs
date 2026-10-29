using System.Text.RegularExpressions;
using StajEvrakSistemi.Core;

namespace StajEvrakSistemi.Models;

/// <summary>
/// Admin'in yüklediği resmi öğrenci/staj veri setindeki tek bir kayıt.
/// Öğrencinin beyan ettiği bilgiler bu kayıtla karşılaştırılarak doğrulanır.
/// </summary>
public class OfficialStudentRecord
{
    private static readonly Regex AcademicYearPattern = new(@"^(\d{4})-(\d{4})$", RegexOptions.Compiled);

    public OfficialStudentRecord(
        string studentNumber,
        string identityNumber,
        string fullName,
        string department,
        string academicYear,
        OfficialInternshipStatus internshipStatus)
    {
        StudentNumber = Require(studentNumber, nameof(StudentNumber));
        if (!StudentNumber.All(char.IsAsciiLetterOrDigit))
            throw new ArgumentException($"Öğrenci numarası geçersiz: {studentNumber}", nameof(studentNumber));

        IdentityNumber = Require(identityNumber, nameof(IdentityNumber));
        if (!IdentityNumberValidator.IsValid(IdentityNumber))
            throw new ArgumentException($"T.C./Yabancı kimlik numarası geçersiz: {identityNumber}", nameof(identityNumber));

        FullName = string.Join(' ', Require(fullName, nameof(FullName)).Split(' ', StringSplitOptions.RemoveEmptyEntries));
        Department = Require(department, nameof(Department));

        AcademicYear = Require(academicYear, nameof(AcademicYear));
        if (!IsValidAcademicYear(AcademicYear))
            throw new ArgumentException($"Öğretim yılı 2025-2026 biçiminde olmalı: {academicYear}", nameof(academicYear));

        InternshipStatus = internshipStatus;
    }

    public int Id { get; internal set; }

    public string StudentNumber { get; }

    public string IdentityNumber { get; }

    public string FullName { get; }

    public string Department { get; }

    public string AcademicYear { get; }

    public OfficialInternshipStatus InternshipStatus { get; }

    public DateTime? ImportedAt { get; internal set; }

    public int? ImportedByUserId { get; internal set; }

    /// <summary>
    /// Mükerrer kontrol anahtarı: bir öğrencinin bir öğretim yılında tek kaydı olur.
    /// </summary>
    public (string StudentNumber, string AcademicYear) Key => (StudentNumber, AcademicYear);

    private static bool IsValidAcademicYear(string value)
    {
        var match = AcademicYearPattern.Match(value);
        return match.Success && int.Parse(match.Groups[2].Value) == int.Parse(match.Groups[1].Value) + 1;
    }

    private static string Require(string? value, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException($"{name} boş olamaz.", name);
        return value.Trim();
    }
}
