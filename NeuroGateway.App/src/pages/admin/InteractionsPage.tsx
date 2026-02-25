import { GitBranch } from 'lucide-react';
import { EmptyState } from '@/components/EmptyState';

export default function InteractionsPage() {
  return (
    <div className="space-y-6">
      <h1 className="text-2xl font-semibold text-text-primary">Interactions</h1>
      <div className="rounded-xl bg-bg-card border border-white/5 p-12">
        <EmptyState icon={GitBranch} title="Coming Soon" description="Interaction management will be available in the next update." />
      </div>
    </div>
  );
}
