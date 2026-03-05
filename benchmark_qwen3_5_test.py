"""
BioChain System Prompt Test for Qwen3.5-A3B-AWQ-4bit (vLLM Docker)

Tests whether an untrained model can produce parseable BioChain notation
from system prompt alone. Ports BioChainParser.cs regex patterns to Python.

Usage:
  python benchmark_qwen3_5_test.py [--endpoint URL] [--model MODEL]
"""

import re, json, sys, time, argparse
from pathlib import Path

try:
    from openai import OpenAI
except ImportError:
    print("pip install openai")
    sys.exit(1)

# ── Parser Port (BioChainParser.cs → Python) ──

VALID_TAGS = {
    "SIGNAL", "RECEPTOR", "GATE", "LIMITER", "FEEDBACK", "FORMULA", "STATE",
    "TRANSPORT", "INTERFACE", "DEF", "DYSREG", "HYPOTHESIS", "PREDICTION", "INTERVENTION"
}

# Signal: optional TYPE: prefix, CODE[state] @REGION
SIGNAL_RE = re.compile(
    r'(?:(?P<type>NT|H|P|NI|NS|eCB):)?(?P<code>[\w]+)\[(?P<state>[^\]]+)\]\s*(?:@(?P<region>[^\s(]+))?'
)

# Receptor: signal.code(subtype)[.state] @region  OR  signal.code[.state] @region (subtype)
RECEPTOR_RE = re.compile(
    r'(?P<signal>\w+)\.(?P<code>\w+)(?:\((?P<subtype1>[^)]+)\))?(?:\[\.?(?P<state>[^\]]+)\])?\s*(?:@\w+)?\s*(?:\((?P<subtype2>[^)]+)\))?'
)

# Gate: {SYMBOL(condition) → effect} or {SYMBOL: expr}
GATE_RE = re.compile(
    r'\{(?P<sym>[\u22A8\u22A1\u03A3\u229B\u22B3\u22BC\u22BD\u00AC\u2295\u2442])'
    r'(?:\((?P<cond>[^)]+)\)\s*\u2192\s*(?P<effect>[^}]+)|:\s*(?P<cond2>[^}]+))\}'
)

# Limiter: CODE⧫?[activity] → reaction @REGION
LIMITER_RE = re.compile(
    r'(?P<code>[\w\u29EB]+)\[(?P<act>[^\]]+)\]\s*\u2192\s*(?P<reaction>.+?)(?:\s+@\w+)?\s*$'
)

# Transporter: CODE[state] @REGION
TRANSPORTER_RE = re.compile(
    r'(?P<code>\w+)\[(?P<state>[^\]]+)\]\s*(?:@(?P<region>\w+))?'
)

# Interface: REGION → REGION (pathway)
INTERFACE_RE = re.compile(
    r'^(?P<src>[\w]+)\s*\u2192\s*(?P<tgt>[\w]+)(?:\s*\((?P<path>[^)]+)\))?'
)

# Signal refs in formulas: CODE@REGION or CODE[state] @REGION
SIGNAL_REF_RE = re.compile(
    r'(?P<code>\w+)(?:@|\[.+?\]\s*@)(?P<region>\w+)'
)

TAG_EXTRACTORS = {
    "SIGNAL": ("signal", SIGNAL_RE),
    "STATE": ("signal", SIGNAL_RE),
    "RECEPTOR": ("receptor", RECEPTOR_RE),
    "GATE": ("gate", GATE_RE),
    "LIMITER": ("limiter", LIMITER_RE),
    "TRANSPORT": ("transporter", TRANSPORTER_RE),
    "INTERFACE": ("interface", INTERFACE_RE),
    "FORMULA": ("signal_ref", SIGNAL_REF_RE),
    "FEEDBACK": ("signal_ref", SIGNAL_REF_RE),
    "DEF": ("signal_ref", SIGNAL_REF_RE),
}

