# Tickframe

F# OHLCV **directive engine** targeting .NET 10. Evaluates compact
**directive expressions** (e.g. `rsi:14 > close`, `ma:5 // ma:20`,
`boll.upper`, `macd.signal:,,5`, `ma:10@(ma:5)`) over OHLCV candle data
into a typed `Series` result (`Float` or `Bool`). Backed by
[FacioQuo](https://github.com/DaveSkender/Stock.Indicators) via a batch
`Bar` → `IReusable` bridge; parsing via `FParsec`.

## Prerequisites

- .NET SDK 10 (`dotnet --version`)
- Dependencies restored on build: `FParsec 1.1.1`, `FacioQuo.Stock.Indicators 3.0.0`

## Install

Tickframe is a library. Reference it from your F# project, or clone and
build this repo:

```sh
git clone <this-repo>
cd Tickframe
dotnet restore
dotnet build
```

From NuGet (once published): `dotnet add package Tickframe`.

## Quickstart

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

let rsiSignal = Directive.eval frame "rsi:14 > close"
let ma        = Directive.eval frame "ma:20@close"
let macd      = Directive.eval frame "macd.signal:,,5"
let upper     = Directive.eval frame "boll.upper"

match ma with
| Float values -> printfn "ma length %d, last %f" values.Length values.[values.Length - 1]
| Bool _       -> ()

match rsiSignal with
| Bool flags -> printfn "true count %d" (flags |> Array.filter id |> Array.length)
| Float _    -> ()
```

## Core concepts

### Candle and frame

```fsharp
type Candle = { Timestamp: DateTime; Open: decimal; High: decimal; Low: decimal; Close: decimal; Volume: decimal }
OhlcvFrame.ofCandles : seq<Candle> -> OhlcvFrame
OhlcvFrame.columnNames // ["open"; "high"; "low"; "close"; "volume"] (use OhlcvFrame.column/tryColumn as well)
frame.RowCount   // int
frame.Candles    // Candle[]
frame.Column "close"  // float[] — case-insensitive; DirectiveValueError on unknown column
```

Frames are batch-only; `Candle[]` is wrapped into 5 `float[]` columns once
(`decimal → double` at the boundary).

### Series

Every directive evaluates to `Series`:

```fsharp
type Series = Float of float[] | Bool of bool[]
Series.length / isFloat / asFloat / asBool
```

`Float` uses `Double.NaN` for warm-up rows; `Bool` uses `false`.

### Directives — syntax at a glance

```
directive   := expr
expr        := cross                          // //  \\  ><
cross       := logical { ("//" | "\\" | "><") logical }
logical     := comparison { ("&" | "^" | "|") comparison }
comparison  := additive [ ("<" | "<=" | "==" | "!=" | ">=" | ">") additive ]   // non-associative
additive    := multiplicative { ("+" | "-") multiplicative }
multiplicative := unary { ("*" | "/") unary }
unary       := ("-" | "~") unary | primary
primary     := column | number | indicator | "(" expr ")"
indicator   := name ["." sub] [":" arg {"," arg}] ["@" (column | "(" expr ")") {"," (column | "(" expr ")")}]
column      := "open" | "high" | "low" | "close" | "volume"   // case-insensitive
number      := float literal (finite)
arg         := scalar token (empty slots preserved: "macd.signal:,,5" → ["","", "5"])
```

Whitespace is significant around operators (`/` vs `//`); most tokens tolerate
surrounding spaces.

### Operators and precedence

Tightest → loosest:

1. Unary `-` (numeric negation), `~` (logical not, `Bool` only)
2. `*`, `/`
3. `+`, `-`
4. Comparisons `< <= == != >= >` — exactly one per expression, non-associative
5. Logical `&` `^` `|` — single level, left-associative
6. Cross `//` (up), `\\` (down), `><` (either) — lowest, left-associative

Cross semantics: at `i = 0` and whenever a `NaN` participates, result is
`false`; otherwise `a // b` is `a[i-1] <= b[i-1] && a[i] > b[i]` (and dual for
`\\`; `><` is `up || down`). Pure and indicator series both participate.

Examples:

```fsharp
let a = Directive.eval frame "close + open * 2"
let b = Directive.eval frame "close > open & high > low"
let c = Directive.eval frame "ma:5 // ma:20"      // gold cross
let d = Directive.eval frame "ma:5 \\ ma:20"
let e = Directive.eval frame "ma:5 >< ma:20"
let f = Directive.eval frame "-close"
let g = Directive.eval frame "~(close > open)"
let h = Directive.eval frame "(close + open) * 2"
```

### Columns

Exactly `open | high | low | close | volume` (case-insensitive). Anything
else raises `DirectiveValueError`, whether in a bare column, an indicator
operand, or nested.

### Indicator arguments and operand overrides

- Positional scalar args after `:`; empty slots are `""` and are forwarded to
  the indicator so `macd.signal:,,5` means `["", "", "5"]`.
- Multi-output indicators use sub-commands: `boll.upper` / `boll.lower` /
  `boll.middle`, `macd.signal` / `macd.histogram`, `stoch.k` / `.d` / `.j`,
  `donchian.upper`, `keltner.lower`, `ichimoku.tenkan`, etc.
- Operands after `@` override the indicator's default `InputSlots`; each entry
  is a column name or a parenthesized nested directive (which itself is
  evaluated first). Defaults per `InputSlots` (CloseOnly, HighLow, HighLowClose,
  OpenHighLowClose, CloseVolume, HighLowCloseVolume, OpenHighLowCloseVolume)
  apply when `@...` is absent. Wrong arity → `DirectiveValueError`.

```fsharp
let maOpen   = Directive.eval frame "ma:10@open"
let nested   = Directive.eval frame "ma:10@(ma:5)"
let inc      = Directive.eval frame "increase:3@(ma:20@close)"
let repeated = Directive.eval frame "repeat:3@(close > open)"
let corr     = Directive.eval frame "correlation:20@close,open"   // 2-operand (close.compareInRange)
```

### Indicators

All indicators live in `Registry.fs`. The registry bridges
`float[]` operands → `decimal` → synthetic `Bar[]` / `Reusable.ToReusable(..., Close)` →
FacioQuo `ToXxx` → `Float`/`Bool` projection (`decimal → double` at the boundary;
`null`/`Nullable` → `NaN` for floats, `false` for bools). See
`docs/facioquo-v3-api.md` for the FacioQuo surface and input families.

**Close-based** (`IReusable`) — `ma`/`sma`, `ema`, `wma`, `hma`, `dema`, `tema`,
`smma`/`mma`/`rma`, `epma`/`lsma`, `alma`, `kama`, `mama`/`mama.fama`, `t3`,
`dynamic`, `rsi`, `cmo`, `trix`, `roc`, `roc-wb`/`rocwb`, `stc`, `pmo`/`pmo.signal`,
`macd`/`macd.signal`/`macd.histogram`, `tsi`/`tsi.signal`, `connors-rsi`/`connorsrsi`,
`stoch-rsi`/`stochrsi` with `.k`/`.d`, `hurst`, `ulcer-index`/`ulcerindex`,
`fisher-transform`/`fishertransform`, `std-dev`/`stddev` with `.zscore`/`.mean`,
`slope`, `ht-trendline`/`httrendline`, `dpo`, `beta`, `correlation`, `prs`, etc.

**OHLC / OHLCV** (`IBar`) — `atr`/`natr`/`tr`, `adx`/`dmi`, `aroon`/`aroon.up`/`aroon.down`,
`cci`, `chop`, `bop`, `ultimate`, `williams-r`/`williamsr`, `stoch`/`kdj` (via
`Stoch.ToStoch`), `smi`/`smi.signal`, `boll`/`boll.upper`/`boll.lower`/`boll.middle`,
`donchian`, `keltner`, `starc-bands`/`starcbands`, `fcb` (`Decimal`), `ma-envelopes`/`maenvelopes`,
`std-dev-channels`/`stddevchannels`, `ichimoku` (`tenkan`/`kijun`/`senkou-a`/`senkou-b`/`chikou`),
`super-trend`/`supertrend` (`.direction` is `Bool`), `vortex` (`.plus`/`.minus`),
`alligator`, `elder-ray`/`elderray` (`.bull`/`.bear`), `gator`, `awesome`,
`atr-stop`/`atrstop`, `chandelier`, `parabolic-sar`/`parabolicsar`, `volatility-stop`/`volatilitystop`,
`doji`, `marubozu` (`Bool`), `pivots`, `fractal` (`Bool`), `adl`, `cmf`,
`chaikin-osc`/`chaikinosc`, `force-index`/`forceindex`, `kvo`/`kvo.signal`,
`mfi`, `obv`, `pvo`/`pvo.signal`, `vwap`, `vwma`, `bar-part`/`barpart` (`hl2`/`hlc3`/`oc2`/`ohl3`/`ohlc4`),
`heikin-ashi`/`heikinashi` (`.open`/`.high`/`.low`/`.close`), `renko`, `zig-zag`/`zigzag`,
`pivot-points`/`pivotpoints`, `rolling-pivots`/`rollingpivots`.

**Synthetic** (pure F#) — `increase:N@series` (`Bool`: strictly increasing over N bars),
`repeat:N@(boolSeries)` (`Bool`: true for N consecutive trues).

Defaults and var-args are documented per `IndicatorSpec` in `Registry.fs`;
unknown indicator or sub-command → `DirectiveValueError`. Adding a new indicator
is a single `IndicatorSpec` + an entry in `Registry.buildTable`; no parser or
evaluator change unless a new operator shape is introduced.

### Warm-up and lookback

Indicator results are batch-only: rows before enough history are `NaN` (or
`false` for `Bool`). `Directive.lookback : string -> int` returns the minimum
warm-up length including nested operands:

```fsharp
let lb  = Directive.lookback "ma:20"
let lb2 = Directive.lookback "ma:20@(ma:5)"
```

`lookback` parses the same grammar and delegates to each spec's `Lookback`.

### Error handling

- `DirectiveSyntaxError` — parse failures, with `Line` and `Column`.
- `DirectiveValueError` — unknown indicator/sub-command/column, bad args,
  wrong series count, type errors (e.g. arithmetic on `Bool`), or evaluator
  faults.

Non-throwing parse is available directly:

```fsharp
match DirectiveParser.parse "rsi:14 >" with
| Ok expr   -> printfn "%A" expr
| Error err -> printfn "parse failed: %s" err.Message

try Directive.eval frame "unknown:14" |> ignore
with :? DirectiveValueError as ex -> printfn "bad indicator: %s" ex.Message
```

### Extending the registry

Edit only `Registry.fs`:

1. Write a local helper (often `smaLike` / `barCloseOnly` / `leaf`).
2. Build an `IndicatorSpec` (`name`, `aliases`, `slots`, `compute`, `lookback`, `sub-commands`).
3. Register it in `Registry.buildTable`'s `all` list.

The parser already understands `name[.sub][:args][@series[,series…]]`; the
evaluator resolves `SeriesRef`s before `Compute` is called.

## Project layout

- `Contract.fs` — `DirectiveError`, `Candle`/`OhlcvFrame`, `Expr`/`IndicatorCall`/`SeriesRef`, `Series`, `IndicatorSpec`/`InputSlots`/`EvalContext`
- `Parser.fs` — `DirectiveParser.parse` (FParsec; see parser notes in `AGENTS.md`)
- `Registry.fs` — `Registry.buildTable` / `IndicatorRegistry` (FacioQuo bridge)
- `Evaluator.fs` — `Evaluator.eval` / `lookback` / `resolveSeriesRef`
- `Library.fs` — `Directive.eval` / `lookback` (`parse` then `eval`)
- `Tickframe.Benchmarks/` — BenchmarkDotNet harness (`Program.fs`)
- `tests/Tickframe.Tests/` — xUnit + 80-row synthetic fixture (`Shared.fs`)
- `docs/facioquo-v3-api.md` — verified FacioQuo v3 signatures for the registry
- `Tickframe.Benchmarks/`, `tests/Tickframe.Tests/` — non-library hosts that reference `Tickframe.fsproj`

Compile order (in `Tickframe.fsproj`): `Contract → Parser → Registry → Evaluator → Library`.

### Benchmarks

Benchmarks auto-generate `BENCHMARKS.md` (summary + detailed tables) plus
`BenchmarkDotNet.Artifacts/results/*.md/.csv/.html`:

```sh
dotnet run -c Release --project Tickframe.Benchmarks -- --all
# or: --filter="*ScaleBenchmarks*"  — also --no-write to skip BENCHMARKS.md
```

Groups: **Parse** (FParsec, N-independent), **Eval (pure, 80 rows)**,
**Lookback**, **Scale — indicators** at N=500/1000/5000/10000/20000, and
**Scale — pure ops**. Current config is `Job.Dry` (fast); use `Job.Default`
for publication-quality figures.

## Development

```sh
dotnet build
dotnet test
dotnet pack -c Release
dotnet run -c Release --project Tickframe.Benchmarks -- --all
dotnet run -c Release --project Tickframe.Benchmarks -- --filter="*ScaleBenchmarks*"
dotnet format   # if Fantomas is installed
```

Pre-PR gate — run exactly this before opening a PR (also enforced by CI):

```sh
dotnet tool restore
dotnet restore
dotnet build -c Release --no-restore -warnaserror
dotnet fantomas --check .
dotnet dotnet-fsharplint lint Tickframe.slnx
dotnet test -c Release --no-build
```

## License

MIT
