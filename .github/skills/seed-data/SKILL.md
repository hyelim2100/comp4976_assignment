---
name: seed-data
description: How to add or change seeded tenants, positions, candidates, images and administrator accounts. Use when the user asks for more sample data, a new tenant or a new admin account.
---

# Seed data

All seeding lives in `MEMS.Data/Seeding/MemsSeeder.cs`. The API runs it on every start (after applying
migrations), so every step must be **idempotent**.

## Election data
- `Tenants` is an array of `TenantSeed(Name, Slug, Color, Tagline, Description, Positions)`; each position is
  `(Name, Seats, CandidateSeed[])`. It is inserted only when the `Municipalities` table is empty, so a tenant
  deleted by a Super Admin is not re-created on restart.
- To reseed a development database, delete the Docker volume: stop the AppHost, then
  `docker rm -f <sql container>` and `docker volume rm mems-sql-data`.
- Slugs must satisfy `SlugRules` (lowercase, hyphens, not reserved). Colours are `#RRGGBB`.

## Images
- PNG files in `MEMS.Data/SeedData/Images` are embedded resources (`SeedImages.<file name>`).
- Logos: `seed-logo-<slug>.png`. Candidate photos: `seed-photo-<SlugRules.FromName(name)>.png`.
- They are stored in the `Images` table with the tenant's `MunicipalityId`.

## Accounts
- `Accounts` lists `(UserName, Role, TenantSlug)`; all use `DefaultPassword` (`P@$$w0rd`).
- Accounts are created through `AdminUserService.CreateAccountAsync`, so the role/tenant rule applies
  (Municipality Admin needs a tenant, Super Admin has none). A Municipality Admin is skipped when its tenant
  no longer exists.
