using MEMS.Api.Infrastructure;
using MEMS.Data.Services;
using MEMS.Shared.Dtos;
using MEMS.Shared.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MEMS.Api.Controllers;

/// <summary>Positions (Mayor, Councillor, ...) of the tenant in the URL.</summary>
[Route("api/{tenant}/positions")]
public class PositionsController(PositionService positions) : ApiControllerBase
{
    /// <summary>Lists the tenant's positions.</summary>
    /// <param name="tenant">Tenant slug, case-insensitive.</param>
    /// <response code="200">Positions of this tenant only.</response>
    /// <response code="404">Unknown tenant.</response>
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType<IReadOnlyList<PositionDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IReadOnlyList<PositionDto>> GetAll(string tenant, CancellationToken cancellationToken) =>
        await positions.ListAsync(cancellationToken: cancellationToken);

    /// <summary>Gets one position of the tenant.</summary>
    /// <response code="200">The position.</response>
    /// <response code="404">Unknown tenant, or the id belongs to another tenant.</response>
    [HttpGet("{id:int}")]
    [AllowAnonymous]
    [ProducesResponseType<PositionDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PositionDto>> GetById(string tenant, int id, CancellationToken cancellationToken) =>
        await positions.GetAsync(id, cancellationToken) is { } position ? position : NotFoundProblem("The position was not found.");

    /// <summary>Creates a position in this tenant (MunicipalityId in the body is ignored).</summary>
    /// <response code="201">Created.</response>
    /// <response code="400">Validation failed (e.g. duplicate name, seats out of 1-100).</response>
    /// <response code="401">Missing or invalid token.</response>
    /// <response code="403">Administrator of another municipality.</response>
    [HttpPost]
    [Authorize(Roles = AppRoles.SuperAdmin + "," + AppRoles.MunicipalityAdmin)]
    [ProducesResponseType<PositionDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<PositionDto>> Create(string tenant, PositionInput input)
    {
        var result = await positions.CreateAsync(input);
        return result.Succeeded
            ? CreatedAtAction(nameof(GetById), new { tenant, id = result.Value!.Id }, result.Value)
            : FromFailure(result);
    }

    /// <summary>Updates a position of this tenant.</summary>
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
    public async Task<ActionResult> Update(string tenant, int id, PositionInput input)
    {
        var result = await positions.UpdateAsync(id, input);
        return result.Succeeded ? NoContent() : FromFailure(result);
    }

    /// <summary>Deletes a position together with its candidates.</summary>
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
        var result = await positions.DeleteAsync(id);
        return result.Succeeded ? NoContent() : FromFailure(result);
    }
}
