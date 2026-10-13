---
status: complete
date: 2026-10-13
---

# Cloudflare Worker deployment

Source commit a34330f pushed to master. Pinned OpenNext 1.20.10/Wrangler 4.150.0, Worker/domain/runtime bindings, Cloudflare CI commands and deployment operations documentation added. The default API origin is https://octopus-dash-api.vilmos.co.uk; backend deployment is deferred.

Validation: nine frontend tests, strict TypeScript, Linux production Worker build, Wrangler dry run and synthetic workerd smoke passed. Compressed bundle is approximately 1.25 MiB. npm audit reports zero vulnerabilities after a patched glob override. Next 16.4 requires the narrow upstream preview-props manifest fix, applied idempotently with an adapter/version guard. Edge middleware avoids the unsupported Node middleware path. Docker auth remains supported; Cloudflare uses shared per-location login counters.

Cloudflare existing vhosztafi Git integration and existing Workers Builds token reused. Worker octopus-dash created, root web, branch master, NODE_VERSION=24, build npm run ci:cloudflare, deploy npm run deploy:worker. Preview builds disabled; automatic production builds enabled, include path web/*.

Initial hosted build f92183dc-e916-43ba-8622-5f9c16034cb4 completed Ready in 1m13s and deployed version ef23aef8 at 100% traffic. Trusted HTTPS at octopus-dash.vilmos.co.uk returns 307 from / to /login and 200 from /login, including CSP/frame/sniffing/referrer headers. Runtime variables and login limiter binding verified in Cloudflare settings.

Follow-up: production APP_PASSWORD_HASH, SESSION_SECRET and INTERNAL_API_TOKEN are not yet set; login remains denied until configured. The API origin is intentionally deferred. Supplier/SQL/Data Protection secrets remain private with the local backend. The next source push verifies the automatic Git-triggered deployment as well as the initial manual build.
