# System architecture

MEMS is one multi-tenant system: every municipality is a **tenant** identified by a URL slug. Adding a
municipality is a data change made in the admin site — no new application, project or deployment.

```mermaid
flowchart TB
    subgraph Browser["Browsers"]
        AdminUser["Administrators<br/>super · burnabyadmin · surreyadmin · richmondadmin · pm"]
        Voter["Voters"]
    end

    subgraph Aspire[".NET Aspire AppHost"]
        direction TB

        Admin["<b>MEMS.Admin</b><br/>shared Blazor Server admin<br/>localhost:7101/admin<br/>Identity cookies · tenant from user / working tenant"]

        subgraph PublicSite["MEMS.Public host :5001"]
            direction TB
            Wasm["<b>MEMS.Public.Client</b><br/>Blazor WebAssembly (runs in the browser)<br/>voting guide in sessionStorage only"]
            Proxy["YARP proxy for /api/*<br/>→ service name api<br/>(Aspire service discovery)"]
        end

        subgraph Tenants["Tenant sites (same app)"]
            direction LR
            Burnaby["/burnaby<br/>purple · Burnaby data only"]
            Surrey["/surrey<br/>teal · Surrey data only"]
            Richmond["/richmond<br/>red · Richmond data only"]
        end

        Api["<b>MEMS.Api</b> — ASP.NET Web API (localhost:7201)<br/>/api/municipalities<br/>/api/{tenant}/municipality · positions · candidates · images<br/>TenantResolutionFilter · JWT · ProblemDetails · Swagger"]

        Data["<b>MEMS.Data</b> (library)<br/>EF Core · services · TenantContext<br/>global query filters by MunicipalityId"]

        Sql[("SQL Server container + volume<br/>database memsdb")]
    end

    AdminUser -->|HTTPS + SignalR circuit| Admin
    Voter --> Wasm
    Wasm --- Tenants
    Wasm -->|GET /api/... same origin| Proxy
    Proxy -->|service discovery| Api
    Admin -->|services, no HTTP hop| Data
    Api --> Data
    Data -->|connection string from Aspire| Sql
    Aspire -. "OpenTelemetry logs · traces · metrics · /health" .-> Dashboard["Aspire dashboard"]
```

**Why `MEMS.Public.Client` is a separate project:** WebAssembly code must be compiled into its own
Blazor WebAssembly project; `MEMS.Public` is the ASP.NET Core host that serves it and proxies `/api/*` to
the API, because a browser cannot resolve Aspire service-discovery names.

## Tenant isolation (enforced three times)

```mermaid
flowchart LR
    R["Request<br/>/api/surrey/candidates/6<br/>Bearer token of burnabyadmin"] --> F{"API: TenantResolutionFilter<br/>slug exists?<br/>user's tenant = route tenant?"}
    F -->|unknown slug| N404["404 ProblemDetails"]
    F -->|other tenant's admin| N403["403 ProblemDetails"]
    F -->|ok: scope bound to Surrey| S["Service layer<br/>tenant from ITenantContext,<br/>never from the body"]
    S --> Q["EF global query filter<br/>WHERE MunicipalityId = @tenant"]
    Q --> DB[("SQL Server<br/>composite FK (PositionId, MunicipalityId)")]
```

| Layer | Mechanism | What it prevents |
|---|---|---|
| Blazor Server admin | `AdminTenantResolver`: Municipality Admin fixed to the `municipality_id` claim; Super Admin "All" or a working tenant (claim in the cookie). Re-validated every minute. | A Municipality Admin seeing or editing another tenant in any list, dropdown, dashboard, URL or image. |
| Web API | `TenantResolutionFilter` binds every `/api/{tenant}/...` request to the route tenant; other-tenant admin tokens get 403; JWT security-stamp check. | Changing the URL or request body to reach another tenant. |
| EF Core | `TenantContext` (scoped, set once, fail closed) read by global query filters; `IDbContextFactory` registered **scoped** so each context gets the tenant of its own request/circuit. | A forgotten `Where` clause leaking rows; tenants mixed between users. |
| Database | Unique slug, composite FK `Candidate(PositionId, MunicipalityId) → Position(Id, MunicipalityId)`, check constraints. | Inconsistent rows even if application code is wrong. |

## Request paths

| Who | Path |
|---|---|
| Voter | Browser → MEMS.Public (static WASM files) → WASM app → `GET /api/{tenant}/...` → YARP → MEMS.Api → MEMS.Data → SQL Server |
| Administrator | Browser → MEMS.Admin (Blazor Server circuit) → MEMS.Data services → SQL Server |
| Other clients | → MEMS.Api with JWT from `POST /api/auth/login` |

## Election privacy

Voting-guide selections never leave the voter's browser: they are kept in `sessionStorage` (ids only),
the WebAssembly API client has GET methods only, and the database has no table for selections or totals.
The E2E check `tests/e2e/public-e2e.mjs` records every network request to prove it.

## Aspire

| Resource | Type | References / waits for | Health check |
|---|---|---|---|
| `sql` | SQL Server container, volume `mems-sql-data`, persistent | — | SQL connection |
| `memsdb` | database | `sql` | database connection |
| `api` | MEMS.Api | `memsdb` (WaitFor), parameter `jwt-key` | `/health` |
| `admin` | MEMS.Admin | `memsdb`, waits for `api` (migrations + seed done) | `/health` |
| `public` | MEMS.Public | `api` (service discovery) | `/health` |

`MEMS.ServiceDefaults` adds OpenTelemetry (logs, traces incl. EF Core/SQL and HTTP, metrics), `/health`
and `/alive`, service discovery and HTTP resilience to every project.
