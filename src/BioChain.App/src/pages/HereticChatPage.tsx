import { useEffect, useRef } from 'react';
import { useNavigate } from 'react-router-dom';
import { useChatStore } from '@/stores/chatStore';
import { ChatMessage } from '@/components/chat/ChatMessage';
import { ChatInput } from '@/components/chat/ChatInput';
import { TypingIndicator } from '@/components/chat/TypingIndicator';
import { Brain, Trash2, Activity, ClipboardList } from 'lucide-react';

export default function HereticChatPage() {
  const navigate = useNavigate();
  const { subjectId, messages, isLoading, analysisCount, sendMessage, clearMessages } = useChatStore();
  const bottomRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    bottomRef.current?.scrollIntoView({ behavior: 'smooth' });
  }, [messages, isLoading]);

  // No subject created yet — prompt to take questionnaire
  if (!subjectId) {
    return (
      <div className="flex items-center justify-center h-full">
        <div className="text-center space-y-4 max-w-sm">
          <div className="w-16 h-16 rounded-2xl bg-layer-peptide/10 flex items-center justify-center mx-auto">
            <Brain className="w-8 h-8 text-layer-peptide/60" />
          </div>
          <h2 className="text-lg font-medium text-text-primary">Welcome to Heretic</h2>
          <p className="text-sm text-text-secondary leading-relaxed">
            Create your profile and take the NeuroMap-18 questionnaire first.
            This builds your initial biochemical profile so Heretic can understand you.
          </p>
          <button
            onClick={() => navigate('/questionnaire')}
            className="flex items-center gap-2 mx-auto px-4 py-2 bg-accent-primary text-white rounded-xl text-sm font-medium hover:opacity-90 transition-opacity"
          >
            <ClipboardList className="w-4 h-4" />
            Take Questionnaire
          </button>
        </div>
      </div>
    );
  }

  return (
    <div className="flex flex-col h-full">
      {/* Header */}
      <header className="flex items-center justify-between px-6 py-3 border-b border-bg-hover bg-bg-secondary/50 shrink-0">
        <div className="flex items-center gap-3">
          <div className="w-8 h-8 rounded-lg bg-layer-peptide/20 flex items-center justify-center">
            <Brain className="w-4 h-4 text-layer-peptide" />
          </div>
          <div>
            <h1 className="text-sm font-medium text-text-primary">Heretic</h1>
            <p className="text-[11px] text-text-muted">Understands through simulation</p>
          </div>
        </div>
        <div className="flex items-center gap-3">
          {analysisCount > 0 && (
            <div className="flex items-center gap-1.5 px-2.5 py-1 rounded-full bg-accent-success/10 text-accent-success text-[11px]">
              <Activity className="w-3 h-3" />
              {analysisCount} analyzed
            </div>
          )}
          <button
            onClick={clearMessages}
            title="Clear chat"
            className="w-8 h-8 rounded-lg flex items-center justify-center text-text-muted hover:text-text-secondary hover:bg-bg-hover transition-colors"
          >
            <Trash2 className="w-4 h-4" />
          </button>
        </div>
      </header>

      {/* Messages */}
      <div className="flex-1 overflow-y-auto px-6 py-4 space-y-4">
        {messages.length === 0 && (
          <div className="flex flex-col items-center justify-center h-full text-center gap-4">
            <div className="w-16 h-16 rounded-2xl bg-layer-peptide/10 flex items-center justify-center">
              <Brain className="w-8 h-8 text-layer-peptide/60" />
            </div>
            <div className="space-y-2 max-w-md">
              <h2 className="text-lg font-medium text-text-primary">Talk to Heretic</h2>
              <p className="text-sm text-text-secondary leading-relaxed">
                Have a natural conversation. Each message you send is analyzed in the background to
                build a deeper understanding of your neurochemistry. The more you share, the more
                insightful the responses become.
              </p>
              <div className="flex flex-wrap gap-2 justify-center pt-2">
                {[
                  "I've been feeling really wired lately",
                  'My sleep has been terrible',
                  "I can't seem to focus on anything",
                  'Exercise has been helping my mood',
                ].map((suggestion) => (
                  <button
                    key={suggestion}
                    onClick={() => sendMessage(suggestion)}
                    className="px-3 py-1.5 rounded-full bg-bg-card border border-bg-hover text-xs text-text-secondary hover:text-text-primary hover:border-accent-primary/30 transition-colors"
                  >
                    {suggestion}
                  </button>
                ))}
              </div>
            </div>
          </div>
        )}

        {messages.map((msg) => (
          <ChatMessage key={msg.id} message={msg} />
        ))}

        {isLoading && <TypingIndicator />}

        <div ref={bottomRef} />
      </div>

      {/* Input */}
      <div className="px-6 py-3 border-t border-bg-hover bg-bg-secondary/50 shrink-0">
        <ChatInput onSend={sendMessage} disabled={isLoading} />
        <p className="text-[10px] text-text-muted text-center mt-2">
          Each message is analyzed to refine your biochemical profile
        </p>
      </div>
    </div>
  );
}
