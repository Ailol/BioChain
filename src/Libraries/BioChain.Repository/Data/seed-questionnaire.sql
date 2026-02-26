-- seed-questionnaire.sql
-- NeuroTriangulate-18: 18 scenario questions x 3 options = 54 items.
-- Each question targets one MBTI axis. Option A = pole 1, Option B = pole 2, Option C = near-neutral.
-- Distribution: E/I x5, N/S x4, F/T x5, P/J x4.
-- Idempotent (ON CONFLICT DO UPDATE — re-seeding updates existing rows).

-- ═══════════════════════════════════════════════════════════════════════
-- P/J Questions (4): Q1, Q13, Q17, Q18
-- ═══════════════════════════════════════════════════════════════════════

-- Q1 — Morning Drive (P/J)
INSERT INTO questionnaire_item (sort_order, scenario, label, option_text, primary_signal, secondary_signal) VALUES
(1, 'When you first wake up, what pulls you out of bed?', 'A', 'Curiosity — something about today feels interesting', 'orexin', 'norepinephrine'),
(1, 'When you first wake up, what pulls you out of bed?', 'B', 'Routine — the comfort of how your mornings go', 'cortisol', 'crh'),
(1, 'When you first wake up, what pulls you out of bed?', 'C', 'People — someone is expecting you, counting on you', 'dhea', 'progesterone')
ON CONFLICT (sort_order, label) DO UPDATE SET
    scenario = EXCLUDED.scenario, option_text = EXCLUDED.option_text,
    primary_signal = EXCLUDED.primary_signal, secondary_signal = EXCLUDED.secondary_signal;

-- ═══════════════════════════════════════════════════════════════════════
-- N/S Questions (4): Q2, Q3, Q9, Q14
-- ═══════════════════════════════════════════════════════════════════════

-- Q2 — Resting State (N/S) — Option C rewritten for cleaner S-pole neutral
INSERT INTO questionnaire_item (sort_order, scenario, label, option_text, primary_signal, secondary_signal) VALUES
(2, 'Your default mental state when you have nothing to do:', 'A', 'Mind wandering — ideas, connections, what-ifs flowing', 'bdnf', 'glutamate'),
(2, 'Your default mental state when you have nothing to do:', 'B', 'Calm — grounded, steady breathing, present in the moment', 'serotonin', 'acetylcholine'),
(2, 'Your default mental state when you have nothing to do:', 'C', 'Focused on what is around you — noticing sounds, textures, the temperature of the room', 'acetylcholine', 'serotonin')
ON CONFLICT (sort_order, label) DO UPDATE SET
    scenario = EXCLUDED.scenario, option_text = EXCLUDED.option_text,
    primary_signal = EXCLUDED.primary_signal, secondary_signal = EXCLUDED.secondary_signal;

-- Q3 — Cancelled Plans (N/S)
INSERT INTO questionnaire_item (sort_order, scenario, label, option_text, primary_signal, secondary_signal) VALUES
(3, 'A close friend cancels plans last minute. Your gut reaction:', 'A', 'Already rearranging — what else could you do instead', 'dopamine', 'bdnf'),
(3, 'A close friend cancels plans last minute. Your gut reaction:', 'B', 'A quiet irritation that sticks with you — plans matter', 'serotonin', 'substance_p'),
(3, 'A close friend cancels plans last minute. Your gut reaction:', 'C', 'Concern — you text to check if they''re okay', 'oxytocin', 'endorphins')
ON CONFLICT (sort_order, label) DO UPDATE SET
    scenario = EXCLUDED.scenario, option_text = EXCLUDED.option_text,
    primary_signal = EXCLUDED.primary_signal, secondary_signal = EXCLUDED.secondary_signal;

-- ═══════════════════════════════════════════════════════════════════════
-- E/I Questions (5): Q4, Q6, Q10, Q11, Q12
-- ═══════════════════════════════════════════════════════════════════════

-- Q4 — Group Dynamics (E/I)
INSERT INTO questionnaire_item (sort_order, scenario, label, option_text, primary_signal, secondary_signal) VALUES
(4, 'In a group of people, you naturally tend to:', 'A', 'Steer the direction — you end up leading without deciding to', 'testosterone', 'dopamine'),
(4, 'In a group of people, you naturally tend to:', 'B', 'Observe and think — you process internally before speaking', 'gaba', 'acetylcholine'),
(4, 'In a group of people, you naturally tend to:', 'C', 'Read the room — you tune into the energy and adapt', 'bdnf', 'endorphins')
ON CONFLICT (sort_order, label) DO UPDATE SET
    scenario = EXCLUDED.scenario, option_text = EXCLUDED.option_text,
    primary_signal = EXCLUDED.primary_signal, secondary_signal = EXCLUDED.secondary_signal;

