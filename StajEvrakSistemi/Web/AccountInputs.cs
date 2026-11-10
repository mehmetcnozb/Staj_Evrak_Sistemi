using System.ComponentModel.DataAnnotations;
using StajEvrakSistemi.Services;

namespace StajEvrakSistemi.Web;

public sealed class LoginInput
{
    [Display(Name = "E-posta veya kimlik numarası")]
    [Required(ErrorMessage = "E-posta veya kimlik numaranızı girin.")]
    public string Identifier { get; set; } = "";

    [Display(Name = "Şifre")]
    [DataType(DataType.Password)]
    [Required(ErrorMessage = "Şifrenizi girin.")]
    public string Password { get; set; } = "";

    public string? ReturnUrl { get; set; }
}

public sealed class StudentRegistrationInput
{
    [Display(Name = "Ad soyad")]
    [Required(ErrorMessage = "Adınızı ve soyadınızı girin.")]
    [StringLength(120)]
    public string FullName { get; set; } = "";

    [Display(Name = "Kimlik numarası")]
    [Required(ErrorMessage = "Kimlik numaranızı girin.")]
    [StringLength(11)]
    public string IdentityNumber { get; set; } = "";

    [Display(Name = "Yabancı uyrukluyum")]
    public bool IsForeign { get; set; }

    [Display(Name = "Öğrenci numarası")]
    [Required(ErrorMessage = "Öğrenci numaranızı girin.")]
    [StringLength(30)]
    public string StudentNumber { get; set; } = "";

    [Display(Name = "E-posta")]
    [Required(ErrorMessage = "E-posta adresinizi girin.")]
    [EmailAddress(ErrorMessage = "Geçerli bir e-posta adresi girin.")]
    [StringLength(254)]
    public string Email { get; set; } = "";

    [Display(Name = "Şifre")]
    [DataType(DataType.Password)]
    [Required(ErrorMessage = "Bir şifre girin.")]
    [MinLength(AuthService.MinPasswordLength, ErrorMessage = "Şifre en az 8 karakter olmalıdır.")]
    public string Password { get; set; } = "";
}
