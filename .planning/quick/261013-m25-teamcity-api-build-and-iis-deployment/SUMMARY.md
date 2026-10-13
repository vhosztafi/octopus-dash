---
id: 261013-m25
status: incomplete
description: TeamCity API build and IIS deployment
---

Created the Octopus Dash TeamCity project from the public repository, with master-only VCS and read-only portable Kotlin DSL synchronized from `.teamcity`. CI runs backend tests and publishes the self-contained Windows API ZIP, SHA-256/revision manifest and TRX results. A successful CI build automatically starts the serialized IIS deployment with snapshot/strict same-chain artifacts on the existing Windows deploy agent.

Deployment validates the exact site binding and physical path before server changes, keeps generated secrets/PFX outside www, uses SQL integrated security for . / OctopusDash_Live, runs EF migrations on startup, retains a private backup and verifies HTTPS database health plus authenticated/unauthenticated status behavior. Failed post-copy verification restores application files, not schema migrations. API startup supports an explicit private JSON configuration file with environment overrides; PFX loading uses ephemeral keys for IIS.

Verification:
- All 52 backend tests passed locally with real SQL Testcontainers and cross-process certificate-protected Data Protection keys. Windows self-contained publish and PowerShell AST/artifact preflight passed.
- HTTPS port follow-up passed all 20 API contract tests.
- TeamCity settings compiler succeeded on the agent (7397/7398), after adding the reference Maven POM and overriding the root's undefined dynamic branch parameter within this project.
- TeamCity CI #1 (7399), revision 37e6889, succeeded in 1m31s and published 57.1 MB of artifacts. The agent reported 18 Core + 32 integration tests passed, 2 skipped; the real SQL test is explicitly gated by RUN_SQL_TESTS and was verified locally.
- Automatic successful-build deployment #1 (7401) fetched the matching artifacts and stopped at the IIS HTTPS binding guard before modifying server files or the database.
- A deployment diagnostics commit (4d4bcd2) triggered CI #2 (7403) automatically; it succeeded in 51 seconds. Automatic deploy #2 (7405) confirmed the correct site, physical folder and pool exist, but only the HTTP binding is present.
- Latest CI #3 (7406), revision ee91cef, succeeded in 41 seconds and automatically triggered deploy #3 (7408). The deploy script checked for an existing trusted, valid Server Authentication certificate matching the hostname or *.vilmos.co.uk before attempting the requested HTTPS binding repair. It found zero eligible certificates and stopped before server changes.

External commissioning remains incomplete. On deploy-agent-zenx (MRB2C-DEV), site/pool octopus-dash-api.vilmos.co.uk and D:\Websites\octopus-dash-api.vilmos.co.uk\www are confirmed. Its sole binding is http *:80:octopus-dash-api.vilmos.co.uk; there is no HTTPS binding and no eligible trusted matching certificate. Install/commission a trusted HTTPS certificate for that hostname on this server, then rerun api-deploy (the script can add the requested SNI binding using that existing certificate). SQL ownership and production runtime startup cannot be verified until this preflight passes. No API files, private secrets/PFX or database migrations were deployed by these failed preflights. User authorization is to log blockers for follow-up without validation stops.

DNS is now complete: the user explicitly approved publishing the Cloudflare A record octopus-dash-api.vilmos.co.uk -> 77.68.99.121 (DNS only), after automatic approval review initially rejected the mutation as unapproved public exposure/IP disclosure. The record was saved and verified through resolver 1.1.1.1. The local resolver briefly retained the previous NXDOMAIN result. A direct TLS probe against that confirmed server reported a hostname mismatch, consistent with the missing SNI binding; certificate validation was never bypassed.

The separate Quartz worker service, production Cloudflare runtime secrets, and production supplier account setup remain separate follow-ups. No local live supplier credentials were copied. Public browser download of a TRX report redirected to the existing artifact service on HTTP port 8089 and timed out; internal TeamCity artifact dependency delivery succeeded. Public artifact endpoint configuration is a separate infrastructure follow-up.

Project: https://teamcity.vilmos.co.uk/project/OctopusDash
CI: https://teamcity.vilmos.co.uk/buildConfiguration/OctopusDash_ApiCi
Deployment: https://teamcity.vilmos.co.uk/buildConfiguration/OctopusDash_ApiDeploy
Operations guide: docs/TEAMCITY.md.
