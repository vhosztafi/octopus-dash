# Tesla Powerwall authorization

Production uses the owner's personal Tesla Fleet API application. Request only **Energy Product Information** (`energy_device_data`) with `openid` and `offline_access` for renewable owner authorization.

## Developer application

Create a personal application at https://developer.tesla.com/dashboard. Select Authorization Code and Machine-to-Machine, use allowed origin `https://octopus-dash.vilmos.co.uk`, and set the exact redirect URI to `https://octopus-dash.vilmos.co.uk/auth/tesla/callback`. Select Energy Product Information only. The owner completes Tesla's final submission, agreements and consent; optional billing was skipped during setup.

The required P-256 domain public key is available anonymously at `https://octopus-dash.vilmos.co.uk/.well-known/appspecific/com.tesla.3p.public-key.pem`. Its private key stays in ignored, owner-private `secrets/tesla-domain-private.pem`; keep a private backup and retain the same public key on the domain. The integration reads Powerwall data and does not use vehicle or energy commands.

## Authorize in the dashboard

1. Sign in to the production dashboard and open Settings → Tesla Powerwall.
2. Enter the app's Client ID and Client secret privately, then select **Authorize with Tesla**.
3. The API registers this domain with Tesla EU and stores the pending credentials encrypted in SQL. Tesla then asks the owner to sign in and grant energy-data access.
4. On return, the dashboard validates the authorization state and session before exchanging the code through the API. Access/refresh tokens are encrypted in the API database. Test the connection, choose the actual energy site and save settings.

Keep app secrets, codes and tokens out of chat, logs and Git. They are never configured as Cloudflare runtime secrets or sent back to browser props. The existing manual refresh-token form remains available under the expandable advanced option. Environment-provided Tesla credentials retain precedence; remove them before using browser authorization.

Pending authorization expires after 15 minutes and is bound to a signed Secure/httpOnly/Lax host cookie and the initiating dashboard session. The SQL pending state is consumed durably before the single-use code exchange, even if the exchange fails. Callback responses redirect to a fixed Settings origin, clear the secure state cookie, disable caching and set `Referrer-Policy: no-referrer`. New tokens and app credentials are saved atomically under the same SQL application lock used for refresh rotation. Cancellation or exchange failure preserves existing credentials and requires starting a fresh authorization when necessary. Token POSTs are not retried automatically.

## Sync follow-up

Commissioned 2026-10-14: the owner completed the consent flow, the production connection test found one energy site, and TR20 was selected and saved. A full Settings reload verified the site selection persisted.

Production now hosts Quartz inside the existing IIS API, reusing its SQL identity and certificate-protected key ring. Deployment and fresh Tesla live polling were verified on 2026-10-14; no separate Windows service is needed. The owner keeps IIS running and recycles it daily at 03:00; startup sync resumes collection. Historical interval/granularity verification remains an account-specific follow-up in OPERATIONS.md.

Sources: [Tesla onboarding](https://developer.tesla.com/docs/fleet-api/getting-started/what-is-fleet-api), [third-party OAuth](https://developer.tesla.com/docs/fleet-api/authentication/third-party-tokens), [scope definitions](https://developer.tesla.com/docs/fleet-api/authentication/overview), [partner registration](https://developer.tesla.com/docs/fleet-api/endpoints/partner-endpoints).
