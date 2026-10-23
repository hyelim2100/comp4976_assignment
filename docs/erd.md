# Entity Relationship Diagram

Generated from the EF Core model (`MunicipalElections/Data/ApplicationDbContext.cs`, migration
`InitialCreate`). Identity tables that are not used directly (claims, logins, tokens) are omitted.

```mermaid
erDiagram
    Municipalities ||--o{ Positions : "has"
    Positions ||--o{ Candidates : "has"
    Municipalities ||--o| AspNetRoles : "admin group"
    AspNetRoles ||--o{ AspNetUserRoles : ""
    AspNetUsers ||--o{ AspNetUserRoles : ""
    AspNetUsers ||--o{ SavedCandidates : "saves"
    Candidates ||--o{ SavedCandidates : "saved as"

    Municipalities {
        int MunicipalityId PK
        string Name "required, unique, max 100"
        string Province "required, max 50"
        datetime ElectionDate "required"
        string Description "max 1000"
        string LogoFileName "GUID file name"
    }
    Positions {
        int PositionId PK
        string Name "required, unique per municipality"
        int SeatsAvailable "1-100"
        int MunicipalityId FK
    }
    Candidates {
        int CandidateId PK
        string Name "required, max 100"
        string Profile "biography, max 2000"
        string PhotoFileName "GUID file name"
        string Website "URL"
        string Email "email"
        int PositionId FK
        datetime DateCreated
    }
    AspNetRoles {
        string Id PK
        string Name "Super Admin or <Municipality> Admin"
        int MunicipalityId FK "null for Super Admin"
    }
    AspNetUsers {
        string Id PK
        string UserName "super, pm"
        string Email
        string Name
    }
    AspNetUserRoles {
        string UserId PK, FK
        string RoleId PK, FK
    }
    SavedCandidates {
        int SavedCandidateId PK
        string UserId FK
        int CandidateId FK
        datetime DateSaved
    }
```

## Relationships and rules
| Relationship | Type | On delete |
|---|---|---|
| Municipality → Positions | one-to-many | cascade |
| Position → Candidates | one-to-many | cascade |
| Municipality → admin group (`AspNetRoles.MunicipalityId`) | one-to-zero-or-one | cascade |
| User ↔ Role (`AspNetUserRoles`) | many-to-many | cascade |
| User / Candidate → SavedCandidates (REST API voting guide) | one-to-many | cascade |

- A candidate's municipality is derived from its position (`Candidate → Position → Municipality`),
  so it is stored once.
- A municipality admin may manage only the municipality linked to their admin group; the
  `Super Admin` group has no municipality and manages everything.
- The public website's voting guide is stored in session state, not in the database.
