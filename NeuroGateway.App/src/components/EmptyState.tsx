import type { LucideIcon } from 'lucide-react';

interface Props {
  icon: LucideIcon;
  title: string;
  description?: string;
}

export function EmptyState({ icon: Icon, title, description }: Props) {
  return (
    <div className="flex flex-col items-center justify-center gap-3 py-12 text-center">
      <Icon className="w-10 h-10 text-text-muted" strokeWidth={1.5} />
      <h3 className="text-lg font-medium text-text-primary">{title}</h3>
      {description && <p className="text-sm text-text-secondary max-w-md">{description}</p>}
    </div>
  );
}
