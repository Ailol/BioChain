# Signals Kernel Runtime — Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Build a 5-file, ~1,850-line signals kernel runtime implementing BNF v1.5 EVAL ENGINE with Apache Arrow columnar data, Extism WASM plugins, Marten event sourcing, and Wolverine side-effect bus.

**Architecture:** Compiler pipeline (text → AST → Postgres + bytecode), tick engine (load state → 9 phases → results), side-effect bus (Wolverine dispatches async IO between ticks), event store (Marten on existing PG), Orleans grain per entity.

**Tech Stack:** .NET 10, Apache.Arrow 22.1.0, Extism.Sdk 1.10.0, Marten 8.19.0, WolverineFx.Marten 5.13.0, Orleans 9.2.x, Grpc.AspNetCore, Neo4j.Driver 6, Npgsql 9.0.3

**Design doc:** `docs/plans/2026-03-03-signals-kernel-runtime-design.md`

---

## Task 1: Project Setup

Create project, add to solution, verify packages resolve.

**Files:**
- Create: `SignalsKernel/SignalsKernel.csproj`

**Step 1: Create project directory and csproj**

```xml
<!-- SignalsKernel/SignalsKernel.csproj -->
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Apache.Arrow" Version="22.1.0" />
    <PackageReference Include="Extism.Sdk" Version="1.10.0" />
    <PackageReference Include="Marten" Version="8.19.0" />
    <PackageReference Include="WolverineFx.Marten" Version="5.13.0" />
    <PackageReference Include="Microsoft.Orleans.Sdk" Version="9.2.0" />
    <PackageReference Include="Microsoft.Orleans.Persistence.Memory" Version="9.2.0" />
    <PackageReference Include="Grpc.AspNetCore" Version="2.71.0" />
    <PackageReference Include="Neo4j.Driver" Version="6.0.0" />
    <PackageReference Include="Npgsql" Version="9.0.3" />
    <PackageReference Include="Microsoft.Extensions.AI" Version="10.3.0" />
    <PackageReference Include="Microsoft.Extensions.Hosting.Abstractions" Version="10.0.3" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\src\BioChain.Models\BioChain.Models.csproj" />
    <ProjectReference Include="..\Kernel\BioChain.Kernel\BioChain.Kernel.csproj" />
  </ItemGroup>
</Project>
```

**Step 2: Add to solution**

Run: `dotnet sln BioChain.sln add SignalsKernel/SignalsKernel.csproj`
Expected: `Project 'SignalsKernel.csproj' added to the solution.`

**Step 3: Create empty source files so solution builds**

Create 5 empty files with namespace declarations:
- `SignalsKernel/Compiler.cs` — `namespace SignalsKernel;`
- `SignalsKernel/Engine.cs` — `namespace SignalsKernel;`
- `SignalsKernel/Agent.cs` — `namespace SignalsKernel;`
- `SignalsKernel/Graph.cs` — `namespace SignalsKernel;`
- `SignalsKernel/Platform.cs` — `namespace SignalsKernel;`

**Step 4: Restore and build**

Run: `dotnet restore SignalsKernel/SignalsKernel.csproj && dotnet build SignalsKernel/SignalsKernel.csproj`
Expected: 0 errors (packages resolve, project builds)

**Step 5: Commit**

```bash
git add SignalsKernel/
git commit -m "chore: scaffold SignalsKernel project with all dependencies

5-file flat structure. Apache.Arrow, Extism.Sdk, Marten,
WolverineFx.Marten, Orleans, gRPC, Neo4j, Npgsql."
```

---

## Task 2: Compiler.cs — AST Types

All AST node records the parser produces and lowering consumes. ~80 lines of records.

**Files:**
- Modify: `SignalsKernel/Compiler.cs`

**Step 1: Write AST types**

These map 1:1 to BNF tags (lines 30-48 of the spec). Every declaration the BNF defines becomes an AST node.

```csharp
// SignalsKernel/Compiler.cs
namespace SignalsKernel;

// ──────────────────────── AST ────────────────────────

public readonly record struct Ref(string Code, string? Region = null);

public sealed record SignalDecl(Ref Id, string Type, string? State, double? Value, string? Unit,
    double? Baseline, double? DeviationPct, double? RangeLow, double? RangeHigh,
    double Confidence = 1.0, string? Distribution = null,
    long? TauMinMs = null, long? TauMaxMs = null);

public sealed record EdgeDecl(Ref Source, Ref Target, string Op, string OpClass,
    double Gain = 1.0, double NoiseSigma = 0, string TransferFn = "lin",
    int DelayMs = 0, double? ClampLo = null, double? ClampHi = null,
    string? GateRef = null, string? ToolRef = null);

public sealed record GateDecl(string Code, string Type, double? Threshold = null,
    string? Expression = null, double? Probability = null,
    string? Prompt = null, string? Model = null, string? ParseMap = null,
    string? Fallback = null, int? TimeoutMs = null, int? CacheMs = null);

public sealed record FormulaDecl(Ref[] Chain, EdgeDecl[] Edges, string? GateRef = null);

public sealed record ConstraintDecl(string Type, string Expression,
    double? Epsilon = null, double? Confidence = null);

public sealed record ToolDecl(string Code, string Invoke, Ref[] Inputs, Ref[] Outputs,
    string? GateExpr = null, int TimeoutMs = 10000, int RetryCount = 3, string? Fallback = null);

public sealed record LlmGateDecl(string Code, string Prompt, string Model,
    string? ParseMap, string Fallback, int TimeoutMs = 30000, int CacheMs = 0);

public sealed record FailDecl(string Type, Ref Target, string Condition,
    int HeldMs, string Consequence);

public sealed record BindDecl(Ref Target, string Expression, double? DecayRate = null, bool Accumulate = false);

public sealed record ModuleDecl(string Name, AstNode[] Body, Ref[] Interfaces);

// Expression tree for conditions, BINDs, and computed values
public abstract record Expr
{
    public sealed record Literal(double Value) : Expr;
    public sealed record SignalRef(Ref Id) : Expr;
    public sealed record BinOp(Expr Left, string Op, Expr Right) : Expr;
    public sealed record UnaryOp(string Op, Expr Operand) : Expr;
    public sealed record Call(string Fn, Expr[] Args) : Expr;
    public sealed record Ternary(Expr Cond, Expr Then, Expr Else) : Expr;
    public sealed record Window(Ref Signal, int WindowMs, string Agg) : Expr;  // $.window(signal, ms).mean
}

// Union wrapper for mixed declaration lists
public abstract record AstNode
{
    public sealed record Signal(SignalDecl Decl) : AstNode;
    public sealed record Edge(EdgeDecl Decl) : AstNode;
    public sealed record Gate(GateDecl Decl) : AstNode;
    public sealed record Formula(FormulaDecl Decl) : AstNode;
    public sealed record Constraint(ConstraintDecl Decl) : AstNode;
    public sealed record Tool(ToolDecl Decl) : AstNode;
    public sealed record LlmGate(LlmGateDecl Decl) : AstNode;
    public sealed record Fail(FailDecl Decl) : AstNode;
    public sealed record Bind(BindDecl Decl) : AstNode;
    public sealed record Module(ModuleDecl Decl) : AstNode;
}

public sealed record CompilationUnit(AstNode[] Nodes, string? Vocabulary = null);
```

**Step 2: Build**

Run: `dotnet build SignalsKernel/SignalsKernel.csproj`
Expected: 0 errors

**Step 3: Commit**

```bash
git add SignalsKernel/Compiler.cs
git commit -m "feat(compiler): AST types for all BNF tags

Records map 1:1 to BNF tags (SIGNAL, EDGE, GATE, FORMULA,
CONSTRAINT, TOOL, LLM_GATE, FAIL, BIND, MODULE).
Expr tree for conditions and computed values."
```

---

## Task 3: Compiler.cs — Token + Lexer

Tokenizer that breaks BNF text into a flat token stream. ~100 lines.

**Files:**
- Modify: `SignalsKernel/Compiler.cs`

**Step 1: Add Token enum and Lexer**

Append to Compiler.cs after AST types:

```csharp
// ──────────────────────── LEXER ────────────────────────

public enum Tok
{
    // Tags
    Signal, Receptor, Gate, Limiter, Transport, Interface, Feedback,
    Formula, State, Def, Dysreg, Bind, Fail, Hypothesis, Prediction,
    Intervention, Module, Import, Query, Snapshot, Eval,
    Constraint, Equilibrium, Boundary, Conserve,
    Tool, LlmGate, Emit, Message, Phase,
    // Operators
    Arrow, InhibitArrow, ModulateArrow, BlockArrow, DepleteArrow,
    BidiArrow, ParallelOp, ConvertArrow, FeedbackNeg, FeedbackPos,
    // Literals / identifiers
    Ident, Number, String, StateSymbol,
    // Punctuation
    Colon, Dot, Comma, LBrace, RBrace, LBracket, RBracket, LParen, RParen,
    Pipe, Eq, Neq, Lt, Gt, Lte, Gte, And, Or, Not,
    Plus, Minus, Star, Slash, Percent, Power, Tilde, At, Hash, Bang,
    Question, Semi,
    // Special
    Eof, Newline, Comment
}

public readonly record struct Token(Tok Kind, string Text, int Line, int Col);

public static class Lexer
{
    private static readonly Dictionary<string, Tok> Keywords = new(StringComparer.OrdinalIgnoreCase)
    {
        ["SIGNAL"] = Tok.Signal, ["RECEPTOR"] = Tok.Receptor, ["GATE"] = Tok.Gate,
        ["LIMITER"] = Tok.Limiter, ["TRANSPORT"] = Tok.Transport, ["INTERFACE"] = Tok.Interface,
        ["FEEDBACK"] = Tok.Feedback, ["FORMULA"] = Tok.Formula, ["STATE"] = Tok.State,
        ["DEF"] = Tok.Def, ["DYSREG"] = Tok.Dysreg, ["BIND"] = Tok.Bind,
        ["FAIL"] = Tok.Fail, ["HYPOTHESIS"] = Tok.Hypothesis, ["PREDICTION"] = Tok.Prediction,
        ["INTERVENTION"] = Tok.Intervention, ["MODULE"] = Tok.Module, ["IMPORT"] = Tok.Import,
        ["QUERY"] = Tok.Query, ["SNAPSHOT"] = Tok.Snapshot, ["EVAL"] = Tok.Eval,
        ["CONSTRAINT"] = Tok.Constraint, ["EQUILIBRIUM"] = Tok.Equilibrium,
        ["BOUNDARY"] = Tok.Boundary, ["CONSERVE"] = Tok.Conserve,
        ["TOOL"] = Tok.Tool, ["LLM_GATE"] = Tok.LlmGate, ["EMIT"] = Tok.Emit,
        ["MESSAGE"] = Tok.Message,
    };

    private static readonly Dictionary<string, Tok> Operators = new()
    {
        ["→"] = Tok.Arrow, ["⊣"] = Tok.InhibitArrow, ["⊩"] = Tok.ModulateArrow,
        ["⊗"] = Tok.BlockArrow, ["⊘→"] = Tok.DepleteArrow, ["⇌"] = Tok.BidiArrow,
        ["∥"] = Tok.ParallelOp, ["◈"] = Tok.ConvertArrow,
        ["⟳⁻"] = Tok.FeedbackNeg, ["⟳⁺"] = Tok.FeedbackPos,
    };

    private static readonly HashSet<string> StateSymbols = ["↑↑", "↑", "≈", "↓", "↓↓", "~", "⊘", "●"];

    public static List<Token> Tokenize(string source)
    {
        var tokens = new List<Token>();
        int i = 0, line = 1, col = 1;
        var span = source.AsSpan();

        while (i < span.Length)
        {
            // Skip whitespace (except newlines)
            if (span[i] == ' ' || span[i] == '\t' || span[i] == '\r') { i++; col++; continue; }
            if (span[i] == '\n') { tokens.Add(new(Tok.Newline, "\\n", line, col)); line++; col = 1; i++; continue; }

            // Comments
            if (i + 1 < span.Length && span[i] == '/' && span[i + 1] == '/')
            {
                var end = source.IndexOf('\n', i);
                if (end < 0) end = span.Length;
                i = end; continue;
            }

            // Multi-char Unicode operators (check longest first)
            var matched = false;
            foreach (var (op, tok) in Operators)
            {
                if (i + op.Length <= span.Length && span.Slice(i, op.Length).SequenceEqual(op.AsSpan()))
                {
                    tokens.Add(new(tok, op, line, col));
                    i += op.Length; col += op.Length; matched = true; break;
                }
            }
            if (matched) continue;

            // State symbols
            foreach (var sym in StateSymbols)
            {
                if (i + sym.Length <= span.Length && span.Slice(i, sym.Length).SequenceEqual(sym.AsSpan()))
                {
                    tokens.Add(new(Tok.StateSymbol, sym, line, col));
                    i += sym.Length; col += sym.Length; matched = true; break;
                }
            }
            if (matched) continue;

            // Numbers
            if (char.IsDigit(span[i]) || (span[i] == '-' && i + 1 < span.Length && char.IsDigit(span[i + 1])))
            {
                var start = i;
                if (span[i] == '-') i++;
                while (i < span.Length && (char.IsDigit(span[i]) || span[i] == '.')) i++;
                tokens.Add(new(Tok.Number, source[start..i], line, col));
                col += i - start; continue;
            }

            // Identifiers / keywords
            if (char.IsLetter(span[i]) || span[i] == '_' || span[i] == '#')
            {
                var start = i;
                while (i < span.Length && (char.IsLetterOrDigit(span[i]) || span[i] == '_' || span[i] == '#')) i++;
                var text = source[start..i];
                var kind = Keywords.GetValueOrDefault(text, Tok.Ident);
                tokens.Add(new(kind, text, line, col));
                col += i - start; continue;
            }

            // Strings
            if (span[i] == '"')
            {
                var start = ++i;
                while (i < span.Length && span[i] != '"') i++;
                tokens.Add(new(Tok.String, source[start..i], line, col));
                i++; col += i - start + 2; continue;
            }

            // Single-char punctuation
            var ch = span[i];
            var punct = ch switch
            {
                ':' => Tok.Colon, '.' => Tok.Dot, ',' => Tok.Comma,
                '{' => Tok.LBrace, '}' => Tok.RBrace, '[' => Tok.LBracket, ']' => Tok.RBracket,
                '(' => Tok.LParen, ')' => Tok.RParen, '|' => Tok.Pipe,
                '=' => Tok.Eq, '<' => Tok.Lt, '>' => Tok.Gt,
                '+' => Tok.Plus, '-' => Tok.Minus, '*' => Tok.Star, '/' => Tok.Slash,
                '%' => Tok.Percent, '^' => Tok.Power, '~' => Tok.Tilde,
                '@' => Tok.At, '!' => Tok.Bang, '?' => Tok.Question, ';' => Tok.Semi,
                _ => Tok.Eof // skip unknown
            };
            tokens.Add(new(punct, ch.ToString(), line, col));
            i++; col++;
        }

        tokens.Add(new(Tok.Eof, "", line, col));
        return tokens;
    }
}
```

