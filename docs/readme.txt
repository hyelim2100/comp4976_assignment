COMP4976 Assignment 1 - Municipal Elections Management System
==============================================================

1. TEAM MEMBERS
---------------
- Hyelim Kim  (A01459149)
- Hailey Kim  (A01412747)


2. CONTRIBUTIONS
----------------
The team worked on this project together in joint working sessions.

Hyelim Kim
- Data model with EF Core Code First (SQLite), migrations and seed data
- ASP.NET Core Identity, Super Admin / municipality admin groups and authorization
- Admin area: dashboard and CRUD for municipalities, positions and candidates,
  including logo/photo upload and validation

Hailey Kim
- Public website: browsing, candidate search/filter, voting guide and printable guide
- Azure App Service deployment


3. WHAT HAS NOT BEEN COMPLETED
------------------------------
- There is no user-management screen. When a Super Admin creates a municipality, its
  "<Name> Admin" group is created automatically, but new admin users must be added in
  the seed code (Data/DbSeeder.cs).
- Optional bonus features (pagination, PDF export, candidate comparison, Azure Blob
  Storage) were not implemented.


4. MAJOR CHALLENGES
-------------------
- Making sure a municipality admin can only manage their own municipality, even when
  an id is typed into the URL or a form value is changed.
- Keeping the SQLite database and uploaded images across Azure deployments
  (stored under /home/data instead of the deployment folder).


5. TESTING INSTRUCTIONS
-----------------------
Accounts (user name / password):
  super / P@$$w0rd   - Super Admin (all municipalities)
  pm    / P@$$w0rd   - Pitt Meadows Admin (Pitt Meadows only)

- Public pages need no login: browse municipalities and candidates, search/filter,
  add candidates to "My Voting Guide" and print it.
- Click "Admin Login", sign in with a user name above, then open "Admin".
- As pm, opening a Burnaby record (e.g. /Admin/Candidates/Edit/16) is denied.
- The Azure Free tier may take 10-20 seconds to respond to the first request.

Running locally:
- appsettings.Development.json is excluded from source control. Create
  MunicipalElections/appsettings.Development.json with:
    {
      "ConnectionStrings": { "DefaultConnection": "DataSource=app.db;Cache=Shared" },
      "Jwt": { "Key": "<any base64 string of 32+ bytes>" }
    }
- Run "dotnet run" in the MunicipalElections folder. The database is created and
  seeded automatically.


6. DEPLOYED APPLICATION
-----------------------
Azure: https://mems-hh-enf4drf2hmemb0as.canadacentral-01.azurewebsites.net
GitHub: https://github.com/hyelim2100/comp4976_assignment
