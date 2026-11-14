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
    StudentDashboardService dashboard, AuthService auth, ILogger<StudentController> logger) : Controller
{
    private readonly InternshipService _internships = internships;
    private readonly DocumentService _documents = documents;
    private readonly StatusWorkflowEngine _workflow = workflow;
    private readonly StudentDashboardService _dashboard = dashboard;
    private readonly AuthService _auth = auth;
    private readonly ILogger<StudentController> _logger = logger;

    [HttpGet]
    public IActionResult Index()
    {
        var student = HttpContext.GetCurrentUser();
        return View(_dashboard.Build(student));
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
            TempData["StudentSuccess"] = "Başvurunuz kaydedildi.";
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
                TempData["StudentError"] = "Başvuruyu mevcut durumunda düzenleyemezsiniz.";
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
                TempData["StudentError"] = "Başvuruyu mevcut durumunda düzenleyemezsiniz.";
                return RedirectToAction(nameof(Details), new { id });
            }

            ValidateGoogleFormUrl(input);
            if (!ModelState.IsValid)
                return View("ApplicationForm", input);

            _internships.UpdateApplication(student, input.ToApplication());
            TempData["StudentSuccess"] = "Başvurunuz güncellendi.";
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
            TempData["StudentError"] = exception.Message;
            return RedirectToAction(nameof(Details), new { id });
        }
    }

    private ViewResult ApplicationDetails(User student, InternshipApplication application)
    {
        var documents = _documents.GetDocuments(student, application.Id);
        var history = _workflow.GetHistory(application.Id);
        var actors = history.Select(entry => entry.ChangedByUserId).OfType<int>().Distinct()
            .ToDictionary(id => id, id => id == student.Id ? student.FullName : _auth.GetUser(id)?.FullName);
        var changes = history.Select(entry => new StudentStatusChange(entry,
            entry.ChangedByUserId is int actorId ? actors[actorId] : null)).ToList();
        return View(nameof(Details), new StudentApplicationDetails(application, documents, changes,
            _workflow.CanModifyOrDelete(application.Status, student),
            InternshipDateValidator.GetLatestSubmissionDate(application.StartDate),
            InternshipDateValidator.IsSubmissionOnTime(InternshipDateValidator.ToTurkeyDate(DateTime.UtcNow), application.StartDate)));
    }

    private void ValidateGoogleFormUrl(StudentApplicationInput input)
    {
        if (!GoogleFormUrlValidator.IsValid(input.GoogleFormUrl))
            ModelState.AddModelError(nameof(StudentApplicationInput.GoogleFormUrl), "Geçerli bir HTTPS Google Form bağlantısı girin.");
    }
}
