using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using StajEvrakSistemi.Core;
using StajEvrakSistemi.Models;
using StajEvrakSistemi.Services;

namespace StajEvrakSistemi.Web;

public static class WebSession
{
    internal const string SessionTokenClaim = "staj:session";
    private static readonly object CurrentUserKey = new();

    public static User GetCurrentUser(this HttpContext context) => (User)context.Items[CurrentUserKey]!;

    internal static async Task ValidateAsync(CookieValidatePrincipalContext context)
    {
        var services = context.HttpContext.RequestServices;
        var token = context.Principal?.FindFirstValue(SessionTokenClaim);
        var session = services.GetRequiredService<SessionManager>().GetSession(token);
        var user = session is null ? null : services.GetRequiredService<AuthService>().GetUser(session.UserId);
        if (user is null || user.Role != session!.Role ||
            context.Principal?.IsInRole(RolePermissions.ToRoleName(user.Role)) != true)
        {
            context.RejectPrincipal();
            await context.HttpContext.SignOutAsync();
            return;
        }

        context.HttpContext.Items[CurrentUserKey] = user;
    }
}