-- ═══════════════════════════════════════════════════════════════════════
-- F/T Questions (5): Q5, Q7, Q8, Q15, Q16
-- ═══════════════════════════════════════════════════════════════════════

-- Q5 — Unexpected Vulnerability (F/T)
INSERT INTO questionnaire_item (sort_order, scenario, label, option_text, primary_signal, secondary_signal) VALUES
(5, 'Someone unexpectedly tells you something deeply personal. You:', 'A', 'Feel honored — lean in, give them everything', 'oxytocin', 'endorphins'),
(5, 'Someone unexpectedly tells you something deeply personal. You:', 'B', 'Start thinking about how to help — what can be done', 'testosterone', 'vasopressin'),
(5, 'Someone unexpectedly tells you something deeply personal. You:', 'C', 'Feel slightly uncomfortable but hold steady', 'melatonin', 'orexin')
ON CONFLICT (sort_order, label) DO UPDATE SET
    scenario = EXCLUDED.scenario, option_text = EXCLUDED.option_text,
    primary_signal = EXCLUDED.primary_signal, secondary_signal = EXCLUDED.secondary_signal;

-- Q6 — Social Energy Recovery (E/I) — FULL REPLACEMENT (was Threat Response)
INSERT INTO questionnaire_item (sort_order, scenario, label, option_text, primary_signal, secondary_signal) VALUES
(6, 'After a full day spent around other people, you feel:', 'A', 'Charged up — you could keep going, the energy feeds you', 'dopamine', 'oxytocin_h'),
(6, 'After a full day spent around other people, you feel:', 'B', 'Drained — you need quiet and solitude to recover', 'gaba', 'melatonin'),
(6, 'After a full day spent around other people, you feel:', 'C', 'It depends entirely on who the people were', 'acetylcholine', 'endorphins')
ON CONFLICT (sort_order, label) DO UPDATE SET
    scenario = EXCLUDED.scenario, option_text = EXCLUDED.option_text,
    primary_signal = EXCLUDED.primary_signal, secondary_signal = EXCLUDED.secondary_signal;

-- Q7 — Hitting a Wall (F/T) — Option B rewritten for T-pole logical detachment
INSERT INTO questionnaire_item (sort_order, scenario, label, option_text, primary_signal, secondary_signal) VALUES
(7, 'You''ve been working on something for hours and hit a wall:', 'A', 'Feel it emotionally — frustration, self-doubt, needing support', 'endorphins', 'estradiol'),
(7, 'You''ve been working on something for hours and hit a wall:', 'B', 'Detach and diagnose — step back, break the problem apart logically, find what is actually stuck', 'acetylcholine', 'vasopressin'),
(7, 'You''ve been working on something for hours and hit a wall:', 'C', 'Step away — you''ll come back fresh tomorrow', 'glutamate', 'npy')
ON CONFLICT (sort_order, label) DO UPDATE SET
    scenario = EXCLUDED.scenario, option_text = EXCLUDED.option_text,
    primary_signal = EXCLUDED.primary_signal, secondary_signal = EXCLUDED.secondary_signal;

-- Q8 — Loss Processing (F/T)
INSERT INTO questionnaire_item (sort_order, scenario, label, option_text, primary_signal, secondary_signal) VALUES
(8, 'When you lose something that genuinely mattered to you:', 'A', 'Reach for people — being with someone helps', 'oxytocin', 'estradiol'),
(8, 'When you lose something that genuinely mattered to you:', 'B', 'Stay busy — action keeps it at a distance', 'vasopressin', 'testosterone'),
(8, 'When you lose something that genuinely mattered to you:', 'C', 'Go inward — need time alone to process', 'melatonin', 'bdnf')
ON CONFLICT (sort_order, label) DO UPDATE SET
    scenario = EXCLUDED.scenario, option_text = EXCLUDED.option_text,
    primary_signal = EXCLUDED.primary_signal, secondary_signal = EXCLUDED.secondary_signal;

