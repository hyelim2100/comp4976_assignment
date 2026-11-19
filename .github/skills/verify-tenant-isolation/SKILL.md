---
name: verify-tenant-isolation
description: Run the automated checks that prove tenant isolation and election privacy (xUnit + API + admin + public E2E). Use after any change to data access, authorization, routing or the voting guide.
---

# Verify tenant isolation

1. Unit tests (no Docker needed): `dotnet test MEMS.Tests` (add `--no-build` if the AppHost is running —
   the test project references MEMS.Admin, whose DLLs are locked while it runs)
   - factory lifetime and tenant context (`TenantDbContextFactoryTests`, `AdminTenantContextTests`)
   - service rules (`ServiceTenantIsolationTests`, `AdminAccountRuleTests`)
2. Start the system: Docker Desktop running, `dotnet run --project MEMS.AppHost`, wait until api, admin and
   public are Healthy in the Aspire dashboard.
3. End-to-end checks (see `tests/e2e/README.md`):
   - `bash tests/e2e/api-isolation.sh` — 403/404 for other tenants' tokens, ids and slugs
   - `python tests/e2e/admin-scope.py` — what `pm`, `burnabyadmin`, `super` can see
   - `node tests/e2e/admin-e2e.mjs` — interactive admin pages, tenant switcher, re-validation sign-out
   - `node tests/e2e/public-e2e.mjs` — routing, stale data, voting guide privacy (network capture)
4. Every script ends with `TOTAL: n passed, 0 failed`. Paste new results into `docs/test-evidence.md`.
