import { NavLink } from 'react-router-dom';
import {
  Globe, FileSearch, Users, MessageSquare,
  Shield, Zap, GitBranch, Layers, Database, Sparkles, ClipboardList, X,
} from 'lucide-react';
import clsx from 'clsx';
import { useAuthStore } from '@/stores/authStore';
import { hasAnyRole } from '@/utils/roles';
import type { LucideIcon } from 'lucide-react';

interface NavItem {
  label: string;
  path: string;
  icon: LucideIcon;
  section: 'personal' | 'professional' | 'admin';
  requiredRoles: string[];
}

const NAV_ITEMS: NavItem[] = [
  // Personal
  { label: 'BioSphere', path: '/personal/biosphere', icon: Globe, section: 'personal', requiredRoles: ['private'] },
  { label: 'Personal Insight', path: '/personal/insight', icon: Sparkles, section: 'personal', requiredRoles: ['private'] },
  { label: 'Questionnaire', path: '/personal/questionnaire', icon: ClipboardList, section: 'personal', requiredRoles: ['private'] },
  // Professional
  { label: 'Analyze Document', path: '/professional/analyze', icon: FileSearch, section: 'professional', requiredRoles: ['work'] },
  { label: 'Candidates', path: '/professional/candidates', icon: Users, section: 'professional', requiredRoles: ['work'] },
  { label: 'Chat Analysis', path: '/professional/chat', icon: MessageSquare, section: 'professional', requiredRoles: ['work'] },
  // Admin
  { label: 'User Management', path: '/admin/users', icon: Shield, section: 'admin', requiredRoles: ['admin'] },
  { label: 'Signals', path: '/admin/signals', icon: Zap, section: 'admin', requiredRoles: ['admin'] },
  { label: 'Interactions', path: '/admin/interactions', icon: GitBranch, section: 'admin', requiredRoles: ['admin'] },
  { label: 'Dimensions', path: '/admin/dimensions', icon: Layers, section: 'admin', requiredRoles: ['admin'] },
  { label: 'Embeddings', path: '/admin/embeddings', icon: Database, section: 'admin', requiredRoles: ['admin'] },
];

const SECTION_LABELS: Record<string, string> = {
  personal: 'Personal',
  professional: 'Professional',
  admin: 'Admin',
};

interface Props {
  open: boolean;
  onClose: () => void;
}

export function Sidebar({ open, onClose }: Props) {
  const { effectiveRoles } = useAuthStore();

  const visibleItems = NAV_ITEMS.filter((item) =>
    hasAnyRole(effectiveRoles, item.requiredRoles)
  );

  const sections = ['personal', 'professional', 'admin'] as const;

  return (
    <>
      {/* Mobile backdrop */}
      {open && (
        <div
          className="fixed inset-0 bg-black/50 z-40 lg:hidden"
          onClick={onClose}
        />
      )}

      <aside
        className={clsx(
          'fixed lg:static inset-y-0 left-0 z-50 w-56 bg-bg-secondary border-r border-white/5 flex flex-col shrink-0 transition-transform lg:translate-x-0',
          open ? 'translate-x-0' : '-translate-x-full',
        )}
      >
        {/* Mobile close */}
        <div className="flex items-center justify-between p-4 lg:hidden">
          <span className="text-sm font-semibold text-text-primary">Navigation</span>
          <button onClick={onClose} className="text-text-muted hover:text-text-primary">
            <X className="w-4 h-4" />
          </button>
        </div>

        <nav className="flex-1 overflow-y-auto py-3 px-2">
          {sections.map((section) => {
            const sectionItems = visibleItems.filter((i) => i.section === section);
            if (sectionItems.length === 0) return null;

            return (
              <div key={section} className="mb-4">
                <div className="px-3 mb-1.5 text-[10px] font-semibold uppercase tracking-wider text-text-muted">
                  {SECTION_LABELS[section]}
                </div>
                {sectionItems.map((item) => (
                  <NavLink
                    key={item.path}
                    to={item.path}
                    onClick={onClose}
                    className={({ isActive }) =>
                      clsx(
                        'flex items-center gap-2.5 px-3 py-2 rounded-lg text-[13px] font-medium transition-colors',
                        isActive
                          ? 'bg-accent-primary/10 text-accent-primary'
                          : 'text-text-secondary hover:bg-bg-hover hover:text-text-primary',
                      )
                    }
                  >
                    <item.icon className="w-4 h-4 shrink-0" />
                    {item.label}
                  </NavLink>
                ))}
              </div>
            );
          })}
        </nav>

        <div className="p-3 border-t border-white/5">
          <div className="text-[10px] text-text-muted text-center">NeuroReact v0.1</div>
        </div>
      </aside>
    </>
  );
}
