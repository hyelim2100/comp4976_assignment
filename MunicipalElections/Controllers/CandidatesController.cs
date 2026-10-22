using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using MunicipalElections.Data;
using MunicipalElections.Models;

namespace MunicipalElections.Controllers;

// Public candidate directory: search by name, filter by municipality and position, view profiles.
public class CandidatesController(ApplicationDbContext db) : Controller
{
    // GET /Candidates?search=&municipalityId=&positionId=
    public async Task<IActionResult> Index(string? search, int? municipalityId, int? positionId)
    {
        var query = WithPosition();

        if (!string.IsNullOrWhiteSpace(search))
        {
            // SQLite LIKE is case-insensitive for ASCII letters.
            query = query.Where(c => EF.Functions.Like(c.Name, $"%{search.Trim()}%"));
        }
        if (municipalityId is not null)
        {
            query = query.Where(c => c.Position!.MunicipalityId == municipalityId);
        }
        if (positionId is not null)
        {
            query = query.Where(c => c.PositionId == positionId);
        }

        var municipalities = await db.Municipalities.AsNoTracking().OrderBy(m => m.Name).ToListAsync();
        var positions = await db.Positions.AsNoTracking()
            .OrderBy(p => p.PositionId)
            .Select(p => new PositionOption(p.PositionId, p.Name, p.MunicipalityId))
            .ToListAsync();

        return View(new CandidateSearchViewModel
        {
            Search = search,
            MunicipalityId = municipalityId,
            PositionId = positionId,
            Candidates = await query
                .OrderBy(c => c.Position!.Municipality!.Name).ThenBy(c => c.PositionId).ThenBy(c => c.Name)
                .ToListAsync(),
            MunicipalityOptions = new SelectList(municipalities, nameof(Municipality.MunicipalityId), nameof(Municipality.Name), municipalityId),
            PositionOptions = positions,
        });
    }

    // GET /Candidates/Details/5
    public async Task<IActionResult> Details(int id)
    {
        var candidate = await WithPosition().FirstOrDefaultAsync(c => c.CandidateId == id);
        if (candidate is null)
        {
            return NotFound();
        }

        ViewData["OtherCandidates"] = await WithPosition()
            .Where(c => c.PositionId == candidate.PositionId && c.CandidateId != id)
            .OrderBy(c => c.Name)
            .ToListAsync();
        return View(candidate);
    }

    private IQueryable<Candidate> WithPosition() =>
        db.Candidates.AsNoTracking().Include(c => c.Position!).ThenInclude(p => p.Municipality);
}
