-- seed-chemicals.sql
-- Chemical master data, dimensions, dimension-chemical affinities, chemical interactions.
-- Idempotent (ON CONFLICT DO NOTHING).

-- ═════════════════════════════════════
-- Chemical Master Data (27)
-- ═════════════════════════════════════

INSERT INTO chemical (key, label, layer) VALUES
('dopamine',        'Dopamine',        'neurotransmitter'),
('serotonin',       'Serotonin',       'neurotransmitter'),
('norepinephrine',  'Norepinephrine',  'neurotransmitter'),
('gaba',            'GABA',            'neurotransmitter'),
('acetylcholine',   'Acetylcholine',   'neurotransmitter'),
('endocannabinoid', 'Endocannabinoid', 'neurotransmitter'),
('glutamate',       'Glutamate',       'neurotransmitter'),
('cortisol',        'Cortisol',        'hormone'),
('testosterone',    'Testosterone',    'hormone'),
('estradiol',       'Estradiol',       'hormone'),
('progesterone',    'Progesterone',    'hormone'),
('thyroid',         'Thyroid',         'hormone'),
('adrenaline',      'Adrenaline',      'hormone'),
('melatonin',       'Melatonin',       'hormone'),
('dhea',            'DHEA',            'hormone'),
('prolactin',       'Prolactin',       'hormone'),
('oxytocin_h',      'Oxytocin (H)',    'hormone'),
('oxytocin',        'Oxytocin',        'peptide'),
('vasopressin',     'Vasopressin',     'peptide'),
('endorphins',      'Endorphins',      'peptide'),
('enkephalins',     'Enkephalins',     'peptide'),
('dynorphin',       'Dynorphin',       'peptide'),
('substance_p',     'Substance P',     'peptide'),
('crh',             'CRH',             'peptide'),
('npy',             'NPY',             'peptide'),
('bdnf',            'BDNF',            'peptide'),
('orexin',          'Orexin',          'peptide')
ON CONFLICT (key) DO NOTHING;

-- ═════════════════════════════════════
-- Dimension Master Data (24)
-- ═════════════════════════════════════

INSERT INTO dimension (name, section, category, description, work_relevance, private_relevance, archetype_name, archetype_essence, sort_order) VALUES
('Ambition',           'work',    'Drive & Trajectory',     'Relentless pursuit of career advancement, promotion-seeking, expanding professional authority and scope. Taking on stretch assignments, volunteering for high-visibility projects, negotiating for larger responsibilities. Building professional brand and reputation deliberately. Setting aggressive personal milestones and KPIs beyond what is required.', 1.0, 0.4, 'Rocketship', 'Relentless forward motion', 1),
('Risk Tolerance',     'work',    'Drive & Trajectory',     'Comfort with ambiguity, making decisions with incomplete information, betting on unproven technologies or architectures. Willingness to challenge established practices, propose radical alternatives, or start ventures without guaranteed outcomes. Accepting personal accountability for uncertain bets. Thriving in environments where failure is possible and visible.', 0.9, 0.5, 'Stunt Pilot', 'Calibrated edge-walking', 2),
('Persistence',        'work',    'Drive & Trajectory',     'Sustained effort through monotony, frustration, and repeated setbacks. Debugging for hours without giving up, maintaining legacy codebases, completing compliance work. Staying focused on long-term goals when short-term rewards are absent. Grinding through tedious documentation, regulatory requirements, and thankless infrastructure maintenance.', 0.9, 0.6, 'Mountain', 'Unshakeable steady climb', 3),
('Team Orientation',   'work',    'Leadership',             'Prioritizing group success over individual recognition. Active mentoring, code reviewing with genuine care for growth. Sharing credit, amplifying others'' contributions, investing time in pair programming and knowledge sharing. Building psychological safety in teams, facilitating inclusive standups and retrospectives, protecting junior members from blame.', 1.0, 0.5, 'Bonfire', 'Magnetic tribal warmth', 4),
('Strategic Thinking', 'work',    'Leadership',             'Multi-quarter planning, technology roadmapping, system architecture decisions that account for organizational constraints. Recognizing patterns across distributed systems, anticipating second-order effects of technical choices. Balancing technical debt against delivery pressure. Making trade-off decisions that consider business context, team capacity, and long-term maintenance.', 1.0, 0.3, 'Chess Master', 'Ten moves ahead', 5),
('Stress Capacity',    'work',    'Leadership',             'Maintaining clear decision-making during production incidents, security breaches, tight deadlines. Recovering quickly from high-pressure situations without accumulated burnout. Managing stakeholder expectations during crises. Functioning effectively when multiple urgent priorities compete simultaneously. Absorbing organizational stress without passing it to the team.', 1.0, 0.6, 'Iron Will', 'Pressure becomes fuel', 6),
('Competitive Drive',  'work',    'Execution',              'Drive to outperform peers, benchmarking against industry standards, pushing for best-in-class solutions. Assertiveness in technical debates, salary negotiations, performance reviews. Urgency to deliver faster or better than competing teams. Measuring personal output and wanting to be recognized as a top performer.', 1.0, 0.3, 'Gladiator', 'Win or learn trying', 7),
('Context Switching',  'work',    'Execution',              'Fluid movement between concurrent projects, meetings, code reviews without cognitive overhead. Managing multiple workstreams with different stakeholders simultaneously. Shifting between deep technical work and collaborative communication rapidly. Maintaining quality across parallel responsibilities without dropping threads.', 1.0, 0.3, 'Quicksilver', 'Effortless fluid shifts', 8),
('Problem Solving',    'work',    'Execution',              'Novel debugging approaches, creative architectural solutions for unfamiliar constraints. Lateral thinking when standard approaches fail. Breaking down complex problems into tractable subproblems. Connecting insights across different domains and technology stacks. Finding elegant solutions that simplify rather than add complexity.', 1.0, 0.5, 'Labyrinth', 'Non-obvious path finder', 9),
('Knowledge Transfer', 'work',    'Professional Growth',    'Teaching, mentoring, writing documentation, giving conference talks. Translating complex concepts into accessible explanations. Creating onboarding materials, architecture decision records, technical blog posts. Building shared understanding across teams with different expertise levels. Actively investing in growing others'' capabilities.', 1.0, 0.4, 'Lighthouse', 'Illuminating shared growth', 10),
('Work-Life Balance',  'work',    'Professional Growth',    'Setting and maintaining boundaries between professional and personal life. Disconnecting from work communications outside hours. Recognizing burnout signals and taking preventive action. Prioritizing personal health, relationships, and hobbies alongside career demands. Sustainable pace over heroic sprints.', 0.8, 0.8, 'Hammock', 'Restoring sacred balance', 11),
('Career Resilience',  'work',    'Professional Growth',    'Bouncing back from job loss, project failures, organizational restructuring. Ability to pivot skills and reinvent professionally after setbacks. Maintaining motivation and professional identity during periods of uncertainty. Learning from failure rather than being defined by it. Building a career that can weather industry disruption.', 1.0, 0.4, 'Phoenix', 'Stronger from the ashes', 12),
('Emotional Depth',    'private', 'Emotional Landscape',    'Capacity for deep vulnerability and trust in close relationships. Experiencing emotions with full intensity rather than surface-level. Willingness to sit with difficult feelings rather than numbing or avoiding. Rich inner emotional life that informs empathy and connection. Emotional warmth and genuine presence with others.', 0.4, 1.0, 'Deep Well', 'Feeling everything deeply', 13),
('Emotional Regulation','private','Emotional Landscape',    'Managing impulsive reactions, controlling anger, moderating anxiety without suppression. Maintaining composure during interpersonal conflict while still feeling the emotion. Processing frustration constructively rather than explosively or through withdrawal. Choosing responses rather than being hijacked by emotional reactivity.', 0.7, 1.0, 'Still Water', 'Calm when others break', 14),
('Sensitivity',        'private', 'Emotional Landscape',    'Heightened awareness of others'' emotional states, picking up on subtle social cues. Feeling others'' pain deeply, being moved by art, music, or stories. Strong empathic attunement that can be both a gift and a burden. Processing criticism or rejection intensely. Mirror-like emotional responsiveness to the environment.', 0.4, 1.0, 'Raw Nerve', 'Everything hits harder', 15),
('Attachment Security','private', 'Relational Style',       'Trusting without excessive anxiety, comfortable with both intimacy and independence. Low jealousy or possessiveness in relationships. Secure bonding that tolerates partner autonomy without triggering abandonment fears. Consistent emotional availability without clinging or avoidance patterns.', 0.3, 1.0, 'Safe Harbor', 'Safe to land on', 16),
('Intimacy Capacity',  'private', 'Relational Style',       'Openness to physical closeness, eye contact, vulnerable conversations. Sharing deeply personal thoughts and experiences with trusted others. Comfort with emotional and physical nakedness. Nurturing behavior in romantic and familial relationships. Creating safe spaces for mutual vulnerability.', 0.2, 1.0, 'Velvet', 'Depth without drowning', 17),
('Social Energy',      'private', 'Relational Style',       'Reward from social gatherings, parties, group activities versus need for solitary recharge. Preference for large social networks versus deep one-on-one connections. Energy levels after extended social interaction. Whether stimulation comes from people or from quiet reflection and solo pursuits.', 0.6, 1.0, 'Sparkler', 'Lighting every room', 18),
('Self-Awareness',     'private', 'Inner Drive',            'Accurate monitoring of own emotional states and behavioral patterns. Honest self-assessment without defensive distortion. Recognizing personal biases, triggers, and habitual reactions. Growth from personal feedback and self-reflection. Understanding the gap between intention and impact in relationships.', 0.6, 1.0, 'Mirror', 'Knowing without flinching', 19),
('Playfulness',        'private', 'Inner Drive',            'Spontaneity, humor, creative expression outside of work. Novelty seeking in hobbies, travel, exploration. Light-hearted social interaction without goal-oriented purpose. Ability to be silly, experiment, and play as an adult. Finding joy in the process rather than only in outcomes.', 0.3, 1.0, 'Kaleidoscope', 'Reality as playground', 20),
('Purpose & Meaning',  'private', 'Inner Drive',            'Contentment from value-aligned living, spiritual or philosophical practice, community contribution. Life satisfaction independent of external achievement. Finding meaning through caregiving, legacy building, generational connection. Sense of direction that transcends immediate goals and material success.', 0.5, 1.0, 'North Star', 'Meaning beyond self', 21),
('Stress Response',    'private', 'Resilience & Recovery',  'Pattern of activation during personal crises, grief, relationship conflict. Whether the default response is fight, flight, or freeze. How the body processes emotional distress from betrayal, loss, or loneliness. Speed and completeness of physiological return to baseline after acute stress.', 0.6, 1.0, 'Tripwire', 'Wired to detect danger', 22),
('Healing Capacity',   'private', 'Resilience & Recovery',  'Natural pain relief and emotional wound processing over time. Recovery after trauma, breakups, or major life transitions. Neuroplastic ability to reorganize after loss and build new meaning. Resilience against chronic grief and post-traumatic rumination. Converting painful experiences into wisdom rather than bitterness.', 0.3, 1.0, 'Salve', 'Healing from anything', 23),
('Inner Peace',        'private', 'Resilience & Recovery',  'Baseline calm, low resting anxiety, comfort with silence and stillness. Quality of sleep and circadian rhythm stability. Contentment without external stimulation or achievement validation. Ability to simply be without needing to do, produce, or perform. Groundedness that persists through external turbulence.', 0.4, 1.0, 'Zen Garden', 'Stillness others feel', 24)
ON CONFLICT (name) DO UPDATE SET
    archetype_name = EXCLUDED.archetype_name,
    archetype_essence = EXCLUDED.archetype_essence;

