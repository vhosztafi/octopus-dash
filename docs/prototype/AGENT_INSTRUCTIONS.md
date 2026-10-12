# Home Energy Dashboard — implementation brief

You are building a single-user, self-hosted dashboard that shows household electricity and gas use and **prices every half-hour exactly**. It uses an Octopus Energy account (smart meters, Intelligent-style EV tariff) and a Tesla Powerwall.

**Visual and interaction reference:** `home-energy-prototype.html`, in the same folder as this brief. Open it in a browser. It has four screens (Day, Half-hours, Weekly report, Settings & alerts) and wired navigation. All its numbers are fake. Match its layout, colours, typography, copy and behaviour. Do not copy its code structure: it is a vanilla-JS mock.

---

## 1. Architecture and stack

```
 Browser ──► Next.js (web)  ──server-side only──►  ASP.NET Core API  ──►  SQL Server
                                                      ▲
                       .NET Worker (Quartz jobs) ─────┘  (same Core + Infrastructure libraries)
                       ├─ Octopus REST/GraphQL
                       └─ Tesla Fleet API
```

- **Backend:** C# on **.NET (current LTS, .NET 10)**, as an ASP.NET Core Web API using Minimal APIs or controllers (your choice; be consistent). Enable nullable reference types, and treat warnings as errors.
- **Database:** **Microsoft SQL Server** through **EF Core** (`Microsoft.EntityFrameworkCore.SqlServer`), with code-first migrations.
- **Worker:** a separate **.NET Worker Service** using **Quartz.NET** (cron triggers in the `Europe/London` time zone) for polling, backfills, pricing, alerts and reports.
  - Live alerts need polling about every 60 seconds, so this must be a long-running process.
- **Frontend:** **Next.js** (latest stable, App Router), **TypeScript** with `strict: true`, **Tailwind CSS v4**. This is a UI layer only: no database access and no third-party API calls.
- **The Next.js server is a backend-for-frontend (BFF).**
  - Server Components and Server Actions call the .NET API.
  - The browser never calls the .NET API directly.
  - The .NET API is not exposed outside the Docker network.
- **API contract:** generate an OpenAPI document from ASP.NET Core (`Microsoft.AspNetCore.OpenApi`), then generate TypeScript types in the web app with `openapi-typescript`, plus a thin typed fetch wrapper. Regenerate whenever the contract changes. Never hand-write DTO types twice.
- **Key .NET packages:**
  - **NodaTime** (+ `NodaTime.Serialization.SystemTextJson`) for all time-zone and DST logic.
  - `Microsoft.Extensions.Http.Resilience` for retries, 429 back-off and timeouts on `IHttpClientFactory` clients.
  - **FluentValidation** for request validation.
  - **Serilog** for structured logs.
  - **MailKit** for email.
- **Tests:**
  - **xUnit** for unit tests. Use plain `Assert` or **Shouldly**; avoid FluentAssertions v8+, which needs a commercial licence.
  - **Testcontainers for .NET** (`Testcontainers.MsSql`) for integration tests against a real SQL Server.
  - Frontend: vitest for anything with logic.
- Money and energy values are `decimal` in C#, stored as `decimal(18,6)`. Never use `double` for pence or kWh.
- **Store every timestamp in UTC** (`datetime2(0)`, named `…Utc`). Convert to `Europe/London` only for grouping into days and for display, using NodaTime `DateTimeZoneProviders.Tzdb["Europe/London"]`.

### Suggested repository layout

```
/api
  EnergyDash.sln
  src/
    EnergyDash.Core/            # domain: entities, value objects, PricingEngine, slot/day maths, alert rules
                                # NO EF, HTTP or framework dependencies
    EnergyDash.Infrastructure/  # EF Core DbContext + migrations, Octopus & Tesla clients, secret store, notifiers
    EnergyDash.Api/             # ASP.NET Core endpoints, DTOs, validation, OpenAPI, auth
    EnergyDash.Worker/          # Worker Service + Quartz jobs
  tests/
    EnergyDash.Core.Tests/      # pricing engine + time tests (xUnit)
    EnergyDash.Integration.Tests/  # Testcontainers MSSQL, API endpoint tests, client tests with recorded JSON fixtures
/web
  app/
    layout.tsx                  # header + nav (Day, Half-hours, Weekly report, Settings & alerts)
    page.tsx                    # Day view            ?date=YYYY-MM-DD&slot=HH:MM
    half-hours/page.tsx         # ledger              ?date=…&filter=all|peak|off|disp|over
    weekly/page.tsx             # weekly report       ?week=YYYY-MM-DD (Monday)
    settings/page.tsx           # server actions → API
    api/export/half-hours/route.ts   # proxies CSV from the .NET API
  components/                   # KpiTile, HalfHourChart, RateRibbon, SlotDetail, PeakChart, CostByRateTable,
                                # PowerwallCard, LedgerTable, FilterChips, DailyCostChart, GasLimitBars, Switch, Field …
  lib/api/                      # generated types (schema.d.ts) + typed client
/docker-compose.yml
```

