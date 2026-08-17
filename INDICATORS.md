# INDICATORS

> Native F# kernels. No FacioQuo, no `decimal`/`IBar`/`Nullable`. `float[]` warm-up is `NaN`, `bool[]` is `false`.

## Supported (native) — 35 (incl. synthetics)

| # | Indicator | Aliases | Category | Slots | Output | Args (default) | Sub-commands | Lookback | Notes |
|---|-----------|---------|----------|-------|--------|----------------|--------------|----------|-------|
| 1 | `ma` | `sma` | Trend | CloseOnly | Float | `period=20` | — | `p-1` | Rolling SMA |
| 2 | `ema` | — | Trend | CloseOnly | Float | `period=20` | — | `p-1` | Seeded with SMA |
| 3 | `wma` | — | Trend | CloseOnly | Float | `period=20` | — | `p-1` | Linear weighted |
| 4 | `smma` | `mma`, `rma` | Trend | CloseOnly | Float | `period=20` | — | `p-1` | Wilder RMA |
| 5 | `hma` | — | Trend | CloseOnly | Float | `period=14` | — | `p-1` | Hull MA |
| 6 | `rsi` | — | Momentum | CloseOnly | Float | `period=14` | — | `p` | Wilder RSI |
| 7 | `macd` | — | Momentum | CloseOnly | Float | `fast=12, slow=26, signal=9` | `signal`, `histogram` | `slow+signal-1` | EMA diff + signal |
| 8 | `stoch` | — | Momentum | HighLowClose | Float | `lb=14, sig=3, smooth=3` | `k`, `d` | `lb-1` | %K / %D (SMA) |
| 9 | `kdj` | — | Momentum | HighLowClose | Float | `lb=14, sig=3, smooth=3` | `k`, `d`, `j` | `lb-1` | Stoch + J=3K-2D |
| 10 | `cci` | — | Momentum | HighLowClose | Float | `period=20` | — | `p-1` | Commodity Channel |
| 11 | `roc` | — | Momentum | CloseOnly | Float | `period=14` | — | `p` | 100*(cur-prev)/prev |
| 12 | `stoch-rsi` | `stochrsi` | Momentum | CloseOnly | Float | `rsi=14, stoch=14, sig=3, smooth=1` | `k`, `d` | `rsi+stoch` | RSI then stoch K |
| 13 | `boll` | — | Volatility | CloseOnly | Float | `lb=20, sd=2.0` | `upper`, `lower`, `middle` | `lb-1` | SMA ± sd*std |
| 14 | `atr` | — | Volatility | HighLowClose | Float | `period=14` | — | `p` | Wilder RMA of TR |
| 15 | `tr` | — | Volatility | HighLowClose | Float | — | — | `0` | True Range |
| 16 | `natr` | — | Volatility | HighLowClose | Float | `period=14` | — | `p` | 100*ATR/close |
| 17 | `donchian` | — | Volatility | HighLow | Float | `period=20` | `upper`, `lower`, `middle` | `p-1` | HH/LL channel |
| 18 | `keltner` | — | Volatility | HighLowClose | Float | `ema=20, mult=2, atr=10` | `upper`, `lower`, `middle` | `ema-1` | EMA ± mult*ATR |
| 19 | `std-dev` | `stddev` | Volatility | CloseOnly | Float | `period=14` | `zscore`, `mean` | `p-1` | Pop std, zscore |
| 20 | `obv` | — | Volume | CloseVolume | Float | — | — | `0` | Cumulative |
| 21 | `adl` | — | Volume | HighLowCloseVolume | Float | — | — | `0` | Acc/Dist line |
| 22 | `cmf` | — | Volume | HighLowCloseVolume | Float | `period=20` | — | `p-1` | ΣMFV/ΣVol |
| 23 | `mfi` | — | Volume | HighLowCloseVolume | Float | `period=14` | — | `p` | Money Flow Index |
| 24 | `vwap` | — | Volume | HighLowCloseVolume | Float | — | — | `0` | Cumulative PV/Vol |
| 25 | `vwma` | — | Volume | CloseVolume | Float | `period=20` | — | `p-1` | Volume-weighted MA |
| 26 | `adx` | `dmi` | Directional | HighLowClose | Float | `period=14` | — | `2*p-1` | Wilder ADX |
| 27 | `aroon` | — | Directional | HighLow | Float | `period=25` | `up`, `down` | `p-1` | Bars since HH/LL |
| 28 | `super-trend` | `supertrend` | Directional | HighLowClose | Float+Bool | `lb=10, mult=3` | `direction` (Bool) | `lb` | HL2 ± mult*ATR |
| 29 | `parabolic-sar` | `parabolicsar` | Directional | HighLow | Float | `step=0.02, max=0.2` | — | `0` | SAR flip |
| 30 | `ichimoku` | — | Directional | HighLowClose | Float | `ten=9, kij=26, sen=52` | `tenkan`, `kijun`, `senkou-a`, `senkou-b`, `chikou` | `52` | Tenkan/Kijun/Senkou |
| 31 | `heikin-ashi` | `heikinashi` | Price | OpenHighLowClose | Float | — | `open`, `high`, `low`, `close` | `0` | HA candles |
| 32 | `bar-part` | `barpart` | Price | HighLowClose | Float | `part=hl2|hlc3|ohlc4` | — | `0` | Price transform |
| 33 | `doji` | — | Price | OpenHighLowClose | Bool | `pct=0.1` | — | `0` | Body/range ≤ pct |
| 34 | `increase` | — | Synthetic | CloseOnly | Bool | `period=3` (required) | — | `p` | Strictly rising N bars |
| 35 | `repeat` | — | Synthetic | (Bool) | Bool | `period=3` (required) | — | `p-1` | N consecutive true |

