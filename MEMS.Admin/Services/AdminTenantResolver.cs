using System.Security.Claims;
using MEMS.Data.Tenancy;
using MEMS.Shared.Security;
using Microsoft.AspNetCore.Components.Authorization;

namespace MEMS.Admin.Services;

// Resolves the tenant of an Admin-app scope (one HTTP request, or one Blazor Server circuit).
//
// * Municipality Admin -> always their own tenant (municipality_id claim). No choice.
// * Super Admin        -> "All municipalities", or the working tenant they picked in the header.
//   The pick is stored as a claim in the sign-in cookie (see /select-tenant): a circuit cannot read
//   cookies, but it always has the user's claims. Changing it re-issues the cookie and reloads the
//   page, so the new circuit starts with a new, set-once TenantContext.
public static class AdminTenantResolver
{
    public const string WorkingMunicipalityClaim = "mems_working_municipality_id";

    public static IServiceCollection AddAdminTenantContext(this IServiceCollection services)
    {
        // Registered before AddMemsData (which uses TryAdd), so these win.
        services.AddScoped<TenantContext>();
        services.AddScoped<ITenantContext, LazyAdminTenantContext>();
        return services;
    }

    public static void Apply(TenantContext tenant, ClaimsPrincipal user)
    {
        if (user.Identity?.IsAuthenticated != true)
        {
            return; // unresolved: queries return nothing
        }

        if (user.IsInRole(AppRoles.SuperAdmin))
        {
            if (int.TryParse(user.FindFirstValue(WorkingMunicipalityClaim), out var working) && working > 0)
            {
                tenant.SetSuperAdmin(working);
            }
            else
            {
                tenant.SetSuperAdmin();
            }
            return;
        }

        tenant.TrySetFromUser(user);
    }

    // Resolves on first READ, not on construction. The DbContext (and so ITenantContext) is created
    // very early in a request - e.g. by the cookie's security-stamp check, before HttpContext.User is
    // set. Resolving there would freeze the scope as "no tenant". Instead the tenant is resolved when a
    // query first needs it; while nobody is signed in it stays unresolved (fail closed) and is retried,
    // and once resolved it is fixed for the rest of the scope (TenantContext is set-once).
    private sealed class LazyAdminTenantContext(TenantContext inner, IServiceProvider services) : ITenantContext
    {
        public bool IsSuperAdmin => Resolved.IsSuperAdmin;
        public int? MunicipalityId => Resolved.MunicipalityId;
        public bool IsResolved => Resolved.IsResolved;

        private TenantContext Resolved
        {
            get
            {
                if (!inner.IsResolved)
                {
                    Apply(inner, GetUser(services));
                }
                return inner;
            }
        }
    }

    // In a circuit (and during prerendering) the user comes from the Blazor authentication state; in
    // plain HTTP requests (minimal API endpoints) from HttpContext.
    private static ClaimsPrincipal GetUser(IServiceProvider services)
    {
        try
        {
            var state = services.GetRequiredService<AuthenticationStateProvider>().GetAuthenticationStateAsync();
            if (state.IsCompletedSuccessfully && state.Result.User.Identity?.IsAuthenticated == true)
            {
                return state.Result.User;
            }
        }
        catch (InvalidOperationException)
        {
            // Not inside a Blazor render: the authentication state was never set.
        }
        return services.GetRequiredService<IHttpContextAccessor>().HttpContext?.User ?? new ClaimsPrincipal(new ClaimsIdentity());
    }
}
