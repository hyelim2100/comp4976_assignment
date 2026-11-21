# Screenshot checklist for the submitted document

Start everything first: Docker Desktop running → `dotnet run --project MEMS.AppHost` → open the dashboard link
printed in the console (it contains a login token). Wait until `api`, `admin` and `public` are **Healthy**.
Windows: `Win + Shift + S` (area) or a browser's full-page capture (Edge: `Ctrl + Shift + S`).
Use a private window for each account so sessions do not mix.

## 1. Aspire dashboard

| # | Screenshot | How |
|---|---|---|
| 1.1 | **Resources** | Dashboard → *Resources*. Show `sql`, `memsdb`, `api`, `admin`, `public` all *Running* + *Healthy*, with the URL column (Swagger UI, Admin site, Public site). |
| 1.2 | **Health checks** | Click `api` → details panel → *Health checks* (`api_https_/health_200_check`, database check) = Healthy. Optionally open `https://localhost:7201/health` → `Healthy`. |
| 1.3 | **Structured logs** | *Structured logs* → resource `api` after the 403 test in 4.3: `Warning` entries "Cross-tenant request blocked: user burnabyadmin (municipality 1) ..." next to the login entries (open one to see `UserName`, `UserMunicipalityId`, `Path` as properties). |
| 1.4 | **Traces** | *Traces* list (a), then open a `public: GET /api/{**catch-all}` trace (b): the span tree shows public (YARP proxy) → api `GET api/{tenant}/candidates` → SQL Server queries. |
| 1.5 | **Metrics** (optional) | *Metrics* → `api` → `http.server.request.duration`. |
| 1.6 | **Service discovery / config** (optional) | `public` → *Environment variables*: `services__api__https__0`; `api` → `ConnectionStrings__memsdb` (value hidden). |

## 2. Admin site (`https://localhost:7101/admin`)

| # | Screenshot | How |
|---|---|---|
| 2.1 | Login page | Signed out, `/admin/Account/Login`. |
| 2.2 | **Super Admin dashboard** | `super` → Dashboard shows 5 / 15 / 22 and the *Working in: All municipalities* selector. |
| 2.3 | Working tenant | Choose *Burnaby* in the selector → dashboard 1 / 3 / 5, candidates list only Burnaby. |
| 2.4 | **Create municipality + admin account** | *Municipalities → Create municipality*; fill the tenant and the *Municipality Admin account* sections (show a validation error first, e.g. slug "admin" reserved). |
| 2.5 | **Administrators page** | *Administrators* list (role, municipality) and the *Edit / assign* form. |
| 2.6 | **Delete impact** | *Municipalities → Delete* on a tenant (here Maple Ridge, view only — nothing is deleted): counts of positions, candidates, images and the admin accounts that would be removed. |
| 2.7 | **pm dashboard** | Private window, `pm` → badge *Pitt Meadows*, dashboard 1 / 3 / 5. |
| 2.8 | **pm lists and dropdowns** | `pm` → Candidates (only Pitt Meadows) (a) and the *Add candidate* form (b); opening its position dropdown lists only Pitt Meadows positions (c). |
| 2.9 | **pm direct URL** | `pm` → type `/admin/candidates/1` (a Burnaby candidate) → "Candidate not found"; `/admin/users` → "Access denied". |
| 2.10 | Candidate form with photo | Edit a candidate: photo preview + validation messages (e.g. invalid email). |

## 3. Public site (`https://localhost:5001`)

| # | Screenshot | How |
|---|---|---|
| 3.1 | **Browse municipalities** | `/` with all tenant cards. |
| 3.2 | **Tenant home ×2** | `/burnaby` and `/surrey` (different colours, logos, taglines, election date). |
| 3.3 | Case-insensitive / not found | `/Richmond` opens the Richmond site (the URL is normalized to `/richmond`) (a) and `/vancouver` shows "Municipality not found" (b). |
| 3.4 | **Candidates with search/filter** | `/burnaby/candidates`, type a name, select a position. |
| 3.5 | Candidate details | Click a candidate (photo, biography, *Add to my guide*). |
| 3.6 | **Voting guide** | Add a Mayor and a Councillor → *My voting guide* with seat counts (a); on the candidates page the other Mayor shows the disabled *Seats full (1/1)* button (b). |
| 3.7 | **One municipality per guide** | Go to `/surrey/candidates`, *Add to my guide* → the *Start a new voting guide?* dialog. |
| 3.8 | **Session storage** | `F12` → *Application* → *Session storage* → `https://localhost:5001` → key `mems.votingGuide` (ids only). *Local storage* has no app data. |
| 3.9 | **No data sent** (optional, strong evidence) | `F12` → *Network*, filter *Fetch/XHR*, add/remove guide items: only `GET /api/...` requests appear. |
| 3.10 | **Printable guide** | *Printable guide* page (municipality, election date, candidates, positions, print date) (a) and its `Ctrl + P` print preview, where the buttons are hidden (b). |
| 3.11 | Mobile | `F12` → device toolbar (iPhone) on `/burnaby/candidates`. |