Charts are simple: CSS/flex bars and positioned SVG, as in the prototype. Don't add a charting library unless it clearly helps. Each bar must be a real `<button>` with an `aria-label`.

---

## 2. Design tokens (from the prototype)

Define these in `web/app/globals.css` with Tailwind v4 `@theme`:

| Token | Value | Use |
|---|---|---|
| bg | `#0E1214` | page |
| panel / panel-2 | `#151B1E` / `#1C2427` | cards / inset |
| line / line-2 / line-3 | `#222B2E` / `#2A3437` / `#3A4548` | borders |
| text / text-2 / muted / faint | `#E8ECEA` / `#C9D1CF` / `#A9B4B2` / `#8A9694` | type |
| offpeak | `#4F9BE0` | off-peak window slots |
| dispatch | `#B6DBFB` | smart-charge dispatch slots |
| peak / peak-dim | `#F2994A` / `#7A5233` | peak over limit / peak under limit |
| gas | `#C9A6F5` | gas |
| standing | `#5C6866` | standing charges |
| good | `#6FD39A` | savings, OK status |
| link | `#8CC4F5` | links, switches |
| warn-bg / warn-line / warn-text | `#241711` / `#6B3A1F` / `#F7C9A3` | alert banner |

Fonts are loaded with `next/font/google`. **Archivo** (400–700) is for UI text. **JetBrains Mono** (400–600) is for every number, time and unit.

Layout rules:
- Max width 1320px with 24px gutters.
- Card radius 12px.
- Touch targets at least 44px.
- Grids use `repeat(auto-fit, minmax(min(Npx,100%),1fr))`.
- Wide tables and the 48-bar chart scroll horizontally inside their card on phones. The page itself never scrolls horizontally.
- Dark theme only.

---

## 3. Data sources (verify every endpoint against current docs before coding)

Implement each source as a typed client in `EnergyDash.Infrastructure`, registered with `IHttpClientFactory` and the standard resilience handler. Model response JSON as `record` types with `System.Text.Json`. Save a few real (redacted) responses as JSON fixtures for tests.

### 3.1 Octopus REST API, base `https://api.octopus.energy/v1/`

Auth is HTTP Basic with the API key as the username and an empty password.

- `GET /accounts/{account_number}/` returns properties, electricity and gas meter points (MPAN/MPRN), meter serials, and **agreements** (`tariff_code`, `valid_from`, `valid_to`).
  - Use agreements to know which tariff applied on any date.
  - Ignore export MPANs for import pricing, but keep them in case solar is added later.
- Electricity consumption: `GET /electricity-meter-points/{mpan}/meters/{serial}/consumption/?period_from=…&period_to=…&page_size=25000&order_by=period`
  - Returns half-hour `interval_start`, `interval_end` and `consumption` in kWh.
- Gas consumption: `GET /gas-meter-points/{mprn}/meters/{serial}/consumption/…`
  - **SMETS2 gas meters usually report m³**, not kWh.
  - Convert: `kWh = m³ × 1.02264 × calorific_value / 3.6`. Calorific value is configurable, about 39–40 MJ/m³; let the user set it.
- Unit rates: `GET /products/{product_code}/electricity-tariffs/{tariff_code}/standard-unit-rates/?period_from&period_to`
  - Time-of-use tariffs return one row per rate period, with `valid_from`/`valid_to` and `value_inc_vat` in pence.
  - **Always use `value_inc_vat`.**
  - `valid_to` can be `null`, meaning open-ended.
  - Dual-register tariffs use `day-unit-rates` and `night-unit-rates` instead. Detect and support both.