-- ═════════════════════════════════════
-- Dimension ↔ Chemical Affinity (~120 rows)
-- ═════════════════════════════════════

-- Ambition
INSERT INTO dimension_chemical_affinity (dimension_id, chemical_id, weight) SELECT d.id, c.id, 1.0 FROM dimension d, chemical c WHERE d.name='Ambition' AND c.key='dopamine' ON CONFLICT DO NOTHING;
INSERT INTO dimension_chemical_affinity (dimension_id, chemical_id, weight) SELECT d.id, c.id, 0.8 FROM dimension d, chemical c WHERE d.name='Ambition' AND c.key='testosterone' ON CONFLICT DO NOTHING;
INSERT INTO dimension_chemical_affinity (dimension_id, chemical_id, weight) SELECT d.id, c.id, 0.5 FROM dimension d, chemical c WHERE d.name='Ambition' AND c.key='norepinephrine' ON CONFLICT DO NOTHING;
INSERT INTO dimension_chemical_affinity (dimension_id, chemical_id, weight) SELECT d.id, c.id, 0.5 FROM dimension d, chemical c WHERE d.name='Ambition' AND c.key='orexin' ON CONFLICT DO NOTHING;
INSERT INTO dimension_chemical_affinity (dimension_id, chemical_id, weight) SELECT d.id, c.id, 0.4 FROM dimension d, chemical c WHERE d.name='Ambition' AND c.key='npy' ON CONFLICT DO NOTHING;
-- Risk Tolerance
INSERT INTO dimension_chemical_affinity (dimension_id, chemical_id, weight) SELECT d.id, c.id, 1.0 FROM dimension d, chemical c WHERE d.name='Risk Tolerance' AND c.key='norepinephrine' ON CONFLICT DO NOTHING;
INSERT INTO dimension_chemical_affinity (dimension_id, chemical_id, weight) SELECT d.id, c.id, 0.7 FROM dimension d, chemical c WHERE d.name='Risk Tolerance' AND c.key='endocannabinoid' ON CONFLICT DO NOTHING;
INSERT INTO dimension_chemical_affinity (dimension_id, chemical_id, weight) SELECT d.id, c.id, 0.6 FROM dimension d, chemical c WHERE d.name='Risk Tolerance' AND c.key='dopamine' ON CONFLICT DO NOTHING;
INSERT INTO dimension_chemical_affinity (dimension_id, chemical_id, weight) SELECT d.id, c.id, 0.5 FROM dimension d, chemical c WHERE d.name='Risk Tolerance' AND c.key='adrenaline' ON CONFLICT DO NOTHING;
INSERT INTO dimension_chemical_affinity (dimension_id, chemical_id, weight) SELECT d.id, c.id, 0.4 FROM dimension d, chemical c WHERE d.name='Risk Tolerance' AND c.key='testosterone' ON CONFLICT DO NOTHING;
-- Persistence
INSERT INTO dimension_chemical_affinity (dimension_id, chemical_id, weight) SELECT d.id, c.id, 1.0 FROM dimension d, chemical c WHERE d.name='Persistence' AND c.key='gaba' ON CONFLICT DO NOTHING;
INSERT INTO dimension_chemical_affinity (dimension_id, chemical_id, weight) SELECT d.id, c.id, 0.8 FROM dimension d, chemical c WHERE d.name='Persistence' AND c.key='serotonin' ON CONFLICT DO NOTHING;
INSERT INTO dimension_chemical_affinity (dimension_id, chemical_id, weight) SELECT d.id, c.id, 0.7 FROM dimension d, chemical c WHERE d.name='Persistence' AND c.key='npy' ON CONFLICT DO NOTHING;
INSERT INTO dimension_chemical_affinity (dimension_id, chemical_id, weight) SELECT d.id, c.id, 0.5 FROM dimension d, chemical c WHERE d.name='Persistence' AND c.key='enkephalins' ON CONFLICT DO NOTHING;
INSERT INTO dimension_chemical_affinity (dimension_id, chemical_id, weight) SELECT d.id, c.id, 0.4 FROM dimension d, chemical c WHERE d.name='Persistence' AND c.key='dhea' ON CONFLICT DO NOTHING;
-- Team Orientation
INSERT INTO dimension_chemical_affinity (dimension_id, chemical_id, weight) SELECT d.id, c.id, 1.0 FROM dimension d, chemical c WHERE d.name='Team Orientation' AND c.key='oxytocin' ON CONFLICT DO NOTHING;
INSERT INTO dimension_chemical_affinity (dimension_id, chemical_id, weight) SELECT d.id, c.id, 0.9 FROM dimension d, chemical c WHERE d.name='Team Orientation' AND c.key='oxytocin_h' ON CONFLICT DO NOTHING;
INSERT INTO dimension_chemical_affinity (dimension_id, chemical_id, weight) SELECT d.id, c.id, 0.7 FROM dimension d, chemical c WHERE d.name='Team Orientation' AND c.key='vasopressin' ON CONFLICT DO NOTHING;
INSERT INTO dimension_chemical_affinity (dimension_id, chemical_id, weight) SELECT d.id, c.id, 0.6 FROM dimension d, chemical c WHERE d.name='Team Orientation' AND c.key='prolactin' ON CONFLICT DO NOTHING;
INSERT INTO dimension_chemical_affinity (dimension_id, chemical_id, weight) SELECT d.id, c.id, 0.4 FROM dimension d, chemical c WHERE d.name='Team Orientation' AND c.key='serotonin' ON CONFLICT DO NOTHING;
-- Strategic Thinking
INSERT INTO dimension_chemical_affinity (dimension_id, chemical_id, weight) SELECT d.id, c.id, 1.0 FROM dimension d, chemical c WHERE d.name='Strategic Thinking' AND c.key='acetylcholine' ON CONFLICT DO NOTHING;
INSERT INTO dimension_chemical_affinity (dimension_id, chemical_id, weight) SELECT d.id, c.id, 0.8 FROM dimension d, chemical c WHERE d.name='Strategic Thinking' AND c.key='glutamate' ON CONFLICT DO NOTHING;
INSERT INTO dimension_chemical_affinity (dimension_id, chemical_id, weight) SELECT d.id, c.id, 0.6 FROM dimension d, chemical c WHERE d.name='Strategic Thinking' AND c.key='thyroid' ON CONFLICT DO NOTHING;
INSERT INTO dimension_chemical_affinity (dimension_id, chemical_id, weight) SELECT d.id, c.id, 0.5 FROM dimension d, chemical c WHERE d.name='Strategic Thinking' AND c.key='bdnf' ON CONFLICT DO NOTHING;
INSERT INTO dimension_chemical_affinity (dimension_id, chemical_id, weight) SELECT d.id, c.id, 0.4 FROM dimension d, chemical c WHERE d.name='Strategic Thinking' AND c.key='serotonin' ON CONFLICT DO NOTHING;
-- Stress Capacity
INSERT INTO dimension_chemical_affinity (dimension_id, chemical_id, weight) SELECT d.id, c.id, 1.0 FROM dimension d, chemical c WHERE d.name='Stress Capacity' AND c.key='cortisol' ON CONFLICT DO NOTHING;
INSERT INTO dimension_chemical_affinity (dimension_id, chemical_id, weight) SELECT d.id, c.id, 0.9 FROM dimension d, chemical c WHERE d.name='Stress Capacity' AND c.key='dhea' ON CONFLICT DO NOTHING;
INSERT INTO dimension_chemical_affinity (dimension_id, chemical_id, weight) SELECT d.id, c.id, 0.7 FROM dimension d, chemical c WHERE d.name='Stress Capacity' AND c.key='adrenaline' ON CONFLICT DO NOTHING;
INSERT INTO dimension_chemical_affinity (dimension_id, chemical_id, weight) SELECT d.id, c.id, 0.5 FROM dimension d, chemical c WHERE d.name='Stress Capacity' AND c.key='gaba' ON CONFLICT DO NOTHING;
INSERT INTO dimension_chemical_affinity (dimension_id, chemical_id, weight) SELECT d.id, c.id, 0.5 FROM dimension d, chemical c WHERE d.name='Stress Capacity' AND c.key='npy' ON CONFLICT DO NOTHING;
-- Competitive Drive
INSERT INTO dimension_chemical_affinity (dimension_id, chemical_id, weight) SELECT d.id, c.id, 1.0 FROM dimension d, chemical c WHERE d.name='Competitive Drive' AND c.key='testosterone' ON CONFLICT DO NOTHING;
INSERT INTO dimension_chemical_affinity (dimension_id, chemical_id, weight) SELECT d.id, c.id, 0.7 FROM dimension d, chemical c WHERE d.name='Competitive Drive' AND c.key='dopamine' ON CONFLICT DO NOTHING;
INSERT INTO dimension_chemical_affinity (dimension_id, chemical_id, weight) SELECT d.id, c.id, 0.6 FROM dimension d, chemical c WHERE d.name='Competitive Drive' AND c.key='norepinephrine' ON CONFLICT DO NOTHING;
INSERT INTO dimension_chemical_affinity (dimension_id, chemical_id, weight) SELECT d.id, c.id, 0.5 FROM dimension d, chemical c WHERE d.name='Competitive Drive' AND c.key='adrenaline' ON CONFLICT DO NOTHING;
INSERT INTO dimension_chemical_affinity (dimension_id, chemical_id, weight) SELECT d.id, c.id, 0.4 FROM dimension d, chemical c WHERE d.name='Competitive Drive' AND c.key='orexin' ON CONFLICT DO NOTHING;
-- Context Switching
INSERT INTO dimension_chemical_affinity (dimension_id, chemical_id, weight) SELECT d.id, c.id, 1.0 FROM dimension d, chemical c WHERE d.name='Context Switching' AND c.key='acetylcholine' ON CONFLICT DO NOTHING;
INSERT INTO dimension_chemical_affinity (dimension_id, chemical_id, weight) SELECT d.id, c.id, 0.6 FROM dimension d, chemical c WHERE d.name='Context Switching' AND c.key='dopamine' ON CONFLICT DO NOTHING;
INSERT INTO dimension_chemical_affinity (dimension_id, chemical_id, weight) SELECT d.id, c.id, 0.6 FROM dimension d, chemical c WHERE d.name='Context Switching' AND c.key='norepinephrine' ON CONFLICT DO NOTHING;
INSERT INTO dimension_chemical_affinity (dimension_id, chemical_id, weight) SELECT d.id, c.id, 0.4 FROM dimension d, chemical c WHERE d.name='Context Switching' AND c.key='thyroid' ON CONFLICT DO NOTHING;
INSERT INTO dimension_chemical_affinity (dimension_id, chemical_id, weight) SELECT d.id, c.id, 0.4 FROM dimension d, chemical c WHERE d.name='Context Switching' AND c.key='orexin' ON CONFLICT DO NOTHING;
-- Problem Solving
INSERT INTO dimension_chemical_affinity (dimension_id, chemical_id, weight) SELECT d.id, c.id, 1.0 FROM dimension d, chemical c WHERE d.name='Problem Solving' AND c.key='glutamate' ON CONFLICT DO NOTHING;
INSERT INTO dimension_chemical_affinity (dimension_id, chemical_id, weight) SELECT d.id, c.id, 0.8 FROM dimension d, chemical c WHERE d.name='Problem Solving' AND c.key='bdnf' ON CONFLICT DO NOTHING;
INSERT INTO dimension_chemical_affinity (dimension_id, chemical_id, weight) SELECT d.id, c.id, 0.6 FROM dimension d, chemical c WHERE d.name='Problem Solving' AND c.key='endocannabinoid' ON CONFLICT DO NOTHING;
INSERT INTO dimension_chemical_affinity (dimension_id, chemical_id, weight) SELECT d.id, c.id, 0.5 FROM dimension d, chemical c WHERE d.name='Problem Solving' AND c.key='acetylcholine' ON CONFLICT DO NOTHING;
INSERT INTO dimension_chemical_affinity (dimension_id, chemical_id, weight) SELECT d.id, c.id, 0.4 FROM dimension d, chemical c WHERE d.name='Problem Solving' AND c.key='dopamine' ON CONFLICT DO NOTHING;
-- Knowledge Transfer
INSERT INTO dimension_chemical_affinity (dimension_id, chemical_id, weight) SELECT d.id, c.id, 1.0 FROM dimension d, chemical c WHERE d.name='Knowledge Transfer' AND c.key='bdnf' ON CONFLICT DO NOTHING;
INSERT INTO dimension_chemical_affinity (dimension_id, chemical_id, weight) SELECT d.id, c.id, 0.7 FROM dimension d, chemical c WHERE d.name='Knowledge Transfer' AND c.key='glutamate' ON CONFLICT DO NOTHING;
INSERT INTO dimension_chemical_affinity (dimension_id, chemical_id, weight) SELECT d.id, c.id, 0.6 FROM dimension d, chemical c WHERE d.name='Knowledge Transfer' AND c.key='acetylcholine' ON CONFLICT DO NOTHING;
INSERT INTO dimension_chemical_affinity (dimension_id, chemical_id, weight) SELECT d.id, c.id, 0.5 FROM dimension d, chemical c WHERE d.name='Knowledge Transfer' AND c.key='oxytocin' ON CONFLICT DO NOTHING;
INSERT INTO dimension_chemical_affinity (dimension_id, chemical_id, weight) SELECT d.id, c.id, 0.4 FROM dimension d, chemical c WHERE d.name='Knowledge Transfer' AND c.key='prolactin' ON CONFLICT DO NOTHING;
-- Work-Life Balance
INSERT INTO dimension_chemical_affinity (dimension_id, chemical_id, weight) SELECT d.id, c.id, 1.0 FROM dimension d, chemical c WHERE d.name='Work-Life Balance' AND c.key='melatonin' ON CONFLICT DO NOTHING;
INSERT INTO dimension_chemical_affinity (dimension_id, chemical_id, weight) SELECT d.id, c.id, 0.8 FROM dimension d, chemical c WHERE d.name='Work-Life Balance' AND c.key='gaba' ON CONFLICT DO NOTHING;
INSERT INTO dimension_chemical_affinity (dimension_id, chemical_id, weight) SELECT d.id, c.id, 0.6 FROM dimension d, chemical c WHERE d.name='Work-Life Balance' AND c.key='progesterone' ON CONFLICT DO NOTHING;
INSERT INTO dimension_chemical_affinity (dimension_id, chemical_id, weight) SELECT d.id, c.id, 0.5 FROM dimension d, chemical c WHERE d.name='Work-Life Balance' AND c.key='serotonin' ON CONFLICT DO NOTHING;
INSERT INTO dimension_chemical_affinity (dimension_id, chemical_id, weight) SELECT d.id, c.id, 0.4 FROM dimension d, chemical c WHERE d.name='Work-Life Balance' AND c.key='endocannabinoid' ON CONFLICT DO NOTHING;
-- Career Resilience
INSERT INTO dimension_chemical_affinity (dimension_id, chemical_id, weight) SELECT d.id, c.id, 1.0 FROM dimension d, chemical c WHERE d.name='Career Resilience' AND c.key='dhea' ON CONFLICT DO NOTHING;
INSERT INTO dimension_chemical_affinity (dimension_id, chemical_id, weight) SELECT d.id, c.id, 0.8 FROM dimension d, chemical c WHERE d.name='Career Resilience' AND c.key='enkephalins' ON CONFLICT DO NOTHING;
INSERT INTO dimension_chemical_affinity (dimension_id, chemical_id, weight) SELECT d.id, c.id, 0.7 FROM dimension d, chemical c WHERE d.name='Career Resilience' AND c.key='orexin' ON CONFLICT DO NOTHING;
INSERT INTO dimension_chemical_affinity (dimension_id, chemical_id, weight) SELECT d.id, c.id, 0.6 FROM dimension d, chemical c WHERE d.name='Career Resilience' AND c.key='npy' ON CONFLICT DO NOTHING;
INSERT INTO dimension_chemical_affinity (dimension_id, chemical_id, weight) SELECT d.id, c.id, 0.4 FROM dimension d, chemical c WHERE d.name='Career Resilience' AND c.key='bdnf' ON CONFLICT DO NOTHING;
-- Emotional Depth
INSERT INTO dimension_chemical_affinity (dimension_id, chemical_id, weight) SELECT d.id, c.id, 1.0 FROM dimension d, chemical c WHERE d.name='Emotional Depth' AND c.key='oxytocin' ON CONFLICT DO NOTHING;
INSERT INTO dimension_chemical_affinity (dimension_id, chemical_id, weight) SELECT d.id, c.id, 0.7 FROM dimension d, chemical c WHERE d.name='Emotional Depth' AND c.key='vasopressin' ON CONFLICT DO NOTHING;
INSERT INTO dimension_chemical_affinity (dimension_id, chemical_id, weight) SELECT d.id, c.id, 0.7 FROM dimension d, chemical c WHERE d.name='Emotional Depth' AND c.key='endorphins' ON CONFLICT DO NOTHING;
INSERT INTO dimension_chemical_affinity (dimension_id, chemical_id, weight) SELECT d.id, c.id, 0.5 FROM dimension d, chemical c WHERE d.name='Emotional Depth' AND c.key='serotonin' ON CONFLICT DO NOTHING;
INSERT INTO dimension_chemical_affinity (dimension_id, chemical_id, weight) SELECT d.id, c.id, 0.4 FROM dimension d, chemical c WHERE d.name='Emotional Depth' AND c.key='estradiol' ON CONFLICT DO NOTHING;
-- Emotional Regulation
INSERT INTO dimension_chemical_affinity (dimension_id, chemical_id, weight) SELECT d.id, c.id, 1.0 FROM dimension d, chemical c WHERE d.name='Emotional Regulation' AND c.key='gaba' ON CONFLICT DO NOTHING;
INSERT INTO dimension_chemical_affinity (dimension_id, chemical_id, weight) SELECT d.id, c.id, 0.9 FROM dimension d, chemical c WHERE d.name='Emotional Regulation' AND c.key='serotonin' ON CONFLICT DO NOTHING;
INSERT INTO dimension_chemical_affinity (dimension_id, chemical_id, weight) SELECT d.id, c.id, 0.5 FROM dimension d, chemical c WHERE d.name='Emotional Regulation' AND c.key='cortisol' ON CONFLICT DO NOTHING;
INSERT INTO dimension_chemical_affinity (dimension_id, chemical_id, weight) SELECT d.id, c.id, 0.4 FROM dimension d, chemical c WHERE d.name='Emotional Regulation' AND c.key='progesterone' ON CONFLICT DO NOTHING;
INSERT INTO dimension_chemical_affinity (dimension_id, chemical_id, weight) SELECT d.id, c.id, 0.4 FROM dimension d, chemical c WHERE d.name='Emotional Regulation' AND c.key='endocannabinoid' ON CONFLICT DO NOTHING;
-- Sensitivity
INSERT INTO dimension_chemical_affinity (dimension_id, chemical_id, weight) SELECT d.id, c.id, 1.0 FROM dimension d, chemical c WHERE d.name='Sensitivity' AND c.key='substance_p' ON CONFLICT DO NOTHING;
INSERT INTO dimension_chemical_affinity (dimension_id, chemical_id, weight) SELECT d.id, c.id, 0.8 FROM dimension d, chemical c WHERE d.name='Sensitivity' AND c.key='dynorphin' ON CONFLICT DO NOTHING;
INSERT INTO dimension_chemical_affinity (dimension_id, chemical_id, weight) SELECT d.id, c.id, 0.6 FROM dimension d, chemical c WHERE d.name='Sensitivity' AND c.key='oxytocin' ON CONFLICT DO NOTHING;
INSERT INTO dimension_chemical_affinity (dimension_id, chemical_id, weight) SELECT d.id, c.id, 0.5 FROM dimension d, chemical c WHERE d.name='Sensitivity' AND c.key='crh' ON CONFLICT DO NOTHING;
INSERT INTO dimension_chemical_affinity (dimension_id, chemical_id, weight) SELECT d.id, c.id, 0.4 FROM dimension d, chemical c WHERE d.name='Sensitivity' AND c.key='estradiol' ON CONFLICT DO NOTHING;
-- Attachment Security
INSERT INTO dimension_chemical_affinity (dimension_id, chemical_id, weight) SELECT d.id, c.id, 1.0 FROM dimension d, chemical c WHERE d.name='Attachment Security' AND c.key='oxytocin' ON CONFLICT DO NOTHING;
INSERT INTO dimension_chemical_affinity (dimension_id, chemical_id, weight) SELECT d.id, c.id, 0.7 FROM dimension d, chemical c WHERE d.name='Attachment Security' AND c.key='vasopressin' ON CONFLICT DO NOTHING;
INSERT INTO dimension_chemical_affinity (dimension_id, chemical_id, weight) SELECT d.id, c.id, 0.6 FROM dimension d, chemical c WHERE d.name='Attachment Security' AND c.key='endorphins' ON CONFLICT DO NOTHING;
INSERT INTO dimension_chemical_affinity (dimension_id, chemical_id, weight) SELECT d.id, c.id, 0.5 FROM dimension d, chemical c WHERE d.name='Attachment Security' AND c.key='serotonin' ON CONFLICT DO NOTHING;
INSERT INTO dimension_chemical_affinity (dimension_id, chemical_id, weight) SELECT d.id, c.id, 0.4 FROM dimension d, chemical c WHERE d.name='Attachment Security' AND c.key='gaba' ON CONFLICT DO NOTHING;
-- Intimacy Capacity
INSERT INTO dimension_chemical_affinity (dimension_id, chemical_id, weight) SELECT d.id, c.id, 1.0 FROM dimension d, chemical c WHERE d.name='Intimacy Capacity' AND c.key='oxytocin' ON CONFLICT DO NOTHING;
INSERT INTO dimension_chemical_affinity (dimension_id, chemical_id, weight) SELECT d.id, c.id, 0.8 FROM dimension d, chemical c WHERE d.name='Intimacy Capacity' AND c.key='prolactin' ON CONFLICT DO NOTHING;
INSERT INTO dimension_chemical_affinity (dimension_id, chemical_id, weight) SELECT d.id, c.id, 0.7 FROM dimension d, chemical c WHERE d.name='Intimacy Capacity' AND c.key='estradiol' ON CONFLICT DO NOTHING;
INSERT INTO dimension_chemical_affinity (dimension_id, chemical_id, weight) SELECT d.id, c.id, 0.5 FROM dimension d, chemical c WHERE d.name='Intimacy Capacity' AND c.key='vasopressin' ON CONFLICT DO NOTHING;
INSERT INTO dimension_chemical_affinity (dimension_id, chemical_id, weight) SELECT d.id, c.id, 0.4 FROM dimension d, chemical c WHERE d.name='Intimacy Capacity' AND c.key='endorphins' ON CONFLICT DO NOTHING;
-- Social Energy
INSERT INTO dimension_chemical_affinity (dimension_id, chemical_id, weight) SELECT d.id, c.id, 1.0 FROM dimension d, chemical c WHERE d.name='Social Energy' AND c.key='dopamine' ON CONFLICT DO NOTHING;
INSERT INTO dimension_chemical_affinity (dimension_id, chemical_id, weight) SELECT d.id, c.id, 0.8 FROM dimension d, chemical c WHERE d.name='Social Energy' AND c.key='orexin' ON CONFLICT DO NOTHING;
INSERT INTO dimension_chemical_affinity (dimension_id, chemical_id, weight) SELECT d.id, c.id, 0.5 FROM dimension d, chemical c WHERE d.name='Social Energy' AND c.key='endorphins' ON CONFLICT DO NOTHING;
INSERT INTO dimension_chemical_affinity (dimension_id, chemical_id, weight) SELECT d.id, c.id, 0.5 FROM dimension d, chemical c WHERE d.name='Social Energy' AND c.key='melatonin' ON CONFLICT DO NOTHING;
INSERT INTO dimension_chemical_affinity (dimension_id, chemical_id, weight) SELECT d.id, c.id, 0.4 FROM dimension d, chemical c WHERE d.name='Social Energy' AND c.key='serotonin' ON CONFLICT DO NOTHING;
-- Self-Awareness
INSERT INTO dimension_chemical_affinity (dimension_id, chemical_id, weight) SELECT d.id, c.id, 1.0 FROM dimension d, chemical c WHERE d.name='Self-Awareness' AND c.key='acetylcholine' ON CONFLICT DO NOTHING;
INSERT INTO dimension_chemical_affinity (dimension_id, chemical_id, weight) SELECT d.id, c.id, 0.8 FROM dimension d, chemical c WHERE d.name='Self-Awareness' AND c.key='serotonin' ON CONFLICT DO NOTHING;
INSERT INTO dimension_chemical_affinity (dimension_id, chemical_id, weight) SELECT d.id, c.id, 0.6 FROM dimension d, chemical c WHERE d.name='Self-Awareness' AND c.key='bdnf' ON CONFLICT DO NOTHING;
INSERT INTO dimension_chemical_affinity (dimension_id, chemical_id, weight) SELECT d.id, c.id, 0.4 FROM dimension d, chemical c WHERE d.name='Self-Awareness' AND c.key='gaba' ON CONFLICT DO NOTHING;
INSERT INTO dimension_chemical_affinity (dimension_id, chemical_id, weight) SELECT d.id, c.id, 0.3 FROM dimension d, chemical c WHERE d.name='Self-Awareness' AND c.key='endocannabinoid' ON CONFLICT DO NOTHING;
-- Playfulness
INSERT INTO dimension_chemical_affinity (dimension_id, chemical_id, weight) SELECT d.id, c.id, 1.0 FROM dimension d, chemical c WHERE d.name='Playfulness' AND c.key='endocannabinoid' ON CONFLICT DO NOTHING;
INSERT INTO dimension_chemical_affinity (dimension_id, chemical_id, weight) SELECT d.id, c.id, 0.8 FROM dimension d, chemical c WHERE d.name='Playfulness' AND c.key='dopamine' ON CONFLICT DO NOTHING;
INSERT INTO dimension_chemical_affinity (dimension_id, chemical_id, weight) SELECT d.id, c.id, 0.7 FROM dimension d, chemical c WHERE d.name='Playfulness' AND c.key='endorphins' ON CONFLICT DO NOTHING;
INSERT INTO dimension_chemical_affinity (dimension_id, chemical_id, weight) SELECT d.id, c.id, 0.3 FROM dimension d, chemical c WHERE d.name='Playfulness' AND c.key='serotonin' ON CONFLICT DO NOTHING;
INSERT INTO dimension_chemical_affinity (dimension_id, chemical_id, weight) SELECT d.id, c.id, 0.3 FROM dimension d, chemical c WHERE d.name='Playfulness' AND c.key='orexin' ON CONFLICT DO NOTHING;
-- Purpose & Meaning
INSERT INTO dimension_chemical_affinity (dimension_id, chemical_id, weight) SELECT d.id, c.id, 1.0 FROM dimension d, chemical c WHERE d.name='Purpose & Meaning' AND c.key='serotonin' ON CONFLICT DO NOTHING;
INSERT INTO dimension_chemical_affinity (dimension_id, chemical_id, weight) SELECT d.id, c.id, 0.8 FROM dimension d, chemical c WHERE d.name='Purpose & Meaning' AND c.key='npy' ON CONFLICT DO NOTHING;
INSERT INTO dimension_chemical_affinity (dimension_id, chemical_id, weight) SELECT d.id, c.id, 0.6 FROM dimension d, chemical c WHERE d.name='Purpose & Meaning' AND c.key='oxytocin' ON CONFLICT DO NOTHING;
INSERT INTO dimension_chemical_affinity (dimension_id, chemical_id, weight) SELECT d.id, c.id, 0.4 FROM dimension d, chemical c WHERE d.name='Purpose & Meaning' AND c.key='endorphins' ON CONFLICT DO NOTHING;
INSERT INTO dimension_chemical_affinity (dimension_id, chemical_id, weight) SELECT d.id, c.id, 0.3 FROM dimension d, chemical c WHERE d.name='Purpose & Meaning' AND c.key='bdnf' ON CONFLICT DO NOTHING;
-- Stress Response
INSERT INTO dimension_chemical_affinity (dimension_id, chemical_id, weight) SELECT d.id, c.id, 1.0 FROM dimension d, chemical c WHERE d.name='Stress Response' AND c.key='crh' ON CONFLICT DO NOTHING;
INSERT INTO dimension_chemical_affinity (dimension_id, chemical_id, weight) SELECT d.id, c.id, 0.8 FROM dimension d, chemical c WHERE d.name='Stress Response' AND c.key='cortisol' ON CONFLICT DO NOTHING;
INSERT INTO dimension_chemical_affinity (dimension_id, chemical_id, weight) SELECT d.id, c.id, 0.7 FROM dimension d, chemical c WHERE d.name='Stress Response' AND c.key='adrenaline' ON CONFLICT DO NOTHING;
INSERT INTO dimension_chemical_affinity (dimension_id, chemical_id, weight) SELECT d.id, c.id, 0.6 FROM dimension d, chemical c WHERE d.name='Stress Response' AND c.key='substance_p' ON CONFLICT DO NOTHING;
INSERT INTO dimension_chemical_affinity (dimension_id, chemical_id, weight) SELECT d.id, c.id, 0.4 FROM dimension d, chemical c WHERE d.name='Stress Response' AND c.key='norepinephrine' ON CONFLICT DO NOTHING;
-- Healing Capacity
INSERT INTO dimension_chemical_affinity (dimension_id, chemical_id, weight) SELECT d.id, c.id, 1.0 FROM dimension d, chemical c WHERE d.name='Healing Capacity' AND c.key='enkephalins' ON CONFLICT DO NOTHING;
INSERT INTO dimension_chemical_affinity (dimension_id, chemical_id, weight) SELECT d.id, c.id, 0.8 FROM dimension d, chemical c WHERE d.name='Healing Capacity' AND c.key='endorphins' ON CONFLICT DO NOTHING;
INSERT INTO dimension_chemical_affinity (dimension_id, chemical_id, weight) SELECT d.id, c.id, 0.7 FROM dimension d, chemical c WHERE d.name='Healing Capacity' AND c.key='bdnf' ON CONFLICT DO NOTHING;
INSERT INTO dimension_chemical_affinity (dimension_id, chemical_id, weight) SELECT d.id, c.id, 0.6 FROM dimension d, chemical c WHERE d.name='Healing Capacity' AND c.key='dhea' ON CONFLICT DO NOTHING;
INSERT INTO dimension_chemical_affinity (dimension_id, chemical_id, weight) SELECT d.id, c.id, 0.4 FROM dimension d, chemical c WHERE d.name='Healing Capacity' AND c.key='npy' ON CONFLICT DO NOTHING;
-- Inner Peace
INSERT INTO dimension_chemical_affinity (dimension_id, chemical_id, weight) SELECT d.id, c.id, 1.0 FROM dimension d, chemical c WHERE d.name='Inner Peace' AND c.key='gaba' ON CONFLICT DO NOTHING;
INSERT INTO dimension_chemical_affinity (dimension_id, chemical_id, weight) SELECT d.id, c.id, 0.9 FROM dimension d, chemical c WHERE d.name='Inner Peace' AND c.key='melatonin' ON CONFLICT DO NOTHING;
INSERT INTO dimension_chemical_affinity (dimension_id, chemical_id, weight) SELECT d.id, c.id, 0.7 FROM dimension d, chemical c WHERE d.name='Inner Peace' AND c.key='endocannabinoid' ON CONFLICT DO NOTHING;
INSERT INTO dimension_chemical_affinity (dimension_id, chemical_id, weight) SELECT d.id, c.id, 0.5 FROM dimension d, chemical c WHERE d.name='Inner Peace' AND c.key='serotonin' ON CONFLICT DO NOTHING;
INSERT INTO dimension_chemical_affinity (dimension_id, chemical_id, weight) SELECT d.id, c.id, 0.4 FROM dimension d, chemical c WHERE d.name='Inner Peace' AND c.key='progesterone' ON CONFLICT DO NOTHING;

