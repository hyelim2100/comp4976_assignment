# Screenshots

This section shows the running system: Aspire dashboard, admin site, public site, API tests and the SQL Server database.

## 1. Aspire dashboard

| # | Screenshot | Description |
|---|---|---|
| 1.1 | **Resources** | All resources (sql, memsdb, admin, api, public) are Running, with links to Swagger UI, the admin site and the public site. |
| 1.2 | **Health checks** | The details panel of the api resource shows its health check `api_https_/health_200_check` as Healthy; environment variable values are masked. |
| 1.3 | **Structured logs** | Structured logs of the api resource, with Warning entries "Cross-tenant request blocked: user burnabyadmin (municipality 1) ..." between the API login entries. |
| 1.4 | **Traces** | The trace list with spans from admin, api, public and memsdb (a), and the detail of a public request: public `GET /api/{**catch-all}` → api `GET api/{tenant}/candidates` → SQL queries on Municipalities and Candidates (b). |
| 1.5 | **Metrics** | `http.server.request.duration` of the api resource (P50, P90, P99) with GET/POST requests and status codes 200, 400, 401, 403 and 404. |
| 1.6 | **Service discovery / configuration** | Environment variables of the public resource, including `services__api__https__0` and `services__api__http__0` set by Aspire; values are masked. |

## 2. Admin site

| # | Screenshot | Description |
|---|---|---|
| 2.1 | Login page | The admin sign-in page with user name and password. |
| 2.2 | **Super Admin dashboard** | Super Admin working in All municipalities: 5 municipalities, 15 positions, 22 candidates. |
| 2.3 | Working tenant | With Burnaby selected as the working tenant, the dashboard shows 1 municipality, 3 positions and 5 candidates (a) and the candidates list shows only Burnaby (b). |
| 2.4 | **Create municipality + admin account** | The create form with validation errors: the slug "admin" is reserved and the administrator account fields are required. |
| 2.5 | **Administrators page** | The administrators list with each account's role and municipality (a) and the edit form of pm, a Municipality Admin of Pitt Meadows (b). |
| 2.6 | **Delete impact** | The delete confirmation for Maple Ridge lists the positions, candidates, images and administrator account that would be deleted. |
| 2.7 | **pm dashboard** | pm is fixed to Pitt Meadows: 1 municipality, 3 positions, 5 candidates. |
| 2.8 | **pm lists and dropdowns** | pm's candidates list contains only Pitt Meadows (a); in the Add candidate form (b) the position dropdown lists only Pitt Meadows positions (c). |
| 2.9 | **pm direct URL** | pm opening a Burnaby candidate by URL sees "Candidate not found" (a); the Administrators page shows "Access denied" (b). |
| 2.10 | Candidate form with photo | The candidate edit form with the current photo and the validation message for an invalid email address. |

## 3. Public site

| # | Screenshot | Description |
|---|---|---|
| 3.1 | **Browse municipalities** | Cards for all five municipalities with logo, election day and tagline. |
| 3.2 | **Tenant home ×2** | The Burnaby home page in purple (a) and the Surrey home page in teal (b), each with its own logo, tagline and election date. |
| 3.3 | Case-insensitive / not found | `/Richmond` opens the Richmond site (a); `/vancouver` shows "Municipality not found" (b). |
| 3.4 | **Candidates with search/filter** | Burnaby candidates filtered by the name search "a" and the position Councillor: 2 results. |
| 3.5 | Candidate details | Julia Kowalski's details with photo, biography and the Add to my guide button. |
| 3.6 | **Voting guide** | My voting guide for Burnaby with Mayor 1 of 1 and Councillor 1 of 8 chosen (a); on the candidates page the other Mayor candidate shows "Seats full (1/1)" (b). |
| 3.7 | **One municipality per guide** | Adding a Surrey candidate while the guide holds Burnaby candidates asks whether to keep the Burnaby guide or start a new one. |
| 3.8 | **Session storage** | Session storage of `https://localhost:5001`: the key `mems.votingGuide` holds the tenant and candidate/position ids only. |
| 3.9 | **No data sent** | Network requests (Fetch/XHR) while using the guide: only positions, municipality and candidates, all with status 200. |
| 3.10 | **Printable guide** | The printable guide for Burnaby with election date, positions, chosen candidates and print date (a), and its print preview without buttons on one page (b). |
| 3.11 | Mobile | The Burnaby candidates page at phone width. |

## 4. API testing

| # | Screenshot | Description |
|---|---|---|
| 4.1 | **Swagger UI** | Swagger UI listing the API endpoints. |
| 4.2 | Login | `POST /api/auth/login` for burnabyadmin returns 200 with a JWT (a); the Authorize dialog shows the Bearer token as authorized (b). |
| 4.3 | **403** | `POST /api/surrey/candidates` with the burnabyadmin token returns 403 "Access to this municipality is not allowed". |
| 4.4 | **404** | `GET /api/burnaby/candidates/6` (a Surrey candidate) returns 404 (a); `GET /api/vancouver/positions` returns 404 "Municipality not found" (b). |
| 4.5 | 400 validation | `POST /api/burnaby/candidates` with an empty name, an invalid email and an invalid website returns 400 with an error for each field. |
| 4.6 | 401 | Without a token, `POST /api/burnaby/positions` returns 401. |
| 4.7 | Script results | `api-isolation.sh` results ending with `TOTAL: 36 passed, 0 failed`. |

## 5. SQL Server database

| # | Screenshot | Description |
|---|---|---|
| 5.1 | **Tables** | The tables of `memsdb`: Municipalities, Positions, Candidates, Images, the Identity tables and __EFMigrationsHistory; there is no table for voting-guide selections. |
| 5.2 | **Tenants** | The Municipalities table: five tenants with slug, primary colour and election date. |
| 5.3 | **Tenant data** | Candidates of every tenant with their position (22 rows). |
| 5.4 | **Administrators and tenants** | Administrator accounts with role and municipality; super has no municipality. |
| 5.5 | Constraints | The foreign keys of Candidates, including the composite key to Positions, and the check constraints for the lowercase slug and the number of seats. |
| 5.6 | Migrations | The applied migration `20261118173947_InitialCreate`. |
