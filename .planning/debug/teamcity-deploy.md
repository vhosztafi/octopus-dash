---
status: fixing
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
hypothesis: WebAdministration exposes identityType as a named string, so the integer cast fails even for the correct identity.
test: reproduce the guard with named and numeric identities, fix validation, run the real deployment.
expecting: ApplicationPoolIdentity and enum value 4 pass; other identities fail closed.
next_action: fix the identity check and publish it for TeamCity verification.

## Evidence
- 2026-10-13: deploy #4 (7411) passed artifact/IIS HTTPS path checks and failed on the identity cast. The previous missing HTTPS binding blocker is no longer current.

## Resolution
root_cause: incorrect assumption that WebAdministration returns identityType as an integer.
fix: pending
verification: pending
files_changed: pending
