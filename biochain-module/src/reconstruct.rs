use spacetimedb::{reducer, ReducerContext};
use crate::base::tables::*;
use crate::plasticity::tables::*;
use crate::meta::tables::*;

#[reducer]
pub fn reconstruct(ctx: &ReducerContext, program_id: u64) -> Result<(), String> {
    let p = ctx.db.program().id().find(program_id)
        .ok_or("Program not found")?;

    let mut out = String::new();

    // Header
    out.push_str(&format!("@domain:{}\n", p.domains.join(",")));
    if let Some(ref phase) = p.phase {
        out.push_str(&format!("#{}\n", phase));
    }

    // Δ seeds (root nodes)
    for n in ctx.db.node().by_program_rank().filter((program_id, "R0"))
        .filter(|n| n.is_root)
    {
        if let Some(ref st) = n.state {
            if let (Some(sign), Some(val)) = (&st.delta_sign, st.delta_val) {
                out.push_str(&format!(
                    "\u{0394}({}:{}@{})={}{}\n",
                    n.kind, n.code,
                    n.region.as_deref().unwrap_or(""),
                    sign, val
                ));
            }
        }
    }

    // @R0
    out.push_str("\n@R0\n\n");
    for e in ctx.db.edge().by_program().filter(program_id)
        .filter(|e| e.rank_tag == "R0")
    {
        let src = ctx.db.node().id().find(e.source_id);
        let tgt = ctx.db.node().id().find(e.target_id);
        if let (Some(s), Some(t)) = (src, tgt) {
            out.push_str(&format!(
                "{{{}:{}@{}}}{}{{{}:{}@{}}}\n",
                s.kind, s.code, s.region.as_deref().unwrap_or(""),
                e.edge_type.as_deref().unwrap_or("\u{2192}"),
                t.kind, t.code, t.region.as_deref().unwrap_or("")
            ));
        }
    }

    // @R1
    let r1: Vec<_> = ctx.db.node().by_program_rank()
        .filter((program_id, "R1"))
        .filter(|n| n.integ.is_some())
        .collect();
    if !r1.is_empty() {
        out.push_str("\n@R1\n\n");
        for u in &r1 {
            let integ = u.integ.as_ref().unwrap();
            let inputs: Vec<String> = integ.inputs.iter().map(|i|
                format!("    {}@{}:{}{}",
                    i.code, i.region,
                    if i.w_type == "mod" { "\u{00d7}" } else { "" },
                    i.weight)
            ).collect();
            out.push_str(&format!(
                "\u{222b}{{{}:{}@{}}}\u{2190}(\n{}\n)\u{2192}{}@{}:{}{}\n\n",
                u.kind, u.code, u.region.as_deref().unwrap_or(""),
                inputs.join(",\n"),
                integ.output.code, integ.output.region,
                integ.output.mode,
                integ.output.threshold.as_ref()
                    .map(|t| format!(":{}", t)).unwrap_or_default()
            ));
        }
    }

    // @R2
    let r2: Vec<_> = ctx.db.edge().by_program().filter(program_id)
        .filter(|e| e.rank_tag == "R2" && e.protocol.is_some())
        .collect();
    if !r2.is_empty() {
        out.push_str("\n@R2\n\n");
        for e in &r2 {
            if let (Some(src), Some(proto)) =
                (ctx.db.node().id().find(e.source_id), &e.protocol)
            {
                let mut specs = Vec::new();
                if let Some(g) = proto.gain { specs.push(format!("gain:\u{00d7}{}", g)); }
                if let Some(ref p) = proto.polarity { specs.push(format!("pol:{}", p)); }
                if let Some(ref tc) = proto.tau_class {
                    specs.push(format!("tau:{}:{}",
                        tc, proto.tau_value.as_deref().unwrap_or("")));
                }
                if let Some(ref g) = proto.gate { specs.push(format!("gate:{}", g)); }
                if let Some(ref c) = proto.coupling { specs.push(format!("coup:{}", c)); }
                out.push_str(&format!(
                    "{{{}:{}@{}}}\u{22b2}{{{}}}\u{005b}{}\u{005d}\n",
                    src.kind, src.code, src.region.as_deref().unwrap_or(""),
                    e.proto_label.as_deref().unwrap_or(""),
                    specs.join(", ")
                ));
            }
        }
    }

    // @R3
    let tensors: Vec<_> = ctx.db.tensor().by_program().filter(program_id).collect();
    if !tensors.is_empty() {
        out.push_str("\n@R3\n\n");
        for t in &tensors {
            let conds: Vec<String> = t.conditions.iter().map(|c|
                format!("{}{{{}@{}}}>={}",
                    if c.negated { "\u{00ac}" } else { "" },
                    c.code, c.region, c.state)
            ).collect();
            let joiner = if t.logic == "AND" { " \u{2227} " } else { " \u{2228} " };
            out.push_str(&format!(
                "\u{2297}( {} )\u{27f9}{{{}@{}}}:{}{}\n",
                conds.join(joiner),
                t.effect.code, t.effect.region,
                t.effect.action,
                t.effect.value.map(|v| format!(":{}", v)).unwrap_or_default()
            ));
        }
    }

    // @Δ
    let deltas: Vec<_> = ctx.db.delta_op().by_program().filter(program_id).collect();
    if !deltas.is_empty() {
        out.push_str("\n@\u{0394}\n\n");
        for d in &deltas {
            out.push_str(&format!(
                "\u{0394}@{}: {{{}@{}[{}]}} \u{226b} {{{}@{}({}:{}\u{2192}{})}} [\u{03c4}:{}]\n",
                d.rank_tag,
                d.trigger_code, d.trigger_region, d.trigger_state,
                d.target_code, d.target_region,
                d.change.property, d.change.before, d.change.after,
                d.tau
            ));
        }
    }

    // @M3->M0
    let metas: Vec<_> = ctx.db.meta_op().by_program().filter(program_id).collect();
    if !metas.is_empty() {
        for rank in &["M3", "M2", "M1", "M0"] {
            let rank_metas: Vec<_> = metas.iter()
                .filter(|m| m.rank_tag == *rank).collect();
            if rank_metas.is_empty() { continue; }
            let op = match *rank {
                "M3" => "\u{2297}\u{0303}",
                "M2" => "\u{22b2}\u{0303}",
                "M1" => "\u{222b}\u{0303}",
                "M0" => "\u{03c3}\u{0303}",
                _ => "?"
            };
            out.push_str(&format!("\n@{}\n\n", rank));
            for m in &rank_metas {
                out.push_str(&format!(
                    "{}[{}:{}]( {{{}@{}}}({}:{}) )\n",
                    op,
                    m.window.kind, m.window.value,
                    m.target.code, m.target.region,
                    m.target.property, m.target.program
                ));
            }
        }
    }

    log::info!("{}", out);
    Ok(())
}