-- ═════════════════════════════════════
-- Chemical Interactions (~80 rows)
-- mod_factor: +1.0 agonist, 0.0 stabilizer, -1.0 antagonist
-- Extracted from agent template Interactions: lines
-- ═════════════════════════════════════

-- Dopamine interactions
INSERT INTO chemical_interaction (source_chemical_id, target_chemical_id, mod_factor) SELECT s.id, t.id, 0.85 FROM chemical s, chemical t WHERE s.key='dopamine' AND t.key='norepinephrine' ON CONFLICT DO NOTHING;
INSERT INTO chemical_interaction (source_chemical_id, target_chemical_id, mod_factor) SELECT s.id, t.id, 0.45 FROM chemical s, chemical t WHERE s.key='dopamine' AND t.key='glutamate' ON CONFLICT DO NOTHING;
INSERT INTO chemical_interaction (source_chemical_id, target_chemical_id, mod_factor) SELECT s.id, t.id, -0.55 FROM chemical s, chemical t WHERE s.key='serotonin' AND t.key='dopamine' ON CONFLICT DO NOTHING;
INSERT INTO chemical_interaction (source_chemical_id, target_chemical_id, mod_factor) SELECT s.id, t.id, -0.80 FROM chemical s, chemical t WHERE s.key='dynorphin' AND t.key='dopamine' ON CONFLICT DO NOTHING;
INSERT INTO chemical_interaction (source_chemical_id, target_chemical_id, mod_factor) SELECT s.id, t.id, -0.20 FROM chemical s, chemical t WHERE s.key='cortisol' AND t.key='dopamine' ON CONFLICT DO NOTHING;

