using StajEvrakSistemi.Models;

namespace StajEvrakSistemi.Web;

public sealed record StudentApplicationDetails(
    InternshipApplication Application,
    IReadOnlyList<Ek16Document> Documents,
    IReadOnlyList<StudentStatusChange> History,
    bool CanModify,
    DateTime? LatestSubmissionDate,
    bool IsSubmissionOnTime)
{
    public bool CanUpload => CanModify && IsSubmissionOnTime;

    public string? CorrectionNote => StudentCorrection.GetReason(Application.Status,
        History.Select(entry => entry.Transition), Documents.LastOrDefault());
}

public sealed record StudentStatusChange(StatusTransitionLog Transition, string? ActorName);
