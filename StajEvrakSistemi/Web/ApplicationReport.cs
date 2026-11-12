using StajEvrakSistemi.Models;

namespace StajEvrakSistemi.Web;

public sealed record ApplicationReport(InternshipApplication Application, string StudentName, Ek16Document? Document,
    IReadOnlyList<StatusTransitionLog> History);
