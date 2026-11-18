using MEMS.Api.Infrastructure;
using MEMS.Data.Services;
using MEMS.Data.Tenancy;
using MEMS.Shared.Dtos;
using MEMS.Shared.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MEMS.Api.Controllers;

/// <summary>The directory of all municipalities (tenants) and tenant creation.</summary>
/// <remarks>These routes are not tenant-scoped: the public directory lists every tenant.</remarks>
[Route("api/municipalities")]
public class MunicipalitiesController(MunicipalityService municipalities) : ApiControllerBase
{
    /// <summary>Lists every municipality (public "Browse municipalities").</summary>
    /// <response code="200">All municipalities, ordered by name.</response>
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType<IReadOnlyList<MunicipalitySummaryDto>>(StatusCodes.Status200OK)]
    public async Task<IReadOnlyList<MunicipalitySummaryDto>> GetAll(CancellationToken cancellationToken) =>
        await municipalities.GetDirectoryAsync(cancellationToken);

    /// <summary>Gets one municipality summary by id.</summary>
    /// <response code="200">The municipality.</response>
    /// <response code="404">No municipality has this id.</response>
    [HttpGet("{id:int}")]
    [AllowAnonymous]
    [ProducesResponseType<MunicipalitySummaryDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MunicipalitySummaryDto>> GetById(int id, CancellationToken cancellationToken)
    {
        var municipality = (await municipalities.GetDirectoryAsync(cancellationToken)).FirstOrDefault(m => m.Id == id);
        return municipality is null ? NotFoundProblem($"There is no municipality with id {id}.") : municipality;
    }

    /// <summary>Creates a tenant together with its first Municipality Admin account (Super Admin only).</summary>
    /// <response code="201">Created; Location points to /api/{slug}/municipality.</response>
    /// <response code="400">Validation failed (e.g. slug already used, weak password).</response>
    /// <response code="401">Missing or invalid token.</response>
    /// <response code="403">The caller is not a Super Admin.</response>
    [HttpPost]
    [Authorize(Roles = AppRoles.SuperAdmin)]
    [ProducesResponseType<MunicipalityDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<MunicipalityDto>> Create(TenantCreateInput input)
    {
        var result = await municipalities.CreateTenantAsync(input, logo: null);
        if (!result.Succeeded)
        {
            return FromFailure(result);
        }
        return CreatedAtAction(nameof(TenantMunicipalityController.Get), "TenantMunicipality",
            new { tenant = result.Value!.Slug }, result.Value);
    }
}

/// <summary>The municipality of the tenant in the URL.</summary>
[Route("api/{tenant}/municipality")]
public class TenantMunicipalityController(MunicipalityService municipalities, ITenantContext tenantContext) : ApiControllerBase
{
    private int MunicipalityId => tenantContext.MunicipalityId!.Value;

    /// <summary>Gets the municipality of this tenant (home page data: logo, description, election date, colour).</summary>
    /// <param name="tenant">Tenant slug, case-insensitive (e.g. burnaby, Richmond).</param>
    /// <response code="200">The municipality.</response>
    /// <response code="403">Signed in as an administrator of another municipality.</response>
    /// <response code="404">Unknown tenant.</response>
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType<MunicipalityDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MunicipalityDto>> Get(string tenant, CancellationToken cancellationToken) =>
        await municipalities.GetAsync(MunicipalityId, cancellationToken) is { } municipality
            ? municipality
            : NotFoundProblem("The municipality was not found.");

    /// <summary>Updates the municipality. Only a Super Admin may change the slug.</summary>
    /// <response code="204">Updated.</response>
    /// <response code="400">Validation failed.</response>
    /// <response code="401">Missing or invalid token.</response>
    /// <response code="403">Administrator of another municipality.</response>
    /// <response code="404">Unknown tenant.</response>
    [HttpPut]
    [Authorize(Roles = AppRoles.SuperAdmin + "," + AppRoles.MunicipalityAdmin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> Update(string tenant, MunicipalityInput input)
    {
        var result = await municipalities.UpdateAsync(MunicipalityId, input, newLogo: null, removeLogo: false);
        return result.Succeeded ? NoContent() : FromFailure(result);
    }

    /// <summary>Uploads or replaces the municipality logo (multipart/form-data, field "file").</summary>
    /// <remarks>JPG, PNG, GIF or WEBP up to 2 MB; the file content must be a real image.</remarks>
    /// <response code="204">Logo saved.</response>
    /// <response code="400">Not an acceptable image.</response>
    [HttpPut("logo")]
    [Authorize(Roles = AppRoles.SuperAdmin + "," + AppRoles.MunicipalityAdmin)]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(3 * 1024 * 1024)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> UploadLogo(string tenant, IFormFile? file)
    {
        var upload = await ReadImageAsync(file, "file");
        if (upload is null)
        {
            return ValidationProblem(ModelState);
        }

        var current = await municipalities.GetAsync(MunicipalityId);
        if (current is null)
        {
            return NotFoundProblem("The municipality was not found.");
        }
        var result = await municipalities.UpdateAsync(MunicipalityId, ToInput(current), upload, removeLogo: false);
        return result.Succeeded ? NoContent() : FromFailure(result);
    }

    /// <summary>Shows what deleting this tenant would remove (Super Admin only).</summary>
    /// <response code="200">Counts of positions, candidates, images and the admin accounts.</response>
    [HttpGet("deletion-impact")]
    [Authorize(Roles = AppRoles.SuperAdmin)]
    [ProducesResponseType<TenantDeletionImpactDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<TenantDeletionImpactDto>> GetDeletionImpact(string tenant)
    {
        var result = await municipalities.GetDeletionImpactAsync(MunicipalityId);
        return result.Succeeded ? result.Value! : FromFailure(result);
    }

    /// <summary>Deletes the tenant with all its positions, candidates, images and admin accounts (Super Admin only).</summary>
    /// <response code="204">Deleted.</response>
    /// <response code="401">Missing or invalid token.</response>
    /// <response code="403">The caller is not a Super Admin.</response>
    /// <response code="404">Unknown tenant.</response>
    [HttpDelete]
    [Authorize(Roles = AppRoles.SuperAdmin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> Delete(string tenant)
    {
        var result = await municipalities.DeleteAsync(MunicipalityId);
        return result.Succeeded ? NoContent() : FromFailure(result);
    }

    private static MunicipalityInput ToInput(MunicipalityDto m) => new()
    {
        Name = m.Name, Slug = m.Slug, Province = m.Province, ElectionDate = m.ElectionDate,
        Tagline = m.Tagline, Description = m.Description, PrimaryColor = m.PrimaryColor,
    };
}
