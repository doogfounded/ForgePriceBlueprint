param(
    [switch]$Launch
)

$ErrorActionPreference = "Stop"

# Ensure execution occurs from the repository root directory regardless of where called
Push-Location "$PSScriptRoot\.."

Write-Host "== ForgePriceBlueprint: end-to-end runner (Windows PowerShell) ==" -ForegroundColor Cyan

# 1. Verify .NET SDK is installed
if (-not (Get-Command "dotnet" -ErrorAction SilentlyContinue)) {
    Write-Error "Error: dotnet SDK not found in PATH. Please install .NET SDK."
}

# 2. Run unit & integration tests
Write-Host "`n--> Running .NET test suite..." -ForegroundColor Green
dotnet test Forge.Tests --nologo -v q

# 3. Locate compiler and build
Write-Host "`n--> Building C++ runtime (price_blueprint.dll)..." -ForegroundColor Green

if (Get-Command "g++" -ErrorAction SilentlyContinue) {
    Write-Host "    Found g++ compiler. Building with g++..." -ForegroundColor DarkGray
    
    $vcpkgInclude = "D:\Desktop\git-stuff\vcpkg\installed\x64-windows\include"
    if ($env:VCPKG_ROOT -and (Test-Path "$env:VCPKG_ROOT\installed\x64-windows\include")) {
        $vcpkgInclude = "$env:VCPKG_ROOT\installed\x64-windows\include"
    }

    $vcpkgFlags = @()
    if (Test-Path $vcpkgInclude) {
        Write-Host "    Found vcpkg includes at: $vcpkgInclude" -ForegroundColor DarkGray
        $vcpkgFlags = "-I$vcpkgInclude"
    }

    Write-Host "    Compiling shared library (price_blueprint.dll)..." -ForegroundColor DarkGray
    g++ -shared -fPIC -static -static-libgcc -static-libstdc++ -std=c++17 PriceBlueprint/src/library.cpp -IPriceBlueprint/include $vcpkgFlags -o price_blueprint.dll
}
else {
    Write-Error "Error: g++ compiler not found."
}

if ($Launch) {
    Write-Host "`n--> Launching Forge Desktop App..." -ForegroundColor Green
    dotnet run --project Forge/Forge.csproj
}

Pop-Location

Write-Host "`n== Done ==" -ForegroundColor Cyan
