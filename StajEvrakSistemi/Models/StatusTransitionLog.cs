using StajEvrakSistemi.Core;

namespace StajEvrakSistemi.Models;

/// <summary>
/// Bir başvurunun statü değişikliğinin kaydı: kim, ne zaman, hangi statüden hangisine ve neden.
/// </summary>
public class StatusTransitionLog
{
    public StatusTransitionLog(
        int applicationId,
        ApplicationStatus oldStatus,
        ApplicationStatus newStatus,
        int? changedByUserId,
        DateTime changedAt,
        string? note)
    {
        if (applicationId <= 0)
            throw new ArgumentOutOfRangeException(nameof(applicationId), "Geçerli bir başvuru gerekli.");

        ApplicationId = applicationId;
        OldStatus = oldStatus;
        NewStatus = newStatus;
        ChangedByUserId = changedByUserId;
        ChangedAt = changedAt;
        Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
    }

    public int Id { get; internal set; }

    public int ApplicationId { get; }

    public ApplicationStatus OldStatus { get; }

    public ApplicationStatus NewStatus { get; }

    /// <summary>
    /// Değişikliği yapan kullanıcı; kullanıcı silindiyse null.
    /// </summary>
    public int? ChangedByUserId { get; }

    public DateTime ChangedAt { get; }

    public string? Note { get; }

    public override string ToString() =>
        $"#{ApplicationId}: {OldStatus} -> {NewStatus} ({ChangedAt:yyyy-MM-dd HH:mm} UTC)" +
        (Note is null ? string.Empty : $" - {Note}");
}
