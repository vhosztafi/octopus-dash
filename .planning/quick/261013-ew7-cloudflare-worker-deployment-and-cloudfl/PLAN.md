---
type: quick
status: planned
date: 2026-10-13
---

# Cloudflare Worker deployment and Cloudflare Builds

User authorizes autonomous setup of the Next.js frontend from vhosztafi/octopus-dash using Cloudflare-hosted builds. Frontend domain: octopus-dash.vilmos.co.uk. Future API origin: https://octopus-dash-api.vilmos.co.uk. Backend deployment is deferred.

1. Check current Cloudflare adapter compatibility, runtime secrets, authentication and build requirements. Choose a supported path that preserves the Next.js app and Docker development.
2. Add locked dependencies, Worker/domain configuration, build/deploy scripts, validation and deployment documentation. Keep runtime secrets out of source and build artifacts.
3. Verify production Worker output, login/session behavior and unavailable-API handling. Configure Cloudflare Git integration, Cloudflare Builds and custom domain once signed in; publish the tested source to the existing repository without force pushes.
4. Verify the Cloudflare build and HTTPS endpoint. Record configuration, evidence and external blockers in SUMMARY.md, STATE.md and PROGRESS.md.

Validation: strict TypeScript and existing frontend tests; Cloudflare production build and Worker runtime smoke checks. API/supplier data remains inaccessible until the API origin is commissioned. No supplier credentials belong in Cloudflare frontend configuration.