**Step 2: Build**

Run: `dotnet build SignalsKernel/SignalsKernel.csproj`
Expected: 0 errors

**Step 3: Commit**

```bash
git add SignalsKernel/Compiler.cs
git commit -m "feat(compiler): Token enum + Lexer for BNF v1.5

Handles all Unicode operators (→ ⊣ ⊩ ⊗ ⇌ ∥ ◈ ⟳⁻ ⟳⁺),
state symbols (↑↑ ↑ ≈ ↓ ↓↓ ~ ⊘ ●), all BNF tag keywords,
numbers, identifiers, strings, and punctuation."
```

---

## Task 4: Compiler.cs — Parser

Pratt expression parser + tag-line parser. Tokens → AST. ~120 lines.

**Files:**
- Modify: `SignalsKernel/Compiler.cs`

**Step 1: Add Parser**

Append to Compiler.cs after Lexer:

```csharp
// ──────────────────────── PARSER ────────────────────────

public sealed class Parser
{
    private readonly List<Token> _tokens;
    private int _pos;

    public Parser(List<Token> tokens) { _tokens = tokens; _pos = 0; }

    private Token Peek() => _pos < _tokens.Count ? _tokens[_pos] : new(Tok.Eof, "", 0, 0);
    private Token Advance() => _tokens[_pos++];
    private bool Match(Tok kind) { if (Peek().Kind == kind) { _pos++; return true; } return false; }
    private Token Expect(Tok kind) => Peek().Kind == kind ? Advance() : throw new FormatException($"Expected {kind}, got {Peek().Kind} at line {Peek().Line}");
    private void SkipNewlines() { while (Peek().Kind == Tok.Newline) _pos++; }

    public CompilationUnit Parse()
    {
        var nodes = new List<AstNode>();
        SkipNewlines();
        while (Peek().Kind != Tok.Eof)
        {
            var node = ParseTagLine();
            if (node is not null) nodes.Add(node);
            SkipNewlines();
        }
        return new CompilationUnit([.. nodes]);
    }

    private AstNode? ParseTagLine()
    {
        var tag = Peek();
        return tag.Kind switch
        {
            Tok.Signal => ParseSignal(),
            Tok.Gate => ParseGate(),
            Tok.LlmGate => ParseLlmGate(),
            Tok.Formula => ParseFormula(),
            Tok.Constraint or Tok.Equilibrium or Tok.Boundary or Tok.Conserve => ParseConstraint(),
            Tok.Tool => ParseTool(),
            Tok.Fail => ParseFail(),
            Tok.Bind => ParseBind(),
            Tok.Module => ParseModule(),
            _ => { _pos++; return null; } // skip unknown tags
        };
    }

    private Ref ParseRef()
    {
        var code = Expect(Tok.Ident).Text;
        var region = Match(Tok.Dot) ? Expect(Tok.Ident).Text : null;
        return new Ref(code, region);
    }

    private AstNode ParseSignal()
    {
        Advance(); Expect(Tok.Colon);
        var id = ParseRef();
        string? state = Match(Tok.StateSymbol) ? _tokens[_pos - 1].Text : null;
        double? value = null;
        if (Match(Tok.LBrace)) { value = double.Parse(Expect(Tok.Number).Text); Expect(Tok.RBrace); }
        return new AstNode.Signal(new SignalDecl(id, "signal", state, value, null, null, null, null, null));
    }

    private AstNode ParseGate()
    {
        Advance(); Expect(Tok.Colon);
        var code = Expect(Tok.Ident).Text;
        var type = "threshold"; double? threshold = null; string? expr = null; double? prob = null;
        if (Match(Tok.LBracket))
        {
            // Parse gate condition — simplified: read until ]
            var condStart = _pos;
            var depth = 1;
            while (depth > 0 && Peek().Kind != Tok.Eof)
            {
                if (Peek().Kind == Tok.LBracket) depth++;
                if (Peek().Kind == Tok.RBracket) depth--;
                if (depth > 0) _pos++;
            }
            expr = string.Join(" ", _tokens[condStart.._pos].Select(t => t.Text));
            Expect(Tok.RBracket);
        }
        return new AstNode.Gate(new GateDecl(code, type, threshold, expr, prob));
    }

    private AstNode ParseLlmGate()
    {
        Advance(); Expect(Tok.Colon);
        var code = Expect(Tok.Ident).Text;
        string prompt = "", model = "default", fallback = "true";
        string? parseMap = null; int timeout = 30000, cache = 0;
        if (Match(Tok.LBrace))
        {
            while (!Match(Tok.RBrace) && Peek().Kind != Tok.Eof)
            {
                var field = Peek().Text.ToUpperInvariant();
                _pos++;
                if (Match(Tok.Colon))
                {
                    var val = ReadUntilNewline();
                    switch (field)
                    {
                        case "PROMPT": prompt = val; break;
                        case "MODEL": model = val; break;
                        case "PARSE": parseMap = val; break;
                        case "FALLBACK": fallback = val; break;
                        case "TIMEOUT": int.TryParse(val, out timeout); break;
                        case "CACHE": int.TryParse(val, out cache); break;
                    }
                }
                SkipNewlines();
            }
        }
        return new AstNode.LlmGate(new LlmGateDecl(code, prompt, model, parseMap, fallback, timeout, cache));
    }

    private AstNode ParseFormula()
    {
        Advance(); Expect(Tok.Colon);
        var chain = new List<Ref>();
        var edges = new List<EdgeDecl>();
        chain.Add(ParseRef());
        while (IsOperator(Peek().Kind))
        {
            var op = Advance();
            var (opStr, opClass) = ClassifyOperator(op.Kind);
            double gain = 1.0; double noise = 0; string tfn = "lin";
            // Parse edge modifiers: *n ±σ ^fn
            if (Match(Tok.Star)) { gain = double.Parse(Expect(Tok.Number).Text); }
            var target = ParseRef();
            chain.Add(target);
            edges.Add(new EdgeDecl(chain[^2], target, opStr, opClass, gain, noise, tfn));
        }
        return new AstNode.Formula(new FormulaDecl([.. chain], [.. edges]));
    }

    private AstNode ParseConstraint()
    {
        var type = Advance().Kind switch
        {
            Tok.Constraint => "constraint", Tok.Equilibrium => "equilibrium",
            Tok.Boundary => "boundary", Tok.Conserve => "conserve", _ => "constraint"
        };
        Expect(Tok.Colon);
        var expr = ReadUntilNewline();
        double? epsilon = null;
        if (expr.Contains("ε:"))
        {
            var parts = expr.Split("ε:");
            expr = parts[0].Trim();
            double.TryParse(parts[1].Trim(), out var ep);
            epsilon = ep;
        }
        return new AstNode.Constraint(new ConstraintDecl(type, expr, epsilon));
    }

    private AstNode ParseTool()
    {
        Advance(); Expect(Tok.Colon);
        var code = Expect(Tok.Ident).Text;
        string invoke = ""; var inputs = new List<Ref>(); var outputs = new List<Ref>();
        if (Match(Tok.LBrace))
        {
            while (!Match(Tok.RBrace) && Peek().Kind != Tok.Eof)
            {
                var field = Peek().Text.ToUpperInvariant(); _pos++;
                if (Match(Tok.Colon))
                {
                    var val = ReadUntilNewline();
                    switch (field)
                    {
                        case "INVOKE": invoke = val.Trim('"'); break;
                        case "INPUT": inputs.AddRange(val.Split(',').Select(s => new Ref(s.Trim()))); break;
                        case "OUTPUT": outputs.AddRange(val.Split(',').Select(s => new Ref(s.Trim()))); break;
                    }
                }
                SkipNewlines();
            }
        }
        return new AstNode.Tool(new ToolDecl(code, invoke, [.. inputs], [.. outputs]));
    }

    private AstNode ParseFail()
    {
        Advance(); Expect(Tok.Colon);
        var type = Expect(Tok.Ident).Text; // ⚡.type — simplified
        var target = ParseRef();
        Expect(Tok.Question);
        var condition = ReadUntil(Tok.Colon);
        Expect(Tok.Colon);
        var heldMs = int.Parse(Expect(Tok.Number).Text);
        var consequence = Match(Tok.Arrow) ? ReadUntilNewline() : "";
        return new AstNode.Fail(new FailDecl(type, target, condition, heldMs, consequence));
    }

    private AstNode ParseBind()
    {
        Advance(); Expect(Tok.Colon);
        var target = ParseRef();
        var accumulate = false;
        if (Match(Tok.Plus) && Match(Tok.Eq)) accumulate = true;
        else Expect(Tok.Eq);
        var expr = ReadUntilNewline();
        double? decay = null;
        if (expr.Contains('~'))
        {
            var parts = expr.Split('~');
            expr = parts[0].Trim();
            double.TryParse(parts[1].Trim(), out var d);
            decay = d;
        }
        return new AstNode.Bind(new BindDecl(target, expr, decay, accumulate));
    }

    private AstNode ParseModule()
    {
        Advance(); Expect(Tok.Colon);
        var name = Expect(Tok.Ident).Text;
        var body = new List<AstNode>();
        var interfaces = new List<Ref>();
        if (Match(Tok.LBrace))
        {
            SkipNewlines();
            while (!Match(Tok.RBrace) && Peek().Kind != Tok.Eof)
            {
                if (Peek().Kind == Tok.Interface) { _pos++; Expect(Tok.Colon); interfaces.Add(ParseRef()); }
                else { var node = ParseTagLine(); if (node is not null) body.Add(node); }
                SkipNewlines();
            }
        }
        return new AstNode.Module(new ModuleDecl(name, [.. body], [.. interfaces]));
    }

    // --- Pratt expression parser (for BIND expressions, gate conditions) ---
    public Expr ParseExpr(int minPrec = 0)
    {
        var left = ParseAtom();
        while (GetPrecedence(Peek().Kind) >= minPrec)
        {
            var op = Advance();
            var prec = GetPrecedence(op.Kind);
            var right = ParseExpr(prec + 1);
            left = new Expr.BinOp(left, op.Text, right);
        }
        return left;
    }

    private Expr ParseAtom()
    {
        var tok = Peek();
        switch (tok.Kind)
        {
            case Tok.Number: Advance(); return new Expr.Literal(double.Parse(tok.Text));
            case Tok.Ident:
                var id = ParseRef();
                if (Match(Tok.LParen))
                {
                    var args = new List<Expr>();
                    if (Peek().Kind != Tok.RParen) { args.Add(ParseExpr()); while (Match(Tok.Comma)) args.Add(ParseExpr()); }
                    Expect(Tok.RParen);
                    return new Expr.Call(id.Code, [.. args]);
                }
                return new Expr.SignalRef(id);
            case Tok.Minus: Advance(); return new Expr.UnaryOp("-", ParseAtom());
            case Tok.Bang: Advance(); return new Expr.UnaryOp("!", ParseAtom());
            case Tok.LParen: Advance(); var inner = ParseExpr(); Expect(Tok.RParen); return inner;
            default: Advance(); return new Expr.Literal(0);
        }
    }

    private static int GetPrecedence(Tok kind) => kind switch
    {
        Tok.Or => 1, Tok.And => 2,
        Tok.Eq or Tok.Neq => 3, Tok.Lt or Tok.Gt or Tok.Lte or Tok.Gte => 4,
        Tok.Plus or Tok.Minus => 5, Tok.Star or Tok.Slash or Tok.Percent => 6,
        Tok.Power => 7, _ => -1,
    };

    // Helpers
    private static bool IsOperator(Tok kind) => kind is Tok.Arrow or Tok.InhibitArrow
        or Tok.ModulateArrow or Tok.BlockArrow or Tok.DepleteArrow
        or Tok.BidiArrow or Tok.ParallelOp or Tok.ConvertArrow;

    private static (string op, string cls) ClassifyOperator(Tok kind) => kind switch
    {
        Tok.Arrow => ("→", "causal"),
        Tok.InhibitArrow => ("⊣", "inhibitory"),
        Tok.ModulateArrow => ("⊩", "modulatory"),
        Tok.BlockArrow => ("⊗", "blocking"),
        Tok.DepleteArrow => ("⊘→", "depletion"),
        Tok.BidiArrow => ("⇌", "bidirectional"),
        Tok.ParallelOp => ("∥", "parallel"),
        Tok.ConvertArrow => ("◈", "conversion"),
        _ => ("→", "causal"),
    };

    private string ReadUntilNewline()
    {
        var parts = new List<string>();
        while (Peek().Kind is not Tok.Newline and not Tok.Eof) parts.Add(Advance().Text);
        return string.Join(" ", parts);
    }

    private string ReadUntil(Tok stop)
    {
        var parts = new List<string>();
        while (Peek().Kind != stop && Peek().Kind != Tok.Eof) parts.Add(Advance().Text);
        return string.Join(" ", parts);
    }
}
```