- `Float` warm-up rows are `Double.NaN`, `Bool` is `false`. `lookback` is the first non-NaN index.
- Operand override via `@`: `ma:10@open`, `ma:10@(ma:5)`, `correlation:20@close,open` etc. Defaults per `InputSlots`.
- Multi-output indicators use sub-commands: `boll.upper`, `macd.signal`, `macd.histogram`, `stoch.k/.d`, `kdj.j`, `donchian.upper`, `keltner.lower`, `std-dev.zscore`, `aroon.up`, `super-trend.direction`, `ichimoku.tenkan`, `heikin-ashi.close`, etc.

## Planned (to add later)

> Candidates for native porting after the FacioQuo removal. Ordered by demand.

| Indicator | Aliases | Category | Slots | Notes |
|-----------|---------|----------|-------|-------|
| `dema` | — | Trend | CloseOnly | Double EMA |
| `tema` | — | Trend | CloseOnly | Triple EMA |
| `epma` | `lsma` | Trend | CloseOnly | Endpoint MA |
| `alma` | — | Trend | CloseOnly | Arnaud Legoux (`offset`, `sigma`) |
| `kama` | — | Trend | CloseOnly | Kaufman (`er`, `fast`, `slow`) |
| `mama` | — | Trend | CloseOnly | MESA (`fast`, `slow`, `.fama`) |
| `t3` | — | Trend | CloseOnly | Tillson T3 |
| `dynamic` | — | Trend | CloseOnly | McGinley Dynamic |
| `cmo` | — | Momentum | CloseOnly | Chande MO |
| `trix` | — | Momentum | CloseOnly | TRIX |
| `roc-wb` | `rocwb` | Momentum | CloseOnly | ROC with Bands |
| `stc` | — | Momentum | CloseOnly | Schaff Trend Cycle |
| `pmo` | — | Momentum | CloseOnly | Price Momentum (`.signal`) |
| `tsi` | — | Momentum | CloseOnly | True Strength (`.signal`) |
| `connors-rsi` | `connorsrsi` | Momentum | CloseOnly | Connors RSI |
| `hurst` | — | Momentum | CloseOnly | Hurst exponent |
| `ulcer-index` | `ulcerindex` | Momentum | CloseOnly | Ulcer Index |
| `fisher-transform` | `fishertransform` | Momentum | CloseOnly | Fisher |
| `slope` | — | Momentum | CloseOnly | Linear slope |
| `ht-trendline` | `httrendline` | Momentum | CloseOnly | Hilbert |
| `dpo` | — | Momentum | CloseOnly | Detrended Price |
| `bop` | — | Momentum | OpenHighLowClose | Balance of Power |
| `ultimate` | — | Momentum | HighLowClose | Ultimate Oscillator |
| `williams-r` | `williamsr` | Momentum | HighLowClose | Williams %R |
| `smi` | — | Momentum | HighLowClose | Stochastic Momentum (`.signal`) |
| `chop` | — | Momentum | HighLowClose | Choppiness |
| `starc-bands` | `starcbands` | Volatility | HighLowClose | STARC (`upper`/`lower`/`middle`) |
| `fcb` | — | Volatility | HighLowClose | Fractal Chaos Bands |
| `ma-envelopes` | `maenvelopes` | Volatility | CloseOnly | MA Envelopes |
| `std-dev-channels` | `stddevchannels` | Volatility | CloseOnly | StdDev Channels |
| `alligator` | — | Volatility | CloseOnly | Bill Williams |
| `gator` | — | Volatility | CloseOnly | Gator Oscillator |
| `awesome` | — | Momentum | CloseOnly | Awesome Oscillator |
| `vortex` | — | Directional | HighLowClose | Vortex (`.plus`/`.minus`) |
| `elder-ray` | `elderray` | Directional | HighLowClose | Bull/Bear Power |
| `atr-stop` | `atrstop` | Directional | HighLowClose | ATR Stop |
| `chandelier` | — | Directional | HighLowClose | Chandelier Exit |
| `volatility-stop` | `volatilitystop` | Directional | HighLowClose | Volatility Stop |
| `fractal` | — | Pattern | HighLow | Fractal (`Bool`) |
| `pivots` | — | Pattern | HighLowClose | Pivot High/Low (`Bool`) |
| `doji` (extended) | — | Pattern | OpenHighLowClose | `Bool` (threshold `pct`) |
| `marubozu` | — | Pattern | OpenHighLowClose | `Bool` |
| `adl` (legacy) | — | Volume | HighLowCloseVolume | Already native |
| `chaikin-osc` | `chaikinosc` | Volume | HighLowCloseVolume | Chaikin Oscillator |
| `force-index` | `forceindex` | Volume | CloseVolume | Force Index |
| `kvo` | — | Volume | HighLowCloseVolume | Klinger (`.signal`) |
| `pvo` | — | Volume | CloseVolume | Price-Volume (`.signal`) |
| `obv` (legacy) | — | Volume | CloseVolume | Already native |
| `beta` | — | Other | CloseOnly | Beta vs market series |
| `correlation` | — | Other | CloseOnly | Correlation (`@s1,s2`) |
| `prs` | — | Other | CloseOnly | Price RS |
| `pivot-points` | `pivotpoints` | Other | HighLowClose | PP/R1/S1/R2/S2 |
| `rolling-pivots` | `rollingpivots` | Other | HighLowClose | Rolling pivots |
| `renko` | — | Price | HighLowCloseVolume | Renko bricks |
| `zig-zag` | `zigzag` | Price | HighLowClose | ZigZag |
| `pivot-points` | `pivotpoints` | Price | HighLowClose | PP |

