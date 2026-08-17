# Spec 05 — Indicator Registry

The registry is the only place FacioQuo v3 is called. It maps directive commands and
sub-commands to typed batch methods, resolves operand series, and converts FacioQuo
results to the runtime `Series` type.

## Core types

```fsharp
type IndicatorOutputKind =
    | FloatOutput
    | BoolOutput

type InputSlots =
    | CloseOnly
    | HighLow
    | HighLowClose
    | OpenHighLowClose
    | CloseVolume
    | HighLowCloseVolume
    | OpenHighLowCloseVolume

type IndicatorSpec = {
    Name:        string
    Aliases:     string list
    SubCommands: Map<string, IndicatorSpec>
    Slots:       InputSlots
    OutputKind:  IndicatorOutputKind
    Compute:     IndicatorCall -> EvalContext -> Series
    Lookback:    IndicatorCall -> int
}

and EvalContext = {
    Frame: OhlcvFrame
    Resolve: SeriesRef -> Series
}
```

- `Name` is the canonical lowercase directive name.
- `Aliases` are alternate lowercase names (e.g. `sma` for `ma`).
- `SubCommands` maps lowercase sub-command names to specs; empty for single-output
  indicators.
- `Compute` performs one batch indicator evaluation and returns a `Series`.
- `Lookback` returns the minimum warm-up row count for the call.

The registry is an extensible `Map<string, IndicatorSpec>` plus alias resolution.
Unknown command/sub-command/arg shapes raise `DirectiveValueError`.

## Uniform bridge strategy

The implementation keeps every `Compute` simple by using a shared helper:

1. Resolve each operand `SeriesRef` through `EvalContext.Resolve`.
2. Build a synthetic `FacioQuo.Stock.Indicators.Bar[]` of length `frame.RowCount`.
3. Map the resolved operands onto the FacioQuo `Bar` fields required by `Slots`.
4. Call the batch extension method.
5. Project the typed result into `Float` or `Bool`.

Mapping examples:

| `InputSlots` | Operand -> Bar field |
| --- | --- |
| `CloseOnly` | single operand -> `Bar.Close` |
| `HighLow` | operand 0 -> `High`, operand 1 -> `Low` |
| `HighLowClose` | `High`, `Low`, `Close` |
| `OpenHighLowClose` | `Open`, `High`, `Low`, `Close` |
| `CloseVolume` | `Close`, `Volume` |
| `HighLowCloseVolume` | `High`, `Low`, `Close`, `Volume` |
| `OpenHighLowCloseVolume` | `Open`, `High`, `Low`, `Close`, `Volume` |

The synthetic `Bar` still needs `Timestamp` and the non-operand fields because
FacioQuo's `Bar` is a concrete class. Use `DateTime.MinValue` for `Timestamp` and
`0m` for fields not consumed by the indicator unless a FacioQuo method is documented
to ignore them. Indicator methods only read their declared input fields, but the
spec should be conservative: when the input is a nested directive rather than a raw
column, map its float values to `decimal` at this boundary (round to decimal precision).

## Default operand series

If `call.Series` is empty, the spec resolves defaults from `Slots`:

| `InputSlots` | Default operands |
| --- | --- |
| `CloseOnly` | `close` |
| `HighLow` | `high`, `low` |
| `HighLowClose` | `high`, `low`, `close` |
| `OpenHighLowClose` | `open`, `high`, `low`, `close` |
| `CloseVolume` | `close`, `volume` |
| `HighLowCloseVolume` | `high`, `low`, `close`, `volume` |
| `OpenHighLowCloseVolume` | `open`, `high`, `low`, `close`, `volume` |

If `call.Series` is non-empty, its length must exactly match the slot count.

## Argument handling

- `call.Args` is a raw `string list`; empty slots are `""`.
- Each spec resolves positional args to the concrete FacioQuo call parameters.
- Empty slots use the FacioQuo-provided default where one exists.
- If FacioQuo has no default for a required slot, an empty slot is an error.
- The registry parses args as `int` or `float` as required and reports a
  `DirectiveValueError` on invalid or out-of-range values.

