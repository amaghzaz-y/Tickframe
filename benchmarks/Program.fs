open System
open System.IO
open BenchmarkDotNet.Attributes
open BenchmarkDotNet.Configs
open BenchmarkDotNet.Exporters
open BenchmarkDotNet.Jobs
open BenchmarkDotNet.Running
open Tickframe

module Fixtures =
    let makeCandles n : Candle[] =
        [| for i in 1..n ->
               { Timestamp = DateTime(2024, 1, 1).AddMinutes(float i)
                 Open = decimal (100.0 + sin (float i) * 5.0)
                 High = decimal (102.0 + sin (float i) * 5.0)
                 Low = decimal (98.0 + cos (float i) * 5.0)
                 Close = decimal (101.0 + cos (float i) * 4.0)
                 Volume = decimal (1000 + (i % 7) * 50) } |]

    let frame n = OhlcvFrame.ofCandles (makeCandles n)

    let frame500 = frame 500
    let frame1k = frame 1000
    let frame5k = frame 5000
    let frame10k = frame 10000
    let frame20k = frame 20000
    let frame80 = frame 80

[<MemoryDiagnoser>]
type ParseBenchmarks() =
    [<Benchmark>]
    member _.ParseSimple() =
        DirectiveParser.parse "rsi:14 > close" |> ignore

    [<Benchmark>]
    member _.ParseComplex() =
        DirectiveParser.parse "ma:5 // ma:20" |> ignore

    [<Benchmark>]
    member _.ParseNested() =
        DirectiveParser.parse "ma:10@(ma:5) + boll.upper" |> ignore

    [<Benchmark>]
    member _.ParseSubArgs() =
        DirectiveParser.parse "macd.signal:,,5" |> ignore

[<MemoryDiagnoser>]
type EvalPureBenchmarks() =
    let frame = Fixtures.frame80

    [<Benchmark>]
    member _.CloseColumn() = Directive.eval frame "close" |> ignore

    [<Benchmark>]
    member _.AddColumns() =
        Directive.eval frame "close + open * 2" |> ignore

    [<Benchmark>]
    member _.Comparison() =
        Directive.eval frame "close > open" |> ignore

    [<Benchmark>]
    member _.CrossOp() =
        Directive.eval frame "close // open" |> ignore

    [<Benchmark>]
    member _.LogicalOp() =
        Directive.eval frame "close > open & high > low" |> ignore

    [<Benchmark>]
    member _.UnaryNegate() = Directive.eval frame "-close" |> ignore

[<MemoryDiagnoser>]
type LookbackBenchmarks() =
    [<Benchmark>]
    member _.LookbackSimple() = Directive.lookback "ma:20" |> ignore

    [<Benchmark>]
    member _.LookbackNested() =
        Directive.lookback "ma:20@(ma:5)" |> ignore

    [<Benchmark>]
    member _.LookbackCross() =
        Directive.lookback "ma:5 // ma:20" |> ignore

[<MemoryDiagnoser>]
type ScaleBenchmarks() =
    [<Params(500, 1000, 5000, 10000, 20000)>]
    member val N = 0 with get, set

    member private this.Frame =
        match this.N with
        | 500 -> Fixtures.frame500
        | 1000 -> Fixtures.frame1k
        | 5000 -> Fixtures.frame5k
        | 10000 -> Fixtures.frame10k
        | 20000 -> Fixtures.frame20k
        | n -> Fixtures.frame n

    [<Benchmark>]
    member this.Ma() =
        Directive.eval this.Frame "ma:20" |> ignore

    [<Benchmark>]
    member this.Rsi() =
        Directive.eval this.Frame "rsi:14" |> ignore

    [<Benchmark>]
    member this.Ema() =
        Directive.eval this.Frame "ema:20" |> ignore

    [<Benchmark>]
    member this.MacdSignal() =
        Directive.eval this.Frame "macd.signal:,,5" |> ignore

    [<Benchmark>]
    member this.BollUpper() =
        Directive.eval this.Frame "boll.upper" |> ignore

    [<Benchmark>]
    member this.Atr() =
        Directive.eval this.Frame "atr:14" |> ignore

    [<Benchmark>]
    member this.CloseColumn() =
        Directive.eval this.Frame "close" |> ignore

    [<Benchmark>]
    member this.CrossMa() =
        Directive.eval this.Frame "ma:5 // ma:20" |> ignore

    [<Benchmark>]
    member this.NestedMa() =
        Directive.eval this.Frame "ma:10@(ma:5)" |> ignore

[<MemoryDiagnoser>]
type ScalePureBenchmarks() =
    [<Params(500, 1000, 5000, 10000, 20000)>]
    member val N = 0 with get, set

    member private this.Frame =
        match this.N with
        | 500 -> Fixtures.frame500
        | 1000 -> Fixtures.frame1k
        | 5000 -> Fixtures.frame5k
        | 10000 -> Fixtures.frame10k
        | 20000 -> Fixtures.frame20k
        | n -> Fixtures.frame n

    [<Benchmark>]
    member this.AddColumns() =
        Directive.eval this.Frame "close + open * 2" |> ignore

    [<Benchmark>]
    member this.CrossPure() =
        Directive.eval this.Frame "close // open" |> ignore

