# FacioQuo v3 (3.0.0) — verified API reference for Tickframe Registry.fs

Verified by inspecting `FacioQuo.Stock.Indicators.xml` from the NuGet package
(2026-08-17). Use these signatures EXACTLY.

## Namespaces and types

- `open FacioQuo.Stock.Indicators` gives `IBar`, `Bar`, `Reusable`, `CandlePart`,
  `EndType`, `Direction`, `MaType`, `PivotPointType`, `BarInterval`, `BetaType`,
  result types, and all `ToXxx` methods.

## The two input families

### 1. Close-based indicators take `IReadOnlyList<IReusable>` (NOT IBar)

`Reusable` wraps a single float price plus a timestamp. Build it from the frame:

```fsharp
open FacioQuo.Stock.Indicators

// prices: float[] (one operand column, e.g. close)
let reusable (ts: DateTime[]) (prices: float[]) : IReadOnlyList<IReusable> =
    [| for i in 0 .. prices.Length - 1 ->
        Reusable(ts.[i], float prices.[i]) :> IReusable |]

// or from bars via CandlePart: Reusable.ToReusable(bars, CandlePart.Close) — but the
// direct constructor above is simpler when operands come from nested directives.
```

Close-based family (verified signatures):

```fsharp
Sma.ToSma(source: IReadOnlyList<IReusable>, period: int) : IReadOnlyList<SmaResult>
Ema.ToEma(source, period) : IReadOnlyList<EmaResult>            // EmaResult.Ema/.Value
Wma.ToWma(source, period)
Hma.ToHma(source, period)
Dema.ToDema(source, period)
Tema.ToTema(source, period)
Epma.ToEpma(source, period)
Alma.ToAlma(source, period: int, offset: double, sigma: double)
Kama.ToKama(source, erPeriod: int, fastPeriod: int, slowPeriod: int)
Mama.ToMama(source, fastLimit: double, slowLimit: double)
Smma.ToSmma(source, period)
SmaAnalysis.ToSmaAnalysis(source, period)
StdDev.ToStdDev(source, period)             // StdDevResult: StdDev, Mean, ZScore, Value
Slope.ToSlope(source, period)
FisherTransform.ToFisherTransform(source, period)
Rsi.ToRsi(source, period)
Cmo.ToCmo(source, period)
Trix.ToTrix(source, period)
Dpo.ToDpo(source, period)
Roc.ToRoc(source, period)
RocWb.ToRocWb(source, rocPeriod: int, emaPeriod: int, wbPeriod: int)
Stc.ToStc(source, fastPeriod: int, slowPeriod: int, kPeriod: int)
Tsi.ToTsi(source, longPeriod: int, shortPeriod: int, signalPeriod: int)
Pmo.ToPmo(source, timePeriod: int, smoothPeriod: int, signalPeriod: int)
ConnorsRsi.ToConnorsRsi(source, rsiPeriod: int, streakPeriod: int, rankPeriod: int)
Hurst.ToHurst(source, period)
UlcerIndex.ToUlcerIndex(source, period)
Macd.ToMacd(source, fastPeriod: int, slowPeriod: int, signalPeriod: int) : IReadOnlyList<MacdResult>
Mama.ToMama(source, fastLimit: double, slowLimit: double)        // MamaResult.Mama/.Fama
StochRsi.ToStochRsi(source, rsiPeriod: int, stochPeriod: int, signalPeriod: int, smoothPeriod: int)
Beta.ToBeta(source, market: IReadOnlyList<IReusable>, period: int, betaType: BetaType)
Correlation.ToCorrelation(source, compare: IReadOnlyList<IReusable>, period: int)
Prs.ToPrs(source, benchmark: IReadOnlyList<IReusable>)           // also ToPrs(..., period: int)
HtTrendline.ToHtTrendline(source)                                // HtTrendlineResult.HtTrendline?/.Value
MgDynamic.ToDynamic(source, period: int, k: double)
```

Result properties (close family): almost all results have a `Value` alias plus
named properties; missing/warm-up rows are `null` result objects OR `NaN` values —
always guard with `if r is null then nan else float r.Value`. Check the type-specific
property table below for each indicator.

### 2. OHLC-based indicators take `IReadOnlyList<IBar>`

