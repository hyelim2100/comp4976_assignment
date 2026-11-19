# MEMS Web API

Base URL (Aspire, development): `https://localhost:7201`. The public site reaches the same API through its own
origin (`https://localhost:5001/api/...`, proxied with YARP + service discovery).

- **Swagger UI:** `https://localhost:7201/swagger` · **OpenAPI document:** `https://localhost:7201/openapi/v1.json`
- `{tenant}` is the municipality slug (`burnaby`, `surrey`, `richmond`, `pitt-meadows`, `maple-ridge`), case-insensitive.
- JSON uses camelCase. Dates: `yyyy-MM-dd`. All examples below are real responses from the seeded database.

## Rules

| Rule | Behaviour |
|---|---|
| Tenant scope | Every `/api/{tenant}/...` response contains only that tenant's data — also for a Super Admin token. |
| Unknown tenant | `404` ProblemDetails "Municipality not found". |
| Id of another tenant | `404` (exactly like a missing id; nothing is revealed). |
| Municipality Admin token on another tenant's route | `403` — for reads and writes. |
| Reads (GET) | Anonymous. |
| Writes (POST/PUT/DELETE) | `Authorization: Bearer {token}` from `POST /api/auth/login`; role Municipality Admin (own tenant) or Super Admin. Creating/deleting tenants: Super Admin only. |
| Body `municipalityId` on tenant routes | Ignored — records are always created in the route tenant. |
| Revoked accounts | Tokens of deleted administrators, or whose role/tenant/password changed, are rejected (`401`). |
| Errors | RFC 7807 `application/problem+json` with `traceId`; never stack traces. |
| Voting guide | There is **no** endpoint for voting-guide selections (election privacy). |

## Endpoints

### Authentication
| Method | Route | Auth | Success | Errors |
|---|---|---|---|---|
| POST | `/api/auth/login` | – | 200 `LoginResponse` | 400, 401 (also when locked out after 5 failures) |
| GET | `/api/auth/me` | Bearer | 200 | 401 |

### Municipalities (tenants)
| Method | Route | Auth | Success | Errors |
|---|---|---|---|---|
| GET | `/api/municipalities` | – | 200 `MunicipalitySummaryDto[]` (all tenants) | – |
| GET | `/api/municipalities/{id}` | – | 200 `MunicipalitySummaryDto` | 404 |
| POST | `/api/municipalities` | Super Admin | 201 `MunicipalityDto` + first admin account created | 400, 401, 403 |
| GET | `/api/{tenant}/municipality` | – | 200 `MunicipalityDto` | 403, 404 |
| PUT | `/api/{tenant}/municipality` | Admin of tenant / Super Admin | 204 (slug change: Super Admin only) | 400, 401, 403, 404 |
| PUT | `/api/{tenant}/municipality/logo` | Admin of tenant / Super Admin | 204 (multipart, field `file`) | 400, 401, 403, 404 |
| GET | `/api/{tenant}/municipality/deletion-impact` | Super Admin | 200 `TenantDeletionImpactDto` | 401, 403, 404 |
| DELETE | `/api/{tenant}/municipality` | Super Admin | 204 (positions, candidates, images, admin accounts deleted) | 401, 403, 404 |

### Positions
| Method | Route | Auth | Success | Errors |
|---|---|---|---|---|
| GET | `/api/{tenant}/positions` | – | 200 `PositionDto[]` | 403, 404 |
| GET | `/api/{tenant}/positions/{id}` | – | 200 `PositionDto` | 403, 404 |
| POST | `/api/{tenant}/positions` | Admin of tenant / Super Admin | 201 `PositionDto` | 400, 401, 403 |
| PUT | `/api/{tenant}/positions/{id}` | Admin of tenant / Super Admin | 204 | 400, 401, 403, 404 |
| DELETE | `/api/{tenant}/positions/{id}` | Admin of tenant / Super Admin | 204 (its candidates are deleted too) | 401, 403, 404 |

