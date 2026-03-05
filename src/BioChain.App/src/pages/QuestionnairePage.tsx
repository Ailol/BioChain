import { useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import { useQuestionnaireStore } from '@/stores/questionnaireStore';
import { QuestionCard } from '@/components/questionnaire/QuestionCard';
import { LoadingSpinner } from '@/components/LoadingSpinner';
import {
  ChevronLeft,
  CheckCircle2,
  ClipboardList,
  User,
  ArrowRight,
  Activity,
} from 'lucide-react';

export default function QuestionnairePage() {
  const navigate = useNavigate();
  const {
    subjectName,
    subjectCreated,
    questions,
    answers,
    analyzedQuestions,
    currentIndex,
    isLoading,
    isComplete,
    error,
    protocolsStored,
    setSubjectName,
    createSubject,
    loadQuestions,
    selectAnswer,
    goTo,
    prev,
    reset,
  } = useQuestionnaireStore();

  const current = questions[currentIndex];
  const selectedId = current ? answers.get(current.sortOrder) : undefined;
  const answeredCount = answers.size;
  const totalQuestions = questions.length;

  useEffect(() => {
    if (subjectCreated && questions.length === 0) loadQuestions();
  }, [subjectCreated]);

  // Loading state
  if (isLoading && subjectCreated) {
    return (
      <div className="flex items-center justify-center h-full">
        <LoadingSpinner text="Loading questions..." />
      </div>
    );
  }

  // Complete state
  if (isComplete) {
    return (
      <div className="flex items-center justify-center h-full">
        <div className="text-center space-y-4 max-w-sm">
          <div className="w-16 h-16 rounded-2xl bg-accent-success/20 flex items-center justify-center mx-auto">
            <CheckCircle2 className="w-8 h-8 text-accent-success" />
          </div>
          <h2 className="text-xl font-medium text-text-primary">Profile Built</h2>
          <p className="text-sm text-text-secondary">
            Your responses are being analyzed in the background.
            {protocolsStored > 0 && (
              <span className="block mt-1 text-accent-success">
                {protocolsStored} signals processed so far.
              </span>
            )}
          </p>
          <div className="flex gap-3 justify-center pt-2">
            <button
              onClick={() => navigate('/')}
              className="px-4 py-2 bg-accent-primary text-white rounded-xl text-sm font-medium hover:opacity-90 transition-opacity"
            >
              Start Chatting
            </button>
            <button
              onClick={reset}
              className="px-4 py-2 bg-bg-card border border-bg-hover text-text-secondary rounded-xl text-sm hover:text-text-primary transition-colors"
            >
              Retake
            </button>
          </div>
        </div>
      </div>
    );
  }

  // Step 1: Name input
  if (!subjectCreated) {
    return (
      <div className="flex items-center justify-center h-full">
        <div className="max-w-md w-full px-6">
          <div className="text-center space-y-6">
            <div className="w-16 h-16 rounded-2xl bg-accent-primary/20 flex items-center justify-center mx-auto">
              <User className="w-8 h-8 text-accent-primary" />
            </div>
            <div>
              <h2 className="text-xl font-medium text-text-primary">Create Your Profile</h2>
              <p className="text-sm text-text-secondary mt-2">
                Enter your name to begin. Your responses will build a unique biochemical
                profile that Heretic uses to understand you.
              </p>
            </div>

            <div className="space-y-3">
              <input
                type="text"
                value={subjectName}
                onChange={(e) => setSubjectName(e.target.value)}
                onKeyDown={(e) => {
                  if (e.key === 'Enter' && subjectName.trim()) createSubject();
                }}
                placeholder="Your name..."
                autoFocus
                className="w-full px-4 py-3 bg-bg-card border border-bg-hover rounded-xl text-text-primary placeholder-text-muted text-sm focus:outline-none focus:border-accent-primary transition-colors"
              />

              {error && <p className="text-accent-danger text-sm">{error}</p>}

              <button
                onClick={createSubject}
                disabled={!subjectName.trim() || isLoading}
                className="w-full flex items-center justify-center gap-2 px-4 py-3 rounded-xl text-sm font-medium bg-accent-primary text-white disabled:opacity-30 hover:opacity-90 transition-opacity"
              >
                {isLoading ? (
                  <LoadingSpinner size="sm" />
                ) : (
                  <>
                    Continue to Questions
                    <ArrowRight className="w-4 h-4" />
                  </>
                )}
              </button>
            </div>
          </div>
        </div>
      </div>
    );
  }

  // No questions loaded
  if (!current) {
    return (
      <div className="flex items-center justify-center h-full">
        <p className="text-text-muted text-sm">No questions available.</p>
      </div>
    );
  }

  // Step 2: Questions (auto-advance on click)
  return (
    <div className="flex flex-col h-full">
      {/* Header */}
      <header className="flex items-center justify-between px-6 py-3 border-b border-bg-hover bg-bg-secondary/50 shrink-0">
        <div className="flex items-center gap-3">
          <div className="w-8 h-8 rounded-lg bg-accent-warning/20 flex items-center justify-center">
            <ClipboardList className="w-4 h-4 text-accent-warning" />
          </div>
          <div>
            <h1 className="text-sm font-medium text-text-primary">NeuroMap-18</h1>
            <p className="text-[11px] text-text-muted">
              {currentIndex + 1} of {totalQuestions}
            </p>
          </div>
        </div>

        <div className="flex items-center gap-3">
          {/* Analysis counter */}
          {analyzedQuestions.size > 0 && (
            <div className="flex items-center gap-1.5 px-2.5 py-1 rounded-full bg-accent-success/10 text-accent-success text-[11px]">
              <Activity className="w-3 h-3" />
              {analyzedQuestions.size} analyzed
            </div>
          )}

          {/* Progress bar */}
          <div className="w-24 h-1.5 bg-bg-hover rounded-full overflow-hidden">
            <div
              className="h-full bg-accent-primary rounded-full transition-all duration-300"
              style={{ width: `${(answeredCount / totalQuestions) * 100}%` }}
            />
          </div>
          <span className="text-[11px] text-text-muted font-mono">
            {Math.round((answeredCount / totalQuestions) * 100)}%
          </span>
        </div>
      </header>

      {/* Question */}
      <div className="flex-1 overflow-y-auto px-6 py-8">
        <div className="max-w-xl mx-auto">
          <QuestionCard
            question={current}
            selectedOptionId={selectedId}
            onSelect={(itemId) => selectAnswer(current.sortOrder, itemId)}
          />

          {error && (
            <p className="text-accent-danger text-sm mt-4">{error}</p>
          )}
        </div>
      </div>

      {/* Navigation */}
      <div className="px-6 py-3 border-t border-bg-hover bg-bg-secondary/50 shrink-0">
        <div className="max-w-xl mx-auto flex items-center justify-between">
          <button
            onClick={prev}
            disabled={currentIndex === 0}
            className="flex items-center gap-1 px-3 py-2 rounded-xl text-sm text-text-secondary hover:text-text-primary disabled:opacity-30 transition-colors"
          >
            <ChevronLeft className="w-4 h-4" />
            Back
          </button>

          {/* Dot navigation */}
          <div className="flex gap-1">
            {questions.map((q, i) => (
              <button
                key={q.sortOrder}
                onClick={() => goTo(i)}
                className={`w-2 h-2 rounded-full transition-colors ${
                  i === currentIndex
                    ? 'bg-accent-primary'
                    : answers.has(q.sortOrder)
                      ? 'bg-accent-success'
                      : 'bg-bg-hover'
                }`}
              />
            ))}
          </div>

          {/* Hint text */}
          <p className="text-[11px] text-text-muted w-20 text-right">
            {selectedId ? 'Answered' : 'Pick one'}
          </p>
        </div>
      </div>
    </div>
  );
}
