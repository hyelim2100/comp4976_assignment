using MEMS.Api.Infrastructure;
using MEMS.Data.Services;
using MEMS.Shared.Dtos;
using MEMS.Shared.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MEMS.Api.Controllers;

/// <summary>Candidates of the tenant in the URL.</summary>
[Route("api/{tenant}/candidates")]
public class CandidatesController(CandidateService candidates) : ApiControllerBase
{
    /// <summary>Lists the tenant's candidates, optionally searched by name and filtered by position.</summary>
    /// <param name="tenant">Tenant slug, case-insensitive.</param>
    /// <param name="search">Part of the candidate's name.</param>
    /// <param name="positionId">Only candidates for this position.</param>
    /// <response code="200">Candidates of this tenant only.</response>
    /// <response code="404">Unknown tenant.</response>
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType<IReadOnlyList<CandidateDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IReadOnlyList<CandidateDto>> GetAll(string tenant, [FromQuery] string? search, [FromQuery] int? positionId, CancellationToken cancellationToken) =>
        await candidates.ListAsync(new CandidateQuery(search, positionId), cancellationToken: cancellationToken);

    /// <summary>Gets one candidate of the tenant.</summary>
    /// <response code="200">The candidate.</response>
    /// <response code="404">Unknown tenant, or the id belongs to another tenant.</response>
    [HttpGet("{id:int}")]
    [AllowAnonymous]
    [ProducesResponseType<CandidateDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CandidateDto>> GetById(string tenant, int id, CancellationToken cancellationToken) =>
        await candidates.GetAsync(id, cancellationToken) is { } candidate ? candidate : NotFoundProblem("The candidate was not found.");

    /// <summary>Creates a candidate. The position must belong to this tenant.</summary>
    /// <response code="201">Created.</response>
    /// <response code="400">Validation failed (e.g. invalid email/URL, position of another tenant).</response>
    /// <response code="401">Missing or invalid token.</response>
    /// <response code="403">Administrator of another municipality.</response>
    [HttpPost]
    [Authorize(Roles = AppRoles.SuperAdmin + "," + AppRoles.MunicipalityAdmin)]
    [ProducesResponseType<CandidateDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<CandidateDto>> Create(string tenant, CandidateInput input)
    {
        var result = await candidates.CreateAsync(input, photo: null);
        return result.Succeeded
            ? CreatedAtAction(nameof(GetById), new { tenant, id = result.Value!.Id }, result.Value)
            : FromFailure(result);
    }

    /// <summary>Updates a candidate of this tenant.</summary>
    /// <response code="204">Updated.</response>
    /// <response code="400">Validation failed.</response>
    /// <response code="403">Administrator of another municipality.</response>
    /// <response code="404">The id does not exist in this tenant.</response>
    [HttpPut("{id:int}")]
    [Authorize(Roles = AppRoles.SuperAdmin + "," + AppRoles.MunicipalityAdmin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> Update(string tenant, int id, CandidateInput input)
    {
        var result = await candidates.UpdateAsync(id, input, newPhoto: null, removePhoto: false);
        return result.Succeeded ? NoContent() : FromFailure(result);
    }

    /// <summary>Uploads or replaces the candidate photo (multipart/form-data, field "file").</summary>
    /// <remarks>JPG, PNG, GIF or WEBP up to 2 MB; the file content must be a real image.</remarks>
    /// <response code="204">Photo saved.</response>
    /// <response code="400">Not an acceptable image.</response>
    /// <response code="404">The id does not exist in this tenant.</response>
    [HttpPut("{id:int}/photo")]
    [Authorize(Roles = AppRoles.SuperAdmin + "," + AppRoles.MunicipalityAdmin)]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(3 * 1024 * 1024)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> UploadPhoto(string tenant, int id, IFormFile? file)
    {
        var current = await candidates.GetAsync(id);
        if (current is null)
        {
            return NotFoundProblem("The candidate was not found.");
        }

        var upload = await ReadImageAsync(file, "file");
        if (upload is null)
        {
            return ValidationProblem(ModelState);
        }

        var input = new CandidateInput
        {
            Name = current.Name, Profile = current.Profile, Website = current.Website, Email = current.Email, PositionId = current.PositionId,
        };
        var result = await candidates.UpdateAsync(id, input, upload, removePhoto: false);
        return result.Succeeded ? NoContent() : FromFailure(result);
    }

    /// <summary>Deletes a candidate (and its photo).</summary>
    /// <response code="204">Deleted.</response>
    /// <response code="403">Administrator of another municipality.</response>
    /// <response code="404">The id does not exist in this tenant.</response>
    [HttpDelete("{id:int}")]
    [Authorize(Roles = AppRoles.SuperAdmin + "," + AppRoles.MunicipalityAdmin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> Delete(string tenant, int id)
    {
        var result = await candidates.DeleteAsync(id);
        return result.Succeeded ? NoContent() : FromFailure(result);
    }
}
