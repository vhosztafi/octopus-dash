# Implementation progress

## 2026-10-12 — initialization
Read the complete prototype brief and HTML reference. Initialized GSD context, requirements and eight-phase roadmap using the user's autonomous authorization. Research is performed inline under the skill's Codex spawn restriction.

### Blockers / follow-up commissioning
- Docker CLI installed; Linux daemon unavailable. SQL Testcontainers verification is pending until it is running.
- No Octopus/Tesla credentials: authenticated schema introspection, real redacted fixtures and account commissioning cannot be executed. Clients will introspect when configured.
- Package registry DNS is restricted in the sandbox; retrying dependency access with the supported escalation mechanism.

### Verified differences from brief
- Current SmartFlex query takes deviceId; legacy plannedDispatches is deprecated. Support explicit device ID and schema-driven fallback.
- Tesla customer data requires an authorized refresh token and app client ID. Client credentials alone are not a customer-data login.
- Next.js current convention is proxy.ts; security semantics remain unchanged with additional checks in actions/BFF.

## Milestone 1 — scaffold and sample UI
Implemented the four-project .NET solution, xUnit projects, Next.js App Router, Tailwind v4 tokens, next/font Archivo/JetBrains Mono, four responsive screens and explicit sample API. TypeScript DTOs are generated from actual ASP.NET OpenAPI. Browser verified desktop at 1440px and containment of all four pages at 390px. Saved screenshots are under output/playwright/. Pixel-perfect equality is not asserted; required settings additions, honest sample wording and exact decimal rounding differ from the mock.

## Milestone 2 — EF and Octopus
Committed the complete SQL Server code-first migration and configurations (UTC datetime2(0), decimal(18,6), natural-key indexes). Account/agreements, standard/day/night rates, standing charges, seven-day refresh/initial configurable history, pagination guard and sync queue implemented. Model and real-client pagination fixtures pass. EF reports no pending model changes; idempotent migration SQL generated. Fresh SQL container/upsert execution is pending: Docker Linux engine remains unavailable.

## Milestone 3 — pricing
Pure NodaTime/decimal engine passes 18 Core cases, including 46/48/50-slot days, dates/rate changes, partial overlap, allowance boundaries, missing readings/rates, gas conversion, dual-register and open-ended rates. Materialization replaces both fuels in one day transaction. Real DB queries, latest-complete selection, filtered ledger and CSV are implemented. Missing values remain null and totals are explicitly partial. SQL execution and actual household data remain commissioning work.

## Milestone 4 — dispatches
GraphQL caches Kraken tokens, introspects the actual root and exposed query fields, uses SmartFlex device ID, and supports legacy queries only if the live schema exposes them. Persistent snapshots retain first/last seen. Per-device midday-to-midday allowance is opt-in and isolated. No private credential was supplied, so authenticated schema/dispatch verification is pending.

Current official cap explanation distinguishes EV charging from household pricing, including the normal off-peak window: https://octopus.energy/blog/intelligent-octopus-go-smarter-charging-for-a-greener-grid/. The brief's whole-house approximation is preserved behind TODO(verify), not presented as a verified billing guarantee. Active charging/device disaggregation is a follow-up.

## Milestone 5 — Tesla
Implemented customer OAuth refresh rotation with a SQL lock/shared encrypted token store, site picker, live/reserve status and Wh history normalization. Live import uses positive-power trapezoids split at slot boundaries; gaps suppress unsafe estimates. Coarse history is rejected rather than relabelled as half-hours. Unit/fixture tests pass. Real payload interval granularity, site response fields and OAuth commissioning await credentials. Live polling has its own Quartz JobKey so historical pricing cannot delay the one-minute cadence.

## Milestone 6 — weekly and gas
Real Monday–Sunday query reuses day data; previous-week comparisons require full coverage. Cost stack, gas-limit bars, peak ledger and battery savings implemented. Gas re-pricing uses retained raw units and current calorific value. Sample weekly numbers reproduce the supplied prototype; real data is never substituted with sample figures. API/browser checks pass.

