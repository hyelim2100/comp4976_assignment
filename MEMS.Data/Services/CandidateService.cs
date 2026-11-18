using MEMS.Data.Entities;
using MEMS.Data.Tenancy;
using MEMS.Shared.Dtos;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MEMS.Data.Services;

// Candidates. The candidate's tenant always comes from its position, which is looked up through the
// tenant query filter - so a position id from another tenant is simply "not found".
public class CandidateService(
    IDbContextFactory<MemsDbContext> dbFactory,
    ILogger<CandidateService> logger)
{
    public async Task<IReadOnlyList<CandidateDto>> ListAsync(CandidateQuery? query = null, int? municipalityId = null, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var candidates = db.Candidates.AsNoTracking();

        if (municipalityId is int mid)
        {
            candidates = candidates.Where(c => c.MunicipalityId == mid);
        }
        if (query?.PositionId is int pid)
        {
            candidates = candidates.Where(c => c.PositionId == pid);
        }
        if (!string.IsNullOrWhiteSpace(query?.Search))
        {
            var term = query.Search.Trim();
            candidates = candidates.Where(c => c.Name.Contains(term));
        }

        return await Project(candidates
                .OrderBy(c => c.Municipality!.Name)
                .ThenBy(c => c.PositionId)
                .ThenBy(c => c.Name))
            .ToListAsync(cancellationToken);
    }

    public async Task<CandidateDto?> GetAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await Project(db.Candidates.AsNoTracking().Where(c => c.Id == id)).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<ServiceResult<CandidateDto>> CreateAsync(CandidateInput input, ImageUpload? photo)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var position = await db.Positions.AsNoTracking().FirstOrDefaultAsync(p => p.Id == input.PositionId);
        if (position is null)
        {
            return ServiceResult<CandidateDto>.From(ServiceResult.Invalid(nameof(input.PositionId), "Please choose a valid position."));
        }

        var candidate = new Candidate
        {
            PositionId = position.Id,
            MunicipalityId = position.MunicipalityId,
            DateCreated = DateTime.UtcNow,
        };
        Apply(candidate, input);

        if (photo is not null)
        {
            var image = ImageService.Create(photo, position.MunicipalityId, out var error);
            if (error is not null)
            {
                return ServiceResult<CandidateDto>.From(ServiceResult.Invalid("Photo", error));
            }
            db.Images.Add(image!);
            candidate.PhotoImageId = image!.Id;
        }

        db.Candidates.Add(candidate);
        await db.SaveChangesAsync();
        logger.LogInformation("Candidate {CandidateId} created for position {PositionId} in municipality {MunicipalityId}",
            candidate.Id, candidate.PositionId, candidate.MunicipalityId);
        return (await GetAsync(candidate.Id))!;
    }

    public async Task<ServiceResult> UpdateAsync(int id, CandidateInput input, ImageUpload? newPhoto, bool removePhoto)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var candidate = await db.Candidates.FirstOrDefaultAsync(c => c.Id == id);
        if (candidate is null)
        {
            return ServiceResult.NotFound("The candidate was not found.");
        }

        if (input.PositionId != candidate.PositionId)
        {
            var position = await db.Positions.AsNoTracking().FirstOrDefaultAsync(p => p.Id == input.PositionId);
            if (position is null)
            {
                return ServiceResult.Invalid(nameof(input.PositionId), "Please choose a valid position.");
            }
            if (position.MunicipalityId != candidate.MunicipalityId)
            {
                return ServiceResult.Invalid(nameof(input.PositionId), "A candidate can only move to a position in the same municipality.");
            }
            candidate.PositionId = position.Id;
        }

        StoredImage? image = null;
        if (newPhoto is not null)
        {
            image = ImageService.Create(newPhoto, candidate.MunicipalityId, out var error);
            if (error is not null)
            {
                return ServiceResult.Invalid("Photo", error);
            }
        }

        Apply(candidate, input);
        var oldPhotoId = candidate.PhotoImageId;
        if (image is not null)
        {
            db.Images.Add(image);
            candidate.PhotoImageId = image.Id;
        }
        else if (removePhoto)
        {
            candidate.PhotoImageId = null;
        }

        await db.SaveChangesAsync();
        if (oldPhotoId is Guid old && old != candidate.PhotoImageId)
        {
            await db.Images.Where(i => i.Id == old).ExecuteDeleteAsync();
        }

        logger.LogInformation("Candidate {CandidateId} updated", id);
        return ServiceResult.Ok();
    }

    public async Task<ServiceResult> DeleteAsync(int id)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var candidate = await db.Candidates.FirstOrDefaultAsync(c => c.Id == id);
        if (candidate is null)
        {
            return ServiceResult.NotFound("The candidate was not found.");
        }

        var photoId = candidate.PhotoImageId;
        db.Candidates.Remove(candidate);
        await db.SaveChangesAsync();
        if (photoId is Guid pid)
        {
            await db.Images.Where(i => i.Id == pid).ExecuteDeleteAsync();
        }

        logger.LogInformation("Candidate {CandidateId} deleted", id);
        return ServiceResult.Ok();
    }

    private static void Apply(Candidate candidate, CandidateInput input)
    {
        candidate.Name = input.Name.Trim();
        candidate.Profile = string.IsNullOrWhiteSpace(input.Profile) ? null : input.Profile.Trim();
        candidate.Website = string.IsNullOrWhiteSpace(input.Website) ? null : input.Website.Trim();
        candidate.Email = string.IsNullOrWhiteSpace(input.Email) ? null : input.Email.Trim();
    }

    private static IQueryable<CandidateDto> Project(IQueryable<Candidate> query) =>
        query.Select(c => new CandidateDto(
            c.Id, c.Name, c.Profile, c.Website, c.Email, c.PhotoImageId,
            c.PositionId, c.Position!.Name, c.Position.SeatsAvailable,
            c.MunicipalityId, c.Municipality!.Name, c.Municipality.Slug,
            c.DateCreated));
}