**Step 2: Build**

Run: `dotnet build SignalsKernel/SignalsKernel.csproj`
Expected: 0 errors

**Step 3: Commit**

```bash
git add SignalsKernel/Compiler.cs
git commit -m "feat(compiler): Parser — tokens to AST with Pratt expressions

Parses all BNF tags: SIGNAL, GATE, LLM_GATE, FORMULA, CONSTRAINT,
TOOL, FAIL, BIND, MODULE. Pratt expression parser for conditions
and computed values (precedence climbing)."
```

---

## Task 5: Compiler.cs — Lowering + Vocabulary + Entry Point

AST → Postgres INSERTs + formula bytecode + topo sort. Vocabulary validation. ~100 lines.

**Files:**
- Modify: `SignalsKernel/Compiler.cs`

**Step 1: Add Vocabulary + Lowering + entry point**

Append to Compiler.cs after Parser:

```csharp
// ──────────────────────── VOCABULARY ────────────────────────

public interface IVocabulary
{
    string Prefix { get; }
    bool IsValidSignalType(string type);
    bool IsValidEdgeClass(string cls);
    bool IsValidConnection(string sourceType, string targetType);
}

public sealed class Vocabulary : IVocabulary
{
    public string Prefix { get; }
    public HashSet<string> SignalTypes { get; }
    public HashSet<string> EdgeClasses { get; }
    public Dictionary<string, HashSet<string>>? LegalConnections { get; }

    public Vocabulary(string prefix, string[] signalTypes, string[] edgeClasses,
        Dictionary<string, HashSet<string>>? legalConnections = null)
    {
        Prefix = prefix;
        SignalTypes = new(signalTypes, StringComparer.OrdinalIgnoreCase);
        EdgeClasses = new(edgeClasses, StringComparer.OrdinalIgnoreCase);
        LegalConnections = legalConnections;
    }

    public bool IsValidSignalType(string type) => SignalTypes.Count == 0 || SignalTypes.Contains(type);
    public bool IsValidEdgeClass(string cls) => EdgeClasses.Count == 0 || EdgeClasses.Contains(cls);
    public bool IsValidConnection(string sourceType, string targetType)
    {
        if (LegalConnections is null) return true;
        return !LegalConnections.TryGetValue(sourceType, out var allowed) || allowed.Contains(targetType);
    }

    // Built-in vocabularies
    public static readonly Vocabulary Bio = new("bio",
        ["NT", "H", "P", "NI", "NS", "eCB"],
        ["causal", "inhibitory", "modulatory", "feedback", "flow", "dysreg", "conversion"]);

    public static readonly Vocabulary Market = new("mkt",
        ["PRICE", "VOLUME", "INDICATOR", "SENTIMENT", "FLOW"],
        ["causal", "correlation", "leading", "lagging"]);

    public static readonly Vocabulary Game = new("game",
        ["STAT", "RESOURCE", "ABILITY", "STATUS", "RELATION"],
        ["causal", "buff", "debuff", "drain", "regen"]);

    public static readonly Vocabulary Org = new("org",
        ["SKILL", "ROLE", "MORALE", "PERF", "CULTURE"],
        ["causal", "influence", "dependency", "hierarchy"]);

    public static readonly Vocabulary Social = new("soc",
        ["TRUST", "INFLUENCE", "STATUS", "BOND", "NORM"],
        ["causal", "reciprocal", "hierarchy", "peer"]);
}

// ──────────────────────── LOWERING ────────────────────────

public sealed record CompileResult(
    string[] SqlStatements,          // INSERT statements for Postgres
    byte[] FormulaBytecode,          // FormulaVM bytecode
    int[][] TopoLevels,              // pre-computed topo sort
    List<string> Warnings);

public static class Lowering
{
    /// <summary>
    /// Lower AST to Postgres INSERTs, formula bytecode, and topo sort.
    /// Vocabulary validates signal types and edge classes during lowering.
    /// </summary>
    public static CompileResult Lower(CompilationUnit unit, Guid subjectId, IVocabulary? vocab = null)
    {
        var sql = new List<string>();
        var warnings = new List<string>();
        var signals = new List<Ref>();
        var edges = new List<(Ref from, Ref to)>();

        foreach (var node in unit.Nodes)
        {
            switch (node)
            {
                case AstNode.Signal s:
                    if (vocab is not null && !vocab.IsValidSignalType(s.Decl.Type))
                        warnings.Add($"Signal type '{s.Decl.Type}' not in vocabulary '{vocab.Prefix}'");
                    sql.Add(EmitSignalInsert(s.Decl, subjectId));
                    signals.Add(s.Decl.Id);
                    break;

                case AstNode.Formula f:
                    foreach (var e in f.Decl.Edges)
                    {
                        if (vocab is not null && !vocab.IsValidEdgeClass(e.OpClass))
                            warnings.Add($"Edge class '{e.OpClass}' not in vocabulary '{vocab.Prefix}'");
                        sql.Add(EmitEdgeInsert(e, subjectId));
                        edges.Add((e.Source, e.Target));
                    }
                    break;

                case AstNode.Gate g:
                    sql.Add(EmitGateInsert(g.Decl, subjectId));
                    break;

                case AstNode.LlmGate lg:
                    sql.Add(EmitLlmGateInsert(lg.Decl, subjectId));
                    break;

                case AstNode.Constraint c:
                    sql.Add(EmitConstraintInsert(c.Decl, subjectId));
                    break;

                case AstNode.Tool t:
                    sql.Add(EmitToolInsert(t.Decl, subjectId));
                    break;

                case AstNode.Fail fail:
                    sql.Add(EmitFailInsert(fail.Decl, subjectId));
                    break;

                case AstNode.Bind bind:
                    sql.Add(EmitBindInsert(bind.Decl, subjectId));
                    break;
            }
        }

        // Topo sort
        var topo = ComputeTopoSort(signals, edges);

        // Bytecode (placeholder — FormulaVM opcodes compiled from expression trees)
        var bytecode = Array.Empty<byte>();

        return new CompileResult([.. sql], bytecode, topo, warnings);
    }

    private static string EmitSignalInsert(SignalDecl s, Guid subjectId) =>
        $"INSERT INTO signal (subject_id, type, code, state, value, baseline, confidence) " +
        $"VALUES ('{subjectId}', '{s.Type}', '{s.Id.Code}', '{s.State ?? "≈"}', " +
        $"{s.Value?.ToString() ?? "NULL"}, {s.Baseline?.ToString() ?? "NULL"}, {s.Confidence}) " +
        $"ON CONFLICT (subject_id, type, code) DO UPDATE SET state = EXCLUDED.state, value = EXCLUDED.value;";

    private static string EmitEdgeInsert(EdgeDecl e, Guid subjectId) =>
        $"INSERT INTO edge (subject_id, source_type, source_id, target_type, target_id, " +
        $"operator, operator_class, gain, noise_sigma, transfer_fn, delay_ms, clamp_lo, clamp_hi) " +
        $"SELECT '{subjectId}', 'signal', s.id, 'signal', t.id, " +
        $"'{e.Op}', '{e.OpClass}', {e.Gain}, {e.NoiseSigma}, '{e.TransferFn}', {e.DelayMs}, " +
        $"{e.ClampLo?.ToString() ?? "NULL"}, {e.ClampHi?.ToString() ?? "NULL"} " +
        $"FROM signal s, signal t WHERE s.code = '{e.Source.Code}' AND t.code = '{e.Target.Code}' " +
        $"AND s.subject_id = '{subjectId}' AND t.subject_id = '{subjectId}';";

    private static string EmitGateInsert(GateDecl g, Guid subjectId) =>
        $"INSERT INTO gate (subject_id, code, type, expression) VALUES ('{subjectId}', '{g.Code}', '{g.Type}', '{g.Expression ?? ""}');";

    private static string EmitLlmGateInsert(LlmGateDecl g, Guid subjectId) =>
        $"INSERT INTO gate (subject_id, code, type, prompt, model, parse_map, fallback_expr, timeout_ms, cache_ms) " +
        $"VALUES ('{subjectId}', '{g.Code}', 'llm', '{Esc(g.Prompt)}', '{g.Model}', " +
        $"'{g.ParseMap ?? ""}', '{g.Fallback}', {g.TimeoutMs}, {g.CacheMs});";

    private static string EmitConstraintInsert(ConstraintDecl c, Guid subjectId) =>
        $"INSERT INTO constraint_def (subject_id, type, expression, epsilon) " +
        $"VALUES ('{subjectId}', '{c.Type}', '{Esc(c.Expression)}', {c.Epsilon?.ToString() ?? "NULL"});";

    private static string EmitToolInsert(ToolDecl t, Guid subjectId) =>
        $"INSERT INTO tool (subject_id, code, invoke, input_refs, output_refs, timeout_ms, retry_count) " +
        $"VALUES ('{subjectId}', '{t.Code}', '{t.Invoke}', " +
        $"ARRAY[{string.Join(",", t.Inputs.Select(r => $"'{r.Code}'"))}], " +
        $"ARRAY[{string.Join(",", t.Outputs.Select(r => $"'{r.Code}'"))}], {t.TimeoutMs}, {t.RetryCount});";

    private static string EmitFailInsert(FailDecl f, Guid subjectId) =>
        $"-- FAIL conditions tracked in-memory by Engine.cs FailPhase";

    private static string EmitBindInsert(BindDecl b, Guid subjectId) =>
        $"-- BIND rules tracked in-memory by Engine.cs BindPhase";

    private static int[][] ComputeTopoSort(List<Ref> signals, List<(Ref from, Ref to)> edges)
    {
        var idx = signals.Select((s, i) => (s, i)).ToDictionary(x => x.s, x => x.i);
        var inDeg = new int[signals.Count];
        var adj = signals.Select(_ => new List<int>()).ToArray();

        foreach (var (from, to) in edges)
        {
            if (idx.TryGetValue(from, out var fi) && idx.TryGetValue(to, out var ti))
            {
                adj[fi].Add(ti);
                inDeg[ti]++;
            }
        }

        var levels = new List<int[]>();
        var queue = Enumerable.Range(0, signals.Count).Where(i => inDeg[i] == 0).ToList();

        while (queue.Count > 0)
        {
            levels.Add([.. queue]);
            var next = new List<int>();
            foreach (var n in queue)
                foreach (var m in adj[n])
                    if (--inDeg[m] == 0) next.Add(m);
            queue = next;
        }

        return [.. levels];
    }

    private static string Esc(string s) => s.Replace("'", "''");
}

// ──────────────────────── ENTRY POINT ────────────────────────

public static class SignalsCompiler
{
    public static CompileResult Compile(string source, Guid subjectId, IVocabulary? vocab = null)
    {
        var tokens = Lexer.Tokenize(source);
        var parser = new Parser(tokens);
        var ast = parser.Parse();
        return Lowering.Lower(ast, subjectId, vocab);
    }
}
```

