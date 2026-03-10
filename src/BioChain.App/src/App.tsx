import { useState } from 'react';
import { ConnectionPanel } from './components/ConnectionPanel';
import { ProgramPanel } from './components/ProgramPanel';
import { GraphPanel } from './components/GraphPanel';
import { AgentPanel } from './components/AgentPanel';
import { SqlPanel } from './components/SqlPanel';
import { SyncPanel } from './components/SyncPanel';
import './App.css';

type Tab = 'agent' | 'graph' | 'sync' | 'sql';

function App() {
  const [connected, setConnected] = useState(false);
  const [selectedProgram, setSelectedProgram] = useState<number | null>(null);
  const [tab, setTab] = useState<Tab>('agent');

  return (
    <div className="app">
      <header>
        <h1>BioChain Module</h1>
        <span className="subtitle">SpacetimeDB + VLLM Inference</span>
      </header>

      <ConnectionPanel onConnected={() => setConnected(true)} />

      {connected && (
        <>
          <ProgramPanel onSelect={setSelectedProgram} />

          {selectedProgram !== null && (
            <>
              <div className="tab-bar">
                <button className={tab === 'agent' ? 'active' : ''} onClick={() => setTab('agent')}>Agent</button>
                <button className={tab === 'graph' ? 'active' : ''} onClick={() => setTab('graph')}>Graph</button>
                <button className={tab === 'sync' ? 'active' : ''} onClick={() => setTab('sync')}>Sync</button>
                <button className={tab === 'sql' ? 'active' : ''} onClick={() => setTab('sql')}>SQL</button>
              </div>

              {tab === 'agent' && <AgentPanel programId={selectedProgram} />}
              {tab === 'graph' && <GraphPanel programId={selectedProgram} />}
              {tab === 'sync' && <SyncPanel programId={selectedProgram} />}
              {tab === 'sql' && <SqlPanel />}
            </>
          )}

          {selectedProgram === null && <SqlPanel />}
        </>
      )}
    </div>
  );
}

export default App;