- Standing charges: the same tariff path with `/standing-charges/`, in pence per day inc VAT.
- Gas rates: `/products/{product_code}/gas-tariffs/{tariff_code}/standard-unit-rates/` and `/standing-charges/`.
- Product code from tariff code: strip the `E-1R-`/`E-2R-`/`G-1R-` prefix and the trailing `-{region letter}`. For example, `E-1R-INTELLI-VAR-22-10-14-C` becomes `INTELLI-VAR-22-10-14`. Look up `/products/` if unsure.
- Paginate on `next`. Cache rates in the DB, because they rarely change.
- Smart-meter data normally arrives **the next day**. Backfill daily, and re-fetch the last 7 days on each run, because late data does arrive.

### 3.2 Octopus GraphQL API, `https://api.octopus.energy/v1/graphql/`

Used for smart-charge dispatches.

- Use a plain `HttpClient` POST with hand-written query strings. No GraphQL client library is needed.
- Get a token with the `obtainKrakenToken(input: { APIKey })` mutation, then send it as `Authorization: <token>`. Cache it and refresh it before it expires.
- Dispatch queries in the schema include `flexPlannedDispatches`, `plannedDispatches` and `completedDispatches`.
  - **Introspect the live schema first.** Prefer the current SmartFlex query (`flexPlannedDispatches`). Fall back to the others.
  - Each dispatch gives `start`, `end` and a delta kWh or source.
- **Persist every planned dispatch you ever see** (table `Dispatch`, with `FirstSeenUtc` and `LastSeenUtc`). Poll every 5 minutes.
  - Completed-dispatch history has been unreliable for third parties. Your own snapshots are the fallback record of which slots were granted.
- Optional live data: if the user has an Octopus Home Mini, `smartMeterTelemetry` gives near-real-time import. Don't build this in v1. Keep an `ILiveImportSource` interface open for it.

### 3.3 Tesla Fleet API (Powerwall)

- EU region base: `https://fleet-api.prd.eu.vn.cloud.tesla.com`.
- **Tesla uses OAuth, not a static API key.**
  - The user will provide either a refresh token or client credentials from their own registered Tesla developer app.
  - Store the refresh token encrypted, rotate it on every refresh, and save the new one.
  - Required scopes include `energy_device_data` and `offline_access`. Check the docs.
- `GET /api/1/products` lists energy sites; pick the `energy_site_id`.
- `GET /api/1/energy_sites/{id}/live_status` gives power in W (grid, battery, load, solar), percentage charged, and grid status.
  - Poll every 60 seconds.
  - **Integrate `grid_power` over time into the current half-hour's import Wh.** This drives live peak alerts.
- `GET /api/1/energy_sites/{id}/calendar_history?kind=energy&period=day&start_date&end_date&time_zone=Europe/London` gives energy intervals in **Wh**: grid import/export, battery charge/discharge, home use.
  - Check the interval size it returns, then aggregate to half-hours aligned with Octopus slots.
- `GET /api/1/energy_sites/{id}/site_info` gives the backup reserve percentage (shown on the Powerwall card).
- Put Tesla behind an `IBatteryProvider` interface so a local-gateway provider can be added later.
- Respect rate limits. Back off on 429.

---

## 4. Data model (EF Core → SQL Server)

Conventions:
- PascalCase tables and columns.
- `…Utc` columns are `datetime2(0)`.
- Energy and money are `decimal(18,6)`.
- Strings get explicit max lengths.
- Configure everything with `IEntityTypeConfiguration<T>` classes; no data annotations on domain types.

