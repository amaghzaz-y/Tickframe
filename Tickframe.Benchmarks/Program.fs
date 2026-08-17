open System
open BenchmarkDotNet.Attributes
open BenchmarkDotNet.Configs
open BenchmarkDotNet.Jobs
open BenchmarkDotNet.Running
open Tickframe

module Fixtures =
    let candlesSmall : Candle[] =
        [| for i in 1 .. 80 ->
            { Timestamp = DateTime(2024, 1, 1).AddMinutes(float i)
              Open      = decimal (100.0 + sin (float i) * 5.0)
              High      = decimal (102.0 + sin (float i) * 5.0)
              Low       = decimal ( 98.0 + cos (float i) * 5.0)
              Close     = decimal (101.0 + cos (float i) * 4.0)
              Volume    = decimal (1000 + (i % 7) * 50) } |]

    let candlesLarge : Candle[] =
        [| for i in 1 .. 5000 ->
            { Timestamp = DateTime(2024, 1, 1).AddMinutes(float i)
              Open      = decimal (100.0 + sin (float i) * 5.0)
              High      = decimal (102.0 + sin (float i) * 5.0)
              Low       = decimal ( 98.0 + cos (float i) * 5.0)
              Close     = decimal (101.0 + cos (float i) * 4.0)
              Volume    = decimal (1000 + (i % 7) * 50) } |]

    let frameSmall = OhlcvFrame.ofCandles candlesSmall
    let frameLarge = OhlcvFrame.ofCandles candlesLarge

[<MemoryDiagnoser>]
type ParseBenchmarks() =
    [<Benchmark>] member _.ParseSimple() = DirectiveParser.parse "rsi:14 > close" |> ignore
    [<Benchmark>] member _.ParseComplex() = DirectiveParser.parse "ma:5 // ma:20" |> ignore
    [<Benchmark>] member _.ParseNested() = DirectiveParser.parse "ma:10@(ma:5) + boll.upper" |> ignore
    [<Benchmark>] member _.ParseSubArgs() = DirectiveParser.parse "macd.signal:,,5" |> ignore

[<MemoryDiagnoser>]
type EvalPureBenchmarks() =
    let frame = Fixtures.frameSmall
    [<Benchmark>] member _.CloseColumn() = Directive.eval frame "close" |> ignore
    [<Benchmark>] member _.AddColumns() = Directive.eval frame "close + open * 2" |> ignore
    [<Benchmark>] member _.Comparison() = Directive.eval frame "close > open" |> ignore
    [<Benchmark>] member _.CrossOp() = Directive.eval frame "close // open" |> ignore
    [<Benchmark>] member _.LogicalOp() = Directive.eval frame "close > open & high > low" |> ignore
    [<Benchmark>] member _.UnaryNegate() = Directive.eval frame "-close" |> ignore

[<MemoryDiagnoser>]
type EvalIndicatorBenchmarks() =
    let small = Fixtures.frameSmall
    let large = Fixtures.frameLarge
    [<Benchmark>] member _.MaSmall() = Directive.eval small "ma:20" |> ignore
    [<Benchmark>] member _.MaLarge() = Directive.eval large "ma:20" |> ignore
    [<Benchmark>] member _.RsiSmall() = Directive.eval small "rsi:14" |> ignore
    [<Benchmark>] member _.EmaSmall() = Directive.eval small "ema:20" |> ignore
    [<Benchmark>] member _.MacdSignal() = Directive.eval small "macd.signal:,,5" |> ignore
    [<Benchmark>] member _.BollUpper() = Directive.eval small "boll.upper" |> ignore
    [<Benchmark>] member _.AtrSmall() = Directive.eval small "atr:14" |> ignore
    [<Benchmark>] member _.NestedMa() = Directive.eval small "ma:10@(ma:5)" |> ignore
    [<Benchmark>] member _.CrossMa() = Directive.eval small "ma:5 // ma:20" |> ignore

[<MemoryDiagnoser>]
type LookbackBenchmarks() =
    [<Benchmark>] member _.LookbackSimple() = Directive.lookback "ma:20" |> ignore
    [<Benchmark>] member _.LookbackNested() = Directive.lookback "ma:20@(ma:5)" |> ignore
    [<Benchmark>] member _.LookbackCross() = Directive.lookback "ma:5 // ma:20" |> ignore

[<EntryPoint>]
let main argv =
    let runAll = argv |> Array.contains "--all"
    let runFilter =
        argv |> Array.tryFind (fun a -> a.StartsWith("--filter=")) |> Option.map (fun s -> s.Substring(9))

    let config = ManualConfig.Create(DefaultConfig.Instance).AddJob(Job.Dry.WithWarmupCount(1).WithIterationCount(1))

    let runBenchmarks () =
        match runFilter with
        | Some f -> BenchmarkSwitcher.FromAssembly(typeof<ParseBenchmarks>.Assembly).Run([| "--filter"; f |], config) |> ignore
        | None when runAll -> BenchmarkRunner.Run(typeof<ParseBenchmarks>.Assembly, config) |> ignore
        | None ->
            printfn "Tickframe benchmarks"
            printfn "  dotnet run -c Release --project Tickframe.Benchmarks -- --all            # all groups"
            printfn "  dotnet run -c Release --project Tickframe.Benchmarks -- --filter=*Parse*  # filter"
            printfn ""
            BenchmarkSwitcher.FromAssembly(typeof<ParseBenchmarks>.Assembly).Run([||], config) |> ignore
        0

    let allowRun =
        #if DEBUG
        false
        #else
        true
        #endif
    if not allowRun then
        printfn "Benchmarks must be run in Release configuration: dotnet run -c Release --project Tickframe.Benchmarks -- --all"
        1
    else
        runBenchmarks ()
