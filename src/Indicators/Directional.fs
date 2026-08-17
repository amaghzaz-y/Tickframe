namespace Tickframe.Indicators

open System

module Directional =

    let adx (period: int) (high: float[]) (low: float[]) (close: float[]) : float[] =
        let n = close.Length
        let out = Array.create n Double.NaN

        if period > 0 && n > period then
            let tr = Common.trueRange high low close
            let plusDM = Array.create n 0.0
            let minusDM = Array.create n 0.0

            for i in 1 .. n - 1 do
                let h = high.[i]
                let ph = high.[i - 1]
                let l = low.[i]
                let pl = low.[i - 1]

                if not (Double.IsNaN h || Double.IsNaN ph || Double.IsNaN l || Double.IsNaN pl) then
                    let upMove = h - ph
                    let downMove = pl - l

                    if upMove > downMove && upMove > 0.0 then
                        plusDM.[i] <- upMove

                    if downMove > upMove && downMove > 0.0 then
                        minusDM.[i] <- downMove

            let smTR = Array.create n Double.NaN
            let smPlus = Array.create n Double.NaN
            let smMinus = Array.create n Double.NaN
            let mutable sumTR = 0.0
            let mutable sumPlus = 0.0
            let mutable sumMinus = 0.0

            for i in 1..period do
                sumTR <- sumTR + tr.[i]
                sumPlus <- sumPlus + plusDM.[i]
                sumMinus <- sumMinus + minusDM.[i]

            smTR.[period] <- sumTR
            smPlus.[period] <- sumPlus
            smMinus.[period] <- sumMinus

            for i in period + 1 .. n - 1 do
                let pTR = smTR.[i - 1]
                let pPlus = smPlus.[i - 1]
                let pMinus = smMinus.[i - 1]

                if
                    Double.IsNaN pTR
                    || Double.IsNaN pPlus
                    || Double.IsNaN pMinus
                    || Double.IsNaN tr.[i]
                then
                    smTR.[i] <- Double.NaN
                    smPlus.[i] <- Double.NaN
                    smMinus.[i] <- Double.NaN
                else
                    smTR.[i] <- pTR - pTR / float period + tr.[i]
                    smPlus.[i] <- pPlus - pPlus / float period + plusDM.[i]
                    smMinus.[i] <- pMinus - pMinus / float period + minusDM.[i]

            let dx = Array.create n Double.NaN

            for i in period .. n - 1 do
                let sTR = smTR.[i]
                let sPlus = smPlus.[i]
                let sMinus = smMinus.[i]

                if
                    not (Double.IsNaN sTR || Double.IsNaN sPlus || Double.IsNaN sMinus)
                    && sTR <> 0.0
                then
                    let diPlus = 100.0 * sPlus / sTR
                    let diMinus = 100.0 * sMinus / sTR
                    let denom = diPlus + diMinus

                    if denom = 0.0 then
                        dx.[i] <- 0.0
                    else
                        dx.[i] <- 100.0 * abs (diPlus - diMinus) / denom

            let mutable sumDX = 0.0
            let mutable count = 0

            for i in period .. period * 2 - 1 do
                if i < n && not (Double.IsNaN dx.[i]) then
                    sumDX <- sumDX + dx.[i]
                    count <- count + 1

            if count = period then
                let firstIdx = period * 2 - 1
                out.[firstIdx] <- sumDX / float period

                for i in firstIdx + 1 .. n - 1 do
                    let prev = out.[i - 1]
                    let cur = dx.[i]

                    if Double.IsNaN prev || Double.IsNaN cur then
                        out.[i] <- Double.NaN
                    else
                        out.[i] <- (prev * float (period - 1) + cur) / float period

        out

    let aroon (period: int) (high: float[]) (low: float[]) : float[] * float[] =
        let n = high.Length
        let up = Array.create n Double.NaN
        let down = Array.create n Double.NaN

        if period > 0 then
            for i in period - 1 .. n - 1 do
                let mutable maxIdx = i - period + 1
                let mutable minIdx = i - period + 1
                let mutable hasNaN = false

                for k in i - period + 1 .. i do
                    if Double.IsNaN high.[k] || Double.IsNaN low.[k] then
                        hasNaN <- true
                    else
                        if high.[k] >= high.[maxIdx] then
                            maxIdx <- k

                        if low.[k] <= low.[minIdx] then
                            minIdx <- k

                if not hasNaN then
                    up.[i] <- 100.0 * float (period - (i - maxIdx) - 1) / float period
                    down.[i] <- 100.0 * float (period - (i - minIdx) - 1) / float period

        up, down

    let superTrend (period: int) (mult: float) (high: float[]) (low: float[]) (close: float[]) : float[] * bool[] =
        let n = close.Length
        let out = Array.create n Double.NaN
        let dir = Array.create n false
        let atrVals = Volatility.atr period high low close
        let mutable prevUpper = Double.NaN
        let mutable prevLower = Double.NaN
        let mutable prevDir = true
        let mutable prevST = Double.NaN

        for i in 0 .. n - 1 do
            let atr = atrVals.[i]
            let h = high.[i]
            let l = low.[i]
            let c = close.[i]

            if Double.IsNaN atr || Double.IsNaN h || Double.IsNaN l || Double.IsNaN c then
                out.[i] <- Double.NaN
                dir.[i] <- if i > 0 then dir.[i - 1] else false
            else
                let hl2 = (h + l) / 2.0
                let upper = hl2 + mult * atr
                let lower = hl2 - mult * atr

                if i = 0 || Double.IsNaN prevST then
                    prevDir <- true
                    out.[i] <- lower
                    dir.[i] <- true
                else
                    let mutable curUpper = upper
                    let mutable curLower = lower

                    if not (Double.IsNaN prevUpper) && c <= prevUpper then
                        curUpper <- min upper prevUpper

                    if not (Double.IsNaN prevLower) && c >= prevLower then
                        curLower <- max lower prevLower

                    let mutable curDir = prevDir

                    if prevDir && c <= curLower then
                        curDir <- false
                    elif not prevDir && c >= curUpper then
                        curDir <- true

                    out.[i] <- if curDir then curLower else curUpper
                    dir.[i] <- curDir
                    prevUpper <- curUpper
                    prevLower <- curLower
                    prevDir <- curDir

                prevST <- out.[i]

                if Double.IsNaN prevUpper then
                    prevUpper <- upper

                if Double.IsNaN prevLower then
                    prevLower <- lower

        out, dir

    let parabolicSar (step: float) (maxAf: float) (high: float[]) (low: float[]) : float[] =
        let n = high.Length
        let out = Array.create n Double.NaN

        if n > 0 then
            let mutable isLong = true
            let mutable sar = low.[0]
            let mutable ep = high.[0]
            let mutable af = step
            out.[0] <- sar

            for i in 1 .. n - 1 do
                let h = high.[i]
                let l = low.[i]

                if Double.IsNaN h || Double.IsNaN l || Double.IsNaN sar || Double.IsNaN ep then
                    out.[i] <- Double.NaN
                else
                    let prevSar = sar
                    sar <- sar + af * (ep - sar)

                    if isLong then
                        if i >= 2 then
                            sar <- min sar (min low.[i - 1] low.[i - 2])
                        else
                            sar <- min sar low.[i - 1]

                        if l < sar then
                            isLong <- false
                            sar <- ep
                            ep <- l
                            af <- step
                        else if h > ep then
                            ep <- h
                            af <- min (af + step) maxAf
                    else
                        if i >= 2 then
                            sar <- max sar (max high.[i - 1] high.[i - 2])
                        else
                            sar <- max sar high.[i - 1]

                        if h > sar then
                            isLong <- true
                            sar <- ep
                            ep <- h
                            af <- step
                        else if l < ep then
                            ep <- l
                            af <- min (af + step) maxAf

                    out.[i] <- sar

                    if not isLong && out.[i] = prevSar then
                        ()

        out

    let ichimoku
        (tenkanPeriod: int)
        (kijunPeriod: int)
        (senkouPeriod: int)
        (high: float[])
        (low: float[])
        (close: float[])
        : float[] * float[] * float[] * float[] * float[] =
        let n = close.Length
        let tenkan = Array.create n Double.NaN
        let kijun = Array.create n Double.NaN
        let senkouA = Array.create n Double.NaN
        let senkouB = Array.create n Double.NaN
        let chikou = Array.create n Double.NaN
        let hhTenkan = Common.highest tenkanPeriod high
        let llTenkan = Common.lowest tenkanPeriod low
        let hhKijun = Common.highest kijunPeriod high
        let llKijun = Common.lowest kijunPeriod low
        let hhSenkou = Common.highest senkouPeriod high
        let llSenkou = Common.lowest senkouPeriod low

        for i in 0 .. n - 1 do
            let ht = hhTenkan.[i]
            let lt = llTenkan.[i]

            if not (Double.IsNaN ht || Double.IsNaN lt) then
                tenkan.[i] <- (ht + lt) / 2.0

            let hk = hhKijun.[i]
            let lk = llKijun.[i]

            if not (Double.IsNaN hk || Double.IsNaN lk) then
                kijun.[i] <- (hk + lk) / 2.0

            let hs = hhSenkou.[i]
            let ls = llSenkou.[i]

            if not (Double.IsNaN hs || Double.IsNaN ls) then
                senkouB.[i] <- (hs + ls) / 2.0

        for i in 0 .. n - 1 do
            let t = tenkan.[i]
            let k = kijun.[i]

            if not (Double.IsNaN t || Double.IsNaN k) then
                senkouA.[i] <- (t + k) / 2.0

        for i in 0 .. n - 1 - kijunPeriod do
            chikou.[i] <- close.[i + kijunPeriod]

        tenkan, kijun, senkouA, senkouB, chikou
