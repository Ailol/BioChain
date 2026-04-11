// ═══════════════════════════════════════════════════════════════════
//  parser_core.rs — BioChain BNF parser (all 4 pipelines) + linter
//  Rewritten from scratch 2026-03-13
//  No regex, no panics, no infinite loops. Pure &str manipulation.
// ═══════════════════════════════════════════════════════════════════

// ── 1. Data structures ──────────────────────────────────────────

#[derive(Clone, Debug, Default)]
pub struct ParsedBase {
    pub domains: Vec<String>,
    pub phase: Option<String>,
    pub chains: Vec<Vec<ChainElement>>,
    pub seeds: Vec<ParsedSeed>,
    pub integrations: Vec<ParsedIntegration>,
    pub protocols: Vec<ParsedProtocol>,
    pub conditionals: Vec<ParsedConditional>,
    pub composites: Vec<ParsedComposite>,
    pub dysregs: Vec<ParsedDysreg>,
    pub observables: Vec<ParsedObservable>,
}

#[derive(Clone, Debug)]
pub enum ChainElement {
    Node(ParsedNode),
    Edge(String),
    Branch(Vec<Vec<ChainElement>>),
    Gate(String, String),
    Terminal(Option<String>),
}

#[derive(Clone, Debug)]
pub struct ParsedNode {
    pub code: String,
    pub kind: String,
    pub region: Option<String>,
    pub state: Option<ParsedState>,
    pub props: Vec<(String, String)>,
    pub is_root: bool,
    pub terminal: Option<String>,
}

#[derive(Clone, Debug)]
pub struct ParsedState {
    pub sym: String,
    pub val: Option<f32>,
    pub delta_sign: Option<String>,
    pub delta_val: Option<f32>,
}

#[derive(Clone, Debug)]
pub struct ParsedSeed {
    pub code: String,
    pub region: String,
    pub sign: String,
    pub val: f32,
}

#[derive(Clone, Debug)]
pub struct ParsedIntegration {
    pub unit: ParsedNode,
    pub inputs: Vec<IntegInputParsed>,
    pub output_code: String,
    pub output_region: String,
    pub mode: String,
}

#[derive(Clone, Debug)]
pub struct IntegInputParsed {
    pub code: String,
    pub region: String,
    pub sign: String,
    pub val: Option<f32>,
}

#[derive(Clone, Debug)]
pub struct ParsedProtocol {
    pub source_code: String,
    pub source_region: Option<String>,
    pub edge_label: String,
    pub gain: Option<f32>,
    pub polarity: Option<String>,
    pub tau_class: Option<String>,
    pub tau_value: Option<String>,
    pub gate_code: Option<String>,
    pub gate_region: Option<String>,
    pub gate_threshold: Option<String>,
    pub coupling: Option<String>,
}

#[derive(Clone, Debug)]
pub struct ParsedConditional {
    pub conditions: Vec<CondItem>,
    pub logic: String,
    pub effect_code: String,
    pub effect_region: String,
    pub effect_action: String,
    pub effect_value: Option<f32>,
    pub effect_switch: Option<String>,
}

#[derive(Clone, Debug)]
pub struct CondItem {
    pub code: String,
    pub region: String,
    pub threshold: String,
    pub negated: bool,
}

#[derive(Clone, Debug)]
pub struct ParsedComposite {
    pub name: String,
    pub refs: Vec<(String, String)>,
}

#[derive(Clone, Debug)]
pub struct ParsedDysreg {
    pub dtype: String,
    pub elements: Vec<ChainElement>,
}

#[derive(Clone, Debug)]
pub struct ParsedObservable {
    pub name: String,
    pub target_code: String,
    pub target_region: String,
    pub detail: String,
}

#[derive(Clone, Debug)]
pub struct ParsedDelta {
    pub rank: u32,
    pub trigger_code: String,
    pub trigger_region: String,
    pub trigger_state: String,
    pub target_code: String,
    pub target_region: String,
    pub change_prop: String,
    pub change_before: String,
    pub change_after: String,
    pub tau: String,
    pub depends: Vec<String>,
    pub status: Option<String>,
    pub cascade_name: Option<String>,
    pub tensor_expr: Option<String>,
}

#[derive(Clone, Debug)]
pub struct ParsedMetaEntry {
    pub rank: String,
    pub window_kind: String,
    pub window_value: String,
    pub target_code: String,
    pub target_region: String,
    pub target_property: String,
    pub target_program: String,
    pub reversible: Option<String>,
    pub unlocks_with: Option<String>,
    pub pull: Option<String>,
}

#[derive(Clone, Debug)]
pub enum ParsedConvEntry {
    State {
        signal_code: String,
        signal_region: String,
        vectors: Vec<(String, String, String)>,
        diagnosis: String,
    },
    Trajectory {
        signal_code: String,
        signal_region: String,
        timeframe: String,
        predicted: String,
        rationale: String,
        confidence: Option<String>,
    },
    Risk {
        risk_name: String,
        risk_target: Option<String>,
        risk_distance: Option<String>,
        risk_window: Option<String>,
        risk_reversible_before: Option<String>,
        risk_reversible_after: Option<String>,
    },
    Monitor {
        measurement: String,
        flag_ref: Option<String>,
        note: Option<String>,
    },
    Flag {
        flag_type: String,
        expr: String,
    },
}

// ── Lint types ──

#[derive(Clone, Debug, PartialEq)]
pub enum LintLevel {
    Error,
    Warn,
}

#[derive(Clone, Debug)]
pub struct LintIssue {
    pub level: LintLevel,
    pub message: String,
    pub line: Option<usize>,
}

#[derive(Clone, Debug)]
pub struct LintResult {
    pub issues: Vec<LintIssue>,
    pub node_count: usize,
    pub edge_count: usize,
    pub chain_count: usize,
    pub valid: bool,
}

// ── 2. Cursor helper ────────────────────────────────────────────

struct Cur<'a> {
    s: &'a str,
    pos: usize,
}

impl<'a> Cur<'a> {
    fn new(s: &'a str) -> Self {
        Self { s, pos: 0 }
    }
    fn rest(&self) -> &'a str {
        &self.s[self.pos..]
    }
    fn at_end(&self) -> bool {
        self.pos >= self.s.len()
    }
    fn peek(&self) -> Option<char> {
        self.rest().chars().next()
    }
    fn adv(&mut self) -> Option<char> {
        let ch = self.rest().chars().next()?;
        self.pos += ch.len_utf8();
        Some(ch)
    }
    fn skip_ws(&mut self) {
        while let Some(c) = self.peek() {
            if c.is_whitespace() {
                self.adv();
            } else {
                break;
            }
        }
    }
    fn eat(&mut self, ch: char) -> bool {
        if self.peek() == Some(ch) {
            self.adv();
            true
        } else {
            false
        }
    }
    fn eat_str(&mut self, s: &str) -> bool {
        if self.rest().starts_with(s) {
            self.pos += s.len();
            true
        } else {
            false
        }
    }
    fn take_until(&mut self, stop: char) -> &'a str {
        let start = self.pos;
        let mut guard = 0usize;
        while let Some(c) = self.peek() {
            if c == stop || guard > 10_000 {
                break;
            }
            self.adv();
            guard += 1;
        }
        &self.s[start..self.pos]
    }
    fn take_until_any(&mut self, stops: &[char]) -> &'a str {
        let start = self.pos;
        let mut guard = 0usize;
        while let Some(c) = self.peek() {
            if stops.contains(&c) || guard > 10_000 {
                break;
            }
            self.adv();
            guard += 1;
        }
        &self.s[start..self.pos]
    }
    /// Extract balanced content between open/close chars, returns inner content
    fn balanced(&mut self, open: char, close: char) -> Option<&'a str> {
        if self.peek() != Some(open) {
            return None;
        }
        self.adv();
        let start = self.pos;
        let mut depth = 1i32;
        let mut guard = 0usize;
        while depth > 0 && guard < 50_000 {
            match self.adv() {
                Some(c) if c == open => depth += 1,
                Some(c) if c == close => depth -= 1,
                Some(_) => {}
                None => return None,
            }
            guard += 1;
        }
        if depth != 0 {
            return None;
        }
        Some(&self.s[start..self.pos - close.len_utf8()])
    }
}

// ── 3. Known type prefixes (longest first) ──────────────────────

const KNOWN_TYPES: &[&str] = &[
    // struct — longest first to avoid prefix collisions
    "N.glia.mg",
    "N.glia.as",
    "N.glia",
    "N.pyr",
    "N.da",
    "N.5ht",
    "N.gaba",
    "N.gran",
    "N.ent",
    "N.eec",
    "N.icc",
    "P.oligo",
    "P.agg",
    "B.gut",
    "B.bbb",
    "B.beh",
    // chem
    "L.nt",
    "L.h",
    "L.p",
    "L.cb",
    "L.ni",
    "L.ns",
    "L.mb",
    // elec
    "E.lf",
    "E.gj",
    "E.v",
    "Ch.vg",
    "Ch.mec",
    "Ch.trp",
    "Ch",
    // meta
    "M.atp",
    "M.glc",
    "M.ros",
    "M.o2",
    "Mt",
    // single-letter last
    "Gp",
    "2m",
    "Ph",
    "TF",
    "NR",
    "R",
    "K",
    "G",
    "T",
    "E",
    "V",
    "N",
    "B",
    "M",
];

// ── 4. Node parser ──────────────────────────────────────────────

