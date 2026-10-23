---
name: azure-deployment
description: How this app is deployed to Azure App Service (Linux) and how to troubleshoot it. Use when changing deployment, app settings, storage paths, or when the live site fails after a push.
---

# Azure deployment

## How it works
- Web app: `mems-hh` (resource group `mems-rg`, Linux, .NET 10, Canada Central).
- Every push to `main` runs `.github/workflows/deploy.yml`: `dotnet publish` of
  `MunicipalElections/MunicipalElections.csproj`, then `azure/webapps-deploy@v3` using the
  `AZURE_WEBAPP_PUBLISH_PROFILE` repository secret.
- On startup `DbSeeder` creates the database folder, applies migrations and seeds data, so a
  fresh deployment needs no manual database step.

## Required app settings (Azure → Environment variables)
| Name | Value |
|---|---|
| `ConnectionStrings__DefaultConnection` | `DataSource=/home/data/app.db;Cache=Shared` |
| `Jwt__Key` | base64 key of at least 32 bytes (never commit it) |
| `Uploads__Path` | `/home/data/uploads` |
| `ASPNETCORE_FORWARDEDHEADERS_ENABLED` | `true` (so generated image URLs use https) |

`/home/data` is persistent storage; the deployment folder (`/home/site/wwwroot`) is replaced on
every deploy, so the database and uploads must not live there.

## Troubleshooting checklist
1. GitHub → Actions: did `build` or `deploy` fail? Read the failing step's log.
2. App fails to start with "Connection string 'DefaultConnection' not found" or
   "JWT signing key 'Jwt:Key' is not configured" → an app setting is missing or misspelled
   (Linux needs double underscores `__`).
3. Images return 404 → check `Uploads__Path`; seed images are re-copied on startup.
4. Image URLs use `http://` → `ASPNETCORE_FORWARDEDHEADERS_ENABLED` is not `true`.
5. Logs: `az webapp log tail -g mems-rg -n mems-hh`.
6. First request after idle takes 10–20 s on the Free tier; this is normal.
