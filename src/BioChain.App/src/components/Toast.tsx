import { createContext, useContext, useState, useCallback, type ReactNode } from 'react';

interface Toast {
  id: number;
  message: string;
  type: 'info' | 'success' | 'error' | 'warning';
}

interface ToastCtx {
  toast: (message: string, type?: Toast['type']) => void;
}

const Ctx = createContext<ToastCtx>({ toast: () => {} });

export const useToast = () => useContext(Ctx);

let nextId = 0;

export function ToastProvider({ children }: { children: ReactNode }) {
  const [toasts, setToasts] = useState<Toast[]>([]);

  const toast = useCallback((message: string, type: Toast['type'] = 'info') => {
    const id = nextId++;
    setToasts((prev) => [...prev, { id, message, type }]);
    setTimeout(() => setToasts((prev) => prev.filter((t) => t.id !== id)), 4000);
  }, []);

  const colors = {
    info: 'bg-accent-info/20 border-accent-info text-accent-info',
    success: 'bg-accent-success/20 border-accent-success text-accent-success',
    error: 'bg-accent-danger/20 border-accent-danger text-accent-danger',
    warning: 'bg-accent-warning/20 border-accent-warning text-accent-warning',
  };

  return (
    <Ctx value={{ toast }}>
      {children}
      <div className="fixed bottom-4 right-4 z-50 flex flex-col gap-2">
        {toasts.map((t) => (
          <div
            key={t.id}
            className={`px-4 py-2 rounded-lg border text-sm animate-[fadeIn_0.2s_ease-out] ${colors[t.type]}`}
          >
            {t.message}
          </div>
        ))}
      </div>
    </Ctx>
  );
}
