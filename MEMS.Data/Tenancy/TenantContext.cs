using System.Security.Claims;
using MEMS.Shared.Security;

namespace MEMS.Data.Tenancy;

// The tenant the current scope (HTTP request, Blazor circuit or background job) works in.
// MemsDbContext reads it in its global query filters, so every EF query is tenant-filtered.
public interface ITenantContext
{
    // True for a Super Admin (or the seeder): query filters are bypassed.
    bool IsSuperAdmin { get; }

    // The active tenant; null when no tenant is set.
    int? MunicipalityId { get; }

    bool IsResolved { get; }
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

    public void SetSuperAdmin() => Set(superAdmin: true, municipalityId: null);

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
        if (user.IsInRole(AppRoles.MunicipalityAdmin)
            && int.TryParse(user.FindFirstValue(MemsClaimTypes.MunicipalityId), out var municipalityId)
            && municipalityId > 0)
        {
            SetMunicipality(municipalityId);
            return true;
        }
        return false;
    }

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
