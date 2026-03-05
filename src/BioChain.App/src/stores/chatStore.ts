import { create } from 'zustand';
import type { ChatMessage, ChatHistoryItem } from '@/types';
import { chatApi } from '@/api/chat';
import { analyzeApi } from '@/api/analyze';

interface ChatState {
  subjectId: string;
  messages: ChatMessage[];
  isLoading: boolean;
  analysisCount: number;

  setSubjectId: (id: string) => void;
  sendMessage: (content: string) => Promise<void>;
  clearMessages: () => void;
}

let msgId = 0;

export const useChatStore = create<ChatState>((set, get) => ({
  subjectId: '',
  messages: [],
  isLoading: false,
  analysisCount: 0,

  setSubjectId: (id) => set({ subjectId: id }),

  sendMessage: async (content: string) => {
    const { subjectId, messages } = get();

    const userMsg: ChatMessage = {
      id: `msg-${msgId++}`,
      role: 'user',
      content,
      timestamp: new Date(),
    };
    set({ messages: [...messages, userMsg], isLoading: true });

    // Background analysis (fire-and-forget)
    analyzeApi
      .analyze({ subjectId, text: content, kind: 'chat' })
      .then(() => set((s) => ({ analysisCount: s.analysisCount + 1 })))
      .catch(() => {}); // non-fatal

    // Build history for the LLM (last 20 messages)
    const allMsgs = [...messages, userMsg];
    const history: ChatHistoryItem[] = allMsgs
      .filter((m) => m.role === 'user' || m.role === 'assistant')
      .slice(-20)
      .map((m) => ({ role: m.role as 'user' | 'assistant', content: m.content }));

    try {
      const response = await chatApi.send({
        subjectId,
        message: content,
        history: history.slice(0, -1), // exclude current message (sent as `message`)
      });

      const assistantMsg: ChatMessage = {
        id: `msg-${msgId++}`,
        role: 'assistant',
        content: response.response,
        timestamp: new Date(),
      };
      set((s) => ({
        messages: [...s.messages, assistantMsg],
        isLoading: false,
      }));
    } catch (err) {
      const errorMsg: ChatMessage = {
        id: `msg-${msgId++}`,
        role: 'system',
        content: `Error: ${err instanceof Error ? err.message : 'Failed to get response'}`,
        timestamp: new Date(),
      };
      set((s) => ({
        messages: [...s.messages, errorMsg],
        isLoading: false,
      }));
    }
  },

  clearMessages: () => set({ messages: [], analysisCount: 0 }),
}));
