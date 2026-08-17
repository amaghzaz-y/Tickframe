namespace Tickframe

open System
open FacioQuo.Stock.Indicators

module internal RegistryHelpers =

    let toReusable (frame: OhlcvFrame) (values: float[]) : System.Collections.Generic.IReadOnlyList<IReusable> =
        let bars =
            [| for i in 0 .. values.Length - 1 ->
                   let v = values.[i]

                   let d =
                       if Double.IsNaN v || Double.IsInfinity v then
                           0m
                       else
                           decimal v

                   Bar(frame.Candles.[i].Timestamp, 0m, 0m, 0m, d, 0m) :> IBar |]
            :> System.Collections.Generic.IReadOnlyList<IBar>

        Reusable.ToReusable(bars, CandlePart.Close)

    let toBars
        (frame: OhlcvFrame)
        (mapped: decimal[][])
        (slotCount: int)
        : System.Collections.Generic.IReadOnlyList<IBar> =
        [| for i in 0 .. frame.RowCount - 1 ->
               let ts = frame.Candles.[i].Timestamp

               let get (idx: int) =
                   if idx < mapped.Length then mapped.[idx].[i] else 0m

               match slotCount with
               | 1 -> Bar(ts, 0m, 0m, 0m, get 0, 0m) :> IBar
               | 2 -> Bar(ts, 0m, get 0, get 1, 0m, 0m) :> IBar
               | 3 -> Bar(ts, 0m, get 0, get 1, get 2, 0m) :> IBar
               | 4 -> Bar(ts, get 0, get 1, get 2, get 3, 0m) :> IBar
               | 5 -> Bar(ts, get 0, get 1, get 2, get 3, get 4) :> IBar
               | _ -> Bar(ts, 0m, 0m, 0m, 0m, 0m) :> IBar |]
        :> System.Collections.Generic.IReadOnlyList<IBar>

    let toBarsOHLCV
        (frame: OhlcvFrame)
        (opens: decimal[])
        (highs: decimal[])
        (lows: decimal[])
        (closes: decimal[])
        (volumes: decimal[])
        =
        [| for i in 0 .. frame.RowCount - 1 ->
               Bar(frame.Candles.[i].Timestamp, opens.[i], highs.[i], lows.[i], closes.[i], volumes.[i]) :> IBar |]
        :> System.Collections.Generic.IReadOnlyList<IBar>

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

    let floatsToDecimals (values: float[]) : decimal[] =
        values
        |> Array.map (fun v ->
            if Double.IsNaN v || Double.IsInfinity v then
                0m
            else
                decimal v)

    let toNullableFloat (v: Nullable<double>) : float =
        if v.HasValue then v.Value else Double.NaN

    let toFloat (v: double) : float = v

    let toFloatN (v: Nullable<double>) : float =
        if v.HasValue then v.Value else Double.NaN

    let toFloatDecimal (v: Nullable<decimal>) : float =
        if v.HasValue then float v.Value else Double.NaN

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

    let parseDecimal (call: IndicatorCall) (idx: int) (defaultValue: decimal) : decimal =
        let s = arg call idx

        if s = "" then
            defaultValue
        else
            match
                Decimal.TryParse(
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

    let projectFloat (results: System.Collections.Generic.IReadOnlyList<'T>) (selector: 'T -> float) : Series =
        Float [| for r in results -> selector r |]

    let projectBool (results: System.Collections.Generic.IReadOnlyList<'T>) (selector: 'T -> bool) : Series =
        Bool [| for r in results -> selector r |]

    let nullableBool (v: Nullable<bool>) : bool = if v.HasValue then v.Value else false

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
            (toFn:
                System.Collections.Generic.IReadOnlyList<IReusable> * int
                    -> System.Collections.Generic.IReadOnlyList<'R>)
            (getValue: 'R -> float)
            (defaultPeriod: int)
            : IndicatorSpec =
            mk
                name
                aliases
                CloseOnly
                FloatOutput
                (fun call ctx ->
                    let period = parseInt call 0 defaultPeriod
                    let ops = resolveOperands ctx call CloseOnly
                    let src = toReusable ctx.Frame ops.[0]
                    let res = toFn (src, period)
                    projectFloat res getValue)
                (fun call -> parseInt call 0 defaultPeriod)
                Map.empty

        let smaSpec =
            smaLike "ma" [ "sma" ] (fun (src, p) -> Sma.ToSma(src, p)) (fun (r: SmaResult) -> toFloatN r.Sma) 20

        let emaSpec =
            smaLike "ema" [] (fun (src, p) -> Ema.ToEma(src, p)) (fun (r: EmaResult) -> toFloatN r.Ema) 20

        let wmaSpec =
            smaLike "wma" [] (fun (src, p) -> Wma.ToWma(src, p)) (fun (r: WmaResult) -> toFloatN r.Wma) 20

        let hmaSpec =
            smaLike "hma" [] (fun (src, p) -> Hma.ToHma(src, p)) (fun (r: HmaResult) -> toFloatN r.Hma) 14

        let demaSpec =
            smaLike "dema" [] (fun (src, p) -> Dema.ToDema(src, p)) (fun (r: DemaResult) -> toFloatN r.Dema) 20

        let temaSpec =
            smaLike "tema" [] (fun (src, p) -> Tema.ToTema(src, p)) (fun (r: TemaResult) -> toFloatN r.Tema) 20

        let smmaSpec =
            smaLike
                "smma"
                [ "mma"; "rma" ]
                (fun (src, p) -> Smma.ToSmma(src, p))
                (fun (r: SmmaResult) -> toFloatN r.Smma)
                20

        let epmaSpec =
            smaLike "epma" [ "lsma" ] (fun (src, p) -> Epma.ToEpma(src, p)) (fun (r: EpmaResult) -> toFloatN r.Value) 10

        let almaSpec =
            mk
                "alma"
                []
                CloseOnly
                FloatOutput
                (fun call ctx ->
                    let period = parseInt call 0 9
                    let offset = parseFloat call 1 0.85
                    let sigma = parseFloat call 2 6.0
                    let ops = resolveOperands ctx call CloseOnly
                    let res = Alma.ToAlma(toReusable ctx.Frame ops.[0], period, offset, sigma)
                    projectFloat res (fun (r: AlmaResult) -> toFloatN r.Alma))
                (fun call -> parseInt call 0 9)
                Map.empty

        let kamaSpec =
            mk
                "kama"
                []
                CloseOnly
                FloatOutput
                (fun call ctx ->
                    let er = parseInt call 0 10
                    let fast = parseInt call 1 2
                    let slow = parseInt call 2 30
                    let ops = resolveOperands ctx call CloseOnly
                    let res = Kama.ToKama(toReusable ctx.Frame ops.[0], er, fast, slow)
                    projectFloat res (fun (r: KamaResult) -> toFloatN r.Kama))
                (fun call -> parseInt call 0 10)
                Map.empty

        let mamaFamaCompute (call: IndicatorCall) (ctx: EvalContext) =
            let fast = parseFloat call 0 0.5
            let slow = parseFloat call 1 0.05
            let ops = resolveOperands ctx call CloseOnly
            let res = Mama.ToMama(toReusable ctx.Frame ops.[0], fast, slow)
            projectFloat res (fun (r: MamaResult) -> toFloatN r.Fama)

        let mamaSpec =
            mk
                "mama"
                []
                CloseOnly
                FloatOutput
                (fun call ctx ->
                    let fast = parseFloat call 0 0.5
                    let slow = parseFloat call 1 0.05
                    let ops = resolveOperands ctx call CloseOnly
                    let res = Mama.ToMama(toReusable ctx.Frame ops.[0], fast, slow)

                    match call.Sub with
                    | Some "fama" -> projectFloat res (fun (r: MamaResult) -> toFloatN r.Fama)
                    | _ -> projectFloat res (fun (r: MamaResult) -> toFloatN r.Mama))
                (fun _ -> 0)
                (Map.ofList
                    [ "fama",
                      { Name = "fama"
                        Aliases = []
                        SubCommands = Map.empty
                        Slots = CloseOnly
                        OutputKind = FloatOutput
                        Compute = mamaFamaCompute
                        Lookback = fun _ -> 0 } ])

        let t3Spec =
            mk
                "t3"
                []
                CloseOnly
                FloatOutput
                (fun call ctx ->
                    let period = parseInt call 0 5
                    let vf = parseFloat call 1 0.7
                    let ops = resolveOperands ctx call CloseOnly
                    let res = T3.ToT3(toReusable ctx.Frame ops.[0], period, vf)
                    projectFloat res (fun (r: T3Result) -> toFloatN r.T3))
                (fun call -> parseInt call 0 5)
                Map.empty

        let mgDynSpec =
            mk
                "dynamic"
                []
                CloseOnly
                FloatOutput
                (fun call ctx ->
                    let period = parseInt call 0 20
                    let k = parseFloat call 1 0.6
                    let s = arg call 0

                    if s = "" then
                        raise (DirectiveValueError "indicator 'dynamic' requires argument 1 (period)")

                    let ops = resolveOperands ctx call CloseOnly
                    let res = MgDynamic.ToDynamic(toReusable ctx.Frame ops.[0], period, k)
                    projectFloat res (fun (r: DynamicResult) -> toFloatN r.Value))
                (fun call -> let _ = requireArg call 0 "period" in parseInt call 0 20)
                Map.empty

        let rsiSpec =
            smaLike "rsi" [] (fun (src, p) -> Rsi.ToRsi(src, p)) (fun (r: RsiResult) -> toFloatN r.Rsi) 14

        let cmoSpec =
            smaLike "cmo" [] (fun (src, p) -> Cmo.ToCmo(src, p)) (fun (r: CmoResult) -> toFloatN r.Cmo) 14

        let trixSpec =
            smaLike "trix" [] (fun (src, p) -> Trix.ToTrix(src, p)) (fun (r: TrixResult) -> toFloatN r.Trix) 14

        let rocSpec =
            smaLike "roc" [] (fun (src, p) -> Roc.ToRoc(src, p)) (fun (r: RocResult) -> toFloatN r.Roc) 14

        let rocWbSpec =
            mk
                "roc-wb"
                [ "rocwb" ]
                CloseOnly
                FloatOutput
                (fun call ctx ->
                    let lb = parseInt call 0 20
                    let ema = parseInt call 1 5
                    let sd = parseInt call 2 5
                    let ops = resolveOperands ctx call CloseOnly
                    let res = RocWb.ToRocWb(toReusable ctx.Frame ops.[0], lb, ema, sd)
                    projectFloat res (fun (r: RocWbResult) -> toFloatN r.Value))
                (fun call -> parseInt call 0 20)
                Map.empty

        let stcSpec =
            mk
                "stc"
                []
                CloseOnly
                FloatOutput
                (fun call ctx ->
                    let cyc = parseInt call 0 10
                    let fast = parseInt call 1 23
                    let slow = parseInt call 2 50
                    let ops = resolveOperands ctx call CloseOnly
                    let res = Stc.ToStc(toReusable ctx.Frame ops.[0], cyc, fast, slow)
                    projectFloat res (fun (r: StcResult) -> toFloatN r.Stc))
                (fun call -> parseInt call 0 10)
                Map.empty

        let pmoSpec =
            mk
                "pmo"
                []
                CloseOnly
                FloatOutput
                (fun call ctx ->
                    let tp = parseInt call 0 35
                    let sm = parseInt call 1 20
                    let sig_ = parseInt call 2 10
                    let ops = resolveOperands ctx call CloseOnly
                    let res = Pmo.ToPmo(toReusable ctx.Frame ops.[0], tp, sm, sig_)

                    match call.Sub with
                    | Some "signal" -> projectFloat res (fun (r: PmoResult) -> toFloatN r.Signal)
                    | _ -> projectFloat res (fun (r: PmoResult) -> toFloatN r.Pmo))
                (fun call -> parseInt call 0 35)
                (Map.ofList [ "signal", leaf "signal" CloseOnly (fun _ _ -> failwith "sub") (fun _ -> 0) ])

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
                    let ops = resolveOperands ctx call CloseOnly
                    let res = Macd.ToMacd(toReusable ctx.Frame ops.[0], fast, slow, sig_)

                    match call.Sub with
                    | Some "signal" -> projectFloat res (fun (r: MacdResult) -> toFloatN r.Signal)
                    | Some "histogram" -> projectFloat res (fun (r: MacdResult) -> toFloatN r.Histogram)
                    | _ -> projectFloat res (fun (r: MacdResult) -> toFloatN r.Macd))
                (fun _ -> 26 + 9)
                (Map.ofList
                    [ "signal", leaf "signal" CloseOnly (fun _ _ -> failwith "sub") (fun _ -> 0)
                      "histogram", leaf "histogram" CloseOnly (fun _ _ -> failwith "sub") (fun _ -> 0) ])

        let tsiSpec =
            mk
                "tsi"
                []
                CloseOnly
                FloatOutput
                (fun call ctx ->
                    let lb = parseInt call 0 25
                    let sm = parseInt call 1 13
                    let sig_ = parseInt call 2 7
                    let ops = resolveOperands ctx call CloseOnly
                    let res = Tsi.ToTsi(toReusable ctx.Frame ops.[0], lb, sm, sig_)

                    match call.Sub with
                    | Some "signal" -> projectFloat res (fun (r: TsiResult) -> toFloatN r.Signal)
                    | _ -> projectFloat res (fun (r: TsiResult) -> toFloatN r.Tsi))
                (fun call -> parseInt call 0 25)
                (Map.ofList [ "signal", leaf "signal" CloseOnly (fun _ _ -> failwith "sub") (fun _ -> 0) ])

        let connorsSpec =
            mk
                "connors-rsi"
                [ "connorsrsi" ]
                CloseOnly
                FloatOutput
                (fun call ctx ->
                    let rsi = parseInt call 0 3
                    let streak = parseInt call 1 2
                    let rank = parseInt call 2 100
                    let ops = resolveOperands ctx call CloseOnly
                    let res = ConnorsRsi.ToConnorsRsi(toReusable ctx.Frame ops.[0], rsi, streak, rank)
                    projectFloat res (fun (r: ConnorsRsiResult) -> toFloatN r.ConnorsRsi))
                (fun call -> parseInt call 2 100)
                Map.empty

        let stochRsiSpec =
            mk
                "stoch-rsi"
                [ "stochrsi" ]
                CloseOnly
                FloatOutput
                (fun call ctx ->
                    let rsi = parseInt call 0 14
                    let stoch = parseInt call 1 14
                    let sig_ = parseInt call 2 3
                    let smooth = parseInt call 3 1
                    let ops = resolveOperands ctx call CloseOnly

                    let res =
                        StochRsi.ToStochRsi(toReusable ctx.Frame ops.[0], rsi, stoch, sig_, smooth)

                    match call.Sub with
                    | Some "k" -> projectFloat res (fun (r: StochRsiResult) -> toFloat r.StochRsi.Value)
                    | Some "d" -> projectFloat res (fun (r: StochRsiResult) -> toFloatN r.Signal)
                    | _ -> projectFloat res (fun (r: StochRsiResult) -> toFloat r.StochRsi.Value))
                (fun _ -> 14)
                (Map.ofList
                    [ "k", leaf "k" CloseOnly (fun _ _ -> failwith "") (fun _ -> 0)
                      "d", leaf "d" CloseOnly (fun _ _ -> failwith "") (fun _ -> 0) ])

        let hurstSpec =
            smaLike
                "hurst"
                []
                (fun (src, p) -> Hurst.ToHurst(src, p))
                (fun (r: HurstResult) -> toFloatN r.HurstExponent)
                100

        let ulcerSpec =
            smaLike
                "ulcer-index"
                [ "ulcerindex" ]
                (fun (src, p) -> UlcerIndex.ToUlcerIndex(src, p))
                (fun (r: UlcerIndexResult) -> toFloatN r.UlcerIndex)
                14

        let fisherSpec =
            smaLike
                "fisher-transform"
                [ "fishertransform" ]
                (fun (src, p) -> FisherTransform.ToFisherTransform(src, p))
                (fun (r: FisherTransformResult) -> r.Fisher)
                10

        let stdDevSpec =
            mk
                "std-dev"
                [ "stddev" ]
                CloseOnly
                FloatOutput
                (fun call ctx ->
                    let p = parseInt call 0 14
                    let ops = resolveOperands ctx call CloseOnly
                    let res = StdDev.ToStdDev(toReusable ctx.Frame ops.[0], p)

                    match call.Sub with
                    | Some "zscore" -> projectFloat res (fun (r: StdDevResult) -> toFloatN r.ZScore)
                    | Some "mean" -> projectFloat res (fun (r: StdDevResult) -> toFloatN r.Mean)
                    | _ -> projectFloat res (fun (r: StdDevResult) -> toFloatN r.StdDev))
                (fun call -> parseInt call 0 14)
                (Map.ofList
                    [ "zscore", leaf "zscore" CloseOnly (fun _ _ -> failwith "") (fun _ -> 0)
                      "mean", leaf "mean" CloseOnly (fun _ _ -> failwith "") (fun _ -> 0) ])

        let slopeSpec =
            smaLike "slope" [] (fun (src, p) -> Slope.ToSlope(src, p)) (fun (r: SlopeResult) -> toFloatN r.Slope) 14

        let htSpec =
            mk
                "ht-trendline"
                [ "httrendline" ]
                CloseOnly
                FloatOutput
                (fun call ctx ->
                    let ops = resolveOperands ctx call CloseOnly
                    let res = HtTrendline.ToHtTrendline(toReusable ctx.Frame ops.[0])
                    projectFloat res (fun (r: HtlResult) -> toFloatN r.Value))
                (fun _ -> 0)
                Map.empty

        let dpoSpec =
            smaLike "dpo" [] (fun (src, p) -> Dpo.ToDpo(src, p)) (fun (r: DpoResult) -> toFloatN r.Dpo) 20

        let barCloseOnly
            (toFn: System.Collections.Generic.IReadOnlyList<IBar> * int -> System.Collections.Generic.IReadOnlyList<'R>)
            (getValue: 'R -> float)
            (defaultPeriod: int)
            : (IndicatorCall -> EvalContext -> Series) * (IndicatorCall -> int) =
            let compute (call: IndicatorCall) (ctx: EvalContext) =
                let p = parseInt call 0 defaultPeriod
                let ops = resolveOperands ctx call HighLowClose
                let decs = ops |> Array.map floatsToDecimals
                let bars = toBars ctx.Frame decs 3
                let res = toFn (bars, p)
                projectFloat res getValue

            let lb (call: IndicatorCall) = parseInt call 0 defaultPeriod
            compute, lb

        let atrSpec =
            let c, l =
                barCloseOnly (fun (b, p) -> Atr.ToAtr(b, p)) (fun (r: AtrResult) -> toFloatN r.Atr) 14

            mk "atr" [] HighLowClose FloatOutput c l Map.empty

        let natrSpec =
            mk
                "natr"
                []
                HighLowClose
                FloatOutput
                (fun call ctx ->
                    let p = parseInt call 0 14
                    let ops = resolveOperands ctx call HighLowClose
                    let decs = ops |> Array.map floatsToDecimals
                    let bars = toBars ctx.Frame decs 3
                    let res = Atr.ToAtr(bars, p)
                    projectFloat res (fun (r: AtrResult) -> toFloatN r.Atrp))
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
                    let decs = ops |> Array.map floatsToDecimals
                    let bars = toBars ctx.Frame decs 3
                    let res = Tr.ToTr(bars)
                    projectFloat res (fun (r: TrResult) -> toFloatN r.Tr))
                (fun _ -> 1)
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
                    let decs = ops |> Array.map floatsToDecimals
                    let bars = toBars ctx.Frame decs 3
                    let res = Adx.ToAdx(bars, p)
                    projectFloat res (fun (r: AdxResult) -> toFloatN r.Adx))
                (fun call -> parseInt call 0 14)
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
                    let decs = ops |> Array.map floatsToDecimals
                    let bars = toBars ctx.Frame decs 2
                    let res = Aroon.ToAroon(bars, p)

                    match call.Sub with
                    | Some "up" -> projectFloat res (fun (r: AroonResult) -> toFloatN r.AroonUp)
                    | Some "down" -> projectFloat res (fun (r: AroonResult) -> toFloatN r.AroonDown)
                    | _ -> projectFloat res (fun (r: AroonResult) -> toFloatN r.Value))
                (fun call -> parseInt call 0 25)
                (Map.ofList
                    [ "up", leaf "up" HighLow (fun _ _ -> failwith "") (fun _ -> 0)
                      "down", leaf "down" HighLow (fun _ _ -> failwith "") (fun _ -> 0) ])

        let cciSpec =
            let c, l =
                barCloseOnly (fun (b, p) -> Cci.ToCci(b, p)) (fun (r: CciResult) -> toFloatN r.Cci) 20

            mk "cci" [] HighLowClose FloatOutput c l Map.empty

        let chopSpec =
            let c, l =
                barCloseOnly (fun (b, p) -> Chop.ToChop(b, p)) (fun (r: ChopResult) -> toFloatN r.Chop) 14

            mk "chop" [] HighLowClose FloatOutput c l Map.empty

        let bopSpec =
            mk
                "bop"
                []
                OpenHighLowClose
                FloatOutput
                (fun call ctx ->
                    let p = parseInt call 0 14
                    let ops = resolveOperands ctx call OpenHighLowClose
                    let decs = ops |> Array.map floatsToDecimals
                    let bars = toBars ctx.Frame decs 4
                    let res = Bop.ToBop(bars, p)
                    projectFloat res (fun (r: BopResult) -> toFloatN r.Bop))
                (fun call -> parseInt call 0 14)
                Map.empty

        let ultimateSpec =
            mk
                "ultimate"
                []
                HighLowClose
                FloatOutput
                (fun call ctx ->
                    let s = parseInt call 0 7
                    let m = parseInt call 1 14
                    let l = parseInt call 2 28
                    let ops = resolveOperands ctx call HighLowClose
                    let decs = ops |> Array.map floatsToDecimals
                    let bars = toBars ctx.Frame decs 3
                    let res = Ultimate.ToUltimate(bars, s, m, l)
                    projectFloat res (fun (r: UltimateResult) -> toFloatN r.Ultimate))
                (fun _ -> 28)
                Map.empty

        let williamsSpec =
            let c, l =
                barCloseOnly
                    (fun (b, p) -> WilliamsR.ToWilliamsR(b, p))
                    (fun (r: WilliamsResult) -> toFloatN r.WilliamsR)
                    14

            mk "williams-r" [ "williamsr" ] HighLowClose FloatOutput c l Map.empty

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
                    let decs = ops |> Array.map floatsToDecimals
                    let bars = toBars ctx.Frame decs 3
                    let res = Stoch.ToStoch(bars, lb, sig_, sm)

                    match call.Sub with
                    | Some "k" -> projectFloat res (fun (r: StochResult) -> toFloatN r.K)
                    | Some "d" -> projectFloat res (fun (r: StochResult) -> toFloatN r.D)
                    | Some "j" -> projectFloat res (fun (r: StochResult) -> toFloatN r.J)
                    | _ -> projectFloat res (fun (r: StochResult) -> toFloatN r.K))
                (fun call -> parseInt call 0 14)
                (Map.ofList
                    [ "k", leaf "k" HighLowClose (fun _ _ -> failwith "") (fun _ -> 0)
                      "d", leaf "d" HighLowClose (fun _ _ -> failwith "") (fun _ -> 0)
                      "j", leaf "j" HighLowClose (fun _ _ -> failwith "") (fun _ -> 0) ])

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
                    let decs = ops |> Array.map floatsToDecimals
                    let bars = toBars ctx.Frame decs 3
                    let res = Stoch.ToStoch(bars, lb, sig_, sm)

                    match call.Sub with
                    | Some "k" -> projectFloat res (fun (r: StochResult) -> toFloatN r.K)
                    | Some "d" -> projectFloat res (fun (r: StochResult) -> toFloatN r.D)
                    | Some "j" -> projectFloat res (fun (r: StochResult) -> toFloatN r.J)
                    | _ -> projectFloat res (fun (r: StochResult) -> toFloatN r.K))
                (fun call -> parseInt call 0 14)
                (Map.ofList
                    [ "k", leaf "k" HighLowClose (fun _ _ -> failwith "") (fun _ -> 0)
                      "d", leaf "d" HighLowClose (fun _ _ -> failwith "") (fun _ -> 0)
                      "j", leaf "j" HighLowClose (fun _ _ -> failwith "") (fun _ -> 0) ])

        let smiSpec =
            mk
                "smi"
                []
                HighLowClose
                FloatOutput
                (fun call ctx ->
                    let lb = parseInt call 0 13
                    let f = parseInt call 1 25
                    let s = parseInt call 2 2
                    let sig_ = parseInt call 3 3
                    let ops = resolveOperands ctx call HighLowClose
                    let decs = ops |> Array.map floatsToDecimals
                    let bars = toBars ctx.Frame decs 3
                    let res = Smi.ToSmi(bars, lb, f, s, sig_)

                    match call.Sub with
                    | Some "signal" -> projectFloat res (fun (r: SmiResult) -> toFloatN r.Signal)
                    | _ -> projectFloat res (fun (r: SmiResult) -> toFloatN r.Smi))
                (fun _ -> 13 + 25)
                (Map.ofList [ "signal", leaf "signal" HighLowClose (fun _ _ -> failwith "") (fun _ -> 0) ])

        let bollSpec =
            mk
                "boll"
                []
                CloseOnly
                FloatOutput
                (fun call ctx ->
                    let lb = parseInt call 0 20
                    let sd = parseFloat call 1 2.0
                    let ops = resolveOperands ctx call CloseOnly
                    let res = BollingerBands.ToBollingerBands(toReusable ctx.Frame ops.[0], lb, sd)

                    match call.Sub with
                    | Some "upper" -> projectFloat res (fun (r: BollingerBandsResult) -> toFloatN r.UpperBand)
                    | Some "lower" -> projectFloat res (fun (r: BollingerBandsResult) -> toFloatN r.LowerBand)
                    | Some "middle" -> projectFloat res (fun (r: BollingerBandsResult) -> toFloatN r.Sma)
                    | _ -> projectFloat res (fun (r: BollingerBandsResult) -> toFloatN r.Sma))
                (fun call -> parseInt call 0 20)
                (Map.ofList
                    [ "upper", leaf "upper" CloseOnly (fun _ _ -> failwith "") (fun _ -> 0)
                      "lower", leaf "lower" CloseOnly (fun _ _ -> failwith "") (fun _ -> 0)
                      "middle", leaf "middle" CloseOnly (fun _ _ -> failwith "") (fun _ -> 0) ])

        let donchianSpec =
            mk
                "donchian"
                []
                HighLow
                FloatOutput
                (fun call ctx ->
                    let p = parseInt call 0 20
                    let ops = resolveOperands ctx call HighLow
                    let decs = ops |> Array.map floatsToDecimals
                    let bars = toBars ctx.Frame decs 2
                    let res = Donchian.ToDonchian(bars, p)

                    match call.Sub with
                    | Some "upper" -> projectFloat res (fun (r: DonchianResult) -> toFloatN r.UpperBand)
                    | Some "lower" -> projectFloat res (fun (r: DonchianResult) -> toFloatN r.LowerBand)
                    | Some "middle" -> projectFloat res (fun (r: DonchianResult) -> toFloatN r.Centerline)
                    | _ -> projectFloat res (fun (r: DonchianResult) -> toFloatN r.Centerline))
                (fun call -> parseInt call 0 20)
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
                    let ema = parseInt call 0 20
                    let mult = parseFloat call 1 2.0
                    let atr = parseInt call 2 10
                    let ops = resolveOperands ctx call HighLowClose
                    let decs = ops |> Array.map floatsToDecimals
                    let bars = toBars ctx.Frame decs 3
                    let res = Keltner.ToKeltner(bars, ema, mult, atr)

                    match call.Sub with
                    | Some "upper" -> projectFloat res (fun (r: KeltnerResult) -> toFloatN r.UpperBand)
                    | Some "lower" -> projectFloat res (fun (r: KeltnerResult) -> toFloatN r.LowerBand)
                    | Some "middle" -> projectFloat res (fun (r: KeltnerResult) -> toFloatN r.Centerline)
                    | _ -> projectFloat res (fun (r: KeltnerResult) -> toFloatN r.Centerline))
                (fun call -> parseInt call 0 20)
                (Map.ofList
                    [ "upper", leaf "upper" HighLowClose (fun _ _ -> failwith "") (fun _ -> 0)
                      "lower", leaf "lower" HighLowClose (fun _ _ -> failwith "") (fun _ -> 0)
                      "middle", leaf "middle" HighLowClose (fun _ _ -> failwith "") (fun _ -> 0) ])

        let starcSpec =
            mk
                "starc-bands"
                [ "starcbands" ]
                HighLowClose
                FloatOutput
                (fun call ctx ->
                    let sma = parseInt call 0 5
                    let mult = parseFloat call 1 2.0
                    let atr = parseInt call 2 10
                    let ops = resolveOperands ctx call HighLowClose
                    let decs = ops |> Array.map floatsToDecimals
                    let bars = toBars ctx.Frame decs 3
                    let res = StarcBands.ToStarcBands(bars, sma, mult, atr)

                    match call.Sub with
                    | Some "upper" -> projectFloat res (fun (r: StarcBandsResult) -> toFloatN r.UpperBand)
                    | Some "lower" -> projectFloat res (fun (r: StarcBandsResult) -> toFloatN r.LowerBand)
                    | Some "middle" -> projectFloat res (fun (r: StarcBandsResult) -> toFloatN r.Centerline)
                    | _ -> projectFloat res (fun (r: StarcBandsResult) -> toFloatN r.Centerline))
                (fun call -> parseInt call 0 5)
                (Map.ofList
                    [ "upper", leaf "upper" HighLowClose (fun _ _ -> failwith "") (fun _ -> 0)
                      "lower", leaf "lower" HighLowClose (fun _ _ -> failwith "") (fun _ -> 0)
                      "middle", leaf "middle" HighLowClose (fun _ _ -> failwith "") (fun _ -> 0) ])

        let fcbSpec =
            mk
                "fcb"
                []
                HighLowClose
                FloatOutput
                (fun call ctx ->
                    let p = parseInt call 0 2
                    let ops = resolveOperands ctx call HighLowClose
                    let decs = ops |> Array.map floatsToDecimals
                    let bars = toBars ctx.Frame decs 3
                    let res = Fcb.ToFcb(bars, p)

                    match call.Sub with
                    | Some "upper" -> projectFloat res (fun (r: FcbResult) -> toFloatDecimal r.UpperBand)
                    | Some "lower" -> projectFloat res (fun (r: FcbResult) -> toFloatDecimal r.LowerBand)
                    | _ -> projectFloat res (fun (r: FcbResult) -> toFloatDecimal r.UpperBand))
                (fun call -> parseInt call 0 2)
                (Map.ofList
                    [ "upper", leaf "upper" HighLowClose (fun _ _ -> failwith "") (fun _ -> 0)
                      "lower", leaf "lower" HighLowClose (fun _ _ -> failwith "") (fun _ -> 0) ])

        let maEnvSpec =
            mk
                "ma-envelopes"
                [ "maenvelopes" ]
                CloseOnly
                FloatOutput
                (fun call ctx ->
                    let lb = parseInt call 0 20
                    let pct = parseFloat call 1 2.5
                    let ops = resolveOperands ctx call CloseOnly
                    let res = MaEnvelopes.ToMaEnvelopes(toReusable ctx.Frame ops.[0], lb, pct)

                    match call.Sub with
                    | Some "upper" -> projectFloat res (fun (r: MaEnvelopeResult) -> toFloatN r.UpperEnvelope)
                    | Some "lower" -> projectFloat res (fun (r: MaEnvelopeResult) -> toFloatN r.LowerEnvelope)
                    | _ -> projectFloat res (fun (r: MaEnvelopeResult) -> toFloatN r.Centerline))
                (fun call -> parseInt call 0 20)
                (Map.ofList
                    [ "upper", leaf "upper" CloseOnly (fun _ _ -> failwith "") (fun _ -> 0)
                      "lower", leaf "lower" CloseOnly (fun _ _ -> failwith "") (fun _ -> 0) ])

        let stdDevChSpec =
            mk
                "std-dev-channels"
                [ "stddevchannels" ]
                CloseOnly
                FloatOutput
                (fun call ctx ->
                    let lb = parseInt call 0 20
                    let sd = parseFloat call 1 2.0
                    let ops = resolveOperands ctx call CloseOnly
                    let res = StdDevChannels.ToStdDevChannels(toReusable ctx.Frame ops.[0], lb, sd)

                    match call.Sub with
                    | Some "upper" -> projectFloat res (fun (r: StdDevChannelsResult) -> toFloatN r.UpperChannel)
                    | Some "lower" -> projectFloat res (fun (r: StdDevChannelsResult) -> toFloatN r.LowerChannel)
                    | Some "middle" -> projectFloat res (fun (r: StdDevChannelsResult) -> toFloatN r.Centerline)
                    | _ -> projectFloat res (fun (r: StdDevChannelsResult) -> toFloatN r.Centerline))
                (fun call -> parseInt call 0 20)
                (Map.ofList
                    [ "upper", leaf "upper" CloseOnly (fun _ _ -> failwith "") (fun _ -> 0)
                      "lower", leaf "lower" CloseOnly (fun _ _ -> failwith "") (fun _ -> 0)
                      "middle", leaf "middle" CloseOnly (fun _ _ -> failwith "") (fun _ -> 0) ])

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
                    let decs = ops |> Array.map floatsToDecimals
                    let bars = toBars ctx.Frame decs 3
                    let res = Ichimoku.ToIchimoku(bars, ten, kij, sen)

                    match call.Sub with
                    | Some "tenkan" -> projectFloat res (fun (r: IchimokuResult) -> toFloatN r.TenkanSen)
                    | Some "kijun" -> projectFloat res (fun (r: IchimokuResult) -> toFloatN r.KijunSen)
                    | Some "senkou-a" -> projectFloat res (fun (r: IchimokuResult) -> toFloatN r.SenkouSpanA)
                    | Some "senkou-b" -> projectFloat res (fun (r: IchimokuResult) -> toFloatN r.SenkouSpanB)
                    | Some "chikou" -> projectFloat res (fun (r: IchimokuResult) -> toFloatN r.ChikouSpan)
                    | _ -> projectFloat res (fun (r: IchimokuResult) -> toFloatN r.TenkanSen))
                (fun _ -> 52)
                (Map.ofList
                    [ "tenkan", leaf "tenkan" HighLowClose (fun _ _ -> failwith "") (fun _ -> 0)
                      "kijun", leaf "kijun" HighLowClose (fun _ _ -> failwith "") (fun _ -> 0)
                      "senkou-a", leaf "senkou-a" HighLowClose (fun _ _ -> failwith "") (fun _ -> 0)
                      "senkou-b", leaf "senkou-b" HighLowClose (fun _ _ -> failwith "") (fun _ -> 0)
                      "chikou", leaf "chikou" HighLowClose (fun _ _ -> failwith "") (fun _ -> 0) ])

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
                    let decs = ops |> Array.map floatsToDecimals
                    let bars = toBars ctx.Frame decs 3
                    let res = SuperTrend.ToSuperTrend(bars, lb, mult)

                    match call.Sub with
                    | Some "direction" ->
                        projectBool res (fun (r: SuperTrendResult) -> r.UpperBand.HasValue && r.LowerBand.HasValue)
                    | _ -> projectFloat res (fun (r: SuperTrendResult) -> toFloatDecimal r.SuperTrend))
                (fun call -> parseInt call 0 10)
                (Map.ofList
                    [ "direction",
                      { leaf "direction" HighLowClose (fun _ _ -> failwith "") (fun _ -> 0) with
                          OutputKind = BoolOutput } ])

        let vortexSpec =
            mk
                "vortex"
                []
                HighLowClose
                FloatOutput
                (fun call ctx ->
                    let p = parseInt call 0 14
                    let ops = resolveOperands ctx call HighLowClose
                    let decs = ops |> Array.map floatsToDecimals
                    let bars = toBars ctx.Frame decs 3
                    let res = Vortex.ToVortex(bars, p)

                    match call.Sub with
                    | Some "plus" -> projectFloat res (fun (r: VortexResult) -> toFloatN r.Pvi)
                    | Some "minus" -> projectFloat res (fun (r: VortexResult) -> toFloatN r.Nvi)
                    | _ -> projectFloat res (fun (r: VortexResult) -> toFloatN r.Pvi))
                (fun call -> parseInt call 0 14)
                (Map.ofList
                    [ "plus", leaf "plus" HighLowClose (fun _ _ -> failwith "") (fun _ -> 0)
                      "minus", leaf "minus" HighLowClose (fun _ _ -> failwith "") (fun _ -> 0) ])

        let alligatorSpec =
            mk
                "alligator"
                []
                CloseOnly
                FloatOutput
                (fun call ctx ->
                    let jaw = parseInt call 0 13
                    let jawOff = parseInt call 1 8
                    let teeth = parseInt call 2 8
                    let teethOff = parseInt call 3 5
                    let lips = parseInt call 4 5
                    let lipsOff = parseInt call 5 3
                    let ops = resolveOperands ctx call CloseOnly

                    let res =
                        Alligator.ToAlligator(
                            toReusable ctx.Frame ops.[0],
                            jaw,
                            jawOff,
                            teeth,
                            teethOff,
                            lips,
                            lipsOff
                        )

                    projectFloat res (fun (r: AlligatorResult) -> toFloatN r.Jaw))
                (fun _ -> 13 + 8)
                Map.empty

        let elderRaySpec =
            mk
                "elder-ray"
                [ "elderray" ]
                HighLowClose
                FloatOutput
                (fun call ctx ->
                    let p = parseInt call 0 13
                    let ops = resolveOperands ctx call HighLowClose
                    let decs = ops |> Array.map floatsToDecimals
                    let bars = toBars ctx.Frame decs 3
                    let res = ElderRay.ToElderRay(bars, p)

                    match call.Sub with
                    | Some "bull" -> projectFloat res (fun (r: ElderRayResult) -> toFloatN r.BullPower)
                    | Some "bear" -> projectFloat res (fun (r: ElderRayResult) -> toFloatN r.BearPower)
                    | _ -> projectFloat res (fun (r: ElderRayResult) -> toFloatN r.BullPower))
                (fun call -> parseInt call 0 13)
                (Map.ofList
                    [ "bull", leaf "bull" HighLowClose (fun _ _ -> failwith "") (fun _ -> 0)
                      "bear", leaf "bear" HighLowClose (fun _ _ -> failwith "") (fun _ -> 0) ])

        let gatorSpec =
            mk
                "gator"
                []
                CloseOnly
                FloatOutput
                (fun call ctx ->
                    let ops = resolveOperands ctx call CloseOnly
                    let res = Gator.ToGator(toReusable ctx.Frame ops.[0])
                    projectFloat res (fun (r: GatorResult) -> toFloatN r.Upper))
                (fun _ -> 0)
                Map.empty

        let awesomeSpec =
            mk
                "awesome"
                []
                CloseOnly
                FloatOutput
                (fun call ctx ->
                    let fast = parseInt call 0 5
                    let slow = parseInt call 1 34
                    let ops = resolveOperands ctx call CloseOnly
                    // Awesome is HighLowClose in FacioQuo but spec says HighLow; use close duplicated for bar bridge.
                    // Actually FacioQuo Awesome takes IReusable — use close-only bridge.
                    let res = Awesome.ToAwesome(toReusable ctx.Frame ops.[0], fast, slow)
                    projectFloat res (fun (r: AwesomeResult) -> toFloatN r.Oscillator))
                (fun _ -> 34)
                Map.empty

        let atrStopSpec =
            mk
                "atr-stop"
                [ "atrstop" ]
                HighLowClose
                FloatOutput
                (fun call ctx ->
                    let lb = parseInt call 0 21
                    let mult = parseFloat call 1 3.0
                    let ops = resolveOperands ctx call HighLowClose
                    let decs = ops |> Array.map floatsToDecimals
                    let bars = toBars ctx.Frame decs 3
                    let res = AtrStop.ToAtrStop(bars, lb, mult)
                    projectFloat res (fun (r: AtrStopResult) -> toFloatN r.Value))
                (fun call -> parseInt call 0 21)
                Map.empty

        let chandelierSpec =
            mk
                "chandelier"
                []
                HighLowClose
                FloatOutput
                (fun call ctx ->
                    let lb = parseInt call 0 22
                    let mult = parseFloat call 1 3.0
                    let ops = resolveOperands ctx call HighLowClose
                    let decs = ops |> Array.map floatsToDecimals
                    let bars = toBars ctx.Frame decs 3
                    let res = Chandelier.ToChandelier(bars, lb, mult, Direction.Long)
                    projectFloat res (fun (r: ChandelierResult) -> toFloatN r.ChandelierExit))
                (fun call -> parseInt call 0 22)
                Map.empty

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
                    let decs = ops |> Array.map floatsToDecimals
                    let bars = toBars ctx.Frame decs 2
                    let res = ParabolicSar.ToParabolicSar(bars, step, maxA)
                    projectFloat res (fun (r: ParabolicSarResult) -> toFloatN r.Sar))
                (fun _ -> 0)
                Map.empty

        let volatilityStopSpec =
            mk
                "volatility-stop"
                [ "volatilitystop" ]
                HighLowClose
                FloatOutput
                (fun call ctx ->
                    let lb = parseInt call 0 7
                    let mult = parseFloat call 1 3.0
                    let ops = resolveOperands ctx call HighLowClose
                    let decs = ops |> Array.map floatsToDecimals
                    let bars = toBars ctx.Frame decs 3
                    let res = VolatilityStop.ToVolatilityStop(bars, lb, mult)
                    projectFloat res (fun (r: VolatilityStopResult) -> toFloatN r.Sar))
                (fun call -> parseInt call 0 7)
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
                    let decs = ops |> Array.map floatsToDecimals
                    let bars = toBars ctx.Frame decs 4
                    let res = Doji.ToDoji(bars, pct / 100.0)
                    projectBool res (fun (r: CandleResult) -> r.Match <> Match.None))
                (fun _ -> 0)
                Map.empty

        let marubozuSpec =
            mk
                "marubozu"
                []
                OpenHighLowClose
                BoolOutput
                (fun call ctx ->
                    let pct = parseFloat call 0 95.0
                    let ops = resolveOperands ctx call OpenHighLowClose
                    let decs = ops |> Array.map floatsToDecimals
                    let bars = toBars ctx.Frame decs 4
                    let res = Marubozu.ToMarubozu(bars, pct)
                    projectBool res (fun (r: CandleResult) -> r.Match <> Match.None))
                (fun _ -> 0)
                Map.empty

        let pivotsSpec =
            mk
                "pivots"
                []
                HighLowClose
                BoolOutput
                (fun call ctx ->
                    let left = parseInt call 0 2
                    let right = parseInt call 1 2
                    let ops = resolveOperands ctx call HighLowClose
                    let decs = ops |> Array.map floatsToDecimals
                    let bars = toBars ctx.Frame decs 3
                    let res = Pivots.ToPivots(bars, left, right)
                    projectBool res (fun (r: PivotsResult) -> r.HighPoint.HasValue || r.LowPoint.HasValue))
                (fun _ -> 2)
                Map.empty

        let fractalSpec =
            mk
                "fractal"
                []
                HighLow
                BoolOutput
                (fun call ctx ->
                    let win = parseInt call 0 2
                    let ops = resolveOperands ctx call HighLow
                    let decs = ops |> Array.map floatsToDecimals
                    let bars = toBars ctx.Frame decs 2
                    let res = Fractal.ToFractal(bars, win)
                    projectBool res (fun (r: FractalResult) -> r.FractalBull.HasValue || r.FractalBear.HasValue))
                (fun _ -> 2)
                Map.empty

        let adlSpec =
            mk
                "adl"
                []
                HighLowCloseVolume
                FloatOutput
                (fun call ctx ->
                    let ops = resolveOperands ctx call HighLowCloseVolume
                    let decs = ops |> Array.map floatsToDecimals
                    let bars = toBars ctx.Frame decs 4
                    let res = Adl.ToAdl(bars)
                    projectFloat res (fun (r: AdlResult) -> r.Adl))
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
                    let decs = ops |> Array.map floatsToDecimals
                    let bars = toBars ctx.Frame decs 4
                    let res = Cmf.ToCmf(bars, p)
                    projectFloat res (fun (r: CmfResult) -> toFloatN r.Cmf))
                (fun call -> parseInt call 0 20)
                Map.empty

        let chaikinSpec =
            mk
                "chaikin-osc"
                [ "chaikinosc" ]
                HighLowCloseVolume
                FloatOutput
                (fun call ctx ->
                    let fast = parseInt call 0 3
                    let slow = parseInt call 1 10
                    let ops = resolveOperands ctx call HighLowCloseVolume
                    let decs = ops |> Array.map floatsToDecimals
                    let bars = toBars ctx.Frame decs 4
                    let res = ChaikinOsc.ToChaikinOsc(bars, fast, slow)
                    projectFloat res (fun (r: ChaikinOscResult) -> toFloatN r.Value))
                (fun _ -> 10)
                Map.empty

        let forceIndexSpec =
            mk
                "force-index"
                [ "forceindex" ]
                CloseVolume
                FloatOutput
                (fun call ctx ->
                    let p = parseInt call 0 2
                    let ops = resolveOperands ctx call CloseVolume
                    let closes = ops.[0] |> floatsToDecimals
                    let vols = ops.[1] |> floatsToDecimals

                    let bars =
                        toBarsOHLCV
                            ctx.Frame
                            (Array.create ctx.Frame.RowCount 0m)
                            (Array.create ctx.Frame.RowCount 0m)
                            (Array.create ctx.Frame.RowCount 0m)
                            closes
                            vols

                    let res = ForceIndex.ToForceIndex(bars, p)
                    projectFloat res (fun (r: ForceIndexResult) -> toFloatN r.ForceIndex))
                (fun call -> parseInt call 0 2)
                Map.empty

        let kvoSpec =
            mk
                "kvo"
                []
                HighLowCloseVolume
                FloatOutput
                (fun call ctx ->
                    let fast = parseInt call 0 34
                    let slow = parseInt call 1 55
                    let sig_ = parseInt call 2 13
                    let ops = resolveOperands ctx call HighLowCloseVolume
                    let decs = ops |> Array.map floatsToDecimals
                    let bars = toBars ctx.Frame decs 4
                    let res = Kvo.ToKvo(bars, fast, slow, sig_)

                    match call.Sub with
                    | Some "signal" -> projectFloat res (fun (r: KvoResult) -> toFloatN r.Signal)
                    | _ -> projectFloat res (fun (r: KvoResult) -> toFloatN r.Oscillator))
                (fun _ -> 55)
                (Map.ofList [ "signal", leaf "signal" HighLowCloseVolume (fun _ _ -> failwith "") (fun _ -> 0) ])

        let mfiSpec =
            mk
                "mfi"
                []
                HighLowCloseVolume
                FloatOutput
                (fun call ctx ->
                    let p = parseInt call 0 14
                    let ops = resolveOperands ctx call HighLowCloseVolume
                    let decs = ops |> Array.map floatsToDecimals
                    let bars = toBars ctx.Frame decs 4
                    let res = Mfi.ToMfi(bars, p)
                    projectFloat res (fun (r: MfiResult) -> toFloatN r.Mfi))
                (fun call -> parseInt call 0 14)
                Map.empty

        let obvSpec =
            mk
                "obv"
                []
                CloseVolume
                FloatOutput
                (fun call ctx ->
                    let ops = resolveOperands ctx call CloseVolume
                    let closes = ops.[0] |> floatsToDecimals
                    let vols = ops.[1] |> floatsToDecimals

                    let bars =
                        toBarsOHLCV
                            ctx.Frame
                            (Array.create ctx.Frame.RowCount 0m)
                            (Array.create ctx.Frame.RowCount 0m)
                            (Array.create ctx.Frame.RowCount 0m)
                            closes
                            vols

                    let res = Obv.ToObv(bars)
                    projectFloat res (fun (r: ObvResult) -> r.Obv))
                (fun _ -> 0)
                Map.empty

        let pvoSpec =
            mk
                "pvo"
                []
                CloseVolume
                FloatOutput
                (fun call ctx ->
                    let fast = parseInt call 0 12
                    let slow = parseInt call 1 26
                    let sig_ = parseInt call 2 9
                    let ops = resolveOperands ctx call CloseVolume
                    let closes = ops.[0] |> floatsToDecimals
                    let vols = ops.[1] |> floatsToDecimals

                    let bars =
                        toBarsOHLCV
                            ctx.Frame
                            (Array.create ctx.Frame.RowCount 0m)
                            (Array.create ctx.Frame.RowCount 0m)
                            (Array.create ctx.Frame.RowCount 0m)
                            closes
                            vols

                    let res = Pvo.ToPvo(bars, fast, slow, sig_)

                    match call.Sub with
                    | Some "signal" -> projectFloat res (fun (r: PvoResult) -> toFloatN r.Signal)
                    | _ -> projectFloat res (fun (r: PvoResult) -> toFloatN r.Pvo))
                (fun _ -> 26)
                (Map.ofList [ "signal", leaf "signal" CloseVolume (fun _ _ -> failwith "") (fun _ -> 0) ])

        let vwapSpec =
            mk
                "vwap"
                []
                HighLowCloseVolume
                FloatOutput
                (fun call ctx ->
                    let ops = resolveOperands ctx call HighLowCloseVolume
                    let decs = ops |> Array.map floatsToDecimals
                    let bars = toBars ctx.Frame decs 4
                    let res = Vwap.ToVwap(bars)
                    projectFloat res (fun (r: VwapResult) -> toFloatN r.Vwap))
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
                    let closes = ops.[0] |> floatsToDecimals
                    let vols = ops.[1] |> floatsToDecimals

                    let bars =
                        toBarsOHLCV
                            ctx.Frame
                            (Array.create ctx.Frame.RowCount 0m)
                            (Array.create ctx.Frame.RowCount 0m)
                            (Array.create ctx.Frame.RowCount 0m)
                            closes
                            vols

                    let res = Vwma.ToVwma(bars, p)
                    projectFloat res (fun (r: VwmaResult) -> toFloatN r.Vwma))
                (fun call -> parseInt call 0 20)
                Map.empty

        let barPartSpec =
            mk
                "bar-part"
                [ "barpart" ]
                HighLowClose
                FloatOutput
                (fun call ctx ->
                    let part =
                        match arg call 0 |> fun s -> s.ToLowerInvariant() with
                        | "hl2" -> CandlePart.HL2
                        | "hlc3" -> CandlePart.HLC3
                        | "oc2" -> CandlePart.OC2
                        | "ohl3" -> CandlePart.OHL3
                        | "ohlc4" -> CandlePart.OHLC4
                        | "" -> CandlePart.Close
                        | s -> raise (DirectiveValueError $"unknown bar-part '{s}'")

                    let ops = resolveOperands ctx call HighLowClose
                    let decs = ops |> Array.map floatsToDecimals
                    let bars = toBars ctx.Frame decs 3
                    let res = BarParts.ToBarPart(bars, part)
                    projectFloat res (fun (r: TimeValue) -> r.Value))
                (fun _ -> 0)
                Map.empty

        let heikinSpec =
            mk
                "heikin-ashi"
                [ "heikinashi" ]
                OpenHighLowClose
                FloatOutput
                (fun call ctx ->
                    let ops = resolveOperands ctx call OpenHighLowClose
                    let decs = ops |> Array.map floatsToDecimals
                    let bars = toBars ctx.Frame decs 4
                    let res = HeikinAshi.ToHeikinAshi(bars)

                    match call.Sub with
                    | Some "open" -> projectFloat res (fun (r: HeikinAshiResult) -> float r.Open)
                    | Some "high" -> projectFloat res (fun (r: HeikinAshiResult) -> float r.High)
                    | Some "low" -> projectFloat res (fun (r: HeikinAshiResult) -> float r.Low)
                    | Some "close" -> projectFloat res (fun (r: HeikinAshiResult) -> float r.Close)
                    | _ -> projectFloat res (fun (r: HeikinAshiResult) -> float r.Close))
                (fun _ -> 0)
                (Map.ofList
                    [ "open", leaf "open" OpenHighLowClose (fun _ _ -> failwith "") (fun _ -> 0)
                      "high", leaf "high" OpenHighLowClose (fun _ _ -> failwith "") (fun _ -> 0)
                      "low", leaf "low" OpenHighLowClose (fun _ _ -> failwith "") (fun _ -> 0)
                      "close", leaf "close" OpenHighLowClose (fun _ _ -> failwith "") (fun _ -> 0) ])

        let renkoSpec =
            mk
                "renko"
                []
                HighLowCloseVolume
                FloatOutput
                (fun call ctx ->
                    let size = parseDecimal call 0 1m
                    let s = arg call 0

                    if s = "" then
                        raise (DirectiveValueError "indicator 'renko' requires argument 1 (brick size)")

                    let ops = resolveOperands ctx call HighLowCloseVolume
                    let decs = ops |> Array.map floatsToDecimals
                    let bars = toBars ctx.Frame decs 4
                    let res = Renko.ToRenko(bars, size)
                    projectFloat res (fun (r: RenkoResult) -> float r.Close))
                (fun _ -> 0)
                Map.empty

        let zigzagSpec =
            mk
                "zig-zag"
                [ "zigzag" ]
                HighLowClose
                FloatOutput
                (fun call ctx ->
                    let pct = parseDecimal call 0 5m
                    let ops = resolveOperands ctx call HighLowClose
                    let decs = ops |> Array.map floatsToDecimals
                    let bars = toBars ctx.Frame decs 3
                    let res = ZigZag.ToZigZag(bars, EndType.Close, pct)
                    projectFloat res (fun (r: ZigZagResult) -> toFloatDecimal r.ZigZag))
                (fun _ -> 0)
                Map.empty

        let betaSpec =
            mk
                "beta"
                []
                CloseOnly
                FloatOutput
                (fun call ctx ->
                    let lb = parseInt call 0 20
                    let ops = resolveOperands ctx call CloseOnly
                    // Beta needs a comparison series: use the same frame's close duplicated for now; proper two-series is via second operand if provided.
                    let src = toReusable ctx.Frame ops.[0]

                    let mrkt =
                        if call.Series.Length = 2 then
                            toReusable ctx.Frame (resolveFloatSeries ctx call.Series.[1])
                        else
                            src

                    let res = Beta.ToBeta(src, mrkt, lb)
                    projectFloat res (fun (r: BetaResult) -> toFloatN r.Beta))
                (fun call -> parseInt call 0 20)
                Map.empty

        let corrSpec =
            mk
                "correlation"
                []
                CloseOnly
                FloatOutput
                (fun call ctx ->
                    let p = parseInt call 0 20

                    let ops =
                        if call.Series.Length = 2 then
                            [| resolveFloatSeries ctx call.Series.[0]
                               resolveFloatSeries ctx call.Series.[1] |]
                        else
                            let baseArr = resolveOperands ctx { call with Series = [] } CloseOnly
                            [| baseArr.[0]; baseArr.[0] |]

                    let a = toReusable ctx.Frame ops.[0]
                    let b = toReusable ctx.Frame ops.[1]
                    let res = Correlation.ToCorrelation(a, b, p)
                    projectFloat res (fun (r: CorrResult) -> toFloatN r.Correlation))
                (fun call -> parseInt call 0 20)
                Map.empty

        let prsSpec =
            mk
                "prs"
                []
                CloseOnly
                FloatOutput
                (fun call ctx ->
                    let ops =
                        if call.Series.Length = 2 then
                            [| resolveFloatSeries ctx call.Series.[0]
                               resolveFloatSeries ctx call.Series.[1] |]
                        else
                            let baseArr = resolveOperands ctx { call with Series = [] } CloseOnly
                            [| baseArr.[0]; baseArr.[0] |]

                    let a = toReusable ctx.Frame ops.[0]
                    let b = toReusable ctx.Frame ops.[1]
                    let s = arg call 0

                    let res =
                        if s = "" then
                            Prs.ToPrs(a, b)
                        else
                            Prs.ToPrs(a, b, parseInt call 0 14)

                    projectFloat res (fun (r: PrsResult) -> toFloatN r.Prs))
                (fun call -> let s = arg call 0 in if s = "" then 0 else parseInt call 0 14)
                Map.empty

        let pivotPointsSpec =
            mk
                "pivot-points"
                [ "pivotpoints" ]
                HighLowClose
                FloatOutput
                (fun call ctx ->
                    let ops = resolveOperands ctx call HighLowClose
                    let decs = ops |> Array.map floatsToDecimals
                    let bars = toBars ctx.Frame decs 3
                    let res = PivotPoints.ToPivotPoints(bars)

                    match call.Sub with
                    | Some "pp" -> projectFloat res (fun (r: PivotPointsResult) -> toFloatN r.PP)
                    | Some "r1" -> projectFloat res (fun (r: PivotPointsResult) -> toFloatN r.R1)
                    | Some "s1" -> projectFloat res (fun (r: PivotPointsResult) -> toFloatN r.S1)
                    | Some "r2" -> projectFloat res (fun (r: PivotPointsResult) -> toFloatN r.R2)
                    | Some "s2" -> projectFloat res (fun (r: PivotPointsResult) -> toFloatN r.S2)
                    | _ -> projectFloat res (fun (r: PivotPointsResult) -> toFloatN r.PP))
                (fun _ -> 0)
                (Map.ofList
                    [ "pp", leaf "pp" HighLowClose (fun _ _ -> failwith "") (fun _ -> 0)
                      "r1", leaf "r1" HighLowClose (fun _ _ -> failwith "") (fun _ -> 0)
                      "s1", leaf "s1" HighLowClose (fun _ _ -> failwith "") (fun _ -> 0) ])

        let rollingPivotsSpec =
            mk
                "rolling-pivots"
                [ "rollingpivots" ]
                HighLowClose
                FloatOutput
                (fun call ctx ->
                    let win = parseInt call 0 20
                    let off = parseInt call 1 0
                    let ops = resolveOperands ctx call HighLowClose
                    let decs = ops |> Array.map floatsToDecimals
                    let bars = toBars ctx.Frame decs 3
                    let res = RollingPivots.ToRollingPivots(bars, win, off)

                    match call.Sub with
                    | Some "pp" -> projectFloat res (fun (r: RollingPivotsResult) -> toFloatN r.PP)
                    | _ -> projectFloat res (fun (r: RollingPivotsResult) -> toFloatN r.PP))
                (fun call -> parseInt call 0 20)
                (Map.ofList [ "pp", leaf "pp" HighLowClose (fun _ _ -> failwith "") (fun _ -> 0) ])

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
              hmaSpec
              demaSpec
              temaSpec
              smmaSpec
              epmaSpec
              almaSpec
              kamaSpec
              mamaSpec
              t3Spec
              mgDynSpec
              rsiSpec
              cmoSpec
              trixSpec
              rocSpec
              rocWbSpec
              stcSpec
              pmoSpec
              macdSpec
              tsiSpec
              connorsSpec
              stochRsiSpec
              hurstSpec
              ulcerSpec
              fisherSpec
              stdDevSpec
              slopeSpec
              htSpec
              dpoSpec
              atrSpec
              natrSpec
              trSpec
              adxSpec
              aroonSpec
              cciSpec
              chopSpec
              bopSpec
              ultimateSpec
              williamsSpec
              stochSpec
              kdjSpec
              smiSpec
              bollSpec
              donchianSpec
              keltnerSpec
              starcSpec
              fcbSpec
              maEnvSpec
              stdDevChSpec
              ichimokuSpec
              superTrendSpec
              vortexSpec
              alligatorSpec
              elderRaySpec
              gatorSpec
              awesomeSpec
              atrStopSpec
              chandelierSpec
              parabolicSpec
              volatilityStopSpec
              dojiSpec
              marubozuSpec
              pivotsSpec
              fractalSpec
              adlSpec
              cmfSpec
              chaikinSpec
              forceIndexSpec
              kvoSpec
              mfiSpec
              obvSpec
              pvoSpec
              vwapSpec
              vwmaSpec
              barPartSpec
              heikinSpec
              renkoSpec
              zigzagSpec
              betaSpec
              corrSpec
              prsSpec
              pivotPointsSpec
              rollingPivotsSpec
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
