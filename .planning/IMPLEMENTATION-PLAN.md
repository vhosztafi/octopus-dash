# Autonomous implementation plan
1. Create .NET domain/infrastructure/API/worker and xUnit projects with strict compiler settings.
2. Implement pure pricing/time/gas/alert math with edge-case tests.
3. Add SQL mappings/migrations, typed provider clients, safe secrets and idempotent ingest/materialization.
4. Add DTOs/queries/OpenAPI, fixture mode and DB command queue.
5. Implement Quartz schedules, telemetry, dedupe and report delivery.
6. Generate web types from actual API OpenAPI. Implement four prototype screens, URL navigation, server actions, secure login and CSV proxy.
7. Add Docker, CI and operational docs. Build/test and inspect desktop/mobile output; record external blockers and verification evidence.

No user checkpoints; decisions follow the supplied brief. Each roadmap phase receives a summary and test evidence.
