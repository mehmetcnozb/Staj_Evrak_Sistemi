using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using StajEvrakSistemi.Core;
using StajEvrakSistemi.Models;
using StajEvrakSistemi.Services;
using StajEvrakSistemi.Web;

namespace StajEvrakSistemi.Controllers;

[ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
public sealed class AccountController(AuthService auth, SessionManager sessions) : Controller
{
    [HttpGet]
    public IActionResult Index() => User.Identity?.IsAuthenticated == true
        ? RedirectToAction("Index", User.IsInRole(RolePermissions.RoleAdmin) ? "Admin" : "Student")
        : RedirectToAction(nameof(Login));

    [HttpGet]
    public IActionResult Login(string? returnUrl) => View(new LoginInput { ReturnUrl = returnUrl });

    [HttpPost]
    public async Task<IActionResult> Login(LoginInput input)
    {
        if (!ModelState.IsValid)
            return View(input);

        var result = auth.Login(input.Identifier, input.Password);
        if (result.User is not { } user)
        {
            ModelState.AddModelError(string.Empty, result.Status == LoginStatus.LockedOut
                ? "Hesap kilitli."
                : "Giris bilgileri veya sifre hatali.");
            return View(input);
        }

        // Yönetici hesabı öğrenci ekranından oturum açamaz.
        if (user.Role == UserRole.Admin)
        {
            ModelState.AddModelError(string.Empty, "Yönetici hesapları yönetici girişi ekranını kullanır.");
            return View(input);
        }

        await SignInAsync(user);
        return Url.IsLocalUrl(input.ReturnUrl) ? LocalRedirect(input.ReturnUrl!) : RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public IActionResult AdminLogin(string? returnUrl) => View(new AdminLoginInput { ReturnUrl = returnUrl });

    [HttpPost]
    public async Task<IActionResult> AdminLogin(AdminLoginInput input)
    {
        if (!ModelState.IsValid)
            return View(input);

        var result = auth.LoginAdmin(input.Email, input.Password);
        if (result.User is not { } user)
        {
            ModelState.AddModelError(string.Empty, result.Status == LoginStatus.LockedOut
                ? "Hesap kilitli."
                : "E-posta veya şifre hatalı.");
            return View(input);
        }

        await SignInAsync(user);
        return Url.IsLocalUrl(input.ReturnUrl) ? LocalRedirect(input.ReturnUrl!) : RedirectToAction("Index", "Admin");
    }

    [HttpGet]
    public IActionResult AdminRegister() => View(new AdminRegistrationInput());

    [HttpPost]
    public IActionResult AdminRegister(AdminRegistrationInput input)
    {
        const string identityField = nameof(AdminRegistrationInput.IdentityNumber);
        if (ModelState.GetFieldValidationState(identityField) != ModelValidationState.Invalid
            && GetIdentityNumberError(input.IdentityNumber, isForeign: false) is { } identityError)
            ModelState.AddModelError(identityField, identityError);

        const string emailField = nameof(AdminRegistrationInput.Email);
        if (ModelState.GetFieldValidationState(emailField) != ModelValidationState.Invalid
            && !FacultyEmailPolicy.IsFacultyEmail(input.Email))
            ModelState.AddModelError(emailField, FacultyEmailPolicy.Requirement);

        if (!ModelState.IsValid)
            return View(input);

        try
        {
            auth.Register(new Admin(input.IdentityNumber, input.FullName, input.Email), input.Password);
            TempData["AccountMessage"] = "Yönetici hesabınız oluşturuldu. Giriş yapabilirsiniz.";
            return RedirectToAction(nameof(AdminLogin));
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException)
        {
            ModelState.AddModelError(string.Empty, error is InvalidOperationException
                ? "Bu T.C. kimlik no veya e-posta ile zaten bir kayıt var."
                : error.Message);
            return View(input);
        }
    }

    private async Task SignInAsync(User user)
    {
        sessions.Logout(User.FindFirstValue(WebSession.SessionTokenClaim));
        var session = sessions.StartSession(user);
        var identity = new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString(CultureInfo.InvariantCulture)),
            new Claim(ClaimTypes.Name, user.FullName),
            new Claim(ClaimTypes.Role, RolePermissions.ToRoleName(user.Role)),
            new Claim(WebSession.SessionTokenClaim, session.SessionToken)
        }, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(new ClaimsPrincipal(identity), new AuthenticationProperties
        {
            ExpiresUtc = session.ExpiresAt
        });
    }

    [HttpGet]
    public IActionResult Register() => View(new StudentRegistrationInput());

    [HttpPost]
    public IActionResult Register(StudentRegistrationInput input)
    {
        ValidateRegistrationNumbers(input);
        if (!ModelState.IsValid)
            return View(input);

        try
        {
            auth.Register(new Student(input.IdentityNumber, input.StudentNumber, input.FullName, input.Email,
                isForeign: input.IsForeign), input.Password);
            TempData["AccountMessage"] = "Öğrenci hesabınız oluşturuldu. Giriş yapabilirsiniz.";
            return RedirectToAction(nameof(Login));
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException)
        {
            ModelState.AddModelError(string.Empty, error.Message);
            return View(input);
        }
    }

    private void ValidateRegistrationNumbers(StudentRegistrationInput input)
    {
        const string identityField = nameof(StudentRegistrationInput.IdentityNumber);
        if (ModelState.GetFieldValidationState(identityField) != ModelValidationState.Invalid)
        {
            if (GetIdentityNumberError(input.IdentityNumber, input.IsForeign) is { } error)
                ModelState.AddModelError(identityField, error);
        }

        const string studentField = nameof(StudentRegistrationInput.StudentNumber);
        if (ModelState.GetFieldValidationState(studentField) != ModelValidationState.Invalid)
        {
            if (input.StudentNumber.Length > 30)
                ModelState.AddModelError(studentField, "Öğrenci numarası en fazla 30 karakter olabilir.");
            else if (!FormatValidator.IsValidStudentNumber(input.StudentNumber))
                ModelState.AddModelError(studentField, "Öğrenci numarası yalnızca A-Z harfleri ve 0-9 rakamları içerebilir.");
        }
    }

    private static string? GetIdentityNumberError(string number, bool isForeign)
    {
        if (number.Length != 11)
            return "Kimlik numarası 11 haneli olmalıdır.";

        if (isForeign)
            return IdentityNumberValidator.IsValidForeignIdentityNumber(number)
                ? null : "Yabancı kimlik numarası 11 rakamdan oluşmalı ve 99 ile başlamalıdır.";

        if (!IdentityNumberValidator.IsValidFormat(number))
            return "T.C. kimlik numarası 11 rakamdan oluşmalı ve 0 ile başlamamalıdır.";

        return IdentityNumberValidator.IsValidTurkishIdentityNumber(number)
            ? null : "T.C. kimlik numarasının kontrol haneleri geçersiz. Numarayı kontrol edin.";
    }

    [Authorize, HttpPost]
    public async Task<IActionResult> Logout()
    {
        bool wasAdmin = User.IsInRole(RolePermissions.RoleAdmin);
        sessions.Logout(User.FindFirstValue(WebSession.SessionTokenClaim));
        await HttpContext.SignOutAsync();
        return RedirectToAction(wasAdmin ? nameof(AdminLogin) : nameof(Login));
    }

    [IgnoreAntiforgeryToken]
    [Route("Account/Status/{id:int}")]
    public IActionResult Status(int id) => View(id);
}
