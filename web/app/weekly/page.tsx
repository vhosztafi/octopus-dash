import Link from "next/link";
import { Header, Kpi, Notice } from "@/components/Header";
import { getWeek } from "@/lib/api/client";
import {
  dayLabel,
  gbp,
  londonToday,
  mondayFor,
  number,
  safeDate,
  shiftDate,
} from "@/lib/format";
import { sendReport } from "../actions";
export default async function WeeklyPage({
  searchParams,
}: {
  searchParams: Promise<{ week?: string; notice?: string }>;
}) {
  const query = await searchParams;
  const week = mondayFor(
    safeDate(query.week, shiftDate(mondayFor(londonToday()), -7)),
  );
  const report = await getWeek(week);
  const k = report.kpis;
  const gasLimit = report.settings.gasDailyLimitPounds * 100;
  const maxGas = Math.max(400, gasLimit, ...report.days.map((d) => d.gasPence));
  const maxCost = Math.max(700, ...report.days.map((d) => d.totalPence));
  const scale = (pence: number) => Math.max(2, (pence / maxCost) * 200);
  const breakdown = [
    [
      "Off-peak electricity",
      "offpeak",
      report.days.reduce((s, d) => s + d.offPence, 0),
    ],
    [
      "Peak electricity",
      "peak",
      report.days.reduce((s, d) => s + d.peakPence, 0),
    ],
    [
      "Standing charges (elec.)",
      "standing",
      report.days.reduce((s, d) => s + d.standingPence, 0),
    ],
    ["Gas incl. standing", "gas", k.gasPence],
    ["Total", "transparent", k.totalPence],
  ] as const;
  return (
    <>
      <Header active="weekly">
        <Link className="btn" href={`/weekly?week=${shiftDate(week, -7)}`}>
          Previous week
        </Link>
        <form action={sendReport}>
          <input type="hidden" name="week" value={week} />
          <button className="btn primary">Send report now</button>
        </form>
      </Header>
      <main>
        <Notice message={query.notice} />
        <section>
          <h1>Weekly report</h1>
          <div className="muted">
            {dayLabel(week)} – {dayLabel(shiftDate(week, 6))} · Monday to Sunday
            {report.sample ? " · Sample figures" : ""}
          </div>
        </section>
        {!report.complete ? (
          <div className="note">
            Partial week. Totals exclude missing data; comparison and delivery
            require a complete week.
          </div>
        ) : null}
        <section className="kpis" aria-label="Week totals">
          <Kpi
            label="Week total"
            value={gbp(k.totalPence)}
            sub="Electricity + gas incl. standing"
          />
          <Kpi
            label="vs previous week"
            value={
              report.differencePence == null
                ? "—"
                : `${report.differencePence < 0 ? "−" : "+"}${gbp(Math.abs(report.differencePence))}`
            }
            sub={`Previous week ${gbp(report.previousTotalPence)}`}
            tone={
              report.differencePence != null && report.differencePence < 0
                ? "good"
                : "warn-text"
            }
          />
          <Kpi
            label="Imported off-peak"
            value={`${number(k.offPeakSharePercent, 0)}%`}
            sub="Share of grid kWh, incl. dispatch slots"
            tone="link"
          />
          <Kpi
            label="Peak-time alerts"
            value={String(k.alertCount)}
            sub={`Half-hours over ${number(report.settings.peakLimitKwh)} kWh`}
            tone="peak"
          />
          <Kpi
            label="Gas over limit"
            value={`${report.gasOverDays} days`}
            sub={`Daily limit ${gbp(gasLimit)}`}
            tone={report.gasOverDays ? "warn-text" : undefined}
          />
          <Kpi
            label="Powerwall saving"
            value={gbp(k.batterySavingPence)}
            sub="Peak load shifted to off-peak"
            tone="good"
          />
        </section>
        <div className="grid2">
          <section className="panel" aria-label="Daily cost">
            <div className="row between">
              <div>
                <h2>Cost by day</h2>
                <div className="sub">
                  Electricity + gas including standing charges.
                </div>
              </div>
              <div className="legend">
                {[
                  ["offpeak", "Off-peak elec."],
                  ["peak", "Peak elec."],
                  ["standing", "Standing"],
                  ["gas", "Gas"],
                ].map(([c, l]) => (
                  <span key={c}>
                    <span
                      className="sw"
                      style={{ background: `var(--${c})` }}
                    />
                    {l}
                  </span>
                ))}
              </div>
            </div>
            <div className="scroll weekly-scroll">
              <div className="stack-chart">
                {report.days.map((d) => (
                  <div key={d.date}>
                    <div
                      className="mono"
                      style={{
                        textAlign: "center",
                        fontSize: 12,
                        color: "var(--text-2)",
                      }}
                    >
                      {gbp(d.totalPence)}
                      {d.complete ? "" : "*"}
                    </div>
                    <div className="stack">
                      {[
                        [d.offPence, "offpeak"],
                        [d.peakPence, "peak"],
                        [d.standingPence, "standing"],
                        [d.gasPence, "gas"],
                      ].map(([v, c]) => (
                        <div
                          key={String(c)}
                          style={{
                            height: scale(Number(v)),
                            background: `var(--${c})`,
                          }}
                        />
                      ))}
                    </div>
                  </div>
                ))}
              </div>
              <div className="days">
                {report.days.map((d) => (
                  <div key={d.date}>{d.name}</div>
                ))}
              </div>
            </div>
          </section>
          <section
            className="panel"
            aria-label="Gas per day against limit"
            style={{ gap: 16 }}
          >
            <div>
              <h2>Gas per day vs {gbp(gasLimit)} limit</h2>
              <div className="sub">
                Including standing charge. Days over the limit triggered an
                alert.
              </div>
            </div>
            <div style={{ display: "flex", flexDirection: "column", gap: 10 }}>
              {report.days.map((d) => (
                <div className="gasrow" key={d.date}>
                  <span>{d.name}</span>
                  <div
                    className="gasbar"
                    role="img"
                    aria-label={`${d.name} gas ${gbp(d.gasPence)}, limit ${gbp(gasLimit)}`}
                  >
                    <i
                      style={{
                        width: `${Math.min(100, (d.gasPence / maxGas) * 100)}%`,
                        background:
                          d.gasPence > gasLimit
                            ? "var(--warn-text)"
                            : "var(--gas)",
                      }}
                    />
                    <b style={{ left: `${(gasLimit / maxGas) * 100}%` }} />
                  </div>
                  <span
                    className="mono"
                    style={{
                      textAlign: "right",
                      color:
                        d.gasPence > gasLimit ? "var(--warn-text)" : undefined,
                    }}
                  >
                    {gbp(d.gasPence)}
                  </span>
                </div>
              ))}
            </div>
            <div className="sub" style={{ fontSize: 12 }}>
              Scale £0–{gbp(maxGas)} · white mark = limit
            </div>
          </section>
        </div>
        <div className="grid2">
          <section className="panel" aria-label="Peak-time alerts this week">
            <h2>Peak-time alerts this week</h2>
            <div className="scroll">
              <table>
                <thead>
                  <tr>
                    <th className="l" scope="col">
                      Day
                    </th>
                    <th className="l" scope="col">
                      Half-hour
                    </th>
                    <th scope="col">Import</th>
                    <th scope="col">Cost</th>
                  </tr>
                </thead>
                <tbody>
                  {report.alerts.map((a, i) => (
                    <tr key={i}>
                      <th scope="row">
                        {new Date(a.date + "T12:00:00Z").toLocaleDateString(
                          "en-GB",
                          { weekday: "short", timeZone: "Europe/London" },
                        )}
                      </th>
                      <td className="l">{a.time}</td>
                      <td style={{ color: "var(--warn-text)" }}>
                        {number(a.kwh)} kWh
                      </td>
                      <td>{number(a.costPence, 1)}p</td>
                    </tr>
                  ))}
                  {!report.alerts.length ? (
                    <tr>
                      <td colSpan={4}>No half-hours over the limit.</td>
                    </tr>
                  ) : null}
                </tbody>
              </table>
            </div>
          </section>
          <section className="panel" aria-label="Where the money went">
            <h2>Where the money went</h2>
            <dl
              className="kv"
              style={{ gridTemplateColumns: "1fr auto", gap: "12px 16px" }}
            >
              {breakdown.map(([label, c, value]) => (
                <div key={label} style={{ display: "contents" }}>
                  <dt
                    style={{
                      color: "var(--text-2)",
                      display: "flex",
                      alignItems: "center",
                      gap: 8,
                    }}
                  >
                    <span
                      className="sw sm"
                      style={{
                        background:
                          c === "transparent" ? "transparent" : `var(--${c})`,
                      }}
                    />
                    {label}
                  </dt>
                  <dd className="mono">{gbp(value)}</dd>
                </div>
              ))}
            </dl>
            <div className="note">
              Peak import cost {gbp(breakdown[1][2])}.{" "}
              {report.alerts.filter((a) => /^1[78]:/.test(a.time)).length} of{" "}
              {report.alerts.length} alerts fell between 17:00 and 19:00.
            </div>
          </section>
        </div>
      </main>
    </>
  );
}
