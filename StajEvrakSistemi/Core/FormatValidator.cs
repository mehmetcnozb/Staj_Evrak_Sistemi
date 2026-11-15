using System.Text.RegularExpressions;
using StajEvrakSistemi.Models;

namespace StajEvrakSistemi.Core;

public static class FormatValidator
{
    private const RegexOptions Options = RegexOptions.CultureInvariant | RegexOptions.NonBacktracking;

    private static readonly Regex PhonePattern = new(@"\A\+90 [0-9]{3} [0-9]{3} [0-9]{2} [0-9]{2}\z", Options);
    private const string PhoneSeparator = @"[\p{Zs}\t]*(?:-[\p{Zs}\t]*)?";
    private static readonly Regex PhoneInputPattern = new(
        $@"\A(?:(?:\+90|0090|0){PhoneSeparator})?"
        + $@"(?<number>(?:\([1-9][0-9]{{2}}\)|[1-9][0-9]{{2}})(?:{PhoneSeparator}[0-9]){{7}})\z", Options);
    private static readonly Regex EmailPattern = new(
        @"\A[A-Za-z0-9!#$%&'*+/=?^_`{|}~-]+(?:\.[A-Za-z0-9!#$%&'*+/=?^_`{|}~-]+)*@(?:[A-Za-z0-9](?:[A-Za-z0-9-]*[A-Za-z0-9])?\.)+[A-Za-z]{2,}\z", Options);
    private static readonly Regex AddressPattern = new(
        @"\A(?<neighborhood>.+?)\s+Mah(?:allesi|alle)?\.?\s+(?<street>.+?)\s+(?:(?<avenue>Cad(?:desi)?\.?)|Sok(?:ak|a[g\u011f][i\u0131])?\.?|Sk\.?)\s+(?:Bina\s+)?No:?\s*(?<building>[0-9]+[A-Za-z]?(?:/[A-Za-z0-9]+)?)\s+(?:Daire(?:\s+No)?|D):?\s*(?<flat>[0-9]+[A-Za-z]?)\s+(?<district>[\p{L}0-9 .'-]+)\s*/\s*(?<city>[\p{L}0-9 .'-]+)\z",
        Options | RegexOptions.IgnoreCase);

    public static bool IsValidStudentNumber(string? value) => !string.IsNullOrWhiteSpace(value);

    public static bool IsValidEmail(string? value) =>
        !string.IsNullOrWhiteSpace(value) && EmailPattern.IsMatch(value.Trim());

    public static bool IsValidTurkishPhone(string? value) =>
        !string.IsNullOrWhiteSpace(value) && PhonePattern.IsMatch(value.Trim());

    // Öğrenci formuna yapıştırılan eşdeğer numaraları kayıt biçimine dönüştürür.
    public static string? NormalizeTurkishPhone(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        string input = value.Trim();
        if (PhonePattern.IsMatch(input))
            return input;

        var match = PhoneInputPattern.Match(input);
        if (!match.Success)
            return null;

        string digits = new(match.Groups["number"].Value.Where(char.IsAsciiDigit).ToArray());
        return $"+90 {digits[..3]} {digits[3..6]} {digits[6..8]} {digits[8..]}";
    }

    public static bool IsValidAddress(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        Match match = AddressPattern.Match(value.Trim().Replace(',', ' '));
        return match.Success && match.Groups["neighborhood"].Value.Any(char.IsLetterOrDigit)
            && match.Groups["street"].Value.Any(char.IsLetterOrDigit)
            && match.Groups["district"].Value.Any(char.IsLetter)
            && match.Groups["city"].Value.Any(char.IsLetter);
    }

    internal static string NormalizeAddress(string address)
    {
        Match match = AddressPattern.Match(address.Trim().Replace(',', ' '));
        var tr = System.Globalization.CultureInfo.GetCultureInfo("tr-TR");
        string Normalize(string value) =>
            string.Join(' ', value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).ToUpper(tr);

        if (!match.Success)
            return Normalize(address.Replace(',', ' '));

        return string.Join('|', match.Groups["avenue"].Success ? "C" : "S",
            Normalize(match.Groups["neighborhood"].Value), Normalize(match.Groups["street"].Value),
            Normalize(match.Groups["building"].Value), Normalize(match.Groups["flat"].Value),
            Normalize(match.Groups["district"].Value), Normalize(match.Groups["city"].Value));
    }

    public static IReadOnlyList<string> GetApplicationErrors(InternshipApplication application)
    {
        ArgumentNullException.ThrowIfNull(application);
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(application.CompanyName))
            errors.Add("Isyeri adi bos olamaz.");
        if (!IsValidAddress(application.Address))
            errors.Add("Adres mahalle, cadde/sokak, bina no, daire no ve ilce/il sirasiyla tam yazilmali.");
        if (!IsValidTurkishPhone(application.Phone))
            errors.Add("Telefon zorunlu ve +90 xxx xxx xx xx biciminde olmali.");
        if (!IsValidEmail(application.Email))
            errors.Add("Gecerli bir e-posta adresi zorunlu.");

        return errors;
    }
}
