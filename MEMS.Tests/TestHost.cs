using MEMS.Data;
using MEMS.Data.Entities;
using MEMS.Data.Tenancy;
using MEMS.Shared.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace MEMS.Tests;

// Builds the real MEMS.Data registrations (AddMemsData + AddMemsIdentityCore) on the EF in-memory
// provider, with DI scope validation on, and seeds two tenants: Burnaby and Surrey.
public sealed class TestHost : IAsyncDisposable
{
    private TestHost(ServiceProvider root) => Root = root;

    public ServiceProvider Root { get; }

    public int BurnabyId { get; private set; }
    public int SurreyId { get; private set; }
    public int BurnabyMayorId { get; private set; }
    public int SurreyMayorId { get; private set; }
    public int SurreyCandidateId { get; private set; }

    public static async Task<TestHost> CreateAsync()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        var databaseName = Guid.NewGuid().ToString();
        services.AddMemsData(options => options
            .UseInMemoryDatabase(databaseName)
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning)));
        services.AddMemsIdentityCore();

        var root = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        var host = new TestHost(root);
        await host.SeedAsync();
        return host;
    }

    // A scope as a request / Blazor circuit would have it: tenant resolved once, at the start.
    public AsyncServiceScope SuperAdminScope()
    {
        var scope = Root.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().SetSuperAdmin();
        return scope;
    }

    // Super Admin calling a tenant route such as /api/burnaby/...
    public AsyncServiceScope SuperAdminScope(int municipalityId)
    {
        var scope = Root.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().SetSuperAdmin(municipalityId);
        return scope;
    }

    public AsyncServiceScope TenantScope(int municipalityId)
    {
        var scope = Root.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().SetMunicipality(municipalityId);
        return scope;
    }

    private async Task SeedAsync()
    {
        await using var scope = SuperAdminScope();
        var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<MemsDbContext>>();
        await using var db = await factory.CreateDbContextAsync();

        var burnaby = new Municipality { Name = "Burnaby", Slug = "burnaby", Province = "BC", ElectionDate = new(2026, 10, 17) };
        var surrey = new Municipality { Name = "Surrey", Slug = "surrey", Province = "BC", ElectionDate = new(2026, 10, 17) };
        db.Municipalities.AddRange(burnaby, surrey);
        await db.SaveChangesAsync();

        var burnabyMayor = new Position { Name = "Mayor", SeatsAvailable = 1, MunicipalityId = burnaby.Id };
        var burnabyCouncil = new Position { Name = "Councillor", SeatsAvailable = 8, MunicipalityId = burnaby.Id };
        var surreyMayor = new Position { Name = "Mayor", SeatsAvailable = 1, MunicipalityId = surrey.Id };
        db.Positions.AddRange(burnabyMayor, burnabyCouncil, surreyMayor);
        await db.SaveChangesAsync();

        var surreyCandidate = new Candidate { Name = "Eleanor Whitfield", PositionId = surreyMayor.Id, MunicipalityId = surrey.Id };
        db.Candidates.AddRange(
            new Candidate { Name = "Julia Kowalski", PositionId = burnabyMayor.Id, MunicipalityId = burnaby.Id },
            new Candidate { Name = "Nina Petrova", PositionId = burnabyCouncil.Id, MunicipalityId = burnaby.Id },
            surreyCandidate);
        await db.SaveChangesAsync();

        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (var role in AppRoles.All)
        {
            await roles.CreateAsync(new IdentityRole(role));
        }

        BurnabyId = burnaby.Id;
        SurreyId = surrey.Id;
        BurnabyMayorId = burnabyMayor.Id;
        SurreyMayorId = surreyMayor.Id;
        SurreyCandidateId = surreyCandidate.Id;
    }

    public ValueTask DisposeAsync() => Root.DisposeAsync();
}
