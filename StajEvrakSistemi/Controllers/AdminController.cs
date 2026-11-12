using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StajEvrakSistemi.Core;
using StajEvrakSistemi.Services;
using StajEvrakSistemi.Web;

namespace StajEvrakSistemi.Controllers;

[Authorize(Roles = RolePermissions.RoleAdmin)]
public sealed class AdminController(
    InternshipService internships,
    DatasetImportService datasets,
    ILogger<AdminController> logger) : Controller
{
    [HttpGet]
    public IActionResult Index(bool completed = false) => Dashboard(completed);

    [HttpPost]
    [RequestSizeLimit(DatasetImportService.MaxFileSizeBytes + 64 * 1024)]
    public async Task<IActionResult> ImportDataset(IFormFile? dataset, CancellationToken cancellationToken)
    {
        if (dataset is null || dataset.Length == 0)
            return Dashboard(error: "Yüklemek için bir veri seti dosyası seçin.");

        if (dataset.Length > DatasetImportService.MaxFileSizeBytes)
            return Dashboard(error: "Veri seti dosyası en fazla 10 MB olabilir.");

        string extension = Path.GetExtension(dataset.FileName).ToLowerInvariant();
        if (extension is not (".csv" or ".json"))
            return Dashboard(error: "Yalnızca .csv ve .json veri setleri yüklenebilir.");

        DirectoryInfo? temporaryDirectory = null;
        string? temporaryFile = null;
        try
        {
            temporaryDirectory = Directory.CreateTempSubdirectory("staj-dataset-");
            temporaryFile = Path.Combine(temporaryDirectory.FullName, "dataset" + extension);
            await using (var destination = System.IO.File.Create(temporaryFile))
            {
                await dataset.CopyToAsync(destination, cancellationToken);
            }

            var result = datasets.ImportDatasetToDatabase(temporaryFile, HttpContext.GetCurrentUser());
            return Dashboard(result: result);
        }
        catch (InvalidDataException exception)
        {
            return Dashboard(error: exception.Message);
        }
        catch (JsonException)
        {
            return Dashboard(error: "JSON okunamadı. Dosyanın geçerli bir JSON dizisi içerdiğini kontrol edin.");
        }
        catch (IOException exception)
        {
            logger.LogWarning(exception, "Veri seti için dosya işlemi başarısız oldu.");
            return Dashboard(error: "Veri seti dosyası okunamadı. Dosyayı yeniden yükleyin.");
        }
        finally
        {
            try
            {
                if (temporaryFile is not null)
                    System.IO.File.Delete(temporaryFile);
                temporaryDirectory?.Delete();
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                logger.LogWarning(exception, "Veri seti için geçici dosya temizlenemedi.");
            }
        }
    }

    private ViewResult Dashboard(bool completed = false, DatasetImportResult? result = null, string? error = null)
    {
        var admin = HttpContext.GetCurrentUser();
        var applications = completed ? internships.GetCompletedApplications(admin) : internships.GetApplications(admin);
        return View("Index", new AdminDashboard(applications, completed, result, error));
    }
}
