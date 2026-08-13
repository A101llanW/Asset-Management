---
name: run-unit-tests
description: Run NUnit unit tests for CodexAsset after C# changes. Use when implementing or fixing Application, Domain, Infrastructure, or test code.
---

# Run unit tests

From repo root:

```bash
dotnet restore tests/AssetManagement.Tests/AssetManagement.Tests.csproj
dotnet build tests/AssetManagement.Tests/AssetManagement.Tests.csproj -c Release
dotnet test tests/AssetManagement.Tests/AssetManagement.Tests.csproj -c Release --no-build --logger "console;verbosity=normal"
```

Do not run Performance category tests unless the user asks and `ASSETMANAGEMENT_TEST_CONNECTION` is set.
