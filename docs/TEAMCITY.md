# API deployment with TeamCity

Repository: https://github.com/vhosztafi/octopus-dash.git, default branch `master`.
TeamCity imports `.teamcity/settings.kts` as portable Kotlin DSL. Keep versioned settings read-only from VCS, so production pipeline changes are reviewed in Git.

`api-ci` runs backend tests and publishes self-contained `win-x64` API and worker ZIPs, TRX results and SHA-256/revision manifests. `api-deploy` follows successful default-branch CI builds, with snapshot and strict same-chain artifact dependencies. Deployment concurrency is one; only the existing Windows agent with `env.ZENX_ROLE=deploy` is eligible. CI watches `api/**`, `scripts/ci/**` and `.teamcity/**`.

The existing IIS site must have the HTTPS host binding `octopus-dash-api.vilmos.co.uk`, physical path `D:\Websites\octopus-dash-api.vilmos.co.uk\www`, and an ApplicationPoolIdentity already granted access to SQL database `OctopusDash_Live` on `.`. The ASP.NET Core Hosting Bundle/ANCM must be installed; the package supplies its own .NET runtime. The deploy script discovers the matching pool and sets it to 64-bit/no managed CLR.

Production configuration is generated once in `D:\Websites\octopus-dash-api.vilmos.co.uk\_energy-private\server-settings.json`. The IIS-generated `web.config` contains only this file path and the production environment. Administrators/SYSTEM have full access to the private directory; the existing pool has read access. Server settings contain the SQL integrated-security connection, a generated bearer token, and a generated RSA Data Protection PFX/password. The PFX encrypts the shared SQL key ring; protect and back up both database and private directory together. Deployments reuse them and refuse silent credential regeneration. SQL transport is encrypted with local-server certificate trust; the public HTTPS certificate is validated normally.

Deployment validates ZIP checksums and the exact checkout revision before touching IIS. It verifies the site path/binding, stages outside www, takes the site offline and stops its pool, saves a private backup, then mirrors the artifact. HTTPS health must confirm database connectivity; `/api/status` must work with the server token and reject unauthenticated requests with 401. Failed verification restores previous application files. EF migrations run under the existing pool at startup; file rollback does not undo database migrations. Retained backups require periodic operator cleanup.

To configure the Cloudflare frontend, use the server's `INTERNAL_API_TOKEN` as its secret of the same name, alongside its `APP_PASSWORD_HASH` and `SESSION_SECRET`. Never paste this file/token into build logs, Git, or a public issue. Supplier credentials must be configured separately in the production dashboard; local development credentials are not copied.

Cloudflare connection completed on 2026-10-14 with the owner's explicit encrypted-transfer approval. [CI #8](https://teamcity.vilmos.co.uk/buildConfiguration/OctopusDash_ApiCi/7429) and [deploy #7](https://teamcity.vilmos.co.uk/buildConfiguration/OctopusDash_ApiDeploy/7431) passed for revision `4374b34`. The temporary RSA4096/OAEP-SHA256 helper returned only ciphertext; the owner-local private key decrypted it for Wrangler stdin upload. The transfer step/helpers/private key were removed after successful authenticated API and browser verification. Future deployments preserve the existing API token and need no commissioning step.

