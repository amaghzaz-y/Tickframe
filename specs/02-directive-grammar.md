# Spec 02 — Directive Grammar

A directive is a single expression string. The grammar is intentionally small and
whitespace-tolerant.

## EBNF

```ebnf
directive     := expression

expression    := cross
cross         := logical ( crossOp logical )*
logical       := comparison ( logicalOp comparison )*
comparison    := additive ( comparisonOp additive )?
additive      := multiplicative ( additiveOp multiplicative )*
multiplicative := unary ( multiplicativeOp unary )*
unary         := ("-" | "~") unary
              |  primary

primary       := number
              |  column
              |  indicator
              |  "(" expression ")"

indicator     := command ("." sub)? (":" args)? ("@" seriesList)?

seriesList    := series ("," series)*
series        := column | "(" directive ")"

command       := identifier
sub           := identifier
column        := identifier
args          := arg ("," arg)*
arg           := "" | number | identifier
number        := float-literal
identifier    := (letter | "_") (letter | digit | "_")*

crossOp       := "//" | "\\" | "><"
logicalOp     := "&" | "|" | "^"
comparisonOp  := "<" | "<=" | "==" | "!=" | ">=" | ">"
additiveOp    := "+" | "-"
multiplicativeOp := "*" | "/"
```

## Tokens

Multi-character operators are single tokens:

| Token | Meaning |
| --- | --- |
| `//` | cross up (gold cross) |
| `\\` | cross down (dead cross) |
| `><` | cross either way |
| `>=` `<=` `==` `!=` | comparisons |
| `>` `<` | strict comparisons |
| `+` `-` `*` `/` | arithmetic |
| `&` `|` `^` | logical and / or / xor |
| `~` `-` | unary not / negate |
| `:` | argument separator after command |
| `@` | operand-series separator |
| `.` | sub-command separator |
| `,` | list separator |
| `(` `)` | grouping / nested directive |

Whitespace (spaces, tabs, newlines) is insignificant between tokens. A directive may
span multiple lines.

## Precedence

From tightest to loosest:

1. unary `-` `~`
2. `*` `/`
3. `+` `-`
4. comparisons `<` `<=` `==` `!=` `>=` `>`
5. logical `&` `^` `|`
6. cross `//` `\\` `><`

All binary operators are left-associative. Parentheses override precedence.

Notes:

- Logical operators share one precedence level; `&`, `^`, and `|` are parsed
  left-to-right with no precedence between them. This mirrors the compact grammar.
  Implementations that prefer conventional `&` > `^` > `|` precedence must document
  the divergence; the spec requires the EBNF above unless deliberately changed.
- Comparisons are non-associative in the grammar (at most one comparison per operand
  chain). Chained comparisons such as `a < b < c` are a syntax error and must be
  parenthesized.

## Commands, sub-commands, and columns

- `command` and `sub` are case-insensitive and canonicalized to lowercase.
- `column` names are also case-insensitive and canonicalized to lowercase for lookup;
  the valid set is `open`, `high`, `low`, `close`, `volume`.
- A bare identifier in `primary` is first resolved as a column; if it is not a column,
  it is treated as an indicator command with no args and no series operands.

This means `close` is a column read, while `rsi` is an indicator read (using the
registry's default operand series, normally `close`).

## Arguments

Arguments before `@` are positional scalar tokens. Empty slots are preserved:

```text
macd.signal:,,5
```

This parses to three arg tokens: `[""; ""; "5"]`.

An empty trailing token list is not distinguished from an absent `:args` at parse
time; the registry decides whether missing/empty slots are allowed.

## Operand series

The `@` list overrides the indicator's default input series.

```text
ma:10@open
ma:10@(ma:5)
macd.signal:,,5@close
```

Each `series` is either a bare column name or a parenthesized directive. A parenthesized
directive is parsed as a full expression.

## Examples

```text
close
rsi:14
rsi:14 > close
ma:20@close
ma:20 > ma:50
(ma:20 > ma:50)
ma:5 // ma:20
macd.signal:,,5
increase:3@(ma:20@close)
repeat:5@(close > boll.upper)
```

## Grammar edge cases

- `a >` is a syntax error (missing right operand).
- `a //` is a syntax error.
- `a & b | c` is legal and left-associative.
- `a < b < c` is a syntax error.
- `-x` and `~x` are legal unary expressions.
- `-5` is a number, not a unary-minus applied to `5`; both evaluate identically.
- `@` with no series (`rsi:14@`) is a syntax error.
- A series list containing an empty entry (`ma@open,,close`) is a syntax error.
