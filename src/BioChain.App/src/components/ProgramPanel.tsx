import { useState } from 'react';
import { sql } from '../api/client';
import { createProgram, reconstruct } from '../api/reducers';
import type { Program } from '../api/types';

export function ProgramPanel({ onSelect }: { onSelect: (id: number) => void }) {
  const [programs, setPrograms] = useState<Program[]>([]);
  const [name, setName] = useState('');
  const [phase, setPhase] = useState('');
  const [domains, setDomains] = useState('neuro');
  const [log, setLog] = useState('');

  const refresh = async () => {
    try {
      const rows = await sql<Program>('SELECT * FROM program');
      setPrograms(rows);
    } catch (e: unknown) {
      setLog(String(e));
    }
  };

  const create = async () => {
    try {
      await createProgram(name, phase || null, domains.split(',').map(s => s.trim()));
      setLog(`Created program "${name}"`);
      setName('');
      await refresh();
    } catch (e: unknown) {
      setLog(String(e));
    }
  };

  const doReconstruct = async (id: number) => {
    try {
      const result = await reconstruct(id);
      setLog(`Reconstruct result:\n${result}`);
    } catch (e: unknown) {
      setLog(String(e));
    }
  };

  return (
    <div className="panel">
      <h2>Programs</h2>
      <div className="form-row">
        <input placeholder="Name" value={name} onChange={e => setName(e.target.value)} />
        <input placeholder="Phase" value={phase} onChange={e => setPhase(e.target.value)} />
        <input placeholder="Domains (comma-sep)" value={domains} onChange={e => setDomains(e.target.value)} />
        <button onClick={create}>Create</button>
        <button onClick={refresh}>Refresh</button>
      </div>

      {programs.length > 0 && (
        <table>
          <thead>
            <tr><th>ID</th><th>Name</th><th>Phase</th><th>Domains</th><th>Tick</th><th>Actions</th></tr>
          </thead>
          <tbody>
            {programs.map(p => (
              <tr key={p.id}>
                <td>{p.id}</td>
                <td>{p.name}</td>
                <td>{p.phase ?? '—'}</td>
                <td>{p.domains?.join(', ')}</td>
                <td>{p.tick}</td>
                <td>
                  <button onClick={() => onSelect(p.id)}>Select</button>
                  <button onClick={() => doReconstruct(p.id)}>Reconstruct</button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      )}

      {log && <pre className="log">{log}</pre>}
    </div>
  );
}