`Bar` is a concrete class with ctor `Bar(timestamp, open, high, low, close, volume)`
(all decimal). Build from the frame with Candle.Timestamp + operand decimals.

```fsharp
let bar (c: Candle) (h: decimal) (l: decimal) (cl: decimal) (v: decimal) =
    Bar(c.Timestamp, c.Open, h, l, cl, v) :> IBar
```

Verified OHLC-based signatures:

```fsharp
Atr.ToAtr(bars: IReadOnlyList<IBar>, period: int)                     // AtrResult: Tr, Atr, Atrp, Value
AtrStop.ToAtrStop(bars, period: int, multiplier: double, endType: EndType)
Adx.ToAdx(bars, period: int)                                          // AdxResult: Pdi, Mdi, Dx, Adx, Adxr
Aroon.ToAroon(bars, period: int)                                      // AroonResult: AroonUp, AroonDown, Oscillator
Bop.ToBop(bars, period: int)
Cci.ToCci(bars, period: int)
ChaikinOsc.ToChaikinOsc(bars, fastPeriod: int, slowPeriod: int)
Chandelier.ToChandelier(bars, period: int, multiplier: double, direction: Direction)
Chop.ToChop(bars, period: int)
Cmf.ToCmf(bars, period: int)                                          // CmfResult: MoneyFlowMultiplier, MoneyFlowVolume, Cmf
Donchian.ToDonchian(bars, period: int)                                // DonchianResult: UpperBand, Centerline, LowerBand
ElderRay.ToElderRay(bars, period: int)                                // ElderRayResult: Ema, BullPower, BearPower
Fcb.ToFcb(bars, period: int)                                          // FcbResult: UpperBand, LowerBand
ForceIndex.ToForceIndex(bars, period: int)                            // ForceIndexResult: ForceIndex
Fractal.ToFractal(bars, left: int, right: int, endType: EndType)      // FractalResult: FractalBear, FractalBull (bool)
HeikinAshi.ToHeikinAshi(bars)                                         // HeikinAshiResult: Open/High/Low/Close (see table)
Ichimoku.ToIchimoku(bars, tenkan: int, kijun: int, senkouB: int)      // IchimokuResult: TenkanSen, KijunSen, SenkouSpanA, SenkouSpanB, ChikouSpan
Keltner.ToKeltner(bars, period: int, multiplier: double, maType: MaType) // KeltnerResult: UpperBand, Centerline, LowerBand
Kvo.ToKvo(bars, fastPeriod: int, slowPeriod: int, signalPeriod: int)  // KvoResult: Oscillator, Signal
Mfi.ToMfi(bars, period: int)                                          // MfiResult: Mfi
Obv.ToObv(bars)                                                       // ObvResult: Obv
ParabolicSar.ToParabolicSar(bars, step: double, maxStep: double)      // ParabolicSarResult: Sar, IsReversal
PivotPoints.ToPivotPoints(bars, interval: BarInterval, ptype: PivotPointType) // PivotPointsResult: PP S1..S4 R1..R4
Pivots.ToPivots(bars, left: int, right: int, maxTrend: int, endType: EndType) // PivotsResult: HighPoint, LowPoint (bool)
Pvo.ToPvo(bars, fastPeriod: int, slowPeriod: int, signalPeriod: int)  // PvoResult: Pvo, Signal, Histogram
RollingPivots.ToRollingPivots(bars, windowPeriod: int, offsetPeriod: int, ptype: PivotPointType)
Smi.ToSmi(bars, period: int, first: int, second: int, signal: int)    // SmiResult: Smi, Signal
StarcBands.ToStarcBands(bars, period: int, multiplier: double, atrPeriod: int) // StarcBandsResult: UpperBand, Centerline, LowerBand
Stoch.ToStoch(bars, kPeriod: int, kSlowingPeriod: int, dPeriod: int)  // StochResult: Oscillator(=K), Signal(=D), PercentJ(=J)
SuperTrend.ToSuperTrend(bars, period: int, multiplier: double)        // SuperTrendResult: SuperTrend, UpperBand, LowerBand
Tr.ToTr(bars)                                                         // TrResult: Tr
Ultimate.ToUltimate(bars, first: int, second: int, third: int)        // UltimateResult: Ultimate
VolatilityStop.ToVolatilityStop(bars, period: int, multiplier: double) // VolatilityStopResult: Sar, IsStop
Vortex.ToVortex(bars, period: int)                                    // VortexResult: Pvi, Nvi
Vwap.ToVwap(bars)                                                     // VwapResult: Vwap
Vwma.ToVwma(bars, period: int)                                        // VwmaResult: Vwma
WilliamsR.ToWilliamsR(bars, period: int)                              // WilliamsRResult: WilliamsR?/.Value
Doji.ToDoji(bars, maxChange: double)                                  // DojiResult (bool pattern)
Marubozu.ToMarubozu(bars, maxChange: double)                          // MarubozuResult (bool pattern)
Renko.ToRenko(bars, brickSize: decimal, endType: EndType)             // RenkoResult (see table)
ZigZag.ToZigZag(bars, endType: EndType, percent: decimal)             // ZigZagResult: ZigZag, PointType
```

