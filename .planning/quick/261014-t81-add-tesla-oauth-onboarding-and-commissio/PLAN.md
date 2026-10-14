---
id: 261014-t81
status: in-progress
description: Add Tesla OAuth onboarding and commission production Powerwall authorization
---

Owner requested setting up Tesla authorization in the browser, without existing refresh token/client ID. Execute inline through GSD quick; user handles Tesla sign-in, MFA, legal acceptance and final consent/private credential entry.

1. Add an authenticated Authorize with Tesla form alongside the existing manual-token form. Store pending app credentials encrypted only in the API database, expire pending authorization after 15 minutes, and bind browser state to a signed httpOnly/Secure/Lax cookie and current dashboard session. Validate state before exchanging; never expose tokens or upstream errors to the browser or logs. Consume pending state durably before single-use code exchange; do not retry token POSTs. Preserve previous credentials on cancellation/failure and save successful tokens atomically under the existing SQL Tesla lock.
2. Host the required P-256 public key at the exact anonymous well-known path, with the private key kept ignored/owner-private. Register only this dashboard domain in Tesla EU, and request openid/offline_access/energy_device_data only. Keep the OAuth callback origin fixed. User creates their own Tesla developer app and authorizes Powerwall data; do not accept terms, financial commitments or grant additional scopes on their behalf.
3. Verify state tampering/session binding/expiry, encrypted pending state and replay prevention using synthetic tokens and real SQL, and verify API contracts, TypeScript, frontend tests and hosted builds. Deploy through TeamCity and Cloudflare, then commission in the browser if Tesla access is available. Select the actual site only after verifying returned choices. Log external blockers, including the still-undeployed Quartz worker; never imply data syncing is commissioned prematurely.

Official references: Tesla getting-started/what-is-fleet-api, authentication/third-party-tokens, authentication/partner-tokens, endpoints/partner-endpoints. Use existing Next.js server-action auth and server-only BFF conventions (vercel-react-best-practices server-auth-actions/server-serialization).