-- Serotonin interactions
INSERT INTO chemical_interaction (source_chemical_id, target_chemical_id, mod_factor) SELECT s.id, t.id, 0.75 FROM chemical s, chemical t WHERE s.key='estradiol' AND t.key='serotonin' ON CONFLICT DO NOTHING;
INSERT INTO chemical_interaction (source_chemical_id, target_chemical_id, mod_factor) SELECT s.id, t.id, -0.75 FROM chemical s, chemical t WHERE s.key='cortisol' AND t.key='serotonin' ON CONFLICT DO NOTHING;

-- Norepinephrine interactions
INSERT INTO chemical_interaction (source_chemical_id, target_chemical_id, mod_factor) SELECT s.id, t.id, 0.60 FROM chemical s, chemical t WHERE s.key='cortisol' AND t.key='norepinephrine' ON CONFLICT DO NOTHING;
INSERT INTO chemical_interaction (source_chemical_id, target_chemical_id, mod_factor) SELECT s.id, t.id, 0.75 FROM chemical s, chemical t WHERE s.key='crh' AND t.key='norepinephrine' ON CONFLICT DO NOTHING;
INSERT INTO chemical_interaction (source_chemical_id, target_chemical_id, mod_factor) SELECT s.id, t.id, -0.65 FROM chemical s, chemical t WHERE s.key='gaba' AND t.key='norepinephrine' ON CONFLICT DO NOTHING;
INSERT INTO chemical_interaction (source_chemical_id, target_chemical_id, mod_factor) SELECT s.id, t.id, -0.50 FROM chemical s, chemical t WHERE s.key='endocannabinoid' AND t.key='norepinephrine' ON CONFLICT DO NOTHING;

