import { useState, useRef, useEffect } from 'react';
import { chatCompletion, checkVllm } from '../api/vllm';
import { loadPrompt, type PipelineStage } from '../api/prompts';
import { storeRawBnf } from '../api/reducers';

interface Message {
  role: 'user' | 'assistant' | 'system';
  content: string;
  timestamp: Date;
}

export function AgentPanel({ programId }: { programId: number }) {
  const [messages, setMessages] = useState<Message[]>([]);
  const [input, setInput] = useState('');
  const [loading, setLoading] = useState(false);
  const [vllmOk, setVllmOk] = useState<boolean | null>(null);
  const [stage] = useState<PipelineStage>('base');
  const bottomRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    checkVllm().then(setVllmOk);
  }, []);

  useEffect(() => {
    bottomRef.current?.scrollIntoView({ behavior: 'smooth' });
  }, [messages]);

  const send = async () => {
    if (!input.trim() || loading) return;
    const userMsg = input.trim();
    setInput('');

    const userMessage: Message = { role: 'user', content: userMsg, timestamp: new Date() };
    setMessages(prev => [...prev, userMessage]);
    setLoading(true);

    try {
      // Load system prompt for current stage
      const systemPrompt = await loadPrompt(stage);

      // Build messages for LLM
      const llmMessages = [
        { role: 'system' as const, content: systemPrompt },
        // Include conversation history (last 4 exchanges for context)
        ...messages.slice(-8).map(m => ({
          role: m.role as 'system' | 'user' | 'assistant',
          content: m.content,
        })),
        { role: 'user' as const, content: userMsg },
      ];

      // Call VLLM
      const bnfOutput = await chatCompletion(llmMessages);

      const assistantMessage: Message = {
        role: 'assistant',
        content: bnfOutput,
        timestamp: new Date(),
      };
      setMessages(prev => [...prev, assistantMessage]);

      // Store raw BNF in SpacetimeDB
      try {
        await storeRawBnf(programId, stage, bnfOutput);
      } catch (e) {
        const errMsg: Message = {
          role: 'system',
          content: `Failed to store BNF: ${e}`,
          timestamp: new Date(),
        };
        setMessages(prev => [...prev, errMsg]);
      }
    } catch (e) {
      const errMsg: Message = {
        role: 'system',
        content: `Error: ${e}`,
        timestamp: new Date(),
      };
      setMessages(prev => [...prev, errMsg]);
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="panel agent-panel">
      <h2>Agent — {stage.toUpperCase()} Pipeline (Program #{programId})</h2>

      <div className="status-row">
        <span className={`status-dot ${vllmOk ? 'green' : vllmOk === false ? 'red' : 'yellow'}`} />
        <span>VLLM {vllmOk ? 'connected' : vllmOk === false ? 'offline' : 'checking...'}</span>
        <button onClick={() => checkVllm().then(setVllmOk)} className="small">Refresh</button>
      </div>

      <div className="chat-messages">
        {messages.length === 0 && (
          <div className="chat-empty">
            Describe a behavioral/psychological/medical scenario.
            The agent will generate BioChain BNF notation.
          </div>
        )}
        {messages.map((msg, i) => (
          <div key={i} className={`chat-msg ${msg.role}`}>
            <span className="chat-role">{msg.role}</span>
            <pre className="chat-content">{msg.content}</pre>
          </div>
        ))}
        {loading && (
          <div className="chat-msg assistant loading">
            <span className="chat-role">assistant</span>
            <span className="chat-content">Generating BNF...</span>
          </div>
        )}
        <div ref={bottomRef} />
      </div>

      <div className="chat-input-row">
        <textarea
          value={input}
          onChange={e => setInput(e.target.value)}
          onKeyDown={e => {
            if (e.key === 'Enter' && !e.shiftKey) {
              e.preventDefault();
              send();
            }
          }}
          placeholder="Describe a scenario (e.g., 'Chronic stress with sleep disruption and anhedonia')..."
          rows={3}
          disabled={loading || !vllmOk}
        />
        <button onClick={send} disabled={loading || !vllmOk || !input.trim()}>
          {loading ? 'Generating...' : 'Send'}
        </button>
      </div>
    </div>
  );
}
