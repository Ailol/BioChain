import { Brain, User, AlertCircle } from 'lucide-react';
import type { ChatMessage as ChatMessageType } from '@/types';
import { formatRelativeTime } from '@/utils/format';

interface Props {
  message: ChatMessageType;
}

export function ChatMessage({ message }: Props) {
  const isUser = message.role === 'user';
  const isError = message.role === 'system';

  return (
    <div className={`flex gap-3 ${isUser ? 'flex-row-reverse' : ''}`}>
      {/* Avatar */}
      <div
        className={`w-8 h-8 rounded-lg flex items-center justify-center shrink-0 ${
          isUser
            ? 'bg-accent-primary/20'
            : isError
              ? 'bg-accent-danger/20'
              : 'bg-layer-peptide/20'
        }`}
      >
        {isUser ? (
          <User className="w-4 h-4 text-accent-primary" />
        ) : isError ? (
          <AlertCircle className="w-4 h-4 text-accent-danger" />
        ) : (
          <Brain className="w-4 h-4 text-layer-peptide" />
        )}
      </div>

      {/* Bubble */}
      <div
        className={`max-w-[75%] rounded-2xl px-4 py-3 ${
          isUser
            ? 'bg-accent-primary/15 text-text-primary rounded-tr-sm'
            : isError
              ? 'bg-accent-danger/10 text-accent-danger rounded-tl-sm'
              : 'bg-bg-card text-text-primary rounded-tl-sm'
        }`}
      >
        <div className="text-sm leading-relaxed whitespace-pre-wrap">{message.content}</div>
        <div className="text-[10px] text-text-muted mt-1.5">
          {formatRelativeTime(message.timestamp)}
        </div>
      </div>
    </div>
  );
}