```
Meter            (Id, Fuel ['Elec'|'Gas'], MpanMprn, Serial, IsExport, Unit ['kWh'|'m3'])
Agreement        (Id, Fuel, TariffCode, ProductCode, ValidFromUtc, ValidToUtc NULL)
UnitRate         (Id, Fuel, TariffCode, Register ['Standard'|'Day'|'Night'], ValidFromUtc, ValidToUtc NULL, PenceIncVat)
                 IX (Fuel, TariffCode, ValidFromUtc)
StandingCharge   (Id, Fuel, TariffCode, ValidFromUtc, ValidToUtc NULL, PencePerDayIncVat)
Consumption      (Fuel, IntervalStartUtc) PK, IntervalEndUtc, Kwh, RawValue, RawUnit, FetchedUtc
Dispatch         (Id, StartUtc, EndUtc, DeltaKwh NULL, Source ['Planned'|'Completed'|'Flex'], FirstSeenUtc, LastSeenUtc)
                 UQ (StartUtc, EndUtc, Source)
BatteryInterval  (IntervalStartUtc PK, GridImportKwh, GridExportKwh, BattChargeKwh, BattDischargeKwh, HomeKwh, SolarKwh, SocEndPct)
LiveAccumulator  (SlotStartUtc PK, ImportWh, Samples, UpdatedUtc)        -- from Tesla live_status polling
PricedSlot       (Fuel, SlotStartUtc) PK, Kwh NULL, RatePence, Band ['Off'|'Disp'|'Peak'|'Std'], RateSource,
                 CostPence NULL, TariffCode, PricedUtc                   -- materialised output of the pricing engine
PricedDay        (Fuel, LocalDate [date]) PK, EnergyCostPence, StandingPence, SlotCount, MissingSlots, PricedUtc
Setting          (Key PK, ValueJson nvarchar(max))                      -- limits, toggles, calorific value, schedule …
Secret           (Key PK, Protected nvarchar(max), Hint nvarchar(8), UpdatedUtc)  -- Data Protection encrypted
DataProtectionKeys                                                       -- EF Core key ring store (see §9)
AlertRule        (Id, Kind ['PeakSlot'|'GasDaily'], Enabled, ParamsJson)
AlertEvent       (Id, RuleId, SubjectKey, FiredUtc, Value, Threshold, DeliveryJson)  UQ (RuleId, SubjectKey)
ReportRun        (Id, WeekStartLocal [date], SentUtc NULL, Status, Error NULL)
SyncLog          (Id, Source, StartedUtc, FinishedUtc NULL, Ok, Detail nvarchar(max))
```

- Make all ingest writes idempotent: upsert on the natural key. With EF Core use a `MERGE` through `ExecuteSqlInterpolated`, or load existing keys, then add or update in batches.
- Re-pricing a day replaces its `PricedSlot` and `PricedDay` rows inside one transaction, so it is safe to repeat.

---

## 5. The pricing engine (the core of the app), in `EnergyDash.Core`

Write the engine as a **pure** function with no I/O: all inputs passed in and the result returned. That makes it fully unit-testable.

```csharp
public sealed record PricingInput(
    LocalDate Day, Fuel Fuel,
    IReadOnlyList<ConsumptionInterval> Consumption,
    IReadOnlyList<Agreement> Agreements,
    IReadOnlyList<UnitRate> Rates,
    IReadOnlyList<StandingCharge> StandingCharges,
    IReadOnlyList<DispatchWindow> Dispatches,
    PricingSettings Settings);   // PriceDispatchAtOffPeak, DispatchAllowanceEnabled, DispatchAllowanceSlots, …

public sealed record PricedDayResult(LocalDate Day, IReadOnlyList<PricedSlotResult> Slots,
    decimal StandingPence, IReadOnlyList<string> Warnings);

PricedDayResult PricingEngine.PriceDay(PricingInput input);
```

The engine returns one slot per half-hour of the **London local day**.

- **Clock-change days have 46 or 50 slots, not 48.** Build slots from the UTC start and end of the local day (NodaTime `AtStartOfDayInZone`), never by multiplying the index by 30 minutes from local midnight.

For each electricity slot:

1. Find the **agreement** whose period contains the slot start. That gives the tariff code.
2. Find the **unit rate** for that tariff whose `[ValidFrom, ValidTo)` contains the slot start. That is the base rate.
   - Classify the band by comparing against that day's lowest rate. Don't hard-code 23:30–05:30: read the windows from the rates data.
3. **Dispatch override**, applied when `PriceDispatchAtOffPeak` is on:
   - If any dispatch overlaps the slot (even partially) and the slot would otherwise be peak, price it at that day's off-peak rate.
   - Set `Band = Disp` and `RateSource = "Smart-charge dispatch"`.
   - Octopus bills in whole 30-minute blocks, so a 10-minute charge makes the whole half-hour off-peak.
