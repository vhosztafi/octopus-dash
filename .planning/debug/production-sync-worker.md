---
status: investigating
trigger: "Also I cannot see any octopus data, although that's configured for sure"
created: 2026-10-14
updated: 2026-10-14
---

## Symptoms
- Expected: configured production Octopus credentials import readings/rates and display priced energy data.
- Actual: dashboard has no meter data and missing/unpriced slots, although production credential saving succeeds.
- Production Octopus Test connection: account verified. Tesla OAuth/test also succeeds; sole site TR20 saved and persisted.
- Timeline: production API deployed via TeamCity; only local Docker has a running worker. The production Quartz worker has not been deployed.

## Current Focus
hypothesis: valid credentials are saved but the persistent command queue has no worker to process it.
test: inspect deployment artifacts and worker service support, then deploy a dedicated Windows service and verify queue consumption plus dated data/pricing.
expecting: shared SQL/Data Protection configuration makes existing supplier credentials usable; startup sync imports data.
next_action: package and deploy the dedicated Quartz worker through TeamCity, with least-privilege virtual service identity, private shared configuration and Windows-compatible heartbeat. Verify actual permissions before provisioning and never log secrets/provider payloads.

## Evidence
- API CI publishes only EnergyDash.Api; deployment config contains no worker service step.
- Worker lacks Windows service lifetime support, private server configuration loading and a Windows heartbeat path.
- Public API tests validate live production Octopus and Tesla credentials; missing data is independent of credential validity.

## Plan
1. Add Windows service hosting/configuration and portable heartbeat; package checksummed self-contained worker alongside API.
2. Add bounded service deployment/preflight for OctopusDashWorker under the existing site's sibling worker directory. Verify fixed paths/service ownership, shared configuration/certificate, integrated SQL rights and account permissions. Use a dedicated virtual identity with database reader/writer roles; stop before mutation if provisioning permissions are unavailable. Stage/backup/rollback files and verify heartbeat/service stability.
3. Integrate coordinated worker stop/deploy/restart with TeamCity exact-chain artifacts. Confirm service starts, existing queued commands execute, and real Octopus/Tesla data and pricing appear. Preserve missing readings and log unresolved supplier-specific limitations. Scheduled notifications must not send unless already configured by the owner.

## Eliminated
- Invalid Octopus key: production account test passed.
- Missing Tesla authorization: callback and site test passed; site selection persists.

## Resolution
Pending implementation and live verification.