Cleanup revision `7a86097` passed [CI #9](https://teamcity.vilmos.co.uk/buildConfiguration/OctopusDash_ApiCi/7434) and [deploy #8](https://teamcity.vilmos.co.uk/buildConfiguration/OctopusDash_ApiDeploy/7436), verifying the ordinary pipeline after transfer removal.

CI #7 encountered newly published cryptography advisories. DataProtection.EntityFrameworkCore and the PKCS override now resolve version 10.0.13; Windows self-contained publish explicitly uses runtime 10.0.13. All 52 local backend tests including SQL passed, the Windows package published, and the NuGet transitive vulnerability audit was clean. Audit warnings remain fatal. References: [Microsoft PKCS advisory](https://github.com/advisories/GHSA-846f-f764-76mx), [Microsoft XML advisory](https://github.com/advisories/GHSA-7hqp-7c5m-4vq6).

Quartz runs in the separate `EnergyDash.Worker` process. `deploy-backend.ps1` validates both packages and worker SQL permissions before any release changes, stops the existing owned worker before API migrations, then installs/verifies the worker. The fixed service `OctopusDashWorker` runs as `NT SERVICE\OctopusDashWorker` from the site's sibling `worker` directory, shares the private configuration/PFX and SQL key ring, and writes a fresh SQL/credential-decryption heartbeat under `_energy-private\worker-state`. Automatic startup/recovery and private backup/file rollback are configured. The worker needs database reader/writer roles, not owner or server administrator.

Worker commissioning is currently blocked: [CI #12](https://teamcity.vilmos.co.uk/buildConfiguration/OctopusDash_ApiCi/7448) passed for `553c932`; [deploy #11](https://teamcity.vilmos.co.uk/buildConfiguration/OctopusDash_ApiDeploy/7450) retrieved both artifacts but stopped before changing IIS or creating a service. The agent runs as `NT AUTHORITY\SYSTEM`, which cannot open `OctopusDash_Live`. Octopus account testing passed; Tesla authorization and TR20 site selection are saved. Neither proves scheduled ingestion until this service runs.

One-time server/DBA follow-up: an administrator should create the **stopped** service with the exact account/path below, then a SQL administrator should provision its login/user and data roles. The deployment account only needs database connection/metadata visibility once these principals exist; it does not need data roles or login-management rights. Review and execute on MRB2C-DEV, not the developer machine:

```powershell
sc.exe create OctopusDashWorker binPath= D:\Websites\octopus-dash-api.vilmos.co.uk\worker\EnergyDash.Worker.exe start= demand obj= "NT SERVICE\OctopusDashWorker"
```

```sql
USE [master];
IF SUSER_ID(N'NT SERVICE\OctopusDashWorker') IS NULL
    CREATE LOGIN [NT SERVICE\OctopusDashWorker] FROM WINDOWS;
IF SUSER_ID(N'NT AUTHORITY\SYSTEM') IS NULL
    CREATE LOGIN [NT AUTHORITY\SYSTEM] FROM WINDOWS;
GRANT VIEW DEFINITION ON LOGIN::[NT SERVICE\OctopusDashWorker] TO [NT AUTHORITY\SYSTEM];
USE [OctopusDash_Live];
IF USER_ID(N'NT SERVICE\OctopusDashWorker') IS NULL
    CREATE USER [NT SERVICE\OctopusDashWorker] FOR LOGIN [NT SERVICE\OctopusDashWorker];
ALTER ROLE [db_datareader] ADD MEMBER [NT SERVICE\OctopusDashWorker];
ALTER ROLE [db_datawriter] ADD MEMBER [NT SERVICE\OctopusDashWorker];
IF USER_ID(N'NT AUTHORITY\SYSTEM') IS NULL
    CREATE USER [NT AUTHORITY\SYSTEM] FOR LOGIN [NT AUTHORITY\SYSTEM];
GRANT CONNECT, VIEW DEFINITION TO [NT AUTHORITY\SYSTEM];
```

Then run the normal CI/deploy chain. Require the fresh heartbeat, successful source SyncLog entries and real dated meter/rate data in the dashboard before closing `.planning/debug/production-sync-worker.md`. Do not grant SYSTEM db_owner/sysadmin to bypass this preflight. CI #11 used the artifact rules from before Kotlin settings import; a fresh build with the imported worker rules resolved that separate artifact issue.

Local verification: run `scripts/ci/build-api.ps1` with `RUN_SQL_TESTS=1` and Docker running. Without that explicit setting, the real SQL Testcontainers test is skipped; pure/domain/provider/API tests still run. `scripts/ci/deploy-api.ps1 -ValidateOnly -ArtifactDirectory output/teamcity-api` validates the package without touching IIS.

References: [Oktoberfest scripts](https://github.com/solutioniser/tr20-oktoberfest-2026/tree/master/scripts/ci), [TeamCity same-chain artifacts](https://www.jetbrains.com/help/teamcity/artifact-dependencies.html), [ASP.NET Core IIS hosting](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/iis/?view=aspnetcore-10.0).

Commissioning status (2026-10-13): [TeamCity project](https://teamcity.vilmos.co.uk/project/OctopusDash) is created and synchronized. [CI #5](https://teamcity.vilmos.co.uk/buildConfiguration/OctopusDash_ApiCi/7413) passed and [deploy #5](https://teamcity.vilmos.co.uk/buildConfiguration/OctopusDash_ApiDeploy/7415) succeeded on `deploy-agent-zenx` (`MRB2C-DEV`). HTTPS certificate/binding, SQL integrated-security migration/startup and private configuration/PFX provisioning are verified. [API health](https://octopus-dash-api.vilmos.co.uk/health) returns healthy over validated TLS; authenticated `/api/status` succeeds and unauthenticated requests return 401. The Cloudflare API DNS A record points to `77.68.99.121` (DNS only).

For future HTTPS binding recovery, install a trusted server certificate with the hostname `octopus-dash-api.vilmos.co.uk` (or `*.vilmos.co.uk`) in LocalMachine/My, including its private key and Server Authentication usage. The script can create the requested SNI binding for the verified site using that existing certificate; it does not obtain public certificates or disable TLS validation. The existing application pool account is preserved; the identity guard accepts WebAdministration's named `ApplicationPoolIdentity` or numeric `4` representation. See the GSD quick task summary and resolved deployment debug record for evidence. Quartz service, frontend runtime secrets and provider account commissioning remain separate follow-ups.
