---
agent: 'ask'
description: 'Review code for tenant-isolation and election-privacy gaps (EF, services, API, Admin, Public)'
---

Review the open file (or the changes on this branch) for multi-tenant isolation and election privacy.

Check:
- **EF:** is every tenant-owned entity covered by a query filter? Is `IgnoreQueryFilters()` used anywhere
  that is not Super-Admin-only or the public directory? Are `Where`/`OrderBy` applied before DTO projection?
- **Services:** is the target tenant taken from `ITenantContext` (not from input)? Does another tenant's id
  return NotFound? Do Super-Admin-only operations check `tenant.IsSuperAdmin`?
- **API:** is the route `/api/{tenant}/...`? Can a Municipality Admin token for one tenant reach another
  tenant's data (403/404 expected)? Are write actions `[Authorize]`d?
- **Admin:** can a Municipality Admin see another tenant in a list, dropdown, dashboard count, detail URL,
  image URL or link? Is any lifetime (singleton factory, cached tenant) able to mix users?
- **Public / privacy:** is any voting-guide data stored outside `sessionStorage` or sent in an HTTP request?
  Can data of the previous tenant stay on screen after the slug changes?

Report findings as a table: location, problem, concrete request or steps that exploit it, suggested fix.
