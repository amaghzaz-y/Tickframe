// Shared fixtures and helpers for Tickframe tests.
module Tickframe.Tests.Shared

open System
open Xunit
open Tickframe

/// Synthetic OHLCV fixture: 80 rows, increasing timestamps, hand-checked values.
let candles: Candle[] =
    [| for i in 1..80 ->
           { Timestamp = DateTime(2024, 1, 1).AddMinutes(float i)
             Open = decimal (100.0 + sin (float i) * 5.0)
             High = decimal (102.0 + sin (float i) * 5.0)
             Low = decimal (98.0 + cos (float i) * 5.0)
             Close = decimal (101.0 + cos (float i) * 4.0)
             Volume = decimal (1000 + (i % 7) * 50) } |]

let frame: OhlcvFrame = OhlcvFrame.ofCandles candles

/// Assert a float series value, treating NaN as a specific value.
let assertFloat (expected: float) (actual: float) =
    if Double.IsNaN expected then
        Assert.True(Double.IsNaN actual, sprintf "expected NaN, got %f" actual)
    else
        Assert.Equal(expected, actual, 6) // 6 decimal places

/// Read a Float series or fail.
let floatOf (s: Series) : float[] =
    match s with
    | Float a -> a
    | Bool _ -> failwith "expected a Float series"

/// Read a Bool series or fail.
let boolOf (s: Series) : bool[] =
    match s with
    | Bool a -> a
    | Float _ -> failwith "expected a Bool series"