## Milestone 7 — alerts and reports
Implemented event/rule dedupe, cooldown, early/full warnings, gas limits and next-day reconciliation. MailKit/ntfy implement the notifier interface. Weekly Razor email uses the same weekly query and optional CSV. SQL application lock prevents overlapping scheduled/manual report sends; Sending is persisted before SMTP. Independent report schedule retries incomplete data during its configured day. No real message was sent. External notification destination verification, durable outbox/retry and interrupted-report recovery are documented follow-ups.

## Milestone 8 — security and operations
Implemented bcrypt login, bounded attempts, signed eight-hour httpOnly/Secure/Lax sessions, password-change revocation without placing the password hash in the token, proxy plus server/action authentication, constant-time internal bearer checks, certificate-protected shared Data Protection keys, environment overrides, safe connection hints, validation and sanitized errors. Private Docker network, SQL volume, health checks, CI and backup/restore notes included. Only the loopback web port is published in Compose.

### Final verification — 2026-10-12
- .NET Release solution build: zero warnings/errors.
- xUnit: 18 Core + 27 API/provider/model tests passed; one real SQL container test skipped explicitly.
- Frontend: strict TypeScript, 7 vitest tests and Next.js production build passed.
- Production npm audit and NuGet transitive vulnerability audit: no known vulnerabilities.
- OpenAPI → TypeScript generation succeeded; EF model/migration consistency and Compose config validation passed.
- Browser: secure login, URL slot selection/detail, over-limit filter/totals, authenticated CSV download, inline settings errors, persisted threshold reflected on Day and responsive overflow containment verified. Console errors absent during checked flows. Screenshot artifacts saved locally; no public deployment performed.
- Final standalone sample preview available at http://localhost:3000. Development-only password: home-energy-sample. Run instructions in README.md.

### Remaining follow-up work
1. Start Docker Linux engine and run RUN_SQL_TESTS=1 plus a real Compose smoke test, including certificate sharing.
2. Commission private Octopus/Tesla accounts and capture genuinely redacted response fixtures (current fixtures are synthetic official-field examples).
3. Verify account-specific SmartFlex active charging/cap semantics before enabling the allowance approximation.
4. Verify configured SMTP/ntfy destinations and delivery recovery. Single-worker/single-web deployment is assumed.

### Timestamp regression close-out
Explicit supplier offsets (including +01:00 on BST and repeated autumn hours) are normalized through DateTimeOffset to UTC before REST/GraphQL domain ingestion. Added provider regression passes; final total is 45 backend plus 7 frontend tests, with one SQL container test skipped.

## 2026-10-12 — live local setup (quick task 261012-el1)
Docker Desktop is now available. Generated unique local login/SQL/session/internal secrets and a Data Protection PFX, stored in ignored/private .env and secrets/ paths. Created an idempotency-guarded local initializer and ensured public/ exists on a clean checkout for Docker builds.

All four Compose services are built and healthy. Only the web port is published on 127.0.0.1:3000. API applied the SQL migration; worker shares the certificate and database key ring. The previous sample API/web processes belonging to this task were replaced.

The previously skipped SQL integration test passed, including migration, repeated ingest/re-pricing and an encrypted synthetic-secret round trip across separate API/worker service providers. Full backend tests now pass: 18 Core + 28 integration, no skips. Live API reports sample=false; credential routes validate rather than reject sample mode; unauthorized requests remain 401. Browser login and empty credential forms are verified. Accounts without credentials now correctly display Not connected.

The current tab is signed into http://localhost:3000/settings. New sessions use the generated password in secrets/local-access.txt. Actual provider credentials were not entered; supplier connection, active-charging/cap and notification commissioning remain pending. Earlier Docker/certificate/SQL blockers are resolved by this setup.

## 2026-10-13 — Octopus live sync repair
Owner-supplied Octopus credentials verified through the private REST account test. Discovery previously stopped on a historical FLAT2R tariff and the failure log saved partially tracked entities. Fixed product parsing, transactional/idempotent discovery and failure tracking cleanup. New fuel discovery now receives its own configured initial history. Saving credentials queues account discovery and sync, and invalidates the cached Kraken token. Source health uses the latest log per source rather than a global limit dominated by one-minute Tesla polling.

