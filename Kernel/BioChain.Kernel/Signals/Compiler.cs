namespace BioChain.Kernel.Signals;

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
    public sealed record Window(Ref Signal, int WindowMs, string Agg) : Expr;
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
        ["\u2192"] = Tok.Arrow, ["\u22A3"] = Tok.InhibitArrow, ["\u22A9"] = Tok.ModulateArrow,
        ["\u2297"] = Tok.BlockArrow, ["\u2298\u2192"] = Tok.DepleteArrow, ["\u21CC"] = Tok.BidiArrow,
        ["\u2225"] = Tok.ParallelOp, ["\u25C8"] = Tok.ConvertArrow,
        ["\u27F3\u207B"] = Tok.FeedbackNeg, ["\u27F3\u207A"] = Tok.FeedbackPos,
    };

    private static readonly HashSet<string> StateSymbols = ["\u2191\u2191", "\u2191", "\u2248", "\u2193", "\u2193\u2193", "~", "\u2298", "\u25CF"];

    public static List<Token> Tokenize(string source)
    {
        var tokens = new List<Token>();
        int i = 0, line = 1, col = 1;
        var span = source.AsSpan();

        while (i < span.Length)
        {
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
                _ => Tok.Eof
            };
            tokens.Add(new(punct, ch.ToString(), line, col));
            i++; col++;
        }

        tokens.Add(new(Tok.Eof, "", line, col));
        return tokens;
    }
}

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
            _ => SkipUnknown(),
        };
    }

    private AstNode? SkipUnknown() { _pos++; return null; }

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
        if (expr.Contains("\u03B5:"))
        {
            var parts = expr.Split("\u03B5:");
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
        var type = Expect(Tok.Ident).Text;
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

    private static bool IsOperator(Tok kind) => kind is Tok.Arrow or Tok.InhibitArrow
        or Tok.ModulateArrow or Tok.BlockArrow or Tok.DepleteArrow
        or Tok.BidiArrow or Tok.ParallelOp or Tok.ConvertArrow;

    private static (string op, string cls) ClassifyOperator(Tok kind) => kind switch
    {
        Tok.Arrow => ("\u2192", "causal"),
        Tok.InhibitArrow => ("\u22A3", "inhibitory"),
        Tok.ModulateArrow => ("\u22A9", "modulatory"),
        Tok.BlockArrow => ("\u2297", "blocking"),
        Tok.DepleteArrow => ("\u2298\u2192", "depletion"),
        Tok.BidiArrow => ("\u21CC", "bidirectional"),
        Tok.ParallelOp => ("\u2225", "parallel"),
        Tok.ConvertArrow => ("\u25C8", "conversion"),
        _ => ("\u2192", "causal"),
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

// ──────────────────────── LOWERING ────────────────────────

public sealed record CompileResult(
    string[] SqlStatements,
    byte[] FormulaBytecode,
    int[][] TopoLevels,
    List<string> Warnings);

public static class Lowering
{
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

        // Topo sort (compile-time variant — operates on Ref-based AST nodes)
        var topo = ComputeTopoSort(signals, edges);

        var bytecode = Array.Empty<byte>();
        return new CompileResult([.. sql], bytecode, topo, warnings);
    }

    private static string EmitSignalInsert(SignalDecl s, Guid subjectId) =>
        $"INSERT INTO signal (entity_id, type, code, state, value, baseline, confidence) " +
        $"VALUES ('{subjectId}', '{s.Type}', '{s.Id.Code}', '{s.State ?? "\u2248"}', " +
        $"{s.Value?.ToString() ?? "NULL"}, {s.Baseline?.ToString() ?? "NULL"}, {s.Confidence});";

    private static string EmitEdgeInsert(EdgeDecl e, Guid subjectId) =>
        $"INSERT INTO edge (entity_id, source_type, source_id, target_type, target_id, " +
        $"operator, operator_class, gain, noise_sigma, transfer_fn, delay_ms, clamp_lo, clamp_hi) " +
        $"SELECT '{subjectId}', 'signal', s.id, 'signal', t.id, " +
        $"'{e.Op}', '{e.OpClass}', {e.Gain}, {e.NoiseSigma}, '{e.TransferFn}', {e.DelayMs}, " +
        $"{e.ClampLo?.ToString() ?? "NULL"}, {e.ClampHi?.ToString() ?? "NULL"} " +
        $"FROM signal s, signal t WHERE s.code = '{e.Source.Code}' AND t.code = '{e.Target.Code}' " +
        $"AND s.entity_id = '{subjectId}' AND t.entity_id = '{subjectId}';";

    private static string EmitGateInsert(GateDecl g, Guid subjectId) =>
        $"INSERT INTO gate (entity_id, code, type, expression) VALUES ('{subjectId}', '{g.Code}', '{g.Type}', '{g.Expression ?? ""}');";

    private static string EmitLlmGateInsert(LlmGateDecl g, Guid subjectId) =>
        $"INSERT INTO gate (entity_id, code, type, prompt, model, parse_map, fallback_expr, timeout_ms, cache_ms) " +
        $"VALUES ('{subjectId}', '{g.Code}', 'llm', '{Esc(g.Prompt)}', '{g.Model}', " +
        $"'{g.ParseMap ?? ""}', '{g.Fallback}', {g.TimeoutMs}, {g.CacheMs});";

    private static string EmitConstraintInsert(ConstraintDecl c, Guid subjectId) =>
        $"INSERT INTO constraint_def (entity_id, type, expression, epsilon) " +
        $"VALUES ('{subjectId}', '{c.Type}', '{Esc(c.Expression)}', {c.Epsilon?.ToString() ?? "NULL"});";

    private static string EmitToolInsert(ToolDecl t, Guid subjectId) =>
        $"INSERT INTO tool (entity_id, code, invoke, input_refs, output_refs, timeout_ms, retry_count) " +
        $"VALUES ('{subjectId}', '{t.Code}', '{t.Invoke}', " +
        $"ARRAY[{string.Join(",", t.Inputs.Select(r => $"'{r.Code}'"))}], " +
        $"ARRAY[{string.Join(",", t.Outputs.Select(r => $"'{r.Code}'"))}], {t.TimeoutMs}, {t.RetryCount});";

    private static string EmitFailInsert(FailDecl f, Guid subjectId) =>
        $"-- FAIL conditions tracked in-memory by Engine.cs FailPhase";

    private static string EmitBindInsert(BindDecl b, Guid subjectId) =>
        $"-- BIND rules tracked in-memory by Engine.cs BindPhase";

    /// <summary>Compile-time topo sort on Ref-based AST (different types than runtime GraphUtils).</summary>
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