-- GABA interactions
INSERT INTO chemical_interaction (source_chemical_id, target_chemical_id, mod_factor) SELECT s.id, t.id, -0.85 FROM chemical s, chemical t WHERE s.key='gaba' AND t.key='glutamate' ON CONFLICT DO NOTHING;
INSERT INTO chemical_interaction (source_chemical_id, target_chemical_id, mod_factor) SELECT s.id, t.id, -0.45 FROM chemical s, chemical t WHERE s.key='endocannabinoid' AND t.key='gaba' ON CONFLICT DO NOTHING;
INSERT INTO chemical_interaction (source_chemical_id, target_chemical_id, mod_factor) SELECT s.id, t.id, 0.80 FROM chemical s, chemical t WHERE s.key='progesterone' AND t.key='gaba' ON CONFLICT DO NOTHING;

-- Acetylcholine interactions
INSERT INTO chemical_interaction (source_chemical_id, target_chemical_id, mod_factor) SELECT s.id, t.id, 0.55 FROM chemical s, chemical t WHERE s.key='acetylcholine' AND t.key='dopamine' ON CONFLICT DO NOTHING;
INSERT INTO chemical_interaction (source_chemical_id, target_chemical_id, mod_factor) SELECT s.id, t.id, 0.50 FROM chemical s, chemical t WHERE s.key='acetylcholine' AND t.key='glutamate' ON CONFLICT DO NOTHING;
INSERT INTO chemical_interaction (source_chemical_id, target_chemical_id, mod_factor) SELECT s.id, t.id, -0.15 FROM chemical s, chemical t WHERE s.key='cortisol' AND t.key='acetylcholine' ON CONFLICT DO NOTHING;