/// Parse a single node from content between { and }.
/// Format: [TYPE:]CODE[STATE](PROPS)@REGION[(PROPS)]
fn parse_node_inner(content: &str) -> Result<ParsedNode, String> {
    let s = content.trim();
    if s.is_empty() {
        return Err("Empty node".into());
    }

    // 1) Try to match TYPE: prefix (longest first)
    let mut kind = String::new();
    let mut rest = s;
    for t in KNOWN_TYPES {
        if rest.starts_with(t) {
            let after = &rest[t.len()..];
            if after.starts_with(':') {
                kind = t.to_string();
                rest = &after[1..]; // skip the ':'
                break;
            }
        }
    }

    // 2) Split on last '@' to separate code_part from region
    let (code_part, region_raw) = if let Some(at) = rest.rfind('@') {
        (&rest[..at], Some(&rest[at + 1..]))
    } else {
        (rest, None)
    };

    // 3) Parse code_part: CODE + optional [STATE] + optional (PROPS)
    let mut cur = Cur::new(code_part);
    let code_raw = cur.take_until_any(&['[', '(']);
    let code = code_raw.trim().to_string();

    let state = if cur.eat('[') {
        let sc = cur.take_until(']');
        cur.eat(']');
        Some(parse_state_str(sc))
    } else {
        None
    };

    let mut props = Vec::new();
    if cur.eat('(') {
        let pc = cur.take_until(')');
        cur.eat(')');
        props.extend(parse_props_str(pc));
    }

    // 4) Parse region part: REGION + optional (PROPS)
    let region = region_raw.map(|rp| {
        let mut rc = Cur::new(rp);
        let reg = rc.take_until_any(&['(', '[']);
        let reg_str = reg.trim().to_string();
        if rc.eat('(') {
            let pc = rc.take_until(')');
            rc.eat(')');
            props.extend(parse_props_str(pc));
        }
        reg_str
    });
    let region = region.filter(|r| !r.is_empty());

    Ok(ParsedNode {
        code,
        kind,
        region,
        state,
        props,
        is_root: false,
        terminal: None,
    })
}

/// Extract a {node} at the current position. Returns (ParsedNode, bytes_consumed).
fn extract_braced_node(s: &str) -> Result<(ParsedNode, usize), String> {
    if !s.starts_with('{') {
        return Err(format!("Expected '{{' at: {}", &s[..s.len().min(20)]));
    }
    // Find matching '}'
    let mut depth = 0i32;
    let mut end = 0;
    for (i, c) in s.char_indices() {
        match c {
            '{' => depth += 1,
            '}' => {
                depth -= 1;
                if depth == 0 {
                    end = i + c.len_utf8();
                    break;
                }
            }
            _ => {}
        }
        if i > 5000 {
            return Err("Brace not closed within 5000 chars".into());
        }
    }
    if depth != 0 {
        return Err("Unmatched '{'".into());
    }
    let inner = &s['{'.len_utf8()..end - '}'.len_utf8()];
    let node = parse_node_inner(inner)?;
    Ok((node, end))
}

fn parse_state_str(s: &str) -> ParsedState {
    let s = s.trim();
    // Could be "++", "+", "=", "~", "-", "--", "X", "*", etc.
    // Check for val after ':'
    if let Some(colon) = s.rfind(':') {
        let sym = s[..colon].trim().to_string();
        let val = s[colon + 1..].trim().parse::<f32>().ok();
        ParsedState { sym, val, delta_sign: None, delta_val: None }
    } else {
        ParsedState { sym: s.to_string(), val: None, delta_sign: None, delta_val: None }
    }
}

fn parse_props_str(s: &str) -> Vec<(String, String)> {
    let s = s.trim();
    if s.is_empty() {
        return Vec::new();
    }
    // Props are either "key:value" pairs or bare values
    // For bare values like "(Gs)", store as ("", "Gs")
    // For "sensitivity:norm→des", store as ("sensitivity", "norm→des")
    let mut result = Vec::new();
    for part in split_top_level(s, ',') {
        let p = part.trim();
        if p.is_empty() {
            continue;
        }
        // Use first ':' as separator (not last, since values may contain ':')
        if let Some(colon) = p.find(':') {
            let k = p[..colon].trim();
            let v = p[colon + 1..].trim();
            result.push((k.to_string(), v.to_string()));
        } else {
            result.push((String::new(), p.to_string()));
        }
    }
    result
}

/// Split string by delimiter, respecting balanced parens/brackets/braces.
fn split_top_level(s: &str, delim: char) -> Vec<&str> {
    let mut parts = Vec::new();
    let mut start = 0;
    let mut depth_paren = 0i32;
    let mut depth_bracket = 0i32;
    let mut depth_brace = 0i32;
    for (i, c) in s.char_indices() {
        match c {
            '(' => depth_paren += 1,
            ')' => depth_paren -= 1,
            '[' => depth_bracket += 1,
            ']' => depth_bracket -= 1,
            '{' => depth_brace += 1,
            '}' => depth_brace -= 1,
            _ if c == delim && depth_paren == 0 && depth_bracket == 0 && depth_brace == 0 => {
                parts.push(&s[start..i]);
                start = i + c.len_utf8();
            }
            _ => {}
        }
    }
    parts.push(&s[start..]);
    parts
}

// ── 5. Chain parser ─────────────────────────────────────────────

/// Edge connectors (multi-byte unicode first, then ASCII)
const EDGE_ARROW: &str = "\u{2192}"; // →
const EDGE_INHIBIT: &str = "\u{22A3}"; // ⊣
const EDGE_MODULATE: &str = "~>";
const EDGE_AMPLIFY: &str = "=>";
const EDGE_GATE: &str = "|>";

/// Terminal tokens
const TERM_LOOP_POS: &str = "\u{21BA}\u{207A}"; // ↺⁺
const TERM_LOOP_NEG: &str = "\u{21BA}\u{207B}"; // ↺⁻
const TERM_LOOP_ZERO: &str = "\u{21BA}\u{2070}"; // ↺⁰
const TERM_DEPLETION: &str = "\u{2192}\u{2298}"; // →⊘
const TERM_STRUCT: &str = "\u{2192}\u{25A1}"; // →□
const TERM_STEADY: &str = "\u{2192}\u{224B}"; // →≋
const TERM_META: &str = "\u{2192}\u{0394}m"; // →Δm

fn try_parse_terminal(s: &str) -> Option<(&str, usize)> {
    let terminals: &[(&str, &str)] = &[
        (TERM_LOOP_POS, "\u{21BA}\u{207A}"),
        (TERM_LOOP_NEG, "\u{21BA}\u{207B}"),
        (TERM_LOOP_ZERO, "\u{21BA}\u{2070}"),
        (TERM_DEPLETION, "\u{2192}\u{2298}"),
        (TERM_STRUCT, "\u{2192}\u{25A1}"),
        (TERM_STEADY, "\u{2192}\u{224B}"),
        (TERM_META, "\u{2192}\u{0394}m"),
    ];
    for (tok, _) in terminals {
        if s.starts_with(tok) {
            return Some((tok, tok.len()));
        }
    }
    // Also handle bare ↺ (without superscript)
    if s.starts_with('\u{21BA}') {
        return Some(("\u{21BA}", '\u{21BA}'.len_utf8()));
    }
    None
}

fn try_parse_edge(s: &str) -> Option<(&str, usize)> {
    let edges: &[&str] = &[EDGE_ARROW, EDGE_INHIBIT, EDGE_MODULATE, EDGE_AMPLIFY, EDGE_GATE];
    for e in edges {
        if s.starts_with(e) {
            return Some((e, e.len()));
        }
    }
    None
}

/// Parse a chain line into ChainElement list.
/// Input: the line content after optional ⊙ prefix.
fn parse_chain_line(line: &str, is_root: bool) -> Result<Vec<ChainElement>, String> {
    let mut elements = Vec::new();
    let mut cur = Cur::new(line);
    let mut first_node = true;
    let mut guard = 0usize;

    while !cur.at_end() && guard < 5000 {
        guard += 1;
        cur.skip_ws();
        if cur.at_end() {
            break;
        }

        let rest = cur.rest();

        // Terminal?
        if let Some((tok, len)) = try_parse_terminal(rest) {
            elements.push(ChainElement::Terminal(Some(tok.to_string())));
            cur.pos += len;
            // Consume trailing parenthesized annotations like (mechanism_impaired)
            loop {
                cur.skip_ws();
                if cur.peek() == Some('(') {
                    let _ = cur.balanced('(', ')');
                } else {
                    break;
                }
            }
            continue;
        }

        // Edge connector?
        if let Some((tok, len)) = try_parse_edge(rest) {
            // Check if this is a terminal like →⊘ or →≋
            let after_arrow = &rest[len..];
            if tok == EDGE_ARROW {
                if let Some((term_tok, term_len)) = try_parse_terminal(rest) {
                    elements.push(ChainElement::Terminal(Some(term_tok.to_string())));
                    cur.pos += term_len;
                    continue;
                }
                // Check for terminal symbols after →
                if after_arrow.starts_with('\u{2298}')
                    || after_arrow.starts_with('\u{25A1}')
                    || after_arrow.starts_with('\u{224B}')
                {
                    // This is →⊘, →□, →≋ — already handled by try_parse_terminal
                    // but just in case, handle it here too
                    let term_char = after_arrow.chars().next().unwrap();
                    let total = len + term_char.len_utf8();
                    let tok_str = &rest[..total];
                    elements.push(ChainElement::Terminal(Some(tok_str.to_string())));
                    cur.pos += total;
                    continue;
                }
            }
            elements.push(ChainElement::Edge(tok.to_string()));
            cur.pos += len;
            continue;
        }

        // Node?
        if rest.starts_with('{') {
            let (mut node, consumed) = extract_braced_node(rest)?;
            if first_node && is_root {
                node.is_root = true;
            }
            first_node = false;
            elements.push(ChainElement::Node(node));
            cur.pos += consumed;
            continue;
        }

        // Skip any other character (annotation, whitespace, etc.)
        cur.adv();
    }

    Ok(elements)
}

// ── 6. parse_base ───────────────────────────────────────────────

