namespace StajEvrakSistemi.Web;

public static class GoogleFormUrlValidator
{
    public static bool IsValid(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return true;
        if (!Uri.TryCreate(value.Trim(), UriKind.Absolute, out var url) || url.Scheme != Uri.UriSchemeHttps ||
            !url.IsDefaultPort || url.UserInfo.Length != 0)
            return false;

        string[] parts = url.AbsolutePath.Split('/');
        if (url.Host == "forms.gle")
            return (parts.Length == 2 || parts.Length == 3 && parts[2].Length == 0) && IsFormId(parts[1]);

        if (url.Host != "docs.google.com" || parts.Length < 5 || parts[1] != "forms" || parts[2] != "d")
            return false;
        int idIndex = parts[3] == "e" ? 4 : 3;
        return parts.Length == idIndex + 2 && IsFormId(parts[idIndex]) && parts[idIndex + 1] == "viewform";
    }

    private static bool IsFormId(string value) =>
        value.Length > 0 && value.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_');
}
