using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MunicipalElections.Data;

namespace MunicipalElections.Controllers;

// Public pages: browse municipalities and the positions (with candidates) on each ballot.
public class MunicipalitiesController(ApplicationDbContext db) : Controller
{
    // GET /Municipalities
    public async Task<IActionResult> Index()
    {
        var municipalities = await db.Municipalities.AsNoTracking()
            .Include(m => m.Positions).ThenInclude(p => p.Candidates)
            .OrderBy(m => m.Name)
            .ToListAsync();

        return View(municipalities);
    }

    // GET /Municipalities/Details/5
    public async Task<IActionResult> Details(int id)
    {
        var municipality = await db.Municipalities.AsNoTracking()
            .Include(m => m.Positions).ThenInclude(p => p.Candidates)
            .FirstOrDefaultAsync(m => m.MunicipalityId == id);

        return municipality is null ? NotFound() : View(municipality);
    }
}
