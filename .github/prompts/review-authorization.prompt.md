---
agent: 'ask'
description: 'Review admin controllers for municipality-scope authorization gaps'
---

Review the controllers in `MunicipalElections/Areas/Admin/Controllers` (or the file I have open) for
authorization gaps. For each action, check:

- Is the action covered by the `AdminAccess` policy (inherits `AdminControllerBase`)?
- Does every action that loads a specific record call `MunicipalityAccess.CanManageAsync` before
  returning data or saving — in both the GET and the POST version?
- Are submitted `MunicipalityId` / `PositionId` values validated, so a municipality admin cannot
  create or move records into another municipality by editing the form?
- Are Super-Admin-only operations (create/delete/rename municipality) restricted with
  `[Authorize(Roles = AppRoles.SuperAdmin)]` or an explicit check?
- Are POST actions protected with `[ValidateAntiForgeryToken]`?
- Can a user registered through `/api/auth/register` (no admin role) reach any admin page?

Report findings as a table: action, problem, concrete request that exploits it, suggested fix.
