using MEMS.Data.Services;
using MEMS.Data.Tenancy;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Infrastructure;

namespace MEMS.Api.Infrastructure;

// Resolves the tenant for every API request before the action runs (global filter, runs first).
//
// Routes with {tenant} (/api/burnaby/...):
//   1. The slug is resolved case-insensitively; an unknown slug is 404.
//   2. The scope is ALWAYS bound to that tenant - anonymous visitors, Municipality Admins and Super
//      Admins alike - so the EF query filters return only that tenant's rows.
//   3. An authenticated Municipality Admin of another tenant gets 403 (for reads too).
// Routes without {tenant} (/api/municipalities, /api/auth/me): an authenticated user's own tenant
// is used; anonymous callers stay unresolved (query filters match nothing).
public sealed class TenantResolutionFilter(
    TenantContext tenant,
    MunicipalityService municipalities,
    ILogger<TenantResolutionFilter> logger) : IAsyncActionFilter, IOrderedFilter
{
    public const string RouteKey = "tenant";

    // Before model validation (-2000), so a bad tenant is reported before a bad body.
    public int Order => -3000;

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var user = context.HttpContext.User;

        if (!context.RouteData.Values.TryGetValue(RouteKey, out var routeValue))
        {
            tenant.TrySetFromUser(user);
            await next();
            return;
        }

        var slug = routeValue as string;
        var municipalityId = await municipalities.ResolveSlugAsync(slug, context.HttpContext.RequestAborted);
        if (municipalityId is not int id)
        {
            context.Result = Problem(context, StatusCodes.Status404NotFound, "Municipality not found",
                $"There is no municipality with the URL segment '{slug}'.");
            return;
        }

        if (user.Identity?.IsAuthenticated == true)
        {
            if (user.IsInRole(Shared.Security.AppRoles.SuperAdmin))
            {
                tenant.SetSuperAdmin(id);
            }
            else if (TenantContext.GetUserMunicipalityId(user) == id)
            {
                tenant.SetMunicipality(id);
            }
            else
            {
                logger.LogWarning("Cross-tenant request blocked: user {UserName} (municipality {UserMunicipalityId}) requested {Method} {Path}",
                    user.Identity.Name, TenantContext.GetUserMunicipalityId(user), context.HttpContext.Request.Method, context.HttpContext.Request.Path);
                context.Result = Problem(context, StatusCodes.Status403Forbidden, "Access to this municipality is not allowed",
                    "Municipality administrators can only access the data of their own municipality.");
                return;
            }
        }
        else
        {
            tenant.SetMunicipality(id);
        }

        await next();
    }

    private static ObjectResult Problem(ActionContext context, int status, string title, string detail)
    {
        var factory = context.HttpContext.RequestServices.GetRequiredService<ProblemDetailsFactory>();
        var problem = factory.CreateProblemDetails(context.HttpContext, status, title, detail: detail);
        return new ObjectResult(problem) { StatusCode = status, ContentTypes = { "application/problem+json" } };
    }
}