The current four-rate IOG tariff returns no standard-unit rates despite its E-1R code. The importer now follows the supplier catalogue's published day/night relations. Live ingestion completed with 4,322 electricity + 4,267 gas readings, 281 dated rates and three standing charges. 2026-10-11 has complete half-hour readings and tariff pricing for both fuels; the following day remains mostly missing at the supplier. Missing readings remain null.

Authenticated GraphQL token verification succeeded, but dispatch ingestion requires a SmartFlex device ID that is not configured. The dashboard now explicitly marks relevant costs partial and warns that extra discounted charging slots may be missing. Device discovery, historical dispatch coverage, four-rate EV disaggregation and account-specific cap verification remain follow-up work; Tesla and notification commissioning also remain pending.

Validation: 18 Core + 34 integration tests passed, no skips, including real SQL atomic discovery, repeated ingestion, per-fuel backfill and health regressions. Seven frontend tests, strict TypeScript and production builds of all services passed. Live account, rates and consumption stages completed successfully. Debug record: .planning/debug/resolved/octopus-sync.md. No credentials, account numbers or meter identifiers were logged or committed.

## 2026-10-13 — Cloudflare frontend CI/CD (quick task 261013-ew7)
Added locked OpenNext/Wrangler integration, Worker custom domain and bindings, Cloudflare CI/deploy scripts, shared per-location login throttling and operations documentation. The pinned Next.js 16.4 adapter needed the upstream preview-props manifest fix; the guarded, idempotent shim applies it before builds. A glob override resolves the adapter build-tool vulnerability. Edge middleware preserves authentication without relying on experimental Node middleware support.

