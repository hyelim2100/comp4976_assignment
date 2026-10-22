using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MunicipalElections.Data;
using MunicipalElections.Dtos;
using MunicipalElections.Models;

namespace MunicipalElections.Controllers.Api;

[Route("api/candidates")]
public class CandidatesController(ApplicationDbContext db) : ApiControllerBase
{
    // GET /api/candidates?name=&municipalityId=&positionId=   (all filters optional)
    [HttpGet]
    public async Task<ActionResult<IEnumerable<CandidateDto>>> GetAll(string? name, int? municipalityId, int? positionId)
    {
        var candidates = await Filter(name, municipalityId, positionId).ToListAsync();
        return Ok(candidates.Select(ToDto));
    }

    // GET /api/candidates/search?name=
    [HttpGet("search")]
    public async Task<ActionResult<IEnumerable<CandidateDto>>> Search(string? name)
    {
        var candidates = await Filter(name, null, null).ToListAsync();
        return Ok(candidates.Select(ToDto));
    }

    // GET /api/candidates/{id}
    [HttpGet("{id:int}")]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CandidateDto>> Get(int id)
    {
        var candidate = await WithPosition().FirstOrDefaultAsync(c => c.CandidateId == id);
        return candidate is null ? NotFound() : ToDto(candidate);
    }

    private IQueryable<Candidate> WithPosition() =>
        db.Candidates.AsNoTracking().Include(c => c.Position!).ThenInclude(p => p.Municipality);

    private IQueryable<Candidate> Filter(string? name, int? municipalityId, int? positionId)
    {
        var query = WithPosition();

        if (!string.IsNullOrWhiteSpace(name))
        {
            // SQLite LIKE is case-insensitive for ASCII letters.
            query = query.Where(c => EF.Functions.Like(c.Name, $"%{name.Trim()}%"));
        }
        if (municipalityId is not null)
        {
            query = query.Where(c => c.Position!.MunicipalityId == municipalityId);
        }
        if (positionId is not null)
        {
            query = query.Where(c => c.PositionId == positionId);
        }

        return query
            .OrderBy(c => c.Position!.Municipality!.Name)
            .ThenBy(c => c.PositionId)
            .ThenBy(c => c.Name);
    }
}
