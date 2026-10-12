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
