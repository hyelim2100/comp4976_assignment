---
agent: 'agent'
description: 'Change the EF Core model safely: entity, DbContext, migration, DTOs, services, seed data and UI'
---

Make this data model change: **${input:change:Describe the model change}**

Steps:
1. Update the entity in `MEMS.Data/Entities` (DataAnnotations with `FieldLengths` constants). If the entity
   belongs to a tenant, implement `ITenantOwned` and add the query filter in `MemsDbContext`.
2. Update `MemsDbContext.OnModelCreating` (relationships, delete behaviour, indexes, check constraints).
3. Run `dotnet ef migrations add <Name> --project MEMS.Data --startup-project MEMS.Data --output-dir Migrations`
   and show me the generated `Up` method for review.
4. Update the DTO / input model in `MEMS.Shared/Dtos` with the same validation rules.
5. Update the service in `MEMS.Data/Services` (tenant from `ITenantContext`, `ServiceResult` errors).
6. Update `MemsSeeder` (idempotent), the API controller + XML docs, `docs/api.md`, the admin form and
   the public page that shows the field.
7. Add or update a test in `MEMS.Tests` that proves the change cannot cross tenants.
8. Run `dotnet build MEMS.sln` and `dotnet test MEMS.Tests` and report the results.
