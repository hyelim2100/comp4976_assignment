using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MunicipalElections.Areas.Admin.Models;
using MunicipalElections.Data;
using MunicipalElections.Services;

namespace MunicipalElections.Areas.Admin.Controllers;

public class DashboardController(ApplicationDbContext db, MunicipalityAccess access) : AdminControllerBase
{
    // GET /Admin
    public async Task<IActionResult> Index()
    {
        var ids = await access.GetManagedMunicipalityIdsAsync();

        var municipalities = await db.Municipalities.AsNoTracking()
            .Where(m => ids == null || ids.Contains(m.MunicipalityId))
            .OrderBy(m => m.Name)
            .Select(m => new MunicipalitySummary(
                m.MunicipalityId,
                m.Name,
                m.ElectionDate,
                m.LogoFileName,
                m.Positions.Count,
                m.Positions.SelectMany(p => p.Candidates).Count()))
            .ToListAsync();

        return View(new DashboardViewModel
        {
            IsSuperAdmin = access.IsSuperAdmin,
            MunicipalityCount = municipalities.Count,
            PositionCount = municipalities.Sum(m => m.PositionCount),
            CandidateCount = municipalities.Sum(m => m.CandidateCount),
            Municipalities = municipalities,
        });
    }
}
