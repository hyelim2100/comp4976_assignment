using MEMS.Data.Entities;
using MEMS.Shared.Validation;
using Microsoft.EntityFrameworkCore;

namespace MEMS.Data.Services;

public sealed record ImageContent(byte[] Data, string ContentType);

// Reads images for display. Writes happen inside the municipality/candidate services so the image and
// the row that references it are saved together.
public class ImageService(IDbContextFactory<MemsDbContext> dbFactory)
{
    // Tenant-filtered: an image is only returned within the scope's tenant. The Admin app passes
    // includeAllTenantsForSuperAdmin so a Super Admin working in one tenant still sees every tenant's
    // logo on the tenant-management pages; it has no effect for anyone else.
    public Task<ImageContent?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
        GetAsync(id, includeAllTenantsForSuperAdmin: false, cancellationToken);

    public async Task<ImageContent?> GetAsync(Guid id, bool includeAllTenantsForSuperAdmin, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var images = includeAllTenantsForSuperAdmin && db.Tenant.IsSuperAdmin ? db.Images.IgnoreQueryFilters() : db.Images;
        return await images.AsNoTracking()
            .Where(i => i.Id == id)
            .Select(i => new ImageContent(i.Data, i.ContentType))
            .FirstOrDefaultAsync(cancellationToken);
    }

    // Validates an upload (extension, size, real signature) and creates the entity, or returns an error.
    internal static StoredImage? Create(ImageUpload upload, int municipalityId, out string? error)
    {
        error = ImageRules.Validate(upload.FileName, upload.Content);
        if (error is not null)
        {
            return null;
        }

        return new StoredImage
        {
            ContentType = ImageRules.ContentTypeFor(upload.FileName),
            FileName = Path.GetFileName(upload.FileName) is { Length: <= FieldLengths.FileName } name ? name : null,
            Data = upload.Content,
            MunicipalityId = municipalityId,
        };
    }
}
