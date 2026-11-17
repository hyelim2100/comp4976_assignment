using MEMS.Data.Entities;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace MEMS.Data;

public class MemsDbContext(DbContextOptions<MemsDbContext> options) : IdentityDbContext<ApplicationUser>(options)
{
}
