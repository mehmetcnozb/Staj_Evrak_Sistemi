using Microsoft.AspNetCore.Mvc;
using StajEvrakSistemi.Models;
using StajEvrakSistemi.Services;
using StajEvrakSistemi.Web;

namespace StajEvrakSistemi.Controllers;

public sealed partial class StudentController
{
    [HttpPost]
    [RequestSizeLimit(Ek16Document.MaxFileSizeBytes + 64 * 1024)]
    public async Task<IActionResult> Upload([FromRoute] int id, IFormFile? file, CancellationToken cancellationToken)
    {
        var student = HttpContext.GetCurrentUser();
        InternshipApplication application;
        try
        {
            application = _internships.GetApplication(student, id);
        }
        catch (ForbiddenAccessException)
        {
            return Forbid();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }

        if (!_workflow.CanModifyOrDelete(application.Status, student))
        {
            TempData["StudentNotice"] = "Başvurunun mevcut durumunda yeni belge yüklenemez.";
            return RedirectToAction(nameof(Details), new { id });
        }
        if (file is null || file.Length == 0)
        {
            ModelState.AddModelError(nameof(file), "Yüklenecek Ek-16 dosyasını seçin.");
            return ApplicationDetails(student, application);
        }
        if (file.Length > Ek16Document.MaxFileSizeBytes)
        {
            ModelState.AddModelError(nameof(file), $"Dosya boyutu en fazla {Ek16Document.MaxFileSizeBytes / (1024 * 1024)} MB olabilir.");
            return ApplicationDetails(student, application);
        }

        string extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (extension is not (".doc" or ".docx" or ".pdf" or ".jpg" or ".jpeg" or ".png"))
        {
            ModelState.AddModelError(nameof(file), "Yalnızca Word (.doc/.docx), PDF, JPEG ve PNG dosyaları kabul edilir.");
            return ApplicationDetails(student, application);
        }

        string temporaryPath = Path.Combine(Path.GetTempPath(), $"staj-ek16-{Guid.NewGuid():N}{extension}");
        try
        {
            await using (var temporaryFile = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write,
                FileShare.None, bufferSize: 81920, useAsync: true))
            {
                await file.CopyToAsync(temporaryFile, cancellationToken);
            }

            _documents.UploadDocument(student, id, temporaryPath);
            TempData["StudentNotice"] = "Ek-16 belgeniz yüklendi.";
            return RedirectToAction(nameof(Details), new { id });
        }
        catch (DocumentValidationException exception)
        {
            ModelState.AddModelError(nameof(file), string.Join(" ", exception.Errors));
        }
        catch (ForbiddenAccessException)
        {
            return Forbid();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (InvalidOperationException exception)
        {
            ModelState.AddModelError(nameof(file), exception.Message);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(exception, "Ek-16 dosyası {ApplicationId} numaralı başvuru için kaydedilemedi.", id);
            ModelState.AddModelError(nameof(file), "Dosya kaydedilemedi. Lütfen tekrar deneyin.");
        }
        finally
        {
            try
            {
                System.IO.File.Delete(temporaryPath);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                _logger.LogWarning(exception, "Geçici Ek-16 dosyası silinemedi.");
            }
        }

        return ApplicationDetails(student, application);
    }
}
