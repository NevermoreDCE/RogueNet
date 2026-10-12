# RogueNet
Multiplayer Rogue clone with ASCII text UI

## Stage 1: local API and client shell

The local stack runs the ASP.NET Core API and React/Vite client with Docker Compose. Both published ports bind to loopback only: the client at `http://localhost:5173` and API at `http://localhost:5130`. The client checks `/health` every three seconds and updates its status automatically if the API is stopped or restarted.

### Prerequisites

- Docker Desktop (or Docker Engine with the Compose plugin)
- Node.js 22+ and npm, only if running the client outside Docker
- .NET 10 SDK, only if running the API outside Docker

### Start and verify

From the repository root, run `docker compose up --build`. Open `http://localhost:5173`; the shell should report **API connected**. The health endpoint is available at `http://localhost:5130/health` and returns `Healthy` with HTTP 200. The API's OpenAPI document is available at `http://localhost:5130/openapi/v1.json` in this local Development configuration.

The API accepts browser requests only from `http://localhost:5173` by default. Its Serilog request middleware writes structured request events, and application lifecycle/configuration events include named properties such as `{EnvironmentName}` and `{ClientOrigin}`.

### Stop, restart, and reset local data

- Stop the stack with `docker compose down`.
- Stop only the API with `docker compose stop api` to see the client switch to **API disconnected**. Start it again with `docker compose start api`; the client reconnects without rebuilding.
- The host `./data` directory is mounted at `/data` in the API container. `ConnectionStrings__GameDatabase` is configured as `Data Source=/data/roguenet.db`, but Stage 1 does not open the database yet; the persistent mount is ready for the character database in a later stage.
- To reset local database files after SQLite is introduced, stop the stack and remove the repository's `data` directory. On PowerShell: `Remove-Item -Recurse -Force .\data`; on macOS/Linux: `rm -rf ./data`. This deletes all local saved data.

For host-based client development, run `npm ci` and `npm run dev` from `src/RogueNetClient`. Set `VITE_API_BASE_URL` if the API is running at a different address; the default is `http://localhost:5130`.
