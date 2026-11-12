using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using StajEvrakSistemi.Core;
using StajEvrakSistemi.Services;
using StajEvrakSistemi.Web;

namespace StajEvrakSistemi.Controllers;

[Authorize(Roles = RolePermissions.RoleAdmin + "," + RolePermissions.RoleStudent)]
[ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
public sealed class ReportController(AuthService auth, InternshipService internships, DocumentService documents,
    StatusWorkflowEngine workflow, Ek16MatcherService matcher) : Controller
{
    [HttpGet]
    public IActionResult Details(int id) => View(BuildReport(id));

    [Authorize(Roles = RolePermissions.RoleAdmin), HttpGet]
    public IActionResult Download(int id)
    {
        var document = documents.GetDocuments(HttpContext.GetCurrentUser(), id).LastOrDefault();
        if (document is null || !System.IO.File.Exists(document.FilePath))
            return NotFound();

        return PhysicalFile(Path.GetFullPath(document.FilePath), "application/octet-stream",
            $"Ek-16-{document.Id}.{document.FileFormat}");
    }

    [Authorize(Roles = RolePermissions.RoleAdmin), HttpPost]
    public IActionResult Review([FromRoute] int id, ReviewInput review)
    {
        var report = BuildReport(id, review);
        if (report.Application.Status != ApplicationStatus.UnderReview)
            return Conflict();

        if (ModelState.IsValid)
        {
            try
            {
                matcher.ReviewScannedDocument(HttpContext.GetCurrentUser(), id, review.DocumentId,
                    review.ToScannedDocument(), review.ToApprovalFields());
                TempData["ReportNotice"] = "Ek-16 inceleme sonucu kaydedildi.";
                return RedirectToAction(nameof(Details), new { id });
            }
            catch (InvalidOperationException error)
            {
                ModelState.AddModelError(string.Empty, error.Message);
            }
        }

        return View(nameof(Details), report);
    }

    [Authorize(Roles = RolePermissions.RoleAdmin), HttpPost]
    public IActionResult UpdateStatus([FromRoute] int id, ApplicationStatus? target, string? note)
    {
        if (!ModelState.IsValid || target is not (ApplicationStatus.UnderReview or ApplicationStatus.FaultyOrMissing
            or ApplicationStatus.Approved or ApplicationStatus.Completed) || note?.Length > 500)
            return BadRequest();

        var user = HttpContext.GetCurrentUser();
        try
        {
            if (target == ApplicationStatus.UnderReview && documents.GetDocuments(user, id).Count == 0)
                throw new InvalidOperationException("İncelemeyi başlatmak için Ek-16 belgesi yüklenmiş olmalıdır.");

            if (target == ApplicationStatus.Approved)
                workflow.ApproveAndComplete(id, user);
            else
                workflow.ChangeStatus(id, target.Value, user, note ?? "");

            TempData["ReportNotice"] = target == ApplicationStatus.Approved
                ? "Başvuru onaylandı ve Tamamlandı durumuna geçti."
                : "Başvuru durumu güncellendi.";
        }
        catch (Exception error) when (error is InvalidOperationException or ArgumentException)
        {
            TempData["ReportError"] = error.Message;
        }

        return RedirectToAction(nameof(Details), new { id });
    }

    public override void OnActionExecuted(ActionExecutedContext context)
    {
        switch (context.Exception)
        {
            case ForbiddenAccessException:
                context.Result = Forbid();
                context.ExceptionHandled = true;
                break;
            case KeyNotFoundException:
                context.Result = NotFound();
                context.ExceptionHandled = true;
                break;
        }
    }

    private ApplicationReport BuildReport(int id, ReviewInput? input = null)
    {
        var user = HttpContext.GetCurrentUser();
        var application = internships.GetApplication(user, id);
        var document = documents.GetDocuments(user, id).LastOrDefault();
        string name = user.Role == UserRole.Student ? user.FullName : auth.GetUser(application.StudentId)?.FullName ?? "";
        var review = input ?? new ReviewInput { DocumentId = document?.Id ?? 0 };
        return new ApplicationReport(application, name, document, workflow.GetHistory(id), review);
    }
}
