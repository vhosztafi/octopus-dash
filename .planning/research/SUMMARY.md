# Verified implementation guidance — 2026-10-12

- Octopus REST: https://docs.octopus.energy/rest/guides/api-basics/ — Basic API-key auth, ISO periods and paginated readings/rates. Preserve VAT-inclusive decimal values.
- SmartFlex: https://developer.octopus.energy/graphql/reference/queries/flexplanneddispatches/ — current query requires deviceId (not accountNumber); returns start/end/energyAddedKwh/type. Discover devices or configure device ID. Legacy plannedDispatches is deprecated; use only when live schema exposes it.
- Tesla: https://developer.tesla.com/docs/fleet-api/endpoints/energy — live watts and history Wh. Interval granularity must be inspected; never relabel a daily total as a half-hour.
- OAuth: https://developer.tesla.com/docs/fleet-api/authentication/third-party-tokens — refresh at fleet-auth.prd.vn.cloud.tesla.com; save rotated refresh token atomically. Client credentials alone do not authorize customer energy data.
- Charge cap: https://octopus.energy/blog/intelligent-octopus-go-smarter-charging-for-a-greener-grid/ — preserve isolated configurable rule; planned windows are not proof of active charging. Commission against customer terms before enabling.
- Next.js: https://nextjs.org/docs/app/api-reference/file-conventions/proxy — current request interception convention is proxy.ts. Enforce session additionally in actions and server fetches.

## Pitfalls
Use UTC slot identity during repeated local times. Store null consumption. Prevent duplicate import meters/dispatch observations, tariff overlap, credential leaks, refresh races and false live alerts after polling gaps. Sample mode is explicitly labelled and disabled by default. Never claim private schema or real responses verified without credentials.
