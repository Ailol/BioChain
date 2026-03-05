import { create } from 'zustand';
import type { Question } from '@/types';
import { questionnaireApi } from '@/api/questionnaire';
import { analyzeApi } from '@/api/analyze';
import { subjectsApi } from '@/api/subjects';
import { useChatStore } from './chatStore';

interface QuestionnaireState {
  // Subject creation
  subjectName: string;
  subjectCreated: boolean;
  createdSubjectId: string | null;

  // Questions
  questions: Question[];
  answers: Map<number, number>; // sortOrder → selectedItemId
  analyzedQuestions: Set<number>; // sortOrders that have been sent for analysis
  currentIndex: number;
  isLoading: boolean;
  isComplete: boolean;
  error: string | null;
  protocolsStored: number;

  setSubjectName: (name: string) => void;
  createSubject: () => Promise<void>;
  loadQuestions: () => Promise<void>;
  selectAnswer: (sortOrder: number, itemId: number) => void;
  goTo: (index: number) => void;
  prev: () => void;
  reset: () => void;
}

/** Build the same analysis text the backend QuestionnaireApi would build */
function buildAnalysisText(question: Question, selectedItemId: number): string {
  const lines = [`Psychological assessment question: ${question.scenario}`, ''];
  for (const opt of question.options) {
    const tag = opt.id === selectedItemId ? 'SELECTED' : 'REJECTED';
    lines.push(`${tag}: ${opt.text}`);
  }
  return lines.join('\n');
}

export const useQuestionnaireStore = create<QuestionnaireState>((set, get) => ({
  subjectName: '',
  subjectCreated: false,
  createdSubjectId: null,

  questions: [],
  answers: new Map(),
  analyzedQuestions: new Set(),
  currentIndex: 0,
  isLoading: false,
  isComplete: false,
  error: null,
  protocolsStored: 0,

  setSubjectName: (name) => set({ subjectName: name, error: null }),

  createSubject: async () => {
    const { subjectName } = get();
    const name = subjectName.trim();
    if (!name) {
      set({ error: 'Please enter a name.' });
      return;
    }

    set({ isLoading: true, error: null });
    try {
      const subject = await subjectsApi.create({ name });
      useChatStore.getState().setSubjectId(subject.id);
      set({ subjectCreated: true, createdSubjectId: subject.id, isLoading: false });
    } catch {
      set({ error: 'Failed to create profile. Try again.', isLoading: false });
    }
  },

  loadQuestions: async () => {
    set({ isLoading: true, error: null });
    try {
      const data = await questionnaireApi.getQuestions();
      set({ questions: data.questions, isLoading: false });
    } catch {
      set({ error: 'Failed to load questions', isLoading: false });
    }
  },

  /** Select answer, fire background analysis, auto-advance */
  selectAnswer: (sortOrder, itemId) => {
    const { questions, currentIndex, answers, analyzedQuestions, createdSubjectId } = get();
    const question = questions.find((q) => q.sortOrder === sortOrder);
    if (!question || !createdSubjectId) return;

    // Store answer
    const newAnswers = new Map(answers);
    newAnswers.set(sortOrder, itemId);

    // Fire background analysis (only once per question, re-fire if answer changed)
    const alreadyAnalyzed = analyzedQuestions.has(sortOrder) && answers.get(sortOrder) === itemId;
    if (!alreadyAnalyzed) {
      const text = buildAnalysisText(question, itemId);
      analyzeApi
        .analyze({ subjectId: createdSubjectId, text, kind: 'psych' })
        .then(() => {
          const updated = new Set(get().analyzedQuestions);
          updated.add(sortOrder);
          set((s) => ({
            analyzedQuestions: updated,
            protocolsStored: s.protocolsStored + 1,
          }));
        })
        .catch(() => {}); // non-fatal
    }

    // Auto-advance or mark complete
    const isLast = currentIndex >= questions.length - 1;
    if (isLast) {
      // Check if all answered
      const allAnswered = newAnswers.size >= questions.length;
      set({
        answers: newAnswers,
        isComplete: allAnswered,
      });
    } else {
      // Brief delay so user sees their selection, then advance
      set({ answers: newAnswers });
      setTimeout(() => {
        set({ currentIndex: currentIndex + 1 });
      }, 300);
    }
  },

  goTo: (index) => set({ currentIndex: index }),

  prev: () => {
    const { currentIndex } = get();
    if (currentIndex > 0) set({ currentIndex: currentIndex - 1 });
  },

  reset: () =>
    set({
      subjectName: '',
      subjectCreated: false,
      createdSubjectId: null,
      answers: new Map(),
      analyzedQuestions: new Set(),
      currentIndex: 0,
      isComplete: false,
      error: null,
      protocolsStored: 0,
    }),
}));
