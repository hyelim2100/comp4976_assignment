using MEMS.Data.Entities;
using MEMS.Data.Tenancy;
using MEMS.Shared.Dtos;
using MEMS.Shared.Security;
using MEMS.Shared.Validation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MEMS.Data.Services;

// Municipalities (tenants).
// * Super Admin: create (with the tenant's first admin account), edit, delete, change slug.
// * Municipality Admin: view and edit their own municipality only (not its slug); the global query
//   filter hides every other tenant.
public class MunicipalityService(
    IDbContextFactory<MemsDbContext> dbFactory,
    ITenantContext tenant,
    AdminUserService adminUsers,
    ILogger<MunicipalityService> logger)
{
    // Public "Browse municipalities" directory. Deliberately tenant-agnostic and read-only: it only
    // returns the summary that every tenant shows publicly anyway.
    public async Task<IReadOnlyList<MunicipalitySummaryDto>> GetDirectoryAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.Municipalities.IgnoreQueryFilters().AsNoTracking()
            .OrderBy(m => m.Name)
            .Select(m => new MunicipalitySummaryDto(m.Id, m.Name, m.Slug, m.Province, m.ElectionDate, m.Tagline, m.PrimaryColor, m.LogoImageId))
            .ToListAsync(cancellationToken);
    }

    // Resolves a URL slug to a tenant id, case-insensitively (/Richmond -> richmond). Null if unknown.
    public async Task<int?> ResolveSlugAsync(string? slug, CancellationToken cancellationToken = default)
    {
        var normalized = SlugRules.Normalize(slug);
        if (normalized.Length == 0 || normalized.Length > FieldLengths.Slug)
        {
            return null;
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.Municipalities.IgnoreQueryFilters()
            .Where(m => m.Slug == normalized)
            .Select(m => (int?)m.Id)
            .FirstOrDefaultAsync(cancellationToken);
    }

    // Tenant management list: every municipality for a Super Admin (whatever tenant they are working
    // in), only their own for a Municipality Admin.
    public async Task<IReadOnlyList<MunicipalityDto>> ListAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await Project(Managed(db).AsNoTracking().OrderBy(m => m.Name)).ToListAsync(cancellationToken);
    }

    public async Task<MunicipalityDto?> GetAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await Project(Managed(db).AsNoTracking().Where(m => m.Id == id)).FirstOrDefaultAsync(cancellationToken);
    }

    // Municipalities the caller may manage. The Super Admin role "manages all tenants", so tenant
    // management ignores the Super Admin's selected working tenant (IgnoreQueryFilters applies to the
    // whole query, so the position/candidate counts are complete too). Everyone else gets the tenant
    // query filter: their own municipality only, with counts of their own rows.
    private IQueryable<Municipality> Managed(MemsDbContext db) =>
        tenant.IsSuperAdmin ? db.Municipalities.IgnoreQueryFilters() : db.Municipalities;

    // Creates a tenant and its first Municipality Admin account.
    public async Task<ServiceResult<MunicipalityDto>> CreateTenantAsync(TenantCreateInput input, ImageUpload? logo)
    {
        if (!tenant.IsSuperAdmin)
        {
            return ServiceResult<MunicipalityDto>.From(ServiceResult.Forbidden("Only a Super Admin can create municipalities."));
        }

        await using var db = await dbFactory.CreateDbContextAsync();

        var errors = await ValidateUniqueAsync(db, input, existingId: null);
        var account = await adminUsers.ValidateNewAccountAsync(input.AdminUserName, input.AdminEmail, input.AdminPassword, nameof(input.AdminPassword));
        foreach (var (field, messages) in account.Errors)
        {
            var key = field switch
            {
                "UserName" => nameof(input.AdminUserName),
                "Email" => nameof(input.AdminEmail),
                "" => string.Empty,
                _ => field,
            };
            errors[key] = messages;
        }
        if (input.AdminPassword != input.ConfirmAdminPassword)
        {
            errors[nameof(input.ConfirmAdminPassword)] = ["The passwords do not match."];
        }
        if (logo is not null && ImageRules.Validate(logo.FileName, logo.Content) is { } logoError)
        {
            errors["Logo"] = [logoError];
        }
        if (errors.Count > 0)
        {
            return ServiceResult<MunicipalityDto>.From(ServiceResult.Invalid(errors));
        }

        var municipality = new Municipality();
        Apply(municipality, input, allowSlugChange: true);
        db.Municipalities.Add(municipality);
        await db.SaveChangesAsync();

        if (logo is not null)
        {
            var image = ImageService.Create(logo, municipality.Id, out _)!;
            db.Images.Add(image);
            municipality.LogoImageId = image.Id;
            await db.SaveChangesAsync();
        }

        // Identity uses its own context, so if the account cannot be created (e.g. a race on the
        // username) the new tenant is removed again rather than left without an administrator.
        var created = await adminUsers.CreateAccountAsync(input.AdminUserName, input.AdminEmail, input.AdminPassword, AppRoles.MunicipalityAdmin, municipality.Id);
        if (!created.Succeeded)
        {
            await DeleteTenantDataAsync(db, municipality.Id);
            return ServiceResult<MunicipalityDto>.From(created);
        }

        logger.LogInformation("Tenant {Slug} (id {MunicipalityId}) created with administrator {UserName}",
            municipality.Slug, municipality.Id, input.AdminUserName);
        return (await GetAsync(municipality.Id))!;
    }

    public async Task<ServiceResult> UpdateAsync(int id, MunicipalityInput input, ImageUpload? newLogo, bool removeLogo)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var municipality = await Managed(db).FirstOrDefaultAsync(m => m.Id == id);
        if (municipality is null)
        {
            // Also the answer for another tenant's municipality: it is filtered out.
            return ServiceResult.NotFound("The municipality was not found.");
        }

        var slugChanged = SlugRules.Normalize(input.Slug) != municipality.Slug;
        if (slugChanged && !tenant.IsSuperAdmin)
        {
            return ServiceResult.Invalid(nameof(input.Slug), "Only a Super Admin can change the tenant slug.");
        }

        var errors = await ValidateUniqueAsync(db, input, existingId: id);
        StoredImage? image = null;
        if (newLogo is not null)
        {
            image = ImageService.Create(newLogo, municipality.Id, out var logoError);
            if (logoError is not null)
            {
                errors["Logo"] = [logoError];
            }
        }
        if (errors.Count > 0)
        {
            return ServiceResult.Invalid(errors);
        }

        Apply(municipality, input, allowSlugChange: tenant.IsSuperAdmin);

        var oldLogoId = municipality.LogoImageId;
        if (image is not null)
        {
            db.Images.Add(image);
            municipality.LogoImageId = image.Id;
        }
        else if (removeLogo)
        {
            municipality.LogoImageId = null;
        }
        if (oldLogoId is Guid old && old != municipality.LogoImageId)
        {
            await db.SaveChangesAsync();
            await db.Images.Where(i => i.Id == old).ExecuteDeleteAsync();
        }

        await db.SaveChangesAsync();
        logger.LogInformation("Municipality {MunicipalityId} updated", id);
        return ServiceResult.Ok();
    }

    // Shown on the delete confirmation screen: everything that is removed with the tenant.
    public async Task<ServiceResult<TenantDeletionImpactDto>> GetDeletionImpactAsync(int id)
    {
        if (!tenant.IsSuperAdmin)
        {
            return ServiceResult<TenantDeletionImpactDto>.From(ServiceResult.Forbidden("Only a Super Admin can delete municipalities."));
        }

        await using var db = await dbFactory.CreateDbContextAsync();
        var impact = await Managed(db).AsNoTracking()
            .Where(m => m.Id == id)
            .Select(m => new TenantDeletionImpactDto(
                m.Id,
                m.Name,
                m.Slug,
                db.Positions.IgnoreQueryFilters().Count(p => p.MunicipalityId == m.Id),
                db.Candidates.IgnoreQueryFilters().Count(c => c.MunicipalityId == m.Id),
                db.Images.IgnoreQueryFilters().Count(i => i.MunicipalityId == m.Id),
                m.Users.OrderBy(u => u.UserName).Select(u => u.UserName!).ToList()))
            .FirstOrDefaultAsync();

        return impact is null
            ? ServiceResult<TenantDeletionImpactDto>.From(ServiceResult.NotFound("The municipality was not found."))
            : impact;
    }

    // Deletes a tenant with all of its data: candidates, positions, images and administrator accounts.
    // Runs in one transaction so a failure never leaves half a tenant behind.
    public async Task<ServiceResult> DeleteAsync(int id)
    {
        if (!tenant.IsSuperAdmin)
        {
            return ServiceResult.Forbidden("Only a Super Admin can delete municipalities.");
        }

        await using var db = await dbFactory.CreateDbContextAsync();
        if (!await Managed(db).AnyAsync(m => m.Id == id))
        {
            return ServiceResult.NotFound("The municipality was not found.");
        }

        await DeleteTenantDataAsync(db, id);
        logger.LogWarning("Tenant {MunicipalityId} deleted with all positions, candidates, images and administrator accounts", id);
        return ServiceResult.Ok();
    }

    private static async Task DeleteTenantDataAsync(MemsDbContext db, int id)
    {
        var strategy = db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync();

            // Only reached by a Super Admin (checked by the callers); filters are ignored so the deletion is
            // complete whatever tenant the Super Admin is currently working in.
            await db.Candidates.IgnoreQueryFilters().Where(c => c.MunicipalityId == id).ExecuteDeleteAsync();
            await db.Positions.IgnoreQueryFilters().Where(p => p.MunicipalityId == id).ExecuteDeleteAsync();
            await db.Municipalities.IgnoreQueryFilters().Where(m => m.Id == id).ExecuteUpdateAsync(s => s.SetProperty(m => m.LogoImageId, (Guid?)null));
            await db.Images.IgnoreQueryFilters().Where(i => i.MunicipalityId == id).ExecuteDeleteAsync();
            // Identity's user roles, claims, logins and tokens cascade with the user rows.
            await db.Users.Where(u => u.MunicipalityId == id).ExecuteDeleteAsync();
            await db.Municipalities.IgnoreQueryFilters().Where(m => m.Id == id).ExecuteDeleteAsync();

            await transaction.CommitAsync();
        });
    }

    private static async Task<Dictionary<string, string[]>> ValidateUniqueAsync(MemsDbContext db, MunicipalityInput input, int? existingId)
    {
        var errors = new Dictionary<string, string[]>();
        var slug = SlugRules.Normalize(input.Slug);
        var name = input.Name.Trim();

        if (!SlugRules.IsValid(slug))
        {
            errors[nameof(input.Slug)] = [SlugRules.Reserved.Contains(slug) ? "This slug is reserved by the application. Please choose another one." : SlugRules.PatternMessage];
        }
        // Uniqueness is checked across all tenants (the unique indexes enforce it in the database too).
        else if (await db.Municipalities.IgnoreQueryFilters().AnyAsync(m => m.Slug == slug && m.Id != existingId))
        {
            errors[nameof(input.Slug)] = ["Another municipality already uses this slug."];
        }
        if (await db.Municipalities.IgnoreQueryFilters().AnyAsync(m => m.Name == name && m.Id != existingId))
        {
            errors[nameof(input.Name)] = ["Another municipality already has this name."];
        }
        if (input.ElectionDate is null)
        {
            errors[nameof(input.ElectionDate)] = ["The Election Date field is required."];
        }
        return errors;
    }

    private static void Apply(Municipality municipality, MunicipalityInput input, bool allowSlugChange)
    {
        municipality.Name = input.Name.Trim();
        if (allowSlugChange)
        {
            municipality.Slug = SlugRules.Normalize(input.Slug);
        }
        municipality.Province = input.Province.Trim();
        municipality.ElectionDate = input.ElectionDate!.Value;
        municipality.Tagline = string.IsNullOrWhiteSpace(input.Tagline) ? null : input.Tagline.Trim();
        municipality.Description = string.IsNullOrWhiteSpace(input.Description) ? null : input.Description.Trim();
        municipality.PrimaryColor = input.PrimaryColor.ToUpperInvariant();
    }

    // Counts use the navigation collections; EF applies the Position/Candidate query filters to them,
    // so a Municipality Admin only ever counts rows of their own tenant.
    private static IQueryable<MunicipalityDto> Project(IQueryable<Municipality> query) =>
        query.Select(m => new MunicipalityDto(
            m.Id, m.Name, m.Slug, m.Province, m.ElectionDate, m.Tagline, m.Description, m.PrimaryColor, m.LogoImageId,
            m.Positions.Count, m.Candidates.Count));
}
