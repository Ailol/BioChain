namespace BioChain.Parser;

/// <summary>
/// Walks tokenized BNF and emits a list of ParsedCommands.
/// Pure function: no I/O, no SpacetimeDB dependency.
/// </summary>
public static class BnfParser
{
    public static ParseResult Parse(string bnfText)
    {
        var commands = new List<ParsedCommand>();
        var errors = new List<string>();

        var lines = bnfText.Split('\n');
        var section = Section.None;
        string? currentPhase = null;

        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            if (string.IsNullOrEmpty(line)) continue;

            try
            {
                // Section headers
                if (line.StartsWith("@domain:"))
                {
                    commands.Add(new SetDomains(line[8..].Trim()));
                    continue;
                }
                if (line.StartsWith("#"))
                {
                    currentPhase = line[1..].Trim();
                    continue;
                }

                var newSection = DetectSection(line);
                if (newSection != Section.None)
                {
                    section = newSection;
                    // Section line may have content after the tag
                    var rest = StripSectionTag(line, section);
                    if (string.IsNullOrWhiteSpace(rest)) continue;
                    line = rest;
                }

                // Dispatch to section-specific parsing
                switch (section)
                {
                    case Section.R0:
                        ParseR0Line(line, commands, errors, i + 1);
                        break;
                    case Section.R1:
                        ParseR1Line(line, commands, errors, i + 1);
                        break;
                    case Section.R2:
                        ParseR2Line(line, commands, errors, i + 1);
                        break;
                    case Section.R3:
                        ParseR3Line(line, commands, errors, i + 1);
                        break;
                    case Section.Delta:
                        ParseDeltaLine(line, commands, errors, i + 1);
                        break;
                    case Section.M3:
                    case Section.M2:
                    case Section.M1:
                    case Section.M0:
                        ParseMetaLine(line, section, commands, errors, i + 1);
                        break;
                    case Section.Post:
                        ParsePostLine(line, commands, errors, i + 1);
                        break;
                    case Section.None:
                        // Δ declarations before @R0, or inline deltas
                        if (line.StartsWith("Δ(") || line.StartsWith("Δ@"))
                            ParseDeltaLine(line, commands, errors, i + 1);
                        else if (line.StartsWith("Σ∇·") || line.StartsWith("◈") || line.StartsWith("⚡")
                              || line.StartsWith("∮") || line.StartsWith("⊳"))
                            ParsePostLine(line, commands, errors, i + 1);
                        break;
                }
            }
            catch (Exception ex)
            {
                errors.Add($"Line {i + 1}: {ex.Message}");
            }
        }

        return errors.Count > 0 && commands.Count == 0
            ? ParseResult.Fail(errors)
            : new ParseResult(errors.Count == 0, commands, errors);
    }

    // ── Section detection ────────────────────────────────────────────────────

    private enum Section { None, R0, R1, R2, R3, Delta, M3, M2, M1, M0, Post }

    private static Section DetectSection(string line) => line switch
    {
        _ when line.StartsWith("@R0") => Section.R0,
        _ when line.StartsWith("@R1") => Section.R1,
        _ when line.StartsWith("@R2") => Section.R2,
        _ when line.StartsWith("@R3") => Section.R3,
        _ when line.StartsWith("@Δ")  => Section.Delta,
        _ when line.StartsWith("@M3") => Section.M3,
        _ when line.StartsWith("@M2") => Section.M2,
        _ when line.StartsWith("@M1") => Section.M1,
        _ when line.StartsWith("@M0") => Section.M0,
        _ when line.StartsWith("Σ∇·") || line.StartsWith("◈") || line.StartsWith("⚡")
            || line.StartsWith("∮") || line.StartsWith("⊳") => Section.Post,
        _ => Section.None,
    };

    private static string StripSectionTag(string line, Section section) => section switch
    {
        Section.R0 => line[3..],
        Section.R1 => line[3..],
        Section.R2 => line[3..],
        Section.R3 => line[3..],
        Section.Delta => line.StartsWith("@Δ") ? line[2..] : line,
        Section.M3 => line[3..],
        Section.M2 => line[3..],
        Section.M1 => line[3..],
        Section.M0 => line[3..],
        _ => line,
    };

    // ── Section parsers (stubs — each walks the BNF grammar for its rank) ────

    private static void ParseR0Line(string line, List<ParsedCommand> cmds, List<string> errs, int lineNum)
    {
        // TODO: Parse R0 chains, branches, rings, gated edges, merges
        // Extract {TYPE:CODE[STATE]@REGION FIELD} nodes and edge operators between them
        // Each node → InsertNode command
        // Each operator → InsertEdge command
    }

    private static void ParseR1Line(string line, List<ParsedCommand> cmds, List<string> errs, int lineNum)
    {
        // TODO: Parse ∫{UNIT}←( inputs )→output:activation
        // → InsertNode for the unit + InsertIntegration
    }

    private static void ParseR2Line(string line, List<ParsedCommand> cmds, List<string> errs, int lineNum)
    {
        // TODO: Parse {SOURCE}⊲{TARGET}[protocol_spec]
        // → InsertProtocol
    }

    private static void ParseR3Line(string line, List<ParsedCommand> cmds, List<string> errs, int lineNum)
    {
        // TODO: Parse ⊗( conditions )⟹effect
        // → InsertTensor
    }

    private static void ParseDeltaLine(string line, List<ParsedCommand> cmds, List<string> errs, int lineNum)
    {
        // TODO: Parse Δ(signal@region)=value and Δ@Rn: blocks
        // Inline: Δ(L.nt:DA@VTA)=+0.3 → InsertDeltaOp (rank R0) or node delta
        // Block:  Δ@R1: ... → InsertDeltaOp (rank R1)
    }

    private static void ParseMetaLine(string line, Section section, List<ParsedCommand> cmds, List<string> errs, int lineNum)
    {
        // TODO: Parse σ̃, ∫̃, ⊲̃, ⊗̃ declarations
        // → InsertMetaOp
    }

    private static void ParsePostLine(string line, List<ParsedCommand> cmds, List<string> errs, int lineNum)
    {
        // TODO: Parse Σ∇·, ◈, ⚡, ∮, ⊳
        // Σ∇·(CODE)=+n/−m → InsertDiag (Conservation)
        // ◈name=... → InsertDiag (Composite)
        // ⚡type:{chain} → InsertDiag (Dysreg)
        // ∮(SIGNAL@REGION)=... → InsertConv
        // ⊳(SIGNAL@REGION,+TIME)=... → InsertConv
        // ⚡allo/resist/diverge/unstable/lock/cascade → InsertConv
    }
}
