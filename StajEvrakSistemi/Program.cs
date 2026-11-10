using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using StajEvrakSistemi.Data;
using StajEvrakSistemi.Services;
using StajEvrakSistemi.Web;

bool createAdmin = args is ["--create-admin"];
var builder = WebApplication.CreateBuilder(createAdmin ? [] : args);
string dataDirectory = Path.GetFullPath(builder.Configuration["StajDataDirectory"] ?? ".",
    builder.Environment.ContentRootPath);
Directory.CreateDirectory(dataDirectory);
var database = new DatabaseManager(Path.Combine(dataDirectory, DatabaseManager.DefaultDatabaseFile));
database.InitializeDatabase();

if (createAdmin)
{
    Environment.ExitCode = AdminSetup.Run(new AuthService(database));
    return;
}

builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = 12 * 1024 * 1024);
builder.Services.AddSingleton(database);
builder.Services.AddSingleton<AuthService>();
builder.Services.AddSingleton<SessionManager>();
builder.Services.AddSingleton<StatusWorkflowEngine>();
builder.Services.AddSingleton<InternshipService>();
builder.Services.AddSingleton<DatasetImportService>();
builder.Services.AddSingleton<Ek16MatcherService>();
builder.Services.AddSingleton(services => new DocumentService(database,
    services.GetRequiredService<StatusWorkflowEngine>(), Path.Combine(dataDirectory, "uploads")));
builder.Services.AddDataProtection().UseEphemeralDataProtectionProvider();
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "StajEvrak.Oturum";
        options.LoginPath = "/Account/Login";
        options.SlidingExpiration = false;
        options.Events.OnValidatePrincipal = WebSession.ValidateAsync;
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
