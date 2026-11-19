# Copilot instructions — MEMS (Municipal Elections Management System), Assignment 2

Multi-tenant election system (BCIT COMP4976). Each **municipality is a tenant** with its own public site
(`/burnaby`, `/surrey`, ...). One shared Blazor Server admin site manages all tenants; a Blazor
WebAssembly public site reads everything through the Web API; .NET Aspire orchestrates it all.

## Tech stack
- .NET 10 (`global.json` pins SDK 10, `rollForward: latestFeature`), C# with nullable reference types
- .NET Aspire 13 (AppHost + ServiceDefaults: OpenTelemetry, health checks, service discovery)
- SQL Server in a Docker container (Aspire `AddSqlServer` + data volume). **SQLite is not allowed.**
- EF Core 10 Code First, migrations, global query filters for tenant isolation
- ASP.NET Core Identity (cookies in Admin, JWT bearer in the API), roles `Super Admin`, `Municipality Admin`
- Blazor Server (Admin, under `/admin`), Blazor WebAssembly (Public), ASP.NET Core Web API + OpenAPI/Swagger UI
- Bootstrap 5, xUnit tests (`MEMS.Tests`), headless-browser E2E scripts (`tests/e2e`)

## Solution layout (`MEMS.sln`)
| Project | Purpose |
|---|---|
| `MEMS.AppHost` | Aspire orchestration: `sql` + `memsdb`, `api`, `admin`, `public`; JWT key parameter |
| `MEMS.ServiceDefaults` | OpenTelemetry logs/traces/metrics, `/health` + `/alive`, service discovery, resilience |
| `MEMS.Shared` | DTOs and input models with DataAnnotations, `FieldLengths`, `SlugRules`, `ImageRules`, `AdminAccountRules`, `AppRoles` |
| `MEMS.Data` | Entities, `MemsDbContext` (query filters), `TenantContext`, services, migrations, `MemsSeeder`, seed images |
| `MEMS.Api` | Controllers under `/api/municipalities` and `/api/{tenant}/...`, `TenantResolutionFilter`, JWT, ProblemDetails |
| `MEMS.Admin` | Blazor Server admin site; uses MEMS.Data services directly; `AdminTenantResolver` |
| `MEMS.Public` | Host of the WASM app; proxies `/api/*` to the API with YARP + service discovery |
| `MEMS.Public.Client` | Blazor WebAssembly pages; `MemsApiClient` (GET only), `VotingGuideStore` (sessionStorage) |
| `MEMS.Tests` | xUnit tests of tenant isolation, factory lifetime, admin tenant context, account rules |

## Rules that must always hold
1. **Tenant isolation in three layers.**
   - EF: every tenant-owned entity (`ITenantOwned`, plus `Municipality`) has a global query filter that
     reads `ITenantContext`. Never call `IgnoreQueryFilters()` except in Super-Admin-only code or the
     public municipality directory, and comment why.
   - Services: take the tenant from `ITenantContext`, never from client input. A candidate's tenant comes
     from its position. Another tenant's id must behave exactly like a missing id (NotFound).
   - API: `TenantResolutionFilter` binds every `/api/{tenant}/...` request to the route tenant (unknown slug
     404, other-tenant admin 403). Admin: `AdminTenantResolver` (Municipality Admin fixed to own tenant,
     Super Admin "All" or a working tenant chosen in the header).
2. **`TenantContext` is scoped, set once and fails closed.** `IDbContextFactory` is registered Scoped so each
   context gets the tenant of its own request/circuit. Do not register the factory or `TenantContext` as singleton.
3. **Election privacy.** The voting guide lives only in browser `sessionStorage` (`VotingGuideStore`).
   No entity, table, API endpoint or HTTP call may store or transmit guide selections. `MemsApiClient`
   has GET methods only. The Admin app has no voting-guide feature.
4. **Account rule.** Municipality Admin ⇒ `MunicipalityId` required; Super Admin ⇒ `null`. Enforced by
   `AdminAccountRules` in the input models and again in `AdminUserService`.
5. **Validation in three layers.** DataAnnotations in `MEMS.Shared` input models (Blazor `DataAnnotationsValidator`
   and API `[ApiController]`), service checks (uniqueness, ownership), database constraints
   (`MaxLength`, unique indexes, check constraints, composite FK `(PositionId, MunicipalityId)`).
   Images: `ImageRules` (extension, 2 MB, real file signature) before saving to the `Images` table.
6. **Errors.** API errors are RFC 7807 ProblemDetails; never return exception messages or stack traces.
   Blazor pages show friendly messages (`ErrorBoundary`, `/Error`). Log with structured templates
   (`logger.LogInformation("Candidate {CandidateId} created", id)`), not string interpolation.
7. **No secrets in source control.** SQL password and `jwt-key` are Aspire parameters stored in the
   AppHost user-secrets. Connection strings come from Aspire (`memsdb`).
8. **Schema changes use migrations** in `MEMS.Data/Migrations`:
   `dotnet ef migrations add <Name> --project MEMS.Data --startup-project MEMS.Data --output-dir Migrations`.
   The API applies migrations and the idempotent seed on startup.

## Coding conventions
- File-scoped namespaces, primary constructors, `async` EF methods, `AsNoTracking()` for reads.
- Filter and order on entities *before* projecting into DTO records (SQL Server cannot translate a
  `Where`/`OrderBy` on members of a constructor-projected record).
- Blazor Server components use services that create short-lived contexts with `IDbContextFactory`.
- Blazor pages that depend on the URL tenant clear their data before awaiting a new load and ignore late
  responses for an old slug.
- Dates are formatted explicitly (`"dddd, MMMM d, yyyy"`); the public site runs with `en-US` culture.
- Short comments that explain *why*.

## Build, run, test
```bash
dotnet build MEMS.sln
dotnet run --project MEMS.AppHost      # Docker Desktop must be running
dotnet test MEMS.Tests
node tests/e2e/public-e2e.mjs          # with the AppHost running (see tests/e2e/README.md)
```
Accounts (password `P@$$w0rd`): `super`, `burnabyadmin`, `surreyadmin`, `richmondadmin`, `pm` (Pitt Meadows), `mapleridgeadmin`.