pub fn parse_base(text: &str) -> Result<ParsedBase, String> {
    let mut result = ParsedBase::default();

    for (line_idx, raw_line) in text.lines().enumerate() {
        let line = raw_line.trim();
        if line.is_empty() {
            continue;
        }

        let res: Result<(), String> = (|| {
            // @domain: ...
            if line.starts_with("@domain:") || line.starts_with("@domain ") {
                let rest = line.trim_start_matches("@domain:").trim_start_matches("@domain ").trim();
                result.domains = rest.split_whitespace().map(|s| s.to_string()).collect();
                return Ok(());
            }

            // #tags (phase)
            if line.starts_with('#') && !line.starts_with("##") {
                let tags: Vec<&str> = line.split_whitespace()
                    .filter(|t| t.starts_with('#'))
                    .map(|t| t.trim_start_matches('#'))
                    .collect();
                if !tags.is_empty() {
                    result.phase = Some(tags.join(","));
                }
                return Ok(());
            }

            // ::fates ...
            if line.starts_with("::fates") {
                // just metadata, skip
                return Ok(());
            }

            // ::open_ends ...
            if line.starts_with("::") {
                // metadata, skip
                return Ok(());
            }

            // Δ(CODE@REGION)=+/- — seed
            if line.starts_with("\u{0394}(") {
                return parse_seed_line(line, &mut result.seeds);
            }

            // ∫{ — integration
            if line.starts_with("\u{222B}{") || line.starts_with("\u{222B} {") {
                return parse_integration_line(line, &mut result.integrations);
            }

            // ⊗( — conditional
            if line.starts_with("\u{2297}(") || line.starts_with("\u{2297} (") {
                return parse_conditional_line(line, &mut result.conditionals);
            }

            // ◈ — composite
            if line.starts_with('\u{25C8}') {
                return parse_composite_line(line, &mut result.composites);
            }

            // ⚡ — dysreg
            if line.starts_with('\u{26A1}') {
                return parse_dysreg_line(line, &mut result.dysregs);
            }

            // ⊕ (not ⊕⊳) — observable
            if line.starts_with('\u{2295}') && !line.starts_with("\u{2295}\u{22B3}") {
                return parse_observable_line(line, &mut result.observables);
            }

            // ⊙{ — root chain
            if line.starts_with("\u{2299}{") || line.starts_with("\u{2299} {") {
                let chain_start = if line.starts_with("\u{2299} ") {
                    &line["\u{2299} ".len()..]
                } else {
                    &line["\u{2299}".len()..]
                };
                let chain = parse_chain_line(chain_start, true)?;
                if !chain.is_empty() {
                    result.chains.push(chain);
                }
                return Ok(());
            }

            // CASCADE_TAG:{... or CASCADE_TAG: {... — cascade-tagged chain
            // Tags: GPCR.Gs, GPCR.Gi, GPCR.Gq, GPCR.G12, NUCLEAR, RTK, CYTOKINE,
            //        IONOTROPIC, VAGAL, GUT_HORMONE, ENZ, RECYCLE, TRANSPORT
            if let Some(chain_start) = try_strip_cascade_tag(line) {
                if contains_protocol_op(chain_start) {
                    return parse_protocol_line(chain_start, &mut result.protocols);
                }
                let chain = parse_chain_line(chain_start, false)?;
                if !chain.is_empty() {
                    result.chains.push(chain);
                }
                return Ok(());
            }

            // {... — chain or protocol
            if line.starts_with('{') {
                // Detect protocol: line contains ⊲ (not ⊲̃)
                if contains_protocol_op(line) {
                    return parse_protocol_line(line, &mut result.protocols);
                }
                let chain = parse_chain_line(line, false)?;
                if !chain.is_empty() {
                    result.chains.push(chain);
                }
                return Ok(());
            }

            // Unknown line — skip silently
            Ok(())
        })();

        if let Err(e) = res {
            log::warn!("BASE parse line {}: {}", line_idx + 1, e);
        }
    }

    Ok(result)
}

/// Known cascade tag names (longest first to avoid prefix collisions)
const CASCADE_TAGS: &[&str] = &[
    "GPCR.G12", "GPCR.Gs", "GPCR.Gi", "GPCR.Gq",
    "GUT_HORMONE", "IONOTROPIC", "TRANSPORT", "CYTOKINE",
    "NUCLEAR", "RECYCLE", "VAGAL", "RTK", "ENZ",
];

/// Try to strip a cascade tag prefix from a line.
/// Returns the chain portion after the tag (with leading whitespace trimmed)
/// if the line starts with a valid cascade tag followed by ':'.
/// Handles both `TAG:{...` and `TAG: {...` (with optional space).
fn try_strip_cascade_tag(line: &str) -> Option<&str> {
    for tag in CASCADE_TAGS {
        if line.starts_with(tag) {
            let after_tag = &line[tag.len()..];
            if let Some(rest) = after_tag.strip_prefix(':') {
                let trimmed = rest.trim_start();
                if trimmed.starts_with('{') || trimmed.starts_with('\u{2299}') {
                    return Some(trimmed);
                }
            }
        }
    }
    None
}

/// Check if line contains the protocol operator ⊲ (but not ⊲̃ with combining tilde)
fn contains_protocol_op(line: &str) -> bool {
    let bytes = line.as_bytes();
    let tri = "\u{22B2}".as_bytes(); // ⊲ = 3 bytes: E2 8A B2
    let tilde = "\u{0303}".as_bytes(); // combining tilde = 2 bytes: CC 83
    for i in 0..bytes.len().saturating_sub(tri.len() - 1) {
        if bytes[i..].starts_with(tri) {
            // Check it's NOT followed by combining tilde
            let after = i + tri.len();
            if after + tilde.len() <= bytes.len() && bytes[after..].starts_with(tilde) {
                continue; // this is ⊲̃, skip
            }
            return true;
        }
    }
    false
}

fn parse_seed_line(line: &str, seeds: &mut Vec<ParsedSeed>) -> Result<(), String> {
    // Δ(CODE@REGION)=SIGN[VAL]
    let mut cur = Cur::new(line);
    cur.eat_str("\u{0394}"); // Δ
    let inner = cur.balanced('(', ')').ok_or("Seed: missing parens")?;
    // inner = "CODE@REGION"
    let (code, region) = split_code_region(inner)?;

    cur.skip_ws();
    if !cur.eat('=') {
        return Err("Seed: missing '='".into());
    }
    let rest = cur.rest().trim();
    let (sign, val) = if rest.starts_with("++") {
        let num = rest[2..].trim().parse::<f32>().unwrap_or(2.0);
        ("++".to_string(), num)
    } else if rest.starts_with("--") {
        let num = rest[2..].trim().parse::<f32>().unwrap_or(2.0);
        ("--".to_string(), num)
    } else if rest.starts_with('+') {
        let num = rest[1..].trim().parse::<f32>().unwrap_or(1.0);
        ("+".to_string(), num)
    } else if rest.starts_with('-') {
        let num = rest[1..].trim().parse::<f32>().unwrap_or(1.0);
        ("-".to_string(), num)
    } else if rest.starts_with('=') {
        ("=".to_string(), 0.0)
    } else if rest.starts_with('~') {
        ("~".to_string(), 0.5)
    } else {
        ("+".to_string(), 1.0)
    };

    seeds.push(ParsedSeed { code, region, sign, val });
    Ok(())
}

fn split_code_region(s: &str) -> Result<(String, String), String> {
    let s = s.trim();
    if let Some(at) = s.rfind('@') {
        Ok((s[..at].trim().to_string(), s[at + 1..].trim().to_string()))
    } else {
        Err(format!("Expected CODE@REGION, got: {}", s))
    }
}

fn parse_integration_line(line: &str, integs: &mut Vec<ParsedIntegration>) -> Result<(), String> {
    // ∫{UNIT}←(INPUTS)→{OUTPUT}:MODE
    let mut cur = Cur::new(line);
    cur.eat_str("\u{222B}"); // ∫
    cur.skip_ws();

    // Parse unit node
    let unit_str = cur.balanced('{', '}').ok_or("Integration: missing unit braces")?;
    let unit = parse_node_inner(unit_str)?;

    cur.skip_ws();
    // ← arrow
    if !cur.eat_str("\u{2190}") {
        // try ASCII fallback "<-"
        if !cur.eat_str("<-") {
            return Err("Integration: missing ← arrow".into());
        }
    }

    // (INPUTS)
    let inputs_str = cur.balanced('(', ')').ok_or("Integration: missing input parens")?;
    let inputs = parse_integ_inputs(inputs_str)?;

    cur.skip_ws();
    // → arrow
    if !cur.eat_str("\u{2192}") && !cur.eat_str("->") {
        return Err("Integration: missing → arrow to output".into());
    }
    cur.skip_ws();

    // {OUTPUT}
    let out_str = cur.balanced('{', '}').ok_or("Integration: missing output braces")?;
    let out_node = parse_node_inner(out_str)?;

    // :MODE
    cur.skip_ws();
    let mode = if cur.eat(':') {
        cur.rest().trim().to_string()
    } else {
        "thr".to_string()
    };

    integs.push(ParsedIntegration {
        unit,
        inputs,
        output_code: out_node.code,
        output_region: out_node.region.unwrap_or_default(),
        mode,
    });
    Ok(())
}

fn parse_integ_inputs(s: &str) -> Result<Vec<IntegInputParsed>, String> {
    let mut inputs = Vec::new();
    for part in split_top_level(s, ',') {
        let p = part.trim();
        if p.is_empty() {
            continue;
        }
        // FORMAT: CODE@REGION:SIGN[VAL]
        // Use rfind(':') to split — the sign is after the last ':'
        if let Some(colon) = p.rfind(':') {
            let node_part = &p[..colon];
            let sign_part = p[colon + 1..].trim();
            let (code, region) = if let Some(at) = node_part.rfind('@') {
                (node_part[..at].trim().to_string(), node_part[at + 1..].trim().to_string())
            } else {
                (node_part.trim().to_string(), String::new())
            };
            let (sign, val) = parse_sign_val(sign_part);
            inputs.push(IntegInputParsed { code, region, sign, val });
        }
    }
    Ok(inputs)
}

fn parse_sign_val(s: &str) -> (String, Option<f32>) {
    let s = s.trim();
    if s.is_empty() {
        return ("+".to_string(), None);
    }
    let first = s.chars().next().unwrap();
    match first {
        '+' | '-' | '\u{00D7}' => {
            // × = multiplication sign
            let sign = if first == '\u{00D7}' { "×".to_string() } else { first.to_string() };
            let rest = &s[first.len_utf8()..];
            let val = rest.trim().parse::<f32>().ok();
            (sign, val)
        }
        _ => {
            // Try to parse as number
            if let Ok(v) = s.parse::<f32>() {
                let sign = if v >= 0.0 { "+" } else { "-" };
                (sign.to_string(), Some(v.abs()))
            } else {
                (s.to_string(), None)
            }
        }
    }
}

