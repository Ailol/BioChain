import { Shield } from 'lucide-react';
import { EmptyState } from '@/components/EmptyState';

export default function UserManagementPage() {
  return (
    <div className="space-y-6">
      <h1 className="text-2xl font-semibold text-text-primary">User Management</h1>
      <div className="rounded-xl bg-bg-card border border-white/5 p-12">
        <EmptyState icon={Shield} title="Coming Soon" description="User management will be available in the next update." />
      </div>
    </div>
  );
}
