using StajEvrakSistemi.Models;

namespace StajEvrakSistemi.Web;

/// <summary>
/// Karsilama bolumunde gosterilen ogrenci bilgisi. Bolum, varsa son basvurudan; yoksa
/// admin veri setindeki ogrenci kaydindan gelir. Hicbiri yoksa bos kalir.
/// </summary>
public sealed record StudentProfile(string FullName, string StudentNumber, string? Department);

/// <summary>
/// Ozet kartlarindaki sayilar. Hepsi ayni basvuru listesinden hesaplanir, bu yuzden tabloyla tutarlidir.
/// </summary>
public sealed record ApplicationSummary(int Total, int Uploaded, int UnderReview, int NeedsCorrection, int Approved, int Completed);

/// <summary>
/// Basvurularim tablosunun bir satiri.
/// </summary>
/// <param name="Application">Basvurunun kendisi (sirket, tarihler, NetWorkDays, durum).</param>
/// <param name="StatusText">Ekranda gosterilecek durum adi, ornek "Hatali / Eksik".</param>
/// <param name="StatusCssClass">Durum rozeti icin sinif adi: status-uploaded, status-under-review, status-faulty-or-missing, status-approved, status-completed.</param>
/// <param name="LatestSubmissionDate">Staj baslangicindan 30 gun onceki son teslim tarihi, hesaplanabiliyorsa.</param>
/// <param name="HasDocument">Basvuruya en az bir Ek-16 yuklenmis mi.</param>
/// <param name="CanUploadDocument">Evrak Yukle butonu aktif olmali mi: durum degisiklige acik VE son teslim tarihi gecmemis.</param>
/// <param name="SubmissionDeadlinePassed">Durum degisiklige acik ama son teslim tarihi gecmis; belge artik yuklenemez, uyari gosterilmeli.</param>
/// <param name="CorrectionNote">Hatali/Eksik durumunda yoneticinin yazdigi gerekce; baska durumda null.</param>
public sealed record StudentApplicationRow(
    InternshipApplication Application,
    string StatusText,
    string StatusCssClass,
    DateTime? LatestSubmissionDate,
    bool HasDocument,
    bool CanUploadDocument,
    bool SubmissionDeadlinePassed,
    string? CorrectionNote);

/// <summary>
/// Ogrenci paneli (Index) sayfasinin modeli.
/// </summary>
public sealed class StudentDashboard
{
    public StudentDashboard(StudentProfile profile, ApplicationSummary summary, IReadOnlyList<StudentApplicationRow> applications)
    {
        Profile = profile;
        Summary = summary;
        Applications = applications;
    }

    public StudentProfile Profile { get; }

    public ApplicationSummary Summary { get; }

    /// <summary>
    /// Yeniden eskiye siralidir (en yeni basvuru ustte).
    /// </summary>
    public IReadOnlyList<StudentApplicationRow> Applications { get; }

    /// <summary>
    /// False ise ilk kullanim rehberi gosterilir.
    /// </summary>
    public bool HasApplications => Applications.Count > 0;

    /// <summary>
    /// Duzeltme bekleyen (Hatali/Eksik) basvurular; uyari bandi icin.
    /// </summary>
    public IEnumerable<StudentApplicationRow> NeedsCorrection =>
        Applications.Where(row => row.Application.Status == Core.ApplicationStatus.FaultyOrMissing);

    // Bilgi kutusundaki kurallar sunucudaki sabitlerle ayni kaynaktan gelir.
    public string AcceptedFormatsText => Ek16Document.AcceptedFormatsDescription;

    public int MaxFileSizeMb => (int)(Ek16Document.MaxFileSizeBytes / (1024 * 1024));

    public int MinimumLeadDays => Core.InternshipDateValidator.MinimumLeadDays;
}