## 4. API testing (`https://localhost:7201/swagger`)

| # | Screenshot | How |
|---|---|---|
| 4.1 | **Swagger UI** | Endpoint list (municipalities, positions, candidates, images, auth). |
| 4.2 | Login | `POST /api/auth/login` with `{"userName":"burnabyadmin","password":"P@$$w0rd"}` → 200 with token (a) → *Authorize* with the token (b). |
| 4.3 | **403** | `POST /api/{tenant}/candidates` with tenant `surrey` (still authorized as burnabyadmin) → 403 ProblemDetails. Swagger UI sends the token only to operations that require authentication, so use a write operation; a GET with a token of another tenant also returns 403 (see `api-isolation.sh`). |
| 4.4 | **404** | `GET /api/{tenant}/candidates/{id}` with tenant `burnaby` and a Surrey candidate id (6) → 404 (a); `GET /api/{tenant}/positions` with tenant `vancouver` → 404 (b). |
| 4.5 | 400 validation | `POST /api/{tenant}/candidates` (burnaby) with `{"name":"","positionId":1,"email":"x"}` → 400 `errors`. |
| 4.6 | 401 | *Logout* in the Authorize dialog, `POST /api/{tenant}/positions` → 401. |
| 4.7 | Script results | Terminal: `bash tests/e2e/api-isolation.sh` ending with `TOTAL: 36 passed, 0 failed`. |

## 5. SQL Server database

The SQL password is in the AppHost user-secrets: `dotnet user-secrets list --project MEMS.AppHost`
(`Parameters:sql-password`). The host port changes per run — read it in the dashboard (`sql` → endpoint `tcp`)
or with `docker ps` (e.g. `127.0.0.1:30767->1433`).

**Option A — VS Code (MSSQL extension) or SQL Server Management Studio / Azure Data Studio**
Server `127.0.0.1,<port>`, SQL Login `sa`, password from user-secrets, *Trust server certificate* on, database `memsdb`.

**Option B — command line inside the container**
```bash
docker exec -it <sql container> /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "<password>" -C -d memsdb
```

| # | Screenshot | Query / view |
|---|---|---|
| 5.1 | **Tables** | Object explorer of `memsdb`: Municipalities, Positions, Candidates, Images, AspNetUsers, AspNetRoles, AspNetUserRoles, __EFMigrationsHistory (plus unused Identity tables). There is no table for voting-guide selections or vote totals. |
| 5.2 | **Tenants** | `SELECT Id, Name, Slug, PrimaryColor, ElectionDate FROM Municipalities;` |
| 5.3 | **Tenant data** | `SELECT m.Slug, p.Name AS Position, c.Name AS Candidate FROM Candidates c JOIN Positions p ON p.Id = c.PositionId JOIN Municipalities m ON m.Id = c.MunicipalityId ORDER BY m.Slug, p.Name;` |
| 5.4 | **Administrators and tenants** | `SELECT u.UserName, r.Name AS Role, m.Slug FROM AspNetUsers u JOIN AspNetUserRoles ur ON ur.UserId = u.Id JOIN AspNetRoles r ON r.Id = ur.RoleId LEFT JOIN Municipalities m ON m.Id = u.MunicipalityId;` |
| 5.5 | Constraints (optional) | `SELECT name FROM sys.foreign_keys WHERE parent_object_id = OBJECT_ID('Candidates'); SELECT name, definition FROM sys.check_constraints;` |
| 5.6 | Migrations | `SELECT * FROM __EFMigrationsHistory;` |
