# Test evidence

Recorded on **2026-11-19** against a **fresh environment**: the SQL Server container and the `mems-sql-data`
volume were deleted, then `dotnet run --project MEMS.AppHost` created a new container, the API applied the
`InitialCreate` migration and seeded the data (all resources Healthy after 204 s, including container start).
Scripts and instructions: [`tests/e2e`](../tests/e2e/README.md).

| Suite | Result |
|---|---|
| Fresh database: migration + seed | `20261118173947_InitialCreate`; 5 municipalities, 15 positions, 22 candidates, 27 images, 6 accounts |
| `MEMS.Tests` (xUnit) | **33 passed, 0 failed** |
| `tests/e2e/api-isolation.sh` | **36 passed, 0 failed** |
| `tests/e2e/admin-scope.py` | see table (no other-tenant data for `pm` / `burnabyadmin`) |
| `tests/e2e/admin-e2e.mjs` | **25 passed, 0 failed** |
| `tests/e2e/public-e2e.mjs` | **35 passed, 0 failed** |
| Source ZIP (`python tools/make-source-zip.py --verify`) | extracted into an empty folder: SDK 10.0.401 selected by `global.json`, build 0 warnings / 0 errors, 33 tests passed |

Note: in the first run of `public-e2e.mjs` on the fresh environment the very first check ("browse municipalities")
failed because the first page load of a new browser profile (WebAssembly runtime download) took longer than the
script's 20 s wait; the two following runs passed all 35 checks. The wait for that first page was raised to 60 s
and the suite was run again — the output below is from that run.

## Source archive check (Assignment 1 deficiency #5)

The submission archive is created from the working tree without `bin/`, `obj/`, `.vs/`, `.git/`, `node_modules/` and
`_spec/` (255 files, 2.6 MB), extracted into an empty folder and built and tested there (the test machine prints
these messages in Korean; shown here in English):

```
> dotnet --version
10.0.401
> dotnet build MEMS.sln -nologo -v q
    0 Warning(s)
    0 Error(s)
> dotnet test MEMS.Tests --no-build -nologo -v q
Passed!  - Failed: 0, Passed: 33, Skipped: 0, Total: 33 - MEMS.Tests.dll (net10.0)
```

## Fresh database contents

```
Municipalities Positions Candidates Images Users
5              15        22         27     6

UserName        Role               Tenant
super           Super Admin        (all)
burnabyadmin    Municipality Admin burnaby
mapleridgeadmin Municipality Admin maple-ridge
pm              Municipality Admin pitt-meadows
richmondadmin   Municipality Admin richmond
surreyadmin     Municipality Admin surrey

Tables: __EFMigrationsHistory, AspNetRoleClaims, AspNetRoles, AspNetUserClaims, AspNetUserLogins,
AspNetUserRoles, AspNetUsers, AspNetUserTokens, Candidates, Images, Municipalities, Positions
(no table for voting-guide selections)
```

## 1. Unit tests (`dotnet test MEMS.Tests`)

