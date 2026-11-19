---
agent: 'agent'
description: 'Add Blazor Server admin pages (list, details, create, edit, delete) for an entity, tenant-scoped'
---

Add admin pages for **${input:entity:Entity name}** in `MEMS.Admin/Components/Pages/${input:entity}`.

Follow the `Positions` pages exactly:

1. `Index.razor`, `Details.razor`, `Create.razor`, `Edit.razor`, `Delete.razor` and a shared `<Entity>Form.razor`.
2. Use the MEMS.Data service only (no `MemsDbContext` in components). A `null` result renders `<NotFoundCard>`.
3. Forms: `EditForm` + `EditContext`, `DataAnnotationsValidator`, `ValidationSummary`, `ValidationMessage` per
   field, `FormErrors.Apply(result)` for service errors, a disabled submit button while saving.
4. Show a municipality dropdown only when `AdminSession.CanChooseMunicipality`; otherwise show the working
   tenant read-only.
5. Delete pages explain what else is deleted before the user confirms.
6. Add a `NavLink` in `Components/Layout/NavMenu.razor` (inside the Super Admin `AuthorizeView` if the pages
   are Super Admin only, together with `[Authorize(Roles = AppRoles.SuperAdmin)]`).
7. Build with `dotnet build MEMS.sln` and fix all warnings.

Finish by listing the URLs to test as `super` and as `pm`, including one that `pm` must not be able to see.
