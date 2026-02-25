using System.Collections.Concurrent;
using System.Text.Json;
using BioChain.AgentFramework;
using BioChain.AnalysisFramework;
using BioChain.Models;
using BioChain.Repository;

namespace BioChain.Service;

// Orchestrates the insights engine: loads profile data from repositories,
// delegates to pure math modules in AnalysisFramework, returns DTO results.
public class ProfileAnalysisService(
    ObservationRepository _observationRepo,
    DimensionDefinitionsService _dimDefs,
    ShadowAnchorService _shadowAnchor,
    AnalyzeService _analyzeService,
    ChatClient _reasoningClient,
    EmbeddingService _embeddingService)
{
    // Cached: signal → primary dimension name (highest affinity weight)
    private IReadOnlyDictionary<string, string>? _primaryDimMap;

    // Cached: signal → any dimension with nonzero affinity (fallback)
    private IReadOnlyDictionary<string, string>? _anyDimMap;

    // Cache for AI-generated strengths/challenges (keyed by person, 30min TTL)
    private readonly ConcurrentDictionary<string, (DateTime CachedAt, StrengthsChallengesResultDto Result)> _scCache = new();
    private readonly ConcurrentDictionary<string, (DateTime CachedAt, CrossProfileResultDto Result)> _crossCache = new();
    private static readonly TimeSpan ScCacheTtl = TimeSpan.FromMinutes(30);

    // Layer color mapping for display-ready DTOs
    private static readonly Dictionary<string, string> LayerColors =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["neurotransmitter"] = "#6366f1",
            ["hormone"] = "#f59e0b",
            ["peptide"] = "#10b981",
        };

    // ── Dimension resolution ──

    private async Task<IReadOnlyDictionary<string, string>> GetPrimaryDimMapAsync()
    {
        if (_primaryDimMap is not null) return _primaryDimMap;

        var dims = await _dimDefs.GetAllAsync();
        var map = new Dictionary<string, (string DimName, float Weight)>(StringComparer.OrdinalIgnoreCase);

        foreach (var dim in dims)
        {
            foreach (var (signal, weight) in dim.SignalAffinity)
            {
                if (!map.TryGetValue(signal, out var best) || weight > best.Weight)
                    map[signal] = (dim.Name, weight);
            }
        }

        _primaryDimMap = map.ToDictionary(
            kvp => kvp.Key,
            kvp => kvp.Value.DimName,
            StringComparer.OrdinalIgnoreCase);

        return _primaryDimMap;
    }

    private async Task<IReadOnlyDictionary<string, string>> GetAnyDimMapAsync()
    {
        if (_anyDimMap is not null) return _anyDimMap;

        var dims = await _dimDefs.GetAllAsync();
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var dim in dims)
        {
            foreach (var (signal, weight) in dim.SignalAffinity)
            {
                if (weight > 0 && !map.ContainsKey(signal))
                    map[signal] = dim.Name;
            }
        }

        _anyDimMap = map;
        return _anyDimMap;
    }

    // Resolve dimension: primary first, then any with nonzero affinity, else null
    private async Task<string?> ResolveDimensionAsync(string signal)
    {
        var primaryDimMap = await GetPrimaryDimMapAsync();
        if (primaryDimMap.TryGetValue(signal, out var dim))
            return dim;

        var anyDimMap = await GetAnyDimMapAsync();
        return anyDimMap.TryGetValue(signal, out var fallback) ? fallback : null;
    }

    // Estimate level with fallback to Intensity when no dimension exists
    private async Task<float> EstimateLevelWithFallbackAsync(
        string signal, float[] embedding, float intensity, string? dimension)
    {
        if (dimension is not null)
        {
            var rawLevel = await _shadowAnchor.EstimateLevelAsync(
                dimension, "work", signal, embedding);
            return (rawLevel - 1f) / 4f;
        }

        return Math.Clamp(intensity, 0f, 1f);
    }

    private static string GetLayerColor(string layer) =>
        LayerColors.TryGetValue(layer, out var color) ? color : "#6b7280";

    // ── Profile building ──

    public async Task<SignalProfileDto> BuildProfileAsync(string person)
    {
        var entries = await _observationRepo.GetObservationEntriesAsync(person);
        var signalToLayer = await _dimDefs.GetSignalToLayerAsync();

        var levels = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
        var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var variances = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);

        var grouped = entries.GroupBy(e => e.Signal, StringComparer.OrdinalIgnoreCase);

        foreach (var group in grouped)
        {
            var signal = group.Key;
            var sorted = group.OrderByDescending(e => e.CreatedAt).ToList();
            var count = sorted.Count;

            var dim = await ResolveDimensionAsync(signal);

            var entryLevels = new List<float>();
            float weightedSum = 0f, weightTotal = 0f;

            for (var i = 0; i < count; i++)
            {
                var entry = sorted[i];
                var normalizedLevel = await EstimateLevelWithFallbackAsync(
                    signal, entry.Embedding, entry.Intensity, dim);

                var halvingWt = ResistanceEngine.HalvingWeight(i);
                weightedSum += normalizedLevel * halvingWt;
                weightTotal += halvingWt;
                entryLevels.Add(normalizedLevel);
            }

            levels[signal] = weightTotal > 0 ? weightedSum / weightTotal : 0.5f;
            counts[signal] = count;
            variances[signal] = entryLevels.Count > 1 ? Variance(entryLevels) : 0f;
        }

        var levelDtos = levels
            .Select(kvp => new SignalLevelDto(
                kvp.Key,
                signalToLayer.TryGetValue(kvp.Key, out var layer) ? layer : "unknown",
                MathF.Round(kvp.Value, 3),
                counts.GetValueOrDefault(kvp.Key),
                MathF.Round(variances.GetValueOrDefault(kvp.Key), 4)))
            .OrderByDescending(l => l.Level)
            .ToList();

        var topFive = levelDtos.Take(5).ToList();

        var totalObs = counts.Values.Sum();
        var avgVariance = variances.Count > 0 ? variances.Values.Average() : 0f;
        var maturity = ResistanceEngine.ProfileMaturity(totalObs, avgVariance);

        return new SignalProfileDto(
            person,
            MathF.Round(maturity, 3),
            totalObs,
            levels.Count,
            levelDtos,
            topFive);
    }

    // ── Forecast, Prescriptions, Health, Dashboard ──

    public async Task<PersonalForecastDto> GetForecastAsync(string person)
    {
        var (levels, counts, variances) = await BuildRawProfileAsync(person);
        var interactions = await _dimDefs.GetInteractionsAsync();
        var simulation = ForecastEngine.PropagateSignals(levels, interactions, counts, variances);
        var forecast = ForecastEngine.Forecast(simulation, interactions);
        return ToForecastDto(forecast);
    }

    public async Task<List<PrescriptionDto>> GetPrescriptionsAsync(string person)
    {
        var (levels, counts, _) = await BuildRawProfileAsync(person);
        return HealthAnalyzer.Prescribe(levels, counts).Select(ToPrescriptionDto).ToList();
    }

    public async Task<HealthIndicatorsDto> GetHealthIndicatorsAsync(string person)
    {
        var (levels, _, _) = await BuildRawProfileAsync(person);
        return BuildHealthIndicators(levels);
    }

    public async Task<DashboardResultDto> GetDashboardAsync(string person)
    {
        var profile = await BuildProfileAsync(person);
        var levels = profile.Levels.ToDictionary(l => l.Signal, l => l.Level, StringComparer.OrdinalIgnoreCase);
        var counts = profile.Levels.ToDictionary(l => l.Signal, l => l.ObservationCount, StringComparer.OrdinalIgnoreCase);
        var variances = profile.Levels.ToDictionary(l => l.Signal, l => l.Variance, StringComparer.OrdinalIgnoreCase);

        var interactions = await _dimDefs.GetInteractionsAsync();
        var simulation = ForecastEngine.PropagateSignals(levels, interactions, counts, variances);
        var forecast = ForecastEngine.Forecast(simulation, interactions);
        var prescriptions = HealthAnalyzer.Prescribe(levels, counts);
        var health = BuildHealthIndicators(levels);

        return new DashboardResultDto(
            profile, ToForecastDto(forecast),
            prescriptions.Select(ToPrescriptionDto).ToList(), health);
    }

    // ── Trajectory (FIXED: no dimension filter, flexible period) ──

    public async Task<TrajectoryResultDto> GetTrajectoryAsync(string person, int periodDays = 90)
    {
        var entries = await _observationRepo.GetObservationEntriesAsync(person);
        var signalToLayer = await _dimDefs.GetSignalToLayerAsync();

        // Use all data when periodDays <= 0, otherwise apply cutoff
        var recentEntries = periodDays > 0
            ? entries.Where(e => e.CreatedAt >= DateTime.UtcNow.AddDays(-periodDays)).ToList()
            : entries.ToList();

        // Fallback: if no entries in cutoff but data exists, use all entries
        if (recentEntries.Count == 0 && entries.Count > 0)
            recentEntries = entries.ToList();

        var signals = new List<SignalTrajectoryDto>();
        var grouped = recentEntries.GroupBy(e => e.Signal, StringComparer.OrdinalIgnoreCase);

        foreach (var signalGroup in grouped)
        {
            var signal = signalGroup.Key;
            var dim = await ResolveDimensionAsync(signal);
            var layer = signalToLayer.TryGetValue(signal, out var l) ? l : "unknown";

            var dailyGroups = signalGroup.GroupBy(e => e.CreatedAt.Date).OrderBy(g => g.Key);
            var points = new List<TrajectoryPointDto>();

            foreach (var dayGroup in dailyGroups)
            {
                float weightedSum = 0f, weightTotal = 0f;
                foreach (var entry in dayGroup)
                {
                    var normalized = await EstimateLevelWithFallbackAsync(
                        signal, entry.Embedding, entry.Intensity, dim);
                    weightedSum += normalized;
                    weightTotal += 1f;
                }

                var dayLevel = weightTotal > 0 ? weightedSum / weightTotal : 0.5f;
                points.Add(new TrajectoryPointDto(dayGroup.Key, MathF.Round(dayLevel, 3)));
            }

            if (points.Count > 0)
                signals.Add(new SignalTrajectoryDto(signal, layer, points));
        }

        return new TrajectoryResultDto(person, periodDays, signals);
    }

    // ── Key Signals (computed, display-ready) ──

    public async Task<KeySignalsResultDto> GetKeySignalsAsync(string person)
    {
        var profile = await BuildProfileAsync(person);
        var signalToLayer = await _dimDefs.GetSignalToLayerAsync();

        var keySignals = profile.Levels
            .Where(l => SignalConstants.PopulationRanges.ContainsKey(l.Signal))
            .Select(l =>
            {
                var range = SignalConstants.PopulationRanges[l.Signal];
                var deviation = MathF.Abs(l.Level - range.Center);
                var importance = Math.Max(l.ObservationCount, 1) * deviation;
                var significance = l.Level > range.High ? "strength"
                    : l.Level < range.Low ? "challenge" : "key";
                var icon = significance == "strength" ? "↑"
                    : significance == "challenge" ? "↓" : "●";
                var layer = signalToLayer.TryGetValue(l.Signal, out var ly) ? ly : l.Layer;

                return new KeySignalDto(
                    l.Signal, FormatSignalLabel(l.Signal),
                    layer, GetLayerColor(layer),
                    l.Level, $"{l.Level * 100:F0}%",
                    range.Center, range.Low, range.High,
                    significance, icon, importance, l.ObservationCount);
            })
            .OrderByDescending(k => k.Importance)
            .Take(5)
            .ToList();

        return new KeySignalsResultDto(person, keySignals, BuildKeySignalsNarrative(keySignals));
    }

    // ── Strengths & Challenges (AI-generated, display-ready) ──

    public async Task<StrengthsChallengesResultDto> GetStrengthsChallengesAsync(string person)
    {
        if (_scCache.TryGetValue(person, out var cached)
            && DateTime.UtcNow - cached.CachedAt < ScCacheTtl)
            return cached.Result;

        var profile = await BuildProfileAsync(person);
        var signalToLayer = await _dimDefs.GetSignalToLayerAsync();
        var health = BuildHealthIndicators(
            profile.Levels.ToDictionary(l => l.Signal, l => l.Level, StringComparer.OrdinalIgnoreCase));

        var strengthCandidates = new List<(SignalLevelDto Level, OptimalRange Range, float Deviation)>();
        var challengeCandidates = new List<(SignalLevelDto Level, OptimalRange Range, float Deviation)>();

        foreach (var level in profile.Levels)
        {
            if (!SignalConstants.PopulationRanges.TryGetValue(level.Signal, out var range))
                continue;

            var deviation = level.Level - range.Center;

            if (level.Level > range.High)
                strengthCandidates.Add((level, range, deviation));
            else if (level.Level < range.Low)
                challengeCandidates.Add((level, range, deviation));
        }

        var topStrengths = strengthCandidates
            .OrderByDescending(c => MathF.Abs(c.Deviation)).Take(5).ToList();
        var topChallenges = challengeCandidates
            .OrderByDescending(c => MathF.Abs(c.Deviation)).Take(5).ToList();

        // Add burnout-related challenge if too few
        if (topChallenges.Count < 2 && health.BurnoutRisk
            && SignalConstants.PopulationRanges.TryGetValue("cortisol", out var cortisolRange))
        {
            var cortisolLevel = profile.Levels.FirstOrDefault(l =>
                l.Signal.Equals("cortisol", StringComparison.OrdinalIgnoreCase));
            if (cortisolLevel is not null && topChallenges.All(c =>
                !c.Level.Signal.Equals("cortisol", StringComparison.OrdinalIgnoreCase)))
                topChallenges.Add((cortisolLevel, cortisolRange, cortisolLevel.Level - cortisolRange.Center));
        }

        // Ensure at least 2 challenges: pick signals closest to their lower boundary
        if (topChallenges.Count < 2)
        {
            var existing = new HashSet<string>(
                topChallenges.Select(c => c.Level.Signal), StringComparer.OrdinalIgnoreCase);
            var nearBoundary = profile.Levels
                .Where(l => !existing.Contains(l.Signal)
                    && SignalConstants.PopulationRanges.ContainsKey(l.Signal))
                .Select(l =>
                {
                    var r = SignalConstants.PopulationRanges[l.Signal];
                    return (Level: l, Range: r, Deviation: l.Level - r.Center);
                })
                .OrderBy(x => x.Deviation) // lowest deviation first = weakest signals
                .Take(2 - topChallenges.Count)
                .ToList();
            topChallenges.AddRange(nearBoundary);
        }

        try
        {
            var result = await GenerateWithAiAsync(
                person, profile, health, topStrengths, topChallenges, signalToLayer);
            _scCache[person] = (DateTime.UtcNow, result);
            return result;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[StrengthsChallenges] AI generation failed, using fallback: {ex.Message}");
            var fallback = BuildFallbackResult(person, topStrengths, topChallenges, signalToLayer, profile);
            _scCache[person] = (DateTime.UtcNow, fallback);
            return fallback;
        }
    }

    // ── Cross-Profile: Strength × Challenge interaction analysis ──

    // Signal knowledge base: what each signal affects in daily life + specific strategies
    private static readonly Dictionary<string, SignalKnowledge> SignalKnowledgeMap =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["dopamine"] = new("learning, motivation, reward drive, focus, habit formation",
                "prefrontal cortex and striatum via D1/D2 receptors",
                "novel learning tasks with incremental difficulty — each small win triggers a dopamine micro-burst that reinforces the neural pathway"),
            ["serotonin"] = new("emotional stability, patience, social confidence, impulse control",
                "raphe nuclei projecting to limbic system via 5-HT receptors",
                "morning bright-light exposure for 20 minutes — activates tryptophan hydroxylase in the raphe nuclei, the rate-limiting step in serotonin synthesis"),
            ["norepinephrine"] = new("alertness, mental clarity, sustained attention, stress readiness",
                "locus coeruleus projecting to prefrontal cortex via alpha/beta adrenergic receptors",
                "cold-water face immersion (30 seconds) — triggers the diving reflex which sharply activates locus coeruleus norepinephrine release"),
            ["gaba"] = new("calm under pressure, mental quieting, sleep quality, anxiety regulation",
                "widely distributed inhibitory interneurons via GABA-A and GABA-B receptors",
                "slow diaphragmatic breathing (4-7-8 pattern) — directly stimulates vagal afferents that enhance GABAergic tone in the amygdala"),
            ["acetylcholine"] = new("memory encoding, attentional spotlight, cognitive flexibility, learning speed",
                "basal forebrain (nucleus basalis) projecting to hippocampus via nicotinic/muscarinic receptors",
                "focused attention meditation (10 min) with eyes-open gaze fixation — directly engages cholinergic circuits in the basal forebrain"),
            ["endocannabinoid"] = new("stress recovery, emotional buffering, pain modulation, creative flow",
                "retrograde signaling at CB1 receptors across cortex and amygdala",
                "moderate-intensity aerobic exercise for 30+ minutes — the 'runner's high' is driven by endocannabinoid release, not endorphins"),
            ["glutamate"] = new("synaptic plasticity, learning intensity, cognitive processing speed",
                "NMDA and AMPA receptors across hippocampus and cortex",
                "alternating intense cognitive work (25 min) with complete rest (5 min) — prevents excitotoxic buildup while maintaining NMDA receptor priming"),
            ["cortisol"] = new("stress response, energy mobilization, immune regulation, wakefulness",
                "HPA axis: hypothalamus → pituitary → adrenal cortex, glucocorticoid receptors throughout brain",
                "regular sleep-wake timing — the cortisol awakening response depends on circadian consistency; irregular schedules dysregulate the HPA axis"),
            ["testosterone"] = new("confidence, competitive drive, assertiveness, risk tolerance, muscle tone",
                "hypothalamic-pituitary-gonadal axis, androgen receptors in amygdala and prefrontal cortex",
                "brief high-intensity resistance training (compound movements, 20 min) — acute testosterone elevation via hypothalamic signaling"),
            ["estradiol"] = new("verbal fluency, emotional memory, social cognition, neuroplasticity",
                "estrogen receptors (ERα/ERβ) in hippocampus, prefrontal cortex, and amygdala",
                "social connection activities with emotional depth — estradiol amplifies hippocampal synaptic plasticity during meaningful social encoding"),
            ["progesterone"] = new("calm, sleep depth, neuroprotection, emotional steadiness",
                "allopregnanolone (progesterone metabolite) acts on GABA-A receptors as a positive allosteric modulator",
                "consistent evening wind-down routine — progesterone's calming metabolite allopregnanolone peaks with regular sleep onset timing"),
            ["thyroid"] = new("metabolic rate, mental energy, body temperature regulation, cognitive speed",
                "thyroid hormone receptors (TRα/TRβ) across every cell, particularly in brain mitochondria",
                "adequate iodine and selenium intake combined with consistent meal timing — supports thyroid peroxidase activity"),
            ["adrenaline"] = new("fight-or-flight response, acute performance, crisis energy, vigilance",
                "adrenal medulla release via sympathetic nervous system, beta-adrenergic receptors",
                "box breathing (4-4-4-4) immediately after stress events — activates parasympathetic brake to prevent chronic adrenergic overstimulation"),
            ["melatonin"] = new("sleep onset, circadian rhythm, seasonal mood, antioxidant protection",
                "pineal gland secretion, MT1/MT2 receptors in suprachiasmatic nucleus",
                "eliminate blue light 90 minutes before bed and keep your room truly dark — melatonin synthesis by the pineal gland is exquisitely sensitive to retinal light input"),
            ["dhea"] = new("stress resilience, anti-aging, cortisol counterbalance, immune function",
                "adrenal cortex production, DHEA:cortisol ratio reflects allostatic load",
                "regular moderate exercise with full recovery — DHEA rises with physical activity but crashes with overtraining, making recovery the key variable"),
            ["prolactin"] = new("bonding after intimacy, parenting instinct, emotional satiation, social nurturing",
                "tuberoinfundibular pathway, dopamine normally inhibits prolactin (D2 receptor)",
                "meaningful physical affection (hugging, skin contact) — prolactin release is strongest during genuine, unhurried physical connection"),
            ["oxytocin_h"] = new("trust, social bonding, empathy, emotional safety, attachment security",
                "hypothalamic paraventricular nucleus, oxytocin receptors in amygdala and insula",
                "prolonged eye contact during meaningful conversation — activates the social engagement system and triggers oxytocin release from the hypothalamus"),
            ["oxytocin"] = new("attachment depth, partner bonding, social trust, group belonging",
                "peptide oxytocin in limbic circuits, enhances GABAergic inhibition in amygdala",
                "regular acts of generosity toward close others — giving triggers a stronger oxytocin response than receiving, building attachment circuits"),
            ["vasopressin"] = new("partner loyalty, territorial protection, long-term pair bonding, vigilance",
                "V1a receptors in ventral pallidum and lateral septum",
                "shared challenging experiences with your partner — vasopressin pair-bonding circuits are strengthened by co-regulation during stress"),
            ["endorphins"] = new("natural pain relief, euphoria, social bonding, resilience to discomfort",
                "mu-opioid receptors in periaqueductal gray and nucleus accumbens",
                "sustained rhythmic exercise (running, dancing, drumming) for 30+ minutes — crosses the threshold for beta-endorphin release from the anterior pituitary"),
            ["enkephalins"] = new("baseline contentment, hedonic tone, emotional warmth, subtle pleasure",
                "delta-opioid receptors in limbic system, shorter-acting than endorphins",
                "savoring pleasant experiences deliberately (eating slowly, lingering in nature) — enkephalin release is enhanced by conscious hedonic attention"),
            ["dynorphin"] = new("dysphoria signal, pain amplification, stress-induced anhedonia, aversion learning",
                "kappa-opioid receptors (KOR) in nucleus accumbens and amygdala — elevated levels dampen reward",
                "break up prolonged stress periods with brief genuine pleasure — dynorphin accumulates during sustained stress and KOR activation suppresses dopamine"),
            ["substance_p"] = new("emotional pain intensity, inflammation signaling, anxiety amplification, distress sensitivity",
                "NK1 receptors in amygdala and hypothalamus — amplifies both physical and emotional pain",
                "aerobic exercise at moderate intensity — directly reduces Substance P levels in cerebrospinal fluid, with effects lasting 24+ hours"),
            ["crh"] = new("anxiety drive, stress activation, hypervigilance, cortisol triggering",
                "CRH receptors (CRHR1/CRHR2) in amygdala and bed nucleus of stria terminalis — the master stress switch",
                "systematic desensitization to stressors through graded exposure — repeated safe exposure downregulates CRH receptor density in the amygdala"),
            ["npy"] = new("stress buffering, anxiety resilience, appetite regulation, emotional steadiness",
                "Y1/Y2 receptors in amygdala and hippocampus — acts as natural anti-anxiety agent",
                "regular moderate exercise combined with adequate caloric intake — NPY production requires both physical activity signals and metabolic sufficiency"),
            ["bdnf"] = new("brain growth, memory consolidation, learning capacity, neuroplasticity, depression resilience",
                "TrkB receptors in hippocampus and cortex — the brain's primary growth factor",
                "vigorous aerobic exercise (above lactate threshold for 20+ min) — BDNF release from hippocampus is intensity-dependent, crossing a threshold with vigorous effort"),
            ["orexin"] = new("wakefulness, motivation energy, appetite drive, arousal stability",
                "orexin neurons in lateral hypothalamus, OX1R/OX2R receptors in locus coeruleus and VTA",
                "consistent meal timing with protein-rich breakfast — orexin neurons are directly activated by amino acids and stabilized by circadian feeding patterns"),
        };

    public async Task<CrossProfileResultDto> GetCrossProfileAsync(string person)
    {
        if (_crossCache.TryGetValue(person, out var cached)
            && DateTime.UtcNow - cached.CachedAt < ScCacheTtl)
            return cached.Result;

        // Get strengths, challenges, and full profile
        var sc = await GetStrengthsChallengesAsync(person);
        var profile = await BuildProfileAsync(person);
        if (sc.Strengths.Count == 0 || sc.Challenges.Count == 0)
            return new CrossProfileResultDto(person, [],
                "Not enough data for cross-profile analysis.", DateTime.UtcNow.ToString("O"));

        // Generate embeddings for each signal's rich profile context
        var allSignals = sc.Strengths.Select(s => s.SignalKey)
            .Concat(sc.Challenges.Select(c => c.SignalKey))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var embeddings = new Dictionary<string, float[]>(StringComparer.OrdinalIgnoreCase);
        foreach (var sig in allSignals)
        {
            var item = sc.Strengths.FirstOrDefault(s =>
                s.SignalKey.Equals(sig, StringComparison.OrdinalIgnoreCase))
                ?? sc.Challenges.First(c =>
                    c.SignalKey.Equals(sig, StringComparison.OrdinalIgnoreCase));

            // Embed the full AI-generated explanation for semantic richness
            var knowledge = GetSignalKnowledge(sig);
            var desc = $"{item.Label} ({item.Layer}): level={item.LevelLabel}, "
                + $"deviation={item.Deviation:F3}, type={item.Type}. "
                + $"Affects: {knowledge.Affects}. "
                + $"Brain pathway: {knowledge.Pathway}. "
                + $"{item.Explanation}";
            var vecStr = await _embeddingService.GenerateVectorAsync(desc);
            embeddings[sig] = EmbeddingMath.ParseVector(vecStr);
        }

        // Compute pairwise cosine similarity between each strength–challenge pair
        var pairs = new List<(StrengthChallengeItemDto Strength, StrengthChallengeItemDto Challenge, float Sim)>();
        foreach (var s in sc.Strengths)
        {
            if (!embeddings.TryGetValue(s.SignalKey, out var sVec)) continue;
            foreach (var c in sc.Challenges)
            {
                if (!embeddings.TryGetValue(c.SignalKey, out var cVec)) continue;
                var sim = EmbeddingMath.CosineSimilarity(sVec, cVec);
                pairs.Add((s, c, sim));
            }
        }

        // Take top pairs by similarity (most neurologically intertwined)
        var topPairs = pairs.OrderByDescending(p => p.Sim).Take(6).ToList();

        // Build rich context for the LLM with full signal profile and explanations
        var profileContext = string.Join("\n", profile.Levels.Select(l =>
        {
            var rangeStr = SignalConstants.PopulationRanges.TryGetValue(l.Signal, out var r)
                ? $"optimal={r.Low:F2}-{r.High:F2}, center={r.Center:F2}" : "no range data";
            var knowledge = GetSignalKnowledge(l.Signal);
            return $"  {FormatSignalLabel(l.Signal)} ({l.Layer}): level={l.Level:F3}, obs={l.ObservationCount}, {rangeStr}\n    Affects: {knowledge.Affects}";
        }));

        var pairContext = string.Join("\n\n", topPairs.Select((p, i) =>
        {
            var sKnowledge = GetSignalKnowledge(p.Strength.SignalKey);
            var cKnowledge = GetSignalKnowledge(p.Challenge.SignalKey);
            return $"Pair {i + 1}: STRENGTH {p.Strength.Label} ({p.Strength.Layer}, level={p.Strength.LevelLabel}) "
                + $"× CHALLENGE {p.Challenge.Label} ({p.Challenge.Layer}, level={p.Challenge.LevelLabel})\n"
                + $"  Embedding similarity: {p.Sim:F3}\n"
                + $"  Strength affects: {sKnowledge.Affects}\n"
                + $"  Strength brain pathway: {sKnowledge.Pathway}\n"
                + $"  Challenge affects: {cKnowledge.Affects}\n"
                + $"  Challenge brain pathway: {cKnowledge.Pathway}\n"
                + $"  Strength context: {p.Strength.Explanation}\n"
                + $"  Challenge context: {p.Challenge.Explanation}";
        }));

        var systemPrompt = """
            You are a world-class neuroscience communicator writing for a curious person exploring their own
            brain chemistry. Write in second person ("your", "you"). Be deeply specific and personal.

            CRITICAL RULES — every pair MUST be genuinely different:
            - Each pair involves DIFFERENT signals with DIFFERENT brain pathways, receptors, and functions.
            - NEVER reuse the same explanation structure across pairs. Each interaction is a unique biological story.
            - Name specific brain regions (amygdala, hippocampus, prefrontal cortex, VTA, etc.) relevant to THAT pair.
            - Name specific receptors (D1/D2, 5-HT2A, GABA-A, mu-opioid, etc.) relevant to THAT pair.
            - Reference the actual level percentages from the data.
            - Each suggestion must be a COMPLETELY DIFFERENT practice — no two suggestions should involve the same activity.

            For the "affects" field: list 3-5 specific life domains this pair impacts (e.g., "learning speed, habit formation, emotional resilience"). These must be unique per pair.

            For each pair's "interaction": Write 3-5 sentences explaining HOW these two specific signals interact
            in THIS person's brain. Reference their actual levels, the specific receptors and pathways involved,
            and what this means for their daily experience. Every pair should read like a mini science story.

            For each pair's "suggestion": Give ONE specific, novel practice (not generic). Explain the neurochemical
            mechanism: which receptor it targets, what cascade it triggers, why it specifically helps this pair.
            Each suggestion across all pairs must be a DIFFERENT activity.

            For "mechanism": Choose the most accurate from compensatory, antagonistic, synergistic, modulatory, independent.

            For the narrative: 3-4 sentences painting a vivid picture of how ALL the pairs together create
            this person's unique neurochemical landscape.

            Respond ONLY with valid JSON:
            {
              "narrative": "...",
              "pairs": [
                {
                  "mechanism": "compensatory|antagonistic|synergistic|modulatory|independent",
                  "affects": "domain1, domain2, domain3",
                  "interaction": "unique 3-5 sentence explanation for this specific pair...",
                  "suggestion": "unique actionable suggestion with specific neurochemical reasoning..."
                }
              ]
            }
            """;

        var userMessage = $"""
            PERSON'S FULL NEUROCHEMICAL PROFILE:
            {profileContext}

            Profile maturity: {profile.Maturity:P0} ({profile.TotalObservations} observations, {profile.UniqueSignals} signals)

            STRENGTH–CHALLENGE PAIRS (ranked by neural pathway overlap):
            {pairContext}

            IMPORTANT: Each pair involves different signals with different biology. Your response for each pair
            MUST reflect that unique biology. If two pairs both involve NPY as a challenge, the interaction is
            STILL different because the strength signal is different and acts on different receptor systems.
            Every suggestion must be a completely different practice.
            """;

        try
        {
            var responseText = await _reasoningClient.SendAsync(systemPrompt, userMessage);
            var json = ExtractJson(responseText);

            var parsed = JsonSerializer.Deserialize<CrossProfileAiResponse>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                ?? throw new InvalidOperationException("Failed to parse cross-profile AI response");

            var items = topPairs.Select((p, i) =>
            {
                var ai = i < (parsed.Pairs?.Count ?? 0) ? parsed.Pairs![i] : null;
                var fallbackAffects = BuildFallbackAffects(p.Strength.SignalKey, p.Challenge.SignalKey);
                return new CrossProfileItemDto(
                    p.Strength.SignalKey, p.Strength.Label,
                    p.Challenge.SignalKey, p.Challenge.Label,
                    MathF.Round(p.Sim, 3),
                    ai?.Affects ?? fallbackAffects,
                    ai?.Interaction ?? BuildFallbackInteraction(p.Strength, p.Challenge),
                    ai?.Suggestion ?? BuildFallbackSuggestion(p.Strength, p.Challenge),
                    ai?.Mechanism ?? "modulatory");
            }).ToList();

            var result = new CrossProfileResultDto(person, items,
                parsed.Narrative ?? "Your neurochemical profile reveals a complex interplay between your natural strengths and growth areas.",
                DateTime.UtcNow.ToString("O"));
            _crossCache[person] = (DateTime.UtcNow, result);
            return result;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[CrossProfile] AI generation failed: {ex.Message}");
            var items = topPairs.Select(p => new CrossProfileItemDto(
                p.Strength.SignalKey, p.Strength.Label,
                p.Challenge.SignalKey, p.Challenge.Label,
                MathF.Round(p.Sim, 3),
                BuildFallbackAffects(p.Strength.SignalKey, p.Challenge.SignalKey),
                BuildFallbackInteraction(p.Strength, p.Challenge),
                BuildFallbackSuggestion(p.Strength, p.Challenge),
                "modulatory")).ToList();

            var result = new CrossProfileResultDto(person, items,
                $"Your profile shows {sc.Strengths.Count} elevated and {sc.Challenges.Count} depleted signals that interact through shared neural pathways, creating a unique pattern of cognitive and emotional tendencies.",
                DateTime.UtcNow.ToString("O"));
            _crossCache[person] = (DateTime.UtcNow, result);
            return result;
        }
    }

    private sealed record SignalKnowledge(string Affects, string Pathway, string Strategy);

    private static SignalKnowledge GetSignalKnowledge(string signal)
    {
        return SignalKnowledgeMap.GetValueOrDefault(signal.ToLowerInvariant(),
            new SignalKnowledge("neural signaling, mood regulation, cognitive function",
                "broadly distributed receptor systems", "targeted lifestyle practice"));
    }

    private static string BuildFallbackAffects(string strengthSig, string challengeSig)
    {
        var sKnow = GetSignalKnowledge(strengthSig);
        var cKnow = GetSignalKnowledge(challengeSig);
        // Merge the first 2 affects from each signal and deduplicate
        var sAffects = sKnow.Affects.Split(',').Select(a => a.Trim()).Take(2);
        var cAffects = cKnow.Affects.Split(',').Select(a => a.Trim()).Take(2);
        var merged = sAffects.Concat(cAffects).Distinct(StringComparer.OrdinalIgnoreCase).Take(4);
        return string.Join(", ", merged);
    }

    private static string BuildFallbackInteraction(StrengthChallengeItemDto strength, StrengthChallengeItemDto challenge)
    {
        var sKnow = GetSignalKnowledge(strength.SignalKey);
        var cKnow = GetSignalKnowledge(challenge.SignalKey);
        return $"Your elevated {strength.Label} (at {strength.LevelLabel}) acts through {sKnow.Pathway}, "
            + $"directly influencing circuits that depend on {challenge.Label} (currently at {challenge.LevelLabel}). "
            + $"{strength.Label} is central to {sKnow.Affects}, while {challenge.Label} governs {cKnow.Affects}. "
            + $"When {challenge.Label} runs low, your brain relies more heavily on {strength.Label} to compensate — "
            + $"this can boost short-term performance but creates imbalance over time, "
            + $"as the {challenge.Layer}-based {challenge.Label} pathways remain understimulated.";
    }

    private static string BuildFallbackSuggestion(StrengthChallengeItemDto strength, StrengthChallengeItemDto challenge)
    {
        var cKnow = GetSignalKnowledge(challenge.SignalKey);
        return $"{cKnow.Strategy}. This specifically targets the {cKnow.Pathway} "
            + $"that {challenge.Label} depends on, helping restore balance while your strong "
            + $"{strength.Label} continues to support you.";
    }


    // Quick mood check-in
    public async Task<CheckInResponse> ProcessMoodCheckInAsync(string person, string text)
    {
        var wordCount = text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
        var decisions = await _analyzeService.AnalyzeAsync(person, text, sourceType: "checkin", save: true);

        return new CheckInResponse(
            decisions.Count > 0, wordCount,
            decisions.Count > 0
                ? $"Processed through {decisions.Count} signal agents"
                : "No signal detections found");
    }

    // ── Private helpers ──

    private async Task<(Dictionary<string, float> Levels, Dictionary<string, int> Counts,
        Dictionary<string, float> Variances)> BuildRawProfileAsync(string person)
    {
        var entries = await _observationRepo.GetObservationEntriesAsync(person);
        var levels = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
        var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var variances = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);

        var grouped = entries.GroupBy(e => e.Signal, StringComparer.OrdinalIgnoreCase);

        foreach (var group in grouped)
        {
            var signal = group.Key;
            var sorted = group.OrderByDescending(e => e.CreatedAt).ToList();
            var dim = await ResolveDimensionAsync(signal);

            var entryLevels = new List<float>();
            float weightedSum = 0f, weightTotal = 0f;

            for (var i = 0; i < sorted.Count; i++)
            {
                var normalizedLevel = await EstimateLevelWithFallbackAsync(
                    signal, sorted[i].Embedding, sorted[i].Intensity, dim);
                var halvingWt = ResistanceEngine.HalvingWeight(i);
                weightedSum += normalizedLevel * halvingWt;
                weightTotal += halvingWt;
                entryLevels.Add(normalizedLevel);
            }

            levels[signal] = weightTotal > 0 ? weightedSum / weightTotal : 0.5f;
            counts[signal] = sorted.Count;
            variances[signal] = entryLevels.Count > 1 ? Variance(entryLevels) : 0f;
        }

        return (levels, counts, variances);
    }

    private async Task<StrengthsChallengesResultDto> GenerateWithAiAsync(
        string person, SignalProfileDto profile, HealthIndicatorsDto health,
        List<(SignalLevelDto Level, OptimalRange Range, float Deviation)> strengths,
        List<(SignalLevelDto Level, OptimalRange Range, float Deviation)> challenges,
        IReadOnlyDictionary<string, string> signalToLayer)
    {
        var signalContext = string.Join("\n", profile.Levels.Select(l =>
        {
            var rangeStr = SignalConstants.PopulationRanges.TryGetValue(l.Signal, out var r)
                ? $"optimal={r.Low:F2}-{r.High:F2}" : "no range";
            return $"- {l.Signal} ({l.Layer}): level={l.Level:F3}, obs={l.ObservationCount}, {rangeStr}";
        }));

        var healthContext = $"Burnout risk: {health.BurnoutRisk}, Growth window: {health.GrowthWindowOpen}";
        if (health.OvertrainingIndicator is not null)
            healthContext += $", Overtraining: {health.OvertrainingIndicator}";

        var strengthList = string.Join("\n", strengths.Select(s =>
            $"  - {s.Level.Signal} ({s.Level.Layer}): level={s.Level.Level:F3}, optimal={s.Range.Low:F2}-{s.Range.High:F2}, deviation=+{s.Deviation:F3}"));
        var challengeList = string.Join("\n", challenges.Select(c =>
            $"  - {c.Level.Signal} ({c.Level.Layer}): level={c.Level.Level:F3}, optimal={c.Range.Low:F2}-{c.Range.High:F2}, deviation={c.Deviation:F3}"));

        var systemPrompt = """
            You are a neurochemistry expert providing personalized insights. Given signal profile data,
            generate detailed strengths and challenges analysis. For each item provide:
            1. "title": A concise descriptive title (e.g., "High Dopaminergic Drive", "Cortisol-Serotonin Imbalance")
            2. "explanation": Detailed neurochemical explanation mentioning specific receptor subtypes (D1/D2, 5-HT1A/2A, etc.),
               pathway names (mesolimbic, mesocortical, nigrostriatal, HPA axis, etc.), brain regions (VTA, PFC, hippocampus,
               amygdala, etc.), and how the levels interact with other signals.
            3. "practicalAdvice": One actionable paragraph of life advice.
            4. "brainExercise": A specific brain exercise or technique the person can practice (with brief instructions).
            5. "relatedChemicals": Array of 1-3 other signal keys that interact with this one.

            Respond ONLY with valid JSON in this exact format:
            {
              "summary": "2-3 sentence overview of the person's neurochemical profile",
              "strengths": [{"title":"...", "explanation":"...", "practicalAdvice":"...", "brainExercise":"...", "relatedChemicals":["..."]}],
              "challenges": [{"title":"...", "explanation":"...", "practicalAdvice":"...", "brainExercise":"...", "relatedChemicals":["..."]}]
            }
            """;

        var userMessage = $"""
            Person's signal profile:
            {signalContext}

            {healthContext}

            Identified strengths (above optimal):
            {(strengthList.Length > 0 ? strengthList : "  (none identified)")}

            Identified challenges (below optimal):
            {(challengeList.Length > 0 ? challengeList : "  (none identified)")}

            Generate analysis for each strength and challenge listed above.
            """;

        var responseText = await _reasoningClient.SendAsync(systemPrompt, userMessage);
        var json = ExtractJson(responseText);

        var parsed = JsonSerializer.Deserialize<AiResponse>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? throw new InvalidOperationException("Failed to parse AI response");

        // Map AI items to display-ready DTOs, merging with computed data
        var strengthDtos = strengths.Select((s, i) =>
        {
            var ai = i < (parsed.Strengths?.Count ?? 0) ? parsed.Strengths![i] : null;
            var layer = signalToLayer.TryGetValue(s.Level.Signal, out var ly) ? ly : s.Level.Layer;
            var related = ai?.RelatedChemicals ?? [];
            return new StrengthChallengeItemDto(
                "strength", "+",
                ai?.Title ?? $"Elevated {FormatSignalLabel(s.Level.Signal)}",
                s.Level.Signal, FormatSignalLabel(s.Level.Signal),
                layer, GetLayerColor(layer),
                s.Level.Level, s.Range.Center, s.Deviation, $"{s.Level.Level * 100:F0}%",
                ai?.Explanation ?? $"{FormatSignalLabel(s.Level.Signal)} is elevated above the optimal range.",
                ai?.PracticalAdvice ?? "Channel this elevated neurochemical activity into focused tasks.",
                ai?.BrainExercise ?? "Practice mindful awareness of how this signal affects your energy.",
                related.ToArray(), related.Select(FormatSignalLabel).ToArray());
        }).ToList();

        var challengeDtos = challenges.Select((c, i) =>
        {
            var ai = i < (parsed.Challenges?.Count ?? 0) ? parsed.Challenges![i] : null;
            var layer = signalToLayer.TryGetValue(c.Level.Signal, out var ly) ? ly : c.Level.Layer;
            var related = ai?.RelatedChemicals ?? [];
            return new StrengthChallengeItemDto(
                "challenge", "!",
                ai?.Title ?? $"Low {FormatSignalLabel(c.Level.Signal)}",
                c.Level.Signal, FormatSignalLabel(c.Level.Signal),
                layer, GetLayerColor(layer),
                c.Level.Level, c.Range.Center, c.Deviation, $"{c.Level.Level * 100:F0}%",
                ai?.Explanation ?? $"{FormatSignalLabel(c.Level.Signal)} is below the optimal range.",
                ai?.PracticalAdvice ?? "Focus on lifestyle changes that support this signal's production.",
                ai?.BrainExercise ?? "Practice targeted exercises that stimulate this pathway.",
                related.ToArray(), related.Select(FormatSignalLabel).ToArray());
        }).ToList();

        return new StrengthsChallengesResultDto(
            person, strengthDtos, challengeDtos,
            parsed.Summary ?? "Your neurochemical profile shows a unique pattern of strengths and areas for growth.",
            DateTime.UtcNow.ToString("O"));
    }

    private static StrengthsChallengesResultDto BuildFallbackResult(
        string person,
        List<(SignalLevelDto Level, OptimalRange Range, float Deviation)> strengths,
        List<(SignalLevelDto Level, OptimalRange Range, float Deviation)> challenges,
        IReadOnlyDictionary<string, string> signalToLayer,
        SignalProfileDto profile)
    {
        var strengthDtos = strengths.Select(s =>
        {
            var layer = signalToLayer.TryGetValue(s.Level.Signal, out var ly) ? ly : s.Level.Layer;
            return new StrengthChallengeItemDto(
                "strength", "+", $"Elevated {FormatSignalLabel(s.Level.Signal)}",
                s.Level.Signal, FormatSignalLabel(s.Level.Signal),
                layer, GetLayerColor(layer),
                s.Level.Level, s.Range.Center, s.Deviation, $"{s.Level.Level * 100:F0}%",
                $"{FormatSignalLabel(s.Level.Signal)} is at {s.Level.Level * 100:F0}%, above the optimal range of {s.Range.Low * 100:F0}%-{s.Range.High * 100:F0}%. This elevated level suggests strong activation of related neural pathways.",
                "Channel this elevated neurochemical activity into focused tasks and creative pursuits.",
                "Practice 5 minutes of focused breathing to balance this elevated signal activity.",
                [], []);
        }).ToList();

        var challengeDtos = challenges.Select(c =>
        {
            var layer = signalToLayer.TryGetValue(c.Level.Signal, out var ly) ? ly : c.Level.Layer;
            return new StrengthChallengeItemDto(
                "challenge", "!", $"Low {FormatSignalLabel(c.Level.Signal)}",
                c.Level.Signal, FormatSignalLabel(c.Level.Signal),
                layer, GetLayerColor(layer),
                c.Level.Level, c.Range.Center, c.Deviation, $"{c.Level.Level * 100:F0}%",
                $"{FormatSignalLabel(c.Level.Signal)} is at {c.Level.Level * 100:F0}%, below the optimal range of {c.Range.Low * 100:F0}%-{c.Range.High * 100:F0}%. This may impact related behavioral and cognitive functions.",
                "Focus on lifestyle factors that naturally support this signal's production — regular exercise, quality sleep, and stress management.",
                "Try a 10-minute mindfulness exercise focusing on calm, grounded awareness.",
                [], []);
        }).ToList();

        return new StrengthsChallengesResultDto(
            person, strengthDtos, challengeDtos,
            $"Based on {profile.TotalObservations} observations across {profile.UniqueSignals} signals, your profile shows {strengthDtos.Count} notable strengths and {challengeDtos.Count} areas for development.",
            DateTime.UtcNow.ToString("O"));
    }

    // ── Static helpers ──

    private static HealthIndicatorsDto BuildHealthIndicators(IReadOnlyDictionary<string, float> levels)
    {
        var (burnoutRisk, burnoutRatio, burnoutNote) = HealthAnalyzer.DetectBurnout(levels);
        var (growthOpen, growthNote) = HealthAnalyzer.DetectGrowthWindow(levels);
        var overtraining = HealthAnalyzer.DetectOvertraining(levels);

        return new HealthIndicatorsDto(
            burnoutRisk,
            float.IsInfinity(burnoutRatio) ? null : MathF.Round(burnoutRatio, 2),
            burnoutNote, growthOpen, growthNote,
            overtraining?.Indicator, overtraining?.Recommendation);
    }

    private static float Variance(List<float> values)
    {
        if (values.Count < 2) return 0f;
        var mean = values.Average();
        return values.Select(v => (v - mean) * (v - mean)).Sum() / (values.Count - 1);
    }

    private static string FormatSignalLabel(string key) =>
        key switch
        {
            "dopamine" => "Dopamine", "serotonin" => "Serotonin",
            "norepinephrine" => "Norepinephrine", "gaba" => "GABA",
            "acetylcholine" => "Acetylcholine", "endocannabinoid" => "Endocannabinoid",
            "glutamate" => "Glutamate", "cortisol" => "Cortisol",
            "testosterone" => "Testosterone", "estradiol" => "Estradiol",
            "progesterone" => "Progesterone", "thyroid" => "Thyroid",
            "adrenaline" => "Adrenaline", "melatonin" => "Melatonin",
            "dhea" => "DHEA", "prolactin" => "Prolactin",
            "oxytocin_h" => "Oxytocin (H)", "oxytocin" => "Oxytocin",
            "vasopressin" => "Vasopressin", "endorphins" => "Endorphins",
            "enkephalins" => "Enkephalins", "dynorphin" => "Dynorphin",
            "substance_p" => "Substance P", "crh" => "CRH",
            "npy" => "NPY", "bdnf" => "BDNF", "orexin" => "Orexin",
            _ => key
        };

    private static string BuildKeySignalsNarrative(List<KeySignalDto> keySignals)
    {
        if (keySignals.Count == 0)
            return "Not enough data to identify key signals yet.";

        var strengths = keySignals.Where(k => k.Significance == "strength").Select(k => k.Label).ToList();
        var challenges = keySignals.Where(k => k.Significance == "challenge").Select(k => k.Label).ToList();
        var keys = keySignals.Where(k => k.Significance == "key").Select(k => k.Label).ToList();

        var parts = new List<string>();
        if (strengths.Count > 0) parts.Add($"elevated {string.Join(" and ", strengths)}");
        if (challenges.Count > 0) parts.Add($"below-optimal {string.Join(" and ", challenges)}");
        if (keys.Count > 0) parts.Add($"notable activity in {string.Join(" and ", keys)}");

        return $"Your neurochemical profile is characterized by {string.Join(", ", parts)}. These signals represent the strongest detections in your profile and are the primary drivers of your behavioral patterns.";
    }

    // ── DTO mappers ──

    private static PrescriptionDto ToPrescriptionDto(Prescription p) =>
        new(p.Modality, p.Rationale, p.TargetSignals, p.Priority);

    private static PersonalForecastDto ToForecastDto(PersonalForecast f) =>
        new(
            f.Signals.Select(s => new SignalForecastDto(
                s.Signal, s.Trend.ToString(), s.CurrentLevel, s.ProjectedLevel,
                s.Velocity, s.ApproachingOptimal, s.DriftingFromOptimal, s.RiskNote)).ToList(),
            f.ActiveCascades.Select(c => new CascadeAlertDto(
                c.TriggerSignal, c.AffectedSignals, c.Mechanism, c.Severity)).ToList(),
            f.StableFoundation, f.InFlux, f.OverallTrajectory, f.Narrative);

    // AI response models
    private sealed class AiResponse
    {
        public string? Summary { get; set; }
        public List<AiItem>? Strengths { get; set; }
        public List<AiItem>? Challenges { get; set; }
    }

    private sealed class AiItem
    {
        public string? Title { get; set; }
        public string? Explanation { get; set; }
        public string? PracticalAdvice { get; set; }
        public string? BrainExercise { get; set; }
        public List<string> RelatedChemicals { get; set; } = [];
    }

    private sealed class CrossProfileAiResponse
    {
        public string? Narrative { get; set; }
        public List<CrossProfileAiPair>? Pairs { get; set; }
    }

    private sealed class CrossProfileAiPair
    {
        public string? Mechanism { get; set; }
        public string? Affects { get; set; }
        public string? Interaction { get; set; }
        public string? Suggestion { get; set; }
    }

    // Robustly extract JSON from AI responses that may include markdown fences,
    // preamble text ("Done! Here is..."), or trailing commentary after the JSON.
    private static string ExtractJson(string responseText)
    {
        var text = responseText.Trim();

        // Strip markdown code fences (```json ... ```)
        if (text.StartsWith("```"))
        {
            var firstNewline = text.IndexOf('\n');
            var lastFence = text.LastIndexOf("```");
            if (firstNewline > 0 && lastFence > firstNewline)
                text = text[(firstNewline + 1)..lastFence].Trim();
        }

        // If it still doesn't start with '{', find the first '{' and match to last '}'
        if (!text.StartsWith('{'))
        {
            var start = text.IndexOf('{');
            var end = text.LastIndexOf('}');
            if (start >= 0 && end > start)
                text = text[start..(end + 1)];
        }

        return text;
    }

}
