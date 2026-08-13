#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$ROOT"

TEST_DLL="$ROOT/tests/AssetManagement.Tests/bin/Release/net40/AssetManagement.Tests.dll"
NUNIT_CONSOLE="$ROOT/.tools/nunit/nunit-console.exe"

dotnet restore tests/AssetManagement.Tests/AssetManagement.Tests.csproj
dotnet build tests/AssetManagement.Tests/AssetManagement.Tests.csproj -c Release

if [ ! -f "$NUNIT_CONSOLE" ]; then
  echo "NUnit runner missing. Run tools/ci/setup-cloud-environment.sh first." >&2
  exit 1
fi

export LC_ALL=en_KE.UTF-8
export LANG=en_KE.UTF-8

mono "$NUNIT_CONSOLE" "$TEST_DLL" -labels