| Result | Test | Time |
|---|---|---|
| Passed | `AdminAccountRuleTests.Input_model_enforces_the_role_tenant_rule(role: "Guest", municipalityId: null, valid: False)` | < 1 ms |
| Passed | `AdminAccountRuleTests.Input_model_enforces_the_role_tenant_rule(role: "Municipality Admin", municipalityId: 1, valid: True)` | 48 ms |
| Passed | `AdminAccountRuleTests.Input_model_enforces_the_role_tenant_rule(role: "Municipality Admin", municipalityId: null, valid: False)` | 2 ms |
| Passed | `AdminAccountRuleTests.Input_model_enforces_the_role_tenant_rule(role: "Super Admin", municipalityId: 1, valid: False)` | < 1 ms |
| Passed | `AdminAccountRuleTests.Input_model_enforces_the_role_tenant_rule(role: "Super Admin", municipalityId: null, valid: True)` | < 1 ms |
| Passed | `AdminAccountRuleTests.Only_super_admin_can_manage_accounts` | 21 ms |
| Passed | `AdminAccountRuleTests.Service_creates_valid_municipality_admin_and_lists_it` | 338 ms |
| Passed | `AdminAccountRuleTests.Service_rejects_municipality_admin_without_tenant_even_when_ui_validation_is_bypassed` | 4 s |
| Passed | `AdminTenantContextTests.Admin_without_tenant_claim_stays_unresolved` | 2 ms |
| Passed | `AdminTenantContextTests.Anonymous_scope_is_unresolved_and_sees_no_tenant` | 2 ms |
| Passed | `AdminTenantContextTests.Circuit_authentication_state_is_preferred_over_HttpContext` | 2 ms |
| Passed | `AdminTenantContextTests.DbContext_created_before_sign_in_filters_by_the_user_tenant_once_known` | 2 s |
| Passed | `AdminTenantContextTests.Municipality_admin_is_fixed_to_own_tenant_and_ignores_a_working_tenant_claim` | 4 ms |
| Passed | `AdminTenantContextTests.Once_resolved_the_tenant_does_not_follow_a_different_user` | 2 ms |
| Passed | `AdminTenantContextTests.Super_admin_with_working_tenant_is_scoped_to_it` | 1 s |
| Passed | `AdminTenantContextTests.Super_admin_without_working_tenant_sees_all` | 1 ms |
| Passed | `AdminTenantContextTests.Tenant_is_not_frozen_while_the_user_is_still_unknown` | 1 ms |
| Passed | `ServiceTenantIsolationTests.Candidate_cannot_be_attached_to_another_tenants_position` | 10 ms |
| Passed | `ServiceTenantIsolationTests.Candidate_takes_its_tenant_from_the_position` | 4 s |
| Passed | `ServiceTenantIsolationTests.Municipality_admin_cannot_change_slug_create_or_delete_tenants` | 31 ms |
| Passed | `ServiceTenantIsolationTests.Municipality_admin_creates_positions_only_in_own_tenant_whatever_is_posted` | 9 ms |
| Passed | `ServiceTenantIsolationTests.Municipality_admin_only_sees_own_tenant_in_lists_and_dashboard` | 113 ms |
| Passed | `ServiceTenantIsolationTests.Other_tenant_ids_are_not_found` | 42 ms |
| Passed | `ServiceTenantIsolationTests.Public_directory_and_slug_resolution_are_case_insensitive` | 44 ms |
| Passed | `ServiceTenantIsolationTests.Scoped_super_admin_writes_go_to_the_route_tenant` | 63 ms |
| Passed | `ServiceTenantIsolationTests.Unscoped_super_admin_must_choose_a_municipality_for_a_new_position` | 10 ms |
| Passed | `TenantDbContextFactoryTests.Contexts_created_by_the_factory_get_the_tenant_of_their_own_scope` | 47 ms |
| Passed | `TenantDbContextFactoryTests.Each_context_from_the_same_factory_shares_the_scope_tenant` | 23 ms |
| Passed | `TenantDbContextFactoryTests.Factory_is_registered_scoped_and_cannot_be_resolved_from_the_root_provider` | 10 ms |
| Passed | `TenantDbContextFactoryTests.Super_admin_scope_sees_every_tenant` | 19 ms |
| Passed | `TenantDbContextFactoryTests.Super_admin_scoped_to_a_tenant_sees_only_that_tenant` | 3 s |
| Passed | `TenantDbContextFactoryTests.Tenant_context_cannot_be_switched_to_another_tenant_once_set` | 13 ms |
| Passed | `TenantDbContextFactoryTests.Unresolved_tenant_fails_closed_and_sees_no_rows` | 14 ms |

## 2. API tenant isolation (`bash tests/e2e/api-isolation.sh`)

### A. Admin token of another tenant (burnabyadmin -> Surrey, pm -> Burnaby)
| Scenario | Request | Expected | Actual | Result |
|---|---|---|---|---|
| burnabyadmin: list Surrey candidates | `GET /api/surrey/candidates` | 403 | 403 | ✅ |
| burnabyadmin: get Surrey candidate | `GET /api/surrey/candidates/6` | 403 | 403 | ✅ |
| burnabyadmin: create candidate in Surrey | `POST /api/surrey/candidates` | 403 | 403 | ✅ |
| burnabyadmin: update Surrey candidate | `PUT /api/surrey/candidates/6` | 403 | 403 | ✅ |
| burnabyadmin: delete Surrey candidate | `DELETE /api/surrey/candidates/6` | 403 | 403 | ✅ |
| burnabyadmin: create position in Surrey | `POST /api/surrey/positions` | 403 | 403 | ✅ |
| burnabyadmin: update Surrey municipality | `PUT /api/surrey/municipality` | 403 | 403 | ✅ |
| pm: delete Burnaby position | `DELETE /api/burnaby/positions/1` | 403 | 403 | ✅ |
| burnabyadmin: own route + Surrey candidate id (GET) | `GET /api/burnaby/candidates/6` | 404 | 404 | ✅ |
| burnabyadmin: own route + Surrey candidate id (PUT) | `PUT /api/burnaby/candidates/6` | 404 | 404 | ✅ |
| burnabyadmin: own route + Surrey candidate id (DELETE) | `DELETE /api/burnaby/candidates/6` | 404 | 404 | ✅ |
| burnabyadmin: own route + Surrey position id (PUT) | `PUT /api/burnaby/positions/4` | 404 | 404 | ✅ |
| burnabyadmin: candidate with a Surrey positionId in the body | `POST /api/burnaby/candidates` | 400 | 400 | ✅ |
| burnabyadmin: delete tenant (Super Admin only) | `DELETE /api/burnaby/municipality` | 403 | 403 | ✅ |
| burnabyadmin: create tenant (Super Admin only) | `POST /api/municipalities` | 403 | 403 | ✅ |
| write without a token | `POST /api/burnaby/positions` | 401 | 401 | ✅ |
| forged / invalid token | `POST /api/burnaby/positions` | 401 | 401 | ✅ |

