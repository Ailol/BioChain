import { Users } from 'lucide-react';
import { EmptyState } from '@/components/EmptyState';

export default function CandidatesPage() {
  return (
    <div className="space-y-6">
      <h1 className="text-2xl font-semibold text-text-primary">Candidates</h1>
      <div className="rounded-xl bg-bg-card border border-white/5 p-12">
        <EmptyState icon={Users} title="Coming Soon" description="Candidate management will be available in the next update." />
      </div>
    </div>
  );
}