## Full mapping table

The table lists FacioQuo v3 batch methods grouped by FacioQuo category. Names are
lowercase directive names. `ma` is the canonical name for SMA; `sma`
is an alias. Method names below are the v3 series extension methods (e.g. `ToSma`).

> **Overload confirmation:** the method names and parameter arities below reflect the
> published FacioQuo v3 API. The implementation must confirm exact overload signatures
> against the installed `FacioQuo.Stock.Indicators` 3.0.0 assembly before finalizing
> each `Compute`; any signature difference that does not change the directive name or
> semantics is an implementation detail.

### Price trends

| Directive | FacioQuo | Slots | Notes |
| --- | --- | --- | --- |
| `alligator` | `ToAlligator(...)` | HighLow | |
| `aroon` | `ToAroon(period)` | HighLow | sub: `aroon.up`, `aroon.down` |
| `atr-stop` | `ToAtrStop(...)` | HighLowClose | |
| `adx` | `ToAdx(period)` | HighLowClose | |
| `dmi` | `ToAdx(period)` | HighLowClose | FacioQuo exposes DMI output through the ADX result set |
| `elder-ray` | `ToElderRay(period)` | HighLowClose | sub: `elder-ray.bull`, `elder-ray.bear` |
| `gator` | `ToGator(...)` | HighLow | |
| `hurst` | `ToHurst(period)` | CloseOnly | |
| `ichimoku` | `ToIchimoku(...)` | HighLowClose | sub: `ichimoku.tenkan`, `ichimoku.kijun`, `ichimoku.senkou-a`, `ichimoku.senkou-b`, `ichimoku.chikou` |
| `macd` | `ToMacd(...)` | CloseOnly | sub: `macd.signal`, `macd.histogram` |
| `super-trend` | `ToSuperTrend(period, multiplier)` | HighLowClose | sub: `super-trend.direction` |
| `vortex` | `ToVortex(period)` | HighLowClose | sub: `vortex.plus`, `vortex.minus` |

### Price channels

| Directive | FacioQuo | Slots | Notes |
| --- | --- | --- | --- |
| `boll` | `ToBollingerBands(...)` | CloseOnly | sub: `boll.upper`, `boll.middle`, `boll.lower` |
| `donchian` | `ToDonchian(period)` | HighLow | sub: `donchian.upper`, `donchian.middle`, `donchian.lower` |
| `fcb` | `ToFcb(period)` | HighLowClose | sub: `fcb.upper`, `fcb.lower` |
| `keltner` | `ToKeltner(...)` | HighLowClose | sub: `keltner.upper`, `keltner.middle`, `keltner.lower` |
| `ma-envelopes` | `ToMaEnvelopes(...)` | CloseOnly | sub: `ma-envelopes.upper`, `ma-envelopes.lower` |
| `pivot-points` | `ToPivotPoints(...)` | HighLowClose | sub: `pivot-points.pp`, `pivot-points.r1`, `pivot-points.s1`, etc. |
| `rolling-pivots` | `ToRollingPivots(...)` | HighLowClose | sub: `rolling-pivots.pp`, `rolling-pivots.r1`, `rolling-pivots.s1`, etc. |
| `starc-bands` | `ToStarcBands(...)` | HighLowClose | sub: `starc-bands.upper`, `starc-bands.middle`, `starc-bands.lower` |
| `std-dev-channels` | `ToStdDevChannels(...)` | CloseOnly | sub: `std-dev-channels.upper`, `std-dev-channels.middle`, `std-dev-channels.lower` |

### Oscillators

