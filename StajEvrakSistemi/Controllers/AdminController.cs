using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StajEvrakSistemi.Core;
using StajEvrakSistemi.Models;
using StajEvrakSistemi.Services;
using StajEvrakSistemi.Web;

namespace StajEvrakSistemi.Controllers;

[Authorize(Roles = RolePermissions.RoleAdmin)]
[ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
public sealed class AdminController(
    InternshipService internships,
    DatasetImportService datasets,
    ILogger<AdminController> logger) : Controller
{
    private const int SearchMaxLength = 120;
    private static readonly CompareInfo StudentNameComparison = CultureInfo.InvariantCulture.CompareInfo;

    [HttpGet]
    public IActionResult Index(bool completed = false, ApplicationStatus? status = null, string? q = null)
    {
        q = q?.Trim();
        if (!ModelState.IsValid || q?.Length > SearchMaxLength || (status.HasValue && !Enum.IsDefined(status.Value)))
            return BadRequest();

        return Dashboard(completed, completed ? null : status, string.IsNullOrEmpty(q) ? null : q);
    }

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

    private ViewResult Dashboard(bool completed = false, ApplicationStatus? status = null, string? search = null,
        DatasetImportResult? result = null, string? error = null)
    {
        var admin = HttpContext.GetCurrentUser();
        var applications = completed
            ? internships.GetCompletedApplications(admin)
            : internships.GetApplications(admin, status);
        IReadOnlyList<InternshipApplication> underReview = completed ? [] : status == ApplicationStatus.UnderReview
            ? applications
            : internships.GetApplications(admin, ApplicationStatus.UnderReview);
        var students = internships.GetApplicationStudents(admin);
        // Türkçe adlar, Türkçe karakter girmeden de aranabilsin.
        string? normalizedSearch = search?.Replace('ı', 'i');

        var applicationRows = applications
            .Select(application => new AdminApplicationRow(application, students.GetValueOrDefault(application.StudentId)))
            .Where(row => normalizedSearch is null || MatchesSearch(row, normalizedSearch))
            .OrderByDescending(row => row.Application.Id)
            .ToList();
        var reviewQueue = underReview
            .Select(application => new AdminApplicationRow(application, students.GetValueOrDefault(application.StudentId)))
            .ToList();
        return View("Index", new AdminDashboard(applicationRows, reviewQueue, completed, status, search, result, error));
    }

    private static bool MatchesSearch(AdminApplicationRow row, string search) =>
        (row.Student is not null && StudentNameComparison.IndexOf(row.Student.FullName.Replace('ı', 'i'), search,
            CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace) >= 0) ||
        row.StudentNumber?.Contains(search, StringComparison.OrdinalIgnoreCase) == true;
}
