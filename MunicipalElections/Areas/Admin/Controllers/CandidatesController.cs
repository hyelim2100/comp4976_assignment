using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using MunicipalElections.Areas.Admin.Models;
using MunicipalElections.Data;
using MunicipalElections.Models;
using MunicipalElections.Services;

namespace MunicipalElections.Areas.Admin.Controllers;

public class CandidatesController(ApplicationDbContext db, MunicipalityAccess access, ImageStorage images) : AdminControllerBase
{
    // GET /Admin/Candidates?search=&municipalityId=&positionId=
    public async Task<IActionResult> Index(string? search, int? municipalityId, int? positionId)
    {
        var ids = await access.GetManagedMunicipalityIdsAsync();
        var query = WithPosition().AsNoTracking()
            .Where(c => ids == null || ids.Contains(c.Position!.MunicipalityId));

        if (!string.IsNullOrWhiteSpace(search))
        {
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

        var candidates = await query
            .OrderBy(c => c.Position!.Municipality!.Name).ThenBy(c => c.PositionId).ThenBy(c => c.Name)
            .ToListAsync();

        var options = await BuildOptionsAsync();
        ViewData["Search"] = search;
        ViewData["MunicipalityFilter"] = municipalityId;
        ViewData["PositionFilter"] = positionId;
        ViewData["Municipalities"] = new SelectList(options.Municipalities, nameof(Municipality.MunicipalityId), nameof(Municipality.Name), municipalityId);
        ViewData["Positions"] = options.Positions;
        return View(candidates);
    }

    // GET /Admin/Candidates/Details/5
    public async Task<IActionResult> Details(int id)
    {
        var candidate = await WithPosition().AsNoTracking().FirstOrDefaultAsync(c => c.CandidateId == id);
        if (candidate is null)
        {
            return NotFound();
        }
        if (!await access.CanManageAsync(candidate.Position!.MunicipalityId))
        {
            return Forbid();
        }

        return View(candidate);
    }

    // GET /Admin/Candidates/Create?positionId=2
    public async Task<IActionResult> Create(int? positionId)
    {
        var model = new CandidateFormViewModel();
        var position = positionId is null ? null : await db.Positions.FindAsync(positionId);
        if (position is not null && await access.CanManageAsync(position.MunicipalityId))
        {
            model.PositionId = position.PositionId;
            model.MunicipalityId = position.MunicipalityId;
        }

        await PopulateOptionsAsync(model);
        if (model.MunicipalityId is null && model.MunicipalityOptions.Count() == 1)
        {
            model.MunicipalityId = int.Parse(model.MunicipalityOptions.First().Value);
        }
        return View(model);
    }

    // POST /Admin/Candidates/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CandidateFormViewModel model)
    {
        await ValidatePositionAsync(model);
        if (!ModelState.IsValid)
        {
            await PopulateOptionsAsync(model);
            return View(model);
        }

        var candidate = new Candidate { DateCreated = DateTime.UtcNow };
        Apply(model, candidate);
        if (model.PhotoFile is not null)
        {
            candidate.PhotoFileName = await images.SaveAsync(model.PhotoFile);
        }

        db.Candidates.Add(candidate);
        await db.SaveChangesAsync();

        SetStatusMessage($"Candidate \"{candidate.Name}\" was created.");
        return RedirectToAction(nameof(Details), new { id = candidate.CandidateId });
    }

    // GET /Admin/Candidates/Edit/5
    public async Task<IActionResult> Edit(int id)
    {
        var candidate = await WithPosition().FirstOrDefaultAsync(c => c.CandidateId == id);
        if (candidate is null)
        {
            return NotFound();
        }
        if (!await access.CanManageAsync(candidate.Position!.MunicipalityId))
        {
            return Forbid();
        }

        var model = new CandidateFormViewModel
        {
            CandidateId = candidate.CandidateId,
            Name = candidate.Name,
            MunicipalityId = candidate.Position.MunicipalityId,
            PositionId = candidate.PositionId,
            Profile = candidate.Profile,
            Website = candidate.Website,
            Email = candidate.Email,
            CurrentPhotoFileName = candidate.PhotoFileName,
        };
        await PopulateOptionsAsync(model);
        return View(model);
    }

    // POST /Admin/Candidates/Edit/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, CandidateFormViewModel model)
    {
        if (id != model.CandidateId)
        {
            return NotFound();
        }

        var candidate = await WithPosition().FirstOrDefaultAsync(c => c.CandidateId == id);
        if (candidate is null)
        {
            return NotFound();
        }
        if (!await access.CanManageAsync(candidate.Position!.MunicipalityId))
        {
            return Forbid();
        }

        await ValidatePositionAsync(model);
        if (!ModelState.IsValid)
        {
            model.CurrentPhotoFileName = candidate.PhotoFileName;
            await PopulateOptionsAsync(model);
            return View(model);
        }

        var oldPhoto = candidate.PhotoFileName;
        Apply(model, candidate);
        if (model.PhotoFile is not null)
        {
            candidate.PhotoFileName = await images.SaveAsync(model.PhotoFile);
        }
        else if (model.RemovePhoto)
        {
            candidate.PhotoFileName = null;
        }

        await db.SaveChangesAsync();

        if (oldPhoto != candidate.PhotoFileName)
        {
            images.Delete(oldPhoto);
        }

        SetStatusMessage($"Candidate \"{candidate.Name}\" was updated.");
        return RedirectToAction(nameof(Details), new { id });
    }

    // GET /Admin/Candidates/Delete/5
    public async Task<IActionResult> Delete(int id)
    {
        var candidate = await WithPosition().AsNoTracking().FirstOrDefaultAsync(c => c.CandidateId == id);
        if (candidate is null)
        {
            return NotFound();
        }
        if (!await access.CanManageAsync(candidate.Position!.MunicipalityId))
        {
            return Forbid();
        }

        return View(candidate);
    }

    // POST /Admin/Candidates/Delete/5
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var candidate = await WithPosition().FirstOrDefaultAsync(c => c.CandidateId == id);
        if (candidate is null)
        {
            return NotFound();
        }
        if (!await access.CanManageAsync(candidate.Position!.MunicipalityId))
        {
            return Forbid();
        }

        db.Candidates.Remove(candidate);
        await db.SaveChangesAsync();
        images.Delete(candidate.PhotoFileName);

        SetStatusMessage($"Candidate \"{candidate.Name}\" was deleted.");
        return RedirectToAction(nameof(Index));
    }

