import Link from "next/link";
import { Header, Kpi } from "@/components/Header";
import { HalfHourChart } from "@/components/HalfHourChart";
import { getDay, getStatus, latestDay } from "@/lib/api/client";
import {
  bandLabel,
  color,
  dayLabel,
  gbp,
  londonToday,
  number,
  safeDate,
  shiftDate,
} from "@/lib/format";

export default async function DayPage({
  searchParams,
}: {
  searchParams: Promise<{ date?: string; slot?: string; slotUtc?: string }>;
}) {
  const [query, latest, status] = await Promise.all([
    searchParams,
    latestDay(),
    getStatus(),
  ]);
  const date = safeDate(
    query.date,
    latest.date ?? shiftDate(londonToday(), -1),
  );
  const day = await getDay(date);
  const sel =
    day.slots.find((s) => s.startUtc === query.slotUtc) ??
    day.slots.find((s) => s.startLocal === query.slot) ??
    day.slots.find((s) => s.startLocal === "18:00") ??
    day.slots[0];
  const k = day.kpis;
  const config = day.settings;
  const peak = day.slots.filter((s) => s.band === "peak");
  const maxPeak = Math.max(
    0.5,
    config.peakLimitKwh * 1.2,
    ...peak.map((s) => s.kwh ?? 0),
  );
  const normalOff = day.slots
    .filter((s) => s.band === "off" && s.ratePence != null)
    .map((s) => s.ratePence!);
  const high = Math.max(0, ...day.slots.map((s) => s.ratePence ?? 0));
  const selectedNote =
    sel.kwh == null
      ? "Meter reading missing. This half-hour is excluded from cost totals until the data arrives."
      : sel.band === "peak"
        ? sel.overLimit
          ? `Above the ${number(config.peakLimitKwh)} kWh alert limit. ${sel.batteryKwh == null ? "Battery history is unavailable." : `Powerwall supplied ${number(Math.max(0, -sel.batteryKwh))} kWh; the rest came from the grid at peak rate.`}`
          : "Within the alert limit. Battery flows show how the house load was supplied."
        : `Billed at off-peak. At the peak rate this slot would have cost ${number(sel.kwh * high, 1)}p.${sel.band === "disp" ? " Detected from the EV charging dispatch, not the fixed window." : ""}`;
  const battery = day.powerwall;
  return (
    <>
      <Header active="day">
        <Link
          className="btn icon"
          aria-label="Previous day"
          href={`/?date=${shiftDate(date, -1)}`}
        >
          ‹
        </Link>
        <div style={{ padding: "0 12px", textAlign: "center" }}>
          <div style={{ fontWeight: 600 }}>{dayLabel(date)}</div>
          <div className="muted" style={{ fontSize: 12 }}>
            {!day.complete
              ? "Partial data"
              : date === latest.date
              ? date === shiftDate(londonToday(), -1)
                ? "Yesterday · latest complete day"
                : "Latest complete day"
              : day.complete
                ? "Historical day"
                : "Partial data"}
          </div>
        </div>
        {latest.date && date < latest.date ? (
          <Link
            className="btn icon"
            aria-label="Next day"
            href={`/?date=${shiftDate(date, 1)}`}
          >
            ›
          </Link>
        ) : (
          <button className="btn icon" aria-label="Next day" disabled>
            ›
          </button>
        )}
      </Header>
      <main>
        <div className="status">
          <span>
            <span className={`dot ${status.octopusDataUtc ? "" : "off"}`} />
            {day.sample
              ? "Octopus meter data to 23:59 Sun"
              : status.octopusDataUtc
                ? `Octopus meter data to ${new Date(status.octopusDataUtc).toLocaleString("en-GB", { timeZone: "Europe/London" })}`
                : "Octopus · no meter data yet"}
          </span>
          <span>
            <span className={`dot ${status.teslaSyncUtc ? "" : "off"}`} />
            {day.sample
              ? "Tesla Powerwall · sample reading 06:38"
              : status.teslaSyncUtc
                ? `Tesla synced ${new Date(status.teslaSyncUtc).toLocaleTimeString("en-GB", { timeZone: "Europe/London", hour: "2-digit", minute: "2-digit" })}`
                : "Tesla · not connected"}
          </span>
          <span>
            <span className={`dot ${day.complete ? "" : "off"}`} />
            {day.complete
              ? "Rates matched per half-hour incl. dispatches"
              : day.missingSlots === 0 && day.unpricedSlots === 0
                ? "Tariff rates matched · pricing checks pending"
                : `${day.missingSlots} missing readings · ${day.unpricedSlots} unpriced slots`}
          </span>
          {day.sample ? <span className="pill">Sample figures</span> : null}
        </div>
        {!day.complete ? (
          <div className="note" role="status">
            <strong>
              Partial data · totals exclude missing readings and prices.
            </strong>{" "}
            {day.warnings.join(" ")}
          </div>
        ) : null}
        {day.alerts.length ? (
          <div className="alert" role="status">
            <div className="row" style={{ gap: 12 }}>
              <span style={{ color: "var(--peak)", fontSize: 24 }} aria-hidden>
                ⚠
              </span>
              <div>
                <div className="t">
                  Peak-time alert · {day.alerts.length} half-hours above{" "}
                  <span className="mono">{number(config.peakLimitKwh)}</span>{" "}
                  kWh
                </div>
                <div className="d">
                  {day.alerts
                    .map(
                      (s) =>
                        `${s.startLocal} ${number(s.kwh)} kWh (${number(s.costPence, 1)}p)`,
                    )
                    .join("  ·  ")}
                </div>
              </div>
            </div>
            <Link href={`/half-hours?date=${date}&filter=over`}>
              Review half-hours
            </Link>
          </div>
        ) : null}
        <section className="kpis" aria-label="Day totals">
          <Kpi
            label="Total cost"
            value={gbp(k.totalPence)}
            sub="Electricity + gas incl. standing charges"
          />
          <Kpi
            label="Electricity"
            value={gbp(k.electricityPence)}
            sub={`${number(k.importKwh, 1)} kWh imported`}
          />
          <Kpi
            label="Effective unit rate"
            value={`${number(k.effectiveRatePence)}p`}
            sub="Per kWh, excl. standing charge"
            tone="link"
          />
          <Kpi
            label="Imported off-peak"
            value={`${number(k.offPeakSharePercent, 0)}%`}
            sub={`${number(
              day.slots
                .filter((s) => s.band === "disp")
                .reduce((sum, s) => sum + (s.kwh ?? 0), 0),
              1,
            )} kWh in dispatch slots`}
            tone="link"
          />
          <Kpi
            label="Peak import"
            value={`${number(k.peakImportKwh)} kWh`}
            sub={`${day.alerts.length} slots over ${number(config.peakLimitKwh)} kWh`}
            tone={day.alerts.length ? "peak" : undefined}
          />
          <Kpi
            label="Gas"
            value={gbp(k.gasPence)}
            sub={`${number(k.gasKwh, 1)} kWh · daily limit ${gbp(config.gasDailyLimitPounds * 100)}`}
            tone={
              k.gasPence > config.gasDailyLimitPounds * 100
                ? "warn-text"
                : undefined
            }
          />
        </section>
        <div className="split">
          <section
            className="panel main"
            aria-label="Grid import by half-hour"
            style={{ gap: 16 }}
          >
            <div
              className="row between"
              style={{ alignItems: "baseline", gap: 12 }}
            >
              <div>
                <h2>Grid import by half-hour</h2>
                <div className="sub">
                  Each bar is billed at the rate that applied in that slot.
                  Select a bar for detail.
                </div>
              </div>
              <div className="legend">
                {["off", "disp", "peak"].map((b) => (
                  <span key={b}>
                    <span
                      className="sw"
                      style={{ background: color(b, true) }}
                    />
                    {bandLabel(b)}
                  </span>
                ))}
              </div>
            </div>
            <HalfHourChart
              slots={day.slots}
              selected={sel.startUtc}
              date={date}
            />
          </section>
          <aside
            className="panel side"
            aria-label="Selected half-hour"
            style={{ gap: 16 }}
          >
            <div>
              <div className="sub">Selected half-hour</div>
              <div className="mono" style={{ fontSize: 26, fontWeight: 500 }}>
                {sel.startLocal}–{sel.endLocal}
              </div>
              {day.slots.filter((s) => s.startLocal === sel.startLocal).length >
              1 ? (
                <div className="sub mono">UTC offset {sel.localOffset}</div>
              ) : null}
              <div
                className="badge"
                style={{ background: color(sel.band, true) }}
              >
                {sel.band === "off"
                  ? "Off-peak · scheduled window"
                  : sel.band === "disp"
                    ? "Off-peak · smart-charge dispatch"
                    : "Peak"}
              </div>
            </div>
            <dl className="kv">
              <dt>Unit rate</dt>
              <dd className="mono">{number(sel.ratePence)}p/kWh</dd>
              <dt>Rate source</dt>
              <dd>{sel.rateSource}</dd>
              <dt>Grid import</dt>
              <dd className="mono">{number(sel.kwh)} kWh</dd>
              <dt>Cost</dt>
              <dd className="mono" style={{ fontWeight: 600 }}>
                {number(sel.costPence, 1)}p
              </dd>
              <dt>Powerwall</dt>
              <dd className="mono">
                {sel.batteryKwh == null
                  ? "Unavailable"
                  : sel.batteryKwh > 0
                    ? `charging +${number(sel.batteryKwh)} kWh`
                    : sel.batteryKwh < 0
                      ? `supplying ${number(-sel.batteryKwh)} kWh`
                      : "idle"}
              </dd>
              <dt>House load</dt>
              <dd className="mono">{number(sel.houseKwh)} kWh</dd>
            </dl>
            <div className="note">{selectedNote}</div>
          </aside>
        </div>
        <div className="grid3">
          <section
            className="panel"
            aria-label="Peak-time import against alert limit"
          >
            <div>
              <h2>Peak-time import vs alert limit</h2>
              <div className="sub">
                Peak slots only. Dispatch slots excluded.
              </div>
            </div>
            <div className="peakplot">
              <div
                className="limit"
                style={{ bottom: (config.peakLimitKwh / maxPeak) * 150 }}
              />
              <div
                className="limlab"
                style={{ bottom: (config.peakLimitKwh / maxPeak) * 150 + 4 }}
              >
                limit {number(config.peakLimitKwh)} kWh
              </div>
              <div className="bars">
                {peak.map((s) => (
                  <div
                    key={s.startUtc}
                    title={`${s.startLocal}: ${number(s.kwh)} kWh`}
                    style={{
                      height:
                        s.kwh == null
                          ? 0
                          : Math.max(2, (s.kwh / maxPeak) * 150),
                      background: color("peak", s.overLimit),
                    }}
                  />
                ))}
              </div>
            </div>
            <div className="xlab">
              <span>{peak[0]?.startLocal ?? "—"}</span>
              <span>12:00</span>
              <span>18:00</span>
              <span>{peak.at(-1)?.endLocal ?? "—"}</span>
            </div>
          </section>
          <section className="panel" aria-label="Cost by rate">
            <h2>Cost by rate</h2>
            <div className="scroll">
              <table>
                <thead>
                  <tr>
                    <th className="l" scope="col">
                      Line
                    </th>
                    <th scope="col">kWh</th>
                    <th scope="col">Rate</th>
                    <th scope="col">Cost</th>
                  </tr>
                </thead>
                <tbody>
                  {[...day.costByRate]
                    .sort(
                      (a, b) =>
                        [
                          "Off-peak window",
                          "Dispatch slots",
                          "Peak",
                          "Elec. standing",
                          "Gas",
                          "Gas standing",
                        ].indexOf(a.label) -
                        [
                          "Off-peak window",
                          "Dispatch slots",
                          "Peak",
                          "Elec. standing",
                          "Gas",
                          "Gas standing",
                        ].indexOf(b.label),
                    )
                    .map((r, i) => (
                      <tr key={i}>
                        <th scope="row">
                          <span className="row" style={{ gap: 8 }}>
                            <span
                              className="sw sm"
                              style={{ background: color(r.band, true) }}
                            />
                            {r.label}
                          </span>
                        </th>
                        <td>{number(r.kwh)}</td>
                        <td>
                          {number(r.ratePence)}
                          {r.perDay ? "p/d" : "p"}
                        </td>
                        <td>{gbp(r.costPence)}</td>
                      </tr>
                    ))}
                </tbody>
                <tfoot>
                  <tr>
                    <th scope="row">Day total</th>
                    <td />
                    <td />
                    <td>{gbp(k.totalPence)}</td>
                  </tr>
                </tfoot>
              </table>
            </div>
          </section>
          <section className="panel" aria-label="Powerwall">
            <div className="row between" style={{ alignItems: "baseline" }}>
              <h2>Powerwall</h2>
              <span className="sub">
                {day.sample ? "Sample figures" : "From Tesla"}
              </span>
            </div>
            <div className="row" style={{ gap: 10, alignItems: "baseline" }}>
              <span className="mono" style={{ fontSize: 36, fontWeight: 500 }}>
                {number(battery.chargePercent, 0)}%
              </span>
              <span className="sub">
                charged · {number(battery.reservePercent, 0)}% reserved for
                outages
              </span>
            </div>
            <div
              className="meter"
              role="img"
              aria-label={`Battery ${number(battery.chargePercent, 0)} percent, ${number(battery.reservePercent, 0)} percent reserve`}
            >
              <i style={{ width: `${battery.chargePercent ?? 0}%` }} />
              <b style={{ left: `${battery.reservePercent ?? 0}%` }} />
            </div>
            <dl className="kv" style={{ gridTemplateColumns: "1fr auto" }}>
              <dt>Charged at off-peak rate</dt>
              <dd className="mono">{number(battery.chargedKwh, 1)} kWh</dd>
              <dt>Supplied house in peak</dt>
              <dd className="mono">{number(battery.dischargedKwh, 1)} kWh</dd>
              <dt>Saved vs buying at peak</dt>
              <dd
                className="mono"
                style={{ color: "var(--good)", fontWeight: 600 }}
              >
                {gbp(battery.savingPence)}
              </dd>
            </dl>
            {!day.sample &&
            battery.syncedUtc &&
            Date.now() - new Date(battery.syncedUtc).getTime() > 120000 ? (
              <div className="sub">
                Live reading is stale. Last sync{" "}
                {new Date(battery.syncedUtc).toLocaleString("en-GB", {
                  timeZone: "Europe/London",
                })}
                .
              </div>
            ) : null}
            {!day.sample && !normalOff.length ? (
              <div className="sub">Off-peak rates are unavailable.</div>
            ) : null}
          </section>
        </div>
      </main>
    </>
  );
}
