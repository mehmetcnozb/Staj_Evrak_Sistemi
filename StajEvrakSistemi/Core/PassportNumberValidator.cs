namespace StajEvrakSistemi.Core;

public static class PassportNumberValidator
{
    // Turkiye umuma mahsus pasaport numarasi: U harfi ve ardindan sekiz rakam.
    // Kaynak ornek: https://smarteritaly.agid.gov.it/wp-content/uploads/2024/04/IQ-Structures_presentazione.pdf
    public static bool IsValidTurkishOrdinaryPassportNumber(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        string number = value.Trim();
        return number.Length == 9
            && (number[0] == 'U' || number[0] == 'u')
            && number.Skip(1).All(char.IsAsciiDigit);
    }
}