-- Endocannabinoid interactions
INSERT INTO chemical_interaction (source_chemical_id, target_chemical_id, mod_factor) SELECT s.id, t.id, -0.40 FROM chemical s, chemical t WHERE s.key='endocannabinoid' AND t.key='glutamate' ON CONFLICT DO NOTHING;
INSERT INTO chemical_interaction (source_chemical_id, target_chemical_id, mod_factor) SELECT s.id, t.id, 0.50 FROM chemical s, chemical t WHERE s.key='oxytocin' AND t.key='endocannabinoid' ON CONFLICT DO NOTHING;

-- Glutamate interactions
INSERT INTO chemical_interaction (source_chemical_id, target_chemical_id, mod_factor) SELECT s.id, t.id, 0.75 FROM chemical s, chemical t WHERE s.key='glutamate' AND t.key='bdnf' ON CONFLICT DO NOTHING;
INSERT INTO chemical_interaction (source_chemical_id, target_chemical_id, mod_factor) SELECT s.id, t.id, 0.55 FROM chemical s, chemical t WHERE s.key='glutamate' AND t.key='dopamine' ON CONFLICT DO NOTHING;

-- Cortisol interactions
INSERT INTO chemical_interaction (source_chemical_id, target_chemical_id, mod_factor) SELECT s.id, t.id, -0.70 FROM chemical s, chemical t WHERE s.key='cortisol' AND t.key='testosterone' ON CONFLICT DO NOTHING;
INSERT INTO chemical_interaction (source_chemical_id, target_chemical_id, mod_factor) SELECT s.id, t.id, -0.60 FROM chemical s, chemical t WHERE s.key='cortisol' AND t.key='oxytocin' ON CONFLICT DO NOTHING;
INSERT INTO chemical_interaction (source_chemical_id, target_chemical_id, mod_factor) SELECT s.id, t.id, -0.80 FROM chemical s, chemical t WHERE s.key='cortisol' AND t.key='bdnf' ON CONFLICT DO NOTHING;
INSERT INTO chemical_interaction (source_chemical_id, target_chemical_id, mod_factor) SELECT s.id, t.id, 0.70 FROM chemical s, chemical t WHERE s.key='cortisol' AND t.key='crh' ON CONFLICT DO NOTHING;
INSERT INTO chemical_interaction (source_chemical_id, target_chemical_id, mod_factor) SELECT s.id, t.id, -0.40 FROM chemical s, chemical t WHERE s.key='dhea' AND t.key='cortisol' ON CONFLICT DO NOTHING;

