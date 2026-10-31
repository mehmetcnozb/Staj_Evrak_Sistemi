namespace StajEvrakSistemi.Core;

/// <summary>
/// T.C. Kimlik No ve Yabancı Kimlik No doğrulaması. Şartname 6.2: iki numara da 11 haneli ve
/// yalnızca rakamdır; T.C. için ilk hane 0 olamaz, yabancı uyruklular için numara 99 ile başlar.
/// Algoritma (kontrol haneleri) kontrolü şartnamede isteğe bağlıdır; IsValid ile ayrıca yapılabilir.
/// </summary>
public static class IdentityNumberValidator
{
    public const string ForeignPrefix = "99";

    /// <summary>
    /// 11 hane, yalnızca rakam, ilk hane 0 değil.
    /// </summary>
    public static bool IsValidFormat(string? value) =>
        value is { Length: 11 } && value.All(char.IsAsciiDigit) && value[0] != '0';

    /// <summary>
    /// Uyruğa göre biçim kontrolü: yabancı uyruklu için ek olarak 99 ile başlamalıdır.
    /// </summary>
    public static bool IsValidForNationality(string? value, bool isForeign) =>
        IsValidFormat(value) && (!isForeign || value!.StartsWith(ForeignPrefix, StringComparison.Ordinal));

    /// <summary>
    /// Biçim kontrolüne ek olarak 10. ve 11. kontrol hanelerini de doğrular (isteğe bağlı sıkı kontrol).
    /// </summary>
    public static bool IsValid(string? value)
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
