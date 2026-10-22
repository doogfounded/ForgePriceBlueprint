# ForgePriceBlueprint
The pricing operating system.

ForgePriceBlueprint combines a high-performance C++ pricing engine runtime (`PriceBlueprint`) with a native Windows desktop studio (`Forge`) built in WPF (.NET 10).

---

## Architecture Overview

1. **`PriceBlueprint` (C++)**:
   - High-throughput execution engine evaluating rule pipelines, expressions, condition keys, tiered volume rates, graduated discounts, price caps, and rounding.
   - Built as a shared library (`price_blueprint.dll`) exporting a thread-safe, reentrant `PB_Calculate` C ABI function.
   - Built optionally as a standalone CLI benchmark/runner (`price_blueprint.exe`).

2. **`Forge` (C# / WPF on .NET 10)**:
   - Modern native desktop studio without any local web server or browser dependencies.
   - **Pipeline Designer**: Add, configure, reorder, and collapse rule cards (BasePrice, TieredPricing, PercentageAdjustment, FlatAdjustment, PriceCap, Rounding).
   - **Live Simulator**: Dynamically infers context variables from active rule conditions and displays live prices, net savings, and complete step-by-step audit trails.
   - **Discount Curve**: Custom-rendered vector canvas plotting the price discount curve and highlighting current active quantity.
   - **What-If Scenario Matrix**: Run side-by-side comparative matrices, save current simulation states, and import/export scenario suites.
   - **Developer Hub**: Synchronized JSON and YAML editors with real-time validation feedback, plus an in-process anomaly detection analyzer.
   - **File Management**: Open, Save, Save As, and Recent Files history, with built-in presets (Enterprise Contract, SaaS Subscription, E-commerce Promo).

3. **`Forge.Tests` (C# / xUnit)**:
   - Comprehensive test suite covering DTOs, the fluent builder, schema validation, the in-process native engine interop wrapper, YAML converters, scenario serialization, and WPF UI smoke tests.

---

## Quickstart

### 1. Build the C++ Engine Runtime
Compile the C++ shared dynamic library (`price_blueprint.dll`) from the repository root:

```powershell
g++ -shared -fPIC -static -static-libgcc -static-libstdc++ -std=c++17 PriceBlueprint/src/library.cpp -IPriceBlueprint/include "-ID:\Desktop\git-stuff\vcpkg\installed\x64-windows\include" -o price_blueprint.dll
```

### 2. Run the Test Suite
```powershell
dotnet test Forge.Tests
```

### 3. Launch the Studio Desktop App
```powershell
dotnet run --project Forge/Forge.csproj
```

### 4. Publish Self-Contained Release
To produce a standalone, self-contained single-folder distribution without needing an installer:

```powershell
dotnet publish Forge/Forge.csproj -c Release -r win-x64 --self-contained true -o ./dist
```
