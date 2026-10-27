# Asset Suite (.NET 10)

Side-by-side ASP.NET Core port of Nanosoft Asset Management. The legacy IIS app under `src/AssetManagement.Web` is unchanged.

## Build and test

```bash
cd net10
dotnet build AssetSuite.sln
dotnet test AssetSuite.sln
```

## Run locally

```bash
cd net10/src/AssetSuite.Web
dotnet run
```

Set `ConnectionStrings:AssetManagementConnection` in user secrets or environment (same database as the net40 app). See `docs/NET10-MIGRATION.md` for IIS hosting notes.

## URLs

- Platform login: `/Account/Login`
- Tenant login: `/{slug}/Account/Login` (e.g. `/nanosoft/Account/Login`)
