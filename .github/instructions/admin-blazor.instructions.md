---
applyTo: "MEMS.Admin/**"
description: "Blazor Server admin site: tenant scope, forms, validation and authorization"
---

# MEMS.Admin instructions

- Pages live in `Components/Pages/<Area>/` and are interactive server pages. `Components/Pages/_Imports.razor`
  requires an administrator; Super-Admin-only pages add `@attribute [Authorize(Roles = AppRoles.SuperAdmin)]`.
- Use MEMS.Data services (`MunicipalityService`, `PositionService`, `CandidateService`, `AdminUserService`,
  `DashboardService`). Never inject `MemsDbContext` into components and never filter tenants by hand in a
  page — the services and query filters do it.
- `AdminSession` tells the UI what to show: `CanChooseMunicipality` is true only for a Super Admin in
  "All municipalities" mode. Otherwise the municipality is fixed and shown read-only.
- Details/Edit/Delete pages load the item through the service; `null` means "not found or another tenant" and
  renders `<NotFoundCard>` (do not reveal that the item exists).
- Forms: `EditForm` with an `EditContext`, `<DataAnnotationsValidator />`, `<ValidationSummary />`, a
  `<ValidationMessage>` per field, and `FormErrors.Apply(result)` to show service errors next to fields.
  Input models come from `MEMS.Shared.Dtos`.
- Images: `<ImageUploadField>` (validates extension, size and signature before submit) and `<ImageThumb>`
  (served by `/admin/images/{id}` through the tenant-filtered `ImageService`).
- Delete screens show what else is deleted (`TenantDeletionImpactDto`, `PositionDto.CandidateCount`).
- Navigation that leaves Blazor routing (e.g. `select-tenant`) needs `target="_top"` or `forceLoad: true`.
- The admin site has **no** voting-guide or "add candidate to guide" feature.
