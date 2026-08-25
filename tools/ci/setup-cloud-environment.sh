#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$ROOT"

if ! command -v dotnet >/dev/null 2>&1; then
  curl -fsSL https://packages.microsoft.com/config/ubuntu/24.04/packages-microsoft-prod.deb -o /tmp/packages-microsoft-prod.deb
  sudo dpkg -i /tmp/packages-microsoft-prod.deb
  sudo apt-get update
  sudo apt-get install -y dotnet-sdk-8.0
fi

if ! command -v mono >/dev/null 2>&1; then
  sudo apt-get update
  sudo apt-get install -y mono-complete unzip locales
fi

if ! locale -a 2>/dev/null | grep -qi 'en_KE'; then
  if [ -f /etc/locale.gen ] && grep -q '# en_KE.UTF-8' /etc/locale.gen; then
    sudo sed -i 's/# en_KE.UTF-8 UTF-8/en_KE.UTF-8 UTF-8/' /etc/locale.gen
  elif [ -f /etc/locale.gen ] && ! grep -q 'en_KE.UTF-8' /etc/locale.gen; then
    echo 'en_KE.UTF-8 UTF-8' | sudo tee -a /etc/locale.gen >/dev/null
  fi
  sudo locale-gen en_KE.UTF-8 2>/dev/null || true
fi

NUNIT_DIR="$ROOT/.tools/nunit"
NUNIT_CONSOLE="$NUNIT_DIR/nunit-console.exe"
if [ ! -f "$NUNIT_CONSOLE" ]; then
  mkdir -p "$NUNIT_DIR"
  curl -fsSL "https://www.nuget.org/api/v2/package/NUnit.Runners/2.6.4" -o /tmp/nunit.runners.zip
  rm -rf /tmp/nunit.runners
  unzip -qo /tmp/nunit.runners.zip -d /tmp/nunit.runners
  cp -r /tmp/nunit.runners/tools/* "$NUNIT_DIR/"
fi

dotnet restore tests/AssetManagement.Tests/AssetManagement.Tests.csproj
dotnet build tests/AssetManagement.Tests/AssetManagement.Tests.csproj -c Release
