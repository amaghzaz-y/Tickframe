namespace Tickframe

open System

[<AbstractClass>]
type DirectiveError(message: string) =
    inherit System.Exception(message)

type DirectiveSyntaxError(message: string, line: int, column: int) =
    inherit DirectiveError(sprintf "syntax error at line %d, column %d: %s" line column message)
    member _.Line = line
    member _.Column = column

type DirectiveValueError(message: string) =
    inherit DirectiveError(message)

module DirectiveErrors =
    let syntax (msg: string) (line: int) (col: int) : 'a =
        raise (DirectiveSyntaxError(msg, line, col))

    let value (msg: string) : 'a =
        raise (DirectiveValueError msg)

[<Struct>]
type Candle = {
    Timestamp: DateTime
    Open: decimal
    High: decimal
    Low: decimal
    Close: decimal
    Volume: decimal
}

type OhlcvFrame internal (candles: Candle[]) =
    member _.RowCount = candles.Length
    member _.Candles = candles

    member internal this.TryColumnInternal (name: string) : float[] option =
        OhlcvFrame.tryColumnInternal name candles

    member _.Column(name: string) : float[] =
        match OhlcvFrame.tryColumnInternal name candles with
        | Some col -> col
        | None -> raise (DirectiveValueError $"unknown column '{name}'")

    static member internal tryColumnInternal (name: string) (cs: Candle[]) : float[] option =
        match name.ToLowerInvariant() with
        | "open"   -> Some (cs |> Array.map (fun c -> float c.Open))
        | "high"   -> Some (cs |> Array.map (fun c -> float c.High))
        | "low"    -> Some (cs |> Array.map (fun c -> float c.Low))
        | "close"  -> Some (cs |> Array.map (fun c -> float c.Close))
        | "volume" -> Some (cs |> Array.map (fun c -> float c.Volume))
        | _ -> None

module OhlcvFrame =
    let ofCandles (candles: seq<Candle>) : OhlcvFrame =
        OhlcvFrame (Seq.toArray candles)

    let tryColumn (frame: OhlcvFrame) (name: string) : float[] option =
        OhlcvFrame.tryColumnInternal name frame.Candles

    let columnNames = [ "open"; "high"; "low"; "close"; "volume" ]

type UnaryOp =
    | Negate
    | Not

type BinaryOp =
    | Add | Sub | Mul | Div
    | Lt | Le | Eq | Ne | Ge | Gt
    | And | Or | Xor
    | CrossUp | CrossDown | CrossAny

type Expr =
    | Number    of float
    | Column    of string
    | Indicator of IndicatorCall
    | Unary     of UnaryOp * Expr
    | Binary    of BinaryOp * Expr * Expr

and IndicatorCall = {
    Name:   string
    Sub:    string option
    Args:   string list
    Series: SeriesRef list
}

and SeriesRef =
    | SeriesColumn of string
    | SeriesExpr   of Expr

type Series =
    | Float of float[]
    | Bool  of bool[]

[<RequireQualifiedAccess>]
module Series =
    let length (s: Series) : int =
        match s with
        | Float a -> a.Length
        | Bool a -> a.Length

    let isFloat (s: Series) : bool =
        match s with Float _ -> true | Bool _ -> false

    let asFloat (s: Series) : float[] =
        match s with
        | Float a -> a
        | Bool _ -> raise (DirectiveValueError "expected a float series")

    let asBool (s: Series) : bool[] =
        match s with
        | Bool a -> a
        | Float _ -> raise (DirectiveValueError "expected a bool series")

type IndicatorOutputKind =
    | FloatOutput
    | BoolOutput

type InputSlots =
    | CloseOnly
    | HighLow
    | HighLowClose
    | OpenHighLowClose
    | CloseVolume
    | HighLowCloseVolume
    | OpenHighLowCloseVolume

type EvalContext = {
    Frame: OhlcvFrame
    Resolve: SeriesRef -> Series
}

type IndicatorSpec = {
    Name:        string
    Aliases:     string list
    SubCommands: Map<string, IndicatorSpec>
    Slots:       InputSlots
    OutputKind:  IndicatorOutputKind
    Compute:     IndicatorCall -> EvalContext -> Series
    Lookback:    IndicatorCall -> int
}
