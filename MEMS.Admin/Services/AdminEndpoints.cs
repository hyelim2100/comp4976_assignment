using System.Security.Claims;
using MEMS.Data.Entities;
using MEMS.Data.Services;
using MEMS.Shared.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace MEMS.Admin.Services;

public static class AdminEndpoints
{
    public static IEndpointRouteBuilder MapAdminEndpoints(this IEndpointRouteBuilder endpoints)
    {
        // Sign out (form POST with antiforgery token from the nav menu).
        endpoints.MapPost("/Account/Logout", async (SignInManager<ApplicationUser> signInManager, ILoggerFactory loggerFactory, ClaimsPrincipal user) =>
        {
            await signInManager.SignOutAsync();
            loggerFactory.CreateLogger("MEMS.Admin.Account").LogInformation("Administrator {UserName} signed out", user.Identity?.Name);
            return TypedResults.LocalRedirect("~/Account/Login");
        });

        // Super Admin: choose the working tenant (id = 0 or missing -> all municipalities).
        // Stores the choice as a claim in the sign-in cookie, then reloads the page.
        endpoints.MapGet("/select-tenant", async (
            [FromQuery] int? municipalityId,
            [FromQuery] string? returnUrl,
            HttpContext context,
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            MunicipalityService municipalities,
            ILoggerFactory loggerFactory) =>
        {
            var user = await userManager.GetUserAsync(context.User);
            if (user is null)
            {
                return Results.LocalRedirect("~/Account/Login");
            }

            var claims = new List<Claim>();
            if (municipalityId is int id && id > 0 && (await municipalities.GetDirectoryAsync()).Any(m => m.Id == id))
            {
                claims.Add(new Claim(AdminTenantResolver.WorkingMunicipalityClaim, id.ToString()));
            }

            var current = await context.AuthenticateAsync(IdentityConstants.ApplicationScheme);
            await signInManager.SignInWithClaimsAsync(user, current.Properties?.IsPersistent ?? false, claims);
            loggerFactory.CreateLogger("MEMS.Admin.Tenant").LogInformation(
                "Super Admin {UserName} switched working tenant to {MunicipalityId}", user.UserName, claims.Count > 0 ? municipalityId : null);

            return Results.LocalRedirect("~/" + SafeLocalPath(returnUrl));
        }).RequireAuthorization(policy => policy.RequireRole(AppRoles.SuperAdmin));

        // Logos / photos for the admin pages, read from SQL Server through the tenant-filtered service:
        // a Municipality Admin cannot load another tenant's images even by guessing the id.
        endpoints.MapGet("/images/{id:guid}", async (Guid id, ImageService images, HttpContext context) =>
        {
            var image = await images.GetAsync(id, includeAllTenantsForSuperAdmin: true, context.RequestAborted);
            if (image is null)
            {
                return Results.NotFound();
            }
            context.Response.Headers.CacheControl = "private, max-age=86400";
            context.Response.Headers.XContentTypeOptions = "nosniff";
            return Results.File(image.Data, image.ContentType);
        }).RequireAuthorization();

        return endpoints;
    }

    // Only base-relative paths inside the admin site (no scheme, host or protocol-relative URLs).
    private static string SafeLocalPath(string? returnUrl)
    {
        if (string.IsNullOrWhiteSpace(returnUrl))
        {
            return string.Empty;
        }
        var path = returnUrl.TrimStart('/');
        return path.Contains("//", StringComparison.Ordinal) || path.Contains(':') || path.Contains('\\') ? string.Empty : path;
    }
}
