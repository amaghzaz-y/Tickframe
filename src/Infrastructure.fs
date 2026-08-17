namespace Tickframe

open System
open Tickframe.Indicators

module internal RegistryHelpers =

    let resolveFloatSeries (ctx: EvalContext) (ref: SeriesRef) : float[] =
        match ctx.Resolve ref with
        | Float a -> a
        | Bool _ -> raise (DirectiveValueError "indicator requires a float series operand")

    let resolveOperands (ctx: EvalContext) (call: IndicatorCall) (slots: InputSlots) : float[][] =
        let defaults =
            match slots with
            | CloseOnly -> [ "close" ]
            | HighLow -> [ "high"; "low" ]
            | HighLowClose -> [ "high"; "low"; "close" ]
            | OpenHighLowClose -> [ "open"; "high"; "low"; "close" ]
            | CloseVolume -> [ "close"; "volume" ]
            | HighLowCloseVolume -> [ "high"; "low"; "close"; "volume" ]
            | OpenHighLowCloseVolume -> [ "open"; "high"; "low"; "close"; "volume" ]

        let refs =
            if call.Series.IsEmpty then
                defaults |> List.map SeriesColumn
            else
                call.Series

        let expected = defaults.Length

        if refs.Length <> expected then
            raise (
                DirectiveValueError $"indicator '{call.Name}' expects {expected} series operand(s), got {refs.Length}"
            )

        refs |> List.map (fun r -> resolveFloatSeries ctx r) |> List.toArray

    let arg (call: IndicatorCall) (idx: int) : string =
        if idx < call.Args.Length then call.Args.[idx] else ""

    let parseInt (call: IndicatorCall) (idx: int) (defaultValue: int) : int =
        let s = arg call idx

        if s = "" then
            defaultValue
        else
            match Int32.TryParse(s) with
            | true, v -> v
            | _ -> raise (DirectiveValueError $"argument '{s}' is not an integer")

    let parseFloat (call: IndicatorCall) (idx: int) (defaultValue: float) : float =
        let s = arg call idx

        if s = "" then
            defaultValue
        else
            match
                Double.TryParse(
                    s,
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture
                )
            with
            | true, v -> v
            | _ -> raise (DirectiveValueError $"argument '{s}' is not a number")

    let requireArg (call: IndicatorCall) (idx: int) (name: string) : string =
        let s = arg call idx

        if s = "" then
            raise (
                DirectiveValueError
                    $"indicator '{call.Name}' requires argument {idx + 1} ({name}); empty slots are not allowed"
            )

        s