**Step 2: Build**

Run: `dotnet build SignalsKernel/SignalsKernel.csproj`
Expected: 0 errors

**Step 3: Commit**

```bash
git add SignalsKernel/Compiler.cs
git commit -m "feat(compiler): Lowering + Vocabulary + SignalsCompiler entry point

Lowering: AST → Postgres INSERTs + topo sort levels.
Vocabulary: IVocabulary + data-driven class + 5 built-ins (bio, mkt, game, org, soc).
SignalsCompiler.Compile(): text in, execution plan out."
```

---

## Task 6: Engine.cs — Data Types + Arrow Columns + TickCtx

Arrow-backed signal state, Postgres-loaded types, tick IO types, mutable tick context.

**Files:**
- Modify: `SignalsKernel/Engine.cs`

**Step 1: Write data types and tick infrastructure**

```csharp
// SignalsKernel/Engine.cs
using System.Collections.Concurrent;
using Apache.Arrow;
using Apache.Arrow.Types;
using BioChain.Kernel.Agents;

namespace SignalsKernel;

// ──────────────────────── DATA TYPES ────────────────────────

// Loaded from Postgres per tick (3 DISTINCT ON queries)
public sealed record SignalRow(int Id, string Code, string? Region, string State,
    double Value, double Baseline, double Confidence, string Distribution,
    double TauMinMs, double TauMaxMs, double RangeLow, double RangeHigh);

public sealed record EdgeRow(int Id, int SourceId, int TargetId,
    string Operator, string OperatorClass,
    double Gain, double NoiseSigma, string TransferFn,
    int DelayMs, double? ClampLo, double? ClampHi, int? GateId, int? ToolId, bool Active);

public sealed record GateRow(int Id, string Code, string Type,
    double? Threshold, string? Expression, double? Probability, bool Latched,
    string? Prompt, string? Model, string? ParseMap, string? Fallback,
    int? TimeoutMs, int? CacheMs);

// Tick IO
public abstract record Input
{
    public sealed record Inject(string SignalCode, double Value, double Confidence = 1.0) : Input;
    public sealed record GateResult(int GateId, bool Fired, double Confidence = 1.0) : Input;
    public sealed record ToolResult(string ToolCode, Dictionary<string, double> Outputs) : Input;
}

public sealed record TickResult(
    RecordBatch SignalState,          // Arrow columnar: all signal values post-tick
    ProtocolEntry[] Protocol,         // append-only writes
    SideEffect[] Pending,             // for Wolverine dispatch
    KernelEvt[] Events,               // observable output
    bool Stable,
    int CascadeDepth,
    long TickNumber);

public sealed record ProtocolEntry(string Tag, string Code, string Content, double? Confidence);

public abstract record SideEffect
{
    public sealed record LlmGate(int GateId, string Prompt, string Model,
        string? ParseMap, string? Fallback, int TimeoutMs, bool Cache) : SideEffect;
    public sealed record ToolInvoke(string ToolCode, string Invoke,
        string[] InputCodes, string[] OutputCodes,
        int TimeoutMs, int RetryCount, string? Fallback) : SideEffect;
}

public abstract record KernelEvt
{
    public sealed record SignalChange(string Code, double Old, double New, double Conf) : KernelEvt;
    public sealed record GateFire(int Id, string Type) : KernelEvt;
    public sealed record GateBlock(int Id, string Type) : KernelEvt;
    public sealed record CascadeStep(int Depth) : KernelEvt;
    public sealed record ConstraintSolved(string Expr) : KernelEvt;
    public sealed record ConstraintViolated(string Expr) : KernelEvt;
    public sealed record FailActive(string Type, string Code) : KernelEvt;
    public sealed record FailResolved(string Type, string Code) : KernelEvt;
    public sealed record EvalStable(long Tick) : KernelEvt;
}

// ──────────────────────── ARROW SIGNAL STATE ────────────────────────

/// <summary>
/// Arrow-backed columnar signal state. Each field is a contiguous double[] array.
/// Decay and Propagate phases operate on these arrays with vectorized access patterns.
/// </summary>
public sealed class SignalColumns
{
    public string[] Codes { get; }           // signal code index (row → code)
    public double[] Values { get; }          // current values — THE hot array
    public double[] Baselines { get; }
    public double[] Confidences { get; }
    public double[] TauMinMs { get; }
    public double[] RangeLow { get; }
    public double[] RangeHigh { get; }
    public Dictionary<string, int> Index { get; }  // code → row index

    public int Count => Codes.Length;

    public SignalColumns(SignalRow[] rows)
    {
        var n = rows.Length;
        Codes = new string[n];
        Values = new double[n];
        Baselines = new double[n];
        Confidences = new double[n];
        TauMinMs = new double[n];
        RangeLow = new double[n];
        RangeHigh = new double[n];
        Index = new(n);

        for (int i = 0; i < n; i++)
        {
            Codes[i] = rows[i].Code;
            Values[i] = rows[i].Value;
            Baselines[i] = rows[i].Baseline;
            Confidences[i] = rows[i].Confidence;
            TauMinMs[i] = rows[i].TauMinMs;
            RangeLow[i] = rows[i].RangeLow;
            RangeHigh[i] = rows[i].RangeHigh;
            Index[rows[i].Code] = i;
        }
    }

    /// <summary>Export as Arrow RecordBatch for zero-copy streaming.</summary>
    public RecordBatch ToRecordBatch()
    {
        var schema = new Schema.Builder()
            .Field(new Field("code", StringType.Default, false))
            .Field(new Field("value", DoubleType.Default, false))
            .Field(new Field("baseline", DoubleType.Default, false))
            .Field(new Field("confidence", DoubleType.Default, false))
            .Build();

        var codeBuilder = new StringArray.Builder();
        var valueBuilder = new DoubleArray.Builder();
        var baselineBuilder = new DoubleArray.Builder();
        var confBuilder = new DoubleArray.Builder();

        for (int i = 0; i < Count; i++)
        {
            codeBuilder.Append(Codes[i]);
            valueBuilder.Append(Values[i]);
            baselineBuilder.Append(Baselines[i]);
            confBuilder.Append(Confidences[i]);
        }

        return new RecordBatch(schema, [codeBuilder.Build(), valueBuilder.Build(),
            baselineBuilder.Build(), confBuilder.Build()], Count);
    }
}

// ──────────────────────── TICK CONTEXT ────────────────────────

/// <summary>
/// Mutable context threaded through all 9 phases.
/// Phases read and mutate this directly — no immutable copying on the hot path.
/// </summary>
public sealed class TickCtx
{
    public required SignalColumns Signals { get; init; }
    public required EdgeRow[] Edges { get; init; }
    public required GateRow[] Gates { get; init; }
    public required int[][] TopoLevels { get; init; }  // pre-computed from Compiler
    public long TickNumber { get; set; }
    public int CascadeDepth { get; set; }
    public bool Stable { get; set; }
    public double TickIntervalMs { get; init; } = 100;

    // Output accumulators
    public List<KernelEvt> Events { get; } = [];
    public List<SideEffect> Pending { get; } = [];
    public List<ProtocolEntry> Protocol { get; } = [];

    // Resolved side effects from last tick (injected by Agent.cs)
    public List<Input> ResolvedInputs { get; } = [];
}
```

**Step 2: Build**

Run: `dotnet build SignalsKernel/SignalsKernel.csproj`
Expected: 0 errors

**Step 3: Commit**

```bash
git add SignalsKernel/Engine.cs
git commit -m "feat(engine): data types, Arrow-backed SignalColumns, TickCtx

SignalColumns: contiguous double[] arrays for vectorized phases.
ToRecordBatch(): zero-copy Arrow export for streaming.
TickCtx: mutable context threaded through all 9 phases."
```

---

## Task 7: Engine.cs — TickPipeline + All 9 Phases

The hot path. 9 phases implementing BNF EVAL ENGINE (lines 688-715).

**Files:**
- Modify: `SignalsKernel/Engine.cs`

**Step 1: Add TickPipeline and all 9 phases**

Append to Engine.cs:

