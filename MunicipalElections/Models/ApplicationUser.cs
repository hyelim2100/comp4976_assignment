using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace MunicipalElections.Models;

public class ApplicationUser : IdentityUser
{
    [StringLength(100)]
    public string? Name { get; set; }
}
