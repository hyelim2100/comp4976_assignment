using MEMS.Data;
using MEMS.Data.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace MEMS.Tests;

// The Admin app (Blazor Server) uses IDbContextFactory together with the ITenantContext-based global
// query filters. These tests pin down that the factory's lifetime keeps every context bound to the
// tenant of the scope that created it - no missing tenant, no tenants mixed between users.
public class TenantDbContextFactoryTests
{
    [Fact]
    public async Task Factory_is_registered_scoped_and_cannot_be_resolved_from_the_root_provider()
    {
        await using var host = await TestHost.CreateAsync();

        // A singleton factory would build contexts from the root provider and lose the user's tenant.
        Assert.Throws<InvalidOperationException>(() => host.Root.GetRequiredService<IDbContextFactory<MemsDbContext>>());
    }

    [Fact]
    public async Task Contexts_created_by_the_factory_get_the_tenant_of_their_own_scope()
    {
        await using var host = await TestHost.CreateAsync();

        await using var burnabyScope = host.TenantScope(host.BurnabyId);
        await using var surreyScope = host.TenantScope(host.SurreyId);
        var burnabyFactory = burnabyScope.ServiceProvider.GetRequiredService<IDbContextFactory<MemsDbContext>>();
        var surreyFactory = surreyScope.ServiceProvider.GetRequiredService<IDbContextFactory<MemsDbContext>>();

        // Interleaved use, as two concurrent Blazor circuits would do.
        await using var burnabyDb = await burnabyFactory.CreateDbContextAsync();
        await using var surreyDb = await surreyFactory.CreateDbContextAsync();

        Assert.All(await burnabyDb.Candidates.ToListAsync(), c => Assert.Equal(host.BurnabyId, c.MunicipalityId));
        Assert.All(await surreyDb.Candidates.ToListAsync(), c => Assert.Equal(host.SurreyId, c.MunicipalityId));
        Assert.Equal(2, await burnabyDb.Candidates.CountAsync());
        Assert.Equal(1, await surreyDb.Candidates.CountAsync());
        Assert.Equal([host.BurnabyId], await burnabyDb.Municipalities.Select(m => m.Id).ToListAsync());
    }

    [Fact]
    public async Task Each_context_from_the_same_factory_shares_the_scope_tenant()
    {
        await using var host = await TestHost.CreateAsync();
        await using var scope = host.TenantScope(host.SurreyId);
        var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<MemsDbContext>>();

        for (var i = 0; i < 3; i++)
        {
            await using var db = await factory.CreateDbContextAsync();
            Assert.Equal(host.SurreyId, db.Tenant.MunicipalityId);
            Assert.Equal(1, await db.Positions.CountAsync());
        }
    }

    [Fact]
    public async Task Unresolved_tenant_fails_closed_and_sees_no_rows()
    {
        await using var host = await TestHost.CreateAsync();
        await using var scope = host.Root.CreateAsyncScope(); // tenant never set
        await using var db = await scope.ServiceProvider.GetRequiredService<IDbContextFactory<MemsDbContext>>().CreateDbContextAsync();

        Assert.Equal(0, await db.Municipalities.CountAsync());
        Assert.Equal(0, await db.Positions.CountAsync());
        Assert.Equal(0, await db.Candidates.CountAsync());
    }

    [Fact]
    public async Task Super_admin_scope_sees_every_tenant()
    {
        await using var host = await TestHost.CreateAsync();
        await using var scope = host.SuperAdminScope();
        await using var db = await scope.ServiceProvider.GetRequiredService<IDbContextFactory<MemsDbContext>>().CreateDbContextAsync();

        Assert.Equal(2, await db.Municipalities.CountAsync());
        Assert.Equal(3, await db.Candidates.CountAsync());
    }

    [Fact]
    public async Task Super_admin_scoped_to_a_tenant_sees_only_that_tenant()
    {
        await using var host = await TestHost.CreateAsync();
        await using var scope = host.SuperAdminScope(host.BurnabyId);
        await using var db = await scope.ServiceProvider.GetRequiredService<IDbContextFactory<MemsDbContext>>().CreateDbContextAsync();

        Assert.True(db.Tenant.IsSuperAdmin);
        Assert.Equal([host.BurnabyId], await db.Municipalities.Select(m => m.Id).ToListAsync());
        Assert.All(await db.Candidates.ToListAsync(), c => Assert.Equal(host.BurnabyId, c.MunicipalityId));
    }

    [Fact]
    public void Tenant_context_cannot_be_switched_to_another_tenant_once_set()
    {
        var tenant = new TenantContext();
        tenant.SetMunicipality(1);

        tenant.SetMunicipality(1); // same tenant again is fine
        Assert.Throws<InvalidOperationException>(() => tenant.SetMunicipality(2));
        Assert.Throws<InvalidOperationException>(() => tenant.SetSuperAdmin());
    }
}
