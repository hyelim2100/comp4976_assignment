using System.Security.Claims;
using MEMS.Admin.Components;
using MEMS.Admin.Components.Account;
using MEMS.Admin.Services;
using MEMS.Data;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Tenant of each request / circuit: the admin's own tenant, or the Super Admin's working tenant.
// Must be registered before AddMemsData.
builder.Services.AddHttpContextAccessor();
builder.Services.AddAdminTenantContext();

// EF Core + SQL Server ("memsdb" from the Aspire AppHost) and the MEMS.Data service layer,
// used directly by the Blazor components (no HTTP hop to the API).
builder.AddMemsData();
builder.Services.AddScoped<AdminSession>();

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents(options =>
        // Never send exception details to the browser.
        options.DetailedErrors = false);

builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<IdentityRedirectManager>();
// Re-checks the security stamp of every open circuit every minute (see the provider).
builder.Services.AddScoped<AuthenticationStateProvider, IdentityRevalidatingAuthenticationStateProvider>();

builder.Services.AddAuthentication(options =>
    {
        options.DefaultScheme = IdentityConstants.ApplicationScheme;
        options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
    })
    .AddIdentityCookies();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.Name = "MEMS.Admin";
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.SlidingExpiration = true;
});

// Plain HTTP requests re-validate the cookie against the database every minute as well. When the
// principal is rebuilt, the Super Admin's working-tenant claim is carried over.
builder.Services.Configure<SecurityStampValidatorOptions>(options =>
{
    options.ValidationInterval = TimeSpan.FromMinutes(1);
    options.OnRefreshingPrincipal = context =>
    {
        var working = context.CurrentPrincipal?.FindFirst(AdminTenantResolver.WorkingMunicipalityClaim);
        if (working is not null && context.NewPrincipal?.Identity is ClaimsIdentity identity)
        {
            identity.AddClaim(new Claim(working.Type, working.Value));
        }
        return Task.CompletedTask;
    };
});

builder.Services.AddMemsIdentityCore().AddSignInManager();

var app = builder.Build();

// The admin site lives under /admin (https://localhost:7101/admin).
app.UsePathBase("/admin");
app.UseRouting();

// Friendly error page; exception details are only logged (structured, via OpenTelemetry).
app.UseExceptionHandler("/Error", createScopeForErrors: true);
if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

// Requests to the site root go to the admin base path.
app.Use(async (context, next) =>
{
    if (!context.Request.PathBase.HasValue && context.Request.Path == "/")
    {
        context.Response.Redirect("/admin/");
        return;
    }
    await next();
});

app.MapStaticAssets();
app.MapDefaultEndpoints();
app.MapAdminEndpoints();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
