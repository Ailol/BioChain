import type { Question } from '@/types';
import { Check } from 'lucide-react';

interface Props {
  question: Question;
  selectedOptionId: number | undefined;
  onSelect: (itemId: number) => void;
}

export function QuestionCard({ question, selectedOptionId, onSelect }: Props) {
  return (
    <div className="space-y-4">
      <div className="space-y-1">
        <span className="text-[11px] font-medium text-accent-primary uppercase tracking-wide">
          Question {question.sortOrder} of 18
        </span>
        <h2 className="text-lg font-medium text-text-primary leading-snug">
          {question.scenario}
        </h2>
      </div>

      <div className="space-y-2">
        {question.options.map((opt) => {
          const isSelected = selectedOptionId === opt.id;
          return (
            <button
              key={opt.id}
              onClick={() => onSelect(opt.id)}
              className={`w-full text-left px-4 py-3 rounded-xl border transition-all ${
                isSelected
                  ? 'bg-accent-primary/15 border-accent-primary/40 text-text-primary'
                  : 'bg-bg-card border-bg-hover text-text-secondary hover:border-text-muted hover:text-text-primary'
              }`}
            >
              <div className="flex items-start gap-3">
                <div
                  className={`w-5 h-5 rounded-md border flex items-center justify-center shrink-0 mt-0.5 transition-colors ${
                    isSelected
                      ? 'bg-accent-primary border-accent-primary'
                      : 'border-text-muted'
                  }`}
                >
                  {isSelected && <Check className="w-3 h-3 text-white" />}
                </div>
                <div className="flex-1 min-w-0">
                  <span className="text-xs font-mono text-text-muted mr-2">{opt.label}.</span>
                  <span className="text-sm">{opt.text}</span>
                </div>
              </div>
            </button>
          );
        })}
      </div>
    </div>
  );
}
