COMP4976 Assignment 2 - Municipal Elections Management System (MEMS) Modernization
==================================================================================

1. TEAM MEMBERS
---------------
- Hyelim Kim  (A01459149)
- Hailey Kim  (A01412747)


2. CONTRIBUTIONS
----------------
TODO (team): describe who did what in Assignment 2.


3. WHAT HAS NOT BEEN COMPLETED
------------------------------
- No Azure deployment (not required for Assignment 2).
- The documentation PDF/Word file must still include the screenshots (see docs/screenshots.md).


4. MAJOR CHALLENGES
-------------------
- Enforcing tenant isolation in three layers (Blazor Server, Web API, EF Core query filters)
  while letting the Super Admin work across all tenants or inside one chosen tenant.
- Blazor Server circuits live for a long time: the tenant must be resolved per circuit, never
  shared between users, and an administrator who is deleted or re-assigned must be signed out
  of pages that are already open (re-validated every minute).
- Making sure the public WebAssembly site never shows data of the previous municipality after
  the URL changes, and never sends voting-guide data to the server.


5. HOW TO RUN
-------------
Requirements: .NET SDK 10 (global.json selects it), Docker Desktop (running), Node.js 22+ and
Python 3 only for the end-to-end scripts.

  dotnet run --project MEMS.AppHost

The Aspire dashboard URL (with login token) is printed in the console. On the first run Aspire
starts a SQL Server container (volume "mems-sql-data"), generates the SQL password and the JWT
signing key and stores them in the AppHost user-secrets. The API applies the EF Core migrations
and seeds the database; the Admin and Public apps start when the API is healthy.

  Aspire dashboard   https://localhost:17286
  Public site        https://localhost:5001          (/burnaby, /surrey, /richmond, ...)
  Admin site         https://localhost:7101/admin
  Web API + Swagger  https://localhost:7201/swagger

Tests:
  dotnet test MEMS.Tests                 (33 xUnit tests, no Docker needed; while the AppHost
                                         is running add --no-build, because the tests reference
                                         MEMS.Admin whose files are then in use)
  bash tests/e2e/api-isolation.sh        (with the AppHost running; see tests/e2e/README.md)
  python tests/e2e/admin-scope.py
  node tests/e2e/admin-e2e.mjs
  node tests/e2e/public-e2e.mjs

Start from an empty database: stop the AppHost, then
  docker rm -f <the sql-xxxxxxxx container>
  docker volume rm mems-sql-data


6. ACCOUNTS (password for all: P@$$w0rd)
----------------------------------------
  super            Super Admin          all municipalities ("Super" also works:
                                        user names are case-insensitive)
  burnabyadmin     Municipality Admin   Burnaby
  surreyadmin      Municipality Admin   Surrey
  richmondadmin    Municipality Admin   Richmond
  pm               Municipality Admin   Pitt Meadows
  mapleridgeadmin  Municipality Admin   Maple Ridge


7. WHAT TO TRY
--------------
- Public: open https://localhost:5001, pick a municipality, search/filter candidates, add
  candidates to "My voting guide" (seat limits apply), try adding a candidate of another
  municipality (confirmation), open the printable guide. /Richmond works; /vancouver shows
  "Municipality not found".
- Admin as pm: only Pitt Meadows appears everywhere; opening another municipality's record by
  URL shows "not found"; the Administrators page is denied.
- Admin as super: "Working in" selector (All municipalities or one tenant), create a
  municipality together with its administrator account, Administrators page, delete a tenant
  (the confirmation lists everything that is deleted).
- API: Swagger UI -> POST /api/auth/login (burnabyadmin) -> Authorize -> call
  /api/surrey/candidates (403) and /api/burnaby/candidates/{a Surrey id} (404).


8. DOCUMENTS
------------
  docs/architecture.md        architecture diagram (Mermaid) and tenant isolation
  docs/erd.md                 entity relationship diagram
  docs/api.md                 API endpoints, request/response examples, error responses
  docs/deficiency-report.md   Assignment 1 deficiency resolution report
  docs/copilot-report.md      GitHub Copilot usage
  docs/test-evidence.md       recorded results of the unit and end-to-end tests
  docs/screenshots.md         screenshot checklist for the submitted document


9. SOURCE ARCHIVE
-----------------
  python tools/make-source-zip.py --verify
creates ../MEMS-A2-source.zip without bin/obj/.git and proves it builds and passes the
tests from a clean extraction.


10. REPOSITORY
-------------
GitHub: https://github.com/hyelim2100/comp4976_assignment
Assignment 1 is preserved in the git tag "assignment-1".