### Candidates
| Method | Route | Auth | Success | Errors |
|---|---|---|---|---|
| GET | `/api/{tenant}/candidates?search=&positionId=` | – | 200 `CandidateDto[]` | 403, 404 |
| GET | `/api/{tenant}/candidates/{id}` | – | 200 `CandidateDto` | 403, 404 |
| POST | `/api/{tenant}/candidates` | Admin of tenant / Super Admin | 201 `CandidateDto` | 400, 401, 403 |
| PUT | `/api/{tenant}/candidates/{id}` | Admin of tenant / Super Admin | 204 | 400, 401, 403, 404 |
| PUT | `/api/{tenant}/candidates/{id}/photo` | Admin of tenant / Super Admin | 204 (multipart, field `file`) | 400, 401, 403, 404 |
| DELETE | `/api/{tenant}/candidates/{id}` | Admin of tenant / Super Admin | 204 | 401, 403, 404 |

### Images
| Method | Route | Auth | Success | Errors |
|---|---|---|---|---|
| GET | `/api/{tenant}/images/{id}` | – | 200 image bytes (`Cache-Control: immutable`) | 404 (also for another tenant's image) |

Image uploads: `.jpg .jpeg .png .gif .webp`, at most 2 MB, and the file content must be a real image of the
type its extension says (checked by file signature).

### Health (ServiceDefaults)
`GET /health` (all checks incl. SQL Server) and `GET /alive` (liveness) — Development environment.

## Request and response examples

### Public reads

`GET /api/municipalities` → 200 (first item)
```json
[
  {
    "id": 1,
    "name": "Burnaby",
    "slug": "burnaby",
    "province": "British Columbia",
    "electionDate": "2026-10-17",
    "tagline": "Burnaby votes - your city, your choice.",
    "primaryColor": "#8E44AD",
    "logoImageId": "1c914151-5680-425c-af97-21e1e6dc3a58"
  }
]
```

`GET /api/burnaby/municipality` → 200
```json
{
  "id": 1,
  "name": "Burnaby",
  "slug": "burnaby",
  "province": "British Columbia",
  "electionDate": "2026-10-17",
  "tagline": "Burnaby votes - your city, your choice.",
  "description": "A large city between Vancouver and the Fraser Valley, home to Metrotown, Burnaby Mountain and extensive parkland.",
  "primaryColor": "#8E44AD",
  "logoImageId": "1c914151-5680-425c-af97-21e1e6dc3a58",
  "positionCount": 3,
  "candidateCount": 5
}
```

`GET /api/burnaby/positions` → 200 (first item)
```json
[
  {
    "id": 1,
    "name": "Mayor",
    "description": null,
    "seatsAvailable": 1,
    "municipalityId": 1,
    "municipalityName": "Burnaby",
    "municipalitySlug": "burnaby",
    "candidateCount": 2
  }
]
```

`GET /api/burnaby/candidates?search=julia` → 200
```json
[
  {
    "id": 1,
    "name": "Julia Kowalski",
    "profile": "Former city planner running on housing supply and rental protection.",
    "website": "https://example.com/kowalski",
    "email": "julia.kowalski@example.com",
    "photoImageId": "38897211-86ab-42fb-a018-c570468ea434",
    "positionId": 1,
    "positionName": "Mayor",
    "seatsAvailable": 1,
    "municipalityId": 1,
    "municipalityName": "Burnaby",
    "municipalitySlug": "burnaby",
    "dateCreated": "2026-11-19T21:43:24.7330824"
  }
]
```

### Sign in

`POST /api/auth/login`
```json
{ "userName": "burnabyadmin", "password": "P@$$w0rd" }
```
→ 200
```json
{
  "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJ...",
  "expiresUtc": "2026-11-19T22:56:06.2641356Z",
  "userName": "burnabyadmin",
  "role": "Municipality Admin",
  "municipalityId": 1,
  "municipalitySlug": "burnaby"
}
```

### Writes (with `Authorization: Bearer {token}`)

`POST /api/burnaby/candidates`
```json
{
  "name": "Jordan Example",
  "profile": "Sample candidate created through the API.",
  "website": "https://example.com/jordan",
  "email": "jordan@example.com",
  "positionId": 2
}
```
→ `201 Created`, `Location: https://localhost:7201/api/burnaby/candidates/24`
```json
{
  "id": 24,
  "name": "Jordan Example",
  "profile": "Sample candidate created through the API.",
  "website": "https://example.com/jordan",
  "email": "jordan@example.com",
  "photoImageId": null,
  "positionId": 2,
  "positionName": "Councillor",
  "seatsAvailable": 8,
  "municipalityId": 1,
  "municipalityName": "Burnaby",
  "municipalitySlug": "burnaby",
  "dateCreated": "2026-11-19T21:56:07.9062853"
}
```

- `PUT /api/burnaby/candidates/24` with `{ "name": "Jordan Example", "positionId": 2 }` → `204 No Content`
- `DELETE /api/burnaby/candidates/24` → `204 No Content`

`POST /api/burnaby/positions`
```json
{ "name": "Park Commissioner", "description": "Oversees parks and recreation.", "seatsAvailable": 3 }
```
→ `201 Created` with a `PositionDto`.

`POST /api/municipalities` (Super Admin)
```json
{
  "name": "Coquitlam", "slug": "coquitlam", "province": "British Columbia", "electionDate": "2026-10-17",
  "tagline": "Coquitlam votes", "primaryColor": "#AA3366",
  "adminUserName": "coquitlamadmin", "adminEmail": "c@example.com",
  "adminPassword": "P@$$w0rd", "confirmAdminPassword": "P@$$w0rd"
}
```
→ `201 Created`, `Location: /api/coquitlam/municipality`, body `MunicipalityDto`.

Photo upload: `curl -X PUT https://localhost:7201/api/burnaby/candidates/1/photo -H "Authorization: Bearer $TOKEN" -F "file=@photo.png"` → `204`.

## Error responses

`400` — validation (`POST /api/burnaby/candidates` with `{"name":"","positionId":2,"email":"not-an-email","website":"nope"}`)
```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": {
    "Name": ["The Name field is required."],
    "Email": ["The Email field is not a valid e-mail address."],
    "Website": ["The Website field is not a valid fully-qualified http, https, or ftp URL."]
  },
  "traceId": "00-b416ea8b2b0273ee08e5931ba2f7dcf3-05e9eb08ca968b58-01"
}
```
Service rules use the same shape, e.g. `"PositionId": ["Please choose a valid position."]` (position of another
tenant), `"Slug": ["Another municipality already uses this slug."]`, `"Photo": ["The file is not a valid image."]`
(signature check) or `"file": ["Only .jpg, .jpeg, .png, .gif, .webp images are allowed."]`.

`401` — no or invalid token (`POST /api/burnaby/positions` without `Authorization`)
```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.2",
  "title": "Unauthorized",
  "status": 401,
  "traceId": "00-4a7adc9a57675c71f0280448f7a9b345-2cd09c9faa685d1e-01"
}
```

`401` — wrong password (`POST /api/auth/login`)
```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.2",
  "title": "Sign-in failed",
  "status": 401,
  "detail": "Invalid user name or password.",
  "traceId": "00-75f4e4b57e3f1515e110f7b42e4fcf14-7008671d32db899a-01"
}
```

`403` — Burnaby administrator token on a Surrey route (`GET /api/surrey/candidates`)
```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.4",
  "title": "Access to this municipality is not allowed",
  "status": 403,
  "detail": "Municipality administrators can only access the data of their own municipality.",
  "traceId": "00-3a7e9c40c4fe6fb647dc7880283db504-e990e8062f8b9ff8-01"
}
```

`404` — unknown tenant (`GET /api/vancouver/candidates`)
```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.5",
  "title": "Municipality not found",
  "status": 404,
  "detail": "There is no municipality with the URL segment 'vancouver'.",
  "traceId": "00-4eecbf37787f3d304eae8314eefc69dd-2edfde10310a4c9e-01"
}
```

`404` — id of another tenant (`GET /api/burnaby/candidates/6`, a Surrey candidate)
```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.5",
  "title": "Not found",
  "status": 404,
  "detail": "The candidate was not found.",
  "traceId": "00-bdc0c780edc26ebe26ced9996506e027-57a9f4da4277f233-01"
}
```

`409` — a uniqueness conflict caught by the database (concurrent requests); `500` — unexpected error:
generic ProblemDetails ("An unexpected error occurred") with `traceId`; details are only in the server log.

The complete isolation matrix (36 requests) is in [test-evidence.md](test-evidence.md).
