---
applyTo: "MEMS.Public.Client/**"
description: "Blazor WebAssembly public site: API-only data access, tenant routing, election privacy"
---

# MEMS.Public.Client instructions

- All data comes from `MemsApiClient` (GET requests to `/api/...`, proxied by the MEMS.Public host).
  Do not add POST/PUT/DELETE methods and do not reference MEMS.Data.
- Tenant pages are `@page "/{Tenant}/..."` and wrap their content in `<TenantShell Tenant="@Tenant">`,
  which normalizes the slug to lowercase, shows "Municipality not found" for unknown slugs and applies the
  tenant colour (`Theme.Style`).
- When the slug changes, clear the page's data **before** the first `await`, ignore responses for an old slug,
  and render only rows whose `MunicipalitySlug` equals the current tenant.
- Voting guide: only through `VotingGuideStore` (browser `sessionStorage`, ids only). One municipality per
  guide (ask before replacing), at most `SeatsAvailable` per position. Never send it to the API, never use
  `localStorage`.
- Images: `MemsApiClient.ImageUrl(slug, imageId)` -> `/api/{slug}/images/{id}`.
- Use Bootstrap utilities and the `tenant-*` / `btn-tenant` classes from `wwwroot/app.css`; pages must work at
  phone width. The print page uses `PrintLayout` and `@media print` rules.
