# Memory

## Project Overview
Tickframe — F# OHLCV Directive Engine (.NET 10). Evaluates directive expressions (e.g. `rsi:14 > close`, `ma:5 // ma:20`) over OHLCV candle data. See `README.md` and `specs/README.md`.

Public entry: `Directive.eval : OhlcvFrame -> string -> Series` and `Directive.lookback : string -> int` (`Library.fs`).
`DirectiveParser.parse : string -> Result<Expr, DirectiveError>` for non-throwing parse; `DirectiveError` has `DirectiveSyntaxError` (Line/Column) vs `DirectiveValueError`.

Dependencies: `FParsec 1.1.1`, `FacioQuo.Stock.Indicators 3.0.0` (net10.0). No Deedle — `OhlcvFrame` is a hand-rolled `Candle[]` with 5 `Column` readers.

## Specification Files (specs/)
1. `01-data-model.md` — Candle, OhlcvFrame (Candle[] + 5 float[] columns; decimal→double at boundary)
2. `02-directive-grammar.md` — EBNF, precedence, tokens; args preserve empty slots, @ series is column or (directive)
3. `03-ast-and-parser.md` — Expr AST, FParsec structure
4. `04-evaluation-semantics.md` — typed evaluator, Series, cross semantics (i=0 false, NaN→false)
5. `05-indicator-registry.md` — FacioQuo v3 registry, InputSlots, sub-commands
6. `06-errors-and-validation.md` — DirectiveSyntaxError / DirectiveValueError, lookback
7. `07-testing-and-acceptance.md` — test plan and acceptance criteria

## Architecture Notes
- `Contract.fs` — single contract: DirectiveError hierarchy, Candle/OhlcvFrame, Expr/IndicatorCall/SeriesRef AST, Series, IndicatorSpec/InputSlots/EvalContext
- `Parser.fs` — `DirectiveParser.parse` (FParsec). Forward refs: `pExprRef` for `()`/`@(...)` and `pUnaryRef` for chained unary. Precedence: unary > * / > + - > comparisons (non-assoc, `attempt pLe/pGe/pEq/pNe` before `pGt/pLt`) > logical (&^| single level via `pChainWs`) > cross (// \\ ><). `pArgList` preserves empty slots; `pSeriesList` is `@`-triggered; `pIndicatorCore` distinguishes `Column` vs `Indicator` by name. `pPrimary` orders: `(...)` / neg number / `pNumber .>> spaces` / `pIndicatorCore`. `pChainWs` is `spaces >>. op .>> spaces` to keep indicators like `rsi:14 > close` and `ma:5 // ma:20` from swallowing trailing ops into args.
- `Evaluator.fs` — `Evaluator.eval` / `lookback` / `resolveSeriesRef`; pure, batch-only, NaN warm-up, IEEE semantics. Sub-command dispatch: delegates to `spec.Compute` (not `subSpec.Compute`); validates `Map.containsKey sub spec.SubCommands` first.
- `Registry.fs` — `Registry.buildTable` / `IndicatorRegistry` (resolve/tryResolve/table) — uniform bridge: `float[]` operands → `decimal` → synthetic `Bar`/`Reusable.ToReusable(..., CandlePart.Close)` → FacioQuo `ToXxx`. `Null`/`Nullable` → `NaN`/`false` via `toFloatN`/`toFloatDecimal`. Includes synthetic `increase`/`repeat` (bool) and `kdj` via `Stoch.ToStoch` (J = `StochResult.J`).
- `Library.fs` — `Directive.eval` / `lookback` (parse then eval)
- `Tickframe.Benchmarks/` — BenchmarkDotNet harness (directive eval, parse, pure ops, cross, indicator subset, lookback)
- Compile order in `Tickframe.fsproj`: Contract -> Parser -> Registry -> Evaluator -> Library

Implementation status: 01–07 done (42 tests). Registry covers the mapped FacioQuo surface; any signature divergence from `docs/facioquo-v3-api.md` is an implementation detail.

Parser gotchas: pNumber needs `.>> spaces` so `1 / 0` does not leave trailing space for the next chain; `pArgList` must use `attempt` around comma branches so `rsi:14 > close` does not consume `>` as an empty arg continuation; `pSeriesList` and `pIndicatorCore` `.`/`:`/`@` prefixes must be `attempt`d.

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
Fixture: 80-row synthetic OHLCV (`Shared.fs`, spec 07). No caching/incremental refresh in v1; warm-up rows are NaN/false. See `README.md` How-to for directive examples, precedence table, and extension guide.