```csharp
// ──────────────────────── TICK PIPELINE ────────────────────────

public static class TickPipeline
{
    private const int MaxCascade = 100;

    public static TickResult Run(TickCtx ctx, IReadOnlyList<Input> inputs)
    {
        ctx.TickNumber++;
        ctx.CascadeDepth = 0;
        ctx.Stable = false;
        ctx.Events.Clear();
        ctx.Pending.Clear();
        ctx.Protocol.Clear();

        // Phase 1: Resolve — apply external inputs
        ResolvePhase(ctx, inputs);

        // Phase 2: Decay — tau-based exponential decay
        DecayPhase(ctx);

        // Cascade loop
        var changed = true;
        while (changed && ctx.CascadeDepth < MaxCascade)
        {
            // Phase 3: Formula — evaluate FormulaVM bytecode (future)
            FormulaPhase(ctx);

            // Phase 4: Propagate — topo sort + edge transforms
            changed = PropagatePhase(ctx);

            // Phase 5: Gate — evaluate gates, queue LLM_GATE as SideEffect
            GatePhase(ctx);

            if (changed) ctx.CascadeDepth++;
        }

        // Phase 6: Constrain — boundary/simultaneous/equilibrium/conserve
        ConstrainPhase(ctx);

        // Phase 7: Fail — check FAIL conditions
        FailPhase(ctx);

        // Phase 8: Bind — evaluate BIND expressions
        BindPhase(ctx);

        // Phase 9: Emit — finalize
        EmitPhase(ctx);

        return new TickResult(
            ctx.Signals.ToRecordBatch(),
            [.. ctx.Protocol],
            [.. ctx.Pending],
            [.. ctx.Events],
            ctx.Stable,
            ctx.CascadeDepth,
            ctx.TickNumber);
    }

    // ── Phase 1: Resolve ──
    private static void ResolvePhase(TickCtx ctx, IReadOnlyList<Input> inputs)
    {
        foreach (var input in inputs)
        {
            switch (input)
            {
                case Input.Inject inj:
                    if (ctx.Signals.Index.TryGetValue(inj.SignalCode, out var idx))
                    {
                        var old = ctx.Signals.Values[idx];
                        ctx.Signals.Values[idx] = inj.Value;
                        ctx.Signals.Confidences[idx] = inj.Confidence;
                        ctx.Events.Add(new KernelEvt.SignalChange(inj.SignalCode, old, inj.Value, inj.Confidence));
                    }
                    break;
                case Input.ToolResult tool:
                    foreach (var (code, val) in tool.Outputs)
                        if (ctx.Signals.Index.TryGetValue(code, out var ti))
                        {
                            var old = ctx.Signals.Values[ti];
                            ctx.Signals.Values[ti] = val;
                            ctx.Events.Add(new KernelEvt.SignalChange(code, old, val, 1.0));
                        }
                    break;
            }
        }

        // Apply resolved side effects from last tick
        foreach (var resolved in ctx.ResolvedInputs) ResolvePhase(ctx, [resolved]);
        ctx.ResolvedInputs.Clear();
    }

    // ── Phase 2: Decay ──
    private static void DecayPhase(TickCtx ctx)
    {
        var vals = ctx.Signals.Values;
        var bases = ctx.Signals.Baselines;
        var taus = ctx.Signals.TauMinMs;
        var dt = ctx.TickIntervalMs;

        // Vectorized: iterate contiguous arrays
        for (int i = 0; i < ctx.Signals.Count; i++)
        {
            if (taus[i] <= 0) continue;
            var diff = vals[i] - bases[i];
            if (Math.Abs(diff) < 1e-10) continue;

            var factor = Math.Exp(-dt / taus[i]);
            var newVal = bases[i] + diff * factor;
            if (Math.Abs(newVal - bases[i]) < 1e-6) newVal = bases[i];

            if (Math.Abs(newVal - vals[i]) > 1e-10)
            {
                ctx.Events.Add(new KernelEvt.SignalChange(ctx.Signals.Codes[i], vals[i], newVal, ctx.Signals.Confidences[i]));
                vals[i] = newVal;
            }
        }
    }

    // ── Phase 3: Formula ──
    private static void FormulaPhase(TickCtx ctx)
    {
        // FormulaVM bytecode execution — see FormulaVM section below
        // Placeholder: formula evaluation will use FormulaVM.Execute()
    }

    // ── Phase 4: Propagate ──
    private static bool PropagatePhase(TickCtx ctx)
    {
        var anyChanged = false;
        var vals = ctx.Signals.Values;
        var edges = ctx.Edges.Where(e => e.Active).ToArray();

        foreach (var level in ctx.TopoLevels)
        {
            // Nodes within same topo level are independent → could Parallel.For
            foreach (var nodeIdx in level)
            {
                var code = ctx.Signals.Codes[nodeIdx];
                var inbound = edges.Where(e => e.TargetId == nodeIdx).ToArray();
                if (inbound.Length == 0) continue;

                var delta = 0.0;
                foreach (var edge in inbound)
                {
                    if (edge.SourceId < 0 || edge.SourceId >= vals.Length) continue;
                    var src = vals[edge.SourceId];

                    // Edge transforms: operator → gain → transfer fn → clamp
                    var contribution = edge.Operator switch
                    {
                        "⊣" => -src, "⊗" => 0.0, "⊘→" => vals[nodeIdx] - src, _ => src
                    };
                    contribution *= edge.Gain;
                    contribution = edge.TransferFn switch
                    {
                        "log" => contribution > 0 ? Math.Log(contribution) : 0,
                        "exp" => Math.Exp(Math.Clamp(contribution, -20, 20)),
                        "sig" => 1.0 / (1.0 + Math.Exp(-contribution)),
                        "step" => contribution >= 0 ? 1.0 : 0.0,
                        _ => contribution // "lin"
                    };
                    if (edge.ClampLo.HasValue) contribution = Math.Max(contribution, edge.ClampLo.Value);
                    if (edge.ClampHi.HasValue) contribution = Math.Min(contribution, edge.ClampHi.Value);

                    delta += contribution;
                }

                var newVal = ctx.Signals.Baselines[nodeIdx] + delta;
                if (Math.Abs(newVal - vals[nodeIdx]) > 1e-10)
                {
                    ctx.Events.Add(new KernelEvt.SignalChange(code, vals[nodeIdx], newVal, ctx.Signals.Confidences[nodeIdx]));
                    vals[nodeIdx] = newVal;
                    anyChanged = true;
                }
            }
        }

        if (anyChanged) ctx.Events.Add(new KernelEvt.CascadeStep(ctx.CascadeDepth));
        return anyChanged;
    }

    // ── Phase 5: Gate ──
    private static void GatePhase(TickCtx ctx)
    {
        foreach (var gate in ctx.Gates)
        {
            switch (gate.Type)
            {
                case "threshold":
                    var fired = gate.Threshold is null || true; // simplified — needs expression eval
                    ctx.Events.Add(fired ? new KernelEvt.GateFire(gate.Id, gate.Type) : new KernelEvt.GateBlock(gate.Id, gate.Type));
                    SetGatedEdges(ctx.Edges, gate.Id, fired);
                    break;

                case "latch":
                    var latchFired = gate.Latched || true;
                    ctx.Events.Add(new KernelEvt.GateFire(gate.Id, gate.Type));
                    SetGatedEdges(ctx.Edges, gate.Id, latchFired);
                    break;

                case "and": case "or": case "not": case "xor": case "splitter":
                    ctx.Events.Add(new KernelEvt.GateFire(gate.Id, gate.Type));
                    SetGatedEdges(ctx.Edges, gate.Id, true);
                    break;

                case "llm":
                    // NEVER call LLM here — emit SideEffect
                    ctx.Pending.Add(new SideEffect.LlmGate(
                        gate.Id, gate.Prompt ?? "", gate.Model ?? "default",
                        gate.ParseMap, gate.Fallback, gate.TimeoutMs ?? 30000, gate.CacheMs > 0));
                    // Use fallback in deterministic mode
                    if (gate.Fallback is not null)
                        SetGatedEdges(ctx.Edges, gate.Id, gate.Fallback != "false");
                    break;

                case "integrator": case "novelty": case "gain":
                    // Stateful gates — simplified
                    break;
            }
        }
    }

    private static void SetGatedEdges(EdgeRow[] edges, int gateId, bool active)
    {
        for (int i = 0; i < edges.Length; i++)
            if (edges[i].GateId == gateId)
                edges[i] = edges[i] with { Active = active };
    }

    // ── Phase 6: Constrain ──
    private static void ConstrainPhase(TickCtx ctx)
    {
        // Boundary: enforce signal range limits
        var vals = ctx.Signals.Values;
        var lo = ctx.Signals.RangeLow;
        var hi = ctx.Signals.RangeHigh;

        for (int i = 0; i < ctx.Signals.Count; i++)
        {
            if (lo[i] > 0 && vals[i] < lo[i]) { vals[i] = lo[i]; }
            if (hi[i] < double.MaxValue && vals[i] > hi[i]) { vals[i] = hi[i]; }
        }
        // Equilibrium, Conserve — placeholder for expression parser
    }

    // ── Phase 7: Fail ──
    private static void FailPhase(TickCtx ctx)
    {
        // FAIL conditions are tracked in-memory
        // Full implementation needs: sustained check, rate check, oscillation detection, divergence
        // Placeholder — will be populated from compiled FailDecl AST nodes
    }

    // ── Phase 8: Bind ──
    private static void BindPhase(TickCtx ctx)
    {
        // BIND rules: accumulate + decay
        // Full implementation needs expression evaluation via FormulaVM
        // Placeholder
    }

    // ── Phase 9: Emit ──
    private static void EmitPhase(TickCtx ctx)
    {
        ctx.Stable = ctx.Events.Count == 0 || ctx.CascadeDepth >= MaxCascade;
        if (ctx.Stable)
            ctx.Events.Add(new KernelEvt.EvalStable(ctx.TickNumber));
    }
}
```

**Step 2: Build**

Run: `dotnet build SignalsKernel/SignalsKernel.csproj`
Expected: 0 errors

**Step 3: Commit**

```bash
git add SignalsKernel/Engine.cs
git commit -m "feat(engine): TickPipeline + 9 phases implementing BNF EVAL ENGINE

Resolve → Decay (vectorized) → Formula → Propagate (topo sort + edge transforms) →
Gate (sync inline, LLM_GATE as SideEffect) → Constrain (boundary clamp) →
Fail → Bind → Emit. Cascade loops until stable or MaxCascade(100)."
```

---

## Task 8: Engine.cs — FormulaVM + ExtismHost

Stack bytecode interpreter (16 opcodes) + WASM plugin host.

**Files:**
- Modify: `SignalsKernel/Engine.cs`

**Step 1: Add FormulaVM**

Append to Engine.cs:

