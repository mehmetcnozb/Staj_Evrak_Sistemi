using System.Text.RegularExpressions;
using StajEvrakSistemi.Models;

namespace StajEvrakSistemi.Core;

public static class FormatValidator
{
    private const RegexOptions Options = RegexOptions.CultureInvariant | RegexOptions.NonBacktracking;

    // Ogrenci numarasinda mevcut harf ve rakam kurali korunur.
    private static readonly Regex StudentNumberPattern = new(@"\A[A-Za-z0-9]+\z", Options);
    private static readonly Regex PhonePattern = new(@"\A\+90 [0-9]{3} [0-9]{3} [0-9]{2} [0-9]{2}\z", Options);
    private static readonly Regex EmailPattern = new(
        @"\A[A-Za-z0-9!#$%&'*+/=?^_`{|}~-]+(?:\.[A-Za-z0-9!#$%&'*+/=?^_`{|}~-]+)*@(?:[A-Za-z0-9](?:[A-Za-z0-9-]*[A-Za-z0-9])?\.)+[A-Za-z]{2,}\z", Options);
    private static readonly Regex AddressPattern = new(
        @"\A(?<neighborhood>.+?)\s+Mah(?:allesi|alle)?\.?\s+(?<street>.+?)\s+(?:Cad(?:desi)?\.?|Sok(?:ak|a[g\u011f][i\u0131])?\.?|Sk\.?)\s+(?:Bina\s+)?No:?\s*[0-9]+[A-Za-z]?(?:/[A-Za-z0-9]+)?\s+(?:Daire(?:\s+No)?|D):?\s*[0-9]+[A-Za-z]?\s+(?<district>[\p{L}0-9 .'-]+)\s*/\s*(?<city>[\p{L}0-9 .'-]+)\z",
        Options | RegexOptions.IgnoreCase);

    public static bool IsValidStudentNumber(string? value, int? requiredLength = null)
    {
        if (requiredLength.HasValue && requiredLength.Value <= 0)
            throw new ArgumentOutOfRangeException(nameof(requiredLength));

        string number = value?.Trim() ?? string.Empty;
        return StudentNumberPattern.IsMatch(number)
            && (!requiredLength.HasValue || number.Length == requiredLength.Value);
    }

    public static bool IsValidEmail(string? value) =>
        !string.IsNullOrWhiteSpace(value) && EmailPattern.IsMatch(value.Trim());

    public static bool IsValidTurkishPhone(string? value) =>
        !string.IsNullOrWhiteSpace(value) && PhonePattern.IsMatch(value.Trim());

    public static bool IsValidOptionalFax(string? value) =>
        string.IsNullOrWhiteSpace(value) || IsValidTurkishPhone(value);

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
        if (!IsValidOptionalFax(application.Fax))
            errors.Add("Faks bos birakilabilir. Girilmisse +90 xxx xxx xx xx biciminde olmali.");

        return errors;
    }
}
