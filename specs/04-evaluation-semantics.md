# Spec 04 — Evaluation Semantics

## Runtime value model

The evaluator works over two homogeneous series types. There is no `object` series,
no string series, and no three-valued boolean.

```fsharp
type Series =
    | Float of float[]
    | Bool  of bool[]
```

Every `Series` has length exactly `OhlcvFrame.RowCount`.

## Entry point

```fsharp
module Directive =
    val eval: OhlcvFrame -> string -> Series
```

`Directive.eval` parses the string and evaluates the resulting `Expr`:

```fsharp
let eval frame directive =
    match DirectiveParser.parse directive with
    | Error err -> raise err
    | Ok expr   -> Evaluator.eval frame expr
```

`Directive.eval` is pure with respect to the frame: it creates no columns and mutates
nothing.

## Evaluation rules

### Number

- `Number n` -> `Float (Array.create rowCount n)`.

### Column

- `Column name` -> `Float (frame.Column name)`.

### Unary

| Op | Input | Output | Rule |
| --- | --- | --- | --- |
| `Negate` | `Float a` | `Float` | `-a[i]` |
| `Not` | `Bool a` | `Bool` | `not a[i]` |

Type mismatch raises `DirectiveValueError`.

### Binary arithmetic

`Add`, `Sub`, `Mul`, `Div` require two `Float` operands and return `Float`.

Division by zero follows IEEE double semantics (produces `Infinity` or `NaN`); it
does not raise.

NaN propagates through arithmetic.

### Binary comparison

`Lt`, `Le`, `Eq`, `Ne`, `Ge`, `Gt` require two `Float` operands and return `Bool`.

Comparisons use IEEE double semantics:

```fsharp
nan <  x  -> false
nan <= x  -> false
nan == x  -> false
nan != x  -> true
nan >  x  -> false
nan >= x  -> false
```

The result is therefore always a total `bool`, never `NaN`.

### Binary logical

`And`, `Or`, `Xor` require two `Bool` operands and return `Bool`:

```fsharp
And -> a[i] && b[i]
Or  -> a[i] || b[i]
Xor -> a[i] <> b[i]
```

There is no Kleene/three-valued logic in v1.

### Cross operators

Cross operators require two `Float` operands and return `Bool`.

For element `i = 0`, all cross results are `false`. For `i > 0`:

```fsharp
crossUp   a b i = a[i-1] <= b[i-1] && a[i] >  b[i]
crossDown a b i = a[i-1] >= b[i-1] && a[i] <  b[i]
crossAny  a b i = crossUp a b i || crossDown a b i
```

NaN participates with IEEE comparison semantics, so any NaN comparison yields false.

## Indicator evaluation

`Indicator call` delegates to the registry:

1. Resolve default or explicit operand series from `call.Series`.
2. Resolve argument slots against the indicator spec.
3. Invoke the FacioQuo batch method through the uniform bridge.
4. Convert the typed FacioQuo result to `Series`.

Warm-up rows before an indicator's lookback are `NaN` for `Float` outputs. Boolean
outputs from indicators (e.g. candlestick patterns) are total `bool`, with
non-matching/insufficient rows `false`.

Indicator evaluation is batch-only. There is no cached column, stale state, append, or
fulfill in v1.

## Type compatibility summary

| Operator | Left | Right | Result |
| --- | --- | --- | --- |
| `+ - * /` | Float | Float | Float |
| `< <= == != >= >` | Float | Float | Bool |
| `& \| ^` | Bool | Bool | Bool |
| `// \\ ><` | Float | Float | Bool |
| `-` unary | Float | — | Float |
| `~` unary | Bool | — | Bool |

Any other combination is a `DirectiveValueError`.

## Determinism

- Results are deterministic for a fixed input frame and directive string.
- `Directive.eval` must not depend on global mutable state, thread-local state, or
  evaluation order beyond the AST traversal order.
