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
    StatusWorkflowEngine workflow) : Controller
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

    private ApplicationReport BuildReport(int id)
    {
        var user = HttpContext.GetCurrentUser();
        var application = internships.GetApplication(user, id);
        var document = documents.GetDocuments(user, id).LastOrDefault();
        string name = user.Role == UserRole.Student ? user.FullName : auth.GetUser(application.StudentId)?.FullName ?? "";
        return new ApplicationReport(application, name, document, workflow.GetHistory(id));
    }
}
