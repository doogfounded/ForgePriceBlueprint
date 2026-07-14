#!/usr/bin/env bash
# scripts/run_end_to_end.sh
# Small helper to run the end-to-end flow from a Unix-like environment.
# Usage: from the repository root: ./scripts/run_end_to_end.sh

set -euo pipefail

echo "== ForgePriceBlueprint: end-to-end runner =="

if ! command -v dotnet >/dev/null 2>&1; then
  echo "Error: dotnet SDK not found in PATH. Please install .NET SDK to run the C# generator."
  exit 1
fi

if ! command -v g++ >/dev/null 2>&1; then
  echo "Warning: g++ not found in PATH. You can build the PriceBlueprint project with Visual Studio on Windows instead."
  echo "Attempting to continue, but build will fail without a C++ compiler."
fi

# 1) Generate the JSON blueprint
echo "--> Generating enterprise_blueprint.json via the Forge C# project..."
dotnet run --project Forge/Forge.csproj

# 2) Build the C++ runtime
echo "--> Building C++ runtime (PriceBlueprint)..."
# If g++ is available, use it; otherwise inform the user
if command -v g++ >/dev/null 2>&1; then
  # Auto-detect vcpkg include directory for nlohmann/json headers
  VCPKG_INCLUDE="D:/Desktop/git-stuff/vcpkg/installed/x64-windows/include"
  if [ ! -d "$VCPKG_INCLUDE" ]; then
    VCPKG_INCLUDE="/mnt/d/Desktop/git-stuff/vcpkg/installed/x64-windows/include"
  fi

  VCPKG_FLAGS=""
  if [ -d "$VCPKG_INCLUDE" ]; then
    echo "    Found vcpkg includes at: $VCPKG_INCLUDE"
    VCPKG_FLAGS="-I$VCPKG_INCLUDE"
  fi

  g++ -std=c++17 PriceBlueprint/src/main.cpp -IPriceBlueprint/include $VCPKG_FLAGS -o price_blueprint
else
  echo "Skipping local g++ build. If you're on Windows, open ForgePriceBlueprint.slnx in Visual Studio and build the PriceBlueprint project."
fi

# 3) Run the runtime (no args)
echo "--> Running price_blueprint (no arguments)"
if [ -x ./price_blueprint ]; then
  ./price_blueprint
else
  echo "Executable ./price_blueprint not found or not executable. Build step may have failed."
  exit 1
fi

echo "== Done =="