## Adding a new indicator

1. Implement a pure kernel in `src/Indicators/<Category>.fs` (`float[] -> float[]` or `bool[]`, `NaN`/`false` warm-up).
2. Wire an `IndicatorSpec` in `src/Infrastructure.fs` via `mk`/`leaf` using `RegistryHelpers.resolveOperands` / `parseInt` / `parseFloat` / `arg`.
3. Add it to `Registry.buildTable`'s `all` list (aliases expand via `List.collect`).
4. Add a row to the Supported table above and update `README.md`.
5. Add a test in `tests/EvaluatorTests.fs` (warm-up `NaN`/`false`, `lookback`, `@` override, sub-command).

## Categories

- **Trend** (5): `ma`, `ema`, `wma`, `smma`, `hma`
- **Momentum** (7): `rsi`, `macd`, `stoch`/`kdj`, `cci`, `roc`, `stoch-rsi`
- **Volatility** (6): `boll`, `atr`/`tr`/`natr`, `donchian`, `keltner`, `std-dev`
- **Volume** (6): `obv`, `adl`, `cmf`, `mfi`, `vwap`, `vwma`
- **Directional** (5): `adx`, `aroon`, `super-trend`, `parabolic-sar`, `ichimoku`
- **Price / Pattern** (4): `heikin-ashi`, `bar-part`, `doji`, plus synthetics `increase`/`repeat`
