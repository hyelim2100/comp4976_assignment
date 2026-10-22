using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using MunicipalElections.Data;
using MunicipalElections.Models;
using MunicipalElections.Services;

namespace MunicipalElections.Areas.Admin.Controllers;

public class PositionsController(ApplicationDbContext db, MunicipalityAccess access, ImageStorage images) : AdminControllerBase
{
    // GET /Admin/Positions?municipalityId=1
    public async Task<IActionResult> Index(int? municipalityId)
    {
        var ids = await access.GetManagedMunicipalityIdsAsync();
        var positions = await db.Positions.AsNoTracking()
            .Include(p => p.Municipality)
            .Include(p => p.Candidates)
            .Where(p => ids == null || ids.Contains(p.MunicipalityId))
            .Where(p => municipalityId == null || p.MunicipalityId == municipalityId)
            .OrderBy(p => p.Municipality!.Name).ThenBy(p => p.PositionId)
            .ToListAsync();

        await PopulateMunicipalitiesAsync(municipalityId);
        ViewData["MunicipalityFilter"] = municipalityId;
        return View(positions);
    }

    // GET /Admin/Positions/Details/5
    public async Task<IActionResult> Details(int id)
    {
        var position = await db.Positions.AsNoTracking()
            .Include(p => p.Municipality)
            .Include(p => p.Candidates)
            .FirstOrDefaultAsync(p => p.PositionId == id);
        if (position is null)
        {
            return NotFound();
        }
        if (!await access.CanManageAsync(position.MunicipalityId))
        {
            return Forbid();
        }

        return View(position);
    }

    // GET /Admin/Positions/Create?municipalityId=1
    public async Task<IActionResult> Create(int? municipalityId)
    {
        var managed = await PopulateMunicipalitiesAsync(municipalityId);
        return View(new Position
        {
            MunicipalityId = municipalityId ?? (managed.Count == 1 ? managed[0] : 0),
            SeatsAvailable = 1,
        });
    }

    // POST /Admin/Positions/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("Name,SeatsAvailable,MunicipalityId")] Position position)
    {
        await ValidateAsync(position);
        if (!ModelState.IsValid)
        {
            await PopulateMunicipalitiesAsync(position.MunicipalityId);
            return View(position);
        }

        position.Name = position.Name.Trim();
        db.Positions.Add(position);
        await db.SaveChangesAsync();

        SetStatusMessage($"Position \"{position.Name}\" was created.");
        return RedirectToAction(nameof(Index), new { municipalityId = position.MunicipalityId });
    }

    // GET /Admin/Positions/Edit/5
    public async Task<IActionResult> Edit(int id)
    {
        var position = await db.Positions.FindAsync(id);
        if (position is null)
        {
            return NotFound();
        }
        if (!await access.CanManageAsync(position.MunicipalityId))
        {
            return Forbid();
        }

        await PopulateMunicipalitiesAsync(position.MunicipalityId);
        return View(position);
    }

    // POST /Admin/Positions/Edit/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, [Bind("PositionId,Name,SeatsAvailable,MunicipalityId")] Position input)
    {
        if (id != input.PositionId)
        {
            return NotFound();
        }

        var position = await db.Positions.FindAsync(id);
        if (position is null)
        {
            return NotFound();
        }
        if (!await access.CanManageAsync(position.MunicipalityId))
        {
            return Forbid();
        }

        await ValidateAsync(input);
        if (!ModelState.IsValid)
        {
            await PopulateMunicipalitiesAsync(input.MunicipalityId);
            return View(input);
        }

        position.Name = input.Name.Trim();
        position.SeatsAvailable = input.SeatsAvailable;
        position.MunicipalityId = input.MunicipalityId;
        await db.SaveChangesAsync();

        SetStatusMessage($"Position \"{position.Name}\" was updated.");
        return RedirectToAction(nameof(Index), new { municipalityId = position.MunicipalityId });
    }

    // GET /Admin/Positions/Delete/5
    public async Task<IActionResult> Delete(int id)
    {
        var position = await db.Positions.AsNoTracking()
            .Include(p => p.Municipality)
            .Include(p => p.Candidates)
            .FirstOrDefaultAsync(p => p.PositionId == id);
        if (position is null)
        {
            return NotFound();
        }
        if (!await access.CanManageAsync(position.MunicipalityId))
        {
            return Forbid();
        }

        return View(position);
    }

    // POST /Admin/Positions/Delete/5
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var position = await db.Positions
            .Include(p => p.Candidates)
            .FirstOrDefaultAsync(p => p.PositionId == id);
        if (position is null)
        {
            return NotFound();
        }
        if (!await access.CanManageAsync(position.MunicipalityId))
        {
            return Forbid();
        }

        var photos = position.Candidates.Select(c => c.PhotoFileName).ToList();

        db.Positions.Remove(position);
        await db.SaveChangesAsync();

        photos.ForEach(images.Delete);

        SetStatusMessage($"Position \"{position.Name}\" and its candidates were deleted.");
        return RedirectToAction(nameof(Index), new { municipalityId = position.MunicipalityId });
    }

    // Rejects a municipality the admin does not manage (e.g. a tampered form value) and duplicate names.
    private async Task ValidateAsync(Position position)
    {
        if (!await db.Municipalities.AnyAsync(m => m.MunicipalityId == position.MunicipalityId))
        {
            ModelState.AddModelError(nameof(position.MunicipalityId), "Please select a municipality.");
            return;
        }
        if (!await access.CanManageAsync(position.MunicipalityId))
        {
            ModelState.AddModelError(nameof(position.MunicipalityId), "You can only manage positions for your own municipality.");
            return;
        }

        var name = position.Name?.Trim();
        if (await db.Positions.AnyAsync(p => p.MunicipalityId == position.MunicipalityId
                                             && p.Name == name
                                             && p.PositionId != position.PositionId))
        {
            ModelState.AddModelError(nameof(position.Name), "This municipality already has a position with this name.");
        }
    }

    // Fills the municipality drop-down with the municipalities this admin manages; returns their ids.
    private async Task<IReadOnlyList<int>> PopulateMunicipalitiesAsync(int? selectedId)
    {
        var ids = await access.GetManagedMunicipalityIdsAsync();
        var municipalities = await db.Municipalities.AsNoTracking()
            .Where(m => ids == null || ids.Contains(m.MunicipalityId))
            .OrderBy(m => m.Name)
            .ToListAsync();

        ViewData["Municipalities"] = new SelectList(municipalities, nameof(Municipality.MunicipalityId), nameof(Municipality.Name), selectedId);
        return municipalities.Select(m => m.MunicipalityId).ToList();
    }
}
