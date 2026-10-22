using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MunicipalElections.Areas.Admin.Models;
using MunicipalElections.Data;
using MunicipalElections.Models;
using MunicipalElections.Services;

namespace MunicipalElections.Areas.Admin.Controllers;

// Super Admin: full CRUD. Municipality admins: view and edit (e.g. logo) their own municipality only.
public class MunicipalitiesController(
    ApplicationDbContext db,
    MunicipalityAccess access,
    ImageStorage images,
    RoleManager<ApplicationRole> roleManager) : AdminControllerBase
{
    // GET /Admin/Municipalities
    public async Task<IActionResult> Index()
    {
        var ids = await access.GetManagedMunicipalityIdsAsync();
        var municipalities = await db.Municipalities.AsNoTracking()
            .Where(m => ids == null || ids.Contains(m.MunicipalityId))
            .Include(m => m.Positions).ThenInclude(p => p.Candidates)
            .OrderBy(m => m.Name)
            .ToListAsync();

        ViewData["IsSuperAdmin"] = access.IsSuperAdmin;
        return View(municipalities);
    }

    // GET /Admin/Municipalities/Details/5
    public async Task<IActionResult> Details(int id)
    {
        var municipality = await db.Municipalities.AsNoTracking()
            .Include(m => m.Positions).ThenInclude(p => p.Candidates)
            .FirstOrDefaultAsync(m => m.MunicipalityId == id);
        if (municipality is null)
        {
            return NotFound();
        }
        if (!await access.CanManageAsync(id))
        {
            return Forbid();
        }

        ViewData["IsSuperAdmin"] = access.IsSuperAdmin;
        return View(municipality);
    }

    // GET /Admin/Municipalities/Create
    [Authorize(Roles = AppRoles.SuperAdmin)]
    public IActionResult Create()
    {
        return View(new MunicipalityFormViewModel { ElectionDate = new DateTime(2026, 10, 17) });
    }

    // POST /Admin/Municipalities/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = AppRoles.SuperAdmin)]
    public async Task<IActionResult> Create(MunicipalityFormViewModel model)
    {
        await ValidateUniqueNameAsync(model);
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var municipality = new Municipality();
        Apply(model, municipality);
        if (model.LogoFile is not null)
        {
            municipality.LogoFileName = await images.SaveAsync(model.LogoFile);
        }

        db.Municipalities.Add(municipality);
        await db.SaveChangesAsync();

        await EnsureAdminRoleAsync(municipality);

        SetStatusMessage($"Municipality \"{municipality.Name}\" was created with the admin group \"{AppRoles.MunicipalityAdmin(municipality.Name)}\".");
        return RedirectToAction(nameof(Details), new { id = municipality.MunicipalityId });
    }

    // GET /Admin/Municipalities/Edit/5
    public async Task<IActionResult> Edit(int id)
    {
        var municipality = await db.Municipalities.FindAsync(id);
        if (municipality is null)
        {
            return NotFound();
        }
        if (!await access.CanManageAsync(id))
        {
            return Forbid();
        }

        ViewData["IsSuperAdmin"] = access.IsSuperAdmin;
        return View(new MunicipalityFormViewModel
        {
            MunicipalityId = municipality.MunicipalityId,
            Name = municipality.Name,
            Province = municipality.Province,
            ElectionDate = municipality.ElectionDate,
            Description = municipality.Description,
            CurrentLogoFileName = municipality.LogoFileName,
        });
    }

    // POST /Admin/Municipalities/Edit/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, MunicipalityFormViewModel model)
    {
        if (id != model.MunicipalityId)
        {
            return NotFound();
        }

        var municipality = await db.Municipalities.FindAsync(id);
        if (municipality is null)
        {
            return NotFound();
        }
        if (!await access.CanManageAsync(id))
        {
            return Forbid();
        }

        // Renaming also renames the admin group, so only a Super Admin may do it.
        if (!access.IsSuperAdmin)
        {
            model.Name = municipality.Name;
            ModelState.Remove(nameof(model.Name));
        }

        await ValidateUniqueNameAsync(model);
        if (!ModelState.IsValid)
        {
            model.CurrentLogoFileName = municipality.LogoFileName;
            ViewData["IsSuperAdmin"] = access.IsSuperAdmin;
            return View(model);
        }

        var oldName = municipality.Name;
        var oldLogo = municipality.LogoFileName;
        Apply(model, municipality);
        if (model.LogoFile is not null)
        {
            municipality.LogoFileName = await images.SaveAsync(model.LogoFile);
        }
        else if (model.RemoveLogo)
        {
            municipality.LogoFileName = null;
        }

        await db.SaveChangesAsync();

        if (oldLogo != municipality.LogoFileName)
        {
            images.Delete(oldLogo);
        }
        if (oldName != municipality.Name)
        {
            await RenameAdminRoleAsync(municipality);
        }

        SetStatusMessage($"Municipality \"{municipality.Name}\" was updated.");
        return RedirectToAction(nameof(Details), new { id });
    }

    // GET /Admin/Municipalities/Delete/5
    [Authorize(Roles = AppRoles.SuperAdmin)]
    public async Task<IActionResult> Delete(int id)
    {
        var municipality = await db.Municipalities.AsNoTracking()
            .Include(m => m.Positions).ThenInclude(p => p.Candidates)
            .FirstOrDefaultAsync(m => m.MunicipalityId == id);

        return municipality is null ? NotFound() : View(municipality);
    }

    // POST /Admin/Municipalities/Delete/5
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = AppRoles.SuperAdmin)]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var municipality = await db.Municipalities
            .Include(m => m.Positions).ThenInclude(p => p.Candidates)
            .FirstOrDefaultAsync(m => m.MunicipalityId == id);
        if (municipality is null)
        {
            return NotFound();
        }

        var files = municipality.Positions
            .SelectMany(p => p.Candidates)
            .Select(c => c.PhotoFileName)
            .Append(municipality.LogoFileName)
            .ToList();

        // Positions, candidates, saved candidates and the admin group are removed by cascade delete.
        db.Municipalities.Remove(municipality);
        await db.SaveChangesAsync();

        files.ForEach(images.Delete);

        SetStatusMessage($"Municipality \"{municipality.Name}\" and its admin group were deleted.");
        return RedirectToAction(nameof(Index));
    }

    private async Task ValidateUniqueNameAsync(MunicipalityFormViewModel model)
    {
        var name = model.Name.Trim();
        if (await db.Municipalities.AnyAsync(m => m.Name == name && m.MunicipalityId != model.MunicipalityId))
        {
            ModelState.AddModelError(nameof(model.Name), "A municipality with this name already exists.");
        }
    }

    private static void Apply(MunicipalityFormViewModel model, Municipality municipality)
    {
        municipality.Name = model.Name.Trim();
        municipality.Province = model.Province.Trim();
        municipality.ElectionDate = model.ElectionDate!.Value.Date;
        municipality.Description = model.Description?.Trim();
    }

    private async Task EnsureAdminRoleAsync(Municipality municipality)
    {
        var roleName = AppRoles.MunicipalityAdmin(municipality.Name);
        var role = await roleManager.FindByNameAsync(roleName);
        if (role is null)
        {
            await roleManager.CreateAsync(new ApplicationRole(roleName) { MunicipalityId = municipality.MunicipalityId });
        }
        else if (role.MunicipalityId != municipality.MunicipalityId)
        {
            role.MunicipalityId = municipality.MunicipalityId;
            await roleManager.UpdateAsync(role);
        }
    }

    private async Task RenameAdminRoleAsync(Municipality municipality)
    {
        var role = await roleManager.Roles.FirstOrDefaultAsync(r => r.MunicipalityId == municipality.MunicipalityId);
        if (role is null)
        {
            await EnsureAdminRoleAsync(municipality);
            return;
        }

        role.Name = AppRoles.MunicipalityAdmin(municipality.Name);
        await roleManager.UpdateAsync(role);
    }
}