fn parse_protocol_line(line: &str, protos: &mut Vec<ParsedProtocol>) -> Result<(), String> {
    // {SOURCE}⊲{TARGET}[PARAMS]
    let proto_op = "\u{22B2}"; // ⊲
    let split_pos = line.find(proto_op).ok_or("Protocol: missing ⊲")?;

    let src_part = &line[..split_pos];
    let rest = &line[split_pos + proto_op.len()..];

    // Parse source node
    let (src_node, _) = extract_braced_node(src_part.trim())?;

    // Parse target node
    let (tgt_node, consumed) = extract_braced_node(rest.trim())?;
    let after_target = rest.trim()[consumed..].trim();

    // Parse [PARAMS] if present
    let mut gain = None;
    let mut polarity = None;
    let mut tau_class = None;
    let mut tau_value = None;
    let mut coupling = None;
    let mut gate_code = None;
    let mut gate_region = None;
    let mut gate_threshold = None;

    if after_target.starts_with('[') {
        let mut pc = Cur::new(after_target);
        if let Some(params_str) = pc.balanced('[', ']') {
            for param in split_top_level(params_str, ',') {
                let p = param.trim();
                if p.is_empty() {
                    continue;
                }
                if let Some(colon) = p.find(':') {
                    let k = &p[..colon].trim();
                    let v = &p[colon + 1..].trim();
                    match *k {
                        "gain" => gain = v.parse::<f32>().ok(),
                        "tau" => tau_value = Some(v.to_string()),
                        "gate" => gate_threshold = Some(v.to_string()),
                        "coupling" | "coup" => coupling = Some(v.to_string()),
                        _ => {}
                    }
                } else {
                    // Positional param
                    match p {
                        "inh" | "exc" => polarity = Some(p.to_string()),
                        "slow" | "fast" | "med" => tau_class = Some(p.to_string()),
                        "syn" | "vol" | "para" => coupling = Some(p.to_string()),
                        _ => {
                            if let Ok(v) = p.parse::<f32>() {
                                gain = Some(v);
                            }
                        }
                    }
                }
            }
        }
    }

    let edge_label = format!("{}@{}→{}@{}",
        src_node.code, src_node.region.as_deref().unwrap_or(""),
        tgt_node.code, tgt_node.region.as_deref().unwrap_or(""));

    protos.push(ParsedProtocol {
        source_code: src_node.code,
        source_region: src_node.region,
        edge_label,
        gain,
        polarity,
        tau_class,
        tau_value,
        gate_code,
        gate_region,
        gate_threshold,
        coupling,
    });
    Ok(())
}

fn parse_conditional_line(line: &str, conds: &mut Vec<ParsedConditional>) -> Result<(), String> {
    // ⊗(CONDITIONS)⟹{TARGET}:ACTION
    let implies = "\u{27F9}"; // ⟹
    let split = line.find(implies).ok_or("Conditional: missing ⟹")?;

    let cond_part = &line[..split];
    let effect_part = &line[split + implies.len()..];

    // Parse conditions from ⊗(...)
    let mut cur = Cur::new(cond_part.trim());
    cur.eat_str("\u{2297}"); // ⊗
    cur.skip_ws();
    let conds_inner = cur.balanced('(', ')').ok_or("Conditional: missing condition parens")?;

    // Detect logic: ∧ or ∨
    let logic = if conds_inner.contains('\u{2227}') {
        "\u{2227}".to_string() // ∧
    } else if conds_inner.contains('\u{2228}') {
        "\u{2228}".to_string() // ∨
    } else {
        "\u{2227}".to_string() // default AND
    };

    let logic_sep = if logic == "\u{2227}" { '\u{2227}' } else { '\u{2228}' };
    let mut items = Vec::new();
    for part in split_top_level(conds_inner, logic_sep) {
        let p = part.trim();
        if p.is_empty() {
            continue;
        }
        items.push(parse_cond_item(p)?);
    }

    // Parse effect: {TARGET}:ACTION[:VALUE]
    let effect = effect_part.trim();
    let (eff_node, consumed) = extract_braced_node(effect)?;
    let after = effect[consumed..].trim();
    let (action, value, switch) = if after.starts_with(':') {
        let rest = &after[1..];
        // Could be "block" or "switch:StateX" or "amplify:1.5"
        if let Some(colon2) = rest.find(':') {
            let act = rest[..colon2].trim().to_string();
            let val_str = rest[colon2 + 1..].trim();
            let val = val_str.parse::<f32>().ok();
            let sw = if val.is_none() { Some(val_str.to_string()) } else { None };
            (act, val, sw)
        } else {
            (rest.trim().to_string(), None, None)
        }
    } else {
        ("pass".to_string(), None, None)
    };

    conds.push(ParsedConditional {
        conditions: items,
        logic,
        effect_code: eff_node.code,
        effect_region: eff_node.region.unwrap_or_default(),
        effect_action: action,
        effect_value: value,
        effect_switch: switch,
    });
    Ok(())
}

fn parse_cond_item(s: &str) -> Result<CondItem, String> {
    let s = s.trim();
    let negated = s.starts_with('\u{00AC}') || s.starts_with('!'); // ¬ or !
    let s = if negated {
        s.trim_start_matches('\u{00AC}').trim_start_matches('!').trim()
    } else {
        s
    };

    // Format: {CODE@REGION}>=STATE or {CODE@REGION}<=STATE
    // Extract the node
    if !s.starts_with('{') {
        return Err(format!("CondItem: expected node, got: {}", s));
    }
    let (node, consumed) = extract_braced_node(s)?;
    let rest = s[consumed..].trim();

    // Parse comparison operator and state
    let threshold = if rest.starts_with(">=") {
        rest[2..].trim().to_string()
    } else if rest.starts_with("<=") {
        rest[2..].trim().to_string()
    } else if rest.starts_with('=') {
        rest[1..].trim().to_string()
    } else {
        rest.to_string()
    };

    Ok(CondItem {
        code: node.code,
        region: node.region.unwrap_or_default(),
        threshold,
        negated,
    })
}

fn parse_composite_line(line: &str, comps: &mut Vec<ParsedComposite>) -> Result<(), String> {
    // ◈NAME:{CODE@REGION}+{CODE@REGION}+...
    let mut cur = Cur::new(line);
    cur.eat_str("\u{25C8}"); // ◈
    cur.skip_ws();
    let name = cur.take_until(':').trim().to_string();
    cur.eat(':');

    let mut refs = Vec::new();
    let rest = cur.rest();
    // Split on '+' and extract nodes
    for part in rest.split('+') {
        let p = part.trim();
        if p.starts_with('{') {
            if let Ok((node, _)) = extract_braced_node(p) {
                refs.push((node.code, node.region.unwrap_or_default()));
            }
        }
    }

    comps.push(ParsedComposite { name, refs });
    Ok(())
}

fn parse_dysreg_line(line: &str, dysregs: &mut Vec<ParsedDysreg>) -> Result<(), String> {
    // ⚡TYPE:CHAIN_ELEMENTS
    let mut cur = Cur::new(line);
    cur.eat_str("\u{26A1}"); // ⚡
    let dtype = cur.take_until(':').trim().to_string();
    cur.eat(':');

    let chain_text = cur.rest().trim();
    let elements = parse_chain_line(chain_text, false)?;

    dysregs.push(ParsedDysreg { dtype, elements });
    Ok(())
}

fn parse_observable_line(line: &str, obs: &mut Vec<ParsedObservable>) -> Result<(), String> {
    // ⊕NAME→{TARGET}(DETAIL)
    let mut cur = Cur::new(line);
    cur.eat_str("\u{2295}"); // ⊕

    let arrow = "\u{2192}"; // →
    let rest = cur.rest();
    let arrow_pos = rest.find(arrow).ok_or("Observable: missing →")?;
    let name = rest[..arrow_pos].trim().to_string();
    let after_arrow = &rest[arrow_pos + arrow.len()..];

    // Parse {TARGET}
    let after_trimmed = after_arrow.trim();
    let (target_node, consumed) = extract_braced_node(after_trimmed)?;

    // Parse (DETAIL) if present
    let remainder = after_trimmed[consumed..].trim();
    let detail = if remainder.starts_with('(') {
        let mut dc = Cur::new(remainder);
        dc.balanced('(', ')').unwrap_or("").to_string()
    } else {
        String::new()
    };

    obs.push(ParsedObservable {
        name,
        target_code: target_node.code,
        target_region: target_node.region.unwrap_or_default(),
        detail,
    });
    Ok(())
}

// ── 7. parse_plasticity ─────────────────────────────────────────

pub fn parse_plasticity(text: &str) -> Result<Vec<ParsedDelta>, String> {
    let mut deltas = Vec::new();

    for (line_idx, raw_line) in text.lines().enumerate() {
        let line = raw_line.trim();
        if line.is_empty() {
            continue;
        }

        let res: Result<(), String> = (|| {
            // Δ0: ... Δ1: ... Δ2: ... Δ3: ...
            if line.starts_with("\u{0394}") && line.len() > "\u{0394}".len() {
                let after_delta = &line["\u{0394}".len()..];
                let first_char = after_delta.chars().next().unwrap_or(' ');
                if first_char.is_ascii_digit() {
                    return parse_delta_op_line(line, &mut deltas);
                }
            }

            // ⊟ cascade — parse and attach cascade_name to referenced deltas
            if line.starts_with("\u{229F}") {
                return parse_cascade_line(line, &mut deltas);
            }

            Ok(())
        })();

        if let Err(e) = res {
            log::warn!("PLASTICITY parse line {}: {}", line_idx + 1, e);
        }
    }

    Ok(deltas)
}

