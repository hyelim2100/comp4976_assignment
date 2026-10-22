using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MunicipalElections.Data;
using MunicipalElections.Dtos;

namespace MunicipalElections.Controllers.Api;

[Route("api/positions")]
public class PositionsController(ApplicationDbContext db) : ApiControllerBase
{
    // GET /api/positions?municipalityId=1
    [HttpGet]
    public async Task<ActionResult<IEnumerable<PositionDto>>> GetAll(int? municipalityId)
    {
        var query = db.Positions.AsNoTracking().Include(p => p.Municipality).AsQueryable();
        if (municipalityId is not null)
        {
            query = query.Where(p => p.MunicipalityId == municipalityId);
        }

        var positions = await query.OrderBy(p => p.Municipality!.Name).ThenBy(p => p.PositionId).ToListAsync();
        return Ok(positions.Select(ToDto));
    }

    // GET /api/positions/{id}
    [HttpGet("{id:int}")]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PositionDto>> Get(int id)
    {
        var position = await db.Positions.AsNoTracking()
            .Include(p => p.Municipality)
            .FirstOrDefaultAsync(p => p.PositionId == id);

        return position is null ? NotFound() : ToDto(position);
    }
}
