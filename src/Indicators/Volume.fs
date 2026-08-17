namespace Tickframe.Indicators

open System

module Volume =

    let obv (close: float[]) (volume: float[]) : float[] =
        let n = close.Length
        let out = Array.create n Double.NaN

        if n > 0 then
            out.[0] <- volume.[0]

            for i in 1 .. n - 1 do
                let prev = out.[i - 1]
                let c = close.[i]
                let pc = close.[i - 1]
                let v = volume.[i]

                if Double.IsNaN prev || Double.IsNaN c || Double.IsNaN pc || Double.IsNaN v then
                    out.[i] <- Double.NaN
                elif c > pc then
                    out.[i] <- prev + v
                elif c < pc then
                    out.[i] <- prev - v
                else
                    out.[i] <- prev

        out

    let adl (high: float[]) (low: float[]) (close: float[]) (volume: float[]) : float[] =
        let n = close.Length
        let out = Array.create n Double.NaN
        let mutable prev = 0.0
        let mutable hasPrev = false

        for i in 0 .. n - 1 do
            let h = high.[i]
            let l = low.[i]
            let c = close.[i]
            let v = volume.[i]

            if Double.IsNaN h || Double.IsNaN l || Double.IsNaN c || Double.IsNaN v then
                out.[i] <- Double.NaN
            else
                let range = h - l
                let mfm = if range = 0.0 then 0.0 else ((c - l) - (h - c)) / range
                let mfv = mfm * v

                if not hasPrev && i = 0 then
                    prev <- mfv
                    hasPrev <- true
                    out.[i] <- prev
                elif hasPrev && not (Double.IsNaN prev) then
                    prev <- prev + mfv
                    out.[i] <- prev
                else
                    out.[i] <- Double.NaN

        out

    let cmf (period: int) (high: float[]) (low: float[]) (close: float[]) (volume: float[]) : float[] =
        let n = close.Length
        let out = Array.create n Double.NaN
        let mfv = Array.create n Double.NaN

        for i in 0 .. n - 1 do
            let h = high.[i]
            let l = low.[i]
            let c = close.[i]
            let v = volume.[i]

            if not (Double.IsNaN h || Double.IsNaN l || Double.IsNaN c || Double.IsNaN v) then
                let range = h - l
                let mfm = if range = 0.0 then 0.0 else ((c - l) - (h - c)) / range
                mfv.[i] <- mfm * v

        if period > 0 then
            for i in period - 1 .. n - 1 do
                let mutable hasNaN = false
                let mutable sumMfv = 0.0
                let mutable sumVol = 0.0

                for k in 0 .. period - 1 do
                    let idx = i - period + 1 + k

                    if Double.IsNaN mfv.[idx] || Double.IsNaN volume.[idx] then
                        hasNaN <- true
                    else
                        sumMfv <- sumMfv + mfv.[idx]
                        sumVol <- sumVol + volume.[idx]

                if not hasNaN then
                    if sumVol = 0.0 then
                        out.[i] <- 0.0
                    else
                        out.[i] <- sumMfv / sumVol

        out

    let mfi (period: int) (high: float[]) (low: float[]) (close: float[]) (volume: float[]) : float[] =
        let n = close.Length
        let out = Array.create n Double.NaN

        if period > 0 && n > period then
            let tp = Array.init n (fun i -> (high.[i] + low.[i] + close.[i]) / 3.0)
            let rmf = Array.init n (fun i -> tp.[i] * volume.[i])

            for i in period .. n - 1 do
                let mutable hasNaN = false

                for k in i - period .. i do
                    if Double.IsNaN tp.[k] || Double.IsNaN volume.[k] || Double.IsNaN rmf.[k] then
                        hasNaN <- true

                if not hasNaN then
                    let mutable pos = 0.0
                    let mutable neg = 0.0

                    for k in i - period + 1 .. i do
                        if tp.[k] > tp.[k - 1] then
                            pos <- pos + rmf.[k]
                        elif tp.[k] < tp.[k - 1] then
                            neg <- neg + rmf.[k]

                    if neg = 0.0 then
                        out.[i] <- 100.0
                    else
                        let mr = pos / neg
                        out.[i] <- 100.0 - 100.0 / (1.0 + mr)

        out

    let vwap (high: float[]) (low: float[]) (close: float[]) (volume: float[]) : float[] =
        let n = close.Length
        let out = Array.create n Double.NaN
        let mutable cumPV = 0.0
        let mutable cumVol = 0.0

        for i in 0 .. n - 1 do
            let h = high.[i]
            let l = low.[i]
            let c = close.[i]
            let v = volume.[i]

            if Double.IsNaN h || Double.IsNaN l || Double.IsNaN c || Double.IsNaN v then
                out.[i] <- Double.NaN
            else
                let tp = (h + l + c) / 3.0
                cumPV <- cumPV + tp * v
                cumVol <- cumVol + v

                if cumVol = 0.0 then
                    out.[i] <- Double.NaN
                else
                    out.[i] <- cumPV / cumVol

        out

    let vwma (period: int) (close: float[]) (volume: float[]) : float[] =
        let n = close.Length
        let out = Array.create n Double.NaN

        if period > 0 then
            for i in period - 1 .. n - 1 do
                let mutable hasNaN = false
                let mutable sumPV = 0.0
                let mutable sumVol = 0.0

                for k in 0 .. period - 1 do
                    let idx = i - period + 1 + k
                    let c = close.[idx]
                    let v = volume.[idx]

                    if Double.IsNaN c || Double.IsNaN v then
                        hasNaN <- true
                    else
                        sumPV <- sumPV + c * v
                        sumVol <- sumVol + v

                if not hasNaN then
                    if sumVol = 0.0 then
                        out.[i] <- Double.NaN
                    else
                        out.[i] <- sumPV / sumVol

        out
