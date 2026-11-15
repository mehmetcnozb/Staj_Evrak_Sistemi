using System.ComponentModel.DataAnnotations;

namespace StajEvrakSistemi.Web;

public sealed class LoginInput
{
    [Display(Name = "E-posta, öğrenci numarası veya kimlik numarası")]
    [Required(ErrorMessage = "E-posta, öğrenci numarası veya kimlik numaranızı girin.")]
    public string Identifier { get; set; } = "";

    [Display(Name = "Şifre")]
    [DataType(DataType.Password)]
    [Required(ErrorMessage = "Şifrenizi girin.")]
    public string Password { get; set; } = "";

    public string? ReturnUrl { get; set; }
}

public sealed class StudentRegistrationInput
{
    private string _identityNumber = "";
    private string _studentNumber = "";

    [Display(Name = "Ad soyad")]
    [Required(ErrorMessage = "Adınızı ve soyadınızı girin.")]
    [StringLength(120)]
    public string FullName { get; set; } = "";

    [Display(Name = "Kimlik numarası")]
    [Required(ErrorMessage = "Kimlik numaranızı girin.")]
    public string IdentityNumber
    {
        get => _identityNumber;
        set => _identityNumber = value?.Trim() ?? "";
    }

    [Display(Name = "Yabancı uyrukluyum")]
    public bool IsForeign { get; set; }

    [Display(Name = "Öğrenci numarası")]
    [Required(ErrorMessage = "Öğrenci numaranızı girin.")]
    public string StudentNumber
    {
        get => _studentNumber;
        set => _studentNumber = value?.Trim() ?? "";
    }

    [Display(Name = "E-posta")]
    [Required(ErrorMessage = "E-posta adresinizi girin.")]
    [EmailAddress(ErrorMessage = "Geçerli bir e-posta adresi girin.")]
    [StringLength(254)]
    public string Email { get; set; } = "";

    [Display(Name = "Şifre")]
    [DataType(DataType.Password)]
    [Required(ErrorMessage = "Bir şifre girin.")]
    public string Password { get; set; } = "";
}
