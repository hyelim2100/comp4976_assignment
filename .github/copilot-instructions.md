# Copilot instructions — Municipal Elections Management System

ASP.NET Core MVC app (BCIT COMP4976) where administrators manage municipalities, positions
and candidates, and anonymous voters browse candidates and print a personal voting guide.

## Tech stack
- .NET 10, ASP.NET Core MVC + Razor Pages (Identity UI), C# with nullable reference types
- Entity Framework Core **Code First** with **SQLite** (`Microsoft.EntityFrameworkCore.Sqlite`)
- ASP.NET Core Identity with roles (`ApplicationUser`, `ApplicationRole`)
- Bootstrap 5 + jQuery unobtrusive validation (from `wwwroot/lib`)
- Extra REST API under `/api` with JWT bearer auth and Swagger (Swashbuckle)
- Deployed to Azure App Service (Linux) by `.github/workflows/deploy.yml`

## Project layout (`MunicipalElections/`)
| Folder | Purpose |
|---|---|
| `Models/` | Entities (`Municipality`, `Position`, `Candidate`, `SavedCandidate`), Identity types, public view models |
| `Data/` | `ApplicationDbContext`, `DbSeeder` (migrate + seed on startup), `AppRoles`, `Migrations/` |
| `Services/` | `MunicipalityAccess` (admin scope), `AdminAccessHandler` (policy), `ImageStorage` (uploads), `VotingGuide` (session), JWT |
| `Validation/` | `ImageFileAttribute` (server + client upload validation) |
| `Controllers/` | Public site (`Home`, `Municipalities`, `Candidates`, `VotingGuide`) |
| `Controllers/Api/` | REST API controllers (`ApiControllerBase`, DTO mapping) |
| `Areas/Admin/` | Admin dashboard and CRUD (`AdminControllerBase`), admin view models |
| `Areas/Identity/Pages/Account/` | Overridden Login (user name) and disabled Register |
| `SeedData/Images/` | Sample logos/photos copied to the uploads folder on startup |

## Rules that must always hold
1. **Municipality-scoped authorization.** Every admin controller inherits `AdminControllerBase`
   (policy `AdminAccess`). For any record, call `MunicipalityAccess.CanManageAsync(municipalityId)`
   and return `Forbid()` when it fails — in GET *and* POST actions. Also validate submitted
   `MunicipalityId` / `PositionId` values, because form values can be tampered with.
   Only `AppRoles.SuperAdmin` may create or delete municipalities.
2. **Admin groups belong to municipalities.** A municipality admin group is an `ApplicationRole`
   named `AppRoles.MunicipalityAdmin(name)` with `MunicipalityId` set. Keep the role in sync when a
   municipality is created or renamed (see `Areas/Admin/Controllers/MunicipalitiesController`).
3. **Uploads go through `ImageStorage`.** Never write files directly. Validate with `[ImageFile]`
   on the view model, save with `ImageStorage.SaveAsync` (GUID file name), and delete the old file
   after `SaveChangesAsync` succeeds. Render images with `Images.GetUrl(...)` and fall back to
   `/images/placeholder-photo.svg` or `/images/placeholder-logo.svg`.
4. **Validation on both sides.** Use data annotations on entities/view models, include
   `<partial name="_ValidationScriptsPartial" />` on forms, and re-check rules on the server.
5. **No secrets in source control.** The connection string and `Jwt:Key` live only in
   `appsettings.Development.json` (git-ignored) or Azure app settings. Do not add them to
   `appsettings.json`.
6. **Schema changes use migrations.** Change the model, then
   `dotnet ef migrations add <Name> -o Data/Migrations`. Do not edit the database by hand;
   `DbSeeder` applies migrations on startup. Seeding must stay idempotent.

## Coding conventions
- File-scoped namespaces, primary constructors for DI, `async`/`await` with EF Core `*Async` methods.
- Read-only queries use `AsNoTracking()`; load navigation properties with `Include`/`ThenInclude`.
- Search by name with `EF.Functions.Like(c.Name, $"%{term}%")` (case-insensitive in SQLite).
- Admin POST actions use `[ValidateAntiForgeryToken]` and the post/redirect/get pattern with
  `SetStatusMessage(...)` for feedback.
- Bind entities with explicit `[Bind(...)]` lists or use a dedicated form view model (never bind
  navigation properties or file names from the form).
- Site culture is fixed to `en-CA`; format dates with explicit patterns such as `"MMMM d, yyyy"`.
- Match existing comment density: short comments that explain *why*, not *what*.

## Build and run
```bash
cd MunicipalElections
dotnet build
dotnet run            # creates, migrates and seeds app.db on first start
```
Seeded accounts: `super` (Super Admin) and `pm` (Pitt Meadows Admin), password `P@$$w0rd`.
