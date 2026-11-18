using System.Security.Claims;
using MEMS.Data.Entities;
using MEMS.Shared.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace MEMS.Data.Tenancy;

// Adds the administrator's tenant (municipality_id) to the sign-in cookie, so TenantContext can be
// resolved from the user's claims without another database call.
public class MemsUserClaimsPrincipalFactory(
    UserManager<ApplicationUser> userManager,
    RoleManager<IdentityRole> roleManager,
    IOptions<IdentityOptions> options)
    : UserClaimsPrincipalFactory<ApplicationUser, IdentityRole>(userManager, roleManager, options)
{
    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(ApplicationUser user)
    {
        var identity = await base.GenerateClaimsAsync(user);
        if (user.MunicipalityId is int municipalityId)
        {
            identity.AddClaim(new Claim(MemsClaimTypes.MunicipalityId, municipalityId.ToString()));
        }
        return identity;
    }
}
