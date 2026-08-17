namespace Tickframe.Indicators

open System

module Common =

    let sma (period: int) (src: float[]) : float[] =
        let n = src.Length
        let out = Array.create n Double.NaN

        if period <= 0 || n = 0 then
            out
        else if period = 1 then
            for i in 0 .. n - 1 do
                out.[i] <- src.[i]

            out
        else
            let mutable sum = 0.0
            let mutable nanCount = 0

            for i in 0 .. n - 1 do
                let v = src.[i]

                if Double.IsNaN v then
                    nanCount <- nanCount + 1
                else
                    sum <- sum + v

                if i >= period then
                    let old = src.[i - period]

                    if Double.IsNaN old then
                        nanCount <- nanCount - 1
                    else
                        sum <- sum - old

                if i >= period - 1 then
                    if nanCount = 0 then
                        out.[i] <- sum / float period

            out

    let ema (period: int) (src: float[]) : float[] =
        let n = src.Length
        let out = Array.create n Double.NaN

        if period <= 0 || n = 0 then
            out
        elif period = 1 then
            for i in 0 .. n - 1 do
                out.[i] <- src.[i]

            out
        else
            let seed = sma period src
            let k = 2.0 / float (period + 1)
            let mutable prev = Double.NaN
            let mutable seeded = false

            for i in 0 .. n - 1 do
                if not seeded then
                    if not (Double.IsNaN seed.[i]) then
                        prev <- seed.[i]
                        out.[i] <- prev
                        seeded <- true
                else
                    let v = src.[i]

                    if Double.IsNaN v || Double.IsNaN prev then
                        prev <- Double.NaN
                        out.[i] <- Double.NaN
                    else
                        prev <- v * k + prev * (1.0 - k)
                        out.[i] <- prev

            out

    let wma (period: int) (src: float[]) : float[] =
        let n = src.Length
        let out = Array.create n Double.NaN

        if period <= 0 || n = 0 then
            out
        elif period = 1 then
            for i in 0 .. n - 1 do
                out.[i] <- src.[i]

            out
        else
            let denom = float (period * (period + 1) / 2)

            for i in period - 1 .. n - 1 do
                let mutable sum = 0.0
                let mutable hasNaN = false

                for k in 0 .. period - 1 do
                    let v = src.[i - period + 1 + k]

                    if Double.IsNaN v then
                        hasNaN <- true
                    else
                        sum <- sum + v * float (k + 1)

                if not hasNaN then
                    out.[i] <- sum / denom

            out

    let rma (period: int) (src: float[]) : float[] =
        let n = src.Length
        let out = Array.create n Double.NaN

        if period <= 0 || n = 0 then
            out
        elif period = 1 then
            for i in 0 .. n - 1 do
                out.[i] <- src.[i]

            out
        else
            let seed = sma period src
            let mutable prev = Double.NaN
            let mutable seeded = false

            for i in 0 .. n - 1 do
                if not seeded then
                    if not (Double.IsNaN seed.[i]) then
                        prev <- seed.[i]
                        out.[i] <- prev
                        seeded <- true
                else
                    let v = src.[i]

                    if Double.IsNaN v || Double.IsNaN prev then
                        prev <- Double.NaN
                        out.[i] <- Double.NaN
                    else
                        prev <- (prev * float (period - 1) + v) / float period
                        out.[i] <- prev

            out

    let rollingMeanAndStd (period: int) (src: float[]) : float[] * float[] =
        let n = src.Length
        let means = Array.create n Double.NaN
        let stds = Array.create n Double.NaN

        if period > 0 && n > 0 then
            for i in period - 1 .. n - 1 do
                let mutable hasNaN = false
                let mutable sum = 0.0

                for k in 0 .. period - 1 do
                    let v = src.[i - period + 1 + k]

                    if Double.IsNaN v then hasNaN <- true else sum <- sum + v

                if not hasNaN then
                    let mean = sum / float period
                    means.[i] <- mean
                    let mutable sq = 0.0

                    for k in 0 .. period - 1 do
                        let d = src.[i - period + 1 + k] - mean
                        sq <- sq + d * d

                    stds.[i] <- sqrt (sq / float period)

        means, stds

    let trueRange (high: float[]) (low: float[]) (close: float[]) : float[] =
        let n = high.Length
        let out = Array.create n Double.NaN

        for i in 0 .. n - 1 do
            let h = high.[i]
            let l = low.[i]
            let c = close.[i]

            if not (Double.IsNaN h || Double.IsNaN l || Double.IsNaN c) then
                if i = 0 then
                    out.[i] <- h - l
                else
                    let pc = close.[i - 1]

                    if Double.IsNaN pc then
                        out.[i] <- Double.NaN
                    else
                        let a = h - l
                        let b = abs (h - pc)
                        let cc = abs (l - pc)
                        out.[i] <- max a (max b cc)

        out

    let atrFromTR (period: int) (tr: float[]) : float[] = rma period tr

    let highest (period: int) (src: float[]) : float[] =
        let n = src.Length
        let out = Array.create n Double.NaN

        if period > 0 && n > 0 then
            for i in period - 1 .. n - 1 do
                let mutable m = Double.NegativeInfinity
                let mutable hasNaN = false

                for k in 0 .. period - 1 do
                    let v = src.[i - period + 1 + k]

                    if Double.IsNaN v then
                        hasNaN <- true
                    elif v > m then
                        m <- v

                if not hasNaN then
                    out.[i] <- m

        out

    let lowest (period: int) (src: float[]) : float[] =
        let n = src.Length
        let out = Array.create n Double.NaN

        if period > 0 && n > 0 then
            for i in period - 1 .. n - 1 do
                let mutable m = Double.PositiveInfinity
                let mutable hasNaN = false

                for k in 0 .. period - 1 do
                    let v = src.[i - period + 1 + k]

                    if Double.IsNaN v then
                        hasNaN <- true
                    elif v < m then
                        m <- v

                if not hasNaN then
                    out.[i] <- m

        out
