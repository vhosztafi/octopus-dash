---
status: complete
date: 2026-10-12
---
# Live local credential testing enabled

Initialized unique private login, session, internal bearer and SQL credentials plus a ten-year self-signed Data Protection PFX. All private files are ignored by Git and excluded from Docker build contexts. Windows ACLs restrict the generated configuration to the owner and SYSTEM. Initializer refuses to overwrite an existing setup.

Built and started all four Compose services. SQL, API, worker and web are healthy. API applied the real migration. Only 127.0.0.1:3000 is published; the API/SQL stay in the Docker network. This task's sample listeners were identified and replaced; unrelated processes/containers were left alone.

## Verification
- Real SQL migration, repeated ingestion and repeated re-pricing passed.
- Separate API/worker service providers successfully round-trip an encrypted synthetic credential using the persisted SQL key ring and shared certificate.
- Full backend suite with RUN_SQL_TESTS=1: 18 Core + 28 integration tests, zero skipped.
- Live status sample=false; authenticated settings/connections return 200; unauthenticated API returns 401; blank provider credentials return validation 400 instead of sample-mode 409.
- Browser successfully logged into live mode and opened Settings & alerts; forms are empty and ready for owner credentials. Both accounts correctly say Not connected.
- Login remains private in secrets/local-access.txt; no actual supplier credentials were entered or external notifications sent.

## Corrections
Ensured the empty public directory survives a clean checkout for Docker builds. Fixed misleading connection health when no credentials exist. The encryption-test service provider now registers IConfiguration explicitly.

## Access / persistence
Open http://localhost:3000/settings. The current browser tab is already signed in. For another browser/session, read the generated password in secrets/local-access.txt. Manage services with docker compose up -d / stop. Preserve .env, secrets/data-protection.pfx and the named SQL volume.

Remaining commissioning: owner credentials, account/site/SmartFlex discovery, live billing/cap validation and configured notification delivery.
