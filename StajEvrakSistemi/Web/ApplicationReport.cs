using System.ComponentModel.DataAnnotations;
using StajEvrakSistemi.Models;

namespace StajEvrakSistemi.Web;

public sealed record ApplicationReport(InternshipApplication Application, string StudentName, Ek16Document? Document,
    IReadOnlyList<StatusTransitionLog> History, ReviewInput Review);

public sealed class ReviewInput
{
    public int DocumentId { get; set; }

    [Display(Name = "Öğrenci numarası"), StringLength(30)]
    public string? StudentNumber { get; set; }

    [Display(Name = "Kimlik numarası"), StringLength(30)]
    public string? IdentityNumber { get; set; }

    [Display(Name = "Ad soyad"), StringLength(120)]
    public string? FullName { get; set; }

    [Display(Name = "Bölüm"), StringLength(150)]
    public string? Department { get; set; }

    [Display(Name = "Öğretim yılı"), StringLength(20)]
    public string? AcademicYear { get; set; }

    [Display(Name = "Yarıyıl"), StringLength(30)]
    public string? Semester { get; set; }

    [Display(Name = "Staj başlangıç tarihi")]
    public DateTime? StartDate { get; set; }

    [Display(Name = "Staj bitiş tarihi")]
    public DateTime? EndDate { get; set; }

    [Display(Name = "Belgede yazan staj günü sayısı")]
    public decimal? DeclaredWorkDays { get; set; }

    [Display(Name = "İşyeri adı"), StringLength(150)]
    public string? CompanyName { get; set; }

    [Display(Name = "İşyerindeki mühendis sayısı")]
    public int? EngineerCount { get; set; }

    [Display(Name = "İkametgâh adresi"), StringLength(500)]
    public string? Address { get; set; }

    [Display(Name = "Telefon"), StringLength(50)]
    public string? Phone { get; set; }

    [Display(Name = "E-posta"), StringLength(254)]
    public string? Email { get; set; }

    [Display(Name = "Faks"), StringLength(50)]
    public string? Fax { get; set; }

    public DayOfWeek[] WorkDays { get; set; } = [];

    public bool? StudentApprovalPresent { get; set; }
    public bool? AdvisorApprovalPresent { get; set; }
    public bool? EmployerFieldsComplete { get; set; }
    public bool? EmployerStampPresent { get; set; }
    public bool? EmployerSignaturePresent { get; set; }

    internal ScannedDocumentFields ToScannedDocument() =>
        new(StudentNumber, IdentityNumber, FullName, Department, AcademicYear, StartDate, EndDate, DeclaredWorkDays,
            WorkDays.Length == 0 ? null : WorkDays, Address, Phone, Email, Fax, Semester, CompanyName, EngineerCount);

    internal DocumentApprovalFields ToApprovalFields() =>
        new(StudentApprovalPresent, AdvisorApprovalPresent, EmployerFieldsComplete, EmployerStampPresent, EmployerSignaturePresent);
}
