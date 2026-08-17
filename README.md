# Tickframe

F# OHLCV directive engine targeting .NET 10. Evaluates compact **directive
expressions** (e.g. `rsi:14 > close`, `ma:5 // ma:20`, `boll.upper`) over
OHLCV candle data via a typed `Series` result.

## Prerequisites

- .NET SDK 10 (`dotnet --version`)
- Dependencies are restored automatically: `FParsec 1.1.1`,
  `FacioQuo.Stock.Indicators 3.0.0`

## Quickstart

```sh
dotnet build
dotnet test                  # 42 tests (80-row synthetic OHLCV fixture)
dotnet pack -c Release
```

## How-to

### 1. Build a frame

```fsharp
open System
open Tickframe

let candles : Candle[] =
    [| for i in 1 .. 80 ->
        { Timestamp = DateTime(2024, 1, 1).AddMinutes(float i)
          Open      = decimal (100.0 + sin (float i) * 5.0)
          High      = decimal (102.0 + sin (float i) * 5.0)
          Low       = decimal ( 98.0 + cos (float i) * 5.0)
          Close     = decimal (101.0 + cos (float i) * 4.0)
          Volume    = decimal (1000 + (i % 7) * 50) } |]

let frame = OhlcvFrame.ofCandles candles
```

### 2. Evaluate directives

```fsharp
let rsiSignal = Directive.eval frame "rsi:14 > close"     // Series.Bool (bool[])
let ma        = Directive.eval frame "ma:20@close"         // Series.Float (float[])
let macd      = Directive.eval frame "macd.signal:,,5"     // args: ["","", "5"]
let upper     = Directive.eval frame "boll.upper"          // sub-command

match ma with
| Float values -> printfn "ma length %d, last %f" values.Length values.[values.Length - 1]
| Bool _       -> ()

match rsiSignal with
| Bool flags -> printfn "true count %d" (flags |> Array.filter id |> Array.length)
| Float _    -> ()
```

### 3. Columns, operators, and indicators

Columns are case-insensitive and fixed to
`open | high | low | close | volume`. Any other column name raises
`DirectiveValueError`.

Precedence (tightest → loosest): unary `-/~` → `* /` → `+ -` →
comparisons (`< <= == != >= >`, non-associative) → logical `& ^ |` (one level) →
cross `// \\ ><`.

```fsharp
let a = Directive.eval frame "close + open * 2"
let b = Directive.eval frame "close > open & high > low"
let c = Directive.eval frame "ma:5 // ma:20"     // cross up (gold cross)
let d = Directive.eval frame "ma:5 \\ ma:20"     // cross down
let e = Directive.eval frame "ma:5 >< ma:20"     // cross either way
let f = Directive.eval frame "-close"
let g = Directive.eval frame "~(close > open)"
```

### 4. Indicator arguments and operands

Args are positional scalars after `:`, empty slots preserved as `""` (e.g.
`"macd.signal:,,5"` → `[""; ""; "5"]`). Multi-output indicators expose
sub-commands (e.g. `boll.upper`, `macd.signal`, `macd.histogram`, `stoch.k`).

Operand overrides follow `@`; each entry is a column or a parenthesized
nested directive. Default operands depend on the indicator's `InputSlots`.

```fsharp
let maOpen   = Directive.eval frame "ma:10@open"
let nested   = Directive.eval frame "ma:10@(ma:5)"
let inc      = Directive.eval frame "increase:3@(ma:20@close)"
let repeated = Directive.eval frame "repeat:3@(close > open)"
```

### 5. Warm-up and lookback

Indicator results are batch-only; rows before enough history are
`Double.NaN` for `Float` and `false` for `Bool`. Cross operators also return
`false` at `i = 0` and whenever a `NaN` participates (IEEE semantics).

```fsharp
let lb = Directive.lookback "ma:20"        // minimum warm-up rows
let lb2 = Directive.lookback "ma:20@(ma:5)"
```

### 6. Error handling

- `DirectiveSyntaxError` — parse failures with `Line`/`Column`.
- `DirectiveValueError` — unknown indicator/sub-command/column, bad args,
  wrong series count, type errors (e.g. arithmetic on `Bool`).

`DirectiveParser.parse : string -> Result<Expr, DirectiveError>` returns a
`Result` for non-exceptional handling; `Directive.eval`/`lookback` raise.

```fsharp
match DirectiveParser.parse "rsi:14 >" with
| Ok expr    -> printfn "%A" expr
| Error err  -> printfn "parse failed: %s" err.Message

try Directive.eval frame "unknown:14" |> ignore
with :? DirectiveValueError as ex -> printfn "bad indicator: %s" ex.Message
```

### 7. Extending the registry

