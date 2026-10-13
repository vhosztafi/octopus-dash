---
status: resolved
trigger: Owner saved Octopus credentials but cannot see syncing
started: 2026-10-13
---

## Symptoms
Live Docker services are healthy. Owner saved an Octopus API key and expects consumption to sync.

## Investigation plan
Verify encrypted credentials through the authenticated account test. Inspect safe sync log and queue metadata, then run a sync and verify imported data. Never record secrets or account/meter identifiers.

## Evidence
- Saving Octopus credentials currently does not enqueue account discovery or ingestion.
- Account discovery normally runs at startup and daily; consumption cannot discover meters itself.
- Authenticated live account test returned 200 and verified the credentials.
- 4,322 electricity readings existed, but no rates or standing charges. Gas discovery had not run.
- Historical E-FLAT2R agreement was rejected by the product-code parser. The worker's log save persisted partially tracked account changes, including an incomplete product code, despite discovery failing.
- Consumption backfill was checked globally rather than per fuel, so newly discovered gas would only fetch seven days after electricity had imported.

## Fix
Accept the supplier's FLAT2R prefix. Discover account entities transactionally with natural-key dictionaries, detach pending mutations after failures, backfill each fuel independently, queue sync on credential save, and query latest status per source so frequent Tesla logs cannot hide tariff failures.

The current IOG four-rate product uses an E-1R code but returns an empty standard rate list. Its public catalogue publishes day/night relations. Added a catalogue-driven fallback without treating EV-device registers as whole-house rates.

## Verification
- Live REST account test: 200, verified. No credentials or account/meter identifiers recorded.
- Account, rates and consumption worker stages all completed successfully after deployment.
- Imported 4,322 electricity and 4,267 gas readings, 281 dated unit rates and three standing-charge records.
- Latest day with complete readings and tariff rates: 2026-10-11, 48 electricity slots; both fuels have zero missing/unpriced slots. 2026-10-12 still has 46 missing readings per fuel in the supplier response.
- 18 Core + 34 integration tests passed with no skips, including real SQL atomic discovery/idempotency, independent fuel backfill and source health regressions. Seven frontend tests, strict TypeScript and production Docker builds passed.
- Dashboard marks costs partial while dispatch verification is pending rather than claiming all discounts are included.

## Follow-up blocker
Authenticated GraphQL token works. Live root exposes flexPlannedDispatches(deviceId) and completedDispatches(accountNumber), but the latter returns no dispatches for this account. SmartFlex device ID is not configured, so planned dispatch ingestion remains blocked. Record/device discovery, historical discount coverage, four-rate EV disaggregation and account-specific charging cap semantics need a follow-up phase. Tesla is not configured. These gaps do not prevent meter and tariff ingestion.

## Hypotheses
- Newly saved credentials need an initial full sync.
- A supplier response or ingestion error may independently prevent import; test before changing code.
