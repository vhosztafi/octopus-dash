---
id: 261014-t81
status: incomplete
---

# Tesla browser authorization commissioning

The owner requested help creating Tesla authorization rather than pasting an existing refresh token. Prepared the personal Tesla application **Octopus Dash Home Energy**, fixed dashboard origin/callback and energy-only scope. Automatic approval review initially rejected selecting the scope; the owner then explicitly approved Energy Product Information. The owner personally completed final app creation without optional billing and confirmed app created. No Tesla login, MFA, agreement or final consent was completed on their behalf. App credentials were not copied to chat or repository.

Implemented an authenticated server-action onboarding form, API-only encrypted pending credentials, Tesla EU domain registration and the official authorization-code exchange. State is signed, session-bound and expires after 15 minutes; backend state is durably consumed before exchange, and successful credentials/tokens are saved atomically under the existing Tesla SQL lock. Cancellation, replay and failed/expired exchanges preserve prior credentials. Callback redirects are fixed, uncached, no-referrer, and clear the Secure host cookie. The manual refresh-token form remains available.

Published only the P-256 public domain key; its private key is ignored and inherits owner/SYSTEM-only directory ACLs. Generated web DTO aliases from a synthetic sample OpenAPI host. Local 54 backend tests passed with real disposable SQL, including encrypted pending data, state mismatch, expiry, replay, cancellation and failed-exchange preservation. Eleven frontend tests/typecheck passed, including tampering, expiry and session binding.

Source commits: 6070568, secure cookie deletion b3b6d4b, callback header fix 7861248. The Worker smoke caught a site-wide Referrer-Policy overriding the callback; the scoped Next header rule fixes it. Windows smoke cleanup now stops only its own child process tree so temporary-directory locks cannot hide test results. Final local OpenNext build and workerd smoke passed, including anonymous public key, invalid callback state, secure cookie clearing and no-referrer/no-store headers. TeamCity CI #10 (7440) and deploy #9 (7442) passed; Cloudflare build 773144a2-49c0-44ba-bdd0-49ab17ab3b8c deployed the new UI. Production public key returns 200 and matches the committed key; the new authenticated app-credential form renders.

The browser is handed to the owner for private Client ID/Client secret entry, Authorize with Tesla and final energy-data consent. Final token exchange, actual site discovery and selection remain pending owner input. Do not mark account authorization complete until these succeed.

Production Quartz worker remains uncommissioned. Credential/site testing through the API is independent of scheduled sync; do not claim Powerwall data ingestion is live until the worker is deployed and real data verified.
