using System.Security.Claims;
using MEMS.Admin.Services;
using MEMS.Data;
using MEMS.Data.Tenancy;
using MEMS.Shared.Security;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace MEMS.Tests;

// The Admin app's tenant context (AdminTenantResolver.AddAdminTenantContext -> LazyAdminTenantContext):
// resolved lazily on first read, from the Blazor authentication state (circuits / prerendering) or
// HttpContext (plain requests); fail-closed while nobody is signed in; set once afterwards.
public class AdminTenantContextTests
{
    // Mimics ServerAuthenticationStateProvider: throws until SetAuthenticationState was called.
    private sealed class TestAuthenticationStateProvider : AuthenticationStateProvider
    {
        public ClaimsPrincipal? User { get; set; }

        public override Task<AuthenticationState> GetAuthenticationStateAsync() =>
            User is null
                ? throw new InvalidOperationException("GetAuthenticationStateAsync was called before SetAuthenticationState.")
                : Task.FromResult(new AuthenticationState(User));
    }

    private sealed class Harness : IAsyncDisposable
    {
        private readonly ServiceProvider _root;

        public Harness(bool withDatabase = false)
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<IHttpContextAccessor>(Http);
            services.AddScoped<AuthenticationStateProvider>(_ => Auth);
            services.AddAdminTenantContext();
            if (withDatabase)
            {
                var name = Guid.NewGuid().ToString();
                services.AddMemsData(o => o.UseInMemoryDatabase(name).ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning)));
            }
            _root = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
            Scope = _root.CreateAsyncScope();
        }

        public HttpContextAccessor Http { get; } = new() { HttpContext = new DefaultHttpContext() };
        public TestAuthenticationStateProvider Auth { get; } = new();
        public AsyncServiceScope Scope { get; }
        public ITenantContext Tenant => Scope.ServiceProvider.GetRequiredService<ITenantContext>();

        public async ValueTask DisposeAsync()
        {
            await Scope.DisposeAsync();
            await _root.DisposeAsync();
        }
    }

    private static ClaimsPrincipal Anonymous() => new(new ClaimsIdentity());

    private static ClaimsPrincipal MunicipalityAdmin(int municipalityId, int? workingClaim = null)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, "pm"),
            new(ClaimTypes.Role, AppRoles.MunicipalityAdmin),
            new(MemsClaimTypes.MunicipalityId, municipalityId.ToString()),
        };
        if (workingClaim is int w)
        {
            claims.Add(new Claim(AdminTenantResolver.WorkingMunicipalityClaim, w.ToString()));
        }
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));
    }

    private static ClaimsPrincipal SuperAdmin(int? working = null)
    {
        var claims = new List<Claim> { new(ClaimTypes.Name, "super"), new(ClaimTypes.Role, AppRoles.SuperAdmin) };
        if (working is int w)
        {
            claims.Add(new Claim(AdminTenantResolver.WorkingMunicipalityClaim, w.ToString()));
        }
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));
    }

    [Fact]
    public async Task Anonymous_scope_is_unresolved_and_sees_no_tenant()
    {
        await using var h = new Harness();
        h.Http.HttpContext!.User = Anonymous();

        Assert.False(h.Tenant.IsResolved);
        Assert.False(h.Tenant.IsSuperAdmin);
        Assert.Null(h.Tenant.MunicipalityId);
    }

    [Fact]
    public async Task Tenant_is_not_frozen_while_the_user_is_still_unknown()
    {
        await using var h = new Harness();
        var tenant = h.Tenant; // created early, e.g. by the cookie's security-stamp check
        Assert.False(tenant.IsResolved);

        h.Http.HttpContext!.User = MunicipalityAdmin(4); // authentication completes later
        Assert.True(tenant.IsResolved);
        Assert.Equal(4, tenant.MunicipalityId);
    }

    [Fact]
    public async Task Municipality_admin_is_fixed_to_own_tenant_and_ignores_a_working_tenant_claim()
    {
        await using var h = new Harness();
        h.Http.HttpContext!.User = MunicipalityAdmin(4, workingClaim: 1);

        Assert.False(h.Tenant.IsSuperAdmin);
        Assert.Equal(4, h.Tenant.MunicipalityId);
    }

    [Fact]
    public async Task Super_admin_without_working_tenant_sees_all()
    {
        await using var h = new Harness();
        h.Http.HttpContext!.User = SuperAdmin();

        Assert.True(h.Tenant.IsSuperAdmin);
        Assert.True(h.Tenant.SeesAllTenants);
    }

    [Fact]
    public async Task Super_admin_with_working_tenant_is_scoped_to_it()
    {
        await using var h = new Harness();
        h.Http.HttpContext!.User = SuperAdmin(working: 2);

        Assert.True(h.Tenant.IsSuperAdmin);
        Assert.Equal(2, h.Tenant.MunicipalityId);
        Assert.False(h.Tenant.SeesAllTenants);
    }

    [Fact]
    public async Task Circuit_authentication_state_is_preferred_over_HttpContext()
    {
        await using var h = new Harness();
        h.Http.HttpContext!.User = MunicipalityAdmin(1); // stale hub-connection user
        h.Auth.User = MunicipalityAdmin(4);              // the circuit's user

        Assert.Equal(4, h.Tenant.MunicipalityId);
    }

    [Fact]
    public async Task Once_resolved_the_tenant_does_not_follow_a_different_user()
    {
        await using var h = new Harness();
        h.Http.HttpContext!.User = MunicipalityAdmin(4);
        Assert.Equal(4, h.Tenant.MunicipalityId);

        h.Http.HttpContext.User = MunicipalityAdmin(1);
        h.Auth.User = SuperAdmin();
        Assert.Equal(4, h.Tenant.MunicipalityId);
        Assert.False(h.Tenant.IsSuperAdmin);
    }

    [Fact]
    public async Task Admin_without_tenant_claim_stays_unresolved()
    {
        await using var h = new Harness();
        h.Http.HttpContext!.User = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.Name, "orphan"), new Claim(ClaimTypes.Role, AppRoles.MunicipalityAdmin)], "Test"));

        Assert.False(h.Tenant.IsResolved);
        Assert.Null(h.Tenant.MunicipalityId);
    }

    // The regression found in step 4: the DbContext (and with it the tenant context) is created before
    // authentication has set the user. Queries must still use the user's tenant once it is known.
    [Fact]
    public async Task DbContext_created_before_sign_in_filters_by_the_user_tenant_once_known()
    {
        await using var h = new Harness(withDatabase: true);
        var factory = h.Scope.ServiceProvider.GetRequiredService<IDbContextFactory<MemsDbContext>>();

        int burnabyId, surreyId;
        await using (var seedScope = h.Scope.ServiceProvider.GetRequiredService<IServiceScopeFactory>().CreateAsyncScope())
        {
            var seedTenant = seedScope.ServiceProvider.GetRequiredService<TenantContext>();
            seedTenant.SetSuperAdmin();
            // The seed scope's own LazyAdminTenantContext reads the inner TenantContext, which is now set.
            await using var seedDb = await seedScope.ServiceProvider.GetRequiredService<IDbContextFactory<MemsDbContext>>().CreateDbContextAsync();
            var burnaby = new Data.Entities.Municipality { Name = "Burnaby", Slug = "burnaby", Province = "BC" };
            var surrey = new Data.Entities.Municipality { Name = "Surrey", Slug = "surrey", Province = "BC" };
            seedDb.Municipalities.AddRange(burnaby, surrey);
            await seedDb.SaveChangesAsync();
            seedDb.Positions.AddRange(
                new Data.Entities.Position { Name = "Mayor", SeatsAvailable = 1, MunicipalityId = burnaby.Id },
                new Data.Entities.Position { Name = "Mayor", SeatsAvailable = 1, MunicipalityId = surrey.Id });
            await seedDb.SaveChangesAsync();
            (burnabyId, surreyId) = (burnaby.Id, surrey.Id);
        }

        await using var db = await factory.CreateDbContextAsync(); // before sign-in
        h.Http.HttpContext!.User = Anonymous();
        Assert.Equal(0, await db.Positions.CountAsync());          // fail closed while anonymous

        h.Http.HttpContext.User = MunicipalityAdmin(surreyId);      // authentication finished
        Assert.Equal([surreyId], await db.Positions.Select(p => p.MunicipalityId).ToListAsync());
        Assert.DoesNotContain(burnabyId, await db.Municipalities.Select(m => m.Id).ToListAsync());
    }
}
