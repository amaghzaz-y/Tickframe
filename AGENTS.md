# Memory

## Project Overview
Tickframe — F# OHLCV Directive Engine (.NET 10). Evaluates directive expressions (e.g. `rsi:14 > close`, `ma:5 // ma:20`) over OHLCV candle data. Public entry: `Directive.eval : OhlcvFrame -> string -> Series` and `Directive.lookback : string -> int` (`Library.fs`). `DirectiveParser.parse : string -> Result<Expr, DirectiveError>` for non-throwing parse; `DirectiveError` has `DirectiveSyntaxError` (Line/Column) vs `DirectiveValueError`.

Dependencies: `FParsec 1.1.1`, `FacioQuo.Stock.Indicators 3.0.0` (net10.0). No Deedle — `OhlcvFrame` is a hand-rolled `Candle[]` with 5 `Column` readers.

## Architecture Notes
- `Contract.fs` — single contract: `DirectiveError` hierarchy, `Candle`/`OhlcvFrame`, `Expr`/`IndicatorCall`/`SeriesRef` AST, `Series`, `IndicatorSpec`/`InputSlots`/`EvalContext`. `NoWarn 3391` for FacioQuo `Nullable` conversions.
- `Parser.fs` — `DirectiveParser.parse` (FParsec). Forward refs: `pExprRef` for `()`/`@(...)` and `pUnaryRef` for chained unary. Precedence: unary > `* /` > `+ -` > comparisons (non-assoc, `attempt pLe/pGe/pEq/pNe` before `pGt/pLt`) > logical (`&^|` single level via `pChainWs`) > cross (`// \\ ><`). `pArgList` preserves empty slots; `pSeriesList` is `@`-triggered; `pIndicatorCore` distinguishes `Column` vs `Indicator` by name. `pPrimary` orders: `(...)` / neg number / `pNumber` / `pIndicatorCore`. `pChainWs` is `spaces >>. op .>> spaces` to keep indicators like `rsi:14 > close` and `ma:5 // ma:20` from swallowing trailing ops into args.
- `Evaluator.fs` — `Evaluator.eval` / `lookback` / `resolveSeriesRef`; pure, batch-only, `NaN` warm-up, IEEE semantics. Sub-command dispatch: delegates to `spec.Compute` (not `subSpec.Compute`); validates `Map.containsKey sub spec.SubCommands` first.
- `Registry.fs` — `Registry.buildTable` / `IndicatorRegistry` (resolve/tryResolve/table) — uniform bridge: `float[]` operands → `decimal` → synthetic `Bar`/`Reusable.ToReusable(..., CandlePart.Close)` → FacioQuo `ToXxx`. `Null`/`Nullable` → `NaN`/`false` via `toFloatN`/`toFloatDecimal`. Includes synthetic `increase`/`repeat` (bool) and `kdj` via `Stoch.ToStoch` (J = `StochResult.J`).
- `Library.fs` — `Directive.eval` / `lookback` (parse then eval)
- `Tickframe.Benchmarks/` — BenchmarkDotNet harness (directive eval, parse, pure ops, cross, indicator subset, lookback, `BENCHMARKS.md`)
- `docs/facioquo-v3-api.md` — verified FacioQuo v3 signatures
- Compile order in `Tickframe.fsproj`: Contract -> Parser -> Registry -> Evaluator -> Library

Parser gotchas: `pArgList` must use `attempt` around comma branches so `rsi:14 > close` does not consume `>` as an empty arg continuation; `pSeriesList` and `pIndicatorCore` `.`/`:`/`@` prefixes must be `attempt`d.

## Code Style Guidelines
- Use descriptive variable names; follow existing patterns
- Extract complex conditions into meaningful boolean variables
- Keep `decimal` at the Candle/Bar boundary, `float[]` for evaluation; convert once in the registry
- Keep `Registry` helpers in `RegistryHelpers` (toReusable/toBars/parseInt/parseFloat/projectFloat/projectBool)

## Common Workflows
```sh
dotnet build
dotnet test                          # 42 tests, 80-row fixture (Shared.fs)
dotnet pack -c Release
dotnet run -c Release --project Tickframe.Benchmarks
```
Fixture: 80-row synthetic OHLCV (`Shared.fs`). No caching/incremental refresh in v1; warm-up rows are `NaN`/`false`. See `README.md` for directive examples, precedence table, and extension guide.

## Pre-PR gate
```sh
dotnet tool restore
dotnet restore
dotnet build -c Release --no-restore -warnaserror
dotnet fantomas --check .
dotnet dotnet-fsharplint lint Tickframe.slnx
dotnet test -c Release --no-build
```
Configured in `.github/workflows/ci.yml` (push on `main` + PRs). Tools: `fantomas 7.0.5`, `dotnet-fsharplint 0.27.0` via `.config/dotnet-tools.json`; lint config `fsharplint.json`, solution `Tickframe.slnx`.
