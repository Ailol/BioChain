import { useState, useRef, useEffect } from 'react';
import { Send } from 'lucide-react';

interface Props {
  onSend: (message: string) => void;
  disabled?: boolean;
  placeholder?: string;
}

export function ChatInput({ onSend, disabled, placeholder }: Props) {
  const [text, setText] = useState('');
  const textareaRef = useRef<HTMLTextAreaElement>(null);

  useEffect(() => {
    if (textareaRef.current) {
      textareaRef.current.style.height = 'auto';
      textareaRef.current.style.height = `${Math.min(textareaRef.current.scrollHeight, 160)}px`;
    }
  }, [text]);

  const handleSubmit = () => {
    const trimmed = text.trim();
    if (!trimmed || disabled) return;
    onSend(trimmed);
    setText('');
  };

  const handleKeyDown = (e: React.KeyboardEvent) => {
    if (e.key === 'Enter' && !e.shiftKey) {
      e.preventDefault();
      handleSubmit();
    }
  };

  return (
    <div className="flex items-end gap-2 bg-bg-card border border-bg-hover rounded-2xl px-4 py-3">
      <textarea
        ref={textareaRef}
        value={text}
        onChange={(e) => setText(e.target.value)}
        onKeyDown={handleKeyDown}
        placeholder={placeholder ?? 'Ask anything about your neurochemistry...'}
        disabled={disabled}
        rows={1}
        className="flex-1 bg-transparent text-text-primary text-sm resize-none outline-none placeholder:text-text-muted min-h-[24px] max-h-[160px]"
      />
      <button
        onClick={handleSubmit}
        disabled={disabled || !text.trim()}
        className="w-8 h-8 rounded-lg flex items-center justify-center bg-accent-primary text-white disabled:opacity-30 hover:opacity-90 transition-opacity shrink-0"
      >
        <Send className="w-4 h-4" />
      </button>
    </div>
  );
}