def parse_biochain(raw: str) -> list[dict]:
    """Port of BioChainParser.Parse()"""
    if not raw or not raw.strip():
        return []

    result = []
    phase = None

    for raw_line in raw.split('\n'):
        line = raw_line.strip()
        if not line:
            continue

        # #PHASE: name
        if line.upper().startswith("#PHASE:"):
            phase = line[7:].strip()
            continue

        # TAG: content — status: value
        colon = line.find(':')
        if colon <= 0 or colon > 15:
            continue

        tag = line[:colon].strip().upper()
        if tag not in VALID_TAGS:
            continue

        rest = line[colon+1:].strip()

        # Extract status suffix (em-dash or hyphen)
        status = None
        em_idx = rest.find('\u2014 status:')
        if em_idx >= 0:
            status = rest[em_idx + len('\u2014 status:'):].strip()
            rest = rest[:em_idx].rstrip()
        else:
            hy_idx = rest.find('- status:')
            if hy_idx >= 0:
                status = rest[hy_idx + len('- status:'):].strip()
                rest = rest[:hy_idx].rstrip()

        result.append({
            'tag': tag,
            'formula': rest,
            'status': status,
            'phase': phase,
        })

    return result


def extract_components(parsed_line: dict) -> dict | None:
    """Port of BioChainParser.Extract* methods — tests if regex matches the formula"""
    tag = parsed_line['tag']
    formula = parsed_line['formula']

    if tag not in TAG_EXTRACTORS:
        # DYSREG, HYPOTHESIS, PREDICTION, INTERVENTION — no regex extraction needed
        return {'type': 'text_only', 'tag': tag}

    kind, pattern = TAG_EXTRACTORS[tag]
    m = pattern.search(formula) if kind != 'interface' else pattern.match(formula)

    if not m:
        return None  # regex failed — parser can't extract this

    if kind == 'signal':
        return {
            'type': 'signal',
            'code': m.group('code'),
            'state': m.group('state'),
            'region': m.group('region'),
            'signal_type': m.group('type') if m.group('type') else infer_signal_type(m.group('code')),
        }
    elif kind == 'receptor':
        return {
            'type': 'receptor',
            'signal': m.group('signal'),
            'code': m.group('code'),
            'state': m.group('state'),
            'subtype': m.group('subtype1') or m.group('subtype2'),
        }
    elif kind == 'gate':
        cond = m.group('cond') or m.group('cond2') or ''
        effect = m.group('effect') or ''
        return {
            'type': 'gate',
            'symbol': m.group('sym'),
            'condition': cond.strip(),
            'effect': effect.strip(),
            'gate_type': map_gate_type(m.group('sym')),
        }
    elif kind == 'limiter':
        raw_code = m.group('code')
        return {
            'type': 'limiter',
            'code': raw_code.replace('\u29EB', ''),
            'activity': m.group('act'),
            'rate_limiting': '\u29EB' in raw_code,
            'reaction': m.group('reaction').strip(),
        }
    elif kind == 'transporter':
        return {
            'type': 'transporter',
            'code': m.group('code'),
            'state': m.group('state'),
            'region': m.group('region'),
        }
    elif kind == 'interface':
        return {
            'type': 'interface',
            'source': m.group('src'),
            'target': m.group('tgt'),
            'pathway': m.group('path'),
        }
    elif kind == 'signal_ref':
        return {
            'type': 'signal_ref',
            'code': m.group('code'),
            'region': m.group('region'),
        }
    return None