| Directive | FacioQuo | Slots | Notes |
| --- | --- | --- | --- |
| `awesome` | `ToAwesome(...)` | HighLow | |
| `cmo` | `ToCmo(period)` | CloseOnly | |
| `cci` | `ToCci(period)` | HighLowClose | |
| `connors-rsi` | `ToConnorsRsi(...)` | CloseOnly | |
| `dpo` | `ToDpo(period)` | CloseOnly | |
| `gator` | `ToGator(...)` | HighLow | |
| `kdj` | `ToKdj(...)` | HighLowClose | sub: `kdj.k`, `kdj.d`, `kdj.j` |
| `pmo` | `ToPmo(...)` | CloseOnly | sub: `pmo.signal` |
| `rsi` | `ToRsi(period)` | CloseOnly | |
| `stc` | `ToStc(...)` | CloseOnly | |
| `smi` | `ToSmi(...)` | HighLowClose | sub: `smi.signal` |
| `stoch` | `ToStoch(...)` | HighLowClose | sub: `stoch.k`, `stoch.d` |
| `stoch-rsi` | `ToStochRsi(...)` | CloseOnly | sub: `stoch-rsi.k`, `stoch-rsi.d` |
| `trix` | `ToTrix(period)` | CloseOnly | |
| `tsi` | `ToTsi(...)` | CloseOnly | sub: `tsi.signal` |
| `ultimate` | `ToUltimate(...)` | HighLowClose | |
| `williams-r` | `ToWilliamsR(period)` | HighLowClose | |

### Stop and reverse

| Directive | FacioQuo | Slots | Notes |
| --- | --- | --- | --- |
| `atr-stop` | `ToAtrStop(...)` | HighLowClose | |
| `chandelier` | `ToChandelier(...)` | HighLowClose | |
| `parabolic-sar` | `ToParabolicSar(...)` | HighLow | |
| `super-trend` | `ToSuperTrend(...)` | HighLowClose | sub: `super-trend.direction` |
| `volatility-stop` | `ToVolatilityStop(...)` | HighLowClose | |

### Candlestick patterns

| Directive | FacioQuo | Slots | Notes |
| --- | --- | --- | --- |
| `doji` | `ToDoji(...)` | OpenHighLowClose | Bool output |
| `marubozu` | `ToMarubozu(...)` | OpenHighLowClose | Bool output |
| `pivots` | `ToPivots(...)` | HighLowClose | Bool output |
| `fractal` | `ToFractal(...)` | HighLow | Bool output |

### Volume-based

| Directive | FacioQuo | Slots | Notes |
| --- | --- | --- | --- |
| `adl` | `ToAdl(...)` | HighLowCloseVolume | |
| `cmf` | `ToCmf(period)` | HighLowCloseVolume | |
| `chaikin-osc` | `ToChaikinOsc(...)` | HighLowCloseVolume | |
| `force-index` | `ToForceIndex(period)` | CloseVolume | |
| `kvo` | `ToKvo(...)` | HighLowCloseVolume | |
| `mfi` | `ToMfi(period)` | HighLowCloseVolume | |
| `obv` | `ToObv(...)` | CloseVolume | |
| `pvo` | `ToPvo(...)` | CloseVolume | sub: `pvo.signal` |
| `vwap` | `ToVwap(...)` | HighLowCloseVolume | |
| `vwma` | `ToVwma(period)` | CloseVolume | |

### Moving averages

| Directive | FacioQuo | Slots | Notes |
| --- | --- | --- | --- |
| `alma` | `ToAlma(...)` | CloseOnly | |
| `dema` | `ToDema(period)` | CloseOnly | |
| `epma` | `ToEpma(period)` | CloseOnly | |
| `ema` | `ToEma(period)` | CloseOnly | |
| `ht-trendline` | `ToHtTrendline(...)` | CloseOnly | |
| `hma` | `ToHma(period)` | CloseOnly | |
| `kama` | `ToKama(...)` | CloseOnly | |
| `lsma` | `ToEpma(period)` | CloseOnly | alias of `epma` |
| `dynamic` | `ToDynamic(period)` | CloseOnly | McGinley Dynamic |
| `mama` | `ToMama(...)` | CloseOnly | sub: `mama.fama` |
| `mma` | `ToSmma(period)` | CloseOnly | alias of modified MA |
| `rma` | `ToSmma(period)` | CloseOnly | alias of running MA |
| `sma` | `ToSma(period)` | CloseOnly | alias of `ma` |
| `ma` | `ToSma(period)` | CloseOnly | canonical name for SMA |
| `smma` | `ToSmma(period)` | CloseOnly | |
| `t3` | `ToT3(...)` | CloseOnly | |
| `tema` | `ToTema(period)` | CloseOnly | |
| `vwap` | `ToVwap(...)` | HighLowCloseVolume | |
| `vwma` | `ToVwma(period)` | CloseVolume | |
| `wma` | `ToWma(period)` | CloseOnly | |

