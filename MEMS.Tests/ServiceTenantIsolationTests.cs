using MEMS.Data.Services;
using MEMS.Shared.Dtos;
using MEMS.Shared.Security;
using Microsoft.Extensions.DependencyInjection;

namespace MEMS.Tests;

// Service-layer tenant rules (A1 deficiency #2: the "pm" admin could see other municipalities).
public class ServiceTenantIsolationTests
{
    [Fact]
    public async Task Municipality_admin_only_sees_own_tenant_in_lists_and_dashboard()
    {
        await using var host = await TestHost.CreateAsync();
        await using var scope = host.TenantScope(host.BurnabyId);
        var sp = scope.ServiceProvider;

        Assert.Equal(["Burnaby"], (await sp.GetRequiredService<MunicipalityService>().ListAsync()).Select(m => m.Name));
        Assert.All(await sp.GetRequiredService<PositionService>().ListAsync(), p => Assert.Equal(host.BurnabyId, p.MunicipalityId));
        // Even an explicit request for another tenant's rows returns nothing.
        Assert.Empty(await sp.GetRequiredService<CandidateService>().ListAsync(municipalityId: host.SurreyId));

        var dashboard = await sp.GetRequiredService<DashboardService>().GetAsync();
        Assert.Equal(new DashboardDto(1, 2, 2), dashboard);
    }

    [Fact]
    public async Task Other_tenant_ids_are_not_found()
    {
        await using var host = await TestHost.CreateAsync();
        await using var scope = host.TenantScope(host.BurnabyId);
        var sp = scope.ServiceProvider;

        Assert.Null(await sp.GetRequiredService<MunicipalityService>().GetAsync(host.SurreyId));
        Assert.Null(await sp.GetRequiredService<PositionService>().GetAsync(host.SurreyMayorId));
        Assert.Null(await sp.GetRequiredService<CandidateService>().GetAsync(host.SurreyCandidateId));

        var update = await sp.GetRequiredService<CandidateService>().UpdateAsync(
            host.SurreyCandidateId, new CandidateInput { Name = "Hacked", PositionId = host.SurreyMayorId }, null, false);
        Assert.Equal(ServiceStatus.NotFound, update.Status);
        Assert.Equal(ServiceStatus.NotFound, (await sp.GetRequiredService<CandidateService>().DeleteAsync(host.SurreyCandidateId)).Status);
    }

    [Fact]
    public async Task Municipality_admin_creates_positions_only_in_own_tenant_whatever_is_posted()
    {
        await using var host = await TestHost.CreateAsync();
        await using var scope = host.TenantScope(host.BurnabyId);

        var result = await scope.ServiceProvider.GetRequiredService<PositionService>()
            .CreateAsync(new PositionInput { Name = "School Trustee", SeatsAvailable = 7, MunicipalityId = host.SurreyId });

        Assert.True(result.Succeeded);
        Assert.Equal(host.BurnabyId, result.Value!.MunicipalityId);
    }

    [Fact]
    public async Task Scoped_super_admin_writes_go_to_the_route_tenant()
    {
        await using var host = await TestHost.CreateAsync();
        await using var scope = host.SuperAdminScope(host.SurreyId);

        var result = await scope.ServiceProvider.GetRequiredService<PositionService>()
            .CreateAsync(new PositionInput { Name = "School Trustee", SeatsAvailable = 6, MunicipalityId = host.BurnabyId });

        Assert.True(result.Succeeded);
        Assert.Equal(host.SurreyId, result.Value!.MunicipalityId);
    }

    [Fact]
    public async Task Candidate_cannot_be_attached_to_another_tenants_position()
    {
        await using var host = await TestHost.CreateAsync();
        await using var scope = host.TenantScope(host.BurnabyId);

        var result = await scope.ServiceProvider.GetRequiredService<CandidateService>()
            .CreateAsync(new CandidateInput { Name = "Intruder", PositionId = host.SurreyMayorId }, null);

        Assert.Equal(ServiceStatus.Invalid, result.Status);
        Assert.Contains(nameof(CandidateInput.PositionId), result.Errors.Keys);
    }

    [Fact]
    public async Task Candidate_takes_its_tenant_from_the_position()
    {
        await using var host = await TestHost.CreateAsync();
        await using var scope = host.SuperAdminScope();

        var result = await scope.ServiceProvider.GetRequiredService<CandidateService>()
            .CreateAsync(new CandidateInput { Name = "New Candidate", PositionId = host.SurreyMayorId }, null);

        Assert.True(result.Succeeded);
        Assert.Equal(host.SurreyId, result.Value!.MunicipalityId);
    }

    [Fact]
    public async Task Municipality_admin_cannot_change_slug_create_or_delete_tenants()
    {
        await using var host = await TestHost.CreateAsync();
        await using var scope = host.TenantScope(host.BurnabyId);
        var municipalities = scope.ServiceProvider.GetRequiredService<MunicipalityService>();

        var slugChange = await municipalities.UpdateAsync(host.BurnabyId, new MunicipalityInput
        {
            Name = "Burnaby", Slug = "bby", Province = "BC", ElectionDate = new(2026, 10, 17), PrimaryColor = "#8E44AD",
        }, null, false);
        Assert.Equal(ServiceStatus.Invalid, slugChange.Status);

        Assert.Equal(ServiceStatus.Forbidden, (await municipalities.DeleteAsync(host.SurreyId)).Status);
        Assert.Equal(ServiceStatus.Forbidden, (await municipalities.GetDeletionImpactAsync(host.BurnabyId)).Status);
        Assert.Equal(ServiceStatus.Forbidden, (await municipalities.CreateTenantAsync(new TenantCreateInput(), null)).Status);
    }

    [Fact]
    public async Task Public_directory_and_slug_resolution_are_case_insensitive()
    {
        await using var host = await TestHost.CreateAsync();
        await using var scope = host.Root.CreateAsyncScope(); // anonymous visitor, no tenant yet
        var municipalities = scope.ServiceProvider.GetRequiredService<MunicipalityService>();

        Assert.Equal(2, (await municipalities.GetDirectoryAsync()).Count);
        Assert.Equal(host.SurreyId, await municipalities.ResolveSlugAsync("Surrey"));
        Assert.Equal(host.SurreyId, await municipalities.ResolveSlugAsync(" SURREY "));
        Assert.Null(await municipalities.ResolveSlugAsync("vancouver"));
    }
}
