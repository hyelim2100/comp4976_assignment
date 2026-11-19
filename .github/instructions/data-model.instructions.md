---
applyTo: "MEMS.Data/**"
description: "EF Core Code First on SQL Server, tenant query filters, services and seeding"
---

# MEMS.Data instructions

- Entities use DataAnnotations for database constraints (`[Required]`, `[MaxLength(FieldLengths.X)]`,
  `[Range]`); take lengths from `MEMS.Shared.Validation.FieldLengths` so the UI/API rules match.
- Tenant-owned entities implement `ITenantOwned` and get a query filter in `MemsDbContext.OnModelCreating`:
  `x => BypassTenantFilter || x.MunicipalityId == CurrentTenantId`. Bypass happens only for a Super Admin
  without a working tenant.
- Keep `Candidate (PositionId, MunicipalityId) -> Position (Id, MunicipalityId)` as a composite foreign key:
  it guarantees in the database that a candidate never points to another tenant's position.
  SQL Server forbids multiple cascade paths: Municipality→Candidate is `NoAction`.
- Services (`Services/*Service.cs`):
  - create contexts with `IDbContextFactory<MemsDbContext>` (one short-lived context per operation);
  - return `ServiceResult` / `ServiceResult<T>` (`NotFound`, `Forbidden`, `Invalid(field, message)`);
  - take the tenant from `ITenantContext`; other-tenant ids are `NotFound`;
  - Super-Admin-only operations check `tenant.IsSuperAdmin` first;
  - log with message templates and ids, never with personal data or passwords.
- Images are stored in the `Images` table (`StoredImage`) after `ImageRules.Validate`; delete the old image
  when it is replaced.
- Multi-statement deletes run inside `db.Database.CreateExecutionStrategy().ExecuteAsync(...)` with a transaction
  (the Aspire SQL Server integration enables retries).
- After a model change: `dotnet ef migrations add <Name> --project MEMS.Data --startup-project MEMS.Data --output-dir Migrations`,
  then `dotnet ef migrations has-pending-model-changes` must report no changes.
- `MemsSeeder` runs on every API start and must stay idempotent.
- There must never be a table or column for voting-guide selections or vote counts (election privacy).
