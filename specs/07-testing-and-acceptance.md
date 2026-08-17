# Spec 07 — Testing and Acceptance

Tests target three layers: parser, evaluator, and indicator-registry bridge.

## Test framework

Use a standard F# test project (xUnit or NUnit) targeting `net10.0`, referencing the
Tickframe library and `FacioQuo.Stock.Indicators` 3.0.0 for direct cross-checks.

## Fixture

Use a fixed synthetic OHLCV fixture with a known, increasing timestamp and hand-checked
values. The fixture should have enough rows (at least 80) to exercise lookback and
cross operations.

```fsharp
let candles = [
    for i in 1 .. 80 ->
        {
            Timestamp = DateTime(2024, 1, 1).AddMinutes(float i)
            Open  = decimal (100.0 + sin (float i) * 5.0)
            High  = decimal (102.0 + sin (float i) * 5.0)
            Low   = decimal ( 98.0 + cos (float i) * 5.0)
            Close = decimal (101.0 + cos (float i) * 4.0)
            Volume = decimal (1000 + (i % 7) * 50)
        }
]
```

## Parser tests

| Case | Expected |
| --- | --- |
| `close` | `Column "close"` |
| `rsi:14` | `Indicator { Name = "rsi"; Args = ["14"] }` |
| `macd.signal:,,5` | `Args = [""; ""; "5"]` |
| `ma:10@open` | `Series = [SeriesColumn "open"]` |
| `ma:10@(ma:5)` | `Series = [SeriesExpr (Indicator ...)]` |
| `-5` | `Number -5.0` |
| `~x` with bool expr | `Unary (Not, ...)` |
| `a + b * c` | precedence: `a + (b * c)` |
| `(a + b) * c` | grouping respected |
| `a & b | c` | left-associative |
| `a // b` | `Binary (CrossUp, a, b)` |
| `a \\ b` | `Binary (CrossDown, a, b)` |
| `a >< b` | `Binary (CrossAny, a, b)` |
| multiline directive | parses identically to single-line |
| `a >` | `DirectiveSyntaxError` |
| `a < b < c` | `DirectiveSyntaxError` |
| `ma:10@` | `DirectiveSyntaxError` |

## Evaluator tests

Using `Directive.eval df directive`, assert on type, length, warm-up, and spot values.

| Directive | Expected |
| --- | --- |
| `close` | `Float`, length 80, matches fixture close |
| `ma:20` | `Float`, first 19 entries `NaN` |
| `rsi:14` | `Float`, first warm-up entries `NaN` |
| `rsi:14 > close` | `Bool`, length 80 |
| `ma:20 > ma:50` | `Bool`, length 80 |
| `(ma:20 > ma:50)` | identical to unparenthesized |
| `ma:5 // ma:20` | `Bool`, cross-up true at expected index |
| `ma:5 \\ ma:20` | `Bool`, cross-down true at expected index |
| `macd.signal:,,5` | `Float`, warm-up rows `NaN` |
| `boll.upper` | `Float` |
| `increase:3@(ma:20@close)` | `Bool`, length 80 |
| `repeat:3@(close > open)` | `Bool`, length 80 |
| `close > open` | `Bool` |

Warm-up assertions may use `Double.IsNaN` rather than exact NaN bit patterns.

## Cross semantics tests

Build tiny frames to isolate cross behavior:

| Case | Expected |
| --- | --- |
| `a[i-1] <= b[i-1] && a[i] > b[i]` | `CrossUp` true |
| `a[i-1] >= b[i-1] && a[i] < b[i]` | `CrossDown` true |
| `i = 0` | all cross false |
| `NaN` operand | comparison false, cross false |

## Type error tests

| Directive | Expected |
| --- | --- |
| `(close > open) + close` | `DirectiveValueError` (arithmetic on bool) |
| `close & open` | `DirectiveValueError` (logical on float) |
| `(close > open) // close` | `DirectiveValueError` (cross on bool) |
| `unknown:14` | `DirectiveValueError` (unknown indicator) |
| `rsi:14@(close > open)` | `DirectiveValueError` (bool into float indicator) |

## Registry cross-check

For a small set of indicators, compare Tickframe output against direct FacioQuo v3
batch output:

- `ma` vs `bars.ToSma(20)`
- `ema` vs `bars.ToEma(20)`
- `rsi` vs `bars.ToRsi(14)`
- `macd.signal` vs `bars.ToMacd(...)`
- `boll.upper` vs `bars.ToBollingerBands(...)`
- `atr` vs `bars.ToAtr(14)`

All decimal-to-double conversions and warm-up positions must match after converting
the direct FacioQuo result's relevant field to `double`.

## Acceptance criteria

The implementation satisfies this spec when all of the following pass:

```text
Directive.eval df "rsi:14 > close"              -> Bool[]
Directive.eval df "ma:20@close"                 -> Float[]
Directive.eval df "(ma:20 > ma:50)"             -> Bool[]
Directive.eval df "ma:5 // ma:20"               -> Bool[]
Directive.eval df "macd.signal:,,5"             -> Float[]
Directive.eval df "increase:3@(ma:20@close)"    -> Bool[]
```

plus:

- parser precedence and multi-character token tests pass,
- warm-up NaN semantics pass,
- cross operator semantics pass,
- type errors raise `DirectiveValueError`,
- syntax errors raise `DirectiveSyntaxError` with line/column,
- registry cross-check values match FacioQuo v3 within decimal-to-double precision.
