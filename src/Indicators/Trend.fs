namespace Tickframe.Indicators

open System

module Trend =

    let hma (period: int) (src: float[]) : float[] =
        let n = src.Length
        let out = Array.create n Double.NaN

        if period > 0 && n > 0 then
            let half = max 1 (period / 2)
            let sqrtP = max 1 (int (sqrt (float period)))
            let wmaHalf = Common.wma half src
            let wmaFull = Common.wma period src
            let diff = Array.create n Double.NaN

            for i in 0 .. n - 1 do
                let a = wmaHalf.[i]
                let b = wmaFull.[i]

                if not (Double.IsNaN a || Double.IsNaN b) then
                    diff.[i] <- 2.0 * a - b

            let wmaDiff = Common.wma sqrtP diff

            for i in 0 .. n - 1 do
                out.[i] <- wmaDiff.[i]

        out
