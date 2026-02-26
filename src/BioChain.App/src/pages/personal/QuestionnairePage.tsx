import { useEffect, useRef, useState } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { ArrowRight, CheckCircle2, Loader2 } from 'lucide-react';
import { questionnaireApi } from '@/api/questionnaire';
import { usePersonStore } from '@/stores/personStore';
import { LoadingSpinner } from '@/components/LoadingSpinner';
import type { QuestionnaireState, QuestionnaireQuestion, SingleAnswerResult } from '@/types';

export default function QuestionnairePage() {
  const { token: urlToken } = useParams<{ token: string }>();
  const navigate = useNavigate();
  const { activePerson } = usePersonStore();

  const [token, setToken] = useState<string | null>(urlToken ?? null);
  const [state, setState] = useState<QuestionnaireState | null>(null);
  const [currentIndex, setCurrentIndex] = useState(0);
  const [answered, setAnswered] = useState<Set<number>>(new Set());
  const [loading, setLoading] = useState(true);
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [complete, setComplete] = useState(false);
  const [hoveredOption, setHoveredOption] = useState<number | null>(null);
  const [selectedOption, setSelectedOption] = useState<number | null>(null);
  const initCalled = useRef(false);

  const isPublic = !!urlToken;

  // Initialize: load existing questionnaire or create new one
  useEffect(() => {
    if (initCalled.current) return;
    initCalled.current = true;

    const init = async () => {
      try {
        if (urlToken) {
          // Public mode: load by token
          const data = await questionnaireApi.get(urlToken);
          setState(data);
          const answeredSet = new Set(data.answeredSortOrders);
          setAnswered(answeredSet);
          if (data.status === 'completed') {
            setComplete(true);
          } else {
            // Find first unanswered question
            const firstUnanswered = data.questions.findIndex(q => !answeredSet.has(q.sortOrder));
            setCurrentIndex(firstUnanswered >= 0 ? firstUnanswered : 0);
          }
        } else if (activePerson) {
          // Authenticated mode: create questionnaire for person
          const { token: newToken } = await questionnaireApi.create({ personName: activePerson });
          setToken(newToken);
          const data = await questionnaireApi.get(newToken);
          setState(data);
          const answeredSet = new Set(data.answeredSortOrders);
          setAnswered(answeredSet);
          if (data.status === 'completed') {
            setComplete(true);
          } else {
            const firstUnanswered = data.questions.findIndex(q => !answeredSet.has(q.sortOrder));
            setCurrentIndex(firstUnanswered >= 0 ? firstUnanswered : 0);
          }
        }
      } catch (err: unknown) {
        const msg = err instanceof Error ? err.message : 'Failed to load questionnaire';
        setError(msg);
      } finally {
        setLoading(false);
      }
    };

    init();
  }, [urlToken, activePerson]);

  const handleAnswer = async (itemId: number) => {
    if (!token || submitting) return;

    setSelectedOption(itemId);
    setSubmitting(true);

    try {
      const result: SingleAnswerResult = await questionnaireApi.answer(token, { itemId });
      const question = state!.questions[currentIndex];
      const newAnswered = new Set(answered);
      newAnswered.add(question.sortOrder);
      setAnswered(newAnswered);

      if (result.isComplete) {
        // Brief delay for the selection animation
        setTimeout(() => {
          setComplete(true);
          setSubmitting(false);
          setSelectedOption(null);
        }, 400);
        return;
      }

      // Auto-advance to next unanswered question after brief animation
      setTimeout(() => {
        const nextIndex = state!.questions.findIndex(
          (q, i) => i > currentIndex && !newAnswered.has(q.sortOrder)
        );
        if (nextIndex >= 0) {
          setCurrentIndex(nextIndex);
        } else {
          // Wrap around to find any unanswered
          const anyUnanswered = state!.questions.findIndex(q => !newAnswered.has(q.sortOrder));
          if (anyUnanswered >= 0) setCurrentIndex(anyUnanswered);
        }
        setSubmitting(false);
        setSelectedOption(null);
      }, 400);
    } catch {
      setError('Failed to submit answer. Please try again.');
      setSubmitting(false);
      setSelectedOption(null);
    }
  };

  // --- Loading / Error / No Person states ---

  if (loading) {
    return (
      <div className="flex items-center justify-center min-h-screen bg-bg-primary">
        <LoadingSpinner text="Loading questionnaire..." />
      </div>
    );
  }

  if (error) {
    return (
      <div className="flex items-center justify-center min-h-screen bg-bg-primary p-4">
        <div className="text-center space-y-3">
          <p className="text-accent-danger text-sm">{error}</p>
          <button
            onClick={() => window.location.reload()}
            className="px-4 py-2 bg-accent-primary text-white rounded-lg text-sm hover:opacity-90 transition-opacity"
          >
            Retry
          </button>
        </div>
      </div>
    );
  }

  if (!isPublic && !activePerson) {
    return (
      <div className="flex items-center justify-center min-h-[60vh]">
        <div className="text-center space-y-2">
          <p className="text-text-secondary text-sm">Select a person to start a questionnaire.</p>
        </div>
      </div>
    );
  }

  if (!state) {
    return (
      <div className="flex items-center justify-center min-h-screen bg-bg-primary">
        <p className="text-text-muted text-sm">No questionnaire data available.</p>
      </div>
    );
  }

  // --- Completion Screen ---

  if (complete) {
    return (
      <Wrapper isPublic={isPublic} personName={state.personName}>
        <div className="flex items-center justify-center min-h-[70vh]">
          <div className="text-center space-y-5 max-w-md">
            <div className="w-16 h-16 rounded-full bg-accent-success/10 flex items-center justify-center mx-auto">
              <CheckCircle2 className="w-8 h-8 text-accent-success" />
            </div>
            <div>
              <h2 className="text-xl font-semibold text-text-primary mb-2">Assessment Complete</h2>
              <p className="text-sm text-text-secondary leading-relaxed">
                All 18 questions answered for <span className="text-text-primary font-medium">{state.personName}</span>.
                Your biochemical profile is being analyzed.
              </p>
            </div>
            {!isPublic && (
              <button
                onClick={() => navigate('/personal/biosphere')}
                className="inline-flex items-center gap-2 px-5 py-2.5 bg-accent-primary text-white rounded-lg text-sm font-medium hover:opacity-90 transition-opacity"
              >
                View BioSphere <ArrowRight className="w-4 h-4" />
              </button>
            )}
          </div>
        </div>
      </Wrapper>
    );
  }

  // --- Question Display ---

  const question = state.questions[currentIndex];
  const answeredCount = answered.size;
  const totalQuestions = state.questions.length;

  return (
    <Wrapper isPublic={isPublic} personName={state.personName}>
      <div className="max-w-2xl mx-auto py-8 px-4">
        {/* Progress */}
        <div className="mb-8">
          <div className="flex items-center justify-between mb-3">
            <span className="text-xs text-text-muted font-medium uppercase tracking-wider">
              Question {answeredCount + 1} of {totalQuestions}
            </span>
            <span className="text-xs text-text-secondary">
              {Math.round((answeredCount / totalQuestions) * 100)}%
            </span>
          </div>
          {/* Progress dots */}
          <div className="flex gap-1">
            {state.questions.map((q, i) => (
              <div
                key={q.sortOrder}
                className={`h-1 flex-1 rounded-full transition-all duration-300 ${
                  answered.has(q.sortOrder)
                    ? 'bg-accent-primary'
                    : i === currentIndex
                    ? 'bg-accent-primary/50'
                    : 'bg-white/5'
                }`}
              />
            ))}
          </div>
        </div>

        {/* Scenario */}
        <div className="mb-8">
          <p className="text-base text-text-primary leading-relaxed">
            {question.scenario}
          </p>
        </div>

        {/* Options */}
        <div className="space-y-3">
          {question.options.map((opt) => {
            const isSelected = selectedOption === opt.id;
            const isHovered = hoveredOption === opt.id;

            return (
              <button
                key={opt.id}
                disabled={submitting}
                onClick={() => handleAnswer(opt.id)}
                onMouseEnter={() => setHoveredOption(opt.id)}
                onMouseLeave={() => setHoveredOption(null)}
                className={`w-full text-left rounded-xl p-5 border transition-all duration-200 ${
                  isSelected
                    ? 'bg-accent-primary/10 border-accent-primary/40 scale-[0.98]'
                    : isHovered && !submitting
                    ? 'bg-bg-hover border-accent-primary/20'
                    : 'bg-bg-card border-white/5 hover:border-white/10'
                } ${submitting ? 'cursor-not-allowed opacity-60' : 'cursor-pointer'}`}
              >
                <div className="flex gap-4 items-start">
                  <div
                    className={`w-8 h-8 rounded-full flex items-center justify-center shrink-0 text-sm font-semibold transition-colors ${
                      isSelected
                        ? 'bg-accent-primary text-white'
                        : 'bg-white/5 text-text-secondary'
                    }`}
                  >
                    {opt.label}
                  </div>
                  <div className="flex-1">
                    <p className="text-sm text-text-primary leading-relaxed">
                      {opt.text}
                    </p>
                  </div>
                  {isSelected && (
                    <Loader2 className="w-4 h-4 text-accent-primary animate-spin shrink-0 mt-1" />
                  )}
                </div>
              </button>
            );
          })}
        </div>
      </div>
    </Wrapper>
  );
}

// --- Wrapper: provides minimal chrome for public mode ---

function Wrapper({ isPublic, personName, children }: { isPublic: boolean; personName: string; children: React.ReactNode }) {
  if (!isPublic) {
    // Authenticated mode — Layout already wraps this page
    return <>{children}</>;
  }

  // Public mode — standalone minimal layout
  return (
    <div className="min-h-screen bg-bg-primary">
      <div className="border-b border-white/5 px-6 py-3 flex items-center justify-between">
        <div>
          <span className="text-sm font-semibold text-accent-primary">BioChain</span>
          <span className="text-sm font-semibold text-text-primary"> Assessment</span>
        </div>
        <span className="text-xs text-text-muted">
          for <span className="text-text-secondary">{personName}</span>
        </span>
      </div>
      {children}
    </div>
  );
}
