import { Component, type ReactNode } from 'react';
import { AlertTriangle } from 'lucide-react';

interface Props {
  children: ReactNode;
  fallback?: ReactNode;
}

interface State {
  hasError: boolean;
  error: Error | null;
}

export class ErrorBoundary extends Component<Props, State> {
  state: State = { hasError: false, error: null };

  static getDerivedStateFromError(error: Error): State {
    return { hasError: true, error };
  }

  render() {
    if (this.state.hasError) {
      if (this.props.fallback) return this.props.fallback;
      return (
        <div className="flex flex-col items-center justify-center gap-3 py-12 text-center">
          <AlertTriangle className="w-10 h-10 text-accent-danger" />
          <h3 className="text-lg font-medium text-text-primary">Something went wrong</h3>
          <p className="text-sm text-text-secondary max-w-md">{this.state.error?.message}</p>
          <button
            onClick={() => this.setState({ hasError: false, error: null })}
            className="px-4 py-2 text-sm font-medium rounded-lg bg-accent-primary hover:bg-accent-primary/80 text-white"
          >
            Try again
          </button>
        </div>
      );
    }
    return this.props.children;
  }
}
