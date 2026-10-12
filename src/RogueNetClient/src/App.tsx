import { useEffect, useState, type FormEvent } from 'react';

type ApiStatus = 'checking' | 'connected' | 'disconnected';
type Terrain = 'Rock' | 'Floor' | 'Wall';
type Feature = 'None' | 'ClosedDoor' | 'OpenDoor' | 'LockedDoor' | 'EntranceStairs' | 'ExitStairs' | 'Pillar' | 'Water' | 'Trap' | 'Treasure';

type DungeonTile = {
  terrain: Terrain;
  feature: Feature;
  roomId: number | null;
};

type DungeonMap = {
  seed: number;
  width: number;
  height: number;
  rooms: { id: number; category: string }[];
  connections: { fromRoomId: number; toRoomId: number; isOptionalLoop: boolean; kind: string }[];
  tiles: DungeonTile[][];
};

const apiBaseUrl = (import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:5130').replace(/\/$/, '');
const healthUrl = `${apiBaseUrl}/health`;

async function requestMap(seed?: number, signal?: AbortSignal): Promise<DungeonMap> {
  const query = seed === undefined ? '' : `?seed=${encodeURIComponent(seed)}`;
  const response = await fetch(`${apiBaseUrl}/api/maps${query}`, {
    headers: { Accept: 'application/json' },
    cache: 'no-store',
    signal,
  });
  if (!response.ok) throw new Error(`Map generation failed (HTTP ${response.status}).`);
  return response.json() as Promise<DungeonMap>;
}

function getGlyph(tile: DungeonTile): string {
  switch (tile.feature) {
    case 'ClosedDoor': return '+';
    case 'OpenDoor': return '/';
    case 'LockedDoor': return 'L';
    case 'EntranceStairs': return '<';
    case 'ExitStairs': return '>';
    case 'Pillar': return 'O';
    case 'Water': return '~';
    case 'Trap': return '^';
    case 'Treasure': return '$';
    default:
      if (tile.terrain === 'Wall') return '#';
      if (tile.terrain === 'Floor') return tile.roomId === null ? ':' : '.';
      return '\u00a0';
  }
}

function getTileClass(tile: DungeonTile): string {
  if (tile.feature !== 'None') return `tile-${tile.feature.toLowerCase()}`;
  if (tile.terrain === 'Wall') return 'tile-wall';
  if (tile.terrain === 'Floor') return tile.roomId === null ? 'tile-corridor' : 'tile-room-floor';
  return 'tile-rock';
}

const legend = [
  { glyph: '.', label: 'Room floor', className: 'tile-room-floor' },
  { glyph: ':', label: 'Corridor', className: 'tile-corridor' },
  { glyph: '#', label: 'Wall', className: 'tile-wall' },
  { glyph: '+', label: 'Closed door', className: 'tile-closeddoor' },
  { glyph: '/', label: 'Open door', className: 'tile-opendoor' },
  { glyph: 'L', label: 'Locked door', className: 'tile-lockeddoor' },
  { glyph: '< >', label: 'Stairs', className: 'tile-stairs' },
  { glyph: 'O ~ ^ $', label: 'Pillar · water · trap · treasure', className: 'tile-decor' },
];

export default function App() {
  const [apiStatus, setApiStatus] = useState<ApiStatus>('checking');
  const [lastChecked, setLastChecked] = useState<Date | null>(null);
  const [map, setMap] = useState<DungeonMap | null>(null);
  const [seedInput, setSeedInput] = useState('37007434');
  const [mapLoading, setMapLoading] = useState(true);
  const [mapError, setMapError] = useState<string | null>(null);

  useEffect(() => {
    let disposed = false;
    let timer: number | undefined;
    let controller: AbortController | undefined;

    const checkHealth = async () => {
      controller = new AbortController();
      const timeout = window.setTimeout(() => controller?.abort(), 3000);
      try {
        const response = await fetch(healthUrl, {
          headers: { Accept: 'text/plain' },
          cache: 'no-store',
          signal: controller.signal,
        });
        if (!disposed) setApiStatus(response.ok ? 'connected' : 'disconnected');
      } catch {
        if (!disposed) setApiStatus('disconnected');
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

  useEffect(() => {
    const controller = new AbortController();
    void requestMap(Number(seedInput), controller.signal)
      .then((generatedMap) => {
        setMap(generatedMap);
        setMapError(null);
      })
      .catch((error: unknown) => {
        if (error instanceof DOMException && error.name === 'AbortError') return;
        setMapError(error instanceof Error ? error.message : 'Could not generate a dungeon map.');
      })
      .finally(() => setMapLoading(false));
    return () => controller.abort();
  }, []);

  async function generateMap(seed?: number) {
    setMapLoading(true);
    setMapError(null);
    try {
      const generatedMap = await requestMap(seed);
      setMap(generatedMap);
      setSeedInput(String(generatedMap.seed));
    } catch (error) {
      setMapError(error instanceof Error ? error.message : 'Could not generate a dungeon map.');
    } finally {
      setMapLoading(false);
    }
  }

  function submitSeed(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const seed = Number(seedInput.trim());
    if (!Number.isInteger(seed) || seed < -2147483648 || seed > 2147483647) {
      setMapError('Enter a whole-number seed between -2147483648 and 2147483647.');
      return;
    }
    void generateMap(seed);
  }

  const connected = apiStatus === 'connected';

  return (
    <main className="app-shell">
      <header className="topbar">
        <a className="brand" href="/" aria-label="RogueNet home">
          <span className="brand-mark" aria-hidden="true">R</span>
          <span>ROGUE<span className="brand-accent">NET</span></span>
        </a>
        <div className={`api-indicator ${apiStatus}`} role="status" aria-live="polite">
          <span className="api-light" />
          <span>{apiStatus === 'checking' ? 'CHECKING API' : connected ? 'API ONLINE' : 'API OFFLINE'}</span>
        </div>
      </header>

      <section className="map-page" aria-labelledby="page-title">
        <div className="page-heading">
          <div>
            <p className="eyebrow"><span /> ATLAS / FIELD NOTES 001</p>
            <h1 id="page-title">Dungeon <em>survey</em></h1>
            <p className="page-description">Seeded layouts from the RogueNet map engine.</p>
          </div>
          <div className="health-meta">
            <span>HEALTH CHECK</span>
            <strong>{lastChecked ? lastChecked.toLocaleTimeString() : '—'}</strong>
          </div>
        </div>

        <div className="map-toolbar">
          <form className="seed-form" onSubmit={submitSeed}>
            <label htmlFor="seed-input">SEED</label>
            <input
              id="seed-input"
              name="seed"
              type="number"
              step="1"
              min="-2147483648"
              max="2147483647"
              value={seedInput}
              onChange={(event) => setSeedInput(event.target.value)}
              aria-label="Dungeon seed"
            />
            <button className="button button-primary" type="submit" disabled={mapLoading || apiStatus === 'disconnected'}>
              Generate seed
            </button>
          </form>
          <button className="button button-secondary" type="button" onClick={() => void generateMap()} disabled={mapLoading || apiStatus === 'disconnected'}>
            <span aria-hidden="true">↻</span> Randomize
          </button>
        </div>

        {mapError && <p className="error-message" role="alert">{mapError}</p>}

        <div className="map-layout">
          <section className="map-viewer" aria-label="Generated dungeon map">
            <div className="viewer-header">
              <div className="viewer-title"><span className="viewer-mark">✳</span><span>GENERATED LAYOUT</span></div>
              {map && <div className="map-dimensions">{map.width} × {map.height}</div>}
            </div>
            <div className="map-scroll">
              {mapLoading && !map ? (
                <div className="map-message"><span className="spinner" /> Drawing the dungeon…</div>
              ) : map ? (
                <div className="map-art" role="img" aria-label={`Dungeon map, seed ${map.seed}, ${map.width} columns by ${map.height} rows`}>
                  {map.tiles.map((row, y) => (
                    <div className="map-row" key={y} aria-hidden="true">
                      {row.map((tile, x) => (
                        <span className={`map-cell ${getTileClass(tile)}`} key={`${x}-${y}`}>{getGlyph(tile)}</span>
                      ))}
                    </div>
                  ))}
                </div>
              ) : (
                <div className="map-message">{mapError ?? 'The dungeon map is unavailable.'}</div>
              )}
              {mapLoading && map && <div className="map-refreshing" role="status">Generating…</div>}
            </div>
            <div className="viewer-footer">
              <span>SERVER GENERATED</span>
              {map && <span>SEED <strong>{map.seed}</strong></span>}
            </div>
          </section>

          <aside className="map-aside" aria-label="Map details">
            <section className="data-strip" aria-label="Generation details">
              <div><span>SEED</span><strong>{map?.seed ?? '—'}</strong></div>
              <div><span>ROOMS</span><strong>{map?.rooms.length ?? '—'}</strong></div>
              <div><span>LINKS</span><strong>{map?.connections.length ?? '—'}</strong></div>
            </section>
            <section className="legend-section" aria-labelledby="legend-heading">
              <div className="section-heading"><h2 id="legend-heading">Map key</h2><span>SYMBOLS</span></div>
              <ul className="legend-list">
                {legend.map((item) => (
                  <li key={item.label}>
                    <span className={`legend-symbol ${item.className}`}>{item.glyph}</span>
                    <span>{item.label}</span>
                  </li>
                ))}
              </ul>
            </section>
            <section className="engine-note">
              <span className="note-kicker">GENERATION RECORD</span>
              <p>{map ? `${map.rooms.length} rooms linked by ${map.connections.length} passages.` : 'Waiting for the map engine.'}</p>
              <span className="note-status"><i /> {connected ? 'ENGINE RESPONDING' : 'ENGINE UNAVAILABLE'}</span>
            </section>
          </aside>
        </div>
      </section>

      <footer className="footer">
        <span>ROGUENET / LOCAL ATLAS</span>
        <span className="footer-separator">◆</span>
        <span>LAYOUTS ARE REPRODUCIBLE BY SEED</span>
      </footer>
    </main>
  );
}
