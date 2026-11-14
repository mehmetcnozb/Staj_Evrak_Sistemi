using StajEvrakSistemi.Core;
using StajEvrakSistemi.Models;
using StajEvrakSistemi.Services;

namespace StajEvrakSistemi.Web;

public sealed record AdminDashboard(
    IReadOnlyList<AdminApplicationRow> Applications,
    IReadOnlyList<AdminApplicationRow> ReviewQueue,
    bool Completed,
    ApplicationStatus? Status,
    string? Search,
    DatasetImportResult? ImportResult,
    string? ImportError);

public sealed record AdminApplicationRow(InternshipApplication Application, ApplicationStudent? Student)
{
    public string StudentName => Student?.FullName ?? $"Kullanıcı #{Application.StudentId}";
    public string? StudentNumber => Student?.StudentNumber;
}