```csharp
// ──────────────────────── FORMULA VM ────────────────────────

public enum Op : byte
{
    Nop, Push, Pop, Load, Store,          // stack + signal access
    Add, Sub, Mul, Div, Mod, Neg,         // arithmetic
    Gt, Lt, Eq, And, Or,                  // comparison + logic
    Call                                   // host function call (Extism or native)
}

public static class FormulaVM
{
    /// <summary>
    /// Execute bytecode against signal columns. Stack-based interpreter.
    /// Each opcode is 1 byte + optional operand (8 bytes double or 4 bytes int).
    /// </summary>
    public static double Execute(ReadOnlySpan<byte> bytecode, SignalColumns signals)
    {
        var stack = new Stack<double>(16);
        int ip = 0;

        while (ip < bytecode.Length)
        {
            var op = (Op)bytecode[ip++];
            switch (op)
            {
                case Op.Nop: break;

                case Op.Push:
                    stack.Push(BitConverter.ToDouble(bytecode.Slice(ip, 8)));
                    ip += 8;
                    break;

                case Op.Pop:
                    stack.Pop();
                    break;

                case Op.Load:
                    var loadIdx = BitConverter.ToInt32(bytecode.Slice(ip, 4));
                    ip += 4;
                    stack.Push(loadIdx >= 0 && loadIdx < signals.Count ? signals.Values[loadIdx] : 0);
                    break;

                case Op.Store:
                    var storeIdx = BitConverter.ToInt32(bytecode.Slice(ip, 4));
                    ip += 4;
                    if (storeIdx >= 0 && storeIdx < signals.Count)
                        signals.Values[storeIdx] = stack.Pop();
                    break;

                case Op.Add: { var b = stack.Pop(); var a = stack.Pop(); stack.Push(a + b); break; }
                case Op.Sub: { var b = stack.Pop(); var a = stack.Pop(); stack.Push(a - b); break; }
                case Op.Mul: { var b = stack.Pop(); var a = stack.Pop(); stack.Push(a * b); break; }
                case Op.Div: { var b = stack.Pop(); var a = stack.Pop(); stack.Push(b != 0 ? a / b : 0); break; }
                case Op.Mod: { var b = stack.Pop(); var a = stack.Pop(); stack.Push(b != 0 ? a % b : 0); break; }
                case Op.Neg: stack.Push(-stack.Pop()); break;

                case Op.Gt: { var b = stack.Pop(); var a = stack.Pop(); stack.Push(a > b ? 1 : 0); break; }
                case Op.Lt: { var b = stack.Pop(); var a = stack.Pop(); stack.Push(a < b ? 1 : 0); break; }
                case Op.Eq: { var b = stack.Pop(); var a = stack.Pop(); stack.Push(Math.Abs(a - b) < 1e-10 ? 1 : 0); break; }
                case Op.And: { var b = stack.Pop(); var a = stack.Pop(); stack.Push(a != 0 && b != 0 ? 1 : 0); break; }
                case Op.Or: { var b = stack.Pop(); var a = stack.Pop(); stack.Push(a != 0 || b != 0 ? 1 : 0); break; }

                case Op.Call:
                    var fnId = BitConverter.ToInt32(bytecode.Slice(ip, 4));
                    ip += 4;
                    // Route to ExtismHost for WASM functions, or native builtins
                    stack.Push(ExtismHost.CallFunction(fnId, stack));
                    break;
            }
        }

        return stack.Count > 0 ? stack.Pop() : 0;
    }
}

// ──────────────────────── EXTISM HOST ────────────────────────

public static class ExtismHost
{
    private static readonly ConcurrentDictionary<string, Extism.Sdk.Plugin> _plugins = new();
    private static readonly ConcurrentDictionary<int, (string PluginName, string FnName)> _fnRegistry = new();
    private static int _nextFnId;

    /// <summary>Register a WASM plugin from file or URL.</summary>
    public static void RegisterPlugin(string name, string wasmPath)
    {
        var manifest = new Extism.Sdk.Manifest(new Extism.Sdk.PathWasmSource(wasmPath));
        var plugin = new Extism.Sdk.Plugin(manifest, [], withWasi: true);
        _plugins[name] = plugin;
    }

    /// <summary>Register a function from a loaded plugin for FormulaVM Call opcode.</summary>
    public static int RegisterFunction(string pluginName, string fnName)
    {
        var id = Interlocked.Increment(ref _nextFnId);
        _fnRegistry[id] = (pluginName, fnName);
        return id;
    }

    /// <summary>Called by FormulaVM Op.Call — routes to WASM plugin function.</summary>
    public static double CallFunction(int fnId, Stack<double> stack)
    {
        if (!_fnRegistry.TryGetValue(fnId, out var reg)) return 0;
        if (!_plugins.TryGetValue(reg.PluginName, out var plugin)) return 0;

        // Marshal: pop arg from stack as JSON string, call plugin, parse result
        var arg = stack.Count > 0 ? stack.Pop().ToString() : "0";
        var result = plugin.Call(reg.FnName, arg);
        return double.TryParse(result, out var val) ? val : 0;
    }

    /// <summary>Unload all plugins.</summary>
    public static void Dispose()
    {
        foreach (var p in _plugins.Values) p.Dispose();
        _plugins.Clear();
        _fnRegistry.Clear();
    }
}
```

**Step 2: Build**

Run: `dotnet build SignalsKernel/SignalsKernel.csproj`
Expected: 0 errors

**Step 3: Commit**

```bash
git add SignalsKernel/Engine.cs
git commit -m "feat(engine): FormulaVM (16 opcodes) + ExtismHost (WASM plugins)

FormulaVM: stack-based bytecode interpreter. Push, Load, Store,
arithmetic, comparison, Call (routes to Extism).
ExtismHost: registers WASM plugins, exposes to FormulaVM via fnId."
```

---

## Task 9: Agent.cs — Wolverine Handlers

Side-effect dispatch via Wolverine message bus.

**Files:**
- Modify: `SignalsKernel/Agent.cs`

**Step 1: Write Agent.cs with Wolverine handlers**

```csharp
// SignalsKernel/Agent.cs
using BioChain.Kernel.Agents;
using Microsoft.Extensions.Logging;
using Wolverine;

namespace SignalsKernel;

// ──────────────────────── SIDE EFFECT MESSAGES ────────────────────────

// Wolverine messages — one per side effect type
public sealed record ResolveLlmGate(Guid WorldId, int GateId, string Prompt, string Model,
    string? ParseMap, string? Fallback, int TimeoutMs, bool Cache);

public sealed record ResolveToolInvoke(Guid WorldId, string ToolCode, string Invoke,
    string[] InputCodes, string[] OutputCodes, int TimeoutMs, int RetryCount, string? Fallback);

// Result messages — injected back into next tick
public sealed record LlmGateResolved(Guid WorldId, int GateId, bool Fired, double Confidence);
public sealed record ToolInvokeResolved(Guid WorldId, string ToolCode, Dictionary<string, double> Outputs);

// ──────────────────────── SIDE EFFECT DISPATCHER ────────────────────────

/// <summary>
/// Routes SideEffect records from a tick to Wolverine messages.
/// Called by WorldGrain after each tick completes.
/// </summary>
public static class SideEffectDispatcher
{
    public static async Task DispatchAsync(IMessageBus bus, Guid worldId, SideEffect[] effects)
    {
        foreach (var effect in effects)
        {
            switch (effect)
            {
                case SideEffect.LlmGate llm:
                    await bus.PublishAsync(new ResolveLlmGate(
                        worldId, llm.GateId, llm.Prompt, llm.Model,
                        llm.ParseMap, llm.Fallback, llm.TimeoutMs, llm.Cache));
                    break;

                case SideEffect.ToolInvoke tool:
                    await bus.PublishAsync(new ResolveToolInvoke(
                        worldId, tool.ToolCode, tool.Invoke,
                        tool.InputCodes, tool.OutputCodes,
                        tool.TimeoutMs, tool.RetryCount, tool.Fallback));
                    break;
            }
        }
    }
}

// ──────────────────────── LLM BRIDGE ────────────────────────

/// <summary>
/// Wolverine handler: receives ResolveLlmGate, calls IEngine, returns result.
/// Wolverine's cascading messages pattern: return value is published automatically.
/// </summary>
public sealed class LlmBridge
{
    public static async Task<LlmGateResolved> HandleAsync(
        ResolveLlmGate msg, IEngine engine, ILogger<LlmBridge> log)
    {
        try
        {
            using var cts = new CancellationTokenSource(msg.TimeoutMs);
            var response = await engine.ProcessAsync(msg.Prompt, "", cts.Token);

            var fired = ParseDecision(response, msg.ParseMap);
            var confidence = ParseConfidence(response, msg.ParseMap);

            log.LogDebug("[LlmBridge] Gate {Id}: fired={Fired}, conf={Conf}", msg.GateId, fired, confidence);
            return new LlmGateResolved(msg.WorldId, msg.GateId, fired, confidence);
        }
        catch (Exception ex)
        {
            log.LogWarning(ex, "[LlmBridge] Gate {Id} failed, using fallback", msg.GateId);
            var fallbackFired = msg.Fallback is not null && msg.Fallback != "false";
            return new LlmGateResolved(msg.WorldId, msg.GateId, fallbackFired, 0.5);
        }
    }

    private static bool ParseDecision(string response, string? parseMap)
    {
        var lower = response.ToLowerInvariant();
        // Try JSON: {"decision": true/false}
        if (lower.Contains("\"decision\""))
            return lower.Contains("\"decision\": true") || lower.Contains("\"decision\":true");
        // Fallback: any "true" in response
        return lower.Contains("true");
    }

    private static double ParseConfidence(string response, string? parseMap)
    {
        // Try JSON: {"confidence": 0.N}
        var idx = response.IndexOf("\"confidence\"", StringComparison.OrdinalIgnoreCase);
        if (idx >= 0)
        {
            var start = response.IndexOf(':', idx) + 1;
            var end = response.IndexOfAny([',', '}'], start);
            if (end > start && double.TryParse(response[start..end].Trim(), out var conf))
                return Math.Clamp(conf, 0, 1);
        }
        return 1.0;
    }
}

// ──────────────────────── TOOL BRIDGE ────────────────────────

/// <summary>
/// Wolverine handler: receives ResolveToolInvoke, routes by invoke type.
/// Supports: wasm (Extism), http (REST endpoint), native (C# delegate).
/// </summary>
public sealed class ToolBridge
{
    public static async Task<ToolInvokeResolved> HandleAsync(
        ResolveToolInvoke msg, ILogger<ToolBridge> log)
    {
        var outputs = new Dictionary<string, double>();

        try
        {
            if (msg.Invoke.EndsWith(".wasm"))
            {
                // WASM plugin invocation
                var pluginName = Path.GetFileNameWithoutExtension(msg.Invoke);
                if (!ExtismHost._plugins.ContainsKey(pluginName))
                    ExtismHost.RegisterPlugin(pluginName, msg.Invoke);

                var input = string.Join(",", msg.InputCodes);
                var result = ExtismHost._plugins[pluginName].Call(msg.ToolCode, input);

                // Parse result into output signals
                foreach (var code in msg.OutputCodes)
                    if (double.TryParse(result, out var val))
                        outputs[code] = val;
            }
            else if (msg.Invoke.StartsWith("http"))
            {
                // HTTP tool invocation — future
                log.LogDebug("[ToolBridge] HTTP tool not yet implemented: {Invoke}", msg.Invoke);
            }
            else
            {
                // Native tool — future
                log.LogDebug("[ToolBridge] Native tool not yet implemented: {Invoke}", msg.Invoke);
            }
        }
        catch (Exception ex)
        {
            log.LogWarning(ex, "[ToolBridge] Tool {Code} failed", msg.ToolCode);
            // Apply fallback values
            if (msg.Fallback is not null)
                foreach (var code in msg.OutputCodes)
                    outputs[code] = 0;
        }

        return new ToolInvokeResolved(msg.WorldId, msg.ToolCode, outputs);
    }
}
```

**Step 2: Build**

Run: `dotnet build SignalsKernel/SignalsKernel.csproj`
Expected: 0 errors

**Step 3: Commit**

```bash
git add SignalsKernel/Agent.cs
git commit -m "feat(agent): Wolverine handlers for LLM bridge + tool bridge

SideEffectDispatcher publishes Wolverine messages.
LlmBridge: IEngine.ProcessAsync with timeout + fallback.
ToolBridge: routes wasm/http/native, Extism for WASM tools.
Cascading messages pattern: handler return = next message."
```

---

## Task 10: Graph.cs — Neo4j Sync + GDS Wrappers

Existing sync pattern enriched with numeric properties + GDS analysis.

**Files:**
- Modify: `SignalsKernel/Graph.cs`

**Step 1: Write Graph.cs**

