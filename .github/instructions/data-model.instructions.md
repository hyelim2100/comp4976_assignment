---
applyTo: "MunicipalElections/{Models,Data}/**"
description: "EF Core Code First, SQLite and seeding rules"
---

# Data model instructions

- Entities use data annotations for validation that the UI should also enforce (`[Required]`,
  `[StringLength]`, `[Range]`, `[EmailAddress]`, `[Url]`, `[Display]`).
- Configure relationships, delete behaviour and indexes in `ApplicationDbContext.OnModelCreating`.
  Current rules: Municipality → Positions → Candidates cascade; a municipality's admin role cascades;
  unique indexes on municipality name, (MunicipalityId, position name) and (UserId, CandidateId).
- A candidate's municipality is reached through `Position.Municipality`; do not add a duplicate
  `MunicipalityId` column to `Candidate`.
- After any model change: `dotnet ef migrations add <DescriptiveName> -o Data/Migrations`, build,
  and review the generated migration before committing.
- `DbSeeder` must be idempotent (check before inserting) because it runs on every startup,
  including on Azure. Seed images belong in `SeedData/Images` and use `seed-*` file names.
- Store dates as `DateTime`; candidate `DateCreated` is set in UTC.
