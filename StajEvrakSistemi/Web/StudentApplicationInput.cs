using System.ComponentModel.DataAnnotations;
using StajEvrakSistemi.Core;
using StajEvrakSistemi.Models;

namespace StajEvrakSistemi.Web;

public sealed class StudentApplicationInput : IValidatableObject
{
    public int? Id { get; internal set; }

    [Display(Name = "Öğretim yılı")]
    [Required(ErrorMessage = "Öğretim yılını yazın.")]
    public string? AcademicYear { get; set; }

    [Display(Name = "Teslim yarıyılı")]
    [Required(ErrorMessage = "Teslim yarıyılını seçin.")]
    public string? Semester { get; set; }

    [Display(Name = "Staj başlangıç tarihi")]
    [Required(ErrorMessage = "Staj başlangıç tarihini seçin.")]
    public DateTime? StartDate { get; set; }

    [Display(Name = "Staj bitiş tarihi")]
    [Required(ErrorMessage = "Staj bitiş tarihini seçin.")]
    public DateTime? EndDate { get; set; }

    [Display(Name = "İşyeri adı")]
    [Required(ErrorMessage = "İşyeri adını yazın.")]
    public string? CompanyName { get; set; }

    [Display(Name = "Öğrencinin bölümü")]
    [Required(ErrorMessage = "Okuduğunuz bölümü yazın.")]
    public string? Department { get; set; }

    [Display(Name = "İkametgâh adresi")]
    [Required(ErrorMessage = "İkametgâh adresini yazın.")]
    public string? Address { get; set; }

    [Display(Name = "Telefon")]
    [Required(ErrorMessage = "Telefon numarasını yazın.")]
    public string? Phone { get; set; }

    [Display(Name = "E-posta")]
    [Required(ErrorMessage = "E-posta adresini yazın.")]
    public string? Email { get; set; }

    [Display(Name = "Faks (isteğe bağlı)")]
    public string? Fax { get; set; }

    public List<DayOfWeek> WorkDays { get; set; } = [];

    [Display(Name = "İşyerindeki mühendis sayısı")]
    [Required(ErrorMessage = "Mühendis sayısını yazın.")]
    [Range(1, int.MaxValue, ErrorMessage = "İşyerinde en az bir mühendis olmalıdır.")]
    public int? EngineerCount { get; set; }

    [Display(Name = "Mühendisin mezun olduğu bölüm")]
    public string? EngineerGraduationDepartment { get; set; }

    [Display(Name = "Staj evrağı teslim Google Form URL'si")]
    public string? GoogleFormUrl { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!FormatValidator.IsValidAddress(Address))
            yield return new ValidationResult("Adresi mahalle, cadde/sokak, bina no, daire no ve ilçe/il sırasıyla yazın.", [nameof(Address)]);
        if (!FormatValidator.IsValidTurkishPhone(Phone))
            yield return new ValidationResult("Telefon +90 xxx xxx xx xx biçiminde olmalıdır.", [nameof(Phone)]);
        if (!FormatValidator.IsValidEmail(Email))
            yield return new ValidationResult("Geçerli bir e-posta adresi yazın.", [nameof(Email)]);
        if (!FormatValidator.IsValidOptionalFax(Fax))
            yield return new ValidationResult("Faks +90 xxx xxx xx xx biçiminde olmalıdır.", [nameof(Fax)]);
        if (WorkDays.Count == 0)
            yield return new ValidationResult("En az bir çalışma günü seçin.", [nameof(WorkDays)]);
    }

    internal static StudentApplicationInput FromApplication(InternshipApplication application) => new()
    {
        Id = application.Id,
        AcademicYear = application.AcademicYear,
        Semester = application.Semester,
        StartDate = application.StartDate,
        EndDate = application.EndDate,
        CompanyName = application.CompanyName,
        Department = application.Department,
        Address = application.Address,
        Phone = application.Phone,
        Email = application.Email,
        Fax = application.Fax,
        WorkDays = application.WorkDays.ToList(),
        EngineerCount = application.EngineerCount,
        EngineerGraduationDepartment = application.EngineerGraduationDepartment,
        GoogleFormUrl = application.GoogleFormUrl
    };

    internal InternshipApplication ToApplication() => new()
    {
        Id = Id.GetValueOrDefault(),
        AcademicYear = AcademicYear!,
        Semester = Semester,
        StartDate = StartDate!.Value,
        EndDate = EndDate!.Value,
        CompanyName = CompanyName!,
        Department = Department,
        Address = Address,
        Phone = Phone,
        Email = Email,
        Fax = Fax,
        WorkDays = WorkDays,
        EngineerCount = EngineerCount!.Value,
        EngineerGraduationDepartment = EngineerGraduationDepartment,
        GoogleFormUrl = GoogleFormUrl
    };
}