### B. Anonymous public GETs and tenant resolution
| Scenario | Request | Expected | Actual | Result |
|---|---|---|---|---|
| municipality directory (not tenant-filtered) | `GET /api/municipalities` | 200 | 200 | ✅ |
| Burnaby candidates | `GET /api/burnaby/candidates` | 200 | 200 | ✅ |
| mixed-case slug /Richmond | `GET /api/Richmond/positions` | 200 | 200 | ✅ |
| unknown slug | `GET /api/vancouver/candidates` | 404 | 404 | ✅ |
| candidate id of another tenant | `GET /api/burnaby/candidates/6` | 404 | 404 | ✅ |
| Burnaby logo on the Burnaby route | `GET /api/burnaby/images/1c914151-5680-425c-af97-21e1e6dc3a58` | 200 | 200 | ✅ |
| Burnaby logo on the Surrey route | `GET /api/surrey/images/1c914151-5680-425c-af97-21e1e6dc3a58` | 404 | 404 | ✅ |
| through the Public host proxy | `GET https://localhost:5001/api/burnaby/municipality` | 200 | 200 | ✅ |
| login with a wrong password | `POST /api/auth/login` | 401 | 401 | ✅ |
| Swagger UI | `GET /swagger/index.html` | 200 | 200 | ✅ |
| OpenAPI document | `GET /openapi/v1.json` | 200 | 200 | ✅ |

### C. Own-tenant writes and Super Admin scope
| Scenario | Request | Expected | Actual | Result |
|---|---|---|---|---|
| burnabyadmin: update own candidate | `PUT /api/burnaby/candidates/23` | 204 | 204 | ✅ |
| validation error (email / URL format) | `POST /api/burnaby/candidates` | 400 | 400 | ✅ |
| burnabyadmin: upload a real PNG photo | `PUT /api/burnaby/candidates/23/photo` | 204 | 204 | ✅ |
| fake image (text file named .png) | `PUT /api/burnaby/candidates/23/photo` | 400 | 400 | ✅ |
| burnabyadmin: delete own candidate | `DELETE /api/burnaby/candidates/23` | 204 | 204 | ✅ |
| Super (capital-S login): Burnaby candidates | `GET /api/burnaby/candidates` | 200 | 200 | ✅ |
| Super: Burnaby route + Surrey candidate id | `GET /api/burnaby/candidates/6` | 404 | 404 | ✅ |
| Super: POST /api/surrey/positions with municipalityId=1 (Burnaby) | -> stored in tenant | surrey | surrey | ✅ |
| Super: delete that position | `DELETE /api/surrey/positions/16` | 204 | 204 | ✅ |

### D. Response contents
- municipalitySlug values in /api/burnaby/candidates with a Super token: ['burnaby'] (count 5)
- municipalitySlug values in anonymous /api/burnaby/candidates: ['burnaby']
- /api/municipalities count: 5
- 403 response body: `{"type":"https://tools.ietf.org/html/rfc9110#section-15.5.4","title":"Access to this municipality is not allowed","status":403,"detail":"Municipality administrators can only access the data of their own municipality.","traceId":"00-16e76769ceea700c013750df1e16ecce-a1a662333f11b654-01"}`
- 400 response body: `{"type":"https://tools.ietf.org/html/rfc9110#section-15.5.1","title":"One or more validation errors occurred.","status":400,"errors":{"Name":["The Name field is required."],"Email":["The Email field is not a valid e-mail address."]},"traceId":"00-2562145b0bdaf1fc8705fa7031080566-fc2d86bac91f3a08-01"}`

