$system = @"
You are a clinical neurochemical synthesizer. You receive a set of biochemical reasoning entries — each from a specialist agent that decided to ADD a specific chemical based on observed behavior.

Input format:
person: [name]
relationship: [type]
layer_summary:
  neurotransmitter: [list of ADD chemicals with reasoning]
  hormone: [list of ADD chemicals with reasoning]
  peptide: [list of ADD chemicals with reasoning]
skipped: [list of chemicals that were NOT activated]

Your task: Synthesize ALL the individual reasoning entries into ONE coherent clinical narrative (200-400 words). Structure:

1. DOMINANT AXIS — Identify the primary neurochemical axis driving this person's state. Name the 2-3 chemicals that form the strongest functional cluster and explain how they interact mechanistically (e.g. "CRH->cortisol->NE forms a classic HPA-sympathetic stress cascade where PVN CRH drives ACTH release while simultaneously potentiating LC norepinephrine firing").

2. CROSS-LAYER PATTERNS — Map interactions BETWEEN layers. How do neurotransmitter activations connect to hormone activations connect to peptide activations? Name specific receptor crosstalk, shared brain regions, or convergent pathways.

3. SUPPORTING SYSTEMS — Secondary chemicals that modulate or contextualize the dominant axis. Explain their role relative to the primary pattern.

4. INFORMATIVE ABSENCES — Which chemicals were SKIPPED and what does their absence reveal? A missing GABA with active NE+cortisol suggests uninhibited stress. Missing endocannabinoid with active glutamate suggests unmodulated excitatory drive. These absences are diagnostic.

5. CLINICAL SIGNATURE — One sentence capturing this person's unique neurochemical fingerprint in this moment.

Rules:
- Reference specific mechanisms from the input reasoning (don't invent new ones)
- Preserve the PhD-level precision — receptor subtypes, pathways, brain regions
- Connect, don't just list — every sentence should show HOW chemicals interact
- The narrative must be MORE than the sum of its parts
"@

$user = @"
person: Sarah
relationship: ex-partner
layer_summary:
  neurotransmitter:
    - dopamine: VTA to NAc mesolimbic pathway activated by betrayal-induced rumination, D2 receptor signaling shifts from tonic to phasic as reward prediction collapses
    - norepinephrine: LC firing potentiated by acute emotional threat, alpha-1 PFC drives hypervigilance, beta-1 cardiac sympathetic output explains physical symptoms
    - serotonin: Intense emotional pain of betrayal triggers raphe nucleus hypoactivation, 5-HT1A autoreceptor upregulation reduces serotonergic tone in PFC-amygdala circuits
  hormone:
    - cortisol: HPA axis fully engaged via PVN CRH release, sustained cortisol from zona fasciculata disrupts hippocampal GR-mediated negative feedback
    - adrenaline: Acute sympathoadrenal activation from emotional shock, chromaffin cell catecholamine release drives cardiac and respiratory symptoms
    - oxytocin_h: Paradoxical oxytocin surge from PVN magnocellular neurons during bond rupture, facilitating social pain via anterior cingulate
  peptide:
    - substance_p: NK1R activation in dorsal horn and amygdala amplifying emotional-somatic pain crossover
    - crh: PVN CRH driving both ACTH release and extrahypothalamic anxiety via CRF1 in BNST and CeA
    - dynorphin: Kappa-opioid receptor activation from aversive emotional state, prodynorphin processing in NAc shell encoding dysphoria
    - endorphins: Beta-endorphin from POMC cleavage in arcuate nucleus activating MOR as endogenous analgesic response to social rejection pain
skipped: gaba, endocannabinoid, glutamate, acetylcholine, melatonin, testosterone, estradiol, progesterone, thyroid, dhea, prolactin, vasopressin, enkephalins, npy, bdnf, orexin, oxytocin
"@

$body = @{
    model    = "neuro"
    messages = @(
        @{ role = "system"; content = $system }
        @{ role = "user";   content = $user }
    )
    max_tokens   = 800
    temperature  = 0.3
    chat_template_kwargs = @{ enable_thinking = $true }
} | ConvertTo-Json -Depth 5

$response = Invoke-RestMethod -Uri "http://localhost:8000/v1/chat/completions" `
    -Method Post -ContentType "application/json" -Body $body -TimeoutSec 120

Write-Host "`n=== Synthesis Response ===`n" -ForegroundColor Cyan
Write-Host $response.choices[0].message.content
Write-Host "`n=== Token Usage ===" -ForegroundColor Yellow
Write-Host "Prompt: $($response.usage.prompt_tokens) | Completion: $($response.usage.completion_tokens) | Total: $($response.usage.total_tokens)"
