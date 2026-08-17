open System
open BenchmarkDotNet.Attributes
open BenchmarkDotNet.Configs
open BenchmarkDotNet.Jobs
open BenchmarkDotNet.Running
open Tickframe

module Fixtures =
    let makeCandles n : Candle[] =
        [| for i in 1 .. n ->
            { Timestamp = DateTime(2024, 1, 1).AddMinutes(float i)
              Open      = decimal (100.0 + sin (float i) * 5.0)
              High      = decimal (102.0 + sin (float i) * 5.0)
              Low       = decimal ( 98.0 + cos (float i) * 5.0)
              Close     = decimal (101.0 + cos (float i) * 4.0)
              Volume    = decimal (1000 + (i % 7) * 50) } |]

    let frame n = OhlcvFrame.ofCandles (makeCandles n)

    let frame500  = frame 500
    let frame1k   = frame 1000
    let frame5k   = frame 5000
    let frame10k  = frame 10000
    let frame20k  = frame 20000
    let frame80   = frame 80

[<MemoryDiagnoser>]
type ParseBenchmarks() =
    [<Benchmark>] member _.ParseSimple() = DirectiveParser.parse "rsi:14 > close" |> ignore
    [<Benchmark>] member _.ParseComplex() = DirectiveParser.parse "ma:5 // ma:20" |> ignore
    [<Benchmark>] member _.ParseNested() = DirectiveParser.parse "ma:10@(ma:5) + boll.upper" |> ignore
    [<Benchmark>] member _.ParseSubArgs() = DirectiveParser.parse "macd.signal:,,5" |> ignore

[<MemoryDiagnoser>]
type EvalPureBenchmarks() =
    let frame = Fixtures.frame80
    [<Benchmark>] member _.CloseColumn() = Directive.eval frame "close" |> ignore
    [<Benchmark>] member _.AddColumns() = Directive.eval frame "close + open * 2" |> ignore
    [<Benchmark>] member _.Comparison() = Directive.eval frame "close > open" |> ignore
    [<Benchmark>] member _.CrossOp() = Directive.eval frame "close // open" |> ignore
    [<Benchmark>] member _.LogicalOp() = Directive.eval frame "close > open & high > low" |> ignore
    [<Benchmark>] member _.UnaryNegate() = Directive.eval frame "-close" |> ignore

[<MemoryDiagnoser>]
type LookbackBenchmarks() =
    [<Benchmark>] member _.LookbackSimple() = Directive.lookback "ma:20" |> ignore
    [<Benchmark>] member _.LookbackNested() = Directive.lookback "ma:20@(ma:5)" |> ignore
    [<Benchmark>] member _.LookbackCross() = Directive.lookback "ma:5 // ma:20" |> ignore

[<MemoryDiagnoser>]
type ScaleBenchmarks() =
    [<Params(500, 1000, 5000, 10000, 20000)>]
    member val N = 0 with get, set

    member private this.Frame =
        match this.N with
        | 500   -> Fixtures.frame500
        | 1000  -> Fixtures.frame1k
        | 5000  -> Fixtures.frame5k
        | 10000 -> Fixtures.frame10k
        | 20000 -> Fixtures.frame20k
        | n     -> Fixtures.frame n

    [<Benchmark>] member this.Ma() = Directive.eval this.Frame "ma:20" |> ignore
    [<Benchmark>] member this.Rsi() = Directive.eval this.Frame "rsi:14" |> ignore
    [<Benchmark>] member this.Ema() = Directive.eval this.Frame "ema:20" |> ignore
    [<Benchmark>] member this.MacdSignal() = Directive.eval this.Frame "macd.signal:,,5" |> ignore
    [<Benchmark>] member this.BollUpper() = Directive.eval this.Frame "boll.upper" |> ignore
    [<Benchmark>] member this.Atr() = Directive.eval this.Frame "atr:14" |> ignore
    [<Benchmark>] member this.CloseColumn() = Directive.eval this.Frame "close" |> ignore
    [<Benchmark>] member this.CrossMa() = Directive.eval this.Frame "ma:5 // ma:20" |> ignore
    [<Benchmark>] member this.NestedMa() = Directive.eval this.Frame "ma:10@(ma:5)" |> ignore

[<MemoryDiagnoser>]
type ScalePureBenchmarks() =
    [<Params(500, 1000, 5000, 10000, 20000)>]
    member val N = 0 with get, set

    member private this.Frame =
        match this.N with
        | 500   -> Fixtures.frame500
        | 1000  -> Fixtures.frame1k
        | 5000  -> Fixtures.frame5k
        | 10000 -> Fixtures.frame10k
        | 20000 -> Fixtures.frame20k
        | n     -> Fixtures.frame n

    [<Benchmark>] member this.AddColumns() = Directive.eval this.Frame "close + open * 2" |> ignore
    [<Benchmark>] member this.CrossPure() = Directive.eval this.Frame "close // open" |> ignore

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
            printfn "  dotnet run -c Release --project Tickframe.Benchmarks -- --filter=*Scale*  # scale only"
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
