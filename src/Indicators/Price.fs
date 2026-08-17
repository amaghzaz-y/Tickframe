namespace Tickframe.Indicators

open System

module Price =

    let barPart (part: string) (high: float[]) (low: float[]) (close: float[]) : float[] =
        let n = close.Length

        match part.ToLowerInvariant() with
        | "hl2" -> Array.init n (fun i -> (high.[i] + low.[i]) / 2.0)
        | "hlc3" -> Array.init n (fun i -> (high.[i] + low.[i] + close.[i]) / 3.0)
        | "ohlc4" ->
            Array.init n (fun i ->
                let o = close.[i]
                (o + high.[i] + low.[i] + close.[i]) / 4.0)
        | _ -> Array.copy close

    let heikinAshi
        (opens: float[])
        (highs: float[])
        (lows: float[])
        (closes: float[])
        : float[] * float[] * float[] * float[] =
        let n = closes.Length
        let haOpen = Array.create n Double.NaN
        let haHigh = Array.create n Double.NaN
        let haLow = Array.create n Double.NaN
        let haClose = Array.create n Double.NaN

        for i in 0 .. n - 1 do
            let o = opens.[i]
            let h = highs.[i]
            let l = lows.[i]
            let c = closes.[i]

            if Double.IsNaN o || Double.IsNaN h || Double.IsNaN l || Double.IsNaN c then
                haOpen.[i] <- Double.NaN
                haHigh.[i] <- Double.NaN
                haLow.[i] <- Double.NaN
                haClose.[i] <- Double.NaN
            else
                let hc = (o + h + l + c) / 4.0
                haClose.[i] <- hc

                let ho =
                    if i = 0 then
                        (o + c) / 2.0
                    else
                        let po = haOpen.[i - 1]
                        let pc = haClose.[i - 1]

                        if Double.IsNaN po || Double.IsNaN pc then
                            (o + c) / 2.0
                        else
                            (po + pc) / 2.0

                haOpen.[i] <- ho
                haHigh.[i] <- max h (max ho hc)
                haLow.[i] <- min l (min ho hc)

        haOpen, haHigh, haLow, haClose

    let doji (thresholdPct: float) (opens: float[]) (highs: float[]) (lows: float[]) (closes: float[]) : bool[] =
        let n = closes.Length
        let out = Array.create n false

        for i in 0 .. n - 1 do
            let o = opens.[i]
            let h = highs.[i]
            let l = lows.[i]
            let c = closes.[i]

            if not (Double.IsNaN o || Double.IsNaN h || Double.IsNaN l || Double.IsNaN c) then
                let range = h - l

                if range <> 0.0 then
                    let body = abs (c - o)
                    let ratio = body / range
                    out.[i] <- ratio <= thresholdPct / 100.0

        out