    private IQueryable<Candidate> WithPosition() =>
        db.Candidates.Include(c => c.Position!).ThenInclude(p => p.Municipality);

    // The position must exist, belong to the chosen municipality, and be in a municipality this
    // admin manages — this also blocks tampered form values that point at another municipality.
    private async Task ValidatePositionAsync(CandidateFormViewModel model)
    {
        if (model.PositionId is null || model.MunicipalityId is null)
        {
            return;
        }

        var position = await db.Positions.AsNoTracking().FirstOrDefaultAsync(p => p.PositionId == model.PositionId);
        if (position is null)
        {
            ModelState.AddModelError(nameof(model.PositionId), "Please select a valid position.");
        }
        else if (position.MunicipalityId != model.MunicipalityId)
        {
            ModelState.AddModelError(nameof(model.PositionId), "The position does not belong to the selected municipality.");
        }
        else if (!await access.CanManageAsync(position.MunicipalityId))
        {
            ModelState.AddModelError(nameof(model.MunicipalityId), "You can only manage candidates for your own municipality.");
        }
    }

    private static void Apply(CandidateFormViewModel model, Candidate candidate)
    {
        candidate.Name = model.Name.Trim();
        candidate.PositionId = model.PositionId!.Value;
        candidate.Profile = model.Profile?.Trim();
        candidate.Website = string.IsNullOrWhiteSpace(model.Website) ? null : model.Website.Trim();
        candidate.Email = string.IsNullOrWhiteSpace(model.Email) ? null : model.Email.Trim();
    }

    private async Task PopulateOptionsAsync(CandidateFormViewModel model)
    {
        var options = await BuildOptionsAsync();
        model.MunicipalityOptions = new SelectList(options.Municipalities, nameof(Municipality.MunicipalityId), nameof(Municipality.Name), model.MunicipalityId);
        model.PositionOptions = options.Positions;
    }

    private async Task<(List<Municipality> Municipalities, List<PositionOption> Positions)> BuildOptionsAsync()
    {
        var ids = await access.GetManagedMunicipalityIdsAsync();
        var municipalities = await db.Municipalities.AsNoTracking()
            .Where(m => ids == null || ids.Contains(m.MunicipalityId))
            .OrderBy(m => m.Name)
            .ToListAsync();
        var positions = await db.Positions.AsNoTracking()
            .Where(p => ids == null || ids.Contains(p.MunicipalityId))
            .OrderBy(p => p.PositionId)
            .Select(p => new PositionOption(p.PositionId, p.Name, p.MunicipalityId))
            .ToListAsync();

        return (municipalities, positions);
    }
}
