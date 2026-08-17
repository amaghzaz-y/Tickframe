namespace Tickframe.Indicators

open System

module Volatility =

    let bollinger (period: int) (mult: float) (src: float[]) : float[] * float[] * float[] =
        let ma, std = Common.rollingMeanAndStd period src
        let n = src.Length
        let upper = Array.create n Double.NaN
        let lower = Array.create n Double.NaN
        let middle = Array.create n Double.NaN

        for i in 0 .. n - 1 do
            let m = ma.[i]
            let s = std.[i]

            if not (Double.IsNaN m || Double.IsNaN s) then
                middle.[i] <- m
                upper.[i] <- m + mult * s
                lower.[i] <- m - mult * s

        upper, lower, middle

    let atr (period: int) (high: float[]) (low: float[]) (close: float[]) : float[] =
        let tr = Common.trueRange high low close
        Common.atrFromTR period tr

    let donchian (period: int) (high: float[]) (low: float[]) : float[] * float[] * float[] =
        let hh = Common.highest period high
        let ll = Common.lowest period low
        let n = high.Length
        let upper = Array.create n Double.NaN
        let lower = Array.create n Double.NaN
        let middle = Array.create n Double.NaN

        for i in 0 .. n - 1 do
            let h = hh.[i]
            let l = ll.[i]

            if not (Double.IsNaN h || Double.IsNaN l) then
                upper.[i] <- h
                lower.[i] <- l
                middle.[i] <- (h + l) / 2.0

        upper, lower, middle

    let keltner
        (emaPeriod: int)
        (mult: float)
        (atrPeriod: int)
        (high: float[])
        (low: float[])
        (close: float[])
        : float[] * float[] * float[] =
        let emaVals = Common.ema emaPeriod close
        let atrVals = atr atrPeriod high low close
        let n = close.Length
        let upper = Array.create n Double.NaN
        let lower = Array.create n Double.NaN
        let middle = Array.create n Double.NaN

        for i in 0 .. n - 1 do
            let e = emaVals.[i]
            let a = atrVals.[i]

            if not (Double.IsNaN e || Double.IsNaN a) then
                middle.[i] <- e
                upper.[i] <- e + mult * a
                lower.[i] <- e - mult * a

        upper, lower, middle

    let stdDevWithBands (period: int) (src: float[]) : float[] * float[] * float[] =
        let ma, std = Common.rollingMeanAndStd period src
        let n = src.Length
        let zscore = Array.create n Double.NaN

        for i in 0 .. n - 1 do
            let m = ma.[i]
            let s = std.[i]
            let v = src.[i]

            if not (Double.IsNaN m || Double.IsNaN s || Double.IsNaN v) then
                if s = 0.0 then
                    zscore.[i] <- 0.0
                else
                    zscore.[i] <- (v - m) / s

        std, zscore, ma