fn parse_delta_op_line(line: &str, deltas: &mut Vec<ParsedDelta>) -> Result<(), String> {
    // Δ0: {TRIGGER} ≫ {TARGET(prop:before→after)} [τ:duration] status:X depends:ΔN
    let mut cur = Cur::new(line);
    cur.eat_str("\u{0394}"); // Δ

    let rank_str = cur.take_until(':');
    let rank = rank_str.trim().parse::<u32>().unwrap_or(0);
    cur.eat(':');
    cur.skip_ws();

    // Parse trigger node
    let trigger_str = cur.balanced('{', '}').ok_or("Delta: missing trigger braces")?;
    let trigger = parse_node_inner(trigger_str)?;

    cur.skip_ws();
    // ≫ operator
    if !cur.eat_str("\u{226B}") {
        return Err("Delta: missing ≫ operator".into());
    }
    cur.skip_ws();

    // Parse target node (may have props like sensitivity:norm→des)
    let target_str = cur.balanced('{', '}').ok_or("Delta: missing target braces")?;
    let target = parse_node_inner(target_str)?;

    // Extract change from target props
    let (change_prop, change_before, change_after) = if let Some((k, v)) = target.props.first() {
        let prop = if k.is_empty() { "property".to_string() } else { k.clone() };
        // value format: "before→after" or "before->after"
        let arrow = "\u{2192}";
        if let Some(arr) = v.find(arrow) {
            (prop, v[..arr].to_string(), v[arr + arrow.len()..].to_string())
        } else if let Some(arr) = v.find("->") {
            (prop, v[..arr].to_string(), v[arr + 2..].to_string())
        } else {
            (prop, String::new(), v.clone())
        }
    } else {
        ("property".to_string(), "norm".to_string(), "changed".to_string())
    };

    let trigger_state = trigger.state.as_ref().map(|s| s.sym.clone()).unwrap_or_default();

    // Parse remaining: [τ:duration] status:X depends:ΔN
    let rest = cur.rest();
    let tau = extract_bracketed_value(rest, "\u{03C4}") // τ
        .or_else(|| extract_bracketed_value(rest, "tau"))
        .unwrap_or_default();

    let status = extract_kv(rest, "status:");
    let depends_str = extract_kv(rest, "depends:");
    let depends = depends_str.map(|d| {
        d.split(',').map(|s| s.trim().to_string()).collect::<Vec<_>>()
    }).unwrap_or_default();

    deltas.push(ParsedDelta {
        rank,
        trigger_code: trigger.code,
        trigger_region: trigger.region.unwrap_or_default(),
        trigger_state,
        target_code: target.code,
        target_region: target.region.unwrap_or_default(),
        change_prop,
        change_before,
        change_after,
        tau,
        depends,
        status,
        cascade_name: None,
        tensor_expr: None,
    });
    Ok(())
}

fn parse_cascade_line(line: &str, deltas: &mut Vec<ParsedDelta>) -> Result<(), String> {
    // ⊟ NAME: Δ0_CODE@REGION [τ:dur] → Δ1_CODE@REGION [τ:dur] → ... [total:dur,position:N]
    let mut cur = Cur::new(line);
    cur.eat_str("\u{229F}"); // ⊟
    cur.skip_ws();
    let name = cur.take_until(':').trim().to_string();
    cur.eat(':');

    // Tag all deltas that match with this cascade name
    // The cascade references deltas by their rank + code@region pattern
    // For simplicity, just set cascade_name on all current deltas
    for d in deltas.iter_mut() {
        if d.cascade_name.is_none() {
            d.cascade_name = Some(name.clone());
        }
    }
    Ok(())
}

/// Extract [KEY:VALUE] from a string
fn extract_bracketed_value(s: &str, key: &str) -> Option<String> {
    let mut pos = 0;
    let bytes = s.as_bytes();
    while pos < s.len() {
        if bytes[pos] == b'[' {
            let start = pos + 1;
            let mut end = start;
            let mut depth = 1;
            while end < s.len() && depth > 0 {
                if bytes[end] == b'[' { depth += 1; }
                if bytes[end] == b']' { depth -= 1; }
                end += 1;
            }
            let inner = &s[start..end - 1];
            // Check if inner starts with key: or key=
            let check1 = format!("{}:", key);
            let check2 = format!("{}=", key);
            if inner.starts_with(&check1) {
                return Some(inner[check1.len()..].trim().to_string());
            }
            if inner.starts_with(&check2) {
                return Some(inner[check2.len()..].trim().to_string());
            }
            pos = end;
        } else {
            pos += 1;
        }
    }
    None
}

/// Extract key:value from a string (space-delimited)
fn extract_kv(s: &str, prefix: &str) -> Option<String> {
    if let Some(idx) = s.find(prefix) {
        let rest = &s[idx + prefix.len()..];
        let val = rest.split_whitespace().next().unwrap_or("");
        if val.is_empty() {
            None
        } else {
            Some(val.to_string())
        }
    } else {
        None
    }
}

// ── 8. parse_meta ───────────────────────────────────────────────

pub fn parse_meta(text: &str) -> Result<Vec<ParsedMetaEntry>, String> {
    let mut entries = Vec::new();

    // Combining tilde
    let sigma_tilde = "\u{03C3}\u{0303}"; // σ̃
    let tri_tilde = "\u{22B2}\u{0303}"; // ⊲̃
    let int_tilde = "\u{222B}\u{0303}"; // ∫̃
    let tensor_tilde = "\u{2297}\u{0303}"; // ⊗̃

    for (line_idx, raw_line) in text.lines().enumerate() {
        let line = raw_line.trim();
        if line.is_empty() {
            continue;
        }

        let res: Result<(), String> = (|| {
            if line.starts_with(sigma_tilde) {
                return parse_meta_entry(line, sigma_tilde, "setpoint", &mut entries);
            }
            if line.starts_with(tri_tilde) {
                return parse_meta_entry(line, tri_tilde, "protocol", &mut entries);
            }
            if line.starts_with(int_tilde) {
                return parse_meta_entry(line, int_tilde, "structural", &mut entries);
            }
            if line.starts_with(tensor_tilde) {
                return parse_meta_entry(line, tensor_tilde, "architecture", &mut entries);
            }
            Ok(())
        })();

        if let Err(e) = res {
            log::warn!("META parse line {}: {}", line_idx + 1, e);
        }
    }

    Ok(entries)
}

fn parse_meta_entry(
    line: &str,
    prefix: &str,
    rank: &str,
    entries: &mut Vec<ParsedMetaEntry>,
) -> Result<(), String> {
    // PREFIX[WINDOW]({NODE}(property:before→after)) KEY:VAL KEY:VAL
    let mut cur = Cur::new(line);
    cur.eat_str(prefix);
    cur.skip_ws();

    // Parse [WINDOW]
    let window_str = cur.balanced('[', ']').unwrap_or("");
    let (window_kind, window_value) = parse_window(window_str);

    cur.skip_ws();

    // Parse ({NODE}(PROP)) or ({NODE}[PROP])
    let body = cur.balanced('(', ')').ok_or("Meta: missing body parens")?;

    // Inside body: {NODE}(property:before→after) or {NODE}[property:before→after]
    let (node, consumed) = extract_braced_node(body.trim())?;
    let after_node = body.trim()[consumed..].trim();

    // Extract property change
    let (property, program) = if after_node.starts_with('(') {
        let mut pc = Cur::new(after_node);
        let inner = pc.balanced('(', ')').unwrap_or("");
        parse_prop_change(inner)
    } else if after_node.starts_with('[') {
        let mut pc = Cur::new(after_node);
        let inner = pc.balanced('[', ']').unwrap_or("");
        parse_prop_change(inner)
    } else {
        (String::new(), String::new())
    };

    // Parse trailing key:value pairs
    let rest = cur.rest();
    let pull = extract_kv(rest, "pull:");
    let reversible = extract_kv(rest, "reversible:");
    let unlocks_with = extract_kv(rest, "unlocks_with:");

    entries.push(ParsedMetaEntry {
        rank: rank.to_string(),
        window_kind,
        window_value,
        target_code: node.code,
        target_region: node.region.unwrap_or_default(),
        target_property: property,
        target_program: program,
        reversible,
        unlocks_with,
        pull,
    });
    Ok(())
}

fn parse_window(s: &str) -> (String, String) {
    let s = s.trim();
    if s.is_empty() {
        return ("unknown".to_string(), String::new());
    }
    // Format: "after:condition:duration" or "age:range" or "cumulative:duration"
    // First token before ':' is the kind
    if let Some(colon) = s.find(':') {
        let kind = s[..colon].trim().to_string();
        let value = s[colon + 1..].trim().to_string();
        (kind, value)
    } else {
        ("condition".to_string(), s.to_string())
    }
}

fn parse_prop_change(s: &str) -> (String, String) {
    let s = s.trim();
    // Format: "property:before→after" or "remodel:description"
    if let Some(colon) = s.find(':') {
        let property = s[..colon].trim().to_string();
        let change = s[colon + 1..].trim().to_string();
        (property, change)
    } else {
        (s.to_string(), String::new())
    }
}

// ── 9. parse_convergence ────────────────────────────────────────

pub fn parse_convergence(text: &str) -> Result<Vec<ParsedConvEntry>, String> {
    let mut entries = Vec::new();

    let conv_state = "\u{222E}"; // ∮
    let conv_traj = "\u{22B3}"; // ⊳
    let conv_risk = "\u{22B3}\u{26A0}"; // ⊳⚠ (⊳ + warning sign)
    let conv_monitor = "\u{2295}\u{22B3}"; // ⊕⊳
    let conv_flag = "\u{26A1}"; // ⚡

    for (line_idx, raw_line) in text.lines().enumerate() {
        let line = raw_line.trim();
        if line.is_empty() {
            continue;
        }

        let res: Result<(), String> = (|| {
            // Order matters: check longer prefixes first
            if line.starts_with(conv_monitor) {
                return parse_conv_monitor(line, &mut entries);
            }
            if line.starts_with(conv_risk) {
                return parse_conv_risk(line, &mut entries);
            }
            if line.starts_with(conv_state) {
                return parse_conv_state(line, &mut entries);
            }
            if line.starts_with(conv_traj) {
                return parse_conv_trajectory(line, &mut entries);
            }
            if line.starts_with(conv_flag) {
                return parse_conv_flag(line, &mut entries);
            }
            Ok(())
        })();

        if let Err(e) = res {
            log::warn!("CONVERGENCE parse line {}: {}", line_idx + 1, e);
        }
    }

    Ok(entries)
}

