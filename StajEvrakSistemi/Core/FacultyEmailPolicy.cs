namespace StajEvrakSistemi.Core;

/// <summary>
/// Yönetici hesabı yalnızca öğretim görevlisi e-postasıyla açılır.
/// Alan adı appsettings içindeki AdminEmailDomain ile değiştirilebilir.
/// </summary>
public static class FacultyEmailPolicy
{
    public const string DefaultDomain = "kocaeli.edu.tr";

    private static string _domain = DefaultDomain;

    public static string Domain
    {
        get => _domain;
        set => _domain = string.IsNullOrWhiteSpace(value) ? DefaultDomain : value.Trim().TrimStart('@').ToLowerInvariant();
    }

    /// <summary>
    /// Adres kurum alan adında olmalı. Yalnızca rakamdan oluşan adresler öğrenci numarasıdır, kabul edilmez.
    /// </summary>
    public static bool IsFacultyEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email) || !FormatValidator.IsValidEmail(email.Trim()))
            return false;

        string value = email.Trim().ToLowerInvariant();
        int at = value.LastIndexOf('@');
        string local = value[..at];
        return value[(at + 1)..] == Domain && !local.All(char.IsDigit);
    }

    public static string Requirement =>
        $"Yönetici hesabı için @{Domain} uzantılı öğretim görevlisi e-postası gerekir. Öğrenci numarasıyla başlayan adresler kabul edilmez.";
}
