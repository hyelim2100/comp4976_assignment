---
name: seed-data
description: How to add or change seeded municipalities, positions, candidates, images, roles and admin users. Use when the user asks for more sample data or a new admin account.
---

# Seed data

All seeding lives in `MunicipalElections/Data/DbSeeder.cs` and runs on every startup (locally and on
Azure), so every step must be **idempotent**.

## Domain data
- `SeedMunicipalitiesAsync` inserts the sample municipalities with nested positions and candidates
  only when the `Municipalities` table is empty. To change sample data on an existing database,
  delete the local `app.db` (it is recreated on the next run) or write an idempotent update step.
- Candidates are created with `NewCandidate(name, profile, website?, email?)`; the photo file name is
  derived from the name: `seed-photo-<slug>.png`.
- Municipality logos use `SeedLogoFileName(name)` → `seed-logo-<slug>.png`.

## Images
- Put PNG/JPG files in `MunicipalElections/SeedData/Images/` with the `seed-` naming above.
- The project file copies that folder to the build and publish output, and
  `ImageStorage.CopySeedImages` copies missing files into the uploads folder on startup.

## Roles and users
- `EnsureRoleAsync` creates `AppRoles.SuperAdmin` and one `"<Municipality> Admin"` role per
  municipality with `MunicipalityId` set.
- `EnsureUserAsync(userName, email, name, roleName)` creates a confirmed user with the default
  password only if the user name does not exist yet. Add new admin accounts the same way, e.g. an
  admin for Burnaby: `EnsureUserAsync(userManager, "bb", "bb@example.com", "Burnaby Administrator",
  AppRoles.MunicipalityAdmin("Burnaby"))`.
