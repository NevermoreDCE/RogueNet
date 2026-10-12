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

type Position = { x: number; y: number };
type DiveActionKind = 'Move' | 'Interact';
type DungeonDive = {
  id: string;
  seed: number;
  map: DungeonMap;
  playerPosition: Position;
  turn: number;
  lastAction: { turn: number; playerPosition: Position; succeeded: boolean; message: string } | null;
};
type Direction = 'North' | 'East' | 'South' | 'West';

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

async function requestDive(seed: number): Promise<DungeonDive> {
  const response = await fetch(`${apiBaseUrl}/api/dungeon-dives`, {
    method: 'POST',
    headers: { Accept: 'application/json', 'Content-Type': 'application/json' },
    body: JSON.stringify({ seed }),
  });
  if (!response.ok) throw new Error(`Starting the dive failed (HTTP ${response.status}).`);
  return response.json() as Promise<DungeonDive>;
}

async function requestAction(diveId: string, kind: DiveActionKind, direction: Direction): Promise<DungeonDive> {
  const response = await fetch(`${apiBaseUrl}/api/dungeon-dives/${encodeURIComponent(diveId)}/actions`, {
    method: 'POST',
    headers: { Accept: 'application/json', 'Content-Type': 'application/json' },
    body: JSON.stringify({ kind, direction }),
  });
  if (!response.ok) throw new Error(`The action failed (HTTP ${response.status}).`);
  return response.json() as Promise<DungeonDive>;
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
  const [dive, setDive] = useState<DungeonDive | null>(null);
  const [divePending, setDivePending] = useState(false);
  const [interactionDirection, setInteractionDirection] = useState<Direction>('North');

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
    if (dive) return;
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

  async function enterDive() {
    const seed = Number(seedInput.trim());
    if (!Number.isInteger(seed) || seed < -2147483648 || seed > 2147483647) {
      setMapError('Enter a whole-number seed between -2147483648 and 2147483647.');
      return;
    }

    setDivePending(true);
    setMapError(null);
    try {
      const startedDive = await requestDive(seed);
      setDive(startedDive);
      setMap(startedDive.map);
      setSeedInput(String(startedDive.seed));
    } catch (error) {
      setMapError(error instanceof Error ? error.message : 'Could not start the dive.');
    } finally {
      setDivePending(false);
    }
  }

  async function submitDiveAction(kind: DiveActionKind, direction: Direction) {
    if (!dive || divePending) return;

    setDivePending(true);
    setMapError(null);
    try {
      const updatedDive = await requestAction(dive.id, kind, direction);
      setDive(updatedDive);
      setMap(updatedDive.map);
    } catch (error) {
      setMapError(error instanceof Error ? error.message : 'Could not process the action.');
    } finally {
      setDivePending(false);
    }
  }

  const connected = apiStatus === 'connected';
  const displayedMap = dive?.map ?? map;

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
              disabled={dive !== null || divePending}
            />
            <button className="button button-primary" type="submit" disabled={mapLoading || dive !== null || apiStatus === 'disconnected'}>
              Generate seed
            </button>
          </form>
          <div className="dive-toolbar-actions">
            <button className="button button-secondary" type="button" onClick={() => void generateMap()} disabled={mapLoading || dive !== null || apiStatus === 'disconnected'}>
            <span aria-hidden="true">↻</span> Randomize
            </button>
            {dive ? (
              <button className="button button-primary" type="button" onClick={() => setDive(null)} disabled={divePending}>
                Leave dive
              </button>
            ) : (
              <button className="button button-primary" type="button" onClick={() => void enterDive()} disabled={divePending || !map || apiStatus === 'disconnected'}>
                Enter dive
              </button>
            )}
          </div>
        </div>

        {mapError && <p className="error-message" role="alert">{mapError}</p>}

        <div className="map-layout">
          <section className="map-viewer" aria-label="Generated dungeon map">
            <div className="viewer-header">
              <div className="viewer-title"><span className="viewer-mark">✳</span><span>GENERATED LAYOUT</span></div>
              {displayedMap && <div className="map-dimensions">{dive ? `TURN ${dive.turn}` : `${displayedMap.width} × ${displayedMap.height}`}</div>}
            </div>
            <div className="map-scroll">
              {mapLoading && !displayedMap ? (
                <div className="map-message"><span className="spinner" /> Drawing the dungeon…</div>
              ) : displayedMap ? (
                <div className="map-art" role="img" aria-label={`Dungeon map, seed ${displayedMap.seed}, ${displayedMap.width} columns by ${displayedMap.height} rows${dive ? `, player at ${dive.playerPosition.x}, ${dive.playerPosition.y}` : ''}`}>
                  {displayedMap.tiles.map((row, y) => (
                    <div className="map-row" key={y} aria-hidden="true">
                      {row.map((tile, x) => (
                        <span
                          className={`map-cell ${getTileClass(tile)}${dive && dive.playerPosition.x === x && dive.playerPosition.y === y ? ' tile-player' : ''}`}
                          key={`${x}-${y}`}
                        >{dive && dive.playerPosition.x === x && dive.playerPosition.y === y ? '@' : getGlyph(tile)}</span>
                      ))}
                    </div>
                  ))}
                </div>
              ) : (
                <div className="map-message">{mapError ?? 'The dungeon map is unavailable.'}</div>
              )}
              {mapLoading && displayedMap && <div className="map-refreshing" role="status">Generating…</div>}
            </div>
            <div className="viewer-footer">
              <span>{dive ? 'AUTHORITATIVE DIVE' : 'SERVER GENERATED'}</span>
              {displayedMap && <span>SEED <strong>{displayedMap.seed}</strong></span>}
            </div>
          </section>

          <aside className="map-aside" aria-label="Map details">
            <section className="data-strip" aria-label="Generation details">
              <div><span>{dive ? 'TURN' : 'SEED'}</span><strong>{dive?.turn ?? displayedMap?.seed ?? '—'}</strong></div>
              <div><span>ROOMS</span><strong>{displayedMap?.rooms.length ?? '—'}</strong></div>
              <div><span>LINKS</span><strong>{displayedMap?.connections.length ?? '—'}</strong></div>
            </section>
            {dive && (
              <>
                <section className="dive-state" aria-label="Player and action status">
                  <div><span>PLAYER POSITION</span><strong>{dive.playerPosition.x}, {dive.playerPosition.y}</strong></div>
                  <p className={dive.lastAction?.succeeded === false ? 'action-result failed' : 'action-result'} role="status" aria-live="polite">
                    {dive.lastAction?.message ?? 'You enter the dungeon.'}
                  </p>
                </section>
                <section className="dive-controls" aria-label="Dungeon actions">
                  <div className="section-heading"><h2>Actions</h2><span>{divePending ? 'PROCESSING' : 'ONE TURN'}</span></div>
                  <div className="dpad" role="group" aria-label="Move one tile">
                    <span />
                    <button type="button" aria-label="Move north" onClick={() => void submitDiveAction('Move', 'North')} disabled={divePending || !connected}>↑</button>
                    <span />
                    <button type="button" aria-label="Move west" onClick={() => void submitDiveAction('Move', 'West')} disabled={divePending || !connected}>←</button>
                    <span className="dpad-center" aria-hidden="true">·</span>
                    <button type="button" aria-label="Move east" onClick={() => void submitDiveAction('Move', 'East')} disabled={divePending || !connected}>→</button>
                    <span />
                    <button type="button" aria-label="Move south" onClick={() => void submitDiveAction('Move', 'South')} disabled={divePending || !connected}>↓</button>
                    <span />
                  </div>
                  <div className="interact-control">
                    <label htmlFor="interact-direction">DOOR DIRECTION</label>
                    <select id="interact-direction" value={interactionDirection} onChange={(event) => setInteractionDirection(event.target.value as Direction)} disabled={divePending}>
                      <option value="North">North</option>
                      <option value="East">East</option>
                      <option value="South">South</option>
                      <option value="West">West</option>
                    </select>
                    <button className="button button-secondary" type="button" onClick={() => void submitDiveAction('Interact', interactionDirection)} disabled={divePending || !connected}>
                      Interact
                    </button>
                  </div>
                </section>
              </>
            )}
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
              <p>{displayedMap ? `${displayedMap.rooms.length} rooms linked by ${displayedMap.connections.length} passages.` : 'Waiting for the map engine.'}</p>
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
