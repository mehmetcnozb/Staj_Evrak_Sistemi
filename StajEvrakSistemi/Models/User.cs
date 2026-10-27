using System.Net.Mail;
using StajEvrakSistemi.Core;

namespace StajEvrakSistemi.Models;

/// <summary>
/// Tüm kullanıcı türlerinin (Öğrenci, Admin, Komisyon) ortak temel sınıfı.
/// Rol ve yetkiler alt sınıflar tarafından belirlenir.
/// </summary>
public abstract class User
{
    private string _identityNumber = string.Empty;
    private string _fullName = string.Empty;
    private string _email = string.Empty;
    private string? _phone;
    private string _passwordHash = string.Empty;

    protected User(string identityNumber, string fullName, string email, string? phone = null)
    {
        IdentityNumber = identityNumber;
        FullName = fullName;
        Email = email;
        Phone = phone;
        CreatedAt = DateTime.UtcNow;
    }

    // Kimlik bilgileri
    public int Id { get; internal set; }

    public string IdentityNumber
    {
        get => _identityNumber;
        set => _identityNumber = Require(value, nameof(IdentityNumber));
    }

    public string FullName
    {
        get => _fullName;
        set => _fullName = Require(value, nameof(FullName));
    }

    // İletişim bilgileri
    public string Email
    {
        get => _email;
        set => _email = Require(value, nameof(Email)).ToLowerInvariant();
    }

    public string? Phone
    {
        get => _phone;
        set => _phone = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    public DateTime CreatedAt { get; internal set; }

    // Hesap kilidi: hatalı giriş sayısı ve kilidin bitiş zamanı (UTC)
    public int FailedLoginAttempts { get; internal set; }

    public DateTime? LockoutEnd { get; internal set; }

    public bool IsLockedOut(DateTime nowUtc) => LockoutEnd.HasValue && LockoutEnd.Value > nowUtc;

    /// <summary>
    /// Hatalı girişi sayar; sınıra ulaşılınca hesabı verilen süre kadar kilitler.
    /// </summary>
    public void RecordFailedLogin(DateTime nowUtc, int maxAttempts, TimeSpan lockoutDuration)
    {
        FailedLoginAttempts++;
        if (FailedLoginAttempts >= maxAttempts)
            LockoutEnd = nowUtc.Add(lockoutDuration);
    }

    public void ResetLoginAttempts()
    {
        FailedLoginAttempts = 0;
        LockoutEnd = null;
    }

    /// <summary>
    /// Parola hash'i dışarıdan okunamaz; yalnızca hash'i hazırlanmış değer atanabilir.
    /// </summary>
    protected string PasswordHash => _passwordHash;

    public abstract UserRole Role { get; }

    public void SetPasswordHash(string passwordHash)
    {
        _passwordHash = Require(passwordHash, nameof(passwordHash));
    }

    public bool VerifyPasswordHash(string passwordHash) =>
        !string.IsNullOrEmpty(_passwordHash) && _passwordHash == passwordHash;

    /// <summary>
    /// Ortak alanları doğrular. Alt sınıflar role özgü kuralları eklemek için ezer
    /// ve <c>base.Validate()</c> çağırır.
    /// </summary>
    public virtual bool Validate()
    {
        return IsValidIdentityNumber(IdentityNumber)
            && FullName.Length >= 3
            && MailAddress.TryCreate(Email, out _)
            && (Phone is null || IsValidPhone(Phone))
            && !string.IsNullOrEmpty(_passwordHash);
    }

    /// <summary>
    /// Kullanıcının verilen işlemi yapmaya yetkisi olup olmadığını döndürür.
    /// </summary>
    public abstract bool HasPermission(Permission permission);

    public virtual string GetDisplayName() => $"{FullName} ({Role})";

    public override string ToString() => GetDisplayName();

    /// <summary>
    /// T.C. Kimlik No: 11 hane, ilk hane 0 olamaz, 10. ve 11. haneler kontrol hanesidir.
    /// </summary>
    protected static bool IsValidIdentityNumber(string value)
    {
        if (value.Length != 11 || !value.All(char.IsAsciiDigit) || value[0] == '0')
            return false;

        var d = value.Select(c => c - '0').ToArray();
        int oddSum = d[0] + d[2] + d[4] + d[6] + d[8];
        int evenSum = d[1] + d[3] + d[5] + d[7];

        int tenth = ((oddSum * 7 - evenSum) % 10 + 10) % 10;
        int eleventh = d.Take(10).Sum() % 10;

        return d[9] == tenth && d[10] == eleventh;
    }

    protected static bool IsValidPhone(string value)
    {
        var digits = new string(value.Where(char.IsAsciiDigit).ToArray());
        return digits.Length is >= 10 and <= 12;
    }

    protected static string Require(string? value, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException($"{name} boş olamaz.", name);
        return value.Trim();
    }
}
