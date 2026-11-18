using System.Security.Claims;
using MEMS.Shared.Security;

namespace MEMS.Data.Tenancy;

// The tenant the current scope (HTTP request, Blazor circuit or background job) works in.
// MemsDbContext reads it in its global query filters, so every EF query is tenant-filtered.
//
// Two separate questions:
//   IsSuperAdmin    - who is acting (may manage every tenant: create/delete tenants, users, slugs)
//   MunicipalityId  - which tenant's data this scope reads and writes
// Query filters are bypassed only when a Super Admin works with no tenant selected (Admin app
// "all municipalities" views). A Super Admin calling /api/burnaby/... is scoped to Burnaby only.
public interface ITenantContext
{
    bool IsSuperAdmin { get; }

    // The tenant whose data this scope sees; null = none (or all, for an unscoped Super Admin).
    int? MunicipalityId { get; }

    bool IsResolved { get; }

    // True only for a Super Admin without a tenant scope: query filters are off.
    bool SeesAllTenants => IsSuperAdmin && MunicipalityId is null;
}

// Registered as a scoped service: one instance per request / circuit.
//
// Safety rules:
// * Fail closed - until a tenant is set, IsSuperAdmin is false and MunicipalityId is null, so the
//   query filters match no rows. Forgetting to resolve the tenant never leaks data.
// * Set once - a scope can be resolved only once. Trying to switch to another tenant throws, so one
//   user's scope can never be re-used for another tenant.
public sealed class TenantContext : ITenantContext
{
    public bool IsSuperAdmin { get; private set; }

    public int? MunicipalityId { get; private set; }

    public bool IsResolved { get; private set; }

    // Super Admin working across all tenants (Admin app lists, seeding).
    public void SetSuperAdmin() => Set(superAdmin: true, municipalityId: null);

    // Super Admin working inside one tenant (e.g. API route /api/{tenant}/...): data is limited to
    // that tenant, but Super-Admin-only operations stay allowed.
    public void SetSuperAdmin(int municipalityId)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(municipalityId);
        Set(superAdmin: true, municipalityId);
    }

    // Municipality Admin or anonymous public visitor of one tenant.
    public void SetMunicipality(int municipalityId)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(municipalityId);
        Set(superAdmin: false, municipalityId);
    }

    // Resolves the tenant from an authenticated administrator (cookie or JWT claims).
    // Returns false when the user is neither a Super Admin nor assigned to a tenant.
    public bool TrySetFromUser(ClaimsPrincipal user)
    {
        if (user.Identity?.IsAuthenticated != true)
        {
            return false;
        }
        if (user.IsInRole(AppRoles.SuperAdmin))
        {
            SetSuperAdmin();
            return true;
        }
        if (GetUserMunicipalityId(user) is int municipalityId)
        {
            SetMunicipality(municipalityId);
            return true;
        }
        return false;
    }

    // The tenant a Municipality Admin belongs to (from the municipality_id claim), or null.
    public static int? GetUserMunicipalityId(ClaimsPrincipal user) =>
        user.IsInRole(AppRoles.MunicipalityAdmin)
        && int.TryParse(user.FindFirstValue(MemsClaimTypes.MunicipalityId), out var id)
        && id > 0
            ? id
            : null;

    private void Set(bool superAdmin, int? municipalityId)
    {
        if (IsResolved)
        {
            if (IsSuperAdmin == superAdmin && MunicipalityId == municipalityId)
            {
                return;
            }
            throw new InvalidOperationException("The tenant for this scope has already been set and cannot be changed.");
        }

        IsSuperAdmin = superAdmin;
        MunicipalityId = municipalityId;
        IsResolved = true;
    }
}
