using Microsoft.AspNetCore.Identity;

namespace MEMS.Data.Entities;

// An administrator account. Super Admins have MunicipalityId = null; a Municipality Admin belongs
// to exactly one tenant (rule enforced by AdminUserService and AdminAccountRules).
public class ApplicationUser : IdentityUser
{
    public int? MunicipalityId { get; set; }

    public Municipality? Municipality { get; set; }
}
