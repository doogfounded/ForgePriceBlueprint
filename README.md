# mypricecalc

A small price calculator for Windows. One starting price, a list of adjustments, a total.

Each adjustment is either a percentage or a flat amount and can be switched off without
deleting it. They apply top to bottom, so order matters — 15% off then $5 off is not the
same as $5 off then 15% off. An optional rounding step then snaps the total to a whole
dollar, `.99`, `.95`, a nickel, or a dime.

Nothing is saved to disk and there are no files to manage. Copy the summary when you want
to keep a result.

---

## Run it

```powershell
dotnet run --project MyPriceCalc/MyPriceCalc.csproj
```

## Test it

```powershell
dotnet test MyPriceCalc.Tests
```

## Build a single executable

```powershell
dotnet publish MyPriceCalc/MyPriceCalc.csproj -c Release -r win-x64
```

The output is a self-contained `MyPriceCalc.exe` with no runtime or installer required.

---

## Layout

| Path | What it is |
| --- | --- |
| `MyPriceCalc/App.xaml`, `App.xaml.cs` | Application entry point. |
| `MyPriceCalc/MainWindow.xaml` | The window: inputs on the left, total on the right. |
| `MyPriceCalc/MainWindow.xaml.cs` | Builds the adjustment rows and recalculates on every edit. |
| `MyPriceCalc/Models/Calculator.cs` | `Adjustment`, `RoundingMode`, and the arithmetic. Pure functions, no UI. |
| `MyPriceCalc/Controls/Theme.cs` | The colour palette. |
| `MyPriceCalc.Tests/` | Tests for the arithmetic and for the window's live recalculation. |

`Calculator.Evaluate` takes a base price, the rows, and a rounding mode, and returns the
total plus the running price after each row. All money is `decimal`, so cents do not drift
the way they can with floating point.