def infer_signal_type(code: str) -> str:
    """Port of BioChainParser.InferSignalType()"""
    c = code.upper()
    nt = {"DA","5HT","NE","GABA","GLU","ACH","GLYCINE","HISTAMINE","ATP","ADENOSINE"}
    h = {"CORTISOL","CRH","ACTH","TESTOSTERONE","ESTRADIOL","PROGESTERONE","DHEA",
         "MELATONIN","INSULIN","LEPTIN","GHRELIN","THYROID","T3","T4",
         "ADRENALINE","EPINEPHRINE","NORADRENALINE"}
    p = {"OXT","AVP","DYNORPHIN","ENDORPHIN","ENKEPHALIN","SUBSTANCE_P","NPY",
         "CRF","BDNF","NGF","OREXIN","VIP","CCK","CGRP"}
    ecb = {"AEA","2AG","ANA","ANANDAMIDE","PEA","OEA","ECB"}
    ni = {"IL6","IL1","IL10","TNF","TNFA","NFKB","IFN","CRP"}
    ns = {"ALLOPREGNANOLONE","PREGNENOLONE","DHEAS"}

    if c in nt: return "NT"
    if c in h: return "H"
    if c in p: return "P"
    if c in ecb: return "eCB"
    if c in ni: return "NI"
    if c in ns: return "NS"
    return "NT"


def map_gate_type(sym: str) -> str:
    """Port of BioChainParser.MapGateType()"""
    return {
        '\u22A8': 'threshold', '\u22A1': 'latch', '\u03A3': 'integrator',
        '\u229B': 'novelty', '\u22B3': 'gain', '\u22BC': 'and',
        '\u22BD': 'or', '\u00AC': 'not', '\u2295': 'xor', '\u2442': 'splitter',
    }.get(sym, 'threshold')


# ── Test Prompts ──

PROMPTS = [
    {
        "id": "Q1_stress_anxiety",
        "text": (
            "Patient reports chronic stress, poor sleep, and difficulty concentrating. "
            "History of anxiety. Frequently irritable, avoids social situations. "
            "Reports racing thoughts at night and morning fatigue."
        ),
    },
    {
        "id": "Q2_depression",
        "text": (
            "Subject exhibits flat affect, anhedonia, and psychomotor retardation. "
            "Lost interest in previously enjoyed activities. Weight gain of 15 lbs over 3 months. "
            "Sleeps 12+ hours but wakes unrefreshed. Reports feelings of worthlessness."
        ),
    },
    {
        "id": "Q3_adhd_traits",
        "text": (
            "Individual shows difficulty maintaining focus, frequently loses items, "
            "and struggles with task completion. Hyperfocuses on novel stimuli but "
            "cannot sustain attention on routine tasks. Impulsive spending habits. "
            "Seeks constant stimulation and novelty."
        ),
    },
]