## Pattern/pattern-like types are bool outputs

- `Doji.ToDoji(bars, maxChange)`, `Marubozu.ToMarubozu(bars, maxChange)` →
  `IReadOnlyList<DojiResult>` where result has `bool`-style members (check at compile
  time; the XML docs show the result type but members are empty in docs — pattern
  results have `IsDoji`/`IsMarubozu`-style properties in v3).
- `Pivots.ToPivots(...)` → `PivotsResult.HighPoint/LowPoint` are `bool?`.
- `Fractal.ToFractal(...)` → `FractalResult.FractalBear/FractalBull` are `bool?`.

## Indicator NOT present in FacioQuo v3

- `Kdj` — NO `ToKdj` method, no `KdjResult` in v3. Spec 05 lists `kdj` — either
  synthesize KDJ from `Stoch` (K = Stoch.Oscillator, D = Stoch.Signal, J = 3K - 2D)
  or leave it out with a `DirectiveValueError`. Document the decision in Registry.fs.
- `T3` — NO `ToT3` method and no `T3Result` in the XML docs. Leave it out or compute
  from EMA; document.

## Other notes

- `StdDevChannels.ToStdDevChannels(source, period, stdDevs)` returns
  `IReadOnlyList<StdDevChannelsResult>` with `Centerline/UpperChannel/LowerChannel`.
- `MaEnvelopes.ToMaEnvelopes(source, period, percent, maType)` → `MaEnvelopesResult`
  (has UpperBand/LowerBand/Centerline).
- `Alligator.ToAlligator(reusable, jawPeriod, jawOffset, teethPeriod, teethOffset, lipsPeriod, lipsOffset)`
  → `AlligatorResult.Jaw/Teeth/Lips`.
- `Gator.ToGator(reusable)` → `GatorResult.Upper/Lower` (expects Alligator input;
  may need `Alligator.ToAlligator(...)` first — check).
- `Awesome.ToAwesome(reusable, fastPeriod, slowPeriod)` → `AwesomeResult` (has
  `Awesome`/`Value` — verify at compile).
- `Renko.ToRenko(bars, brickSize: decimal, endType)` → `RenkoResult` with
  Open/High/Low/Close members (rename to renko.open/.high/.low/.close sub-commands;
  the result type is a bar-like).
- `HeikinAshi.ToHeikinAshi(bars)` → `HeikinAshiResult.Open/.High/.Low/.Close`.
- `BarParts.ToBarPart(bars, CandlePart)` → `IBarPart` (Open/High/Low/Close) for
  `bar-part.hl2/.hlc3/.oc2/.ohl3/.ohlc4`.
- `Beta.ToBeta(source, market, period, BetaType.Standard)` — needs a second series;
  treat as 2-operand (close + close) indicator.
- `Correlation.ToCorrelation(source, compare, period)` — 2-operand.
- `Prs.ToPrs(source, benchmark)` — 2-operand.
- `Vwap.ToVwap(bars)` — no args; `Vwap.ToVwap(bars, startDate)` for session-start.

## Unverifiable-at-a-glance members (verify at compile, fall back to `.Value`)

For result types whose named property is unclear, prefer the documented
`Value` alias when present. The pattern results (Doji/Marubozu/Pivots/Fractal) need a
compile-time check of their actual member names — write a tiny scratch test first if
unsure, or use reflection `typeof<DojiResult>.GetProperties()`.