4. **Off-peak smart-charging cap**, behind a configurable setting:
   - Octopus has announced a limit for Intelligent Octopus Go: up to **6 hours of off-peak smart charging per car per day, counted midday to midday**. Only active charging counts.
   - Charging beyond the cap, or during Boost, is billed at the standard rate.
   - Implementation:
     - Count dispatch slots per midday-to-midday window in time order.
     - Once the configured allowance (default 12 slots = 6 h) is used up, later dispatch slots outside the home's off-peak window revert to peak.
     - Set `RateSource = "Dispatch beyond daily allowance"`.
   - The rollout date and the exact rules (for example, how charging during the home's 23:30–05:30 window counts) must be confirmed against Octopus's current terms. Keep this rule isolated in one class (`DispatchAllowancePolicy`) with a clear `// TODO(verify)`.
5. `CostPence = Kwh × RatePence` as `decimal`. Don't round until display.
6. Standing charges are added once per day, never spread across slots. Look up the standing charge valid on that day.

Gas is priced per slot at the gas unit rate valid at that time, plus the daily gas standing charge.

Derived figures (computed in the API from `PricedSlot`/`PricedDay`):
- **Effective unit rate** = total energy cost ÷ total import kWh, excluding standing charges.
- **Off-peak share** = (Off + Disp kWh) ÷ import kWh.
- **Powerwall saving** = battery discharge kWh in peak slots × (peak rate − off-peak rate) for that day.
- **Day total** = electricity energy + electricity standing + gas energy + gas standing.

If consumption is missing for a slot, `Kwh` and `CostPence` are `NULL` and the slot counts toward `MissingSlots`. The UI shows a gap in the chart and a data-status warning. It is never treated as zero.

---

## 6. Required tests

### `EnergyDash.Core.Tests` (xUnit)
- Normal day: 48 slots, off-peak window from the rate rows, costs summed correctly.
- **BST→GMT day (last Sunday in October, e.g. 2026-10-25): 50 slots. GMT→BST day: 46 slots.**
- Tariff change at midnight, and a rate change mid-day (new `ValidFrom`).
- Dispatch fully inside a slot, spanning two slots, and partially overlapping a slot: all overlapped slots become `Disp`.
- Dispatch inside the normal off-peak window: stays `Off`, and is counted or not counted toward the cap according to the configured rule.
- Cap: 13 dispatch slots in a midday-to-midday window. The 13th is priced at peak.
- `PriceDispatchAtOffPeak = false` prices those slots at peak.
- Gas m³ → kWh conversion.
- Missing consumption slots are excluded from totals and flagged.
- Dual-register (day/night) tariff.
- An open-ended rate (`ValidTo = null`).

### `EnergyDash.Integration.Tests`
- Migrations apply cleanly to a fresh SQL Server container.
- Running ingest twice with the same fixture data creates no duplicate rows.
- Re-pricing a day is idempotent.
- Each API endpoint returns the documented shape: snapshot-test the JSON.
- Octopus and Tesla clients parse the recorded fixtures, including pagination and `null` `valid_to`.

---

## 7. HTTP API (ASP.NET Core) consumed by the Next.js server

All routes are under `/api`, documented in OpenAPI, and require the internal bearer token (§9). JSON uses camelCase, ISO dates, and decimals serialised as numbers.

| Method & route | Purpose |
|---|---|
| `GET /api/status` | Last Octopus data time, last Tesla sync, last pricing run, connection health |
| `GET /api/days/latest-complete` | The latest local date with complete Octopus data |
| `GET /api/days/{date}` | Day view: KPIs, slots (start/end local + UTC, band, rate, source, kWh, cost, battery, house, overLimit), cost-by-rate rows, Powerwall summary, alerts |
| `GET /api/days/{date}/slots?filter=` | Ledger rows and totals for a filter (`all`, `peak`, `off`, `disp`, `over`) |
| `GET /api/days/{date}/slots.csv` | CSV export |
| `GET /api/weeks/{monday}` | Weekly report data, including previous-week comparison |
| `GET /api/powerwall/live` | Live charge level, reserve, and current flows |
| `GET /api/settings` · `PUT /api/settings` | Pricing, alerts and report settings (FluentValidation) |
| `GET /api/connections` | Status and secret hints only ("…abcd"), never the secret values |
| `PUT /api/connections/octopus` · `PUT /api/connections/tesla` | Save credentials (encrypted) |
| `POST /api/connections/octopus/test` · `POST /api/connections/tesla/test` | Live connectivity test |
| `GET /api/tesla/sites` | Energy sites for the site picker |
| `POST /api/sync` | Queue an immediate sync and re-pricing (triggers a Quartz job) |
| `POST /api/reports/weekly/send?week=` | Send a report now (phase 2) |

- The API computes the UI-ready numbers. The frontend only formats them: £ to 2 dp, pence to 1–2 dp, kWh to 2–3 dp.
- Errors use `ProblemDetails`.

---

## 8. Screens (Next.js; match the prototype)

### Day view (`/`)
- **Date stepper.** Defaults to `latest-complete`. Next is disabled on that day.
- **Data-status line.**
- **Peak alert banner.** Links to `/half-hours?filter=over`.
- **Six KPI tiles:** total, electricity, effective rate, off-peak share, peak import, gas vs limit.
- **Grid import chart:** one bar per slot, coloured by band.
  - Peak bars are dim unless over the limit.
  - A rate ribbon runs underneath.
  - Clicking a bar selects it, and selection is kept in the URL (`?slot=HH:MM`). The chart is a small client component; everything else is a Server Component.
- **Selected-slot panel.**
- **Peak-time import vs limit chart.**
- **Cost by rate table.**
- **Powerwall card:** live charge level with the reserve marker.

### Half-hours (`/half-hours`)
- Filter chips with counts.
- Ledger table: time, band, rate source, unit rate, import (3 dp), Powerwall, cost, alert.
- Totals footer and CSV export.

### Weekly report (`/weekly`)
- Mon–Sun.
- KPI tiles including the comparison with the previous week.
- Stacked daily cost bars.
- Gas per day against the limit.
- Peak-alert table and a "where the money went" breakdown.

### Settings (`/settings`)
- **Connections:**
  - Octopus API key and account number.
  - Tesla token and site picker.
  - Each has a "Test connection" button and a status dot.
  - The form never displays a stored secret again (show "Saved · ending …abcd").
- **Pricing:** dispatch toggle, smart-charge allowance, gas unit and calorific value.
- **Alerts:** peak limit (kWh, default 0.30), source (Tesla live or Octopus next-day), ignore dispatch slots, 80% early warning, gas daily £ limit (default 3.00, including standing charge), channels, cooldown.
- **Weekly report:** on/off, day, time, recipients, included sections.
- Use Server Actions that call the API. Show API validation errors inline.

---

## 9. Security

- **Single-user login at the Next.js layer:**
  - The password hash (argon2 or bcrypt) lives in `APP_PASSWORD_HASH`.
  - The session is an httpOnly, secure, SameSite=Lax cookie.
  - It is enforced in `middleware.ts` for every route except `/login`.
- **Next.js → API:** every request carries `Authorization: Bearer ${INTERNAL_API_TOKEN}`. The API rejects anything else.
  - Use a custom authentication handler or a minimal-API endpoint filter.
  - The API listens only on the Docker network. Don't publish its port.
- **Secrets at rest:**
  - Use **ASP.NET Core Data Protection** (`IDataProtector`, purpose `"EnergyDash.Secrets"`).
  - Persist the key ring to SQL Server (`PersistKeysToDbContext`).
  - Protect the key ring with a certificate or with `DATA_PROTECTION_CERT`.
  - The API and the worker share the same application name and key ring, so both can decrypt.
  - Environment variables override DB secrets.
- **Logging:**
  - Never log secrets.
  - Add a Serilog destructuring policy or enricher that redacts `Authorization` headers and token fields.
  - Turn off `HttpClient` request-header logging.
- Recommend Tailscale or LAN-only exposure of the web app as well.
- `.env.example` must list:
  - `APP_PASSWORD_HASH`, `SESSION_SECRET`, `INTERNAL_API_TOKEN`, `API_BASE_URL` (web)
  - `ConnectionStrings__Default`, `MSSQL_SA_PASSWORD`, `DATA_PROTECTION_CERT`/`DATA_PROTECTION_CERT_PASSWORD`
  - `Octopus__ApiKey`, `Octopus__AccountNumber`
  - `Tesla__ClientId`, `Tesla__ClientSecret`, `Tesla__RefreshToken`, `Tesla__Region`
  - `Smtp__*`, `Ntfy__Topic`
  - `TZ=Europe/London`

---

## 10. Worker jobs (Quartz.NET, all cron in `Europe/London`)

| Job | Schedule | Does |
|---|---|---|
| `OctopusAccountJob` | daily 03:00 + on startup | Refresh meters and agreements |
| `OctopusRatesJob` | daily 03:10 | Fetch rates and standing charges for active and recent tariffs, 2 days ahead |
| `OctopusConsumptionJob` | hourly from 06:00–14:00, then every 4 h | Backfill the last 7 days for elec + gas. On first run, backfill the configurable history (default 90 days) |
| `DispatchPollJob` | every 5 min | Fetch planned dispatches and upsert them |
| `TeslaHistoryJob` | hourly | `calendar_history` for today and yesterday → `BatteryInterval` |
| `TeslaLivePollJob` | every 60 s | `live_status` → `LiveAccumulator`; then evaluate the live peak alert |
| `PricingJob` | after any consumption, rates or dispatch change, plus nightly | Re-price affected days |
| `GasAlertJob` | after gas data lands | Evaluate the gas daily limit |
| `WeeklyReportJob` | configured day and time | Build and send the weekly report (phase 2) |

- Mark jobs `[DisallowConcurrentExecution]`.
- Log every run to `SyncLog`.
- `POST /api/sync` triggers jobs on demand through a shared Quartz store or a DB-backed command queue (your choice; document it).

---

## 11. Alerts and reports (phase 2: build the data model and settings now, delivery later)

- **Peak slot alert, live.** Every 60 seconds the worker adds the Tesla grid import to `LiveAccumulator` for the current slot.
  - If the slot is peak (using the same `PricingEngine` with today's rates and dispatches) and the import passes the limit, fire once per slot.
  - An optional early warning fires at 80%.
  - Dedupe on `AlertEvent (RuleId, SubjectKey = slot start)`. Respect the cooldown.
- **Peak slot alert, reconciliation.** When Octopus data arrives next day, re-check with metered kWh and record any mismatch. Don't re-notify unless configured to.
- **Gas daily alert.** If a day's gas cost including standing charge exceeds the limit, fire once per day.
- **Weekly report.**
  - Render the HTML email in .NET with Razor components through `HtmlRenderer`, and inline styles that use the design tokens.
  - Send with MailKit.
  - The `/weekly` page and the email use the same `GET /api/weeks/{monday}` data.
- **Notifiers:** an `INotifier` interface with Email (MailKit) and ntfy/Pushover first. Web Push can come later.

---

## 12. Deployment (`docker-compose.yml`)

- **Services:**
  - `web`: Next.js standalone output.
  - `api`: ASP.NET Core.
  - `worker`: .NET Worker.
  - `mssql`: `mcr.microsoft.com/mssql/server`, current GA tag, `MSSQL_PID=Express` or `Developer`.
- Give `mssql` a named volume.
- Add healthchecks. `api` and `worker` wait for `mssql` healthy, and `web` waits for `api`.
- The API applies EF migrations on startup behind a flag (`Database__MigrateOnStartup=true`), or through a one-shot `migrator` service. The worker never migrates.
- **SQL Server's Linux container images are x64 only.** The host must be x64; a Raspberry Pi or ARM box cannot run it. Azure SQL Edge is retired, so it is not an option.
- Only `web` publishes a port.

---

## 13. Milestones (finish each with tests passing and a short note in `PROGRESS.md`)

1. **Scaffold.**
   - The .NET solution with the four projects plus tests.
   - Next.js app with tokens, fonts, layout and nav.
   - Docker compose with MSSQL.
   - The API serves **fixture data** that reproduces the prototype's sample data, through the real endpoint shapes.
   - All four pages pixel-match the prototype at 1440px and 390px.
2. **EF model + Octopus ingest.** Migrations, account discovery, agreements, consumption backfill, rates and standing charges, `SyncLog`, "Sync now".
3. **Pricing engine.** All Core tests in §6. Materialise `PricedSlot`/`PricedDay`. The Day and Half-hours endpoints switch from fixtures to real data.
4. **Dispatches.** GraphQL token, dispatch polling and persistence, dispatch override, `DispatchAllowancePolicy`.
5. **Tesla.** OAuth refresh with token rotation, site discovery, `BatteryInterval` backfill, live polling into `LiveAccumulator`, Powerwall card and slot detail.
6. **Weekly endpoint and page** on real data. Gas conversion and the gas limit.
7. **Alerts, notifiers, weekly email** (phase 2).
8. **Hardening.** Auth, Data Protection certificate, log redaction, healthchecks, backup notes for the MSSQL volume.

## 14. Ground rules for you, the agent

- Before writing code against an external API, check the current official docs or live schema. If something here disagrees with them, follow the docs and note the difference in `PROGRESS.md`.
- Don't invent tariff numbers. Prices come from the API only. Fixtures are labelled as sample data.
- Keep business logic in `EnergyDash.Core`. Keep the frontend presentational.
- Ask the owner before changing anything in the pricing rules, the alert semantics or the security model.
- Keep the UI copy from the prototype unless it is factually wrong for the real data.
