---
agent: 'agent'
description: 'Add admin CRUD pages for an entity, following the municipality-scoped authorization pattern'
---

Add full admin CRUD (Index, Details, Create, Edit, Delete) for the entity **${input:entity:Entity name}**
in `MunicipalElections/Areas/Admin`.

Follow the existing `PositionsController` / `CandidatesController` patterns exactly:

1. Controller inherits `AdminControllerBase` and injects `ApplicationDbContext` and `MunicipalityAccess`
   (plus `ImageStorage` if the entity has an image).
2. Index filters by `GetManagedMunicipalityIdsAsync()`; every Details/Edit/Delete action (GET and POST)
   checks `CanManageAsync` and returns `Forbid()`.
3. Create/Edit POST validate submitted foreign keys (existence, ownership, municipality scope) and
   duplicate names, adding `ModelState` errors.
4. Use a form view model in `Areas/Admin/Models` when the form has a file upload; otherwise bind the
   entity with an explicit `[Bind]` list.
5. Views: `_Form` partial shared by Create and Edit, Bootstrap 5 layout, `_ValidationScriptsPartial`
   (and `image-validation.js` for uploads), confirmation page for Delete.
6. Add a link to the admin navigation in `Areas/Admin/Views/Shared/_AdminLayout.cshtml`.
7. Build with `dotnet build` and fix all warnings.

Finish by listing the URLs to test as `super` and as `pm`, including one that `pm` must not be able to open.
