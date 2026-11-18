using MEMS.Data.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace MEMS.Data;

// Used only by "dotnet ef migrations add". The connection string is a placeholder: migrations are
// generated without connecting, and at runtime the Aspire-managed connection string is used.
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<MemsDbContext>
{
    public MemsDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<MemsDbContext>()
            .UseSqlServer("Server=(design-time);Database=memsdb;Trusted_Connection=True")
            .Options;
        var tenant = new TenantContext();
        tenant.SetSuperAdmin();
        return new MemsDbContext(options, tenant);
    }
}
