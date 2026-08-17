# Tickframe

F# OHLCV directive engine targeting .NET 10.

Evaluates directive expressions (e.g. `rsi:14 > close`, `ma:5 // ma:20`) over OHLCV
candle data.

## Usage

```fsharp
open Tickframe

let frame = OhlcvFrame.ofCandles candles
let signal = Directive.eval frame "rsi:14 > close"
let ma = Directive.eval frame "ma:20@close"
```

## Development

```sh
dotnet build
dotnet test
dotnet pack
```
