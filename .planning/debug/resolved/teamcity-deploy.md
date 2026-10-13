---
status: resolved
trigger: TeamCity API deployment failure
created: 2026-10-13
updated: 2026-10-13
---

## Symptoms
Expected: successful build deploys the API to the existing IIS site and verifies protected HTTPS health.
Actual: latest deploy #4 (7411) fails before server configuration/file changes.
Error: Cannot convert value ApplicationPoolIdentity to System.Int32 at deploy-api.ps1:57.
Reproduction: TeamCity api-deploy on deploy-agent-zenx.
Authorization: autonomous fixes and deployment; log genuine external blockers for follow-up. GSD debug executes inline.

## Current Focus
hypothesis: confirmed; named identity guard fix removes the preflight failure without changing the existing application pool account.
test: completed full TeamCity CI/deploy chain and HTTPS database/authentication verification.
expecting: met; production API is healthy and protected.
next_action: separate commissioning follow-ups for Quartz service, frontend runtime secrets and provider credentials.

## Evidence
- 2026-10-13: deploy #4 (7411) passed artifact/IIS HTTPS path checks and failed on the identity cast. The previous missing HTTPS binding blocker is no longer current.
- 2026-10-13: direct HTTPS probe with normal certificate validation and the confirmed DNS IP succeeds at TLS and returns IIS HTTP 404 before API deployment.
- 2026-10-13: named/numeric accepted identity cases and rejected service/null identity cases pass; PowerShell syntax validation passes. Fix commit 5320aa2 pushed; CI #5 (7413) triggered by Git and queued behind another project.
- 2026-10-13: CI #5 (7413) succeeded in 50 seconds. Automatic deploy #5 (7415) succeeded in 30 seconds, verified revision 5320aa29a342eb17a8c65fe64ece896242253439, and retained a private pre-deployment backup. Persistent private production configuration/PFX were generated without logging values.
- 2026-10-13: deployment's HTTPS health check confirms SQL access; authenticated /api/status succeeds against the migrated schema; unauthenticated /api/status returns 401. Independent certificate-validating HTTPS probes against the confirmed DNS IP return health 200 with status healthy and API status 401 without credentials.

## Resolution
root_cause: incorrect assumption that WebAdministration returns identityType as an integer.
fix: compare the provider's identity string against ApplicationPoolIdentity/4 instead of casting the named value to Int32.
verification: identity guard cases and PowerShell parse passed; actual TeamCity CI/deployment succeeded; production HTTPS health and bearer authentication verified.
files_changed: scripts/ci/deploy-api.ps1
