using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MunicipalElections.Data;
using MunicipalElections.Models;
using MunicipalElections.Services;

namespace MunicipalElections.Controllers;

// The anonymous visitor's personal voting guide (session state) and its printable version.
public class VotingGuideController(ApplicationDbContext db, VotingGuide guide) : Controller
{
    // GET /VotingGuide
    public async Task<IActionResult> Index()
    {
        return View(await BuildAsync(municipalityId: null));
    }

    // GET /VotingGuide/Print?municipalityId=1&photos=true
    public async Task<IActionResult> Print(int? municipalityId, bool photos = true)
    {
        var model = await BuildAsync(municipalityId);
        model.ShowPhotos = photos;
        return View(model);
    }

    // POST /VotingGuide/Add/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Add(int id, string? returnUrl)
    {
        if (await db.Candidates.AnyAsync(c => c.CandidateId == id))
        {
            guide.Add(id);
        }
        return RedirectBack(returnUrl);
    }

    // POST /VotingGuide/Remove/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Remove(int id, string? returnUrl)
    {
        guide.Remove(id);
        return RedirectBack(returnUrl);
    }

    // POST /VotingGuide/Clear
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Clear()
    {
        guide.Clear();
        return RedirectToAction(nameof(Index));
    }

    private async Task<VotingGuideViewModel> BuildAsync(int? municipalityId)
    {
        var ids = guide.CandidateIds;
        var candidates = await db.Candidates.AsNoTracking()
            .Include(c => c.Position!).ThenInclude(p => p.Municipality)
            .Where(c => ids.Contains(c.CandidateId))
            .ToListAsync();

        guide.KeepOnly(candidates.Select(c => c.CandidateId));

        var groups = candidates
            .Where(c => municipalityId == null || c.Position!.MunicipalityId == municipalityId)
            .GroupBy(c => c.Position!.MunicipalityId)
            .Select(m => new GuideMunicipality(
                m.First().Position!.Municipality!,
                m.GroupBy(c => c.PositionId)
                    .OrderBy(p => p.Key)
                    .Select(p => new GuidePosition(p.First().Position!, p.OrderBy(c => c.Name).ToList()))
                    .ToList()))
            .OrderBy(g => g.Municipality.Name)
            .ToList();

        return new VotingGuideViewModel { Municipalities = groups, MunicipalityFilter = municipalityId };
    }

    private IActionResult RedirectBack(string? returnUrl) =>
        !string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl)
            ? LocalRedirect(returnUrl)
            : RedirectToAction(nameof(Index));
}
