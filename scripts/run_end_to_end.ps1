# scripts/run_end_to_end.ps1
# Helper to run the end-to-end flow on Windows using PowerShell.
# Usage: .\scripts\run_end_to_end.ps1

$ErrorActionPreference = "Stop"

# Ensure execution occurs from the repository root directory regardless of where called
Push-Location "$PSScriptRoot\.."

Write-Host "== ForgePriceBlueprint: end-to-end runner (Windows PowerShell) ==" -ForegroundColor Cyan

# 1. Verify .NET SDK is installed
if (-not (Get-Command "dotnet" -ErrorAction SilentlyContinue)) {
    Write-Error "Error: dotnet SDK not found in PATH. Please install .NET SDK to run the C# generator."
}

# 2. Run C# generator
Write-Host "`n--> Generating enterprise_blueprint.json via the Forge C# project..." -ForegroundColor Green
dotnet run --project Forge/Forge.csproj

# 3. Locate compiler and build
Write-Host "`n--> Building C++ runtime (PriceBlueprint)..." -ForegroundColor Green

$builtExecutable = ""

# Try g++ first
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

    g++ -std=c++17 PriceBlueprint/src/main.cpp -IPriceBlueprint/include $vcpkgFlags -o price_blueprint.exe
    $builtExecutable = ".\price_blueprint.exe"
}
else {
    # Try finding MSBuild
    Write-Host "    g++ not found. Searching for MSBuild..." -ForegroundColor DarkGray
    $msbuildPath = ""
    
    $commonPaths = @(
        "C:\Program Files (x86)\Microsoft Visual Studio\18\BuildTools\MSBuild\Current\Bin\MSBuild.exe",
        "C:\Program Files (x86)\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe",
        "C:\Program Files (x86)\Microsoft Visual Studio\2022\Professional\MSBuild\Current\Bin\MSBuild.exe",
        "C:\Program Files (x86)\Microsoft Visual Studio\2022\Enterprise\MSBuild\Current\Bin\MSBuild.exe"
    )

    foreach ($path in $commonPaths) {
        if (Test-Path $path) {
            $msbuildPath = $path
            break
        }
    }

    if (-not $msbuildPath -and (Get-Command "vswhere" -ErrorAction SilentlyContinue)) {
        $vsInstallPath = vswhere -latest -property installationPath
        if ($vsInstallPath) {
            $candidate = Join-Path $vsInstallPath "MSBuild\Current\Bin\MSBuild.exe"
            if (Test-Path $candidate) { $msbuildPath = $candidate }
        }
    }

    if ($msbuildPath) {
        Write-Host "    Found MSBuild at: $msbuildPath. Building vcxproj..." -ForegroundColor DarkGray
        & $msbuildPath PriceBlueprint/PriceBlueprint.vcxproj /p:Configuration=Debug /p:Platform=x64
        $builtExecutable = "PriceBlueprint\x64\Debug\PriceBlueprint.exe"
    }
    else {
        Write-Error "Error: No suitable C++ compiler (g++ or MSBuild.exe) was found."
    }
}

# 4. Run the resulting executable
if ($builtExecutable -and (Test-Path $builtExecutable)) {
    Write-Host "`n--> Running Price Engine ($builtExecutable)..." -ForegroundColor Green
    & $builtExecutable
} else {
    Write-Error "Error: Executable not found. Build step may have failed."
}

Pop-Location

Write-Host "`n== Done ==" -ForegroundColor Cyan