Cloudflare Builds is connected to vhosztafi/octopus-dash, production master, root web, Node 24, frontend-only include path web/*. Build command npm run ci:cloudflare runs typecheck, nine tests, production Worker build and synthetic workerd auth/render checks. Deploy command npm run deploy:worker publishes the verified output. Existing Git integration/build token reused; preview builds disabled. Source commit a34330f pushed successfully.

Hosted build f92183dc-e916-43ba-8622-5f9c16034cb4 completed Ready in 1m13s; version ef23aef8 receives 100% traffic. https://octopus-dash.vilmos.co.uk is verified over trusted HTTPS, / redirects to /login, and /login returns 200 with security headers. Local build/smoke/dry-run passed; compressed Worker approximately 1.25 MiB; npm audit zero known vulnerabilities.

Production APP_PASSWORD_HASH, SESSION_SECRET and INTERNAL_API_TOKEN still need secure runtime configuration; the initial Worker denies login until configured. The future API origin is https://octopus-dash-api.vilmos.co.uk, intentionally deferred. Local supplier credentials, SQL and Data Protection keys remain with the private backend. Deployment/setup details are in docs/CLOUDFLARE.md.

Automatic Git trigger verified on frontend commit f4e8964: Cloudflare hosted build 22a39124-344e-4425-9566-ce5ccfdb2c93 completed successfully in about one minute; active version 02d1f433 at 100% traffic. GitHub backend/frontend checks passed. Fixed the pre-existing intermittent session-test mutation (occasionally replaced a character with itself) using a deterministic decoded signature bit flip. Worker static assets retain nosniff protection. Final HTTPS login still returns 200. Screenshot evidence saved in ignored output/cloudflare/deployment.png.

## TeamCity API pipeline — 2026-10-13

Created https://teamcity.vilmos.co.uk/project/OctopusDash from master with read-only Kotlin DSL and Windows api-ci/api-deploy configurations following Oktoberfest TR20. CI publishes a self-contained IIS API ZIP and checksum/revision manifest; successful builds automatically trigger one-at-a-time deployment using strict same-chain artifacts. Server scripts validate the explicit IIS binding/path, preserve private production settings/PFX outside www, use integrated SQL security for . / OctopusDash_Live, run startup EF migrations, retain backups and verify HTTPS database health/bearer authentication with file rollback on failure. No local supplier credentials are transferred.

Local verification: all 52 backend tests passed with real SQL and certificate key-ring sharing; Windows publish, PowerShell AST and artifact preflight passed. TeamCity CI #1 (7399) passed and published artifacts, then automatically triggered deployment #1 (7401). Deployment stopped before touching server files or SQL because no exact HTTPS binding for octopus-dash-api.vilmos.co.uk was found on the existing deploy-agent-zenx.

Final diagnostics: CI #2 (7403) and #3 (7406) were triggered by Git changes and passed; automatic deploy #2 (7405) and #3 (7408) confirmed the expected site, folder and pool on deploy-agent-zenx (MRB2C-DEV). The sole site binding is http *:80:octopus-dash-api.vilmos.co.uk. The agent found zero existing trusted valid server certificates matching the hostname or *.vilmos.co.uk; it stopped before changing API files, private settings or SQL. The script can add the requested HTTPS SNI binding once an eligible existing certificate is installed.

User-approved Cloudflare DNS A record octopus-dash-api.vilmos.co.uk -> 77.68.99.121 (DNS only) is saved and verified through resolver 1.1.1.1. Automatic approval review initially rejected this DNS mutation as public exposure/IP disclosure without explicit authorization; the user subsequently approved that exact record. Certificate validation was never bypassed.

Resolved API deployment blocker: HTTPS certificate/binding were subsequently commissioned. Deployment #4 (7411) then failed on the script's cast of the named ApplicationPoolIdentity to Int32. Fix 5320aa2 accepts the provider's named/numeric identity representation while rejecting other accounts. CI #5 (7413) passed and automatic deploy #5 (7415) succeeded: private production settings/PFX generated, SQL integrated-security migration/startup verified, HTTPS health returned healthy, authenticated API status succeeded and unauthenticated status returned 401. Independent certificate-validating probes confirm health 200 and API 401. The application pool identity was preserved. Evidence: .planning/debug/resolved/teamcity-deploy.md.

Quartz Windows service deployment, Cloudflare production runtime secrets and production supplier account setup remain pending. Public TeamCity artifact download on its existing HTTP port 8089 timed out; internal artifact dependency transfer worked. Details: .planning/quick/261013-m25-teamcity-api-build-and-iis-deployment/SUMMARY.md and docs/TEAMCITY.md.

## 2026-10-13 — Cloudflare production login (quick task 261013-tnd)

Configured APP_PASSWORD_HASH using the owner's explicitly selected localhost password and a separate stable random production SESSION_SECRET through Wrangler stdin. Both runtime secret names are verified; their values were not printed or committed. Browser production sign-in succeeded and reached the authenticated unavailable-data screen.

The initial approval-review rejection was resolved by explicit owner approval on 2026-10-14. A one-time RSA4096/OAEP-SHA256 envelope returned only ciphertext through TeamCity; local decryption fed Wrangler stdin for INTERNAL_API_TOKEN. All three runtime secrets are verified by name. Authenticated API status returned 200; fresh production browser sign-in renders the Day dashboard and Settings. The temporary TeamCity step/public helpers and ignored private-key/ciphertext files were removed after upload (7a86097). Supplier credentials, database and Data Protection material stay on the backend.

CI #7 encountered newly published cryptography advisories. Patch 4374b34 updates DataProtection.EntityFrameworkCore and the PKCS override to 10.0.13 and self-contained IIS runtime to 10.0.13 without suppressing audit warnings. All 52 local SQL-inclusive tests, Windows publish and transitive NuGet audit passed. CI #8 (7429) and automatic deploy #7 (7431) succeeded. Production supplier credentials and Quartz Windows service still need commissioning before queued sync can run; the dashboard currently reports missing/unpriced meter data.

Cleanup revision 7a86097 passed CI #9 (7434) and normal automatic deploy #8 (7436), confirming the one-time transfer is removed and production configuration remains intact.
