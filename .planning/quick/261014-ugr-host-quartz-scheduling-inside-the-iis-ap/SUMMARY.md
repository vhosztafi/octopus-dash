---
id: 261014-ugr
status: complete
---

# Quartz in the existing IIS API

The owner accepted replacing the proposed Windows service with Quartz hosted inside the existing API and will configure IIS always-running plus a daily 03:00 recycle. Commit 6b554db moves shared scheduling/jobs into Infrastructure and enables hosted scheduling outside sample mode. Compose explicitly disables API scheduling while retaining its worker. The unused service packaging/provisioning scripts and WindowsServices dependency were removed; the original app pool/database identity and private encrypted credentials are reused.

Per-job-group SQL session leases prevent overlapping IIS processes from executing the same group concurrently. Heavy jobs remain serialized; telemetry/reports keep independent groups. Graceful cancellation releases queue commands without marking them completed; hard-killed claims retain the existing 30-minute recovery. Startup account/rates/consumption/history sync catches the daily recycle boundary. A protected /api/scheduler endpoint verifies enabled/running/non-standby status during TeamCity deployment.

All 57 backend tests passed with no skips, including real disposable SQL tests for cross-host exclusion/release and canceled command retry, schedule registration and sample-mode suppression. Self-contained Windows API publish, secret-exclusion check, checksum/revision artifact preflight, PowerShell syntax, Compose validation and transitive vulnerability audit passed. No frontend source changed. TeamCity Kotlin settings compiled successfully; CI #13 passed and automatic deploy #12 (7455) succeeded. Deployment verified the scheduler and existing SQL/authentication checks. The production dashboard now renders dated electricity/gas readings and prices with fresh Tesla polling; meter coverage reaches 14 October 01:00. The remaining warning is specifically missing SmartFlex dispatch discounts, a previously logged account-specific follow-up. No private telemetry values or credentials were committed. Screenshot evidence: ignored output/cloudflare/quartz-iis-live-sync.png.

Resolved debug record: .planning/debug/resolved/production-sync-worker.md. The previous SYSTEM/worker SQL provisioning blocker is superseded; no such access was granted. The owner configures IIS always-running/preload, idle timeout zero and daily 03:00 recycle. This deployment preserves that policy.