def score_output(raw: str, prompt_id: str) -> dict:
    """Score a single model output"""
    # Strip thinking blocks if present (Qwen3.5 may use <think>...</think>)
    cleaned = re.sub(r'<think>.*?</think>', '', raw, flags=re.DOTALL).strip()

    lines = parse_biochain(cleaned)

    # Count total non-empty lines in output
    total_output_lines = len([l for l in cleaned.split('\n') if l.strip() and not l.strip().upper().startswith('#PHASE')])

    # Parse rate
    parse_rate = len(lines) / max(total_output_lines, 1)

    # Tag coverage
    tags_found = set(l['tag'] for l in lines)
    core_tags = {"SIGNAL", "RECEPTOR", "GATE", "LIMITER", "TRANSPORT", "INTERFACE"}
    extended_tags = {"FEEDBACK", "FORMULA", "DYSREG", "HYPOTHESIS", "PREDICTION", "INTERVENTION"}
    core_coverage = len(tags_found & core_tags) / len(core_tags)
    extended_coverage = len(tags_found & extended_tags) / len(extended_tags)

    # Extraction success (regex match rate for extractable tags)
    extractable = [l for l in lines if l['tag'] in TAG_EXTRACTORS]
    extracted = 0
    extract_details = []
    for l in extractable:
        comp = extract_components(l)
        if comp is not None:
            extracted += 1
            extract_details.append(f"  OK {l['tag']}: {l['formula'][:60]}")
        else:
            extract_details.append(f"  FAIL {l['tag']}: {l['formula'][:60]}")

    extraction_rate = extracted / max(len(extractable), 1)

    # Status suffix rate
    with_status = len([l for l in lines if l['status']])
    status_rate = with_status / max(len(lines), 1)

    # Phase usage
    has_phases = any(l['phase'] for l in lines)

    # Signal diversity (unique codes)
    signal_lines = [l for l in lines if l['tag'] in ('SIGNAL', 'STATE')]
    signal_codes = set()
    for l in signal_lines:
        m = SIGNAL_RE.search(l['formula'])
        if m:
            signal_codes.add(m.group('code'))

    # Unicode compliance (uses actual arrows, not ASCII)
    uses_unicode_arrows = bool(re.search(r'[↑↓≈→⧫⊨⊛Σ⟳]', cleaned))

    return {
        'prompt_id': prompt_id,
        'total_output_lines': total_output_lines,
        'parsed_lines': len(lines),
        'parse_rate': round(parse_rate, 3),
        'core_tag_coverage': round(core_coverage, 3),
        'extended_tag_coverage': round(extended_coverage, 3),
        'tags_found': sorted(tags_found),
        'extraction_rate': round(extraction_rate, 3),
        'status_rate': round(status_rate, 3),
        'has_phases': has_phases,
        'signal_codes': sorted(signal_codes),
        'signal_diversity': len(signal_codes),
        'uses_unicode': uses_unicode_arrows,
        'extract_details': extract_details,
    }


