# Home Energy roadmap

All eight milestones have implemented source. External verification is explicitly pending; see PROGRESS.md and Phase 8 VERIFICATION.md. Requirements map to exactly one phase.

| Phase | Implementation | Verification |
|---|---|---|
| 1. Scaffold and sample dashboard | Implemented | Automated/browser evidence; commissioning gaps noted |
| 2. SQL model and Octopus ingestion | Implemented | External commissioning pending |
| 3. Exact half-hour pricing | Implemented | Automated/browser evidence; commissioning gaps noted |
| 4. SmartFlex dispatch persistence | Implemented | External commissioning pending |
| 5. Tesla Powerwall integration | Implemented | External commissioning pending |
| 6. Weekly and gas reporting | Implemented | Automated/browser evidence; commissioning gaps noted |
| 7. Alerts, notifiers and weekly email | Implemented | External commissioning pending |
| 8. Security and operational verification | Implemented | External commissioning pending |

### Phase 1: Scaffold and sample dashboard
**Goal:** Four routes reproduce prototype tokens/layout with accessible slot buttons.
**Mode:** mvp
**Requirements:** UI-01, API-01, DEP-01
**Status:** Implemented; Browser/UI and TypeScript/build checks passed; pixel equality is not claimed.
**Plans:** 1
- [x] 01-01-PLAN.md — source implemented and summary recorded
**Success Criteria:**
1. Four routes reproduce prototype tokens/layout with accessible slot buttons.
2. API supplies labelled sample data using the real OpenAPI contract.
3. Private four-service Compose configuration validates.

### Phase 2: SQL model and Octopus ingestion
**Goal:** Committed EF migration and natural-key upserts cover all domain tables.
**Mode:** mvp
**Requirements:** DB-01, ING-01, ING-02, ING-03
**Status:** Implemented; Model/pagination tests passed; real SQL test skipped because Docker Linux daemon is unavailable. Live account ingest awaits credentials.
**Plans:** 1
- [x] 02-01-PLAN.md — source implemented and summary recorded
**Success Criteria:**
1. Committed EF migration and natural-key upserts cover all domain tables.
2. Typed REST client discovers meters/agreements and paginates VAT-inclusive rates/readings.
3. Sync commands persist and worker schedules backfills.

### Phase 3: Exact half-hour pricing
**Goal:** 46/48/50-slot London days preserve missing readings and decimal costs.
**Mode:** mvp
**Requirements:** PRC-01, PRC-02, PRC-03, TST-01
**Status:** Implemented; 18 Core tests passed; materialization source implemented, real SQL execution pending.
**Plans:** 1
- [x] 03-01-PLAN.md — source implemented and summary recorded
**Success Criteria:**
1. 46/48/50-slot London days preserve missing readings and decimal costs.
2. Tariff changes, partial dispatches, cap, open-ended and dual-register rates have tests.
3. Both fuels are atomically materialized per date; ledger/CSV use UTC identity.

### Phase 4: SmartFlex dispatch persistence
**Goal:** Runtime introspection discovers query root and exposed dispatch fields.
**Mode:** mvp
**Requirements:** DSP-01, DSP-02, DSP-03
**Status:** Implemented; Dispatch engine tests passed; authenticated live schema and active charging/terms verification pending.
**Plans:** 1
- [x] 04-01-PLAN.md — source implemented and summary recorded
**Success Criteria:**
1. Runtime introspection discovers query root and exposed dispatch fields.
2. SmartFlex device queries and supported legacy fallback persist first/last observations.
3. Opt-in per-device midday allowance remains isolated with TODO(verify).

### Phase 5: Tesla Powerwall integration
**Goal:** Customer OAuth refresh tokens rotate under a shared SQL application lock.
**Mode:** mvp
**Requirements:** BAT-01, BAT-02
**Status:** Implemented; Telemetry/Wh fixture tests passed; real OAuth/API/history granularity commissioning pending.
**Plans:** 1
- [x] 05-01-PLAN.md — source implemented and summary recorded
**Success Criteria:**
1. Customer OAuth refresh tokens rotate under a shared SQL application lock.
2. Energy-site discovery and reserve/live/history data feed the Powerwall view.
3. Wh aggregation and positive-W live integration split at slots and reject gaps/coarse data.

### Phase 6: Weekly and gas reporting
**Goal:** Monday–Sunday totals share the real materialized day query.
**Mode:** mvp
**Requirements:** WK-01, GAS-01
**Status:** Implemented; Gas/Core, weekly API and browser checks passed; household data comparison pending.
**Plans:** 1
- [x] 06-01-PLAN.md — source implemented and summary recorded
**Success Criteria:**
1. Monday–Sunday totals share the real materialized day query.
2. Previous-week comparisons require complete data and gas includes standing charges.
3. Raw gas measurements are recalculated using configurable calorific value.

### Phase 7: Alerts, notifiers and weekly email
**Goal:** Rule/subject dedupe, cooldown, early warning and reconciliation are implemented.
**Mode:** mvp
**Requirements:** ALT-01, RPT-01, NTF-01
**Status:** Implemented; Code compiled; no real notifications were sent. SMTP/ntfy destination tests and durable delivery recovery are follow-up commissioning tasks.
**Plans:** 1
- [x] 07-01-PLAN.md — source implemented and summary recorded
**Success Criteria:**
1. Rule/subject dedupe, cooldown, early warning and reconciliation are implemented.
2. MailKit and ntfy use a notifier interface and configured channels.
3. Weekly Razor email shares weekly data; scheduled/manual sends are serialized and incomplete weeks held.

### Phase 8: Security and operational verification
**Goal:** Secure sessions, server/action checks, internal bearer and encrypted shared key ring are implemented.
**Mode:** mvp
**Requirements:** SEC-01, SEC-02, SEC-03, TST-02
**Status:** Implemented; 44 .NET tests passed, one SQL test skipped; 7 web tests and production build passed. Docker/certificate deployment and provider commissioning remain external gaps.
**Plans:** 1
- [x] 08-01-PLAN.md — source implemented and summary recorded
**Success Criteria:**
1. Secure sessions, server/action checks, internal bearer and encrypted shared key ring are implemented.
2. Validation and sanitized errors prevent credentials appearing in public responses.
3. Tests/build/audits, health checks, backup documentation and CI are provided.

