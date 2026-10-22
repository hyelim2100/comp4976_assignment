# Municipal Elections Web API — Contract

This is the agreement between the ASP.NET Core backend (COMP4976 / COMP4977 Part 1) and the
SwiftUI app *Municipal Elections Companion* (COMP4977 Part 2). Changes to anything here must be
agreed by both sides first.

- **Base URL (Azure):** `https://<app-name>.azurewebsites.net` — *to be filled in after the first deployment*
- **Base URL (local):** `http://localhost:5099` (run with `dotnet run --urls http://localhost:5099`)
- **Interactive docs:** `<base URL>/swagger`

## Conventions

| Topic | Rule |
|---|---|
| Format | JSON, `Content-Type: application/json`, property names in **camelCase** |
| Auth | Protected endpoints need `Authorization: Bearer <token>` |
| `electionDate` | Date only, `"2026-10-17"` (format `yyyy-MM-dd`) |
| Other dates (`dateCreated`, `dateSaved`, `expiresAt`) | UTC ISO 8601, whole seconds: `"2026-10-22T18:34:35Z"` — works with `JSONDecoder.DateDecodingStrategy.iso8601` |
| Images (`logoUrl`, `photoUrl`) | Absolute URL, or `null` when no image has been uploaded — show a placeholder |
| Optional fields | `description`, `biography`, `website`, `email` may be `null` |

### Swift decoding tip

`electionDate` is not a full ISO 8601 timestamp. Either decode it as `String`, or use a custom strategy:

```swift
let dayFormatter = DateFormatter()
dayFormatter.dateFormat = "yyyy-MM-dd"
dayFormatter.locale = Locale(identifier: "en_US_POSIX")
dayFormatter.timeZone = TimeZone(identifier: "UTC")

let decoder = JSONDecoder()
decoder.dateDecodingStrategy = .custom { decoder in
    let s = try decoder.singleValueContainer().decode(String.self)
    if let d = dayFormatter.date(from: s) ?? ISO8601DateFormatter().date(from: s) { return d }
    throw DecodingError.dataCorrupted(.init(codingPath: decoder.codingPath, debugDescription: "Bad date: \(s)"))
}
```

## Errors

Every error with a body uses [RFC 9457 Problem Details](https://www.rfc-editor.org/rfc/rfc9457):

```json
{ "type": "...", "title": "Invalid email or password.", "status": 401, "traceId": "..." }
```

Validation errors (`400`) add an `errors` object keyed by field name — show these next to the form fields:

```json
{
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": {
    "Email": ["The Email field is not a valid e-mail address."],
    "Password": ["Passwords must have at least one uppercase ('A'-'Z')."]
  }
}
```

| Status | Meaning |
|---|---|
| 200 / 201 | OK / created (body returned) |
| 204 | Deleted (no body) |
| 400 | Validation error (`errors` object) |
| 401 | Missing/expired token (**empty body**) or wrong email/password on login (Problem Details) |
| 404 | Not found |
| 409 | Candidate already saved |

---

## Authentication

### `POST /api/auth/register`

Password rules: at least **6** characters, with an uppercase letter, a lowercase letter, a digit and a
symbol (e.g. `Voter123!`). Email must be unique.

Request:

```json
{ "name": "Test Voter", "email": "voter@example.com", "password": "Voter123!" }
```

Response `201` — same shape as login, so the user is signed in right away:

```json
{
  "token": "eyJhbGciOiJIUzI1NiIs...",
  "expiresAt": "2026-10-23T18:34:33Z",
  "user": { "id": "7c3bd3ca-9442-4369-8f8e-07268ae3737f", "name": "Test Voter", "email": "voter@example.com" }
}
```

Errors: `400` (validation, duplicate email, weak password).

### `POST /api/auth/login`

Request:

```json
{ "email": "voter@example.com", "password": "Voter123!" }
```

Response `200`: same as register. Store `token` in the Keychain; tokens expire after 24 hours
(`expiresAt`) — on any `401` from a protected endpoint, send the user back to the login screen.

Errors: `400` (validation), `401` (wrong email/password, or locked after 5 failed attempts).

### `GET /api/auth/me` 🔒

For the Profile screen. Response `200`:

```json
{ "id": "7c3bd3ca-...", "name": "Test Voter", "email": "voter@example.com" }
```

---

## Municipalities

### `GET /api/municipalities`

Response `200`, sorted by name:

```json
[
  {
    "municipalityId": 1,
    "name": "Pitt Meadows",
    "province": "British Columbia",
    "electionDate": "2026-10-17",
    "description": "A small city in Metro Vancouver ...",
    "logoUrl": "https://<base>/uploads/pitt-meadows.png"
  }
]
```

### `GET /api/municipalities/{id}`

Same fields plus its positions. `404` if not found.

```json
{
  "municipalityId": 1,
  "name": "Pitt Meadows",
  "province": "British Columbia",
  "electionDate": "2026-10-17",
  "description": "...",
  "logoUrl": null,
  "positions": [
    { "positionId": 1, "name": "Mayor", "seatsAvailable": 1, "municipalityId": 1, "municipalityName": "Pitt Meadows" }
  ]
}
```

## Positions

### `GET /api/positions[?municipalityId=1]`

`municipalityId` is optional. Response `200`:

```json
[
  { "positionId": 1, "name": "Mayor", "seatsAvailable": 1, "municipalityId": 1, "municipalityName": "Pitt Meadows" }
]
```

### `GET /api/positions/{id}`

One position object. `404` if not found.

## Candidates

Candidate object (used by every candidate endpoint and inside saved candidates):

```json
{
  "candidateId": 3,
  "name": "Priya Sandhu",
  "biography": "Registered nurse advocating for expanded community health services.",
  "photoUrl": null,
  "website": null,
  "email": "priya.sandhu@example.com",
  "positionId": 2,
  "positionName": "Councillor",
  "municipalityId": 1,
  "municipalityName": "Pitt Meadows",
  "dateCreated": "2026-10-22T18:19:03Z"
}
```

### `GET /api/candidates[?name=&municipalityId=&positionId=]`

All query parameters are optional and can be combined — use this for the directory's search and filters.
`name` is a case-insensitive "contains" match. Sorted by municipality, position, name.

### `GET /api/candidates/search?name=liu`

Name search only (same matching as above).

### `GET /api/candidates/{id}`

One candidate. `404` if not found.

## Voting guide 🔒

All endpoints need the bearer token and only see the signed-in user's own entries.

### `GET /api/savedcandidates`

Response `200`, oldest first:

```json
[
  {
    "savedCandidateId": 1,
    "candidateId": 3,
    "dateSaved": "2026-10-22T18:34:35Z",
    "candidate": { "candidateId": 3, "name": "Priya Sandhu", "...": "full candidate object" }
  }
]
```

### `POST /api/savedcandidates`

Request:

```json
{ "candidateId": 3 }
```

Response `201`: the new saved-candidate object (as above). Errors: `400` (missing `candidateId`),
`404` (no such candidate), `409` (already saved).

### `DELETE /api/savedcandidates/{id}`

`id` is the **`savedCandidateId`**, not the `candidateId`. Response `204`. `404` if it does not exist
or belongs to another user.

---

## Seed data for testing

- Municipalities: Pitt Meadows, Maple Ridge, Burnaby (election date 2026-10-17)
- Positions per municipality: Mayor, Councillor, School Trustee
- 22 candidates
- Mobile users: register your own via `POST /api/auth/register`. (The web admin accounts `super@example.com`
  and `pm@example.com`, password `P@$$w0rd`, can also log in through the API.)
