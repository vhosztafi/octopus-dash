import Link from "next/link";
import { Header } from "@/components/Header";
import { getLedger, latestDay } from "@/lib/api/client";
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
export default async function LedgerPage({
  searchParams,
}: {
  searchParams: Promise<{ date?: string; filter?: string }>;
}) {
  const [query, latest] = await Promise.all([searchParams, latestDay()]);
  const date = safeDate(
    query.date,
    latest.date ?? shiftDate(londonToday(), -1),
  );
  const filter = ["all", "peak", "off", "disp", "over"].includes(
    query.filter ?? "",
  )
    ? query.filter!
    : "all";
  const data = await getLedger(date, filter);
  return (
    <>
      <Header active="slots">
        <span style={{ fontWeight: 600 }}>{dayLabel(date)}</span>
        <a
          className="btn"
          href={`/api/export/half-hours?date=${date}&filter=${filter}`}
        >
          ↓ Export CSV
        </a>
      </Header>
      <main>
        <section style={{ display: "flex", flexDirection: "column", gap: 6 }}>
          <h1>Half-hour ledger</h1>
          <p className="muted" style={{ margin: 0, maxWidth: 760 }}>
            Every slot is priced with the unit rate in force for that half-hour:
            the tariff’s fixed windows, plus any extra cheap slots Octopus
            granted for smart EV charging. Battery flows come from Tesla.{" "}
            {data.sample ? "Sample figures." : ""}
          </p>
        </section>
        <div className="chips" role="group" aria-label="Filter slots">
          {[
            ["all", `All ${data.counts.all} slots`],
            ["peak", "Peak"],
            ["off", "Off-peak window"],
            ["disp", "Dispatch slots"],
            ["over", "Over alert limit"],
          ].map(([id, label]) => (
            <Link
              className="chip"
              key={id}
              href={`/half-hours?date=${date}&filter=${id}`}
              role="button"
              aria-pressed={filter === id}
            >
              {label} · <span className="mono">{data.counts[id]}</span>
            </Link>
          ))}
        </div>
        {data.missingSlots ? (
          <div className="note">
            {data.missingSlots} missing readings. Totals exclude gaps.
          </div>
        ) : null}
        <div className="ledger">
          <table>
            <thead>
              <tr>
                {[
                  "Half-hour",
                  "Rate band",
                  "Rate source",
                  "Unit rate",
                  "Grid import",
                  "Powerwall",
                  "Cost",
                  "Alert",
                ].map((h, i) => (
                  <th
                    key={h}
                    scope="col"
                    className={i < 3 || i === 7 ? "l" : undefined}
                  >
                    {h}
                  </th>
                ))}
              </tr>
            </thead>
            <tbody>
              {data.slots.map((s) => (
                <tr
                  key={s.startUtc}
                  className={s.overLimit ? "over" : undefined}
                >
                  <th
                    scope="row"
                    className="mono"
                    style={{ fontFamily: "var(--mono)" }}
                  >
                    <Link
                      href={`/?date=${date}&slot=${s.startLocal}&slotUtc=${encodeURIComponent(s.startUtc)}`}
                    >
                      {s.startLocal}–{s.endLocal}
                    </Link>
                    <span className="sub"> {s.localOffset}</span>
                  </th>
                  <td className="l" style={{ fontFamily: "var(--sans)" }}>
                    <span className="row" style={{ gap: 8 }}>
                      <span
                        className="sw sm"
                        style={{ background: color(s.band, true) }}
                      />
                      {s.band === "disp"
                        ? "Off-peak (dispatch)"
                        : s.band === "off"
                          ? "Off-peak"
                          : bandLabel(s.band)}
                    </span>
                  </td>
                  <td
                    className="l"
                    style={{
                      fontFamily: "var(--sans)",
                      color: "var(--text-2)",
                    }}
                  >
                    {s.rateSource}
                  </td>
                  <td>{number(s.ratePence)}p</td>
                  <td>{number(s.kwh, 3)} kWh</td>
                  <td
                    style={{
                      color:
                        s.batteryKwh == null
                          ? "var(--faint)"
                          : s.batteryKwh > 0
                            ? "var(--link)"
                            : "var(--good)",
                    }}
                  >
                    {s.batteryKwh == null
                      ? "—"
                      : s.batteryKwh > 0
                        ? `+${number(s.batteryKwh)} charge`
                        : s.batteryKwh < 0
                          ? `${number(s.batteryKwh)} supply`
                          : "—"}
                  </td>
                  <td style={{ fontWeight: 600 }}>{number(s.costPence)}p</td>
                  <td
                    className="l"
                    style={{
                      fontFamily: "var(--sans)",
                      color: "var(--warn-text)",
                      fontWeight: 600,
                    }}
                  >
                    {s.overLimit
                      ? "Over limit"
                      : s.kwh == null
                        ? "Missing data"
                        : ""}
                  </td>
                </tr>
              ))}
              {!data.slots.length ? (
                <tr>
                  <td colSpan={8} className="l">
                    No slots match this filter.
                  </td>
                </tr>
              ) : null}
            </tbody>
            <tfoot>
              <tr>
                <th colSpan={4} scope="row">
                  Total for {data.slots.length} slots (excl. standing charge)
                </th>
                <td>{number(data.importKwh)} kWh</td>
                <td />
                <td>{gbp(data.costPence)}</td>
                <td />
              </tr>
            </tfoot>
          </table>
        </div>
      </main>
    </>
  );
}
