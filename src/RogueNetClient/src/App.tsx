import { useEffect, useState } from 'react';

type ApiStatus = 'checking' | 'connected' | 'disconnected';

const apiBaseUrl = (import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:5130').replace(/\/$/, '');
const healthUrl = `${apiBaseUrl}/health`;

export default function App() {
  const [status, setStatus] = useState<ApiStatus>('checking');
  const [lastChecked, setLastChecked] = useState<Date | null>(null);

  useEffect(() => {
    let disposed = false;
    let timer: number | undefined;
    let controller: AbortController | undefined;

    const checkHealth = async () => {
      controller = new AbortController();
      const timeout = window.setTimeout(() => controller?.abort(), 3000);

      try {
        const response = await fetch(healthUrl, {
          method: 'GET',
          headers: { Accept: 'text/plain' },
          cache: 'no-store',
          signal: controller.signal,
        });
        if (!disposed) setStatus(response.ok ? 'connected' : 'disconnected');
      } catch {
        if (!disposed) setStatus('disconnected');
      } finally {
        window.clearTimeout(timeout);
        if (!disposed) {
          setLastChecked(new Date());
          timer = window.setTimeout(checkHealth, 3000);
        }
      }
    };

    void checkHealth();
    return () => {
      disposed = true;
      controller?.abort();
      if (timer !== undefined) window.clearTimeout(timer);
    };
  }, []);

  const isConnected = status === 'connected';
  const heading = status === 'checking' ? 'Checking the local API' : isConnected ? 'API connected' : 'API disconnected';
  const description = status === 'checking'
    ? 'Checking the local API…'
    : isConnected
      ? 'The RogueNet server is healthy and ready for the next stage.'
      : 'The server could not be reached. Start or restart the local stack; this page will reconnect automatically.';

  return (
    <main className="page-shell">
      <div className="ambient ambient-one" aria-hidden="true" />
      <div className="ambient ambient-two" aria-hidden="true" />
      <header className="topbar">
        <a className="brand" href="/" aria-label="RogueNet home">
          <span className="brand-mark" aria-hidden="true">R</span>
          <span>ROGUE<span className="brand-accent">NET</span></span>
        </a>
        <span className="local-tag"><span className="local-dot" /> LOCAL INSTANCE</span>
      </header>

      <section className="hero" aria-labelledby="welcome-title">
        <p className="eyebrow"><span /> A NEW DESCENT BEGINS</p>
        <h1 id="welcome-title">The dungeon<br /><em>is waiting.</em></h1>
        <p className="intro">A small world of danger, discovery, and unlikely alliances.<br className="desktop-break" /> Your local gateway is the first step below.</p>

        <div className={`connection-card ${status}`} role="status" aria-live="polite">
          <div className="status-icon" aria-hidden="true">
            {status === 'checking' ? <span className="spinner" /> : isConnected ? <span className="check">✓</span> : <span className="cross">×</span>}
          </div>
          <div className="status-copy">
            <span className="status-label">SERVER STATUS</span>
            <h2>{heading}</h2>
            <p>{description}</p>
          </div>
          <span className="status-pill">{status === 'checking' ? 'CONNECTING' : isConnected ? 'ONLINE' : 'OFFLINE'}</span>
        </div>

        <div className="connection-meta">
          <span><span className="meta-key">ENDPOINT</span><code>{healthUrl}</code></span>
          <span><span className="meta-key">LAST CHECK</span><span>{lastChecked ? lastChecked.toLocaleTimeString() : '—'}</span></span>
        </div>
      </section>

      <footer className="footer">
        <span>BUILT FOR THE LONG WAY DOWN</span>
        <span className="footer-separator">◆</span>
        <span>LOCAL PLAY · NO CLOUD REQUIRED</span>
      </footer>
    </main>
  );
}
