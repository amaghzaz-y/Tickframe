namespace Tickframe

open FParsec

module DirectiveParser =

    let private columnNames = Set.ofList [ "open"; "high"; "low"; "close"; "volume" ]

    let private pCrossUp = pstring "//" >>% CrossUp
    let private pCrossDown = pstring "\\" >>% CrossDown
    let private pCrossAny = pstring "><" >>% CrossAny
    let private pGt = pstring ">" >>% Gt
    let private pGe = pstring ">=" >>% Ge
    let private pLt = pstring "<" >>% Lt
    let private pLe = pstring "<=" >>% Le
    let private pEq = pstring "==" >>% Eq
    let private pNe = pstring "!=" >>% Ne
    let private pAdd = pstring "+" >>% Add
    let private pSub = pstring "-" >>% Sub
    let private pMul = pstring "*" >>% Mul
    let private pDiv = pstring "/" >>% Div
    let private pAnd = pstring "&" >>% And
    let private pOr = pstring "|" >>% Or
    let private pXor = pstring "^" >>% Xor

    let private pNumber: Parser<Expr, unit> =
        pfloat
        >>= fun v ->
            if System.Double.IsFinite v then
                preturn (Number v)
            else
                fail "expected a finite number"

    let private pIdentifier: Parser<string, unit> =
        (letter <|> pchar '_') .>>. manyChars (letter <|> digit <|> pchar '_')
        |>> (fun (c, rest) -> string c + rest)

    let private pExprRef, pExprRefImpl = createParserForwardedToRef<Expr, unit> ()
    let private pCrossRef, pCrossRefImpl = createParserForwardedToRef<Expr, unit> ()

    let private pArgList: Parser<string list, unit> =
        let pToken = many1Chars (letter <|> digit <|> pchar '_' <|> pchar '.' <|> pchar '-')
        let pSlot = attempt pToken <|> preturn ""

        attempt (
            (pSlot .>> spaces)
            .>>. many (attempt (pchar ',' >>. spaces >>. pSlot .>> spaces))
            |>> (fun (first, rest) -> first :: rest)
        )
        <|> preturn []

    let private pSeriesList: Parser<SeriesRef list, unit> =
        let pSeries =
            (attempt (pchar '(' >>. spaces >>. pCrossRef .>> spaces .>> pchar ')' |>> SeriesExpr))
            <|> (pIdentifier |>> fun n -> SeriesColumn(n.ToLowerInvariant()))

        attempt (
            pchar '@' >>. spaces >>. pSeries .>>. many (pchar ',' >>. spaces >>. pSeries)
            |>> (fun (first, rest) -> first :: rest)
        )

    let private pIndicatorCore: Parser<Expr, unit> =
        pIdentifier .>> spaces
        >>= fun command ->
            let cmdLC = command.ToLowerInvariant()

            opt (
                attempt (
                    pchar '.' >>. spaces >>. pIdentifier .>> spaces
                    |>> fun s -> s.ToLowerInvariant()
                )
            )
            >>= fun subOpt ->
                let pArgs = attempt (pchar ':' >>. spaces >>. pArgList) <|> preturn []

                pArgs
                >>= fun args ->
                    opt pSeriesList
                    >>= fun seriesOpt ->
                        spaces
                        >>= fun _ ->
                            preturn (
                                let hasArgs = args <> []

                                let nameIsColumn =
                                    Set.contains cmdLC columnNames
                                    && subOpt.IsNone
                                    && not hasArgs
                                    && seriesOpt.IsNone

                                if nameIsColumn then
                                    Column cmdLC
                                else
                                    Indicator
                                        { Name = cmdLC
                                          Sub = subOpt
                                          Args = args
                                          Series = defaultArg seriesOpt [] }
                            )

    let private pPrimary: Parser<Expr, unit> =
        choice
            [ attempt (pchar '(' >>. spaces >>. pCrossRef .>> spaces .>> pchar ')' .>> spaces)
              attempt (
                  pstring "-" >>. spaces >>. pfloat .>> spaces
                  >>= fun v ->
                      if System.Double.IsFinite v then
                          preturn (Number -v)
                      else
                          fail "expected a finite number"
              )
              attempt (pNumber)
              pIndicatorCore ]

    let private pChainWs (pOperand: Parser<Expr, unit>) (pOp: Parser<BinaryOp, unit>) : Parser<Expr, unit> =
        pOperand .>>. many (attempt (spaces >>. pOp .>> spaces .>>. pOperand))
        |>> (fun (first, rest) -> List.fold (fun acc (op, rhs) -> Binary(op, acc, rhs)) first rest)

    let private pUnaryRef, pUnaryRefImpl = createParserForwardedToRef<Expr, unit> ()

    let private pUnary: Parser<Expr, unit> =
        let pOp = (pstring "-" >>% Negate) <|> (pstring "~" >>% Not)

        (attempt (pOp .>> spaces .>>. pUnaryRef |>> (fun (op, e) -> Unary(op, e))))
        <|> pPrimary

    do pUnaryRefImpl.Value <- pUnary

    let private pMultiplicative = pChainWs pUnary (pMul <|> pDiv)
    let private pAdditive = pChainWs pMultiplicative (pAdd <|> pSub)

    let private pComparison: Parser<Expr, unit> =
        let pCompOp =
            attempt pLe <|> attempt pGe <|> attempt pEq <|> attempt pNe <|> pGt <|> pLt

        pAdditive .>>. opt (attempt (spaces >>. pCompOp .>> spaces .>>. pAdditive))
        |>> (function
        | lhs, None -> lhs
        | lhs, Some(op, rhs) -> Binary(op, lhs, rhs))

    let private pLogical = pChainWs pComparison (pAnd <|> pXor <|> pOr)

    let private pCross =
        pChainWs pLogical (attempt pCrossUp <|> attempt pCrossDown <|> attempt pCrossAny)

    do pCrossRefImpl.Value <- pCross

    let private pExpr: Parser<Expr, unit> = spaces >>. pCross .>> eof

    do pExprRefImpl.Value <- pExpr

    let private toOutcome (input: string) : Choice<Expr, DirectiveError> =
        match run pExpr input with
        | Success(expr, _, _) -> Choice1Of2 expr
        | Failure(msg, err, _) ->
            let pos = err.Position
            Choice2Of2(DirectiveSyntaxError(msg, int pos.Line, int pos.Column))

    let parse (input: string) : Result<Expr, DirectiveError> =
        match toOutcome input with
        | Choice1Of2 expr -> Result.Ok expr
        | Choice2Of2 err -> Result.Error err
