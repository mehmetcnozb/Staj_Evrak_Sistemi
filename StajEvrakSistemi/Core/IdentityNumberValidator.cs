namespace StajEvrakSistemi.Core;

/// <summary>
/// T.C. Kimlik No ve Yabancı Kimlik No (99 ile başlar) doğrulaması.
/// İkisi de 11 hanelidir, ilk hane 0 olamaz, 10. ve 11. haneler kontrol hanesidir.
/// </summary>
public static class IdentityNumberValidator
{
    public static bool IsValid(string? value)
    {
        if (value is null || value.Length != 11 || !value.All(char.IsAsciiDigit) || value[0] == '0')
            return false;

        var d = value.Select(c => c - '0').ToArray();
        int oddSum = d[0] + d[2] + d[4] + d[6] + d[8];
        int evenSum = d[1] + d[3] + d[5] + d[7];

        int tenth = ((oddSum * 7 - evenSum) % 10 + 10) % 10;
        int eleventh = d.Take(10).Sum() % 10;

        return d[9] == tenth && d[10] == eleventh;
    }
}
