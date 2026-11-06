namespace StajEvrakSistemi.Core;

/// <summary>
/// T.C. ve yabanci kimlik numaralarinin bicim ve kontrol hanelerini denetler.
/// Bu kontroller numaranin gercek bir kisiye ait oldugunu dogrulamaz.
/// </summary>
public static class IdentityNumberValidator
{
    public const string ForeignPrefix = "99";

    /// <summary>
    /// 11 hane, yalnizca 0-9 rakamlari, ilk hane sifir degil.
    /// </summary>
    public static bool IsValidFormat(string? value) =>
        value is { Length: 11 } && value.All(char.IsAsciiDigit) && value[0] != '0';

    /// <summary>
    /// Mevcut cagirilarda bicim kontrolu korunur. Istenirse T.C. kontrol haneleri de denetlenir.
    /// </summary>
    public static bool IsValidForNationality(string? value, bool isForeign, bool validateChecksum = false)
    {
        if (isForeign)
            return IsValidForeignIdentityNumber(value);

        return validateChecksum ? IsValidTurkishIdentityNumber(value) : IsValidFormat(value);
    }

    /// <summary>
    /// Proje kuralina gore yabanci kimlik numarasi 11 haneli olmali ve 99 ile baslamali.
    /// </summary>
    public static bool IsValidForeignIdentityNumber(string? value) =>
        IsValidFormat(value) && value!.StartsWith(ForeignPrefix, StringComparison.Ordinal);

    // Onceki IsValid cagirilari ayni T.C. kontrolunu kullanmaya devam eder.
    public static bool IsValid(string? value) => IsValidTurkishIdentityNumber(value);

    // T.C. kimlik numarasinin 10. ve 11. hanelerini ilk dokuz haneden hesaplar.
    public static bool IsValidTurkishIdentityNumber(string? value)
    {
        if (!IsValidFormat(value))
            return false;

        var d = value!.Select(c => c - '0').ToArray();
        int oddSum = d[0] + d[2] + d[4] + d[6] + d[8];
        int evenSum = d[1] + d[3] + d[5] + d[7];

        int tenth = ((oddSum * 7 - evenSum) % 10 + 10) % 10;
        int eleventh = d.Take(10).Sum() % 10;

        return d[9] == tenth && d[10] == eleventh;
    }
}
