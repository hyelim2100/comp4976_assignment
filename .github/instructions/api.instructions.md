---
applyTo: "MEMS.Api/**"
description: "Multi-tenant Web API: routes, authorization, ProblemDetails and OpenAPI documentation"
---

# MEMS.Api instructions

- Tenant resources use `[Route("api/{tenant}/...")]` and a `string tenant` action parameter. The global
  `TenantResolutionFilter` has already bound the scope to that tenant — do not resolve it again and never
  accept a `MunicipalityId` from the body as the target tenant.
- Controllers inherit `ApiControllerBase` and map service results with `FromFailure(result)`
  (404 / 403 / 400 `ValidationProblemDetails`).
- Public reads: `[AllowAnonymous]`. Writes: `[Authorize(Roles = AppRoles.SuperAdmin + "," + AppRoles.MunicipalityAdmin)]`
  or `AppRoles.SuperAdmin` only for tenant creation/deletion.
- Document every action with XML comments (`<summary>`, `<response code>`) and `ProducesResponseType`
  for every status code it can return; update `docs/api.md` when endpoints change.
- Uploads: multipart field `file`, `[RequestSizeLimit]`, `ReadImageAsync` (extension + size), then the
  service validates the file signature.
- Never return exception details; `GlobalExceptionHandler` logs and returns a generic ProblemDetails.
- Verify isolation with `bash tests/e2e/api-isolation.sh`.
