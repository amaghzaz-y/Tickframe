namespace Tickframe

module Directive =
    let eval (frame: OhlcvFrame) (directive: string) : Series =
        match DirectiveParser.parse directive with
        | Error err -> raise err
        | Ok expr -> Evaluator.eval frame expr

    let lookback (directive: string) : int =
        match DirectiveParser.parse directive with
        | Error err -> raise err
        | Ok expr -> Evaluator.lookback expr