**TOTAL: 36 passed, 0 failed**

## 3. What each administrator can see (`python tests/e2e/admin-scope.py`)

Pages are fetched after signing in through the real login form; every page's text is searched for the names of
other municipalities and their candidates.

| Check | pm | burnabyadmin | Super (All municipalities) | Super (working in Burnaby) |
|---|---|---|---|---|
| Dashboard (municipalities / positions / candidates) | 1, 3, 5 | 1, 3, 5 | 5, 15, 22 | 1, 3, 5 |
| scope | Pitt Meadows | Burnaby | All municipalities | Burnaby |
| municipality list | Pitt Meadows | Burnaby | Burnaby, Maple Ridge, Pitt Meadows, Richmond, Surrey | Burnaby, Maple Ridge, Pitt Meadows, Richmond, Surrey |
| positions: municipalities | Pitt Meadows | Burnaby | Burnaby, Maple Ridge, Pitt Meadows, Richmond, Surrey | Burnaby |
| candidates: municipalities | Pitt Meadows | Burnaby | Burnaby, Maple Ridge, Pitt Meadows, Richmond, Surrey | Burnaby |
| candidate count | 5 | 5 | 22 | 5 |
| position dropdown groups | Pitt Meadows | Burnaby | Burnaby, Maple Ridge, Pitt Meadows, Richmond, Surrey | Burnaby |
| candidate filter: municipality dropdown | (not shown) | (not shown) | All municipalities, Burnaby, Maple Ridge, Pitt Meadows, Richmond, Surrey | – |
| direct URLs | candidates/2 (burnaby): not found<br>candidates/2/edit: not found<br>candidates/2/delete: not found<br>positions/1/edit (burnaby): not found<br>municipalities/1 (burnaby): not found<br>municipalities/1/edit: not found | candidates/20 (maple-ridge): not found<br>candidates/20/edit: not found<br>candidates/20/delete: not found<br>positions/13/edit (maple-ridge): not found<br>municipalities/5 (maple-ridge): not found<br>municipalities/5/edit: not found | – | – |
| super-admin pages | users: access denied<br>municipalities/create: access denied<br>municipalities/1/delete: access denied | users: access denied<br>municipalities/create: access denied<br>municipalities/5/delete: access denied | – | – |
| other tenant logo /images | 404 | 404 | – | – |
| select-tenant (super only) | 302 -> Account/AccessDenied?ReturnUrl=%2Fadmin%2Fselect-tenant%3FmunicipalityId%3D1 | 302 -> Account/AccessDenied?ReturnUrl=%2Fadmin%2Fselect-tenant%3FmunicipalityId%3D1 | – | – |
| leaked other-tenant names (all pages) | (none) | (none) | – | – |
| guide/ballot words in admin HTML | (none) | (none) | – | – |

## 4. Interactive admin site (`node tests/e2e/admin-e2e.mjs`)

```
# MEMS.Admin E2E (headless browser, Blazor Server circuits)
# 2026-11-19T21:46:48.276Z

PASS | Login: empty submit shows validation messages | The User name field is required. / The Password field is required.
PASS | Login with "Super" (capital S) = account "super" | https://localhost:7101/admin/
PASS | All mode: position form shows a municipality dropdown
PASS | All mode: saving without a municipality is rejected | Please choose a municipality.
PASS | All mode: candidate position dropdown grouped by municipality | Burnaby, Maple Ridge, Pitt Meadows, Richmond, Surrey
PASS | Create tenant: empty submit -> DataAnnotations messages | The Admin username field is required. / The Admin email field is required. / The Admin password field is required. / The Name field is required. / The Tenant slug field is required.
PASS | Slug suggested from the name | coquitlam
PASS | Slug typed in upper case becomes lowercase | port-coquitlam
PASS | Reserved slug rejected
PASS | Email / password length / confirmation validation | The Admin email field is not a valid e-mail address. / The field Admin password must be a string with a minimum length of 8 and a maximum length of 100. / The passwords do not match.
PASS | Service-layer errors shown on fields (slug taken, username taken) | Username 'burnabyadmin' is already taken. / Another municipality already uses this slug.
PASS | Tenant + its Municipality Admin account created together | https://localhost:7101/admin/municipalities/6?created=1
PASS | Users page lists coquitlamadmin -> Coquitlam
PASS | All mode: position saved in the chosen municipality | https://localhost:7101/admin/positions/17
PASS | Users: Municipality Admin without a municipality is rejected
PASS | Users: Super Admin role has no municipality
PASS | Switcher: Super Admin now works in Coquitlam | dashboard 1/1/0
PASS | Working tenant: municipality is fixed (no dropdown)
PASS | Position: seats must be 1-100
PASS | Position: duplicate name rejected by the service
PASS | coquitlamadmin is fixed to Coquitlam
PASS | Delete screen lists positions, candidates, images and admin accounts | Positions 1 | Candidates 0 | Images (logo and photos) 0 | Administrator accounts 1Deleted and signed out: coquitlamadmin
PASS | Delete button disabled until confirmed
PASS | Tenant deleted; Super Admin back to "All municipalities"
PASS | Open circuit of the deleted admin is redirected to login | 53 s after deletion

TOTAL: 25 passed, 0 failed
```

