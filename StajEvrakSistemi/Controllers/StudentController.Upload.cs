using Microsoft.AspNetCore.Mvc;
using StajEvrakSistemi.Core;
using StajEvrakSistemi.Models;
using StajEvrakSistemi.Services;
using StajEvrakSistemi.Web;

namespace StajEvrakSistemi.Controllers;

public sealed partial class StudentController
{
    [HttpPost]
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
            TempData["StudentError"] = "Başvurunun mevcut durumunda yeni belge yüklenemez.";
            return RedirectToAction(nameof(Details), new { id });
        }
        try
        {
            await Ek16Upload.Store(file, path => _documents.UploadDocument(student, id, path), _logger, cancellationToken);
            TempData["StudentSuccess"] = "Ek-16 belgeniz yüklendi.";
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

        return Details(id);
    }
}
