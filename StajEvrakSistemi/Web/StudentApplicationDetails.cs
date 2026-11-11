using StajEvrakSistemi.Models;

namespace StajEvrakSistemi.Web;

public sealed record StudentApplicationDetails(
    InternshipApplication Application,
    IReadOnlyList<Ek16Document> Documents,
    bool CanModify);
