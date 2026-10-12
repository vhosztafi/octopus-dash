# Home energy

A self-hosted Octopus Energy + Tesla Powerwall dashboard. Next.js is the authenticated backend-for-frontend; ASP.NET Core and a Quartz worker share a SQL Server database. Day, half-hour ledger, weekly report and settings follow the supplied prototype.

## Run locally with sample data

Requires .NET 10 and Node 24. Prices in sample mode come only from the supplied prototype; credentials cannot be saved and notifications cannot be sent in this mode.

```powershell
dotnet restore api/EnergyDash.sln
npm ci --prefix web
# Terminal 1
$env:SampleMode = 'true'
$env:INTERNAL_API_TOKEN = 'local-sample-internal-token-at-least-32-characters'
dotnet run --project api/src/EnergyDash.Api --urls http://localhost:5080
# Terminal 2, from web/
$env:API_BASE_URL = 'http://localhost:5080'
$env:INTERNAL_API_TOKEN = 'local-sample-internal-token-at-least-32-characters'
$env:SESSION_SECRET = 'local-sample-session-secret-at-least-32-characters'
$env:APP_PASSWORD_HASH = node -e "process.stdout.write(require('bcryptjs').hashSync('home-energy-sample',12))"
npm run dev
```

Open http://localhost:3000 and use `home-energy-sample` for this explicit local preview only. All data is marked **Sample figures**. Production mode never falls back to fixtures.

## Self-host with real data

1. Use an x64 host with Docker Linux containers and enough RAM for SQL Server. Copy `.env.example` to `.env`.
2. Choose a password; generate its bcrypt hash with `node -e "require('bcryptjs').hash(process.argv[1],12).then(console.log)" "your-password"` from `web/`. Set `APP_PASSWORD_HASH` in single quotes in `.env`. Generate distinct cryptographically random 32+ character `SESSION_SECRET` and `INTERNAL_API_TOKEN`.
3. Create a certificate with its private key in PFX format for Data Protection (for example with OpenSSL) and set its path and password. Mount it read-only; ensure the container app user can read it. Keep this certificate backed up outside the repository.
4. Set a strong SQL SA password. Provider credentials can be environment variables or saved encrypted in Settings. Environment values override saved credentials. Tesla requires a customer-authorized refresh token and app client ID with `energy_device_data` and `offline_access`; app client credentials alone cannot read household data.
5. `docker compose up --build -d`. API applies the committed EF migration; worker starts after SQL/API health. Only the web port is published, on loopback. Put an HTTPS reverse proxy in front of the web app. Session cookies are always Secure. Use LAN/Tailscale access; do not publish SQL or the API.
6. In Settings, save/test connections, select the Tesla energy site, set the SmartFlex device ID, choose gas meter units/calorific value and press **Sync now**. Sync runs through a persistent SQL command queue polled every 15 seconds. The initial backfill defaults to 90 days; subsequent meter ingestion re-fetches 7 days.

The deployment currently assumes one worker process and one web process. Quartz serializes the pipeline's jobs. SQL application locks serialize OAuth refresh across API and worker. Supplied dual-register night start/end settings apply only to day/night tariffs; time-of-use standard rates determine their own windows.

## Checks

```powershell
dotnet test api/EnergyDash.sln
# Real SQL tests: requires Docker Linux engine
$env:RUN_SQL_TESTS = '1'
dotnet test api/tests/EnergyDash.Integration.Tests
# From web/
npm run typecheck
npm test
npm run build
```

Refresh the generated TypeScript contract after API changes: start the API, set `API_BASE_URL` and `INTERNAL_API_TOKEN`, then run `npm run generate:api` from `web/`. The committed `lib/api/schema.d.ts` and `openapi.json` are generated from ASP.NET Core; frontend DTOs are aliases to these types.

See [PROGRESS.md](PROGRESS.md) for verification and commissioning blockers, and [operations](docs/OPERATIONS.md) for backup/security and alert delivery behavior.
