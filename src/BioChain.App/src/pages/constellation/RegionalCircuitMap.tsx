import { useMemo, useState } from 'react';
import type { ConstellationGraphResponse, ConstellationAnalysisResponse, ConstellationNode } from '@/types/constellation';
import { TYPE_COLORS, stateIcon, humanLabel, COMMUNITY_STATUS_COLORS } from '@/types/constellation';
import type { FocusTarget, FocusState } from '@/stores/constellationStore';

interface RegionalCircuitMapProps {
  graph: ConstellationGraphResponse;
  analysis: ConstellationAnalysisResponse | null;
  focus: FocusState;
  onFocus: (target: FocusTarget) => void;
}

interface RegionGroup {
  code: string;
  nodes: ConstellationNode[];
  communityStatus?: string;
}

// Anatomical region taxonomy — groups every known backend region code
const REGION_TAXONOMY: { label: string; color: string; codes: string[] }[] = [
  { label: 'Cortical', color: '#6366f1', codes: ['PFC', 'mPFC', 'dlPFC', 'OFC', 'ACC', 'INS', 'sensory_cortex'] },
  { label: 'Limbic', color: '#ec4899', codes: ['AMY', 'HPC', 'NAc', 'BNST'] },
  { label: 'Basal Ganglia', color: '#f59e0b', codes: ['BG', 'STR', 'SN'] },
  { label: 'Brainstem', color: '#10b981', codes: ['VTA', 'DRN', 'LC', 'PAG', 'NTS', 'RVM', 'TMN', 'VLPO'] },
  { label: 'Hypothalamic', color: '#8b5cf6', codes: ['HYP', 'PVN', 'PIT', 'SCN', 'LH'] },
  { label: 'Peripheral', color: '#f97316', codes: ['ADR', 'thyroid', 'liver', 'gut', 'periphery', 'CNS'] },
];

// Full names for region codes
const REGION_NAMES: Record<string, string> = {
  PFC: 'Prefrontal Cortex', mPFC: 'Medial PFC', dlPFC: 'Dorsolateral PFC',
  OFC: 'Orbitofrontal', ACC: 'Anterior Cingulate', INS: 'Insula',
  sensory_cortex: 'Sensory Cortex',
  AMY: 'Amygdala', HPC: 'Hippocampus', NAc: 'Nucleus Accumbens',
  BNST: 'Bed Nucleus ST',
  BG: 'Basal Ganglia', STR: 'Striatum', SN: 'Substantia Nigra',
  VTA: 'Ventral Tegmental', DRN: 'Dorsal Raphe', LC: 'Locus Coeruleus',
  PAG: 'Periaqueductal Gray', NTS: 'Nucleus Tractus', RVM: 'Rostral Ventromedial',
  TMN: 'Tuberomammillary', VLPO: 'Ventrolateral Preoptic',
  HYP: 'Hypothalamus', PVN: 'Paraventricular', PIT: 'Pituitary',
  SCN: 'Suprachiasmatic', LH: 'Lateral Hypothalamus',
  ADR: 'Adrenal', thyroid: 'Thyroid', liver: 'Liver',
  gut: 'Gut', periphery: 'Periphery', CNS: 'Central NS',
};