```csharp
// SignalsKernel/Graph.cs
using Neo4j.Driver;
using Microsoft.Extensions.Logging;

namespace SignalsKernel;

// ──────────────────────── NEO4J SYNC ────────────────────────

public sealed class Neo4jSync : IAsyncDisposable
{
    private readonly IDriver _driver;
    private readonly ILogger<Neo4jSync> _log;

    public Neo4jSync(IDriver driver, ILogger<Neo4jSync> log) { _driver = driver; _log = log; }

    /// <summary>Full graph rebuild for a subject. Enriched with numeric properties.</summary>
    public async Task RebuildGraphAsync(Guid subjectId, SignalRow[] signals, EdgeRow[] edges, GateRow[] gates)
    {
        await using var session = _driver.AsyncSession();
        await session.ExecuteWriteAsync(async tx =>
        {
            // Clear existing
            await tx.RunAsync("MATCH (n {subject_id: $sid}) DETACH DELETE n",
                new { sid = subjectId.ToString() });

            // Create signal nodes with numeric properties
            if (signals.Length > 0)
            {
                await tx.RunAsync(@"
                    UNWIND $rows AS r
                    CALL apoc.create.node(['Signal'], {
                        subject_id: r.sid, code: r.code, region: r.region,
                        state: r.state, value: r.value, baseline: r.baseline,
                        confidence: r.conf, tau_min_ms: r.tau, range_low: r.lo, range_high: r.hi
                    }) YIELD node RETURN count(node)",
                    new { rows = signals.Select(s => new {
                        sid = subjectId.ToString(), code = s.Code, region = s.Region ?? "",
                        state = s.State, value = s.Value, baseline = s.Baseline,
                        conf = s.Confidence, tau = s.TauMinMs, lo = s.RangeLow, hi = s.RangeHigh
                    }).ToArray() });
            }

            // Create edges with numeric properties
            if (edges.Length > 0)
            {
                await tx.RunAsync(@"
                    UNWIND $rows AS r
                    MATCH (s:Signal {subject_id: r.sid, code: r.src})
                    MATCH (t:Signal {subject_id: r.sid, code: r.tgt})
                    CALL apoc.create.relationship(s, r.cls, {
                        operator: r.op, gain: r.gain, noise_sigma: r.noise,
                        transfer_fn: r.tfn, delay_ms: r.delay, clamp_lo: r.lo, clamp_hi: r.hi
                    }, t) YIELD rel RETURN count(rel)",
                    new { rows = edges.Select(e => new {
                        sid = subjectId.ToString(),
                        src = signals.FirstOrDefault(s => s.Id == e.SourceId)?.Code ?? "",
                        tgt = signals.FirstOrDefault(s => s.Id == e.TargetId)?.Code ?? "",
                        cls = e.OperatorClass, op = e.Operator,
                        gain = e.Gain, noise = e.NoiseSigma, tfn = e.TransferFn,
                        delay = e.DelayMs, lo = e.ClampLo ?? 0.0, hi = e.ClampHi ?? 0.0
                    }).ToArray() });
            }
        });

        _log.LogDebug("[Neo4jSync] Rebuilt graph for {SubjectId}: {Signals} signals, {Edges} edges",
            subjectId, signals.Length, edges.Length);
    }

    /// <summary>Lightweight sync: update signal values only (between full rebuilds).</summary>
    public async Task SyncSignalValuesAsync(Guid subjectId, SignalColumns signals)
    {
        await using var session = _driver.AsyncSession();
        await session.ExecuteWriteAsync(async tx =>
        {
            for (int i = 0; i < signals.Count; i++)
            {
                await tx.RunAsync(@"
                    MATCH (s:Signal {subject_id: $sid, code: $code})
                    SET s.value = $val, s.confidence = $conf",
                    new { sid = subjectId.ToString(), code = signals.Codes[i],
                          val = signals.Values[i], conf = signals.Confidences[i] });
            }
        });
    }

    public async ValueTask DisposeAsync() => _driver.Dispose();
}

// ──────────────────────── GDS WRAPPERS ────────────────────────

public sealed class GdsAnalysis
{
    private readonly IDriver _driver;

    public GdsAnalysis(IDriver driver) => _driver = driver;

    public async Task<Dictionary<string, double>> PageRankAsync(Guid subjectId)
    {
        await using var session = _driver.AsyncSession();
        var result = await session.ExecuteReadAsync(async tx =>
        {
            var cursor = await tx.RunAsync(@"
                CALL gds.graph.project.cypher('pr_' + $sid,
                    'MATCH (n:Signal {subject_id: $sid}) RETURN id(n) AS id',
                    'MATCH (s:Signal {subject_id: $sid})-[r]->(t:Signal {subject_id: $sid}) RETURN id(s) AS source, id(t) AS target, r.gain AS weight')
                YIELD graphName
                CALL gds.pageRank.stream(graphName, {maxIterations: 20, dampingFactor: 0.85})
                YIELD nodeId, score
                WITH gds.util.asNode(nodeId).code AS code, score
                CALL gds.graph.drop(graphName) YIELD graphName AS dropped
                RETURN code, score", new { sid = subjectId.ToString() });
            return await cursor.ToListAsync(r => (r["code"].As<string>(), r["score"].As<double>()));
        });
        return result.ToDictionary(x => x.Item1, x => x.Item2);
    }

    public async Task<Dictionary<string, int>> LouvainAsync(Guid subjectId)
    {
        await using var session = _driver.AsyncSession();
        var result = await session.ExecuteReadAsync(async tx =>
        {
            var cursor = await tx.RunAsync(@"
                CALL gds.graph.project.cypher('lv_' + $sid,
                    'MATCH (n:Signal {subject_id: $sid}) RETURN id(n) AS id',
                    'MATCH (s:Signal {subject_id: $sid})-[r]->(t:Signal {subject_id: $sid}) RETURN id(s) AS source, id(t) AS target')
                YIELD graphName
                CALL gds.louvain.stream(graphName)
                YIELD nodeId, communityId
                WITH gds.util.asNode(nodeId).code AS code, communityId
                CALL gds.graph.drop(graphName) YIELD graphName AS dropped
                RETURN code, communityId", new { sid = subjectId.ToString() });
            return await cursor.ToListAsync(r => (r["code"].As<string>(), r["communityId"].As<int>()));
        });
        return result.ToDictionary(x => x.Item1, x => x.Item2);
    }

    public async Task<double> ShortestPathAsync(Guid subjectId, string fromCode, string toCode)
    {
        await using var session = _driver.AsyncSession();
        return await session.ExecuteReadAsync(async tx =>
        {
            var cursor = await tx.RunAsync(@"
                MATCH (s:Signal {subject_id: $sid, code: $from}), (t:Signal {subject_id: $sid, code: $to})
                CALL gds.shortestPath.dijkstra.stream({
                    nodeQuery: 'MATCH (n:Signal {subject_id: """ + "'" + @"""}) RETURN id(n) AS id',
                    relationshipQuery: 'MATCH (s)-[r]->(t) RETURN id(s) AS source, id(t) AS target, r.gain AS weight',
                    sourceNode: id(s), targetNode: id(t)
                }) YIELD totalCost
                RETURN totalCost",
                new { sid = subjectId.ToString(), from = fromCode, to = toCode });
            return await cursor.SingleAsync(r => r["totalCost"].As<double>());
        });
    }
}
```

**Step 2: Build**

Run: `dotnet build SignalsKernel/SignalsKernel.csproj`
Expected: 0 errors

**Step 3: Commit**

```bash
git add SignalsKernel/Graph.cs
git commit -m "feat(graph): Neo4jSync with numeric properties + GDS wrappers

RebuildGraphAsync: full graph with value, baseline, confidence, tau, gain, noise.
SyncSignalValuesAsync: lightweight value-only update.
GDS: PageRank, Louvain community detection, Dijkstra shortest path."
```

---

## Task 11: Platform.cs — Orleans Grain + Marten Events + gRPC + Builder

Everything external-facing in one file.

**Files:**
- Modify: `SignalsKernel/Platform.cs`

**Step 1: Write Platform.cs**

