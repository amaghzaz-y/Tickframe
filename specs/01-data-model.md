# Spec 01 — Data Model

## Canonical candle

The lowest-level input is a `Candle`. Its numeric fields use `decimal` to match
FacioQuo's `Bar` type exactly and avoid floating-point round trips at the indicator
boundary.

```fsharp
namespace Tickframe

type Candle = {
    Timestamp: System.DateTime
    Open: decimal
    High: decimal
    Low: decimal
    Close: decimal
    Volume: decimal
}
```

## OHLCV frame

`OhlcvFrame` is an ordered sequence of candles with a columnar projection used for
directive evaluation.

```fsharp
type OhlcvFrame internal (candles: Candle[]) =
    member _.RowCount = candles.Length
    member _.Candles = candles
    member _.Column(name: string) : float[] = ...
```

Construction and helpers:

```fsharp
module OhlcvFrame =
    val ofCandles: seq<Candle> -> OhlcvFrame
    val tryColumn: OhlcvFrame -> string -> float[] option
```

### Canonical columns

Exactly five column names are supported. Lookup is case-insensitive and canonicalized
to lowercase:

| Name | Source |
| --- | --- |
| `open` | `candle.Open` |
| `high` | `candle.High` |
| `low` | `candle.Low` |
| `close` | `candle.Close` |
| `volume` | `candle.Volume` |

No other columns exist. A request for any other name is a
`DirectiveValueError`.

### Deedle role

`OhlcvFrame` may keep a Deedle `Frame<int, string>` projection internally to reuse
Deedle's column and elementwise machinery. The projection is an implementation
detail; the public contract is `Candle[]` + the five `float[]` column readers above.

The canonical numeric representation for evaluation is `float[]` (`double`).
Deedle column reads must therefore convert `decimal` to `double`. This is the only
place decimal-to-double conversion happens for raw OHLCV columns.

### Missing values

- Raw OHLCV columns are always complete; they have no missing values.
- Computed indicator columns use `System.Double.NaN` for warm-up rows (rows before the
  indicator has enough history).
- Boolean outputs are always total `bool`; there is no nullable/three-valued boolean in
  the runtime value model.

## FacioQuo bridge

To call a FacioQuo v3 batch indicator, the implementation synthesizes an
`IReadOnlyList<FacioQuo.Stock.Indicators.Bar>` from the current `OhlcvFrame`. The
`decimal` candle fields map 1:1 to `Bar.Open/High/Low/Close/Volume`, and
`Candle.Timestamp` maps to `Bar.Timestamp`.

This bridge exists only inside the indicator registry; the public model never exposes
FacioQuo result types.