fn parse_conv_state(line: &str, entries: &mut Vec<ParsedConvEntry>) -> Result<(), String> {
    // ∮(CODE@REGION)=v_past:STATE(DETAIL),v_current:STATE(DETAIL),v_meta:STATE(DETAIL)→DIAGNOSIS
    let mut cur = Cur::new(line);
    cur.eat_str("\u{222E}"); // ∮
    cur.skip_ws();

    // (CODE@REGION)
    let signal_str = cur.balanced('(', ')').ok_or("ConvState: missing signal parens")?;
    let (signal_code, signal_region) = split_code_region(signal_str)?;

    cur.skip_ws();
    if !cur.eat('=') {
        return Err("ConvState: missing '='".into());
    }

    // Parse vectors until → then diagnosis
    let rest = cur.rest();
    let arrow = "\u{2192}"; // →
    let arrow_pos = rest.find(arrow).ok_or("ConvState: missing → before diagnosis")?;
    let vectors_str = &rest[..arrow_pos];
    let diagnosis = rest[arrow_pos + arrow.len()..].trim().to_string();

    let mut vectors = Vec::new();
    for part in split_top_level(vectors_str, ',') {
        let p = part.trim();
        if p.is_empty() {
            continue;
        }
        vectors.push(parse_conv_vector(p));
    }

    entries.push(ParsedConvEntry::State {
        signal_code,
        signal_region,
        vectors,
        diagnosis,
    });
    Ok(())
}

fn parse_conv_vector(s: &str) -> (String, String, String) {
    // Format: v_NAME:STATE(DETAIL)
    let s = s.trim();
    // Split on first ':'
    if let Some(colon) = s.find(':') {
        let source = s[..colon].trim().to_string();
        let rest = &s[colon + 1..];
        // STATE is the first unicode symbol(s), DETAIL is in parens
        if let Some(paren) = rest.find('(') {
            let state = rest[..paren].trim().to_string();
            let detail = rest[paren + 1..].trim_end_matches(')').trim().to_string();
            (source, state, detail)
        } else {
            (source, rest.trim().to_string(), String::new())
        }
    } else {
        (s.to_string(), String::new(), String::new())
    }
}

fn parse_conv_trajectory(line: &str, entries: &mut Vec<ParsedConvEntry>) -> Result<(), String> {
    // ⊳(CODE@REGION,TIMEFRAME)=PREDICTED (RATIONALE) [confidence:LEVEL]
    let mut cur = Cur::new(line);
    cur.eat_str("\u{22B3}"); // ⊳
    cur.skip_ws();

    let paren_inner = cur.balanced('(', ')').ok_or("ConvTraj: missing parens")?;
    // Split on last ','
    let (signal_part, timeframe) = if let Some(comma) = paren_inner.rfind(',') {
        (&paren_inner[..comma], paren_inner[comma + 1..].trim().to_string())
    } else {
        (paren_inner, String::new())
    };
    let (signal_code, signal_region) = split_code_region(signal_part)?;

    cur.skip_ws();
    if !cur.eat('=') {
        return Err("ConvTraj: missing '='".into());
    }

    // PREDICTED is until ' (' or '[' or end
    let rest = cur.rest().trim();

    // Split on first ' (' (space + open paren) for rationale
    let (predicted, rationale, confidence) = {
        let mut predicted = String::new();
        let mut rationale = String::new();
        let mut confidence = None;

        // Find the rationale in parens and confidence in brackets
        let mut remaining = rest;

        // Extract predicted: everything before first ' (' or '['
        let pred_end = remaining.find(" (")
            .or_else(|| remaining.find(" ["))
            .unwrap_or(remaining.len());
        predicted = remaining[..pred_end].trim().to_string();
        remaining = &remaining[pred_end..];

        // Extract rationale in parens
        if remaining.trim_start().starts_with('(') {
            let trimmed = remaining.trim_start();
            let mut rc = Cur::new(trimmed);
            if let Some(rat) = rc.balanced('(', ')') {
                rationale = rat.to_string();
                remaining = &trimmed[rc.pos..];
            }
        }

        // Extract [confidence:LEVEL]
        confidence = extract_bracketed_value(remaining, "confidence");

        (predicted, rationale, confidence)
    };

    entries.push(ParsedConvEntry::Trajectory {
        signal_code,
        signal_region,
        timeframe,
        predicted,
        rationale,
        confidence,
    });
    Ok(())
}

fn parse_conv_risk(line: &str, entries: &mut Vec<ParsedConvEntry>) -> Result<(), String> {
    // ⊳⚠(NAME,TARGET,distance:X,window:Y,reversible_before:X,reversible_after:Y)
    let mut cur = Cur::new(line);
    cur.eat_str("\u{22B3}\u{26A0}"); // ⊳⚠
    // Also handle ⊳⚠ with variation (warning sign U+26A0 vs U+FE0F)
    if !cur.rest().starts_with('(') {
        // Try eating the variation selector
        let _ = cur.eat_str("\u{FE0F}");
    }
    cur.skip_ws();

    let inner = cur.balanced('(', ')').ok_or("ConvRisk: missing parens")?;

    // Parse comma-separated key:value pairs (first two are positional: name, target)
    let parts: Vec<&str> = split_top_level(inner, ',').into_iter().map(|s| s.trim()).collect();

    let risk_name = parts.first().unwrap_or(&"").to_string();
    let risk_target = parts.get(1).map(|s| s.to_string());

    let mut risk_distance = None;
    let mut risk_window = None;
    let mut risk_reversible_before = None;
    let mut risk_reversible_after = None;

    for &part in parts.iter().skip(2) {
        if let Some(colon) = part.find(':') {
            let k = part[..colon].trim();
            let v = part[colon + 1..].trim().to_string();
            match k {
                "distance" => risk_distance = Some(v),
                "window" => risk_window = Some(v),
                "reversible_before" => risk_reversible_before = Some(v),
                "reversible_after" => risk_reversible_after = Some(v),
                _ => {}
            }
        }
    }

    entries.push(ParsedConvEntry::Risk {
        risk_name,
        risk_target,
        risk_distance,
        risk_window,
        risk_reversible_before,
        risk_reversible_after,
    });
    Ok(())
}

fn parse_conv_monitor(line: &str, entries: &mut Vec<ParsedConvEntry>) -> Result<(), String> {
    // ⊕⊳(measurement,flag_ref,note)
    let mut cur = Cur::new(line);
    cur.eat_str("\u{2295}\u{22B3}"); // ⊕⊳
    cur.skip_ws();

    let inner = cur.balanced('(', ')').ok_or("ConvMonitor: missing parens")?;
    let parts: Vec<&str> = split_top_level(inner, ',').into_iter().map(|s| s.trim()).collect();

    let measurement = parts.first().unwrap_or(&"").to_string();
    let flag_ref = parts.get(1).map(|s| s.to_string());
    let note = parts.get(2).map(|s| s.to_string());

    entries.push(ParsedConvEntry::Monitor {
        measurement,
        flag_ref,
        note,
    });
    Ok(())
}

fn parse_conv_flag(line: &str, entries: &mut Vec<ParsedConvEntry>) -> Result<(), String> {
    // ⚡TYPE:EXPR
    let mut cur = Cur::new(line);
    cur.eat_str("\u{26A1}"); // ⚡
    let flag_type = cur.take_until(':').trim().to_string();
    cur.eat(':');
    let expr = cur.rest().trim().to_string();

    entries.push(ParsedConvEntry::Flag { flag_type, expr });
    Ok(())
}

// ── 10. Linter ──────────────────────────────────────────────────

/// Lint BASE pipeline BNF text.
/// Runs parse_base then performs post-parse validation checks.
pub fn lint_base(text: &str) -> LintResult {
    let mut issues = Vec::new();
    let mut node_count = 0usize;
    let mut edge_count = 0usize;

    let parsed = match parse_base(text) {
        Ok(p) => p,
        Err(e) => {
            issues.push(LintIssue {
                level: LintLevel::Error,
                message: format!("Parse failed: {}", e),
                line: None,
            });
            return LintResult { issues, node_count: 0, edge_count: 0, chain_count: 0, valid: false };
        }
    };

    let chain_count = parsed.chains.len();

    // Collect all declared nodes from chains
    let mut declared_nodes: std::collections::HashSet<String> = std::collections::HashSet::new();
    let mut typed_nodes: std::collections::HashSet<String> = std::collections::HashSet::new();

    for chain in &parsed.chains {
        count_chain_elements(chain, &mut declared_nodes, &mut typed_nodes, &mut node_count, &mut edge_count);
    }

    // Check 1: domains declared
    if parsed.domains.is_empty() {
        issues.push(LintIssue {
            level: LintLevel::Warn,
            message: "@domain declaration missing".into(),
            line: None,
        });
    }

    // Check 2: seeds reference existing nodes
    for seed in &parsed.seeds {
        let key = format!("{}@{}", seed.code, seed.region);
        if !declared_nodes.contains(&key) {
            issues.push(LintIssue {
                level: LintLevel::Warn,
                message: format!("Seed references undeclared node: {}", key),
                line: None,
            });
        }
    }

    // Check 3: integration inputs reference existing nodes
    for integ in &parsed.integrations {
        for inp in &integ.inputs {
            let key = format!("{}@{}", inp.code, inp.region);
            if !declared_nodes.contains(&key) {
                issues.push(LintIssue {
                    level: LintLevel::Warn,
                    message: format!("Integration input references undeclared node: {}", key),
                    line: None,
                });
            }
        }
    }

    // Check 4: chains should end with terminal fates
    for (i, chain) in parsed.chains.iter().enumerate() {
        if !chain_has_terminal(chain) {
            issues.push(LintIssue {
                level: LintLevel::Warn,
                message: format!("Chain {} has no terminal fate", i),
                line: None,
            });
        }
    }

    // Check 5: bare ligands (nodes without type prefix on first mention)
    for key in &declared_nodes {
        if !typed_nodes.contains(key) {
            issues.push(LintIssue {
                level: LintLevel::Warn,
                message: format!("Node {} has no type prefix (bare ligand)", key),
                line: None,
            });
        }
    }

    // Check 6: protocol sources reference existing nodes
    for proto in &parsed.protocols {
        let key = format!("{}@{}", proto.source_code, proto.source_region.as_deref().unwrap_or(""));
        if !declared_nodes.contains(&key) {
            issues.push(LintIssue {
                level: LintLevel::Warn,
                message: format!("Protocol source references undeclared node: {}", key),
                line: None,
            });
        }
    }

    // Check 7: conditional effect targets
    for cond in &parsed.conditionals {
        let key = format!("{}@{}", cond.effect_code, cond.effect_region);
        if !declared_nodes.contains(&key) {
            issues.push(LintIssue {
                level: LintLevel::Warn,
                message: format!("Conditional effect target references undeclared node: {}", key),
                line: None,
            });
        }
    }

    let has_errors = issues.iter().any(|i| i.level == LintLevel::Error);
    LintResult {
        issues,
        node_count,
        edge_count,
        chain_count,
        valid: !has_errors,
    }
}

