import { useEffect, useState } from 'react';
import { ChevronDown, Plus, User } from 'lucide-react';
import { usePersonStore } from '@/stores/personStore';
import { personsApi } from '@/api/persons';

export function PersonSelector() {
  const { activePerson, personList, isLoading, setActivePerson, fetchPersonList } = usePersonStore();
  const [open, setOpen] = useState(false);
  const [creating, setCreating] = useState(false);
  const [newName, setNewName] = useState('');

  useEffect(() => {
    fetchPersonList();
  }, [fetchPersonList]);

  const handleCreate = async () => {
    if (!newName.trim()) return;
    try {
      await personsApi.create({ name: newName.trim() });
      setNewName('');
      setCreating(false);
      await fetchPersonList();
      setActivePerson(newName.trim());
    } catch {
      // toast error
    }
  };

  return (
    <div className="relative">
      <button
        onClick={() => setOpen(!open)}
        className="flex items-center gap-2 px-3 py-1.5 rounded-lg bg-bg-secondary border border-white/10 hover:border-white/20 text-sm transition-colors"
      >
        <User className="w-3.5 h-3.5 text-text-muted" />
        <span className="text-text-primary">{activePerson ?? 'Select person'}</span>
        <ChevronDown className="w-3.5 h-3.5 text-text-muted" />
      </button>

      {open && (
        <>
          <div className="fixed inset-0 z-40" onClick={() => setOpen(false)} />
          <div className="absolute right-0 top-full mt-1 w-56 bg-bg-card border border-white/10 rounded-lg shadow-xl z-50 py-1">
            {isLoading && <div className="px-3 py-2 text-xs text-text-muted">Loading...</div>}
            {personList.map((name) => (
              <button
                key={name}
                onClick={() => { setActivePerson(name); setOpen(false); }}
                className={`w-full text-left px-3 py-2 text-sm hover:bg-bg-hover transition-colors ${
                  name === activePerson ? 'text-accent-primary' : 'text-text-primary'
                }`}
              >
                {name}
              </button>
            ))}
            <div className="border-t border-white/5 mt-1 pt-1">
              {creating ? (
                <div className="px-3 py-2 flex gap-2">
                  <input
                    value={newName}
                    onChange={(e) => setNewName(e.target.value)}
                    onKeyDown={(e) => e.key === 'Enter' && handleCreate()}
                    placeholder="Name..."
                    className="flex-1 px-2 py-1 text-sm bg-bg-secondary border border-white/10 rounded text-white"
                    autoFocus
                  />
                  <button onClick={handleCreate} className="text-xs text-accent-primary font-medium">Add</button>
                </div>
              ) : (
                <button
                  onClick={() => setCreating(true)}
                  className="w-full text-left px-3 py-2 text-sm text-text-muted hover:text-text-primary hover:bg-bg-hover flex items-center gap-2"
                >
                  <Plus className="w-3.5 h-3.5" /> New person
                </button>
              )}
            </div>
          </div>
        </>
      )}
    </div>
  );
}
