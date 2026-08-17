# Tickframe — F# OHLCV Directive Engine Specification

Tickframe evaluates **directive expressions** over **candlestick (OHLCV) data**.
The library provides a small directive expression language (e.g. `rsi:14 > close`)
with a natural F# API, typed evaluator, and indicator registry over a well-known
indicator library.

The library evaluates directive expressions over OHLCV data only. No Python,
pandas, NumPy, Arrow, DLPack, timezone, resampling, or live cumulation features
are included.

## Public entry point

```fsharp
open Tickframe

let candles = [ Candle.create ... ]   // see 01-data-model.md

let df = OhlcvFrame.ofCandles candles

let bullish = Directive.eval df "rsi:14 > close"   // bool[]
let ma      = Directive.eval df "ma:20@close"       // float[]
let signal  = Directive.eval df "macd.signal:,,5"   // float[]
```

`Directive.eval` returns a `Series` value — either the `Float` or `Bool` case of the
`Series` union defined in the evaluation spec.

## Confirmed dependencies

| Concern | Package | Version | Target |
| --- | --- | --- | --- |
| Columnar frame projection | `Deedle` | `8.0.0` | current stable |
| Directive parser | `FParsec` | `1.1.1` | .NET Standard 2.0 |
| Indicator kernels | `FacioQuo.Stock.Indicators` | `3.0.0` | .NET 8.0 |

> `FacioQuo.Stock.Indicators` is the renamed v3 successor to the deprecated
> `Skender.Stock.Indicators` package (v2 2.7.3 stops receiving maintenance updates
> at the end of 2026). Tickframe must target v3 only.

All dependencies are consumable from the existing `net10.0` project.

## Scope

In scope:

- OHLCV `Candle` and `OhlcvFrame`.
- Directive parser and AST.
- Typed evaluator with arithmetic, comparison, logical, and cross operators.
- Extensible indicator registry dispatching directive names to FacioQuo v3.
- `@` operand overrides and nested directives.
- Multi-output sub-commands (`macd.signal`, `boll.upper`, `kdj.k`, ...).
- Typed errors: `DirectiveSyntaxError`, `DirectiveValueError`.
- Batch recompute only; no caching or incremental refresh in v1.

Out of scope:

- pandas / NumPy / Arrow / DLPack interop.
- Timezones, `DatetimeIndex`, cumulate/resample, live append/fulfill.
- Bounded rolling window / `max_lookback`.
- General dataframe indexing (`.loc` / `.iloc`), string/datetime columns.
- Deedle rolling/ewm compatibility surface.
- Indicators absent from FacioQuo (e.g. BRAR, DKX, PSY, BIAS) are not included;
  names that *are* present in FacioQuo (KDJ, ConnorsRSI, CMF, CHOP, DPO, TSI,
  etc.) are included.

## Specification files

1. [`01-data-model.md`](./01-data-model.md) — `Candle`, `OhlcvFrame`, series model.
2. [`02-directive-grammar.md`](./02-directive-grammar.md) — full EBNF and operators.
3. [`03-ast-and-parser.md`](./03-ast-and-parser.md) — F# AST and FParsec structure.
4. [`04-evaluation-semantics.md`](./04-evaluation-semantics.md) — typed evaluator.
5. [`05-indicator-registry.md`](./05-indicator-registry.md) — registry and mapping.
6. [`06-errors-and-validation.md`](./06-errors-and-validation.md) — error rules.
7. [`07-testing-and-acceptance.md`](./07-testing-and-acceptance.md) — test plan.

## Naming conventions

- Column names are lowercase and canonical: `open`, `high`, `low`, `close`, `volume`.
- Directive command names are case-insensitive; registry keys are lowercase.
- FacioQuo results are converted from `decimal` to `double` exactly once, at the
  registry boundary. Missing/warm-up values become `NaN`.
