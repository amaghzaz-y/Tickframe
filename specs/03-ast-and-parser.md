# Spec 03 — AST and Parser

## AST

The parser produces a small discriminated-union tree. The AST is the single source of
truth between the grammar and the evaluator.

```fsharp
namespace Tickframe

type Expr =
    | Number    of float
    | Column    of string                    // one of: open | high | low | close | volume
    | Indicator of IndicatorCall
    | Unary     of UnaryOp * Expr
    | Binary    of BinaryOp * Expr * Expr

and IndicatorCall = {
    Name:   string                            // e.g. "rsi", "macd", "boll"
    Sub:    string option                     // e.g. "signal", "upper", "k"
    Args:   string list                       // raw scalar tokens; empty slots preserved as ""
    Series: SeriesRef list                    // operands after '@'
}

and SeriesRef =
    | SeriesColumn of string
    | SeriesExpr   of Expr                    // parenthesized nested directive

type UnaryOp =
    | Negate
    | Not

type BinaryOp =
    | Add | Sub | Mul | Div
    | Lt | Le | Eq | Ne | Ge | Gt
    | And | Or | Xor
    | CrossUp | CrossDown | CrossAny
```

## Parser structure

The parser is written with FParsec and is a direct translation of
[`02-directive-grammar.md`](./02-directive-grammar.md).

The entry point:

```fsharp
module DirectiveParser =
    val parse: string -> Result<Expr, DirectiveError>
```

### Precedence layers

Each grammar production maps to one FParsec parser. The recommended structure:

```fsharp
let pExpr         = pCross
let pCross        = pLogical >>= manyWithCrossOps
let pLogical      = pComparison >>= manyWithLogicalOps
let pComparison   = pAdditive >>= optionalComparison
let pAdditive     = pMultiplicative >>= manyWithAdditiveOps
let pMultiplicative = pUnary >>= manyWithMultiplicativeOps
let pUnary        = (unaryOp >>. pUnary) <|> pPrimary
```

`pPrimary` chooses between `Number`, `Column`, `Indicator`, and a parenthesized
expression.

### Indicator parsing

`pIndicator` parses, in order:

1. `command` identifier.
2. optional `.` followed by `sub` identifier.
3. optional `:` followed by the argument list.
4. optional `@` followed by the series list.

Each optional part only starts when its leading token is present; whitespace is
skipped around every token.

### Argument list

- Split on `,`.
- An empty slot is represented by `""`.
- Leading/trailing empty slots are preserved:

```text
,5     -> [""; "5"]
5,     -> ["5"; ""]
,,5    -> [""; ""; "5"]
```

### Series list

Each entry is either:

- a bare identifier (column), producing `SeriesColumn`;
- or a parenthesized directive, producing `SeriesExpr`.

A series list has at least one entry. Empty entries are rejected.

### Column validation

`pPrimary` resolves a bare identifier by case-insensitive name:

- If it is one of `open`, `high`, `low`, `close`, `volume`, produce `Column`.
- Otherwise, produce `Indicator`.

The evaluator/registry is responsible for the final error when an indicator command is
unknown; the parser does not need a registry lookup.

## Parse error mapping

FParsec failures are converted to `DirectiveSyntaxError` carrying the message, line,
and column. The mapping is specified in
[`06-errors-and-validation.md`](./06-errors-and-validation.md).

## AST invariants

The following must hold after a successful parse:

- `Column` names are lowercase canonical OHLCV names.
- `Indicator.Name` and `Indicator.Sub` are lowercase.
- `Indicator.Series` is empty when no `@` list is present.
- `Binary` children are `Expr` and cannot be malformed `IndicatorCall` fragments.
- All `Number` values are finite `float` literals; non-finite literals are rejected
  at parse time.
