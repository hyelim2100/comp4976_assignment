using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MunicipalElections.Data;
using MunicipalElections.Dtos;

namespace MunicipalElections.Controllers.Api;

[Route("api/municipalities")]
public class MunicipalitiesController(ApplicationDbContext db) : ApiControllerBase
{
    // GET /api/municipalities
    [HttpGet]
    public async Task<ActionResult<IEnumerable<MunicipalityDto>>> GetAll()
    {
        var municipalities = await db.Municipalities.AsNoTracking().OrderBy(m => m.Name).ToListAsync();
        return Ok(municipalities.Select(ToDto));
    }

    // GET /api/municipalities/{id}
    [HttpGet("{id:int}")]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MunicipalityDetailDto>> Get(int id)
    {
        var m = await db.Municipalities.AsNoTracking()
            .Include(m => m.Positions)
            .FirstOrDefaultAsync(m => m.MunicipalityId == id);
        if (m is null)
        {
            return NotFound();
        }

        var positions = m.Positions
            .OrderBy(p => p.PositionId)
            .Select(p => new PositionDto(p.PositionId, p.Name, p.SeatsAvailable, m.MunicipalityId, m.Name))
            .ToList();

        return new MunicipalityDetailDto(m.MunicipalityId, m.Name, m.Province, DateOnly.FromDateTime(m.ElectionDate),
            m.Description, ImageUrl(m.LogoFileName), positions);
    }
}