Indicators live only in `Registry.fs`. Add a new `IndicatorSpec` (name,
aliases, slots, compute, lookback, sub-commands) and register it in
`Registry.buildTable`. No parser/evaluator changes are needed unless a new
operator shape is introduced.

Batch bridge: resolved `float[]` operands → synthetic `Bar[]` →
`FacioQuo` `ToXxx` → `Float`/`Bool` projection (`decimal → double` at the
boundary; `null`/`Nullable` → `NaN` or `false`). See `docs/facioquo-v3-api.md`
and `specs/05-indicator-registry.md`.

## Project layout

- `Contract.fs` — `DirectiveError`, `Candle`/`OhlcvFrame`, `Expr`/`IndicatorCall`/`SeriesRef`, `Series`, `IndicatorSpec`/`InputSlots`/`EvalContext`
- `Parser.fs` — `DirectiveParser.parse` (FParsec)
- `Registry.fs` — `Registry.buildTable` / `IndicatorRegistry` (FacioQuo bridge)
- `Evaluator.fs` — `Evaluator.eval` / `lookback` / `resolveSeriesRef`
- `Library.fs` — `Directive.eval` / `lookback`
- `Tickframe.Benchmarks/` — BenchmarkDotNet harness (parser, pure ops, indicators)
- `tests/Tickframe.Tests/` — xUnit + 80-row fixture (`Shared.fs`, `EvaluatorTests.fs`)

Compile order: `Contract -> Parser -> Registry -> Evaluator -> Library`.

Specs: see `specs/README.md` (01 data model → 07 testing/acceptance).

## Development

```sh
dotnet build
dotnet test
dotnet pack -c Release
dotnet run -c Release --project Tickframe.Benchmarks -- --all
dotnet run -c Release --project Tickframe.Benchmarks -- --filter="*ScaleBenchmarks*"
dotnet format   # if Fantomas is installed
```

## Benchmarks

Preliminary **Dry** numbers (BenchmarkDotNet 0.15.8, .NET 10, 12th Gen i5-12500H,
Release, `Dry` with warmup=1/iter=1 — expect noise; `Mean` here includes process
startup overhead, so compare *relative* cost across N/directives, not absolute ms).

### Parse (directive size, not N)

| Method | Mean | Allocated |
|---|---:|---:|
| ParseSimple (`rsi:14 > close`) | 17.7 ms | 5 KB |
| ParseComplex (`ma:5 // ma:20`) | 18.0 ms | 7.3 KB |
| ParseNested (`ma:10@(ma:5) + boll.upper`) | 19.1 ms | 9.3 KB |
| ParseSubArgs (`macd.signal:,,5`) | 18.2 ms | 4.8 KB |

### Scale — `Directive.eval` across N candles (Dry)

| Method | N=500 | N=1000 | N=5000 | N=10000 | N=20000 |
|---|---:|---:|---:|---:|---:|
| `ma:20` | 69.3 ms | 70.1 ms | 73.2 ms | 78.6 ms | 85.4 ms |
| `rsi:14` | 71.4 ms | 71.1 ms | 72.8 ms | 79.5 ms | 81.9 ms |
| `ema:20` | 70.6 ms | 68.6 ms | 72.1 ms | 79.7 ms | 82.9 ms |
| `macd.signal:,,5` | 68.9 ms | 70.0 ms | 73.0 ms | 81.1 ms | 81.4 ms |
| `boll.upper` | 70.4 ms | 73.9 ms | 76.5 ms | 83.5 ms | 88.1 ms |
| `atr:14` | 67.8 ms | 68.5 ms | 74.6 ms | 86.1 ms | 85.0 ms |
| `close` | 31.7 ms | 30.8 ms | 32.0 ms | 33.4 ms | 34.0 ms |
| `ma:5 // ma:20` (cross, 2× ma) | 72.8 ms | 71.3 ms | 83.4 ms | 85.9 ms | 90.5 ms |
| `ma:10@(ma:5)` (nested) | 70.7 ms | 71.3 ms | 80.6 ms | 83.2 ms | 89.4 ms |

Pure ops (`close`, `close + open * 2`, `close // open`) scale near-linearly
with N (just `Array.map2/mapi`), while FacioQuo-backed indicators add
`Bar`→`IReusable`→`ToXxx` overhead and allocations (e.g. `boll.upper` at
20k ≈ 10 MB). For production numbers use a real run:

```sh
# From Program.fs, switch config from Job.Dry to Job.Default before publishing
dotnet run -c Release --project Tickframe.Benchmarks -- --filter="*Scale*"
```

Benchmark sources: `Tickframe.Benchmarks/Program.fs` (`Fixtures`, `ParseBenchmarks`,
`EvalPureBenchmarks`, `ScaleBenchmarks`, `ScalePureBenchmarks`, `LookbackBenchmarks`).

## License

MIT