module BenchmarkArtifacts =

    let repoRoot () =
        let exeDir = AppContext.BaseDirectory

        let rec walk (dir: DirectoryInfo) =
            if dir = null then
                DirectoryInfo(exeDir)
            elif File.Exists(Path.Combine(dir.FullName, "Tickframe.fsproj")) then
                dir
            else
                walk dir.Parent

        walk (DirectoryInfo(exeDir))

    let artifactsDir () =
        Path.Combine(repoRoot().FullName, "BenchmarkDotNet.Artifacts", "results")

    let latestReports () =
        let dir = artifactsDir ()

        if not (Directory.Exists dir) then
            [||]
        else
            Directory.GetFiles(dir, "*.md")
            |> Array.sortByDescending (fun f -> File.GetLastWriteTimeUtc f)

    let readLatest (pattern: string) =
        latestReports ()
        |> Array.tryFind (fun f -> f.Contains(pattern))
        |> Option.map File.ReadAllText
        |> Option.defaultValue ""

[<EntryPoint>]
let main argv =
    let runAll = argv |> Array.contains "--all"

    let runFilter =
        argv
        |> Array.tryFind (fun a -> a.StartsWith("--filter="))
        |> Option.map (fun s -> s.Substring(9))

    let skipWrite = argv |> Array.contains "--no-write"

    let config =
        ManualConfig.Create(DefaultConfig.Instance).AddJob(Job.Dry.WithWarmupCount(1).WithIterationCount(1))

    let allowRun =
#if DEBUG
        false
#else
        true
#endif
    if not allowRun then
        printfn
            "Benchmarks must be run in Release configuration: dotnet run -c Release --project Tickframe.Benchmarks -- --all"

        1
    else
        let summaries =
            match runFilter with
            | Some f ->
                BenchmarkSwitcher.FromAssembly(typeof<ParseBenchmarks>.Assembly).Run([| "--filter"; f |], config)
            | None when runAll -> BenchmarkRunner.Run(typeof<ParseBenchmarks>.Assembly, config)
            | None ->
                printfn "Tickframe benchmarks"
                printfn "  dotnet run -c Release --project Tickframe.Benchmarks -- --all            # all groups"
                printfn "  dotnet run -c Release --project Tickframe.Benchmarks -- --filter=*Scale*  # scale only"
                printfn ""
                BenchmarkSwitcher.FromAssembly(typeof<ParseBenchmarks>.Assembly).Run([||], config)

        if not skipWrite then
            let outPath = Path.Combine(BenchmarkArtifacts.repoRoot().FullName, "BENCHMARKS.md")
            let parseMd = BenchmarkArtifacts.readLatest "ParseBenchmarks"
            let pureMd = BenchmarkArtifacts.readLatest "EvalPureBenchmarks"
            let lookMd = BenchmarkArtifacts.readLatest "LookbackBenchmarks"
            let scaleMd = BenchmarkArtifacts.readLatest "ScaleBenchmarks"
            let scaleP = BenchmarkArtifacts.readLatest "ScalePureBenchmarks"

            let ver =
                try
                    let psi = System.Diagnostics.ProcessStartInfo("dotnet", "--version")
                    psi.RedirectStandardOutput <- true
                    psi.UseShellExecute <- false
                    use p = System.Diagnostics.Process.Start(psi)
                    p.WaitForExit()
                    p.StandardOutput.ReadToEnd().Trim()
                with _ ->
                    "unknown"

            let env =
                sprintf
                    "BenchmarkDotNet 0.15.x — .NET %s — %s — %s"
                    ver
                    Environment.OSVersion.VersionString
                    (DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm UTC"))

            let section title md =
                if String.IsNullOrWhiteSpace md then
                    sprintf
                        "## %s\n\n_No report yet — run `dotnet run -c Release --project Tickframe.Benchmarks -- --all`._\n"
                        title
                else
                    sprintf "## %s\n\n%s\n" title md

            let doc =
                String.concat
                    "\n"
                    [ "# BENCHMARKS"
                      ""
                      sprintf
                          "> Auto-generated by `Tickframe.Benchmarks` on %s. Do not edit by hand — re-run benchmarks to refresh."
                          (DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm UTC"))
                      ""
                      "## Summary"
                      ""
                      "- **Parse** — directive string → `Expr` (FParsec), independent of N."
                      "- **Eval (pure)** — 80-row fixture, column/arithmetic/comparison/cross/logical."
                      "- **Lookback** — `Directive.lookback` (no FacioQuo call)."
                      "- **Scale (indicator)** — `Directive.eval` at N=500/1000/5000/10000/20000 (FacioQuo `Bar`→`IReusable` bridge)."
                      "- **Scale (pure)** — `close + open * 2` / `close // open` at same N (no FacioQuo)."
                      ""
                      sprintf "Environment: %s" env
                      ""
                      "**Config**: `Job.Dry` (warmup=1, iter=1) — relative comparisons are meaningful; absolute ms includes process-startup overhead. For publishable numbers use `Job.Default` (or `--all` with a non-Dry config)."
                      ""
                      section "Parse" parseMd
                      section "Eval (pure, N=80)" pureMd
                      section "Lookback" lookMd
                      section "Scale — indicators" scaleMd
                      section "Scale — pure ops" scaleP
                      "## How to reproduce"
                      ""
                      "```sh"
                      "dotnet run -c Release --project Tickframe.Benchmarks -- --all"
                      "# filtered:"
                      "dotnet run -c Release --project Tickframe.Benchmarks -- --filter=\"*ScaleBenchmarks*\""
                      "# also writes BenchmarkDotNet.Artifacts/results/*.md/.csv/.html"
                      "```" ]

            File.WriteAllText(outPath, doc) |> ignore
            printfn "Wrote %s" outPath

        summaries |> ignore
        0
