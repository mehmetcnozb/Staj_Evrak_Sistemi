using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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
                ? $"Hesap geçici olarak kilitli. {Math.Ceiling(result.RemainingLockout?.TotalMinutes ?? 15)} dakika sonra tekrar deneyin."
                : "E-posta, kimlik numarası veya şifre hatalı.");
            return View(input);
        }

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
            ExpiresUtc = new DateTimeOffset(session.ExpiresAt)
        });

        return Url.IsLocalUrl(input.ReturnUrl) ? LocalRedirect(input.ReturnUrl!) : RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public IActionResult Register() => View(new StudentRegistrationInput());

    [HttpPost]
    public IActionResult Register(StudentRegistrationInput input)
    {
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

    [Authorize, HttpPost]
    public async Task<IActionResult> Logout()
    {
        sessions.Logout(User.FindFirstValue(WebSession.SessionTokenClaim));
        await HttpContext.SignOutAsync();
        return RedirectToAction(nameof(Login));
    }

    [IgnoreAntiforgeryToken]
    [Route("Account/Status/{id:int}")]
    public IActionResult Status(int id) => View(id);
}
