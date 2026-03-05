import { Brain } from 'lucide-react';

export function TypingIndicator() {
  return (
    <div className="flex gap-3">
      <div className="w-8 h-8 rounded-lg flex items-center justify-center shrink-0 bg-layer-peptide/20">
        <Brain className="w-4 h-4 text-layer-peptide" />
      </div>
      <div className="bg-bg-card rounded-2xl rounded-tl-sm px-4 py-3 flex items-center gap-1.5">
        <span className="w-2 h-2 rounded-full bg-text-muted animate-bounce [animation-delay:0ms]" />
        <span className="w-2 h-2 rounded-full bg-text-muted animate-bounce [animation-delay:150ms]" />
        <span className="w-2 h-2 rounded-full bg-text-muted animate-bounce [animation-delay:300ms]" />
      </div>
    </div>
  );
}
