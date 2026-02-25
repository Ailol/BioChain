import { Zap } from 'lucide-react';
import { EmptyState } from '@/components/EmptyState';

export default function SignalsPage() {
  return (
    <div className="space-y-6">
      <h1 className="text-2xl font-semibold text-text-primary">Signals</h1>
      <div className="rounded-xl bg-bg-card border border-white/5 p-12">
        <EmptyState icon={Zap} title="Coming Soon" description="Signal management will be available in the next update." />
      </div>
    </div>
  );
}
