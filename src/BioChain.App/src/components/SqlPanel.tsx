import { useState } from 'react';
import { sql } from '../api/client';

export function SqlPanel() {
  const [query, setQuery] = useState('SELECT * FROM program');
  const [result, setResult] = useState('');
  const [error, setError] = useState('');

  const run = async () => {
    setError('');
    try {
      const rows = await sql(query);
      setResult(JSON.stringify(rows, null, 2));
    } catch (e: unknown) {
      setError(String(e));
      setResult('');
    }
  };

  return (
    <div className="panel">
      <h2>SQL Console</h2>
      <div className="form-row">
        <textarea
          value={query}
          onChange={e => setQuery(e.target.value)}
          rows={3}
          style={{ flex: 1, fontFamily: 'monospace' }}
        />
        <button onClick={run}>Run</button>
      </div>
      {error && <pre className="log err">{error}</pre>}
      {result && <pre className="log">{result}</pre>}
    </div>
  );
}
