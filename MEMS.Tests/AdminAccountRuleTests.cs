using System.ComponentModel.DataAnnotations;
using MEMS.Data.Services;
using MEMS.Shared.Dtos;
using MEMS.Shared.Security;
using Microsoft.Extensions.DependencyInjection;

namespace MEMS.Tests;

// "Municipality Admin -> MunicipalityId required, Super Admin -> null" is validated both by the
// input model (user-management form / API) and by the service layer.
public class AdminAccountRuleTests
{
    [Theory]
    [InlineData(AppRoles.MunicipalityAdmin, null, false)]
    [InlineData(AppRoles.MunicipalityAdmin, 1, true)]
    [InlineData(AppRoles.SuperAdmin, null, true)]
    [InlineData(AppRoles.SuperAdmin, 1, false)]
    [InlineData("Guest", null, false)]
    public void Input_model_enforces_the_role_tenant_rule(string role, int? municipalityId, bool valid)
    {
        var input = new AdminUserCreateInput
        {
            UserName = "newadmin", Email = "new@example.com", Password = "P@$$w0rd", ConfirmPassword = "P@$$w0rd",
            Role = role, MunicipalityId = municipalityId,
        };
        var results = new List<ValidationResult>();

        Assert.Equal(valid, Validator.TryValidateObject(input, new ValidationContext(input), results, validateAllProperties: true));
    }

    [Fact]
    public async Task Service_rejects_municipality_admin_without_tenant_even_when_ui_validation_is_bypassed()
    {
        await using var host = await TestHost.CreateAsync();
        await using var scope = host.SuperAdminScope();
        var users = scope.ServiceProvider.GetRequiredService<AdminUserService>();

        var missingTenant = await users.CreateAsync(new AdminUserCreateInput
        {
            UserName = "orphan", Email = "orphan@example.com", Password = "P@$$w0rd", ConfirmPassword = "P@$$w0rd",
            Role = AppRoles.MunicipalityAdmin, MunicipalityId = null,
        });
        Assert.Equal(ServiceStatus.Invalid, missingTenant.Status);

        var superWithTenant = await users.CreateAsync(new AdminUserCreateInput
        {
            UserName = "super2", Email = "super2@example.com", Password = "P@$$w0rd", ConfirmPassword = "P@$$w0rd",
            Role = AppRoles.SuperAdmin, MunicipalityId = host.BurnabyId,
        });
        Assert.Equal(ServiceStatus.Invalid, superWithTenant.Status);

        var unknownTenant = await users.CreateAsync(new AdminUserCreateInput
        {
            UserName = "ghost", Email = "ghost@example.com", Password = "P@$$w0rd", ConfirmPassword = "P@$$w0rd",
            Role = AppRoles.MunicipalityAdmin, MunicipalityId = 999,
        });
        Assert.Equal(ServiceStatus.Invalid, unknownTenant.Status);
    }

    [Fact]
    public async Task Service_creates_valid_municipality_admin_and_lists_it()
    {
        await using var host = await TestHost.CreateAsync();
        await using var scope = host.SuperAdminScope();
        var users = scope.ServiceProvider.GetRequiredService<AdminUserService>();

        var created = await users.CreateAsync(new AdminUserCreateInput
        {
            UserName = "Surreyadmin2", Email = "s2@example.com", Password = "P@$$w0rd", ConfirmPassword = "P@$$w0rd",
            Role = AppRoles.MunicipalityAdmin, MunicipalityId = host.SurreyId,
        });

        Assert.True(created.Succeeded, created.Message);
        Assert.Equal(host.SurreyId, created.Value!.MunicipalityId);
        Assert.Equal(AppRoles.MunicipalityAdmin, created.Value.Role);
    }

    [Fact]
    public async Task Only_super_admin_can_manage_accounts()
    {
        await using var host = await TestHost.CreateAsync();
        await using var scope = host.TenantScope(host.BurnabyId);
        var users = scope.ServiceProvider.GetRequiredService<AdminUserService>();

        Assert.Equal(ServiceStatus.Forbidden, (await users.ListAsync()).Status);
        Assert.Equal(ServiceStatus.Forbidden, (await users.CreateAsync(new AdminUserCreateInput())).Status);
    }
}
