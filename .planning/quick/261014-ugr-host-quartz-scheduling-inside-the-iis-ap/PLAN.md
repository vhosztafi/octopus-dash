---
id: 261014-ugr
status: complete
---

# Host Quartz inside the IIS API

Authorization: owner accepted the simpler in-process Quartz architecture and will configure IIS always-running plus daily 03:00 recycling. Execute autonomously; preserve credentials and the existing app pool identity. No new Windows service or SQL identity is needed.

1. Extract existing Quartz registration/job implementation into Infrastructure for reuse by API and optional Docker worker. API enables scheduling outside sample mode; Compose explicitly disables API scheduling while its existing worker runs. Add per-job-group SQL session leases across overlapped IIS recycling, and release interrupted queued commands for retry.
2. Restore single-API TeamCity artifacts/deployment, remove unused Windows service deployment scripts/package dependency, verify protected scheduler startup as part of deployment. Keep restart scheduling owned by user. Startup sync recovers the 03:00 account job; 03:10 rates and 03:30 pricing remain scheduled.
3. Add meaningful real-SQL tests for cross-host exclusion/release and interrupted queue work, sample scheduler suppression, plus scheduler startup registration. Run backend tests with Docker SQL, Windows publish/preflight and dependency audit; deploy via TeamCity and verify real Octopus ingestion/Tesla telemetry. Document any remaining provider-specific gaps.

Done when API scheduler is live under the existing identity, TeamCity deployment passes without separate-service SQL provisioning, and dated consumption/rates are visible (or an exact independent supplier blocker is recorded).
