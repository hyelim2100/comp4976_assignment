using Microsoft.AspNetCore.Identity;

namespace MunicipalElections.Models;

// An admin group. A group with a MunicipalityId (e.g. "Pitt Meadows Admin") may manage
// only that municipality; "Super Admin" has no MunicipalityId and manages everything.
public class ApplicationRole : IdentityRole
{
    public ApplicationRole() { }

    public ApplicationRole(string roleName) : base(roleName) { }

    public int? MunicipalityId { get; set; }

    public Municipality? Municipality { get; set; }
}
