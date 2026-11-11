using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StajEvrakSistemi.Core;
using StajEvrakSistemi.Models;
using StajEvrakSistemi.Services;
using StajEvrakSistemi.Web;

namespace StajEvrakSistemi.Controllers;

[Authorize(Roles = RolePermissions.RoleStudent)]
public sealed partial class StudentController(
    InternshipService internships, DocumentService documents, StatusWorkflowEngine workflow,
    ILogger<StudentController> logger) : Controller
{
    private readonly InternshipService _internships = internships;
    private readonly DocumentService _documents = documents;
    private readonly StatusWorkflowEngine _workflow = workflow;
    private readonly ILogger<StudentController> _logger = logger;

    [HttpGet]
    public IActionResult Index()
    {
        var student = HttpContext.GetCurrentUser();
        return View(_internships.GetMyApplications(student));
    }

    [HttpGet]
    public IActionResult Details(int id)
    {
        var student = HttpContext.GetCurrentUser();
        try
        {
            var application = _internships.GetApplication(student, id);
            return ApplicationDetails(student, application);
        }
        catch (ForbiddenAccessException)
        {
            return Forbid();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpGet]
    public IActionResult Create() => View("ApplicationForm", new StudentApplicationInput());

    [HttpPost]
    public IActionResult Create(StudentApplicationInput input)
    {
        var student = HttpContext.GetCurrentUser();
        ValidateGoogleFormUrl(input);
        if (!ModelState.IsValid)
            return View("ApplicationForm", input);

        try
        {
            int id = _internships.CreateApplication(student, input.ToApplication());
            TempData["StudentNotice"] = "Başvurunuz kaydedildi. Ek-16 belgesini aşağıdan yükleyebilirsiniz.";
            return RedirectToAction(nameof(Details), new { id });
        }
        catch (ArgumentException exception)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            return View("ApplicationForm", input);
        }
    }

    [HttpGet]
    public IActionResult Edit(int id)
    {
        var student = HttpContext.GetCurrentUser();
        try
        {
            var application = _internships.GetApplication(student, id);
            if (!_workflow.CanModifyOrDelete(application.Status, student))
            {
                TempData["StudentNotice"] = "Başvuruyu mevcut durumunda düzenleyemezsiniz.";
                return RedirectToAction(nameof(Details), new { id });
            }

            return View("ApplicationForm", StudentApplicationInput.FromApplication(application));
        }
        catch (ForbiddenAccessException)
        {
            return Forbid();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpPost]
    public IActionResult Edit([FromRoute] int id, StudentApplicationInput input)
    {
        var student = HttpContext.GetCurrentUser();
        input.Id = id;
        try
        {
            var application = _internships.GetApplication(student, id);
            if (!_workflow.CanModifyOrDelete(application.Status, student))
            {
                TempData["StudentNotice"] = "Başvuruyu mevcut durumunda düzenleyemezsiniz.";
                return RedirectToAction(nameof(Details), new { id });
            }

            ValidateGoogleFormUrl(input);
            if (!ModelState.IsValid)
                return View("ApplicationForm", input);

            _internships.UpdateApplication(student, input.ToApplication());
            TempData["StudentNotice"] = "Başvurunuz güncellendi.";
            return RedirectToAction(nameof(Details), new { id });
        }
        catch (ForbiddenAccessException)
        {
            return Forbid();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (ArgumentException exception)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            return View("ApplicationForm", input);
        }
        catch (InvalidOperationException exception)
        {
            TempData["StudentNotice"] = exception.Message;
            return RedirectToAction(nameof(Details), new { id });
        }
    }

    private ViewResult ApplicationDetails(User student, InternshipApplication application) => View(nameof(Details),
        new StudentApplicationDetails(application, _documents.GetDocuments(student, application.Id),
            _workflow.CanModifyOrDelete(application.Status, student)));

    private void ValidateGoogleFormUrl(StudentApplicationInput input)
    {
        if (!GoogleFormUrlValidator.IsValid(input.GoogleFormUrl))
            ModelState.AddModelError(nameof(StudentApplicationInput.GoogleFormUrl), "Geçerli bir HTTPS Google Form bağlantısı girin.");
    }
}