module Registry =

    open RegistryHelpers

    let private mk
        (name: string)
        (aliases: string list)
        (slots: InputSlots)
        (outputKind: IndicatorOutputKind)
        (compute: IndicatorCall -> EvalContext -> Series)
        (lookback: IndicatorCall -> int)
        (subs: Map<string, IndicatorSpec>)
        : IndicatorSpec =
        { Name = name
          Aliases = aliases
          SubCommands = subs
          Slots = slots
          OutputKind = outputKind
          Compute = compute
          Lookback = lookback }

    let private leaf
        (name: string)
        (slots: InputSlots)
        (compute: IndicatorCall -> EvalContext -> Series)
        (lookback: IndicatorCall -> int)
        : IndicatorSpec =
        { Name = name
          Aliases = []
          SubCommands = Map.empty
          Slots = slots
          OutputKind = FloatOutput
          Compute = compute
          Lookback = lookback }

    let buildTable () : Map<string, IndicatorSpec> =

        let smaLike
            (name: string)
            (aliases: string list)
            (kernel: int -> float[] -> float[])
            (defaultPeriod: int)
            : IndicatorSpec =
            mk
                name
                aliases
                CloseOnly
                FloatOutput
                (fun call ctx ->
                    let period = parseInt call 0 defaultPeriod
                    let src = (resolveOperands ctx call CloseOnly).[0]
                    Float(kernel period src))
                (fun call -> parseInt call 0 defaultPeriod - 1)
                Map.empty

        let smaSpec = smaLike "ma" [ "sma" ] Common.sma 20

        let emaSpec = smaLike "ema" [] Common.ema 20

        let wmaSpec = smaLike "wma" [] Common.wma 20

        let smmaSpec = smaLike "smma" [ "mma"; "rma" ] Common.rma 20

        let hmaSpec =
            mk
                "hma"
                []
                CloseOnly
                FloatOutput
                (fun call ctx ->
                    let period = parseInt call 0 14
                    let src = (resolveOperands ctx call CloseOnly).[0]
                    Float(Trend.hma period src))
                (fun call -> parseInt call 0 14 - 1)
                Map.empty

        let rsiSpec =
            mk
                "rsi"
                []
                CloseOnly
                FloatOutput
                (fun call ctx ->
                    let period = parseInt call 0 14
                    let src = (resolveOperands ctx call CloseOnly).[0]
                    Float(Momentum.rsi period src))
                (fun call -> parseInt call 0 14)
                Map.empty

        let macdSpec =
            mk
                "macd"
                []
                CloseOnly
                FloatOutput
                (fun call ctx ->
                    let fast = parseInt call 0 12
                    let slow = parseInt call 1 26
                    let sig_ = parseInt call 2 9
                    let src = (resolveOperands ctx call CloseOnly).[0]
                    let line = Momentum.macdLine fast slow src
                    let signal = Momentum.macdSignal sig_ line
                    let hist = Momentum.macdHistogram line signal

                    match call.Sub with
                    | Some "signal" -> Float signal
                    | Some "histogram" -> Float hist
                    | _ -> Float line)
                (fun call ->
                    let slow = parseInt call 1 26
                    let sig_ = parseInt call 2 9
                    slow - 1 + sig_ - 1)
                (Map.ofList
                    [ "signal", leaf "signal" CloseOnly (fun _ _ -> failwith "sub") (fun _ -> 0)
                      "histogram", leaf "histogram" CloseOnly (fun _ _ -> failwith "sub") (fun _ -> 0) ])

        let stochSpec =
            mk
                "stoch"
                []
                HighLowClose
                FloatOutput
                (fun call ctx ->
                    let lb = parseInt call 0 14
                    let sig_ = parseInt call 1 3
                    let sm = parseInt call 2 3
                    let ops = resolveOperands ctx call HighLowClose
                    let high = ops.[0]
                    let low = ops.[1]
                    let close = ops.[2]
                    let k = Momentum.stochK lb high low close
                    let d = Momentum.stochD sm k

                    match call.Sub with
                    | Some "k" -> Float k
                    | Some "d" -> Float d
                    | _ -> Float k)
                (fun call -> parseInt call 0 14 - 1)
                (Map.ofList
                    [ "k", leaf "k" HighLowClose (fun _ _ -> failwith "") (fun _ -> 0)
                      "d", leaf "d" HighLowClose (fun _ _ -> failwith "") (fun _ -> 0) ])

        let kdjSpec =
            mk
                "kdj"
                []
                HighLowClose
                FloatOutput
                (fun call ctx ->
                    let lb = parseInt call 0 14
                    let sig_ = parseInt call 1 3
                    let sm = parseInt call 2 3
                    let ops = resolveOperands ctx call HighLowClose
                    let k = Momentum.stochK lb ops.[0] ops.[1] ops.[2]
                    let d = Momentum.stochD sm k
                    let n = k.Length
                    let j = Array.create n Double.NaN

                    for i in 0 .. n - 1 do
                        let kv = k.[i]
                        let dv = d.[i]

                        if not (Double.IsNaN kv || Double.IsNaN dv) then
                            j.[i] <- 3.0 * kv - 2.0 * dv

                    match call.Sub with
                    | Some "k" -> Float k
                    | Some "d" -> Float d
                    | Some "j" -> Float j
                    | _ -> Float k)
                (fun call -> parseInt call 0 14 - 1)
                (Map.ofList
                    [ "k", leaf "k" HighLowClose (fun _ _ -> failwith "") (fun _ -> 0)
                      "d", leaf "d" HighLowClose (fun _ _ -> failwith "") (fun _ -> 0)
                      "j", leaf "j" HighLowClose (fun _ _ -> failwith "") (fun _ -> 0) ])

        let cciSpec =
            mk
                "cci"
                []
                HighLowClose
                FloatOutput
                (fun call ctx ->
                    let period = parseInt call 0 20
                    let ops = resolveOperands ctx call HighLowClose
                    Float(Momentum.cci period ops.[0] ops.[1] ops.[2]))
                (fun call -> parseInt call 0 20 - 1)
                Map.empty

        let rocSpec =
            mk
                "roc"
                []
                CloseOnly
                FloatOutput
                (fun call ctx ->
                    let period = parseInt call 0 14
                    let src = (resolveOperands ctx call CloseOnly).[0]
                    Float(Momentum.roc period src))
                (fun call -> parseInt call 0 14)
                Map.empty

        let stochRsiSpec =
            mk
                "stoch-rsi"
                [ "stochrsi" ]
                CloseOnly
                FloatOutput
                (fun call ctx ->
                    let rsiP = parseInt call 0 14
                    let stochP = parseInt call 1 14
                    let sig_ = parseInt call 2 3
                    let sm = parseInt call 3 1
                    let src = (resolveOperands ctx call CloseOnly).[0]
                    let k = Momentum.stochRsiK rsiP stochP src
                    let d = Common.sma sm k

                    match call.Sub with
                    | Some "k" -> Float k
                    | Some "d" -> Float d
                    | _ -> Float k)
                (fun call -> parseInt call 0 14 + parseInt call 1 14)
                (Map.ofList
                    [ "k", leaf "k" CloseOnly (fun _ _ -> failwith "") (fun _ -> 0)
                      "d", leaf "d" CloseOnly (fun _ _ -> failwith "") (fun _ -> 0) ])

        let bollSpec =
            mk
                "boll"
                []
                CloseOnly
                FloatOutput
                (fun call ctx ->
                    let lb = parseInt call 0 20
                    let sd = parseFloat call 1 2.0
                    let src = (resolveOperands ctx call CloseOnly).[0]
                    let upper, lower, middle = Volatility.bollinger lb sd src

                    match call.Sub with
                    | Some "upper" -> Float upper
                    | Some "lower" -> Float lower
                    | Some "middle" -> Float middle
                    | _ -> Float middle)
                (fun call -> parseInt call 0 20 - 1)
                (Map.ofList
                    [ "upper", leaf "upper" CloseOnly (fun _ _ -> failwith "") (fun _ -> 0)
                      "lower", leaf "lower" CloseOnly (fun _ _ -> failwith "") (fun _ -> 0)
                      "middle", leaf "middle" CloseOnly (fun _ _ -> failwith "") (fun _ -> 0) ])

        let atrSpec =
            mk
                "atr"
                []
                HighLowClose
                FloatOutput
                (fun call ctx ->
                    let p = parseInt call 0 14
                    let ops = resolveOperands ctx call HighLowClose
                    Float(Volatility.atr p ops.[0] ops.[1] ops.[2]))
                (fun call -> parseInt call 0 14)
                Map.empty

        let trSpec =
            mk
                "tr"
                []
                HighLowClose
                FloatOutput
                (fun call ctx ->
                    let ops = resolveOperands ctx call HighLowClose
                    Float(Common.trueRange ops.[0] ops.[1] ops.[2]))
                (fun _ -> 0)
                Map.empty

        let natrSpec =
            mk
                "natr"
                []
                HighLowClose
                FloatOutput
                (fun call ctx ->
                    let p = parseInt call 0 14
                    let ops = resolveOperands ctx call HighLowClose
                    let atrVals = Volatility.atr p ops.[0] ops.[1] ops.[2]
                    let n = atrVals.Length
                    let out = Array.create n Double.NaN

                    for i in 0 .. n - 1 do
                        let a = atrVals.[i]
                        let c = ops.[2].[i]

                        if not (Double.IsNaN a || Double.IsNaN c) && c <> 0.0 then
                            out.[i] <- 100.0 * a / c

                    Float out)
                (fun call -> parseInt call 0 14)
                Map.empty

        let donchianSpec =
            mk
                "donchian"
                []
                HighLow
                FloatOutput
                (fun call ctx ->
                    let p = parseInt call 0 20
                    let ops = resolveOperands ctx call HighLow
                    let upper, lower, middle = Volatility.donchian p ops.[0] ops.[1]

                    match call.Sub with
                    | Some "upper" -> Float upper
                    | Some "lower" -> Float lower
                    | Some "middle" -> Float middle
                    | _ -> Float middle)
                (fun call -> parseInt call 0 20 - 1)
                (Map.ofList
                    [ "upper", leaf "upper" HighLow (fun _ _ -> failwith "") (fun _ -> 0)
                      "lower", leaf "lower" HighLow (fun _ _ -> failwith "") (fun _ -> 0)
                      "middle", leaf "middle" HighLow (fun _ _ -> failwith "") (fun _ -> 0) ])

        let keltnerSpec =
            mk
                "keltner"
                []
                HighLowClose
                FloatOutput
                (fun call ctx ->
                    let emaP = parseInt call 0 20
                    let mult = parseFloat call 1 2.0
                    let atrP = parseInt call 2 10
                    let ops = resolveOperands ctx call HighLowClose
                    let upper, lower, middle = Volatility.keltner emaP mult atrP ops.[0] ops.[1] ops.[2]

                    match call.Sub with
                    | Some "upper" -> Float upper
                    | Some "lower" -> Float lower
                    | Some "middle" -> Float middle
                    | _ -> Float middle)
                (fun call -> parseInt call 0 20 - 1)
                (Map.ofList
                    [ "upper", leaf "upper" HighLowClose (fun _ _ -> failwith "") (fun _ -> 0)
                      "lower", leaf "lower" HighLowClose (fun _ _ -> failwith "") (fun _ -> 0)
                      "middle", leaf "middle" HighLowClose (fun _ _ -> failwith "") (fun _ -> 0) ])

        let stdDevSpec =
            mk
                "std-dev"
                [ "stddev" ]
                CloseOnly
                FloatOutput
                (fun call ctx ->
                    let p = parseInt call 0 14
                    let src = (resolveOperands ctx call CloseOnly).[0]
                    let std, zscore, mean = Volatility.stdDevWithBands p src

                    match call.Sub with
                    | Some "zscore" -> Float zscore
                    | Some "mean" -> Float mean
                    | _ -> Float std)
                (fun call -> parseInt call 0 14 - 1)
                (Map.ofList
                    [ "zscore", leaf "zscore" CloseOnly (fun _ _ -> failwith "") (fun _ -> 0)
                      "mean", leaf "mean" CloseOnly (fun _ _ -> failwith "") (fun _ -> 0) ])

        let obvSpec =
            mk
                "obv"
                []
                CloseVolume
                FloatOutput
                (fun call ctx ->
                    let ops = resolveOperands ctx call CloseVolume
                    Float(Volume.obv ops.[0] ops.[1]))
                (fun _ -> 0)
                Map.empty

        let adlSpec =
            mk
                "adl"
                []
                HighLowCloseVolume
                FloatOutput
                (fun call ctx ->
                    let ops = resolveOperands ctx call HighLowCloseVolume
                    Float(Volume.adl ops.[0] ops.[1] ops.[2] ops.[3]))
                (fun _ -> 0)
                Map.empty

        let cmfSpec =
            mk
                "cmf"
                []
                HighLowCloseVolume
                FloatOutput
                (fun call ctx ->
                    let p = parseInt call 0 20
                    let ops = resolveOperands ctx call HighLowCloseVolume
                    Float(Volume.cmf p ops.[0] ops.[1] ops.[2] ops.[3]))
                (fun call -> parseInt call 0 20 - 1)
                Map.empty

        let mfiSpec =
            mk
                "mfi"
                []
                HighLowCloseVolume
                FloatOutput
                (fun call ctx ->
                    let p = parseInt call 0 14
                    let ops = resolveOperands ctx call HighLowCloseVolume
                    Float(Volume.mfi p ops.[0] ops.[1] ops.[2] ops.[3]))
                (fun call -> parseInt call 0 14)
                Map.empty

        let vwapSpec =
            mk
                "vwap"
                []
                HighLowCloseVolume
                FloatOutput
                (fun call ctx ->
                    let ops = resolveOperands ctx call HighLowCloseVolume
                    Float(Volume.vwap ops.[0] ops.[1] ops.[2] ops.[3]))
                (fun _ -> 0)
                Map.empty

        let vwmaSpec =
            mk
                "vwma"
                []
                CloseVolume
                FloatOutput
                (fun call ctx ->
                    let p = parseInt call 0 20
                    let ops = resolveOperands ctx call CloseVolume
                    Float(Volume.vwma p ops.[0] ops.[1]))
                (fun call -> parseInt call 0 20 - 1)
                Map.empty

        let adxSpec =
            mk
                "adx"
                [ "dmi" ]
                HighLowClose
                FloatOutput
                (fun call ctx ->
                    let p = parseInt call 0 14
                    let ops = resolveOperands ctx call HighLowClose
                    Float(Directional.adx p ops.[0] ops.[1] ops.[2]))
                (fun call -> parseInt call 0 14 * 2 - 1)
                Map.empty

        let aroonSpec =
            mk
                "aroon"
                []
                HighLow
                FloatOutput
                (fun call ctx ->
                    let p = parseInt call 0 25
                    let ops = resolveOperands ctx call HighLow
                    let up, down = Directional.aroon p ops.[0] ops.[1]

                    match call.Sub with
                    | Some "up" -> Float up
                    | Some "down" -> Float down
                    | _ -> Float up)
                (fun call -> parseInt call 0 25 - 1)
                (Map.ofList
                    [ "up", leaf "up" HighLow (fun _ _ -> failwith "") (fun _ -> 0)
                      "down", leaf "down" HighLow (fun _ _ -> failwith "") (fun _ -> 0) ])

        let superTrendSpec =
            mk
                "super-trend"
                [ "supertrend" ]
                HighLowClose
                FloatOutput
                (fun call ctx ->
                    let lb = parseInt call 0 10
                    let mult = parseFloat call 1 3.0
                    let ops = resolveOperands ctx call HighLowClose
                    let st, dir = Directional.superTrend lb mult ops.[0] ops.[1] ops.[2]

                    match call.Sub with
                    | Some "direction" -> Bool dir
                    | _ -> Float st)
                (fun call -> parseInt call 0 10)
                (Map.ofList
                    [ "direction",
                      { leaf "direction" HighLowClose (fun _ _ -> failwith "") (fun _ -> 0) with
                          OutputKind = BoolOutput } ])

        let parabolicSpec =
            mk
                "parabolic-sar"
                [ "parabolicsar" ]
                HighLow
                FloatOutput
                (fun call ctx ->
                    let step = parseFloat call 0 0.02
                    let maxA = parseFloat call 1 0.2
                    let ops = resolveOperands ctx call HighLow
                    Float(Directional.parabolicSar step maxA ops.[0] ops.[1]))
                (fun _ -> 0)
                Map.empty

        let ichimokuSpec =
            mk
                "ichimoku"
                []
                HighLowClose
                FloatOutput
                (fun call ctx ->
                    let ten = parseInt call 0 9
                    let kij = parseInt call 1 26
                    let sen = parseInt call 2 52
                    let ops = resolveOperands ctx call HighLowClose

                    let t, k, sa, sb, ch = Directional.ichimoku ten kij sen ops.[0] ops.[1] ops.[2]

                    match call.Sub with
                    | Some "tenkan" -> Float t
                    | Some "kijun" -> Float k
                    | Some "senkou-a" -> Float sa
                    | Some "senkou-b" -> Float sb
                    | Some "chikou" -> Float ch
                    | _ -> Float t)
                (fun _ -> 52)
                (Map.ofList
                    [ "tenkan", leaf "tenkan" HighLowClose (fun _ _ -> failwith "") (fun _ -> 0)
                      "kijun", leaf "kijun" HighLowClose (fun _ _ -> failwith "") (fun _ -> 0)
                      "senkou-a", leaf "senkou-a" HighLowClose (fun _ _ -> failwith "") (fun _ -> 0)
                      "senkou-b", leaf "senkou-b" HighLowClose (fun _ _ -> failwith "") (fun _ -> 0)
                      "chikou", leaf "chikou" HighLowClose (fun _ _ -> failwith "") (fun _ -> 0) ])

        let heikinSpec =
            mk
                "heikin-ashi"
                [ "heikinashi" ]
                OpenHighLowClose
                FloatOutput
                (fun call ctx ->
                    let ops = resolveOperands ctx call OpenHighLowClose

                    let haOpen, haHigh, haLow, haClose =
                        Price.heikinAshi ops.[0] ops.[1] ops.[2] ops.[3]

                    match call.Sub with
                    | Some "open" -> Float haOpen
                    | Some "high" -> Float haHigh
                    | Some "low" -> Float haLow
                    | Some "close" -> Float haClose
                    | _ -> Float haClose)
                (fun _ -> 0)
                (Map.ofList
                    [ "open", leaf "open" OpenHighLowClose (fun _ _ -> failwith "") (fun _ -> 0)
                      "high", leaf "high" OpenHighLowClose (fun _ _ -> failwith "") (fun _ -> 0)
                      "low", leaf "low" OpenHighLowClose (fun _ _ -> failwith "") (fun _ -> 0)
                      "close", leaf "close" OpenHighLowClose (fun _ _ -> failwith "") (fun _ -> 0) ])

        let barPartSpec =
            mk
                "bar-part"
                [ "barpart" ]
                HighLowClose
                FloatOutput
                (fun call ctx ->
                    let part =
                        match arg call 0 |> fun s -> s.ToLowerInvariant() with
                        | "hl2" -> "hl2"
                        | "hlc3" -> "hlc3"
                        | "ohlc4" -> "ohlc4"
                        | "ohl3" -> "hlc3"
                        | "oc2" -> "hlc3"
                        | "" -> "close"
                        | s -> raise (DirectiveValueError $"unknown bar-part '{s}'")

                    let ops = resolveOperands ctx call HighLowClose
                    Float(Price.barPart part ops.[0] ops.[1] ops.[2]))
                (fun _ -> 0)
                Map.empty

        let dojiSpec =
            mk
                "doji"
                []
                OpenHighLowClose
                BoolOutput
                (fun call ctx ->
                    let pct = parseFloat call 0 0.1
                    let ops = resolveOperands ctx call OpenHighLowClose
                    Bool(Price.doji pct ops.[0] ops.[1] ops.[2] ops.[3]))
                (fun _ -> 0)
                Map.empty

        let increaseCompute (call: IndicatorCall) (ctx: EvalContext) =
            let n = parseInt call 0 3
            let s = arg call 0

            if s = "" then
                raise (DirectiveValueError "indicator 'increase' requires argument 1 (periods)")

            let ops = resolveOperands ctx call CloseOnly
            let a = ops.[0]
            let out = Array.zeroCreate<bool> a.Length

            for i in 0 .. a.Length - 1 do
                if i >= n then
                    let mutable ok = true

                    for k in 1..n do
                        if a.[i - k + 1] <= a.[i - k] then
                            ok <- false

                    out.[i] <- ok

            Bool out

        let increaseSpec =
            mk "increase" [] CloseOnly BoolOutput increaseCompute (fun call -> parseInt call 0 3) Map.empty

        let repeatCompute (call: IndicatorCall) (ctx: EvalContext) =
            let n = parseInt call 0 3
            let s = arg call 0

            if s = "" then
                raise (DirectiveValueError "indicator 'repeat' requires argument 1 (periods)")

            if call.Series.IsEmpty then
                raise (DirectiveValueError "indicator 'repeat' requires a bool series operand")

            let b =
                match ctx.Resolve call.Series.[0] with
                | Bool arr -> arr
                | Float _ -> raise (DirectiveValueError "indicator 'repeat' requires a bool series operand")

            let out = Array.zeroCreate<bool> b.Length

            for i in 0 .. b.Length - 1 do
                if i >= n - 1 then
                    let mutable ok = true

                    for k in 0 .. n - 1 do
                        if not b.[i - k] then
                            ok <- false

                    out.[i] <- ok

            Bool out

        let repeatSpec =
            mk "repeat" [] CloseOnly BoolOutput repeatCompute (fun call -> parseInt call 0 3) Map.empty

        let all =
            [ smaSpec
              emaSpec
              wmaSpec
              smmaSpec
              hmaSpec
              rsiSpec
              macdSpec
              stochSpec
              kdjSpec
              cciSpec
              rocSpec
              stochRsiSpec
              bollSpec
              atrSpec
              trSpec
              natrSpec
              donchianSpec
              keltnerSpec
              stdDevSpec
              obvSpec
              adlSpec
              cmfSpec
              mfiSpec
              vwapSpec
              vwmaSpec
              adxSpec
              aroonSpec
              superTrendSpec
              parabolicSpec
              ichimokuSpec
              heikinSpec
              barPartSpec
              dojiSpec
              increaseSpec
              repeatSpec ]

        all
        |> List.collect (fun s -> (s.Name, s) :: (s.Aliases |> List.map (fun a -> a, s)))
        |> Map.ofList

module IndicatorRegistry =
    let table: Map<string, IndicatorSpec> = Registry.buildTable ()

    let tryResolve (name: string) : IndicatorSpec option =
        Map.tryFind (name.ToLowerInvariant()) table

    let resolve (name: string) : IndicatorSpec =
        match tryResolve name with
        | Some spec -> spec
        | None -> raise (DirectiveValueError $"unknown indicator '{name.ToLowerInvariant()}'")