fn count_chain_elements(
    elements: &[ChainElement],
    declared: &mut std::collections::HashSet<String>,
    typed: &mut std::collections::HashSet<String>,
    nodes: &mut usize,
    edges: &mut usize,
) {
    for el in elements {
        match el {
            ChainElement::Node(n) => {
                let key = format!("{}@{}", n.code, n.region.as_deref().unwrap_or(""));
                declared.insert(key.clone());
                if !n.kind.is_empty() {
                    typed.insert(key);
                }
                *nodes += 1;
            }
            ChainElement::Edge(_) => {
                *edges += 1;
            }
            ChainElement::Branch(branches) => {
                for branch in branches {
                    count_chain_elements(branch, declared, typed, nodes, edges);
                }
            }
            ChainElement::Terminal(_) | ChainElement::Gate(_, _) => {}
        }
    }
}

fn chain_has_terminal(elements: &[ChainElement]) -> bool {
    for el in elements.iter().rev() {
        match el {
            ChainElement::Terminal(_) => return true,
            ChainElement::Branch(branches) => {
                if branches.iter().all(|b| chain_has_terminal(b)) {
                    return true;
                }
            }
            _ => {}
        }
    }
    false
}

// ── 11. Utility functions used by other modules ─────────────────

/// Parse a tau duration string like "72h", "2wk", "1mo", "6wk" into hours.
pub fn parse_tau_to_hours(tau: &str) -> f32 {
    let tau = tau.trim();
    if tau.is_empty() {
        return 0.0;
    }
    // Try to split into numeric part and unit
    let mut num_end = 0;
    for (i, c) in tau.char_indices() {
        if c.is_ascii_digit() || c == '.' {
            num_end = i + c.len_utf8();
        } else {
            break;
        }
    }
    let num: f32 = if num_end > 0 {
        tau[..num_end].parse().unwrap_or(0.0)
    } else {
        0.0
    };
    let unit = tau[num_end..].trim().to_lowercase();
    match unit.as_str() {
        "h" | "hr" | "hrs" | "hour" | "hours" => num,
        "d" | "day" | "days" => num * 24.0,
        "wk" | "wks" | "week" | "weeks" => num * 24.0 * 7.0,
        "mo" | "month" | "months" => num * 24.0 * 30.0,
        "yr" | "year" | "years" => num * 24.0 * 365.0,
        "min" | "mins" => num / 60.0,
        "s" | "sec" | "secs" => num / 3600.0,
        _ => num, // default to hours
    }
}

// ── 12. Tests ───────────────────────────────────────────────────

#[cfg(all(test, not(target_arch = "wasm32")))]
mod tests {
    use super::*;

    const SAMPLE_BASE: &str = r#"@domain: L.nt L.h L.p R Gp 2m K TF G E V B.beh
#chronic_stress #anhedonia #male #30yo
::fates ↺⁺,↺⁻,↺⁰,→⊘,→≋
::open_ends 0
Δ(CRH@PVN)=+
Δ(CORT@ADR)=+
⊙{L.h:CRH[+]@PVN}→{R:CRH-R1(Gs)@PIT}→{Gp:Gsα@PIT}→{2m:cAMP[+]@PIT}→{K:PKA@PIT}→{TF:CREB@PIT}→{G:POMC@PIT}→{L.h:ACTH[+]@PIT}↺⁻
{ACTH@PIT}→{R:MC2R(Gs)@ADR}→{Gp:Gsα@ADR}→{2m:cAMP[+]@ADR}→{K:PKA@ADR}→{L.h:CORT[+]@ADR}→≋
{CORT@ADR}→{NR:GR@HPC}→{TF:GR@HPC}→{G:BDNF@HPC}⊣{L.p:BDNF[-]@HPC}→≋
{CORT@ADR}→{NR:GR@PVN}⊣{CRH@PVN}↺⁻
{BDNF@HPC}→{R:TrkB(RTK)@HPC}→{K:ERK@HPC}→{TF:CREB@HPC}→{G:BDNF@HPC}↺⁺
∫{5HT_tone@DRN}←(5HT@DRN:+,CORT@ADR:-,BDNF@HPC:+)→{L.nt:5HT[-]@DRN}:tonic
∫{DA_tone@VTA}←(DA@VTA:+,CORT@ADR:-,BDNF@HPC:+)→{L.nt:DA[-]@VTA}:tonic
{5HT@DRN}⊲{R:5HT1A@DRN}[inh,slow,syn]
⊗({CORT@ADR}>=+ ∧ {BDNF@HPC}>=-)⟹{DA@VTA}:block
⚡sus:{CORT@ADR}→{GR@PVN}⊣{CRH@PVN}↺⁻
⊕serum_cortisol→{CORT@ADR}(direct)
⊕plasma_BDNF→{BDNF@HPC}(proxy)
⊕CSF_5HIAA→{5HT@DRN}(metabolite)"#;

    #[test]
    fn test_parse_base_domains() {
        let result = parse_base(SAMPLE_BASE).unwrap();
        assert_eq!(result.domains.len(), 12);
        assert!(result.domains.contains(&"L.nt".to_string()));
        assert!(result.domains.contains(&"B.beh".to_string()));
    }

    #[test]
    fn test_parse_base_phase() {
        let result = parse_base(SAMPLE_BASE).unwrap();
        assert!(result.phase.is_some());
        let phase = result.phase.unwrap();
        assert!(phase.contains("chronic_stress"));
    }

    #[test]
    fn test_parse_base_seeds() {
        let result = parse_base(SAMPLE_BASE).unwrap();
        assert_eq!(result.seeds.len(), 2);
        assert_eq!(result.seeds[0].code, "CRH");
        assert_eq!(result.seeds[0].region, "PVN");
        assert_eq!(result.seeds[0].sign, "+");
    }

    #[test]
    fn test_parse_base_chains() {
        let result = parse_base(SAMPLE_BASE).unwrap();
        // 5 chain lines + 1 root chain = 6 chains total
        assert!(result.chains.len() >= 5, "Expected at least 5 chains, got {}", result.chains.len());

        // First chain should start with a root node
        let first_chain = &result.chains[0];
        if let ChainElement::Node(n) = &first_chain[0] {
            assert!(n.is_root, "First node should be root");
            assert_eq!(n.code, "CRH");
            assert_eq!(n.kind, "L.h");
            assert_eq!(n.region, Some("PVN".to_string()));
        } else {
            panic!("First element should be a node");
        }
    }

    #[test]
    fn test_parse_base_node_with_props() {
        let result = parse_base(SAMPLE_BASE).unwrap();
        // Find the CRH-R1 node which has (Gs) prop
        let found = result.chains.iter().any(|chain| {
            chain.iter().any(|el| {
                if let ChainElement::Node(n) = el {
                    n.code == "CRH-R1" && !n.props.is_empty()
                } else {
                    false
                }
            })
        });
        assert!(found, "Should find CRH-R1 node with props");
    }

    #[test]
    fn test_parse_base_integrations() {
        let result = parse_base(SAMPLE_BASE).unwrap();
        assert_eq!(result.integrations.len(), 2);
        assert_eq!(result.integrations[0].unit.code, "5HT_tone");
        assert_eq!(result.integrations[0].inputs.len(), 3);
        assert_eq!(result.integrations[0].mode, "tonic");
    }

    #[test]
    fn test_parse_base_protocols() {
        let result = parse_base(SAMPLE_BASE).unwrap();
        assert_eq!(result.protocols.len(), 1);
        assert_eq!(result.protocols[0].source_code, "5HT");
        assert_eq!(result.protocols[0].polarity, Some("inh".to_string()));
        assert_eq!(result.protocols[0].tau_class, Some("slow".to_string()));
        assert_eq!(result.protocols[0].coupling, Some("syn".to_string()));
    }

    #[test]
    fn test_parse_base_conditionals() {
        let result = parse_base(SAMPLE_BASE).unwrap();
        assert_eq!(result.conditionals.len(), 1);
        assert_eq!(result.conditionals[0].conditions.len(), 2);
        assert_eq!(result.conditionals[0].effect_code, "DA");
        assert_eq!(result.conditionals[0].effect_action, "block");
    }

    #[test]
    fn test_parse_base_dysregs() {
        let result = parse_base(SAMPLE_BASE).unwrap();
        assert_eq!(result.dysregs.len(), 1);
        assert_eq!(result.dysregs[0].dtype, "sus");
    }

    #[test]
    fn test_parse_base_observables() {
        let result = parse_base(SAMPLE_BASE).unwrap();
        assert_eq!(result.observables.len(), 3);
        assert_eq!(result.observables[0].name, "serum_cortisol");
        assert_eq!(result.observables[0].target_code, "CORT");
    }

    const SAMPLE_PLASTICITY: &str = r#"Δ0: {L.h:CORT[+]@ADR} ≫ {R:GR@HPC(sensitivity:norm→des)} [τ:72h] status:active
Δ0: {L.nt:5HT[-]@DRN} ≫ {R:5HT1A@DRN(sensitivity:norm→up)} [τ:48h] status:active
Δ1: {R:GR[-]@HPC} ≫ {L.p:BDNF@HPC(expression:norm→low)} [τ:2wk] status:pending depends:Δ0
Δ2: {BDNF[-]@HPC} ≫ {R:TrkB@HPC(gain:norm→reduced)} [τ:1mo] status:pending depends:Δ1
⊟ hpa_cascade: Δ0_GR@HPC [τ:72h] → Δ1_BDNF@HPC [τ:2wk] → Δ2_TrkB@HPC [τ:1mo] [total:6wk,position:0]"#;

