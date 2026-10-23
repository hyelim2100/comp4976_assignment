---
applyTo: "MunicipalElections/Areas/Admin/**"
description: "Authorization and CRUD patterns for the Admin area"
---

# Admin area instructions

- Controllers inherit `AdminControllerBase` (`[Area("Admin")]`, policy `AdminPolicies.AdminAccess`)
  and receive `MunicipalityAccess` through the primary constructor.
- Lists: get `await access.GetManagedMunicipalityIdsAsync()` (null means Super Admin = all) and
  filter with `ids == null || ids.Contains(x.MunicipalityId)`.
- Details / Edit / Delete (GET and POST): load the record, return `NotFound()` if missing, then
  `if (!await access.CanManageAsync(municipalityId)) return Forbid();`.
- Create / Edit POST: validate that the submitted municipality or position exists, belongs together,
  and is managed by the current admin; add a `ModelState` error instead of saving.
- Municipality Create/Delete are `[Authorize(Roles = AppRoles.SuperAdmin)]`; renaming is Super Admin only.
- Drop-downs only list municipalities/positions the admin manages. Position drop-downs carry
  `data-municipality` and are filtered by `wwwroot/js/position-filter.js`.
- Forms with uploads use `enctype="multipart/form-data"`, a view model with `[ImageFile] IFormFile`,
  and include `~/js/image-validation.js` after `_ValidationScriptsPartial`.
- After deleting records that own images, delete the files with `ImageStorage.Delete` **after**
  `SaveChangesAsync`.
- Use `SetStatusMessage("...")` and redirect (post/redirect/get).
