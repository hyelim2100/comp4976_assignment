using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.JsonWebTokens;
using MunicipalElections.Data;
using MunicipalElections.Dtos;
using MunicipalElections.Models;

namespace MunicipalElections.Controllers.Api;

// The signed-in mobile user's voting guide.
[Route("api/savedcandidates")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public class SavedCandidatesController(ApplicationDbContext db) : ApiControllerBase
{
    private string UserId => User.FindFirst(JwtRegisteredClaimNames.Sub)!.Value;

    // GET /api/savedcandidates
    [HttpGet]
    public async Task<ActionResult<IEnumerable<SavedCandidateDto>>> GetAll()
    {
        var saved = await WithCandidate()
            .Where(s => s.UserId == UserId)
            .OrderBy(s => s.DateSaved)
            .ToListAsync();

        return Ok(saved.Select(ToDto));
    }

    // POST /api/savedcandidates   { "candidateId": 1 }
    [HttpPost]
    [ProducesResponseType<SavedCandidateDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Save(SaveCandidateRequest request)
    {
        var candidateId = request.CandidateId!.Value;

        if (!await db.Candidates.AnyAsync(c => c.CandidateId == candidateId))
        {
            return Problem(title: "Candidate not found.", statusCode: StatusCodes.Status404NotFound);
        }
        if (await db.SavedCandidates.AnyAsync(s => s.UserId == UserId && s.CandidateId == candidateId))
        {
            return Problem(title: "Candidate is already in the voting guide.", statusCode: StatusCodes.Status409Conflict);
        }

        var saved = new SavedCandidate { UserId = UserId, CandidateId = candidateId };
        db.SavedCandidates.Add(saved);
        await db.SaveChangesAsync();

        var created = await WithCandidate().FirstAsync(s => s.SavedCandidateId == saved.SavedCandidateId);
        return StatusCode(StatusCodes.Status201Created, ToDto(created));
    }

    // DELETE /api/savedcandidates/{id}   (id = savedCandidateId)
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id)
    {
        var saved = await db.SavedCandidates.FirstOrDefaultAsync(s => s.SavedCandidateId == id && s.UserId == UserId);
        if (saved is null)
        {
            return NotFound();
        }

        db.SavedCandidates.Remove(saved);
        await db.SaveChangesAsync();
        return NoContent();
    }

    private IQueryable<SavedCandidate> WithCandidate() =>
        db.SavedCandidates.AsNoTracking()
            .Include(s => s.Candidate!).ThenInclude(c => c.Position!).ThenInclude(p => p.Municipality);

    private SavedCandidateDto ToDto(SavedCandidate s) =>
        new(s.SavedCandidateId, s.CandidateId, ToUtcSeconds(s.DateSaved), ToDto(s.Candidate!));
}