    #[test]
    fn test_parse_plasticity() {
        let result = parse_plasticity(SAMPLE_PLASTICITY).unwrap();
        assert_eq!(result.len(), 4);

        assert_eq!(result[0].rank, 0);
        assert_eq!(result[0].trigger_code, "CORT");
        assert_eq!(result[0].trigger_region, "ADR");
        assert_eq!(result[0].target_code, "GR");
        assert_eq!(result[0].target_region, "HPC");
        assert_eq!(result[0].change_prop, "sensitivity");
        assert_eq!(result[0].change_before, "norm");
        assert_eq!(result[0].change_after, "des");
        assert_eq!(result[0].tau, "72h");
        assert_eq!(result[0].status, Some("active".to_string()));
    }

    const SAMPLE_META: &str = "σ̃[after:chronic_stress:18mo]({L.h:CORT@ADR}(baseline:=→+)) pull:strong\nσ̃[after:chronic_stress:18mo]({L.nt:5HT@DRN}(baseline:=→-)) pull:moderate\n⊲̃[after:chronic_stress:18mo]({⊲:GR@HPC}[sensitivity:norm→desensitized]) reversible:partial unlocks_with:stress_removal\n∫̃[after:chronic_stress:18mo]({5HT_tone@DRN}(remodel:dendritic_retraction))";

    #[test]
    fn test_parse_meta() {
        let result = parse_meta(SAMPLE_META).unwrap();
        assert_eq!(result.len(), 4);

        assert_eq!(result[0].rank, "setpoint");
        assert_eq!(result[0].target_code, "CORT");
        assert_eq!(result[0].target_region, "ADR");
        assert_eq!(result[0].target_property, "baseline");
        assert_eq!(result[0].pull, Some("strong".to_string()));
    }

    const SAMPLE_CONVERGENCE: &str = "∮(CORT@ADR)=v_past:+(Δ0_drift),v_current:+(∫_elevated),v_meta:+(σ̃_elevated)→converging_high\n∮(BDNF@HPC)=v_past:-(Δ0_suppression),v_current:-(∫_reduced),v_meta:-(σ̃_low)→converging_low\n⊳(CORT@ADR,+6mo)=stable_high (chronic_HPA_activation_maintains_elevated_cortisol) [confidence:moderate]\n⊳(BDNF@HPC,+3mo)=declining (sustained_cortisol_suppresses_BDNF_expression) [confidence:high]\n⊳⚠(HPA_exhaustion,HPA,distance:moderate,window:12mo,reversible_before:yes,reversible_after:partial)\n⊕⊳(serum_cortisol,CORT@ADR,track_diurnal_curve)";

    #[test]
    fn test_parse_convergence() {
        let result = parse_convergence(SAMPLE_CONVERGENCE).unwrap();
        assert_eq!(result.len(), 6);

        // Check state entry
        if let ParsedConvEntry::State { signal_code, signal_region, vectors, diagnosis } = &result[0] {
            assert_eq!(signal_code, "CORT");
            assert_eq!(signal_region, "ADR");
            assert_eq!(vectors.len(), 3);
            assert_eq!(diagnosis, "converging_high");
        } else {
            panic!("Expected State entry");
        }

        // Check trajectory
        if let ParsedConvEntry::Trajectory { signal_code, predicted, confidence, .. } = &result[2] {
            assert_eq!(signal_code, "CORT");
            assert_eq!(predicted, "stable_high");
            assert_eq!(confidence.as_deref(), Some("moderate"));
        } else {
            panic!("Expected Trajectory entry");
        }

        // Check risk
        if let ParsedConvEntry::Risk { risk_name, risk_distance, .. } = &result[4] {
            assert_eq!(risk_name, "HPA_exhaustion");
            assert_eq!(risk_distance.as_deref(), Some("moderate"));
        } else {
            panic!("Expected Risk entry");
        }

        // Check monitor
        if let ParsedConvEntry::Monitor { measurement, flag_ref, note } = &result[5] {
            assert_eq!(measurement, "serum_cortisol");
            assert_eq!(flag_ref.as_deref(), Some("CORT@ADR"));
            assert_eq!(note.as_deref(), Some("track_diurnal_curve"));
        } else {
            panic!("Expected Monitor entry");
        }
    }

    #[test]
    fn test_lint_base() {
        let result = lint_base(SAMPLE_BASE);
        assert!(result.valid, "Should be valid, issues: {:?}", result.issues);
        assert!(result.node_count > 0);
        assert!(result.edge_count > 0);
        assert!(result.chain_count > 0);
    }

    #[test]
    fn test_parse_node_with_dash_code() {
        let node = parse_node_inner("R:CRH-R1(Gs)@PIT").unwrap();
        assert_eq!(node.code, "CRH-R1");
        assert_eq!(node.kind, "R");
        assert_eq!(node.region, Some("PIT".to_string()));
        assert!(!node.props.is_empty());
    }

    #[test]
    fn test_parse_node_shorthand() {
        let node = parse_node_inner("ACTH@PIT").unwrap();
        assert_eq!(node.code, "ACTH");
        assert_eq!(node.kind, "");
        assert_eq!(node.region, Some("PIT".to_string()));
    }

    #[test]
    fn test_parse_node_with_state() {
        let node = parse_node_inner("L.h:CRH[+]@PVN").unwrap();
        assert_eq!(node.code, "CRH");
        assert_eq!(node.kind, "L.h");
        assert!(node.state.is_some());
        assert_eq!(node.state.as_ref().unwrap().sym, "+");
    }

    #[test]
    fn test_empty_input() {
        let result = parse_base("").unwrap();
        assert!(result.chains.is_empty());
    }

    #[test]
    fn test_cascade_tagged_chains() {
        let input = r#"@domain: L.nt L.h L.p R Gp 2m K TF G E V B.beh
#treatment_resistant_depression #SSRI
::fates ↺⁺,↺⁻,↺⁰,→⊘,→≋
::open_ends 0
Δ(CORT@ADR)=++
Δ(5HT@DRN)=+
::pathway HPA Axis
GPCR.Gs:{L.h:CRH[++]@PVN}→{R:CRH-R(Gs)@PVN}→{Gp:Gs@PVN}→{2m:cAMP@PVN}→{K:PKA@PVN}→{TF:CREB@PVN}→{G:CRH@PVN}↺⁺
GPCR.Gi:{L.h:CORT[++]@PVN}→{R:GR@PVN}→{TF:GR@PVN}⊣{G:CRH@PVN}↺⁻
IONOTROPIC:{L.h:CORT[++]@AMY}→{R:NMDA@AMY}→{2m:Ca²⁺@AMY}→{Mt:excitotoxicity@AMY}→⊘
⊕serum_cortisol→{L.h:CORT@ADR}(direct)
::pathway Serotonin
GPCR.Gi:{L.nt:5HT[=]@DRN}→{R:5HT1A(Gi)@DRN}→{Gp:Gi@DRN}⊣{2m:cAMP@DRN}↺⁻
GPCR.Gq:{L.nt:5HT[=]@PFC}→{R:5HT2A(Gq)@PFC}→{Gp:Gq@PFC}→{2m:IP3@PFC}→{K:PKC@PFC}→⊘
RTK:{L.p:BDNF[=]@HPC}→{R:TrkB(RTK)@HPC}→{K:ERK@HPC}→{TF:CREB@HPC}→{G:BDNF@HPC}↺⁺
∫{G:pituitary}←(CRH@PVN:+,ACTH@ADR:+)→{L.h:CORT@ADR}:thr
⊕CSF_5HIAA→{L.nt:5HT@DRN}(metabolite)
⚡hormonal_collapse_cascade:{L.h:CORT@PVN}↺⁺
"#;

        let result = parse_base(input).unwrap();

        // Should have 7 cascade-tagged chains (GPCR.Gs, GPCR.Gi, IONOTROPIC, GPCR.Gi, GPCR.Gq, RTK, + dysreg chain)
        assert!(result.chains.len() >= 6, "Expected at least 6 chains, got {}", result.chains.len());

        // Verify first chain (GPCR.Gs) has nodes
        let first_chain = &result.chains[0];
        let node_count = first_chain.iter().filter(|e| matches!(e, ChainElement::Node(_))).count();
        assert!(node_count >= 5, "First cascade chain should have at least 5 nodes, got {}", node_count);

        // Check that CRH node was parsed from cascade-tagged line
        let has_crh = result.chains.iter().any(|chain| {
            chain.iter().any(|el| {
                if let ChainElement::Node(n) = el {
                    n.code == "CRH" && n.kind == "L.h" && n.region == Some("PVN".to_string())
                } else {
                    false
                }
            })
        });
        assert!(has_crh, "Should find CRH node from cascade-tagged chain");

        // Seeds should be parsed
        assert_eq!(result.seeds.len(), 2);

        // Integration should be parsed
        assert_eq!(result.integrations.len(), 1);

        // Observables should be parsed
        assert_eq!(result.observables.len(), 2);

        // Dysreg should be parsed
        assert_eq!(result.dysregs.len(), 1);
    }

    #[test]
    fn test_cascade_tag_with_space() {
        // Test that "GPCR.Gs: {..." (with space after colon) is also handled
        let input = r#"@domain: L.nt L.h R Gp 2m K TF G
GPCR.Gs: {L.h:CRH[+]@PVN}→{R:CRH-R(Gs)@PIT}→{Gp:Gs@PIT}→⊘
"#;
        let result = parse_base(input).unwrap();
        assert_eq!(result.chains.len(), 1, "Should parse cascade-tagged chain with space after colon");
        let node_count = result.chains[0].iter().filter(|e| matches!(e, ChainElement::Node(_))).count();
        assert!(node_count >= 3, "Should have at least 3 nodes");
    }

    #[test]
    fn test_all_cascade_tags() {
        // Verify all 13 cascade tags are recognized
        for tag in CASCADE_TAGS {
            let input = format!("{}:{{L.nt:DA[+]@VTA}}→{{R:D1@NAc}}→⊘", tag);
            let result = parse_base(&input).unwrap();
            assert_eq!(result.chains.len(), 1, "Tag '{}' should produce 1 chain", tag);
        }
    }

    #[test]
    fn test_malformed_input_no_panic() {
        // Should not panic on malformed input
        let _ = parse_base("{unclosed");
        let _ = parse_base("random garbage text 12345");
        let _ = parse_base("{}→{}→{}"); // empty nodes
        let _ = parse_plasticity("Δ garbage");
        let _ = parse_meta("σ̃ garbage");
        let _ = parse_convergence("∮ garbage");
    }
}
