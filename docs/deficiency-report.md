# Assignment 1 deficiency resolution report

## Issues identified by the instructor

| # | Issue | Original problem (Assignment 1) | Resolution (Assignment 2) | Evidence |
|---|---|---|---|---|
| 1 | Administrators could use the voting guide | Administrators and voters used the same MVC application, and the voting-guide buttons were available to signed-in administrators, so an administrator could add candidates to a guide. | The administration site (Blazor Server, `MEMS.Admin`) is a separate application with **no** voting-guide or "add candidate" feature. The guide exists only in the public Blazor WebAssembly site, for anonymous voters, in their own browser. | `MEMS.Admin` contains no guide code (`grep -i guide` finds only a comment saying so); `admin-scope.py` scans every admin page for guide/ballot UI. |
| 2 | The `pm` administrator could see other municipalities' data | Signed in as `pm` (Pitt Meadows), data of other municipalities was visible. Access was checked in each MVC controller action through a role-to-municipality lookup, so isolation depended on every action and view remembering to apply it. | Tenant isolation is enforced in three layers: (a) EF Core global query filters on every tenant-owned entity driven by a scoped, set-once, fail-closed `TenantContext`; (b) services take the tenant from that context, never from input, and treat another tenant's id as "not found"; (c) the API binds every `/api/{tenant}/...` request to its route tenant and returns 403 for another tenant's administrator. In the admin site a Municipality Admin is fixed to their own tenant everywhere (lists, dropdowns, dashboard counts, detail URLs, images). Sessions are re-validated every minute. | `MEMS.Tests` (33 tests incl. `TenantDbContextFactoryTests`, `AdminTenantContextTests`, `ServiceTenantIsolationTests`); `admin-scope.py`: `pm` sees 1 municipality / 3 positions / 5 candidates, 0 other-tenant names on any page; `api-isolation.sh`: 403/404 for every cross-tenant request. See `docs/test-evidence.md`. |
| 3 | No administrator account could be created for a new municipality | Creating a municipality created an empty admin group; users had to be added in the seed code. | Creating a tenant requires its first Municipality Admin account in the same form (validated together; if the account cannot be created the tenant is rolled back). A Super-Admin-only **Administrators** page lists accounts and can create them, assign role and municipality, reset passwords and delete them. The rule "Municipality Admin ⇒ exactly one municipality, Super Admin ⇒ none" is validated in the form and again in the service. | `admin-e2e.mjs`: tenant "Coquitlam" created with `coquitlamadmin`, who can sign in and only sees Coquitlam; `AdminAccountRuleTests`. |
| 4 | Visitors could select candidates from several cities at once | The guide was a single list of candidate ids in the server-side ASP.NET Core session, with no check of the candidate's municipality or of the number of seats. | A guide belongs to exactly one tenant. Adding a candidate from another municipality opens a confirmation dialog ("Keep my Burnaby guide" / "Clear and start new guide"). A position accepts at most `SeatsAvailable` choices ("Seats full (1/1)"). The guide is per tab (`sessionStorage`). | `public-e2e.mjs`: replace dialog, keep/replace behaviour, seat limit, guide of another tenant not shown. |
| 5 | The source ZIP could not be checked | The submitted archive was not verified. | The archive is produced from the working tree without `bin/`, `obj/`, `.vs/`, `node_modules/` and `.git/`, then extracted into an empty folder and built and tested from there before submission. | Build + test output of the extracted archive in `docs/test-evidence.md`. |

## Specification conflict: local storage vs session storage

The specification contradicts itself about where voting selections are kept:

- Page 6, *Personal Voting Guide → Storage*: "Selections must be persisted using Browser **local** storage".
- Pages 7–8, *Election Privacy and Vote Confidentiality*: selections must be stored "only within the user's
  browser session", "Use browser **Session Storage**", and "Remove ballot data automatically when the browser
  session ends". The *Technology Requirement* repeats: "The Blazor WebAssembly application must use Browser
  Session Storage."

**Decision: `sessionStorage`.** The privacy section is the more specific and more recent requirement, it states
its rationale (stored ballot choices could reveal voting trends), and only `sessionStorage` satisfies "removed
automatically when the browser session ends" — `localStorage` survives closing the browser, so another person
using the same computer could see the choices. The guide still persists while the voter uses the site (page
reloads, navigation), which is what page 6 asks for. Implementation: `MEMS.Public.Client/Services/VotingGuideStore.cs`
(native `sessionStorage` via JS interop, candidate/position ids only). The E2E check confirms that the application
writes nothing to `localStorage` (only Blazor's own `blazor-resource-hash` cache entry exists there).

## Removed: `SavedCandidate` and `/api/savedcandidates`

Assignment 1 contained a `SavedCandidate` entity (table `SavedCandidates`: user id + candidate id) and a
JWT-protected `/api/savedcandidates` endpoint that stored a signed-in user's guide in the database. It was an
extra for a planned mobile client and was not used.

It was **removed** in Assignment 2 because it directly contradicts the election-privacy requirements: the
database "must not store individual ballot selections / candidate choices made by public users / in-progress
voting selections", and selections must not be "transmitted to backend APIs". Keeping it would also have let
administrators with database access read individual voters' choices. No replacement exists on the server; the
ERD and the API have no ballot-related table or endpoint.

## Other improvements made with the migration

| Area | Assignment 1 | Assignment 2 |
|---|---|---|
| Database | SQLite file | SQL Server in a Docker container managed by Aspire (data volume); EF Core Code First migrations |
| Architecture | One MVC app | Aspire AppHost + ServiceDefaults, Blazor Server admin, Blazor WebAssembly public site, Web API, data and shared libraries |
| Multi-tenancy | Municipality pages under one site | Tenant per URL slug (`/burnaby`), per-tenant colours, logo and tagline; new tenants need no new application |
| Validation | Data annotations + jQuery validation | Same rules in three layers: Blazor `DataAnnotationsValidator`, API `[ApiController]` → `ValidationProblemDetails`, database constraints (lengths, unique indexes, check constraints, composite FK). Images: extension, 2 MB, real file signature, extension must match content |
| Security | Admin roles per municipality group, JWT key in local settings | Two roles + tenant claim; Admin re-validates sessions every minute; JWT includes the security stamp (deleted/re-assigned admins' tokens stop working); login lockout after 5 failures; JWT key and SQL password are Aspire secret parameters in user-secrets |
| Errors and logging | Default error page | Global exception handler returning RFC 7807 ProblemDetails without stack traces; Blazor `ErrorBoundary` with friendly messages; structured logging and tracing in the Aspire dashboard |
| Images | Files in `wwwroot/uploads` (lost between deployments) | Stored in SQL Server and shared by Admin and API |
| Voting guide storage | Server-side ASP.NET Core session (kept in server memory) | Browser `sessionStorage` only; the server never receives the selections |
| Tests | Manual | 33 xUnit tests + 4 end-to-end scripts (API, admin scope, interactive admin, public site with network capture) |
