using MEMS.Data.Entities;
using MEMS.Data.Tenancy;
using MEMS.Shared.Dtos;
using MEMS.Shared.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MEMS.Data.Services;

// Administrator accounts. Super Admin only.
//
// Invariant (checked here, in the service, and also by the input models in the UI/API):
//   Municipality Admin -> MunicipalityId required and pointing to an existing tenant
//   Super Admin        -> MunicipalityId is null
public class AdminUserService(
    UserManager<ApplicationUser> userManager,
    IDbContextFactory<MemsDbContext> dbFactory,
    ITenantContext tenant,
    ILogger<AdminUserService> logger)
{
    public async Task<ServiceResult<IReadOnlyList<AdminUserDto>>> ListAsync()
    {
        if (!tenant.IsSuperAdmin)
        {
            return ServiceResult<IReadOnlyList<AdminUserDto>>.From(ServiceResult.Forbidden());
        }

        await using var db = await dbFactory.CreateDbContextAsync();
        var users = await QueryUsers(db).OrderBy(u => u.MunicipalityName).ThenBy(u => u.UserName).ToListAsync();
        return users;
    }

    public async Task<ServiceResult<AdminUserDto>> GetAsync(string id)
    {
        if (!tenant.IsSuperAdmin)
        {
            return ServiceResult<AdminUserDto>.From(ServiceResult.Forbidden());
        }

        await using var db = await dbFactory.CreateDbContextAsync();
        var user = await QueryUsers(db).FirstOrDefaultAsync(u => u.Id == id);
        return user is null ? ServiceResult<AdminUserDto>.From(ServiceResult.NotFound("The administrator was not found.")) : user;
    }

    public async Task<ServiceResult<AdminUserDto>> CreateAsync(AdminUserCreateInput input)
    {
        if (!tenant.IsSuperAdmin)
        {
            return ServiceResult<AdminUserDto>.From(ServiceResult.Forbidden());
        }
        if (input.Password != input.ConfirmPassword)
        {
            return ServiceResult<AdminUserDto>.From(ServiceResult.Invalid(nameof(input.ConfirmPassword), "The passwords do not match."));
        }

        var ruleError = await ValidateAssignmentAsync(input.Role, input.MunicipalityId);
        if (ruleError is not null)
        {
            return ServiceResult<AdminUserDto>.From(ruleError);
        }

        var result = await CreateAccountAsync(input.UserName, input.Email, input.Password, input.Role, input.MunicipalityId);
        if (!result.Succeeded)
        {
            return ServiceResult<AdminUserDto>.From(result);
        }

        return await GetAsync(result.Value!);
    }

    public async Task<ServiceResult> UpdateAssignmentAsync(string id, AdminUserAssignmentInput input, string? currentUserId)
    {
        if (!tenant.IsSuperAdmin)
        {
            return ServiceResult.Forbidden();
        }

        var user = await userManager.FindByIdAsync(id);
        if (user is null)
        {
            return ServiceResult.NotFound("The administrator was not found.");
        }

        var ruleError = await ValidateAssignmentAsync(input.Role, input.MunicipalityId);
        if (ruleError is not null)
        {
            return ruleError;
        }

        var currentRoles = await userManager.GetRolesAsync(user);
        if (currentRoles.Contains(AppRoles.SuperAdmin) && input.Role != AppRoles.SuperAdmin)
        {
            if (id == currentUserId)
            {
                return ServiceResult.Invalid(nameof(input.Role), "You cannot remove your own Super Admin role.");
            }
            if (await IsLastSuperAdminAsync(user))
            {
                return ServiceResult.Invalid(nameof(input.Role), "At least one Super Admin must remain.");
            }
        }

        user.Email = input.Email;
        user.MunicipalityId = input.Role == AppRoles.SuperAdmin ? null : input.MunicipalityId;
        var update = await userManager.UpdateAsync(user);
        if (!update.Succeeded)
        {
            return ToResult(update);
        }

        var roleChange = await SetSingleRoleAsync(user, input.Role, currentRoles);
        if (!roleChange.Succeeded)
        {
            return roleChange;
        }

        // A new security stamp signs the user out everywhere, so a changed tenant takes effect
        // immediately instead of when the old cookie / claims expire.
        await userManager.UpdateSecurityStampAsync(user);
        logger.LogInformation("Administrator {UserName} assigned to role {Role} and municipality {MunicipalityId}",
            user.UserName, input.Role, user.MunicipalityId);
        return ServiceResult.Ok();
    }

    public async Task<ServiceResult> ResetPasswordAsync(string id, ResetPasswordInput input)
    {
        if (!tenant.IsSuperAdmin)
        {
            return ServiceResult.Forbidden();
        }
        if (input.NewPassword != input.ConfirmPassword)
        {
            return ServiceResult.Invalid(nameof(input.ConfirmPassword), "The passwords do not match.");
        }

        var user = await userManager.FindByIdAsync(id);
        if (user is null)
        {
            return ServiceResult.NotFound("The administrator was not found.");
        }

        var token = await userManager.GeneratePasswordResetTokenAsync(user);
        var reset = await userManager.ResetPasswordAsync(user, token, input.NewPassword);
        if (!reset.Succeeded)
        {
            return ToResult(reset, nameof(input.NewPassword));
        }

        logger.LogInformation("Password reset for administrator {UserName}", user.UserName);
        return ServiceResult.Ok();
    }

    public async Task<ServiceResult> DeleteAsync(string id, string? currentUserId)
    {
        if (!tenant.IsSuperAdmin)
        {
            return ServiceResult.Forbidden();
        }
        if (id == currentUserId)
        {
            return ServiceResult.Invalid(string.Empty, "You cannot delete your own account.");
        }

        var user = await userManager.FindByIdAsync(id);
        if (user is null)
        {
            return ServiceResult.NotFound("The administrator was not found.");
        }
        if (await userManager.IsInRoleAsync(user, AppRoles.SuperAdmin) && await IsLastSuperAdminAsync(user))
        {
            return ServiceResult.Invalid(string.Empty, "At least one Super Admin must remain.");
        }

        var delete = await userManager.DeleteAsync(user);
        if (!delete.Succeeded)
        {
            return ToResult(delete);
        }

        logger.LogInformation("Administrator {UserName} deleted", user.UserName);
        return ServiceResult.Ok();
    }

    // Checks that a new account could be created (unique username, password policy) without creating it.
    // Used before creating a tenant so the tenant is not created when its admin account would fail.
    internal async Task<ServiceResult> ValidateNewAccountAsync(string userName, string email, string password, string? passwordField = null)
    {
        var candidate = new ApplicationUser { UserName = userName, Email = email };
        var errors = new List<IdentityError>();
        foreach (var validator in userManager.UserValidators)
        {
            var result = await validator.ValidateAsync(userManager, candidate);
            errors.AddRange(result.Errors);
        }
        foreach (var validator in userManager.PasswordValidators)
        {
            var result = await validator.ValidateAsync(userManager, candidate, password);
            errors.AddRange(result.Errors);
        }
        return errors.Count == 0 ? ServiceResult.Ok() : ToResult(IdentityResult.Failed([.. errors]), passwordField);
    }

    // Creates an account with exactly one role. Returns the new user id.
    internal async Task<ServiceResult<string>> CreateAccountAsync(string userName, string email, string password, string role, int? municipalityId)
    {
        var rule = AdminAccountRules.Validate(role, municipalityId);
        if (rule is not null)
        {
            return ServiceResult<string>.From(ServiceResult.Invalid("MunicipalityId", rule));
        }

        var user = new ApplicationUser
        {
            UserName = userName.Trim(),
            Email = email.Trim(),
            EmailConfirmed = true,
            MunicipalityId = role == AppRoles.SuperAdmin ? null : municipalityId,
        };

        var create = await userManager.CreateAsync(user, password);
        if (!create.Succeeded)
        {
            return ServiceResult<string>.From(ToResult(create));
        }

        var addRole = await userManager.AddToRoleAsync(user, role);
        if (!addRole.Succeeded)
        {
            await userManager.DeleteAsync(user);
            return ServiceResult<string>.From(ToResult(addRole));
        }

        logger.LogInformation("Administrator {UserName} created with role {Role} for municipality {MunicipalityId}",
            user.UserName, role, user.MunicipalityId);
        return user.Id;
    }

    private async Task<ServiceResult?> ValidateAssignmentAsync(string role, int? municipalityId)
    {
        var rule = AdminAccountRules.Validate(role, municipalityId);
        if (rule is not null)
        {
            return ServiceResult.Invalid(nameof(AdminUserAssignmentInput.MunicipalityId), rule);
        }

        if (municipalityId is int id)
        {
            await using var db = await dbFactory.CreateDbContextAsync();
            if (!await db.Municipalities.IgnoreQueryFilters().AnyAsync(m => m.Id == id))
            {
                return ServiceResult.Invalid(nameof(AdminUserAssignmentInput.MunicipalityId), "The selected municipality does not exist.");
            }
        }
        return null;
    }

    private async Task<ServiceResult> SetSingleRoleAsync(ApplicationUser user, string role, IList<string> currentRoles)
    {
        var toRemove = currentRoles.Where(r => r != role).ToList();
        if (toRemove.Count > 0)
        {
            var remove = await userManager.RemoveFromRolesAsync(user, toRemove);
            if (!remove.Succeeded)
            {
                return ToResult(remove);
            }
        }
        if (!currentRoles.Contains(role))
        {
            var add = await userManager.AddToRoleAsync(user, role);
            if (!add.Succeeded)
            {
                return ToResult(add);
            }
        }
        return ServiceResult.Ok();
    }

    private async Task<bool> IsLastSuperAdminAsync(ApplicationUser user) =>
        (await userManager.GetUsersInRoleAsync(AppRoles.SuperAdmin)).All(u => u.Id == user.Id);

    private static IQueryable<AdminUserDto> QueryUsers(MemsDbContext db) =>
        from u in db.Users.AsNoTracking()
        let roleName = (from ur in db.UserRoles
                        join r in db.Roles on ur.RoleId equals r.Id
                        where ur.UserId == u.Id
                        select r.Name).FirstOrDefault()
        select new AdminUserDto(
            u.Id,
            u.UserName!,
            u.Email,
            roleName ?? "(none)",
            u.MunicipalityId,
            u.Municipality != null ? u.Municipality.Name : null);

    private static ServiceResult ToResult(IdentityResult result, string? passwordField = null)
    {
        var errors = result.Errors
            .GroupBy(e => e.Code.StartsWith("Password", StringComparison.Ordinal) && passwordField is not null ? passwordField
                : e.Code.Contains("UserName", StringComparison.Ordinal) ? "UserName"
                : e.Code.Contains("Email", StringComparison.Ordinal) ? "Email"
                : e.Code.StartsWith("Password", StringComparison.Ordinal) ? "Password"
                : string.Empty)
            .ToDictionary(g => g.Key, g => g.Select(e => e.Description).ToArray());
        return ServiceResult.Invalid(errors);
    }
}