-- Q9 — Post-Achievement (N/S)
INSERT INTO questionnaire_item (sort_order, scenario, label, option_text, primary_signal, secondary_signal) VALUES
(9, 'You just accomplished something significant. What do you want most?', 'A', 'Already thinking ahead — what''s the next level, what connects', 'bdnf', 'glutamate'),
(9, 'You just accomplished something significant. What do you want most?', 'B', 'Sit with it — let the feeling land, savour the real moment', 'serotonin', 'substance_p'),
(9, 'You just accomplished something significant. What do you want most?', 'C', 'Share it — tell the person who''d understand', 'enkephalins', 'prolactin')
ON CONFLICT (sort_order, label) DO UPDATE SET
    scenario = EXCLUDED.scenario, option_text = EXCLUDED.option_text,
    primary_signal = EXCLUDED.primary_signal, secondary_signal = EXCLUDED.secondary_signal;

-- Q10 — Competition (E/I)
INSERT INTO questionnaire_item (sort_order, scenario, label, option_text, primary_signal, secondary_signal) VALUES
(10, 'In a competitive situation, you''re most motivated by:', 'A', 'Winning together — the outcome and the team energy', 'dopamine', 'oxytocin_h'),
(10, 'In a competitive situation, you''re most motivated by:', 'B', 'Performing well — regardless of who else is there', 'acetylcholine', 'gaba'),
(10, 'In a competitive situation, you''re most motivated by:', 'C', 'The intensity itself — the rush of being in it', 'endorphins', 'enkephalins')
ON CONFLICT (sort_order, label) DO UPDATE SET
    scenario = EXCLUDED.scenario, option_text = EXCLUDED.option_text,
    primary_signal = EXCLUDED.primary_signal, secondary_signal = EXCLUDED.secondary_signal;

-- Q11 — Risk Decision (E/I)
INSERT INTO questionnaire_item (sort_order, scenario, label, option_text, primary_signal, secondary_signal) VALUES
(11, 'You''re offered something you want, but it requires giving up comfort and certainty:', 'A', 'Take it — the pull toward new is stronger than safe', 'testosterone', 'adrenaline'),
(11, 'You''re offered something you want, but it requires giving up comfort and certainty:', 'B', 'Weigh it carefully — you need to think before disrupting what works', 'gaba', 'glutamate'),
(11, 'You''re offered something you want, but it requires giving up comfort and certainty:', 'C', 'Trust the process — things tend to work out either way', 'endocannabinoid', 'npy')
ON CONFLICT (sort_order, label) DO UPDATE SET
    scenario = EXCLUDED.scenario, option_text = EXCLUDED.option_text,
    primary_signal = EXCLUDED.primary_signal, secondary_signal = EXCLUDED.secondary_signal;

-- Q12 — Free Day (E/I)
INSERT INTO questionnaire_item (sort_order, scenario, label, option_text, primary_signal, secondary_signal) VALUES
(12, 'A completely free day. You gravitate toward:', 'A', 'Something social — people, energy, shared experience', 'dopamine', 'oxytocin'),
(12, 'A completely free day. You gravitate toward:', 'B', 'Something absorbing — a project, deep reading, building alone', 'acetylcholine', 'cortisol'),
(12, 'A completely free day. You gravitate toward:', 'C', 'Something warm — good food, low-key company, softness', 'enkephalins', 'progesterone')
ON CONFLICT (sort_order, label) DO UPDATE SET
    scenario = EXCLUDED.scenario, option_text = EXCLUDED.option_text,
    primary_signal = EXCLUDED.primary_signal, secondary_signal = EXCLUDED.secondary_signal;

-- Q13 — Sleep Pattern (P/J)
INSERT INTO questionnaire_item (sort_order, scenario, label, option_text, primary_signal, secondary_signal) VALUES
(13, 'Your relationship with sleep:', 'A', 'You resist it — there''s always more to do or think about', 'orexin', 'endocannabinoid'),
(13, 'Your relationship with sleep:', 'B', 'You crash on schedule — same time each night, reliable rhythm', 'cortisol', 'serotonin'),
(13, 'Your relationship with sleep:', 'C', 'It''s unreliable — some nights wired, others obliterated', 'dhea', 'progesterone')
ON CONFLICT (sort_order, label) DO UPDATE SET
    scenario = EXCLUDED.scenario, option_text = EXCLUDED.option_text,
    primary_signal = EXCLUDED.primary_signal, secondary_signal = EXCLUDED.secondary_signal;

-- Q14 — Learning Style (N/S) — FULL REPLACEMENT (was Eating Pattern)
INSERT INTO questionnaire_item (sort_order, scenario, label, option_text, primary_signal, secondary_signal) VALUES
(14, 'When you need to learn something completely new, you:', 'A', 'Jump to the big picture first — you want the theory, the framework, how it connects to everything else', 'bdnf', 'glutamate'),
(14, 'When you need to learn something completely new, you:', 'B', 'Start with the concrete steps — give you the manual, the examples, let you follow along hands-on', 'serotonin', 'acetylcholine'),
(14, 'When you need to learn something completely new, you:', 'C', 'Ask someone who already knows — you learn best through conversation and shared experience', 'oxytocin', 'endorphins')
ON CONFLICT (sort_order, label) DO UPDATE SET
    scenario = EXCLUDED.scenario, option_text = EXCLUDED.option_text,
    primary_signal = EXCLUDED.primary_signal, secondary_signal = EXCLUDED.secondary_signal;