export function RegionalCircuitMap({ graph, analysis, focus, onFocus }: RegionalCircuitMapProps) {
  const [expandedRegion, setExpandedRegion] = useState<string | null>(null);

  // Group nodes by region code
  const regionMap = useMemo(() => {
    const map = new Map<string, ConstellationNode[]>();
    for (const node of graph.nodes) {
      if (node.kind === 'bind' || node.kind === 'interface') continue;
      const region = node.region || 'UNG';
      if (!map.has(region)) map.set(region, []);
      map.get(region)!.push(node);
    }
    return map;
  }, [graph]);

  // Build grouped regions by anatomical taxonomy
  const groupedRegions = useMemo(() => {
    const assigned = new Set<string>();

    const groups = REGION_TAXONOMY.map((taxon) => {
      const regions: RegionGroup[] = [];
      for (const code of taxon.codes) {
        const nodes = regionMap.get(code);
        if (!nodes || nodes.length === 0) continue;
        assigned.add(code);

        const sorted = [...nodes].sort((a, b) => b.weight - a.weight);
        const comIds = [...new Set(sorted.map((n) => n.community))];
        const dominantCom = comIds.length > 0 ? graph.communities.find((c) => c.id === comIds[0]) : undefined;

        regions.push({ code, nodes: sorted, communityStatus: dominantCom?.status });
      }
      return { ...taxon, regions };
    }).filter((g) => g.regions.length > 0);

    // Collect unassigned regions
    const unassigned: RegionGroup[] = [];
    for (const [code, nodes] of regionMap) {
      if (assigned.has(code)) continue;
      const sorted = [...nodes].sort((a, b) => b.weight - a.weight);
      const comIds = [...new Set(sorted.map((n) => n.community))];
      const dominantCom = comIds.length > 0 ? graph.communities.find((c) => c.id === comIds[0]) : undefined;
      unassigned.push({ code, nodes: sorted, communityStatus: dominantCom?.status });
    }
    if (unassigned.length > 0) {
      groups.push({ label: 'Other', color: '#64748b', codes: [], regions: unassigned });
    }

    return groups;
  }, [regionMap, graph]);

  // Inter-region interfaces
  const interfaces = useMemo(() => graph.nodes.filter((n) => n.kind === 'interface'), [graph]);

  // Unique inter-region flows (deduplicate)
  const flows = useMemo(() => {
    const seen = new Set<string>();
    return interfaces.filter((iface) => {
      const key = iface.code;
      if (seen.has(key)) return false;
      seen.add(key);
      return true;
    }).slice(0, 20);
  }, [interfaces]);

  const totalNodeCount = useMemo(() => {
    return groupedRegions.reduce((s, g) => s + g.regions.reduce((s2, r) => s2 + r.nodes.length, 0), 0);
  }, [groupedRegions]);

  return (
    <div style={{ width: '100%', height: '100%', overflow: 'auto', padding: 6 }}>
      {/* Summary bar */}
      <div style={{ display: 'flex', alignItems: 'center', gap: 8, marginBottom: 6, padding: '0 2px' }}>
        <span style={{ fontSize: 9, color: '#475569' }}>
          {totalNodeCount} nodes across {regionMap.size} regions
        </span>
        {flows.length > 0 && (
          <span style={{ fontSize: 9, color: '#6366f180' }}>
            {flows.length} inter-region flows
          </span>
        )}
      </div>

      {/* Anatomical groups */}
      {groupedRegions.map((group) => (
        <div key={group.label} style={{ marginBottom: 8 }}>
          {/* Group header */}
          <div style={{
            display: 'flex', alignItems: 'center', gap: 6,
            padding: '3px 6px', marginBottom: 3,
            borderLeft: `3px solid ${group.color}`, background: group.color + '08',
            borderRadius: '0 4px 4px 0',
          }}>
            <span style={{ fontSize: 9, fontWeight: 700, color: group.color, textTransform: 'uppercase', letterSpacing: '0.06em' }}>
              {group.label}
            </span>
            <span style={{ fontSize: 8, color: '#475569' }}>
              {group.regions.reduce((s, r) => s + r.nodes.length, 0)} nodes
            </span>
          </div>

          {/* Region cards */}
          <div style={{ display: 'flex', flexWrap: 'wrap', gap: 3, padding: '0 2px' }}>
            {group.regions.map((region) => {
              const isFocused = focus.entity ? region.nodes.some((n) => n.id === focus.entity) : false;
              const isExpanded = expandedRegion === region.code;
              const statusColor = region.communityStatus
                ? COMMUNITY_STATUS_COLORS[region.communityStatus] ?? '#334155'
                : '#334155';
              const visibleNodes = isExpanded ? region.nodes : region.nodes.slice(0, 4);

              return (
                <div
                  key={region.code}
                  onClick={() => setExpandedRegion(isExpanded ? null : region.code)}
                  style={{
                    flex: isExpanded ? '1 1 100%' : '0 0 auto',
                    minWidth: isExpanded ? '100%' : 120,
                    maxWidth: isExpanded ? '100%' : 200,
                    padding: 0, borderRadius: 8, background: '#0f172a',
                    border: `1px solid ${isFocused ? group.color : '#1e293b'}`,
                    cursor: 'pointer', overflow: 'hidden',
                    transition: 'border-color 0.15s',
                  }}
                >
                  {/* Status bar */}
                  <div style={{ height: 2, background: statusColor + '30' }}>
                    <div style={{ height: '100%', background: statusColor, width: region.communityStatus === 'functional' ? '100%' : region.communityStatus === 'compensated' ? '70%' : region.communityStatus === 'impaired' ? '45%' : '25%', borderRadius: '0 2px 2px 0' }} />
                  </div>

                  <div style={{ padding: '4px 6px' }}>
                    {/* Region header */}
                    <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 3 }}>
                      <div style={{ display: 'flex', alignItems: 'center', gap: 4 }}>
                        <span style={{ fontSize: 10, fontWeight: 700, fontFamily: 'monospace', color: group.color }}>
                          {region.code}
                        </span>
                        {REGION_NAMES[region.code] && (
                          <span style={{ fontSize: 8, color: '#475569' }}>
                            {REGION_NAMES[region.code]}
                          </span>
                        )}
                      </div>
                      <span style={{ fontSize: 8, color: '#475569', fontFamily: 'monospace' }}>
                        {region.nodes.length}
                      </span>
                    </div>

                    {/* Signal pills */}
                    <div style={{ display: 'flex', flexWrap: 'wrap', gap: 2 }}>
                      {visibleNodes.map((node) => {
                        const color = TYPE_COLORS[node.type] ?? '#64748b';
                        const isNodeFocused = focus.entity === node.id;
                        return (
                          <span
                            key={node.id}
                            onClick={(e) => { e.stopPropagation(); onFocus({ type: 'entity', id: node.id }); }}
                            style={{
                              padding: '1px 4px', borderRadius: 3, fontSize: 8, fontFamily: 'monospace',
                              background: isNodeFocused ? color + '40' : color + '12',
                              color: color,
                              border: isNodeFocused ? `1px solid ${color}` : '1px solid transparent',
                              cursor: 'pointer', whiteSpace: 'nowrap',
                            }}
                          >
                            {humanLabel(node.code, analysis?.humanLabels)} {stateIcon(node.state)}
                          </span>
                        );
                      })}
                      {!isExpanded && region.nodes.length > 4 && (
                        <span style={{ padding: '1px 4px', fontSize: 8, color: '#475569' }}>
                          +{region.nodes.length - 4}
                        </span>
                      )}
                    </div>
                  </div>
                </div>
              );
            })}
          </div>
        </div>
      ))}

      {/* Inter-region flows */}
      {flows.length > 0 && (
        <div style={{ marginTop: 6, padding: '4px 6px', borderTop: '1px solid #1e293b' }}>
          <div style={{ fontSize: 8, fontWeight: 700, color: '#6366f1', textTransform: 'uppercase', marginBottom: 4 }}>
            Inter-Region Flows
          </div>
          <div style={{ display: 'flex', flexWrap: 'wrap', gap: 3 }}>
            {flows.map((iface, i) => (
              <span
                key={i}
                onClick={() => onFocus({ type: 'entity', id: iface.id })}
                style={{
                  padding: '2px 6px', borderRadius: 4, fontSize: 8, fontFamily: 'monospace',
                  background: '#6366f110', color: '#a5b4fc', cursor: 'pointer',
                  border: focus.entity === iface.id ? '1px solid #6366f1' : '1px solid transparent',
                }}
              >
                {iface.code} {stateIcon(iface.state)}
              </span>
            ))}
          </div>
        </div>
      )}
    </div>
  );
}
