namespace Tickframe

open FParsec

module DirectiveParser =

    let private columnNames = Set.ofList [ "open"; "high"; "low"; "close"; "volume" ]

    let private strWs (s: string) = pstring s .>> spaces

    let private pCrossUp   = strWs "//" >>% CrossUp
    let private pCrossDown = strWs "\\" >>% CrossDown
    let private pCrossAny  = strWs "><" >>% CrossAny
    let private pGt        = strWs ">"  >>% Gt
    let private pGe        = strWs ">=" >>% Ge
    let private pLt        = strWs "<"  >>% Lt
    let private pLe        = strWs "<=" >>% Le
    let private pEq        = strWs "==" >>% Eq
    let private pNe        = strWs "!=" >>% Ne
    let private pAdd       = strWs "+"  >>% Add
    let private pSub       = strWs "-"  >>% Sub
    let private pMul       = strWs "*"  >>% Mul
    let private pDiv       = strWs "/"  >>% Div
    let private pAnd       = strWs "&"  >>% And
    let private pOr        = strWs "|"  >>% Or
    let private pXor       = strWs "^"  >>% Xor

    let private pNumber : Parser<Expr, unit> =
        pfloat >>= fun v ->
            if System.Double.IsFinite v then preturn (Number v)
            else fail "expected a finite number"

    let private pIdentifier : Parser<string, unit> =
        (letter <|> pchar '_') .>>. manyChars (letter <|> digit <|> pchar '_')
        |>> (fun (c, rest) -> string c + rest)

    let private pExprRef, pExprRefImpl = createParserForwardedToRef<Expr, unit> ()

    let private pArg : Parser<string, unit> =
        manyChars (letter <|> digit <|> pchar '_' <|> pchar '.' <|> pchar '-')
        <|> preturn ""

    let private pArgList : Parser<string list, unit> =
        pArg .>>. many (pchar ',' .>> spaces >>. pArg)
        |>> (fun (first, rest) -> first :: rest)

    let private pSeriesList : Parser<SeriesRef list, unit> =
        let pSeries =
            (attempt (pchar '(' .>> spaces >>. pExprRef .>> pchar ')' .>> spaces |>> SeriesExpr))
            <|> (pIdentifier |>> SeriesColumn)
        pSeries .>>. many (pchar ',' .>> spaces >>. pSeries)
        |>> (fun (first, rest) -> first :: rest)

    let private pIndicator : Parser<Expr, unit> =
        pIdentifier .>> spaces >>= fun command ->
        let command = command.ToLowerInvariant()
        let pSub =
            (pchar '.' .>> spaces >>. pIdentifier .>> spaces)
            |>> (fun s -> Some (s.ToLowerInvariant()))
        let pArgs =
            (pchar ':' .>> spaces >>. pArgList)
            |>> Some
        let pSeries =
            (pchar '@' .>> spaces >>. pSeriesList)
            |>> Some
        (pSub .>>. (pArgs <|> preturn None) .>>. (pSeries <|> preturn None))
        |>> (fun ((sub, args), series) ->
            Indicator {
                Name = command
                Sub = sub
                Args = defaultArg args []
                Series = defaultArg series []
            })

    let private pPrimary : Parser<Expr, unit> =
        choice [
            attempt (pstring "-" .>> spaces >>. pfloat .>> spaces >>= fun v ->
                if System.Double.IsFinite v then preturn (Number -v)
                else fail "expected a finite number")
            pNumber
            (attempt (pchar '(' .>> spaces >>. pExprRef .>> pchar ')' .>> spaces))
            (pIdentifier .>> spaces >>= fun name ->
                let name = name.ToLowerInvariant()
                if Set.contains name columnNames then preturn (Column name)
                else preturn (Indicator { Name = name; Sub = None; Args = []; Series = [] }))
        ]

    let private pChain (pOperand: Parser<Expr, unit>) (pOp: Parser<BinaryOp, unit>) : Parser<Expr, unit> =
        pOperand .>>. many (pOp .>>. pOperand)
        |>> (fun (first, rest) ->
            List.fold (fun acc (op, rhs) -> Binary (op, acc, rhs)) first rest)

    let private pUnaryRef, pUnaryRefImpl = createParserForwardedToRef<Expr, unit> ()

    let private pUnary : Parser<Expr, unit> =
        let pOp = (strWs "-" >>% Negate) <|> (strWs "~" >>% Not)
        (attempt (pOp .>>. pUnaryRef |>> (fun (op, e) -> Unary (op, e))))
        <|> pPrimary

    do pUnaryRefImpl.Value <- pUnary

    let private pMultiplicative = pChain pUnary (pMul <|> pDiv)
    let private pAdditive       = pChain pMultiplicative (pAdd <|> pSub)

    let private pComparison : Parser<Expr, unit> =
        pAdditive .>>. (opt ((pLe <|> pLt <|> pEq <|> pNe <|> pGe <|> pGt) .>>. pAdditive))
        |>> (function
            | lhs, None -> lhs
            | lhs, Some (op, rhs) -> Binary (op, lhs, rhs))

    let private pLogical = pChain pComparison (pAnd <|> pXor <|> pOr)
    let private pCross   = pChain pLogical (pCrossUp <|> pCrossDown <|> pCrossAny)

    let private pExpr : Parser<Expr, unit> = pCross .>> eof

    do pExprRefImpl.Value <- pExpr

    let private toOutcome (input: string) : Choice<Expr, DirectiveError> =
        let parser : Parser<Expr, unit> = spaces >>. pExpr
        match run parser input with
        | Success (expr, _, _) -> Choice1Of2 expr
        | Failure (msg, err, _) ->
            let pos = err.Position
            Choice2Of2 (DirectiveSyntaxError(msg, int pos.Line, int pos.Column))

    let parse (input: string) : Result<Expr, DirectiveError> =
        match toOutcome input with
        | Choice1Of2 expr -> Result.Ok expr
        | Choice2Of2 err -> Result.Error err