## 5. Public WebAssembly site (`node tests/e2e/public-e2e.mjs`)

```
# MEMS.Public E2E (headless browser, Blazor WebAssembly)
# 2026-11-19T21:54:23.289Z

PASS | Client source: no HTTP write calls and no localStorage | 23 files scanned
PASS | Client source: sessionStorage only in the guide/compare stores | CompareStore.cs, VotingGuideStore.cs
PASS | / : browse municipalities lists every tenant | Burnaby, Maple Ridge, Pitt Meadows, Richmond, Surrey
PASS | /burnaby : name, election date, tenant colour, logo | --tenant: #8E44AD; --tenant-contrast: #ffffff;
PASS | /Richmond/candidates resolves case-insensitively to /richmond/candidates | /richmond/candidates
PASS | /vancouver (unknown slug) shows "Municipality not found"
PASS | Unknown deep URL shows the not-found page
PASS | SPA switch /burnaby -> /surrey: no Burnaby data in any of the recorded DOM states | 3 DOM states recorded, 0 with stale data
PASS | After the switch: only Surrey candidates, no Burnaby names on the page | 5 cards
PASS | Address bar /surrey/positions -> /burnaby/positions: only Burnaby positions
PASS | Search by name | Julia Kowalski
PASS | Filter by position | Hamid Rahimi, Julia Kowalski
PASS | Municipality filter shows the current tenant
PASS | Changing the municipality filter opens /surrey/candidates
PASS | Candidate details with photograph
PASS | Surrey candidate id under /burnaby is "not found"
PASS | Select a candidate -> guide count 1
PASS | Seat limit: second Mayor (1 seat) cannot be added | Seats full (1/1)
PASS | Guide stored in sessionStorage (ids only, one tenant) | {"tenantSlug":"burnaby","municipalityName":"Burnaby","items":[{"candidateId":2,"positionId":1},{"candidateId":4,"positionId":2}]}
PASS | Nothing of the app stored in localStorage | app keys: []
PASS | Guide page lists the selection by position | Mayor 1 of 1 chosen | Councillor 1 of 8 chosen | School Trustee 0 of 7 chosen
PASS | Guide kept after a page reload (same tab session)
PASS | Printable guide: municipality, election date, candidates, positions, print date | My voting guide Burnaby Election date: Saturday, October 17, 2026 Position Candidate(s) Mayor (choose up to 1) ☐ Hamid Rahimi Councillor (choose up to 8) ☐ Aish
PASS | @media print hides the buttons
PASS | Surrey page: guide count 0 (the guide belongs to Burnaby)
PASS | Adding a Surrey candidate asks to replace the Burnaby guide | Your voting guide contains candidates from Burnaby. A guide can only contain one municipality. Clear it and start a new guide for Surrey with Eleanor Whitfield?
PASS | "Keep" leaves the Burnaby guide unchanged
PASS | "Clear and start new" -> guide now only has the Surrey candidate | {"tenantSlug":"surrey","municipalityName":"Surrey","items":[{"candidateId":6,"positionId":4}]}
PASS | /burnaby/guide no longer shows Burnaby choices
PASS | Remove a candidate from the guide
PASS | Another browser session does not see this guide
PASS | Compare candidates side by side | Hamid Rahimi vs Julia Kowalski
PASS | Network: no POST/PUT/PATCH/DELETE request was sent by the public site
PASS | Network: no request carried a body (guide never transmitted)
PASS | Network: during guide use only GETs of public data were made | 55 API GETs, e.g. /api/municipalities, /api/burnaby/municipality, /api/burnaby/images/{guid}, /api/burnaby/positions, /api/burnaby/candidates

TOTAL: 35 passed, 0 failed
```