-- Testosterone interactions
INSERT INTO chemical_interaction (source_chemical_id, target_chemical_id, mod_factor) SELECT s.id, t.id, 0.55 FROM chemical s, chemical t WHERE s.key='testosterone' AND t.key='dopamine' ON CONFLICT DO NOTHING;
INSERT INTO chemical_interaction (source_chemical_id, target_chemical_id, mod_factor) SELECT s.id, t.id, 0.70 FROM chemical s, chemical t WHERE s.key='testosterone' AND t.key='vasopressin' ON CONFLICT DO NOTHING;

-- Estradiol interactions
INSERT INTO chemical_interaction (source_chemical_id, target_chemical_id, mod_factor) SELECT s.id, t.id, 0.50 FROM chemical s, chemical t WHERE s.key='estradiol' AND t.key='dopamine' ON CONFLICT DO NOTHING;
INSERT INTO chemical_interaction (source_chemical_id, target_chemical_id, mod_factor) SELECT s.id, t.id, 0.70 FROM chemical s, chemical t WHERE s.key='estradiol' AND t.key='bdnf' ON CONFLICT DO NOTHING;

-- Progesterone interactions (GABA already covered)
INSERT INTO chemical_interaction (source_chemical_id, target_chemical_id, mod_factor) SELECT s.id, t.id, -0.45 FROM chemical s, chemical t WHERE s.key='cortisol' AND t.key='progesterone' ON CONFLICT DO NOTHING;
INSERT INTO chemical_interaction (source_chemical_id, target_chemical_id, mod_factor) SELECT s.id, t.id, 0.55 FROM chemical s, chemical t WHERE s.key='estradiol' AND t.key='progesterone' ON CONFLICT DO NOTHING;

-- Thyroid interactions
INSERT INTO chemical_interaction (source_chemical_id, target_chemical_id, mod_factor) SELECT s.id, t.id, -0.55 FROM chemical s, chemical t WHERE s.key='cortisol' AND t.key='thyroid' ON CONFLICT DO NOTHING;

-- Adrenaline interactions
INSERT INTO chemical_interaction (source_chemical_id, target_chemical_id, mod_factor) SELECT s.id, t.id, 0.80 FROM chemical s, chemical t WHERE s.key='cortisol' AND t.key='adrenaline' ON CONFLICT DO NOTHING;
INSERT INTO chemical_interaction (source_chemical_id, target_chemical_id, mod_factor) SELECT s.id, t.id, 0.80 FROM chemical s, chemical t WHERE s.key='adrenaline' AND t.key='norepinephrine' ON CONFLICT DO NOTHING;

-- Melatonin interactions
INSERT INTO chemical_interaction (source_chemical_id, target_chemical_id, mod_factor) SELECT s.id, t.id, 0.90 FROM chemical s, chemical t WHERE s.key='serotonin' AND t.key='melatonin' ON CONFLICT DO NOTHING;
INSERT INTO chemical_interaction (source_chemical_id, target_chemical_id, mod_factor) SELECT s.id, t.id, -0.50 FROM chemical s, chemical t WHERE s.key='cortisol' AND t.key='melatonin' ON CONFLICT DO NOTHING;
INSERT INTO chemical_interaction (source_chemical_id, target_chemical_id, mod_factor) SELECT s.id, t.id, 0.45 FROM chemical s, chemical t WHERE s.key='melatonin' AND t.key='gaba' ON CONFLICT DO NOTHING;

-- DHEA interactions
INSERT INTO chemical_interaction (source_chemical_id, target_chemical_id, mod_factor) SELECT s.id, t.id, -0.40 FROM chemical s, chemical t WHERE s.key='dhea' AND t.key='cortisol' ON CONFLICT DO NOTHING;
INSERT INTO chemical_interaction (source_chemical_id, target_chemical_id, mod_factor) SELECT s.id, t.id, 0.50 FROM chemical s, chemical t WHERE s.key='dhea' AND t.key='testosterone' ON CONFLICT DO NOTHING;
INSERT INTO chemical_interaction (source_chemical_id, target_chemical_id, mod_factor) SELECT s.id, t.id, 0.45 FROM chemical s, chemical t WHERE s.key='dhea' AND t.key='estradiol' ON CONFLICT DO NOTHING;