def run_test(endpoint: str, model: str, system_prompt: str, iterations: int = 1):
    """Run the full benchmark"""
    client = OpenAI(base_url=endpoint, api_key="not-needed")

    all_scores = []

    for iteration in range(iterations):
        print(f"\n{'='*60}")
        print(f"Iteration {iteration + 1}/{iterations}")
        print(f"{'='*60}")

        for prompt in PROMPTS:
            print(f"\n--- {prompt['id']} ---")

            try:
                t0 = time.time()
                response = client.chat.completions.create(
                    model=model,
                    messages=[
                        {"role": "system", "content": system_prompt},
                        {"role": "user", "content": f"Analyze this psychological assessment:\n\n{prompt['text']}"},
                    ],
                    max_tokens=2048,
                    temperature=0.6,
                    top_p=0.95,
                    extra_body={"repetition_penalty": 1.1},
                )
                elapsed = time.time() - t0

                raw = response.choices[0].message.content or ""
                print(f"  Response: {len(raw)} chars in {elapsed:.1f}s")

                if not raw.strip():
                    print("  EMPTY RESPONSE")
                    all_scores.append({
                        'prompt_id': prompt['id'],
                        'iteration': iteration,
                        'empty': True,
                        'parse_rate': 0,
                        'core_tag_coverage': 0,
                        'extended_tag_coverage': 0,
                        'extraction_rate': 0,
                    })
                    continue

                # Print first 500 chars of raw output
                preview = raw[:500].replace('\n', '\n  | ')
                print(f"  | {preview}")
                if len(raw) > 500:
                    print(f"  | ... ({len(raw) - 500} more chars)")

                score = score_output(raw, prompt['id'])
                score['iteration'] = iteration
                score['empty'] = False
                score['elapsed_s'] = round(elapsed, 1)
                score['raw_length'] = len(raw)

                # Print score summary
                print(f"  Parse rate: {score['parse_rate']:.0%} ({score['parsed_lines']}/{score['total_output_lines']})")
                print(f"  Core tags: {score['core_tag_coverage']:.0%} | Extended: {score['extended_tag_coverage']:.0%}")
                print(f"  Extraction: {score['extraction_rate']:.0%} | Status: {score['status_rate']:.0%}")
                print(f"  Phases: {score['has_phases']} | Unicode: {score['uses_unicode']}")
                print(f"  Signals: {score['signal_codes']}")
                print(f"  Tags: {score['tags_found']}")

                # Show extraction details
                for d in score['extract_details']:
                    print(f"  {d}")

                all_scores.append(score)

            except Exception as e:
                print(f"  ERROR: {e}")
                all_scores.append({
                    'prompt_id': prompt['id'],
                    'iteration': iteration,
                    'error': str(e),
                    'parse_rate': 0,
                    'core_tag_coverage': 0,
                    'extraction_rate': 0,
                })

    # Summary
    valid = [s for s in all_scores if not s.get('empty') and not s.get('error')]
    if valid:
        avg_parse = sum(s['parse_rate'] for s in valid) / len(valid)
        avg_core = sum(s['core_tag_coverage'] for s in valid) / len(valid)
        avg_ext = sum(s.get('extended_tag_coverage', 0) for s in valid) / len(valid)
        avg_extract = sum(s['extraction_rate'] for s in valid) / len(valid)
        avg_status = sum(s.get('status_rate', 0) for s in valid) / len(valid)

        print(f"\n{'='*60}")
        print(f"SUMMARY ({len(valid)}/{len(all_scores)} valid responses)")
        print(f"{'='*60}")
        print(f"  Parse rate:      {avg_parse:.1%}")
        print(f"  Core coverage:   {avg_core:.1%}")
        print(f"  Extended coverage: {avg_ext:.1%}")
        print(f"  Extraction rate: {avg_extract:.1%}")
        print(f"  Status rate:     {avg_status:.1%}")
        print(f"  Empty/Error:     {len(all_scores) - len(valid)}/{len(all_scores)}")

        # Overall score (weighted)
        overall = (
            avg_parse * 0.3 +
            avg_core * 0.2 +
            avg_ext * 0.1 +
            avg_extract * 0.3 +
            avg_status * 0.1
        )
        print(f"\n  OVERALL SCORE: {overall:.1%}")
    else:
        print("\nNo valid responses to score.")

    # Save results
    results = {
        'model': model,
        'endpoint': endpoint,
        'timestamp': time.strftime('%Y-%m-%d %H:%M:%S'),
        'iterations': iterations,
        'scores': all_scores,
        'summary': {
            'valid_responses': len(valid),
            'total_runs': len(all_scores),
            'avg_parse_rate': round(avg_parse, 4) if valid else 0,
            'avg_core_coverage': round(avg_core, 4) if valid else 0,
            'avg_extraction_rate': round(avg_extract, 4) if valid else 0,
            'overall_score': round(overall, 4) if valid else 0,
        } if valid else {},
    }

    out_path = Path('benchmark_qwen3_5_results.json')
    out_path.write_text(json.dumps(results, indent=2, default=str))
    print(f"\nResults saved to {out_path}")


def main():
    parser = argparse.ArgumentParser(description="BioChain system prompt test for Qwen3.5")
    parser.add_argument('--endpoint', default='http://localhost:8000/v1',
                        help='vLLM OpenAI-compatible endpoint (default: http://localhost:8000/v1)')
    parser.add_argument('--model', default='Qwen/Qwen3-235B-A22B-AWQ',
                        help='Model name as registered in vLLM')
    parser.add_argument('--iterations', type=int, default=1,
                        help='Number of iterations per prompt (default: 1)')
    args = parser.parse_args()

    # Load system prompt
    prompt_path = Path(__file__).parent / 'ollama' / 'system-prompt-biochain-qwen3.5.txt'
    if not prompt_path.exists():
        print(f"System prompt not found: {prompt_path}")
        sys.exit(1)

    system_prompt = prompt_path.read_text(encoding='utf-8')
    print(f"System prompt: {len(system_prompt)} chars")
    print(f"Endpoint: {args.endpoint}")
    print(f"Model: {args.model}")

    run_test(args.endpoint, args.model, system_prompt, args.iterations)


if __name__ == '__main__':
    main()
