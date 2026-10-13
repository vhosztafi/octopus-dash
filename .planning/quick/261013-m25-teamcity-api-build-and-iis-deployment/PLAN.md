---
id: 261013-m25
description: TeamCity API build and IIS deployment
status: in-progress
---

Implement inline under the GSD quick workflow, as authorized for autonomous execution.

1. Follow Oktoberfest TR20's versioned Kotlin DSL pattern: Windows CI builds/tests/publishes an API ZIP and revision/checksum manifest; serialized deployment consumes the same successful snapshot on the existing agent with env.ZENX_ROLE=deploy. Default branch is master.
2. Add project-local PowerShell build/deploy scripts. Restrict deployment to D:\Websites\octopus-dash-api.vilmos.co.uk\www and the matching IIS binding/pool. Validate artifacts before touching IIS, retain a private backup, stop the pool during replacement, verify HTTPS database health and bearer authentication. Never log secrets or response bodies.
3. Keep persistent configuration and the Data Protection PFX outside www and TeamCity artifacts, generated once on the server and readable only by administrators/SYSTEM and the existing app pool. Use SQL integrated security against . / OctopusDash_Live, EF startup migrations, production mode. Preserve credentials across deployments. Optional explicit external JSON configuration with environment overrides; use ephemeral PFX loading for IIS.
4. Run backend tests, publish and PowerShell validation; commit/push reviewed changes. Create the TeamCity project from this repository, connect versioned settings, and run CI/deployment. Verify the actual server results and public endpoints, fixing failures autonomously.
5. Record evidence and external blockers in SUMMARY.md, STATE.md and PROGRESS.md. Quartz Windows service deployment and Cloudflare runtime secrets remain follow-up commissioning work; existing local supplier credentials are not transferred.

Reference inspected: OktoberfestTR20_ApiCi / OktoberfestTR20_ApiDeploy and scripts/ci/{build-api,deploy-api,deploy-iis}.ps1 in solutioniser/tr20-oktoberfest-2026. No reference configurations are modified.
