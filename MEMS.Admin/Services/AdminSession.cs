using MEMS.Data.Services;
using MEMS.Data.Tenancy;
using MEMS.Shared.Dtos;

namespace MEMS.Admin.Services;

// What the current administrator may see, for the UI (scoped per circuit). The data itself is always
// filtered by the services; this only decides which controls to show.
public sealed class AdminSession(ITenantContext tenant, MunicipalityService municipalities)
{
    private IReadOnlyList<MunicipalitySummaryDto>? _directory;

    public bool IsSuperAdmin => tenant.IsSuperAdmin;

    // The tenant this scope works in; null = Super Admin viewing all municipalities.
    public int? WorkingMunicipalityId => tenant.MunicipalityId;

    // A municipality dropdown is shown only to a Super Admin in "All municipalities" mode.
    public bool CanChooseMunicipality => tenant.SeesAllTenants;

    // Municipalities offered in tenant pickers / dropdowns: all for a Super Admin, only their own
    // for a Municipality Admin.
    public async Task<IReadOnlyList<MunicipalitySummaryDto>> GetMunicipalityChoicesAsync()
    {
        if (_directory is null)
        {
            var all = await municipalities.GetDirectoryAsync();
            _directory = tenant.IsSuperAdmin ? all : all.Where(m => m.Id == tenant.MunicipalityId).ToList();
        }
        return _directory;
    }

    public async Task<string> GetScopeNameAsync()
    {
        if (tenant.MunicipalityId is not int id)
        {
            return tenant.IsSuperAdmin ? "All municipalities" : "(no municipality)";
        }
        var municipality = (await GetMunicipalityChoicesAsync()).FirstOrDefault(m => m.Id == id);
        return municipality?.Name ?? "(deleted municipality)";
    }
}