-- Q15 — Conflict Response (F/T) — FULL REPLACEMENT (was Physical Pain)
INSERT INTO questionnaire_item (sort_order, scenario, label, option_text, primary_signal, secondary_signal) VALUES
(15, 'You disagree with someone you care about on something that matters to both of you:', 'A', 'The relationship comes first — you would rather find harmony than win the point', 'oxytocin', 'endorphins'),
(15, 'You disagree with someone you care about on something that matters to both of you:', 'B', 'The truth comes first — you need to say what you actually think, even if it creates tension', 'testosterone', 'vasopressin'),
(15, 'You disagree with someone you care about on something that matters to both of you:', 'C', 'You go quiet — you need space to figure out what you actually believe before speaking', 'melatonin', 'bdnf')
ON CONFLICT (sort_order, label) DO UPDATE SET
    scenario = EXCLUDED.scenario, option_text = EXCLUDED.option_text,
    primary_signal = EXCLUDED.primary_signal, secondary_signal = EXCLUDED.secondary_signal;

-- Q16 — Memory Bias (F/T)
INSERT INTO questionnaire_item (sort_order, scenario, label, option_text, primary_signal, secondary_signal) VALUES
(16, 'The memories that surface most often for you tend to be:', 'A', 'People — faces, conversations, moments of connection', 'oxytocin', 'estradiol'),
(16, 'The memories that surface most often for you tend to be:', 'B', 'Challenges — problems solved, strategies that worked', 'testosterone', 'vasopressin'),
(16, 'The memories that surface most often for you tend to be:', 'C', 'Peaks — firsts, wins, breakthroughs, novel moments', 'melatonin', 'npy')
ON CONFLICT (sort_order, label) DO UPDATE SET
    scenario = EXCLUDED.scenario, option_text = EXCLUDED.option_text,
    primary_signal = EXCLUDED.primary_signal, secondary_signal = EXCLUDED.secondary_signal;

-- Q17 — Open Time (P/J) — FULL REPLACEMENT (was Decision Making)
INSERT INTO questionnaire_item (sort_order, scenario, label, option_text, primary_signal, secondary_signal) VALUES
(17, 'A weekend opens up with nothing scheduled. Your instinct is to:', 'A', 'Keep it open — decide in the moment what feels right, let the day unfold', 'orexin', 'dopamine'),
(17, 'A weekend opens up with nothing scheduled. Your instinct is to:', 'B', 'Make a plan — even a loose structure makes the time feel more valuable', 'cortisol', 'crh'),
(17, 'A weekend opens up with nothing scheduled. Your instinct is to:', 'C', 'Fill it with people — call someone, organize something together', 'dhea', 'oxytocin')
ON CONFLICT (sort_order, label) DO UPDATE SET
    scenario = EXCLUDED.scenario, option_text = EXCLUDED.option_text,
    primary_signal = EXCLUDED.primary_signal, secondary_signal = EXCLUDED.secondary_signal;

-- Q18 — Plan Change (P/J) — FULL REPLACEMENT (was inverted "What You'd Change")
-- No longer inverted — direct scenario about plan disruption.
INSERT INTO questionnaire_item (sort_order, scenario, label, option_text, primary_signal, secondary_signal, is_inverted) VALUES
(18, 'Plans you were counting on change at the last minute. Your honest reaction:', 'A', 'Relief, honestly — new possibilities just opened up', 'orexin', 'endocannabinoid', FALSE),
(18, 'Plans you were counting on change at the last minute. Your honest reaction:', 'B', 'Frustration — you had mentally committed and now everything is off', 'cortisol', 'crh', FALSE),
(18, 'Plans you were counting on change at the last minute. Your honest reaction:', 'C', 'Depends on who changed them and why — context matters more than the change itself', 'dhea', 'progesterone', FALSE)
ON CONFLICT (sort_order, label) DO UPDATE SET
    scenario = EXCLUDED.scenario, option_text = EXCLUDED.option_text,
    primary_signal = EXCLUDED.primary_signal, secondary_signal = EXCLUDED.secondary_signal,
    is_inverted = EXCLUDED.is_inverted;
