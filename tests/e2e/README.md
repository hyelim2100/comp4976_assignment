# End-to-end checks

Black-box checks against the running system. They complement the xUnit tests in `MEMS.Tests`
(service layer and tenant context) by exercising the real apps: SQL Server, the Web API, the Blazor
Server admin site (circuits included) and the Blazor WebAssembly public site.

## Prerequisites

1. Docker Desktop running, then the AppHost started: `dotnet run --project MEMS.AppHost`
   (API `https://localhost:7201`, Admin `https://localhost:7101/admin`, Public `https://localhost:5001`).
2. Node.js 22+ (built-in `fetch` and `WebSocket`; no npm packages), Python 3, bash + curl.
3. Microsoft Edge or Google Chrome (run headless). Set `BROWSER` to the executable if it is not found.

## Scripts

| Script | What it proves |
|---|---|
| `api-isolation.sh` | API tenant isolation: another tenant's admin token gets 403, another tenant's ids 404, unknown slugs 404, Super Admin on `/api/burnaby/...` sees Burnaby only, image signature validation, ProblemDetails bodies. |
| `admin-scope.py` | What `pm`, `burnabyadmin` and `super` can see in the admin site: dashboard totals, lists, dropdowns, direct URLs of other tenants, Super-Admin-only pages, other tenants' images. Scans every page for other tenants' names. |
| `admin-e2e.mjs` | Interactive admin pages in a headless browser: form validation, slug lowercasing, tenant creation with its admin account, "All municipalities" requires choosing a tenant, working-tenant switcher, delete-impact screen, and sign-out of an open circuit after the admin is deleted (re-validation). |
| `public-e2e.mjs` | Public WebAssembly site: tenant routing (`/Richmond`, unknown slug), `/burnaby` -> `/surrey` shows no stale data, search/filter, voting guide in sessionStorage only (network capture proves nothing is sent to the server), one tenant per guide with replace confirmation, seat limits, print page. |

```bash
bash tests/e2e/api-isolation.sh
python tests/e2e/admin-scope.py
node tests/e2e/admin-e2e.mjs
node tests/e2e/public-e2e.mjs
```

`admin-e2e.mjs` creates a temporary tenant "Coquitlam" (with `coquitlamadmin`) and deletes it at the end;
`api-isolation.sh` creates and deletes a temporary candidate and position. Seed data is left unchanged.

The recorded output of a full run is in [`docs/test-evidence.md`](../../docs/test-evidence.md).
