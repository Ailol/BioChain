import { type ReactNode } from 'react';
import { Link, useLocation } from 'react-router-dom';
import { MessageCircle, ClipboardList, Brain } from 'lucide-react';

const navItems = [
  { to: '/', icon: MessageCircle, label: 'Chat' },
  { to: '/questionnaire', icon: ClipboardList, label: 'Questionnaire' },
];

export function Layout({ children }: { children: ReactNode }) {
  const { pathname } = useLocation();

  return (
    <div className="flex h-screen overflow-hidden">
      {/* Sidebar */}
      <nav className="w-16 bg-bg-secondary border-r border-bg-hover flex flex-col items-center py-4 gap-1 shrink-0">
        <div className="mb-6 flex items-center justify-center w-10 h-10 rounded-xl bg-accent-primary/20">
          <Brain className="w-5 h-5 text-accent-primary" />
        </div>
        {navItems.map(({ to, icon: Icon, label }) => {
          const active = pathname === to || (to !== '/' && pathname.startsWith(to));
          return (
            <Link
              key={to}
              to={to}
              title={label}
              className={`w-10 h-10 rounded-xl flex items-center justify-center transition-colors ${
                active
                  ? 'bg-accent-primary/20 text-accent-primary'
                  : 'text-text-muted hover:text-text-secondary hover:bg-bg-hover'
              }`}
            >
              <Icon className="w-5 h-5" />
            </Link>
          );
        })}
      </nav>

      {/* Content */}
      <main className="flex-1 overflow-hidden">{children}</main>
    </div>
  );
}