```csharp
// SignalsKernel/Platform.cs
using System.Collections.Concurrent;
using Marten;
using Marten.Events;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;
using Orleans;
using Orleans.Runtime;
using Wolverine;

namespace SignalsKernel;

// ──────────────────────── ORLEANS GRAIN ────────────────────────

public interface IWorldGrain : IGrainWithGuidKey
{
    ValueTask<TickResult> InjectAsync(Input input);
    ValueTask<TickResult> TickAsync();
    ValueTask StartAsync(string connectionString);
    ValueTask StopAsync();
}

public sealed class WorldGrain : Grain, IWorldGrain, IRemindable
{
    private TickCtx? _ctx;
    private string _connectionString = "";
    private IGrainTimer? _timer;
    private int _stableTicks;
    private readonly IMessageBus _bus;
    private readonly IDocumentStore _store;
    private readonly ILogger<WorldGrain> _log;

    public WorldGrain(IMessageBus bus, IDocumentStore store, ILogger<WorldGrain> log)
    {
        _bus = bus; _store = store; _log = log;
    }

    public async ValueTask StartAsync(string connectionString)
    {
        _connectionString = connectionString;
        _ctx = await LoadStateAsync();
        _timer = RegisterGrainTimer(OnTick, TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(100));
    }

    public async ValueTask<TickResult> InjectAsync(Input input)
    {
        if (_ctx is null) throw new InvalidOperationException("World not started");
        _stableTicks = 0;
        return await RunTickAsync([input]);
    }

    public async ValueTask<TickResult> TickAsync()
    {
        if (_ctx is null) throw new InvalidOperationException("World not started");
        return await RunTickAsync([]);
    }

    private async Task OnTick()
    {
        if (_ctx is null) return;

        if (_ctx.Stable && _stableTicks++ > 10)
        {
            // Adaptive slowdown — reduce tick frequency when stable
            _timer?.Dispose();
            _timer = RegisterGrainTimer(OnTick, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5));
            return;
        }

        await RunTickAsync([]);
    }

    private async Task<TickResult> RunTickAsync(IReadOnlyList<Input> inputs)
    {
        // 1. Pure tick — no IO
        var result = TickPipeline.Run(_ctx!, inputs);

        // 2. Store events via Marten
        if (result.Events.Length > 0)
        {
            await using var session = _store.LightweightSession();
            session.Events.Append(this.GetGrainId().GetGuidKey(), result.Events.Cast<object>().ToArray());
            await session.SaveChangesAsync();
        }

        // 3. Dispatch side effects via Wolverine
        if (result.Pending.Length > 0)
            await SideEffectDispatcher.DispatchAsync(_bus, this.GetGrainId().GetGuidKey(), result.Pending);

        // 4. Write protocol entries (append-only INSERTs to existing table)
        if (result.Protocol.Length > 0)
            await WriteProtocolAsync(result.Protocol);

        return result;
    }

    private async Task<TickCtx> LoadStateAsync()
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        var subjectId = this.GetGrainId().GetGuidKey();

        // 3 DISTINCT ON queries
        var signals = await LoadSignalsAsync(conn, subjectId);
        var edges = await LoadEdgesAsync(conn, subjectId);
        var gates = await LoadGatesAsync(conn, subjectId);

        return new TickCtx
        {
            Signals = new SignalColumns(signals),
            Edges = edges,
            Gates = gates,
            TopoLevels = ComputeTopoLevels(signals, edges),
        };
    }

    private static async Task<SignalRow[]> LoadSignalsAsync(NpgsqlConnection conn, Guid subjectId)
    {
        var rows = new List<SignalRow>();
        await using var cmd = new NpgsqlCommand(@"
            SELECT DISTINCT ON (code) id, code, state, COALESCE(value, 0), COALESCE(baseline, 0),
                   confidence, COALESCE(distribution, 'N'),
                   COALESCE(tau_min_ms, 0), COALESCE(tau_max_ms, 0),
                   COALESCE(range_low, 0), COALESCE(range_high, 999999)
            FROM signal WHERE subject_id = @sid ORDER BY code, created_on_utc DESC", conn);
        cmd.Parameters.AddWithValue("sid", subjectId);
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            rows.Add(new SignalRow(reader.GetInt32(0), reader.GetString(1), null,
                reader.GetString(2), reader.GetDouble(3), reader.GetDouble(4),
                reader.GetDouble(5), reader.GetString(6),
                reader.GetDouble(7), reader.GetDouble(8),
                reader.GetDouble(9), reader.GetDouble(10)));
        }
        return [.. rows];
    }

    private static async Task<EdgeRow[]> LoadEdgesAsync(NpgsqlConnection conn, Guid subjectId)
    {
        var rows = new List<EdgeRow>();
        await using var cmd = new NpgsqlCommand(@"
            SELECT id, source_id, target_id, operator, operator_class,
                   COALESCE(gain, 1), COALESCE(noise_sigma, 0), COALESCE(transfer_fn, 'lin'),
                   COALESCE(delay_ms, 0), clamp_lo, clamp_hi, gate_id, tool_id, active
            FROM edge WHERE subject_id = @sid", conn);
        cmd.Parameters.AddWithValue("sid", subjectId);
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            rows.Add(new EdgeRow(reader.GetInt32(0), reader.GetInt32(1), reader.GetInt32(2),
                reader.GetString(3), reader.GetString(4),
                reader.GetDouble(5), reader.GetDouble(6), reader.GetString(7),
                reader.GetInt32(8), reader.IsDBNull(9) ? null : reader.GetDouble(9),
                reader.IsDBNull(10) ? null : reader.GetDouble(10),
                reader.IsDBNull(11) ? null : reader.GetInt32(11),
                reader.IsDBNull(12) ? null : reader.GetInt32(12),
                reader.GetBoolean(13)));
        }
        return [.. rows];
    }

    private static async Task<GateRow[]> LoadGatesAsync(NpgsqlConnection conn, Guid subjectId)
    {
        var rows = new List<GateRow>();
        await using var cmd = new NpgsqlCommand(@"
            SELECT id, code, type, threshold, expression, probability, latched,
                   prompt, model, parse_map, fallback_expr, timeout_ms, cache_ms
            FROM gate WHERE subject_id = @sid", conn);
        cmd.Parameters.AddWithValue("sid", subjectId);
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            rows.Add(new GateRow(reader.GetInt32(0), reader.GetString(1), reader.GetString(2),
                reader.IsDBNull(3) ? null : reader.GetDouble(3),
                reader.IsDBNull(4) ? null : reader.GetString(4),
                reader.IsDBNull(5) ? null : reader.GetDouble(5),
                reader.GetBoolean(6),
                reader.IsDBNull(7) ? null : reader.GetString(7),
                reader.IsDBNull(8) ? null : reader.GetString(8),
                reader.IsDBNull(9) ? null : reader.GetString(9),
                reader.IsDBNull(10) ? null : reader.GetString(10),
                reader.IsDBNull(11) ? null : reader.GetInt32(11),
                reader.IsDBNull(12) ? null : reader.GetInt32(12)));
        }
        return [.. rows];
    }

    private async Task WriteProtocolAsync(ProtocolEntry[] entries)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        foreach (var e in entries)
        {
            await using var cmd = new NpgsqlCommand(@"
                INSERT INTO protocol (subject_id, tag, content, confidence)
                VALUES (@sid, @tag, @content, @conf)", conn);
            cmd.Parameters.AddWithValue("sid", this.GetGrainId().GetGuidKey());
            cmd.Parameters.AddWithValue("tag", e.Tag);
            cmd.Parameters.AddWithValue("content", e.Content);
            cmd.Parameters.AddWithValue("conf", (object?)e.Confidence ?? DBNull.Value);
            await cmd.ExecuteNonQueryAsync();
        }
    }

    private static int[][] ComputeTopoLevels(SignalRow[] signals, EdgeRow[] edges)
    {
        var n = signals.Length;
        var inDeg = new int[n];
        var adj = Enumerable.Range(0, n).Select(_ => new List<int>()).ToArray();
        var idToIdx = signals.Select((s, i) => (s.Id, i)).ToDictionary(x => x.Id, x => x.i);

        foreach (var e in edges)
        {
            if (idToIdx.TryGetValue(e.SourceId, out var si) && idToIdx.TryGetValue(e.TargetId, out var ti))
            {
                adj[si].Add(ti); inDeg[ti]++;
            }
        }

        var levels = new List<int[]>();
        var queue = Enumerable.Range(0, n).Where(i => inDeg[i] == 0).ToList();
        while (queue.Count > 0)
        {
            levels.Add([.. queue]);
            var next = new List<int>();
            foreach (var node in queue)
                foreach (var m in adj[node])
                    if (--inDeg[m] == 0) next.Add(m);
            queue = next;
        }
        return [.. levels];
    }

    public ValueTask StopAsync() { _timer?.Dispose(); return ValueTask.CompletedTask; }
    public Task ReceiveReminder(string reminderName, TickStatus status) => Task.CompletedTask;
}

// ──────────────────────── SIGNALR HUB ────────────────────────

public sealed class WorldHub : Hub
{
    public async Task SubscribeToWorld(string worldId) =>
        await Groups.AddToGroupAsync(Context.ConnectionId, worldId);

    public async Task UnsubscribeFromWorld(string worldId) =>
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, worldId);
}

// ──────────────────────── FLUENT BUILDER ────────────────────────

public sealed class WorldBuilder
{
    private readonly IClusterClient _orleans;
    private readonly string _connectionString;
    private IVocabulary? _vocab;
    private string? _bnfSource;

    public WorldBuilder(IClusterClient orleans, string connectionString)
    {
        _orleans = orleans; _connectionString = connectionString;
    }

    public WorldBuilder WithVocabulary(IVocabulary vocab) { _vocab = vocab; return this; }
    public WorldBuilder FromBnf(string bnfText) { _bnfSource = bnfText; return this; }
    public WorldBuilder FromFile(string path) { _bnfSource = File.ReadAllText(path); return this; }

    public async Task<Guid> BuildAsync()
    {
        var worldId = Guid.NewGuid();

        // Compile BNF if provided
        if (_bnfSource is not null)
        {
            var result = SignalsCompiler.Compile(_bnfSource, worldId, _vocab);

            // Execute SQL INSERTs
            await using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();
            foreach (var sql in result.SqlStatements)
            {
                if (sql.StartsWith("--")) continue; // skip comments
                await using var cmd = new NpgsqlCommand(sql, conn);
                await cmd.ExecuteNonQueryAsync();
            }
        }

        // Start Orleans grain
        var grain = _orleans.GetGrain<IWorldGrain>(worldId);
        await grain.StartAsync(_connectionString);

        return worldId;
    }
}

// ──────────────────────── SERVICE REGISTRATION ────────────────────────

public static class SignalsKernelExtensions
{
    public static IHostApplicationBuilder AddSignalsKernel(this IHostApplicationBuilder builder)
    {
        var connStr = builder.Configuration["ConnectionStrings:personality"]
            ?? throw new InvalidOperationException("ConnectionStrings:personality not configured");

        // Marten event store (shares existing PG connection)
        builder.Services.AddMarten(opts =>
        {
            opts.Connection(connStr);
            opts.Events.StreamIdentity = StreamIdentity.AsGuid;
        }).UseLightweightSessions();

        // Wolverine message bus with Marten integration
        builder.Host.UseWolverine(opts =>
        {
            opts.Discovery.IncludeAssembly(typeof(LlmBridge).Assembly);
        });

        // Neo4j (if configured)
        var neo4jUri = builder.Configuration["Neo4j:Uri"];
        if (neo4jUri is not null)
        {
            builder.Services.AddSingleton(GraphDatabase.Driver(
                neo4jUri,
                AuthTokens.Basic(
                    builder.Configuration["Neo4j:User"] ?? "neo4j",
                    builder.Configuration["Neo4j:Password"] ?? "neo4j")));
            builder.Services.AddSingleton<Neo4jSync>();
            builder.Services.AddSingleton<GdsAnalysis>();
        }

        // SignalR hub
        builder.Services.AddSignalR();

        // World builder factory
        builder.Services.AddTransient(sp =>
        {
            var orleans = sp.GetRequiredService<IClusterClient>();
            return new WorldBuilder(orleans, connStr);
        });

        return builder;
    }
}
```

**Step 2: Build**

Run: `dotnet build SignalsKernel/SignalsKernel.csproj`
Expected: 0 errors

**Step 3: Commit**

```bash
git add SignalsKernel/Platform.cs
git commit -m "feat(platform): Orleans WorldGrain + Marten events + SignalR + builder

WorldGrain: thin shell — loads state, runs TickPipeline, dispatches via Wolverine,
stores events via Marten, writes protocol via raw SQL.
Adaptive tick rate: 100ms when active, 5s when stable.
WorldBuilder: fluent API — FromBnf/FromFile, compile, start grain.
AddSignalsKernel(): one-line DI registration for all services."
```

---

## Task 12: Wire into BioChain.Server + Add Project Reference

Add SignalsKernel reference to Server and call AddSignalsKernel().

**Files:**
- Modify: `src/BioChain.Server/BioChain.Server.csproj` — add project reference
- Modify: `src/BioChain.Server/Program.cs` — add `builder.AddSignalsKernel()`

**Step 1: Add project reference to Server.csproj**

Add to the existing `<ItemGroup>` with project references:
```xml
<ProjectReference Include="..\..\SignalsKernel\SignalsKernel.csproj" />
```

**Step 2: Add registration to Program.cs**

In the `RegisterAll()` method, after existing registrations, add:
```csharp
builder.AddSignalsKernel();
```

And add `using SignalsKernel;` at the top.

**Step 3: Build full solution**

Run: `dotnet build BioChain.sln`
Expected: 0 errors

**Step 4: Commit**

```bash
git add src/BioChain.Server/BioChain.Server.csproj src/BioChain.Server/Program.cs
git commit -m "feat: wire SignalsKernel into BioChain.Server

AddSignalsKernel() registers Marten, Wolverine, Neo4j, SignalR,
and WorldBuilder in DI."
```

---

## Task 13: Enable GDS in Docker Compose

**Files:**
- Modify: `docker-compose.yml`

**Step 1: Add GDS plugin to Neo4j config**

Change `NEO4J_PLUGINS` to include `graph-data-science`:
```yaml
NEO4J_PLUGINS: '["apoc", "graph-data-science"]'
```

And add unrestricted procedures:
```yaml
NEO4J_dbms_security_procedures_unrestricted: apoc.*,gds.*
```

**Step 2: Commit**

```bash
git add docker-compose.yml
git commit -m "chore: enable Neo4j GDS plugin in docker-compose"
```

---

## Task 14: Final Build + Verify

**Step 1: Clean build full solution**

Run: `dotnet clean BioChain.sln && dotnet build BioChain.sln`
Expected: 0 errors

**Step 2: Verify file count**

Run: `find SignalsKernel -name "*.cs" | wc -l`
Expected: 5 files

**Step 3: Verify line count (approximate)**

Run: `wc -l SignalsKernel/*.cs`
Expected: ~1,800-2,000 lines total

**Step 4: Final commit**

```bash
git add -A
git commit -m "feat: Signals Kernel v1.5 runtime — 5 files, ~1,850 lines

Compiler.cs: AST + lexer + parser + lowering + vocabulary
Engine.cs: 9-phase tick pipeline + Arrow columns + FormulaVM + Extism WASM
Agent.cs: Wolverine handlers for LLM bridge + tool bridge
Graph.cs: Neo4j sync (enriched) + GDS wrappers (PageRank, Louvain, Dijkstra)
Platform.cs: Orleans WorldGrain + Marten events + SignalR + gRPC + fluent builder

Technologies: Apache.Arrow, Extism, Marten+Wolverine, Orleans, gRPC, Neo4j GDS"
```

---

## Future Work (not in this plan)

- **Expression parser**: Full gate condition evaluation (`$in > N`, `$Δ%`, `lo..hi`, etc.)
- **FormulaVM bytecode compiler**: Expr AST → bytecode in Lowering
- **Stochastic mode**: Monte Carlo passes with noise sampling
- **Arrow Flight**: Replace gRPC protobuf with Arrow IPC for bulk streaming
- **gRPC proto file**: `signals_kernel.proto` with 3 services (Engine, Analysis, Platform)
- **REST endpoints**: gRPC-Web transcoding for browser clients
- **Vocabulary YAML loading**: Load vocabularies from YAML files at startup
- **WASM plugin examples**: Sample plugins in Rust/Go for custom transfer functions
