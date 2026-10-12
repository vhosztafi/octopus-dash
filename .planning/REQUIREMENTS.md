# Requirements: Home Energy
Defined: 2026-10-12. Source: docs/prototype/AGENT_INSTRUCTIONS.md.

| ID | Requirement | Phase |
|---|---|---|
| UI-01 | Four responsive prototype screens with tokens/fonts and accessible slot buttons | 1 |
| API-01 | OpenAPI-generated frontend contracts and authenticated server-only BFF | 1 |
| DEP-01 | Four-service Docker deployment, private API/SQL, health dependencies | 1 |
| DB-01 | Full SQL model with UTC/decimal conventions, indexes and migrations | 2 |
| ING-01 | Account discovery, dated agreements, pagination and idempotent backfill | 2 |
| ING-02 | VAT-inclusive rates, standing charges, standard and dual-register tariffs | 2 |
| ING-03 | Logged scheduled jobs and on-demand DB sync command | 2 |
| PRC-01 | Pure per-slot pricing, tariff changes, open-ended rates and missing values | 3 |
| PRC-02 | 46/48/50-slot London days, full precision and daily standing | 3 |
| PRC-03 | Atomic materialization and real day/ledger/CSV endpoints | 3 |
| DSP-01 | Kraken token, live schema introspection, SmartFlex and legacy fallback | 4 |
| DSP-02 | Persistent dispatch snapshots and partial-slot override | 4 |
| DSP-03 | Isolated configurable midday-to-midday allowance with verification note | 4 |
| BAT-01 | Tesla OAuth refresh rotation, energy-site discovery, reserve/live card | 5 |
| BAT-02 | Wh history normalization and gap-aware slot integration of live watts | 5 |
| WK-01 | Real Monday–Sunday aggregates, previous-week comparison and gas limits | 6 |
| GAS-01 | Configurable m³ conversion/calorific value | 6 |
| ALT-01 | Dedupe, cooldown, early warning, reconciliation and gas alert | 7 |
| RPT-01 | Shared weekly data, styled HTML email and configured scheduling | 7 |
| NTF-01 | MailKit and ntfy delivery via notifier interface | 7 |
| SEC-01 | Login/session enforcement and constant-time internal bearer checks | 8 |
| SEC-02 | Encrypted DB secrets, shared certificate-protected key ring, env overrides | 8 |
| SEC-03 | Safe logs, validation/ProblemDetails and operational backup docs | 8 |
| TST-01 | Required Core pricing/time cases | 3 |
| TST-02 | Real SQL integration tests, fixtures/client parsing and contract checks | 8 |

## Deferred
Live commissioning, redacted real responses and authenticated account schema verification require credentials. Pixel verification and SQL container tests require a working browser/daemon. Log limitations rather than change requirements.
