---
agent: 'agent'
description: 'Change the EF Core model safely: entity, DbContext, migration, seed data and views'
---

Make this data model change: **${input:change:Describe the model change}**

Steps:
1. Update the entity in `MunicipalElections/Models` with validation attributes.
2. Update `ApplicationDbContext.OnModelCreating` for relationships, delete behaviour and indexes.
3. Run `dotnet ef migrations add <DescriptiveName> -o Data/Migrations` from `MunicipalElections/`
   and show me the generated `Up` method for review.
4. Update `Data/DbSeeder.cs` so seed data includes the new data; keep seeding idempotent.
5. Update admin view models, forms and public views that display the changed fields.
6. If the field is exposed by the REST API, update the DTO in `Dtos/` and `docs/api-contract.md`.
7. Run `dotnet build` and report anything that could break existing data on Azure
   (the migration runs automatically on startup there).
