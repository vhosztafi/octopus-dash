# Next.js on Cloudflare Workers

The frontend deploys to https://octopus-dash.vilmos.co.uk using Cloudflare Workers Builds connected to https://github.com/vhosztafi/octopus-dash. The backend is deployed separately through TeamCity to IIS at https://octopus-dash-api.vilmos.co.uk. Browsers continue to use only the frontend BFF.

## Build configuration

| Setting | Value |
|---|---|
| Worker | `octopus-dash` |
| Production branch | `master` |
| Root directory | `web` |
| Build command | `npm run ci:cloudflare` |
| Deploy command | `npm run deploy:worker` |
| Preview command (if enabled later) | `npm run upload:worker` |
| Node version | `24` (build variable `NODE_VERSION`) |
| Include watch paths | `web/*` |

Cloudflare installs dependencies from `web/package-lock.json`, runs strict TypeScript and Vitest checks, builds Next.js through the pinned OpenNext adapter, then deploys the existing output. Failed checks prevent deployment. GitHub Actions retains independent backend/frontend checks; Cloudflare performs the production build and deployment itself.

Before deployment, the build also boots workerd with synthetic credentials and checks protected routes, login rendering, security headers, signed sessions and unavailable-API handling. The smoke test uses a temporary config directory so it cannot load an owner's local `.dev.vars`.

`web/wrangler.jsonc` defines the custom domain, static assets, Node compatibility, API origin and login rate limiter. `workers.dev` and public version URLs are disabled. Preview builds remain disabled until preview credentials and data access are deliberately isolated. All dashboard fetches use `no-store`; no R2/ISR cache is required.

The pinned OpenNext 1.20.10 adapter requires the one-line manifest fix from [upstream PR 1356](https://github.com/opennextjs/opennextjs-cloudflare/pull/1356) for Next.js 16.4. `scripts/prepare-cloudflare.mjs` applies it idempotently before Worker builds and refuses unexpected adapter versions/layouts. Remove the shim when upgrading to an adapter release that includes the fix, and repeat the workerd smoke test. A patched `glob` override removes the adapter build tool's known CLI vulnerability.

## Runtime secrets

Configure these as **Secret** values in Worker Settings → Variables & Secrets. They are runtime values, not build variables or `NEXT_PUBLIC_` values:

- `APP_PASSWORD_HASH`: bcrypt hash for the production dashboard login.
- `SESSION_SECRET`: stable random string of at least 32 characters, used to sign eight-hour sessions.
- `INTERNAL_API_TOKEN`: the production bearer token already configured on the ASP.NET API. The localhost API token is different and must not be substituted.

Do not upload Octopus or Tesla credentials, SQL credentials, or the Data Protection certificate to the frontend Worker. Those stay with the API/worker deployment. Do not commit `.env`, `.dev.vars` or generated Worker output. The initial deployment denies login until the production login secrets are configured. After login is configured, unavailable backend data is shown through the existing error screen until the API is commissioned.

On 2026-10-13, the owner selected the existing localhost password for production. Its bcrypt hash was uploaded as `APP_PASSWORD_HASH` with a separate random production `SESSION_SECRET` using Wrangler's stdin secret upload. On 2026-10-14, the owner explicitly approved connecting the existing production API token. A temporary RSA4096/OAEP-SHA256 envelope carried ciphertext through TeamCity; the token was decrypted locally and uploaded through Wrangler stdin as `INTERNAL_API_TOKEN`. All three runtime secrets are configured. Fresh production browser login and dashboard API rendering succeeded. The temporary transfer step, scripts and private key were removed afterward. The password and secret values were never committed or written to build logs.

For local Worker tests, copy `web/.dev.vars.example` to the ignored `web/.dev.vars` and populate it with synthetic credentials. Run `npm run build:worker`, then `npm run preview:worker`. Ordinary `npm run dev`, `npm run build` and Docker remain supported. Edge middleware is used because the adapter's Node middleware path is experimental; Server Actions and BFF routes independently enforce sessions.

The Cloudflare login binding limits attempts across isolates within each Cloudflare location. Local Docker keeps its ten-attempt/fifteen-minute process limit. Cloudflare limit failures deny login.

## Backend follow-up

The IIS API origin uses trusted HTTPS and enforces the same internal bearer token as the Worker; authenticated status returns 200 and unauthenticated API requests return 401. The Worker successfully renders production data contracts. SQL and the Quartz worker remain private. Production Octopus credentials and Tesla OAuth/site TR20 passed connection tests. The separate Quartz Windows service is packaged but its deployment is blocked on server SQL identity provisioning; [TeamCity commissioning details](TEAMCITY.md) record the required follow-up before meter data can sync. A Cloudflare Access service token or tunnel/VPC restrictions may be added later; configure corresponding BFF headers then if required. Never disable authentication just to make the separate origin reachable.

## Operations

Push changes to `master` to trigger Cloudflare Builds. Check the Worker’s Deployments/Builds page for build and deployment status. Use Cloudflare’s previous-version rollback when needed. Keep runtime secrets stable across normal builds so signed sessions survive deployments; rotate the session key or password hash to revoke them.

Sources: [Cloudflare OpenNext guide](https://developers.cloudflare.com/workers/framework-guides/web-apps/opennext/), [OpenNext setup](https://opennext.js.org/cloudflare/get-started), [Workers Builds configuration](https://developers.cloudflare.com/workers/ci-cd/builds/configuration/), [Worker rate limiting](https://developers.cloudflare.com/workers/runtime-apis/bindings/rate-limit/).
