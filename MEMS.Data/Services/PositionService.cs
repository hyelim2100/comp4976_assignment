using MEMS.Data.Entities;
using MEMS.Data.Tenancy;
using MEMS.Shared.Dtos;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MEMS.Data.Services;

// Positions. All reads go through the tenant query filter; writes take the tenant from ITenantContext
// (a Municipality Admin cannot choose another municipality, whatever the form or request contains).
public class PositionService(
    IDbContextFactory<MemsDbContext> dbFactory,
    ITenantContext tenant,
    ILogger<PositionService> logger)
{
    // municipalityId narrows a Super Admin's list; for a Municipality Admin the filter already does.
    public async Task<IReadOnlyList<PositionDto>> ListAsync(int? municipalityId = null, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var query = db.Positions.AsNoTracking();
        if (municipalityId is int id)
        {
            query = query.Where(p => p.MunicipalityId == id);
        }
        return await Project(query.OrderBy(p => p.Municipality!.Name).ThenBy(p => p.Id)).ToListAsync(cancellationToken);
    }

    public async Task<PositionDto?> GetAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await Project(db.Positions.AsNoTracking().Where(p => p.Id == id)).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<ServiceResult<PositionDto>> CreateAsync(PositionInput input)
    {
        var municipalityId = ResolveTargetMunicipality(input.MunicipalityId, out var failure);
        if (failure is not null)
        {
            return ServiceResult<PositionDto>.From(failure);
        }

        await using var db = await dbFactory.CreateDbContextAsync();
        if (!await db.Municipalities.AnyAsync(m => m.Id == municipalityId))
        {
            return ServiceResult<PositionDto>.From(ServiceResult.Invalid(nameof(input.MunicipalityId), "The selected municipality does not exist."));
        }
        if (await NameTakenAsync(db, municipalityId, input.Name, existingId: null))
        {
            return ServiceResult<PositionDto>.From(ServiceResult.Invalid(nameof(input.Name), "This municipality already has a position with this name."));
        }

        var position = new Position { MunicipalityId = municipalityId };
        Apply(position, input);
        db.Positions.Add(position);
        await db.SaveChangesAsync();

        logger.LogInformation("Position {PositionId} '{Name}' created in municipality {MunicipalityId}", position.Id, position.Name, municipalityId);
        return (await GetAsync(position.Id))!;
    }

    public async Task<ServiceResult> UpdateAsync(int id, PositionInput input)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var position = await db.Positions.FirstOrDefaultAsync(p => p.Id == id);
        if (position is null)
        {
            return ServiceResult.NotFound("The position was not found.");
        }

        // A position stays in its tenant (its candidates are tied to it by the composite key).
        if (tenant.IsSuperAdmin && input.MunicipalityId is int target && target != position.MunicipalityId)
        {
            return ServiceResult.Invalid(nameof(input.MunicipalityId), "A position cannot be moved to another municipality.");
        }
        if (await NameTakenAsync(db, position.MunicipalityId, input.Name, existingId: id))
        {
            return ServiceResult.Invalid(nameof(input.Name), "This municipality already has a position with this name.");
        }

        Apply(position, input);
        await db.SaveChangesAsync();
        logger.LogInformation("Position {PositionId} updated", id);
        return ServiceResult.Ok();
    }

    // Deletes the position together with its candidates and their photos (the confirmation screen
    // shows PositionDto.CandidateCount first).
    public async Task<ServiceResult> DeleteAsync(int id)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var position = await db.Positions.FirstOrDefaultAsync(p => p.Id == id);
        if (position is null)
        {
            return ServiceResult.NotFound("The position was not found.");
        }

        var photoIds = await db.Candidates.Where(c => c.PositionId == id && c.PhotoImageId != null)
            .Select(c => c.PhotoImageId!.Value).ToListAsync();

        var strategy = db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync();
            await db.Candidates.Where(c => c.PositionId == id).ExecuteDeleteAsync();
            await db.Positions.Where(p => p.Id == id).ExecuteDeleteAsync();
            await db.Images.Where(i => photoIds.Contains(i.Id)).ExecuteDeleteAsync();
            await transaction.CommitAsync();
        });

        logger.LogInformation("Position {PositionId} deleted with its candidates and {PhotoCount} photo(s)", id, photoIds.Count);
        return ServiceResult.Ok();
    }

    // Super Admin chooses the municipality; a Municipality Admin always writes to their own tenant.
    private int ResolveTargetMunicipality(int? requested, out ServiceResult? failure)
    {
        failure = null;
        if (tenant.IsSuperAdmin)
        {
            if (requested is int id and > 0)
            {
                return id;
            }
            failure = ServiceResult.Invalid(nameof(PositionInput.MunicipalityId), "Please choose a municipality.");
            return 0;
        }
        if (tenant.MunicipalityId is int own)
        {
            return own;
        }
        failure = ServiceResult.Forbidden();
        return 0;
    }

    private static Task<bool> NameTakenAsync(MemsDbContext db, int municipalityId, string name, int? existingId)
    {
        var trimmed = name.Trim();
        return db.Positions.AnyAsync(p => p.MunicipalityId == municipalityId && p.Name == trimmed && p.Id != existingId);
    }

    private static void Apply(Position position, PositionInput input)
    {
        position.Name = input.Name.Trim();
        position.Description = string.IsNullOrWhiteSpace(input.Description) ? null : input.Description.Trim();
        position.SeatsAvailable = input.SeatsAvailable;
    }

    private static IQueryable<PositionDto> Project(IQueryable<Position> query) =>
        query.Select(p => new PositionDto(
            p.Id, p.Name, p.Description, p.SeatsAvailable, p.MunicipalityId,
            p.Municipality!.Name, p.Municipality.Slug, p.Candidates.Count));
}