### Price transforms

| Directive | FacioQuo | Slots | Notes |
| --- | --- | --- | --- |
| `bar-part` | `ToBarPart(...)` | HighLowClose | sub: `bar-part.hl2`, `bar-part.hlc3`, `bar-part.oc2`, `bar-part.ohl3`, `bar-part.ohlc4` |
| `fisher-transform` | `ToFisherTransform(period)` | CloseOnly | |
| `heikin-ashi` | `ToHeikinAshi(...)` | OpenHighLowClose | sub: `heikin-ashi.open`, `heikin-ashi.high`, `heikin-ashi.low`, `heikin-ashi.close` |
| `renko` | `ToRenko(...)` | CloseOnly | |
| `zig-zag` | `ToZigZag(...)` | HighLowClose | |

### Price characteristics

| Directive | FacioQuo | Slots | Notes |
| --- | --- | --- | --- |
| `atr` | `ToAtr(period)` | HighLowClose | |
| `bop` | `ToBop(...)` | OpenHighLowClose | |
| `elder-ray` | `ToElderRay(period)` | HighLowClose | sub: `elder-ray.bull`, `elder-ray.bear` |
| `chop` | `ToChop(period)` | HighLowClose | |
| `pmo` | `ToPmo(...)` | CloseOnly | sub: `pmo.signal` |
| `ht-trendline` | `ToHtTrendline(...)` | CloseOnly | dominant cycle period |
| `std-dev` | `ToStdDev(...)` | CloseOnly | historical volatility |
| `hurst` | `ToHurst(period)` | CloseOnly | |
| `roc` | `ToRoc(period)` | CloseOnly | momentum oscillator |
| `atr` | `ToAtr(period)` | HighLowClose | normalized ATR via `natr` alias |
| `prs` | `ToPrs(...)` | CloseOnly | price relative strength |
| `roc-wb` | `ToRocWb(...)` | CloseOnly | |
| `tr` | `ToTr(...)` | HighLowClose | |
| `tsi` | `ToTsi(...)` | CloseOnly | |
| `ulcer-index` | `ToUlcerIndex(period)` | CloseOnly | |

### Numerical analysis

| Directive | FacioQuo | Slots | Notes |
| --- | --- | --- | --- |
| `beta` | `ToBeta(...)` | CloseOnly | requires a comparison series |
| `correlation` | `ToCorrelation(...)` | CloseOnly | |
| `slope` | `ToSlope(period)` | CloseOnly | |
| `sma-analysis` | `ToSmaAnalysis(period)` | CloseOnly | MAD / MAPE / MSE |
| `std-dev` | `ToStdDev(...)` | CloseOnly | standard deviation / z-score |

## Naming rules

- Directive command names are lowercased before registry lookup.
- Hyphens are used in directive names where the FacioQuo concept is multiword
  (`super-trend`, `force-index`, `pivot-points`, `stoch-rsi`).
- Common short names are preferred where they exist (`ma`, `ema`, `rsi`, `macd`,
  `boll`, `atr`, `adx`, `stoch`, `cci`, `mfi`, `obv`, `donchian`, `keltner`).
- FacioQuo-native names are included for indicators that do not have a
  stable single directive (Alligator, Elder-ray, Hurst, STARC, PMO, KVO, VWAP,
  Heikin-Ashi, etc.).

## Extensibility

Adding an indicator requires only:

1. A new `IndicatorSpec` record.
2. A `Compute` implementation using the uniform bridge.
3. A `Lookback` implementation.
4. Registration in the `Map`.

No parser or evaluator changes are needed unless the indicator introduces a new
operator or operand shape, which is out of scope for v1.
