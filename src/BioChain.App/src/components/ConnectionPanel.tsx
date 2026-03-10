import { useState } from 'react';
import { setConfig, getConfig, checkConnection } from '../api/client';

export function ConnectionPanel({ onConnected }: { onConnected: () => void }) {
  const cfg = getConfig();
  const [host, setHost] = useState(cfg.host);
  const [database, setDatabase] = useState(cfg.database);
  const [status, setStatus] = useState<'idle' | 'connecting' | 'ok' | 'error'>('idle');
  const [error, setError] = useState('');

  const connect = async () => {
    setStatus('connecting');
    setConfig({ host, database });
    const ok = await checkConnection();
    if (ok) {
      setStatus('ok');
      onConnected();
    } else {
      setStatus('error');
      setError('Could not reach SpacetimeDB. Is the server running?');
    }
  };

  return (
    <div className="panel">
      <h2>Connect to SpacetimeDB</h2>
      <div className="form-row">
        <label>Host</label>
        <input value={host} onChange={e => setHost(e.target.value)} />
      </div>
      <div className="form-row">
        <label>Database</label>
        <input value={database} onChange={e => setDatabase(e.target.value)} />
      </div>
      <button onClick={connect} disabled={status === 'connecting'}>
        {status === 'connecting' ? 'Connecting...' : 'Connect'}
      </button>
      {status === 'ok' && <span className="badge ok">Connected</span>}
      {status === 'error' && <span className="badge err">{error}</span>}
    </div>
  );
}
