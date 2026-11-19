# GitHub Copilot usage report (draft)

> **TODO (team):** complete the sections marked TODO with your own experience before submitting.
> Only describe what actually happened.

## 1. How Copilot was used

The repository gives Copilot project-specific context through the files in `.github/`:

| File | Applies to | Purpose |
|---|---|---|
| `copilot-instructions.md` | whole repository | Architecture, project layout, the eight rules that must always hold (tenant isolation in three layers, set-once tenant context, election privacy, account rule, three-layer validation, ProblemDetails, no secrets, migrations), conventions, build/test commands |
| `instructions/data-model.instructions.md` | `MEMS.Data/**` | Query filters, composite FK, service pattern, migrations, idempotent seeding, no ballot tables |
| `instructions/api.instructions.md` | `MEMS.Api/**` | Tenant routes, authorization attributes, ProblemDetails, XML documentation |
| `instructions/admin-blazor.instructions.md` | `MEMS.Admin/**` | Services only, `NotFoundCard`, forms with `DataAnnotationsValidator` + `FormErrors`, no guide feature |
| `instructions/public-wasm.instructions.md` | `MEMS.Public.Client/**` | GET-only API client, `TenantShell`, stale-data rule, sessionStorage-only guide |
| `prompts/add-admin-page.prompt.md` | agent | Generate tenant-scoped admin pages following the Positions pages |
| `prompts/change-data-model.prompt.md` | agent | Entity → DbContext → migration → DTO → service → seed → UI → test |
| `prompts/review-tenant-isolation.prompt.md` | ask | Review a file/branch for isolation and privacy gaps, output as a table |
| `skills/seed-data/SKILL.md` | skill | How to change seed tenants, images and accounts |
| `skills/verify-tenant-isolation/SKILL.md` | skill | Run the unit tests and the four E2E scripts |

TODO (team): describe how you used Copilot day to day (chat, inline completions, agent mode, which model),
and which of the files above you used for which tasks.

## 2. Example prompts

Prompts kept in the repository (run from Copilot Chat with `/` + prompt name):

- `/change-data-model` — e.g. *"Add a Tagline (max 150) and PrimaryColor (#RRGGBB) to Municipality and show them on the tenant home page."*
- `/add-admin-page` — e.g. *"Candidates"* → list/details/create/edit/delete pages with the tenant-scoped pattern.
- `/review-tenant-isolation` — run on `MEMS.Data/Services/MunicipalityService.cs` or on the branch diff.

TODO (team): add 3–5 real prompts you typed, with what Copilot produced and what you changed.

## 3. Productivity improvements

TODO (team): describe where Copilot saved time, for example boilerplate CRUD pages, XML documentation of API
actions, DTO/record definitions, seed data, test cases, Bootstrap layouts.

## 4. Problems encountered (and how they were caught)

These problems occurred during development of Assignment 2 with AI-generated code; each was caught by a test or
by running the system, and is now covered by an automated check:

| Problem | How it was found | Fix / safeguard |
|---|---|---|
| The tenant was resolved before the user was known: the cookie security-stamp check creates the DbContext early, so every admin saw 0 rows. | `admin-scope.py` showed "(no municipality)" for every account. | Lazy, set-once tenant resolution (`AdminTenantResolver`); regression test `DbContext_created_before_sign_in_filters_by_the_user_tenant_once_known`. |
| A Super Admin's filter bypass would have mixed all tenants on `/api/burnaby/...`. | Design review of the requirement "Super Admin on /api/burnaby sees Burnaby only". | Tenant context separates *who* (Super Admin) from *scope* (tenant); tests `Super_admin_scoped_to_a_tenant_sees_only_that_tenant`. |
| A LINQ query filtered and sorted on a DTO record after projection; it worked with the EF in-memory provider in unit tests but failed on SQL Server. | The administrators page showed the error message in the browser E2E run. | Filter/sort before projecting; rule added to `copilot-instructions.md`. |
| The SQL check constraint for lowercase slugs accepted "Upper" because the default collation is case-insensitive. | Direct `INSERT` test against SQL Server. | `COLLATE Latin1_General_BIN2` in the constraint. |
| A generated E2E selector `button[type=submit]` clicked the **Log out** button in the nav menu first. | Browser test landed on the login page. | Selectors scoped to the page content (`article ...`). |
| Generated code assumed the browser culture; dates printed in Korean on a Korean Windows machine. | Public E2E output. | The WebAssembly app runs with `en-US` culture. |

TODO (team): add problems you met with Copilot suggestions yourself (wrong APIs, outdated .NET versions,
insecure suggestions, hallucinated packages, ...).

## 5. Lessons learned

TODO (team).
