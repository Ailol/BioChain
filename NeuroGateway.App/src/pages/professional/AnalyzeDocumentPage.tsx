import { FileSearch } from 'lucide-react';
import { EmptyState } from '@/components/EmptyState';

export default function AnalyzeDocumentPage() {
  return (
    <div className="space-y-6">
      <h1 className="text-2xl font-semibold text-text-primary">Analyze Document</h1>
      <div className="rounded-xl bg-bg-card border border-white/5 p-12">
        <EmptyState icon={FileSearch} title="Coming Soon" description="Document analysis will be available in the next update." />
      </div>
    </div>
  );
}
