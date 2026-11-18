namespace MEMS.Shared.Security;

// The two administrator roles. A Super Admin manages every tenant and has no MunicipalityId;
// a Municipality Admin belongs to exactly one tenant (ApplicationUser.MunicipalityId).
public static class AppRoles
{
    public const string SuperAdmin = "Super Admin";
    public const string MunicipalityAdmin = "Municipality Admin";

    public static readonly IReadOnlyList<string> All = [SuperAdmin, MunicipalityAdmin];
}

public static class MemsClaimTypes
{
    // Id of the tenant a Municipality Admin belongs to (cookie and JWT).
    public const string MunicipalityId = "municipality_id";
}
