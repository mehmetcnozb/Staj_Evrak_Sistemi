using StajEvrakSistemi.Services;

namespace StajEvrakSistemi.Web;

internal static class Ek16Upload
{
    public const string AcceptedExtensions = ".doc,.docx,.pdf,.jpg,.jpeg,.png";

    public static async Task Store(IFormFile? file, Action<string> save, ILogger logger, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
            throw new DocumentValidationException(["Yüklenecek Ek-16 dosyasını seçin."]);

        string extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!AcceptedExtensions.Split(',').Contains(extension, StringComparer.Ordinal))
            throw new DocumentValidationException(["Yalnızca Word (.doc/.docx), PDF, JPEG ve PNG dosyaları kabul edilir."]);

        string path = Path.Combine(Path.GetTempPath(), $"staj-ek16-{Guid.NewGuid():N}{extension}");
        try
        {
            await using (var temporary = new FileStream(path, FileMode.CreateNew, FileAccess.Write,
                FileShare.None, bufferSize: 81920, useAsync: true))
                await file.CopyToAsync(temporary, cancellationToken);

            save(path);
        }
        finally
        {
            try { File.Delete(path); }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                logger.LogWarning(error, "Geçici Ek-16 dosyası silinemedi.");
            }
        }
    }
}
