using System.Net.Mail;
using System.Text.RegularExpressions;
using StajEvrakSistemi.Core;

namespace StajEvrakSistemi.Models;

/// <summary>
/// Admin'in yüklediği test/resmi veri setindeki tek bir öğrenci kaydı (şartname 4.3 sütunları).
/// Öğrencinin yüklediği Ek-16 belgesindeki bilgiler bu kayıtla karşılaştırılır.
/// </summary>
public class OfficialStudentRecord
{
    private static readonly Regex AcademicYearPattern = new(@"^(\d{4})-(\d{4})$", RegexOptions.Compiled);

    private static readonly HashSet<string> TurkishNationalityNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "t.c.", "t.c", "tc", "tr", "türkiye", "turkiye", "türk", "turk", "t.c. vatandaşı", "türkiye cumhuriyeti"
    };

    public OfficialStudentRecord(
        string studentNumber,
        string identityNumber,
        string fullName,
        string nationality,
        string department,
        string academicYear,
        DateTime? birthDate = null,
        string? address = null,
        int? previousInternshipDays = null,
        string? email = null,
        string? phone = null,
        string? healthAidStatus = null)
    {
        StudentNumber = Require(studentNumber, nameof(StudentNumber));
        if (!StudentNumber.All(char.IsAsciiLetterOrDigit))
            throw new ArgumentException($"Öğrenci numarası geçersiz: {studentNumber}", nameof(studentNumber));

        Nationality = Require(nationality, nameof(Nationality));

        IdentityNumber = Require(identityNumber, nameof(IdentityNumber));
        if (!IdentityNumberValidator.IsValidForNationality(IdentityNumber, IsForeign))
            throw new ArgumentException(
                IsForeign
                    ? $"Yabancı kimlik numarası 11 haneli olmalı ve 99 ile başlamalı: {identityNumber}"
                    : $"T.C. kimlik numarası 11 haneli olmalı ve 0 ile başlamamalı: {identityNumber}",
                nameof(identityNumber));

        FullName = string.Join(' ', Require(fullName, nameof(FullName)).Split(' ', StringSplitOptions.RemoveEmptyEntries));
        Department = Require(department, nameof(Department));

        AcademicYear = Require(academicYear, nameof(AcademicYear));
        if (!IsValidAcademicYear(AcademicYear))
            throw new ArgumentException($"Öğretim yılı 2025-2026 biçiminde olmalı: {academicYear}", nameof(academicYear));

        if (birthDate is { } birth && (birth > DateTime.Today || birth.Year < 1900))
            throw new ArgumentException($"Doğum tarihi geçersiz: {birth:dd.MM.yyyy}", nameof(birthDate));
        BirthDate = birthDate?.Date;

        if (previousInternshipDays is < 0)
            throw new ArgumentException("Önceki staj gün sayısı negatif olamaz.", nameof(previousInternshipDays));
        PreviousInternshipDays = previousInternshipDays;

        Address = Optional(address);

        Email = Optional(email)?.ToLowerInvariant();
        if (Email is not null && !MailAddress.TryCreate(Email, out _))
            throw new ArgumentException($"E-posta geçersiz: {email}", nameof(email));

        Phone = Optional(phone);
        if (Phone is not null && Phone.Count(char.IsAsciiDigit) is < 10 or > 12)
            throw new ArgumentException($"Telefon numarası geçersiz: {phone}", nameof(phone));

        HealthAidStatus = Optional(healthAidStatus);
    }

    public int Id { get; internal set; }

    public string StudentNumber { get; }

    public string IdentityNumber { get; }

    public string FullName { get; }

    /// <summary>
    /// Uyruk (ör. "T.C."). T.C. dışındaki her uyruk yabancı sayılır.
    /// </summary>
    public string Nationality { get; }

    public bool IsForeign => !TurkishNationalityNames.Contains(Nationality);

    public string Department { get; }

    public string AcademicYear { get; }

    public DateTime? BirthDate { get; }

    /// <summary>
    /// İkametgâh adresi (açık adres).
    /// </summary>
    public string? Address { get; }

    public int? PreviousInternshipDays { get; }

    public string? Email { get; }

    public string? Phone { get; }

    public string? HealthAidStatus { get; }

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

    private static string? Optional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
