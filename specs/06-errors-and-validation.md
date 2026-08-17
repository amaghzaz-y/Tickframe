# Spec 06 — Errors and Validation

All directive failures are typed. Callers should be able to distinguish syntax
problems from semantic/value problems.

```fsharp
type DirectiveError =
    | DirectiveSyntaxError of string * line: int * column: int
    | DirectiveValueError  of string
```

`DirectiveError` should implement `System.Exception` semantics in the implementation
so it can be raised and caught; the two cases above define the public shape.

## Syntax errors

Produced by the parser and represented by `DirectiveSyntaxError`.

| Input | Message example |
| --- | --- |
| `a >` | `"unexpected end of input; expected an expression"` |
| `a //` | `"unexpected end of input; expected an expression"` |
| `a < b < c` | `"comparison operators are non-associative; use parentheses"` |
| `ma:10@` | `"expected a series after '@'"` |
| `ma@open,,close` | `"empty series entries are not allowed"` |
| `(` without `)` | `"missing ')'"` |
| non-finite literal | `"expected a finite number"` |

The exact wording is not fixed; line and column must be populated from the FParsec
failure.

## Value errors

Produced by the evaluator or registry and represented by `DirectiveValueError`.

### Unknown names

- Unknown indicator command: `"unknown indicator 'foo'"`
- Unknown sub-command: `"unknown sub-command 'macd.bar'"`
- Unknown column (in a non-indicator series position where a column is required):
  `"unknown column 'settle'"`

### Argument validation

- Wrong arg count: `"indicator 'rsi' expects 1 argument, got 2"`
- Non-numeric arg where numeric required: `"argument 'abc' is not an integer"`
- Out-of-range arg: `"argument 20 is outside the valid range [2, 100]"`
- Empty required arg: `"indicator 'foo' requires argument 1; empty slots are not allowed"`

### Operand-series validation

- Wrong series count: `"indicator 'rsi' expects 1 series operand, got 2"`
- Type mismatch when a nested directive produces `Bool` but the indicator requires
  `Float`: `"indicator 'rsi' requires a float series operand"`

### Operator type validation

- Arithmetic on bool: `"operator '+' requires float operands"`
- Comparison on bool: `"operator '>' requires float operands"`
- Logical on float: `"operator '&' requires bool operands"`
- Cross on bool: `"operator '//' requires float operands"`
- Unary not on float: `"operator '~' requires a bool operand"`
- Unary negate on bool: `"operator '-' requires a float operand"`

## No silent coercion

The evaluator never coerces `Bool` to `Float` or `Float` to `Bool`. In particular:

- A bool nested directive cannot be used as an indicator input requiring a float
  series.
- A float series cannot be used in a logical operator.
- Indicator results are converted from FacioQuo `decimal` to `double` only inside the
  registry bridge; this is an explicit numeric conversion, not implicit coercion.

## Lookback

Each `IndicatorSpec.Lookback` returns the minimum number of warm-up rows for that
call. A compound expression accumulates lookback across nested indicators.

A planned public helper:

```fsharp
module Directive =
    val lookback: string -> int
```

Semantics:

- `Directive.lookback "ma:20"` returns the lookback for the indicator (typically
  `period - 1` for SMA-style finite windows; recursive indicators may differ).
- Nested expressions sum the lookbacks of all contained indicators.
- A column or number has lookback `0`.
- Binary operators take the maximum of child lookbacks; an indicator whose operand is
  a nested directive sums the operand lookback and its own.

This helper is part of the specification for warm-up clarity, not an incremental
refresh mechanism. v1 computes the whole series and leaves warm-up rows as `NaN` or
`false`.