-- Prolactin interactions
INSERT INTO chemical_interaction (source_chemical_id, target_chemical_id, mod_factor) SELECT s.id, t.id, -0.90 FROM chemical s, chemical t WHERE s.key='dopamine' AND t.key='prolactin' ON CONFLICT DO NOTHING;
INSERT INTO chemical_interaction (source_chemical_id, target_chemical_id, mod_factor) SELECT s.id, t.id, 0.60 FROM chemical s, chemical t WHERE s.key='estradiol' AND t.key='prolactin' ON CONFLICT DO NOTHING;
INSERT INTO chemical_interaction (source_chemical_id, target_chemical_id, mod_factor) SELECT s.id, t.id, 0.50 FROM chemical s, chemical t WHERE s.key='serotonin' AND t.key='prolactin' ON CONFLICT DO NOTHING;
INSERT INTO chemical_interaction (source_chemical_id, target_chemical_id, mod_factor) SELECT s.id, t.id, 0.50 FROM chemical s, chemical t WHERE s.key='oxytocin' AND t.key='prolactin' ON CONFLICT DO NOTHING;

-- Oxytocin (H) interactions
INSERT INTO chemical_interaction (source_chemical_id, target_chemical_id, mod_factor) SELECT s.id, t.id, 0.50 FROM chemical s, chemical t WHERE s.key='oxytocin_h' AND t.key='dopamine' ON CONFLICT DO NOTHING;
INSERT INTO chemical_interaction (source_chemical_id, target_chemical_id, mod_factor) SELECT s.id, t.id, 0.45 FROM chemical s, chemical t WHERE s.key='oxytocin_h' AND t.key='serotonin' ON CONFLICT DO NOTHING;
INSERT INTO chemical_interaction (source_chemical_id, target_chemical_id, mod_factor) SELECT s.id, t.id, 0.55 FROM chemical s, chemical t WHERE s.key='oxytocin_h' AND t.key='endorphins' ON CONFLICT DO NOTHING;
INSERT INTO chemical_interaction (source_chemical_id, target_chemical_id, mod_factor) SELECT s.id, t.id, -0.55 FROM chemical s, chemical t WHERE s.key='cortisol' AND t.key='oxytocin_h' ON CONFLICT DO NOTHING;
INSERT INTO chemical_interaction (source_chemical_id, target_chemical_id, mod_factor) SELECT s.id, t.id, -0.45 FROM chemical s, chemical t WHERE s.key='testosterone' AND t.key='oxytocin_h' ON CONFLICT DO NOTHING;

-- Oxytocin (P) interactions
INSERT INTO chemical_interaction (source_chemical_id, target_chemical_id, mod_factor) SELECT s.id, t.id, 0.55 FROM chemical s, chemical t WHERE s.key='oxytocin' AND t.key='endorphins' ON CONFLICT DO NOTHING;
INSERT INTO chemical_interaction (source_chemical_id, target_chemical_id, mod_factor) SELECT s.id, t.id, 0.40 FROM chemical s, chemical t WHERE s.key='oxytocin' AND t.key='vasopressin' ON CONFLICT DO NOTHING;
INSERT INTO chemical_interaction (source_chemical_id, target_chemical_id, mod_factor) SELECT s.id, t.id, -0.50 FROM chemical s, chemical t WHERE s.key='dynorphin' AND t.key='oxytocin' ON CONFLICT DO NOTHING;

-- Vasopressin interactions
INSERT INTO chemical_interaction (source_chemical_id, target_chemical_id, mod_factor) SELECT s.id, t.id, 0.45 FROM chemical s, chemical t WHERE s.key='vasopressin' AND t.key='testosterone' ON CONFLICT DO NOTHING;

-- Endorphins interactions
INSERT INTO chemical_interaction (source_chemical_id, target_chemical_id, mod_factor) SELECT s.id, t.id, -0.60 FROM chemical s, chemical t WHERE s.key='dynorphin' AND t.key='endorphins' ON CONFLICT DO NOTHING;

-- Enkephalins interactions
INSERT INTO chemical_interaction (source_chemical_id, target_chemical_id, mod_factor) SELECT s.id, t.id, 0.25 FROM chemical s, chemical t WHERE s.key='gaba' AND t.key='enkephalins' ON CONFLICT DO NOTHING;

-- Dynorphin interactions
INSERT INTO chemical_interaction (source_chemical_id, target_chemical_id, mod_factor) SELECT s.id, t.id, 0.55 FROM chemical s, chemical t WHERE s.key='crh' AND t.key='dynorphin' ON CONFLICT DO NOTHING;
INSERT INTO chemical_interaction (source_chemical_id, target_chemical_id, mod_factor) SELECT s.id, t.id, 0.30 FROM chemical s, chemical t WHERE s.key='dynorphin' AND t.key='crh' ON CONFLICT DO NOTHING;

-- Substance P interactions
INSERT INTO chemical_interaction (source_chemical_id, target_chemical_id, mod_factor) SELECT s.id, t.id, 0.50 FROM chemical s, chemical t WHERE s.key='cortisol' AND t.key='substance_p' ON CONFLICT DO NOTHING;
INSERT INTO chemical_interaction (source_chemical_id, target_chemical_id, mod_factor) SELECT s.id, t.id, 0.45 FROM chemical s, chemical t WHERE s.key='crh' AND t.key='substance_p' ON CONFLICT DO NOTHING;
INSERT INTO chemical_interaction (source_chemical_id, target_chemical_id, mod_factor) SELECT s.id, t.id, -0.75 FROM chemical s, chemical t WHERE s.key='endorphins' AND t.key='substance_p' ON CONFLICT DO NOTHING;

-- CRH interactions
INSERT INTO chemical_interaction (source_chemical_id, target_chemical_id, mod_factor) SELECT s.id, t.id, 0.90 FROM chemical s, chemical t WHERE s.key='crh' AND t.key='cortisol' ON CONFLICT DO NOTHING;
INSERT INTO chemical_interaction (source_chemical_id, target_chemical_id, mod_factor) SELECT s.id, t.id, -0.70 FROM chemical s, chemical t WHERE s.key='npy' AND t.key='crh' ON CONFLICT DO NOTHING;
INSERT INTO chemical_interaction (source_chemical_id, target_chemical_id, mod_factor) SELECT s.id, t.id, -0.60 FROM chemical s, chemical t WHERE s.key='oxytocin' AND t.key='crh' ON CONFLICT DO NOTHING;

-- NPY interactions
INSERT INTO chemical_interaction (source_chemical_id, target_chemical_id, mod_factor) SELECT s.id, t.id, -0.50 FROM chemical s, chemical t WHERE s.key='npy' AND t.key='norepinephrine' ON CONFLICT DO NOTHING;

-- BDNF interactions
INSERT INTO chemical_interaction (source_chemical_id, target_chemical_id, mod_factor) SELECT s.id, t.id, 0.60 FROM chemical s, chemical t WHERE s.key='serotonin' AND t.key='bdnf' ON CONFLICT DO NOTHING;
INSERT INTO chemical_interaction (source_chemical_id, target_chemical_id, mod_factor) SELECT s.id, t.id, 0.45 FROM chemical s, chemical t WHERE s.key='oxytocin' AND t.key='bdnf' ON CONFLICT DO NOTHING;
INSERT INTO chemical_interaction (source_chemical_id, target_chemical_id, mod_factor) SELECT s.id, t.id, -0.55 FROM chemical s, chemical t WHERE s.key='dynorphin' AND t.key='bdnf' ON CONFLICT DO NOTHING;

-- Orexin interactions
INSERT INTO chemical_interaction (source_chemical_id, target_chemical_id, mod_factor) SELECT s.id, t.id, 0.75 FROM chemical s, chemical t WHERE s.key='orexin' AND t.key='norepinephrine' ON CONFLICT DO NOTHING;
INSERT INTO chemical_interaction (source_chemical_id, target_chemical_id, mod_factor) SELECT s.id, t.id, 0.60 FROM chemical s, chemical t WHERE s.key='orexin' AND t.key='dopamine' ON CONFLICT DO NOTHING;
INSERT INTO chemical_interaction (source_chemical_id, target_chemical_id, mod_factor) SELECT s.id, t.id, 0.50 FROM chemical s, chemical t WHERE s.key='orexin' AND t.key='crh' ON CONFLICT DO NOTHING;
INSERT INTO chemical_interaction (source_chemical_id, target_chemical_id, mod_factor) SELECT s.id, t.id, -0.55 FROM chemical s, chemical t WHERE s.key='gaba' AND t.key='orexin' ON CONFLICT DO NOTHING;