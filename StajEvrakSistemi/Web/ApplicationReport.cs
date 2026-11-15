using StajEvrakSistemi.Models;

namespace StajEvrakSistemi.Web;

public sealed record ApplicationReport(InternshipApplication Application, string StudentName, Ek16Document? Document,
    IReadOnlyList<StatusTransitionLog> History, ReviewInput Review,
    ScannedDocumentFields? ReadFields, string? ReadError);

public sealed class ReviewInput
{
    public int DocumentId { get; set; }

    public bool? StudentApprovalPresent { get; set; }
    public bool? AdvisorApprovalPresent { get; set; }
    public bool? EmployerFieldsComplete { get; set; }
    public bool? EmployerStampPresent { get; set; }
    public bool? EmployerSignaturePresent { get; set; }

    internal DocumentApprovalFields ToApprovalFields() =>
        new(StudentApprovalPresent, AdvisorApprovalPresent, EmployerFieldsComplete, EmployerStampPresent, EmployerSignaturePresent);
}
