#!/usr/bin/env bash
# scripts/run_end_to_end.sh
# Small helper to run the end-to-end flow from a Unix-like environment.
# Usage: from the repository root: ./scripts/run_end_to_end.sh

set -euo pipefail

echo "== ForgePriceBlueprint: end-to-end runner =="

if ! command -v dotnet >/dev/null 2>&1; then
  echo "Error: dotnet SDK not found in PATH. Please install .NET SDK."
  exit 1
fi

# 1) Run tests
echo "--> Running .NET test suite..."
dotnet test Forge.Tests --nologo -v q

# 2) Build the C++ runtime
echo "--> Building C++ runtime library (PriceBlueprint)..."
if command -v g++ >/dev/null 2>&1; then
  VCPKG_INCLUDE="D:/Desktop/git-stuff/vcpkg/installed/x64-windows/include"
  if [ ! -d "$VCPKG_INCLUDE" ]; then
    VCPKG_INCLUDE="/mnt/d/Desktop/git-stuff/vcpkg/installed/x64-windows/include"
  fi

  VCPKG_FLAGS=""
  if [ -d "$VCPKG_INCLUDE" ]; then
    echo "    Found vcpkg includes at: $VCPKG_INCLUDE"
    VCPKG_FLAGS="-I$VCPKG_INCLUDE"
  fi

  g++ -shared -fPIC -static -static-libgcc -static-libstdc++ -std=c++17 PriceBlueprint/src/library.cpp -IPriceBlueprint/include $VCPKG_FLAGS -o price_blueprint.dll
else
  echo "Skipping local g++ build. If you're on Windows, build with Visual Studio."
fi

echo "== Done =="
