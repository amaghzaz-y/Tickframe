# Memory

## Project Overview
Tickframe — F# OHLCV Directive Engine (.NET 10). Evaluates directive expressions (e.g. `rsi:14 > close`, `ma:5 // ma:20`) over OHLCV candle data. See `README.md` and `specs/README.md`.

Public entry: `Directive.eval : OhlcvFrame -> string -> Series` and `Directive.lookback : string -> int` (`Library.fs`).

Dependencies: `Deedle 8.0.0`, `FParsec 1.1.1`, `FacioQuo.Stock.Indicators 3.0.0` (net10.0).

## Specification Files (specs/)
1. `01-data-model.md` — Candle, OhlcvFrame
2. `02-directive-grammar.md` — EBNF, precedence, tokens
3. `03-ast-and-parser.md` — Expr AST, FParsec structure
4. `04-evaluation-semantics.md` — typed evaluator, Series, cross semantics
5. `05-indicator-registry.md` — FacioQuo v3 registry, InputSlots, sub-commands
6. `06-errors-and-validation.md` — DirectiveSyntaxError / DirectiveValueError, lookback
7. `07-testing-and-acceptance.md` — test plan and acceptance criteria

## Architecture Notes
- `Contract.fs` — single contract: DirectiveError hierarchy, Candle/OhlcvFrame, Expr/IndicatorCall/SeriesRef AST, Series, IndicatorSpec/InputSlots/EvalContext
- `Parser.fs` — `DirectiveParser.parse` (FParsec), precedence: unary > * / > + - > comparisons (non-assoc) > logical (&^| single level) > cross (// \\ ><)
- `Evaluator.fs` — `Evaluator.eval` / `lookback` / `resolveSeriesRef`; pure, batch-only, NaN warm-up, IEEE semantics
- `Registry.fs` — `Registry.buildTable` / `IndicatorRegistry` (resolve/tryResolve/table) — uniform Bar bridge to FacioQuo
- `Library.fs` — `Directive.eval` / `lookback` (parse then eval)
- Compile order in `Tickframe.fsproj`: Contract -> Parser -> Registry -> Evaluator -> Library

Implementation status: 01/02/03 done; 04 done for pure ops (indicator path delegates to registry); 05 TODO (Registry is Map.empty); 06 partial (arg/series validation needs 05); 07 TODO (placeholder test only).

## Code Style Guidelines
- Use descriptive variable names
- Follow existing patterns in the codebase
- Extract complex conditions into meaningful boolean variables
- F#: descriptive names, match existing module/namespace style (Tickframe), keep decimal at Candle/Bar boundary, float[] for evaluation

## Common Workflows
```sh
dotnet build
dotnet test
dotnet pack
```
Fixture for tests: 80-row synthetic OHLCV (see 07-testing-and-acceptance.md). No caching/incremental refresh in v1; warm-up rows are NaN/false.
