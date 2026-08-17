namespace Tickframe.Indicators

open System

module Momentum =

    let rsi (period: int) (src: float[]) : float[] =
        let n = src.Length
        let out = Array.create n Double.NaN

        if period > 0 && n > period then
            let gains = Array.create n 0.0
            let losses = Array.create n 0.0

            for i in 1 .. n - 1 do
                let a = src.[i]
                let b = src.[i - 1]

                if not (Double.IsNaN a || Double.IsNaN b) then
                    let d = a - b

                    if d > 0.0 then
                        gains.[i] <- d
                    elif d < 0.0 then
                        losses.[i] <- -d

            let avgGain = Array.create n Double.NaN
            let avgLoss = Array.create n Double.NaN
            let mutable sumG = 0.0
            let mutable sumL = 0.0

            for i in 1..period do
                sumG <- sumG + gains.[i]
                sumL <- sumL + losses.[i]

            avgGain.[period] <- sumG / float period
            avgLoss.[period] <- sumL / float period

            for i in period + 1 .. n - 1 do
                let prevG = avgGain.[i - 1]
                let prevL = avgLoss.[i - 1]

                if Double.IsNaN prevG || Double.IsNaN prevL then
                    avgGain.[i] <- Double.NaN
                    avgLoss.[i] <- Double.NaN
                else
                    avgGain.[i] <- (prevG * float (period - 1) + gains.[i]) / float period
                    avgLoss.[i] <- (prevL * float (period - 1) + losses.[i]) / float period

            for i in period .. n - 1 do
                let ag = avgGain.[i]
                let al = avgLoss.[i]

                if not (Double.IsNaN ag || Double.IsNaN al) then
                    if al = 0.0 then
                        out.[i] <- 100.0
                    else
                        let rs = ag / al
                        out.[i] <- 100.0 - 100.0 / (1.0 + rs)

        out

    let macdLine (fast: int) (slow: int) (src: float[]) : float[] =
        let ef = Common.ema fast src
        let es = Common.ema slow src
        let n = src.Length
        let out = Array.create n Double.NaN

        for i in 0 .. n - 1 do
            let a = ef.[i]
            let b = es.[i]

            if not (Double.IsNaN a || Double.IsNaN b) then
                out.[i] <- a - b

        out

    let macdSignal (signal: int) (line: float[]) : float[] = Common.ema signal line

    let macdHistogram (line: float[]) (signal: float[]) : float[] =
        let n = line.Length
        let out = Array.create n Double.NaN

        for i in 0 .. n - 1 do
            let a = line.[i]
            let b = signal.[i]

            if not (Double.IsNaN a || Double.IsNaN b) then
                out.[i] <- a - b

        out

    let stochK (period: int) (high: float[]) (low: float[]) (close: float[]) : float[] =
        let n = close.Length
        let out = Array.create n Double.NaN
        let hh = Common.highest period high
        let ll = Common.lowest period low

        for i in 0 .. n - 1 do
            let h = hh.[i]
            let l = ll.[i]
            let c = close.[i]

            if not (Double.IsNaN h || Double.IsNaN l || Double.IsNaN c) then
                let denom = h - l

                if denom = 0.0 then
                    out.[i] <- 0.0
                else
                    out.[i] <- 100.0 * (c - l) / denom

        out

    let stochD (smooth: int) (k: float[]) : float[] = Common.sma smooth k

    let cci (period: int) (high: float[]) (low: float[]) (close: float[]) : float[] =
        let n = close.Length
        let out = Array.create n Double.NaN

        if period > 0 && n >= period then
            let tp = Array.init n (fun i -> (high.[i] + low.[i] + close.[i]) / 3.0)
            let ma = Common.sma period tp

            for i in period - 1 .. n - 1 do
                let m = ma.[i]

                if not (Double.IsNaN m) then
                    let mutable hasNaN = false
                    let mutable sumDev = 0.0

                    for k in 0 .. period - 1 do
                        let v = tp.[i - period + 1 + k]

                        if Double.IsNaN v then
                            hasNaN <- true
                        else
                            sumDev <- sumDev + abs (v - m)

                    if not hasNaN then
                        let md = sumDev / float period

                        if md = 0.0 then
                            out.[i] <- 0.0
                        else
                            out.[i] <- (tp.[i] - m) / (0.015 * md)

        out

    let roc (period: int) (src: float[]) : float[] =
        let n = src.Length
        let out = Array.create n Double.NaN

        if period > 0 then
            for i in period .. n - 1 do
                let cur = src.[i]
                let prev = src.[i - period]

                if not (Double.IsNaN cur || Double.IsNaN prev) then
                    if prev = 0.0 then
                        out.[i] <- Double.NaN
                    else
                        out.[i] <- 100.0 * (cur - prev) / prev

        out

    let stochRsiK (rsiPeriod: int) (stochPeriod: int) (src: float[]) : float[] =
        let rsiVals = rsi rsiPeriod src
        let n = src.Length
        let out = Array.create n Double.NaN
        let hh = Common.highest stochPeriod rsiVals
        let ll = Common.lowest stochPeriod rsiVals

        for i in 0 .. n - 1 do
            let r = rsiVals.[i]
            let h = hh.[i]
            let l = ll.[i]

            if not (Double.IsNaN r || Double.IsNaN h || Double.IsNaN l) then
                let denom = h - l

                if denom = 0.0 then
                    out.[i] <- 0.0
                else
                    out.[i] <- 100.0 * (r - l) / denom

        out
