using StajEvrakSistemi.Core;
using StajEvrakSistemi.Models;

namespace StajEvrakSistemi.Web;

internal static class StudentCorrection
{
    public static string? GetReason(ApplicationStatus status, IEnumerable<StatusTransitionLog> history, Ek16Document? latestDocument)
    {
        if (status != ApplicationStatus.FaultyOrMissing)
            return null;

        var transitionNote = history.LastOrDefault(entry => entry.NewStatus == ApplicationStatus.FaultyOrMissing)?.Note;
        if (!string.IsNullOrWhiteSpace(transitionNote))
            return transitionNote;

        return string.IsNullOrWhiteSpace(latestDocument?.RejectionReason) ? null : latestDocument.RejectionReason;
    }
}
