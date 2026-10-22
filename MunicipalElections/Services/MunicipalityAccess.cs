using Microsoft.EntityFrameworkCore;
using MunicipalElections.Data;

namespace MunicipalElections.Services;

// Answers "which municipalities may the current admin manage?" (scoped per request).
// Super Admin manages all of them; a municipality admin group manages only the
// municipality its role is assigned to (ApplicationRole.MunicipalityId).
public class MunicipalityAccess(ApplicationDbContext db, IHttpContextAccessor httpContextAccessor)
{
    private int[]? _managedIds;
    private bool _loaded;

    public bool IsSuperAdmin => httpContextAccessor.HttpContext?.User.IsInRole(AppRoles.SuperAdmin) ?? false;

    // Null means every municipality (Super Admin).
    public async Task<int[]?> GetManagedMunicipalityIdsAsync()
    {
        if (_loaded)
        {
            return _managedIds;
        }

        if (!IsSuperAdmin)
        {
            var roleNames = httpContextAccessor.HttpContext?.User.Claims
                .Where(c => c.Type == System.Security.Claims.ClaimTypes.Role)
                .Select(c => c.Value)
                .ToList() ?? [];

            _managedIds = await db.Roles
                .Where(r => r.MunicipalityId != null && roleNames.Contains(r.Name!))
                .Select(r => r.MunicipalityId!.Value)
                .ToArrayAsync();
        }

        _loaded = true;
        return _managedIds;
    }

    public async Task<bool> CanManageAsync(int municipalityId)
    {
        var ids = await GetManagedMunicipalityIdsAsync();
        return ids is null || ids.Contains(municipalityId);
    }

    public async Task<bool> IsAdminAsync()
    {
        var ids = await GetManagedMunicipalityIdsAsync();
        return ids is null || ids.Length > 0;
    }
}
