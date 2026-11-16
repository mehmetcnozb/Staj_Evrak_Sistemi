using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using StajEvrakSistemi.Core;
using StajEvrakSistemi.Data;
using StajEvrakSistemi.Services;
using StajEvrakSistemi.Web;

bool createAdmin = args is ["--create-admin"];
bool resetAdmins = args is ["--reset-admins"];
var builder = WebApplication.CreateBuilder(createAdmin || resetAdmins ? [] : args);
FacultyEmailPolicy.Domain = builder.Configuration["AdminEmailDomain"] ?? FacultyEmailPolicy.DefaultDomain;
string dataDirectory = Path.GetFullPath(builder.Configuration["StajDataDirectory"] ?? ".",
    builder.Environment.ContentRootPath);
Directory.CreateDirectory(dataDirectory);
var database = new DatabaseManager(Path.Combine(dataDirectory, DatabaseManager.DefaultDatabaseFile));
database.InitializeDatabase();

if (resetAdmins)
{
    int removed = new AuthService(database).DeleteAllAdmins();
    Console.WriteLine($"{removed} yönetici hesabı silindi. Yeni hesaplar /Account/AdminRegister ekranından açılır.");
    return;
}

if (createAdmin)
{
    Environment.ExitCode = AdminSetup.Run(new AuthService(database));
    return;
}

builder.Services.AddSingleton(database);
builder.Services.AddSingleton<AuthService>();
builder.Services.AddSingleton<SessionManager>();
builder.Services.AddSingleton<StatusWorkflowEngine>();
builder.Services.AddSingleton<InternshipService>();
builder.Services.AddSingleton<DatasetImportService>();
builder.Services.AddSingleton<Ek16MatcherService>();
builder.Services.AddSingleton<Ek16DocumentReader>();
builder.Services.AddSingleton(services => new DocumentService(database,
    services.GetRequiredService<StatusWorkflowEngine>(), Path.Combine(dataDirectory, "uploads")));
builder.Services.AddSingleton<StudentDashboardService>();
builder.Services.AddDataProtection().UseEphemeralDataProtectionProvider();
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "StajEvrak.Oturum";
        options.LoginPath = "/Account/Login";
        options.SlidingExpiration = false;
        options.Events.OnValidatePrincipal = WebSession.ValidateAsync;
        // Yönetici sayfasına girişsiz gelen kişi yönetici girişine yönlenir.
        options.Events.OnRedirectToLogin = context =>
        {
            string target = context.RedirectUri;
            if (context.Request.Path.StartsWithSegments("/Admin"))
                target = target.Replace("/Account/Login", "/Account/AdminLogin", StringComparison.Ordinal);
            context.Response.Redirect(target);
            return Task.CompletedTask;
        };
        options.Events.OnRedirectToAccessDenied = context =>
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        };
    });
builder.Services.AddAuthorization();
builder.Services.AddControllersWithViews(options => options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute()));

var app = builder.Build();
app.UseStatusCodePagesWithReExecute("/Account/Status/{0}");
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllerRoute("default", "{controller=Account}/{action=Index}/{id?}");
app.Run();
