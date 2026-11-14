using StajEvrakSistemi.Core;
using StajEvrakSistemi.Data;
using StajEvrakSistemi.Models;
using StajEvrakSistemi.Services;

namespace StajEvrakSistemi.Web;

/// <summary>
/// Ogrenci paneli sayfasinin verisini toplar: karsilama bilgisi, ozet sayilari ve basvuru satirlari.
/// Yalnizca giris yapan ogrencinin kendi basvurulari kullanilir; yetki kontrolu mevcut servislerdedir.
/// </summary>
public sealed class StudentDashboardService
{
    private readonly DatabaseManager _database;
    private readonly InternshipService _internships;
    private readonly DocumentService _documents;
    private readonly StatusWorkflowEngine _workflow;
    private readonly Func<DateTime> _utcNow;

    public StudentDashboardService(
        DatabaseManager database, InternshipService internships, DocumentService documents,
        StatusWorkflowEngine workflow, Func<DateTime>? utcNow = null)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
        _internships = internships ?? throw new ArgumentNullException(nameof(internships));
        _documents = documents ?? throw new ArgumentNullException(nameof(documents));
        _workflow = workflow ?? throw new ArgumentNullException(nameof(workflow));
        _utcNow = utcNow ?? (() => DateTime.UtcNow);
    }

    public StudentDashboard Build(User student)
    {
        if (student is not Student currentStudent)
            throw student is null
                ? new LoginRequiredException("Paneli görmek için giriş yapmalısınız.")
                : new ForbiddenAccessException("403 Yetkisiz Erişim: Bu panel yalnızca öğrenciler içindir.");

        var applications = _internships.GetMyApplications(currentStudent)
            .OrderByDescending(application => application.Id)
            .ToList();

        DateTime today = InternshipDateValidator.ToTurkeyDate(_utcNow());
        var rows = applications.Select(application => BuildRow(currentStudent, application, today)).ToList();

        return new StudentDashboard(BuildProfile(currentStudent, applications), BuildSummary(applications), rows);
    }

    private StudentApplicationRow BuildRow(Student student, InternshipApplication application, DateTime today)
    {
        var documents = _documents.GetDocuments(student, application.Id);
        bool statusAllowsChanges = _workflow.CanModifyOrDelete(application.Status, student);
        DateTime? latestSubmission = InternshipDateValidator.GetLatestSubmissionDate(application.StartDate);

        // Yukleme sirasinda sunucu da ayni fonksiyonla bakar (DocumentService); bu yuzden ekrandaki durum
        // ile sunucu kurali ayrismaz: son teslim gecmisse belge yuklenemez.
        bool deadlinePassed = statusAllowsChanges && !InternshipDateValidator.IsSubmissionOnTime(today, application.StartDate);
        bool canUpload = statusAllowsChanges && !deadlinePassed;

        string? correctionNote = application.Status == ApplicationStatus.FaultyOrMissing
            ? StudentCorrection.GetReason(application.Status, _workflow.GetHistory(application.Id), documents.LastOrDefault())
            : null;

        return new StudentApplicationRow(
            application,
            StatusWorkflowEngine.GetDisplayName(application.Status),
            StatusCssClass(application.Status),
            latestSubmission,
            documents.Count > 0,
            canUpload,
            deadlinePassed,
            correctionNote);
    }

    // Kartlardaki sayilar tablodaki listeyle ayni kaynaktan hesaplanir.
    private static ApplicationSummary BuildSummary(IReadOnlyCollection<InternshipApplication> applications)
    {
        int Count(ApplicationStatus status) => applications.Count(application => application.Status == status);

        return new ApplicationSummary(
            applications.Count,
            Count(ApplicationStatus.Uploaded),
            Count(ApplicationStatus.UnderReview),
            Count(ApplicationStatus.FaultyOrMissing),
            Count(ApplicationStatus.Approved),
            Count(ApplicationStatus.Completed));
    }

    private StudentProfile BuildProfile(Student student, IReadOnlyList<InternshipApplication> applications)
    {
        string? department = applications
            .Select(application => application.Department)
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))
            ?? FindDepartmentInDataset(student);

        return new StudentProfile(student.FullName, student.StudentNumber, department?.Trim());
    }

    // Basvuru yoksa bolum, admin veri setindeki en yeni ogretim yilina ait kayittan alinir.
    private string? FindDepartmentInDataset(Student student)
    {
        using var connection = _database.GetConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT Department FROM OfficialStudentRecords
            WHERE StudentNumber = $studentNumber AND IdentityNumber = $identityNumber
            ORDER BY AcademicYear DESC, Id DESC
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("$studentNumber", student.StudentNumber);
        command.Parameters.AddWithValue("$identityNumber", student.IdentityNumber);
        return command.ExecuteScalar() as string;
    }

    private static string StatusCssClass(ApplicationStatus status) => status switch
    {
        ApplicationStatus.Uploaded => "status-uploaded",
        ApplicationStatus.UnderReview => "status-under-review",
        ApplicationStatus.FaultyOrMissing => "status-faulty-or-missing",
        ApplicationStatus.Approved => "status-approved",
        ApplicationStatus.Completed => "status-completed",
        _ => "status-unknown"
    };
}
