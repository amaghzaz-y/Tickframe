namespace Tickframe

module Evaluator =
    let rec resolveSeriesRef (frame: OhlcvFrame) (ref: SeriesRef) : Series =
        match ref with
        | SeriesColumn name -> Float (frame.Column name)
        | SeriesExpr expr -> eval frame expr

    and eval (frame: OhlcvFrame) (expr: Expr) : Series =
        match expr with
        | Number n -> Float (Array.create frame.RowCount n)

        | Column name -> Float (frame.Column name)

        | Unary (op, operand) ->
            match op, eval frame operand with
            | Negate, Float a -> Float (Array.map (~-) a)
            | Not, Bool a -> Bool (Array.map not a)
            | Negate, Bool _ -> raise (DirectiveValueError "operator '-' requires a float operand")
            | Not, Float _ -> raise (DirectiveValueError "operator '~' requires a bool operand")

        | Binary (op, left, right) ->
            match op with
            | Add | Sub | Mul | Div ->
                let a = eval frame left |> Series.asFloat
                let b = eval frame right |> Series.asFloat
                match op with
                | Add -> Float (Array.map2 (+) a b)
                | Sub -> Float (Array.map2 (-) a b)
                | Mul -> Float (Array.map2 (*) a b)
                | _ -> Float (Array.map2 (/) a b)

            | Lt | Le | Eq | Ne | Ge | Gt ->
                let a = eval frame left |> Series.asFloat
                let b = eval frame right |> Series.asFloat
                match op with
                | Lt -> Bool (Array.map2 (<) a b)
                | Le -> Bool (Array.map2 (<=) a b)
                | Eq -> Bool (Array.map2 (=) a b)
                | Ne -> Bool (Array.map2 (<>) a b)
                | Ge -> Bool (Array.map2 (>=) a b)
                | _ -> Bool (Array.map2 (>) a b)

            | And | Or | Xor ->
                let a = eval frame left |> Series.asBool
                let b = eval frame right |> Series.asBool
                match op with
                | And -> Bool (Array.map2 (&&) a b)
                | Or -> Bool (Array.map2 (||) a b)
                | _ -> Bool (Array.map2 (<>) a b)

            | CrossUp | CrossDown | CrossAny ->
                let a = eval frame left |> Series.asFloat
                let b = eval frame right |> Series.asFloat
                let cross (up: bool) (down: bool) : bool = up || down
                let compute i (up: bool) (down: bool) : bool =
                    match op with
                    | CrossUp -> up
                    | CrossDown -> down
                    | _ -> cross up down
                let result =
                    Array.mapi (fun i aI ->
                        if i = 0 then false
                        else
                            let up = a.[i - 1] <= b.[i - 1] && aI > b.[i]
                            let down = a.[i - 1] >= b.[i - 1] && aI < b.[i]
                            compute i up down) a
                Bool result

        | Indicator call ->
            let spec = IndicatorRegistry.resolve call.Name
            if call.Sub.IsSome && not (Map.containsKey call.Sub.Value spec.SubCommands) then
                raise (DirectiveValueError $"unknown sub-command '{call.Name}.{call.Sub.Value}'")
            spec.Compute call { Frame = frame; Resolve = resolveSeriesRef frame }

    let rec lookback (expr: Expr) : int =
        match expr with
        | Number _ | Column _ -> 0
        | Indicator call ->
            let spec = IndicatorRegistry.resolve call.Name
            let operands =
                call.Series
                |> List.sumBy (function SeriesColumn _ -> 0 | SeriesExpr e -> lookback e)
            spec.Lookback call + operands
        | Unary (_, e) -> lookback e
        | Binary (_, l, r) -> max (lookback l) (lookback r)
