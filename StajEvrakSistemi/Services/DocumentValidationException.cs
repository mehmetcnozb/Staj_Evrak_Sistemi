namespace StajEvrakSistemi.Services;

/// <summary>
/// Yüklenen belge kurallara uymuyor (ör. kabul edilmeyen format). Errors, kullanıcıya uyarı olarak
/// gösterilecek mesajları içerir.
/// </summary>
public class DocumentValidationException : Exception
{
    public DocumentValidationException(IReadOnlyList<string> errors)
        : base("Belge kabul edilmedi: " + string.Join(" ", errors))
    {
        Errors = errors;
    }

    public IReadOnlyList<string> Errors { get; }
}
