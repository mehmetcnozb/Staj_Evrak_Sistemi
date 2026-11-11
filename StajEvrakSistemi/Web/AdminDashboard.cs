using StajEvrakSistemi.Models;
using StajEvrakSistemi.Services;

namespace StajEvrakSistemi.Web;

public sealed record AdminDashboard(
    IReadOnlyList<InternshipApplication> Applications,
    bool Completed,
    DatasetImportResult? ImportResult,
    string? ImportError);
