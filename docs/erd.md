# Entity relationship diagram

Generated from the EF Core model (`MEMS.Data/MemsDbContext.cs`, migration `InitialCreate`) on SQL Server.
Identity tables not used directly (claims, logins, tokens, role claims) are omitted.

```mermaid
erDiagram
    Municipalities ||--o{ Positions : "has"
    Municipalities ||--o{ Candidates : "has (tenant)"
    Positions ||--o{ Candidates : "has (composite FK)"
    Municipalities ||--o{ AspNetUsers : "administrators"
    Municipalities |o--o| Images : "logo"
    Candidates |o--o| Images : "photo"
    Municipalities ||--o{ Images : "owns (tenant)"
    AspNetUsers ||--o{ AspNetUserRoles : ""
    AspNetRoles ||--o{ AspNetUserRoles : ""

    Municipalities {
        int Id PK
        nvarchar100 Name "required, unique"
        varchar50 Slug "required, unique, lowercase CHECK"
        nvarchar50 Province "required"
        date ElectionDate "required"
        nvarchar150 Tagline
        nvarchar1000 Description
        char7 PrimaryColor "required, #RRGGBB"
        uniqueidentifier LogoImageId FK "SET NULL"
    }
    Positions {
        int Id PK
        int MunicipalityId FK "tenant, CASCADE"
        nvarchar100 Name "required, unique per municipality"
        nvarchar500 Description
        int SeatsAvailable "CHECK 1-100"
    }
    Candidates {
        int Id PK
        int MunicipalityId FK "tenant, NO ACTION"
        int PositionId FK "with MunicipalityId -> Positions(Id, MunicipalityId)"
        nvarchar100 Name "required"
        nvarchar2000 Profile
        nvarchar255 Website "URL"
        nvarchar255 Email "email"
        uniqueidentifier PhotoImageId FK "SET NULL"
        datetime2 DateCreated "UTC"
    }
    Images {
        uniqueidentifier Id PK
        int MunicipalityId "tenant, indexed"
        varchar50 ContentType "required"
        nvarchar255 FileName
        varbinary Data "required, validated image"
        datetime2 CreatedUtc
    }
    AspNetUsers {
        nvarchar450 Id PK
        nvarchar256 UserName "unique (normalized, case-insensitive)"
        nvarchar256 Email
        int MunicipalityId FK "null for Super Admin, RESTRICT"
        nvarchar SecurityStamp "changes sign the admin out"
    }
    AspNetRoles {
        nvarchar450 Id PK
        nvarchar256 Name "Super Admin | Municipality Admin"
    }
    AspNetUserRoles {
        nvarchar450 UserId PK, FK
        nvarchar450 RoleId PK, FK
    }
```

## Notes

- **Tenant relationship** (spec "Tenant Data Model"): `Position`, `Candidate`, `Images` and administrator
  accounts (`AspNetUsers`) carry `MunicipalityId`. A Municipality Admin has exactly one tenant; a Super Admin has none.
- `Candidates.MunicipalityId` duplicates the position's tenant on purpose: it allows a row-level query filter
  on candidates, and the composite foreign key guarantees it always equals the position's tenant.
- Images are stored in SQL Server so the Admin app and the API (and therefore the public site) share them;
  the API serves them at `/api/{tenant}/images/{id}`.
- Deleting a tenant is done by `MunicipalityService.DeleteAsync` in one transaction: candidates, positions,
  images, administrator accounts, then the municipality.
- Changes since Assignment 1: SQLite → SQL Server; `Slug`, `Tagline`, `PrimaryColor` added; per-municipality
  admin roles replaced by `AspNetUsers.MunicipalityId` + two roles; logo/photo files → `Images` table;
  `SavedCandidates` table **removed** (election privacy).
- There is no table for voting-guide selections, ballots or vote totals.
