"use client";
import { useActionState } from "react";
import Link from "next/link";
import { Header, Notice } from "./Header";
import { TeslaAuthorizationForm } from "./TeslaAuthorizationForm";
import {
  saveSettings,
  saveConnection,
  type FormState,
} from "@/app/settings/actions";
import { syncNow, testConnection } from "@/app/actions";
import type { components } from "@/lib/api/schema";
type Settings = components["schemas"]["DashboardSettings"];
type Connections = components["schemas"]["ConnectionsDto"];
type Site = components["schemas"]["EnergySiteDto"];
const initial: FormState = { message: "", errors: {}, ok: false };
const formId = "settings-form";
function Toggle({
  name,
  label,
  checked,
  sub,
}: {
  name: string;
  label: string;
  checked: boolean;
  sub?: string;
}) {
  return (
    <div className="row between" style={{ flexWrap: "nowrap" }}>
      <div>
        <div style={{ fontWeight: 600 }} id={`label-${name}`}>
          {label}
        </div>
        {sub ? <div className="sub">{sub}</div> : null}
      </div>
      <label className="switch-control">
        <input
          className="switch-input"
          type="checkbox"
          role="switch"
          aria-labelledby={`label-${name}`}
          name={name}
          form={formId}
          defaultChecked={checked}
        />
        <span className="switch-track">
          <span />
        </span>
      </label>
    </div>
  );
}
function Check({
  name,
  label,
  checked,
}: {
  name: string;
  label: string;
  checked: boolean;
}) {
  return (
    <label className="check">
      <input
        type="checkbox"
        name={name}
        defaultChecked={checked}
        form={formId}
      />
      {label}
    </label>
  );
}
function Field({
  name,
  label,
  value,
  type = "number",
  step,
  options,
  errors,
}: {
  name: string;
  label: string;
  value: string | number | null | undefined;
  type?: string;
  step?: string;
  options?: [string, string][];
  errors?: string[];
}) {
  return (
    <div className="field">
      <label htmlFor={name}>{label}</label>
      {options ? (
        <select
          className="input"
          id={name}
          name={name}
          form={formId}
          defaultValue={value ?? ""}
          aria-invalid={errors?.length ? true : undefined}
        >
          {options.map(([v, l]) => (
            <option key={v} value={v}>
              {l}
            </option>
          ))}
        </select>
      ) : (
        <input
          className={`input ${type !== "email" ? "mono" : ""}`}
          id={name}
          name={name}
          form={formId}
          type={type}
          step={step ?? "any"}
          defaultValue={value ?? ""}
          aria-invalid={errors?.length ? true : undefined}
          aria-describedby={errors?.length ? `${name}-error` : undefined}
        />
      )}{" "}
      {errors?.length ? (
        <div className="form-error" id={`${name}-error`}>
          {errors.join(" ")}
        </div>
      ) : null}
    </div>
  );
}
function ConnectionForm({
  provider,
  hint,
  account,
}: {
  provider: "octopus" | "tesla";
  hint?: string | null;
  account?: string | null;
}) {
  const [state, action, pending] = useActionState(saveConnection, initial);
  return (
    <>
      <form action={action} className="block" style={{ border: 0, padding: 0 }}>
        <input type="hidden" name="provider" value={provider} />
        <div className="fields">
          {provider === "octopus" ? (
            <>
              <div className="field">
                <label htmlFor="oe-key">API key</label>
                <input
                  className="input mono"
                  name="apiKey"
                  id="oe-key"
                  type="password"
                  placeholder={hint ? `Saved · ending …${hint}` : "sk_live_…"}
                  autoComplete="off"
                  required
                />
              </div>
              <div className="field">
                <label htmlFor="oe-acc">Account number</label>
                <input
                  className="input mono"
                  name="accountNumber"
                  id="oe-acc"
                  placeholder="A-XXXXXXXX"
                  defaultValue={account ?? ""}
                  required
                />
              </div>
            </>
          ) : (
            <>
              <div className="field">
                <label htmlFor="t-token">OAuth refresh token</label>
                <input
                  className="input mono"
                  name="refreshToken"
                  id="t-token"
                  type="password"
                  placeholder={
                    hint
                      ? `Saved · ending …${hint}`
                      : "Paste authorized refresh token"
                  }
                  autoComplete="off"
                  required
                />
              </div>
              <div className="field">
                <label htmlFor="t-client">Client ID</label>
                <input
                  className="input mono"
                  name="clientId"
                  id="t-client"
                  required
                />
              </div>
              <div className="field">
                <label htmlFor="t-secret">Client secret (optional)</label>
                <input
                  className="input mono"
                  name="clientSecret"
                  id="t-secret"
                  type="password"
                  autoComplete="off"
                />
              </div>
            </>
          )}
        </div>
        <div className="row">
          <button className="btn ghost" disabled={pending}>
            {pending ? "Saving…" : "Save credentials"}
          </button>
          {hint ? <span className="sub">Saved · ending …{hint}</span> : null}
        </div>
        {state.message ? (
          <p role="status" className={state.ok ? "sub" : "form-error"}>
            {state.message} {Object.values(state.errors).flat().join(" ")}
          </p>
        ) : null}
      </form>
      <form action={testConnection}>
        <input type="hidden" name="provider" value={provider} />
        <button className="btn ghost">Test connection</button>
      </form>
    </>
  );
}
export function SettingsForm({
  settings: s,
  connections: c,
  sites,
  notice,
}: {
  settings: Settings;
  connections: Connections;
  sites: Site[];
  notice?: string;
}) {
  const [state, action, pending] = useActionState(saveSettings, initial);
  const f = (
    name: keyof Settings,
    label: string,
    type = "number",
    step?: string,
    options?: [string, string][],
  ) => (
    <Field
      name={name}
      label={label}
      value={s[name] as string | number | null | undefined}
      type={type}
      step={step}
      options={options}
      errors={state.errors[name]}
    />
  );
  return (
    <>
      <Header active="settings">
        <form action={syncNow}>
          <button className="btn">Sync now</button>
        </form>
        <button className="btn primary" form={formId} disabled={pending}>
          {pending ? "Saving…" : "Save changes"}
        </button>
      </Header>
      <form id={formId} action={action} />
      <main>
        <Notice message={notice || state.message} />
        {Object.keys(state.errors).length ? (
          <div className="form-error" role="alert">
            {Object.values(state.errors).flat().join(" ")}
          </div>
        ) : null}
        <h1>Settings &amp; alerts</h1>
        <div className="grid2">
          <section
            className="panel"
            aria-labelledby="h-conn"
            style={{ padding: 22, gap: 20 }}
          >
            <div>
              <h2 id="h-conn" style={{ fontSize: 18 }}>
                Connections
              </h2>
              <p className="sub" style={{ margin: "4px 0 0" }}>
                Keys are stored encrypted on the server and never sent to the
                browser after saving.
              </p>
            </div>
            <div className="block">
              <div className="row between">
                <h3>Octopus Energy account</h3>
                <span className="row sub" style={{ gap: 6 }}>
                  <span
                    className={`dot ${c.octopus.configured ? "" : "off"}`}
                  />
                  {c.octopus.status}
                </span>
              </div>
              <ConnectionForm
                provider="octopus"
                hint={c.octopus.hint}
                account={c.octopus.accountNumber}
              />
              <dl className="kv" style={{ fontSize: 13, gap: "8px 16px" }}>
                <dt>Electricity meter</dt>
                <dd className="mono" style={{ textAlign: "left" }}>
                  Found after connecting (MPAN + serial)
                </dd>
                <dt>Gas meter</dt>
                <dd className="mono" style={{ textAlign: "left" }}>
                  Found after connecting (MPRN + serial)
                </dd>
                <dt>Tariff</dt>
                <dd className="mono" style={{ textAlign: "left" }}>
                  Read from agreements, incl. past tariff changes
                </dd>
              </dl>
            </div>
            <div className="block">
              <div className="row between">
                <h3>Tesla Powerwall</h3>
                <span className="row sub" style={{ gap: 6 }}>
                  <span className={`dot ${c.tesla.configured ? "" : "off"}`} />
                  {c.tesla.status}
                </span>
              </div>
              <TeslaAuthorizationForm />
              <details>
                <summary>Already have a Tesla refresh token?</summary>
                <ConnectionForm provider="tesla" hint={c.tesla.hint} />
              </details>
              {f("teslaSiteId", "Energy site", "text", undefined, [
                ["", "Available after connecting"],
                ...sites.map(
                  (site) =>
                    [String(site.id), site.name ?? `Site ${site.id}`] as [
                      string,
                      string,
                    ],
                ),
              ])}
              <p className="sub" style={{ margin: 0 }}>
                Provides live grid import, battery charge/discharge and state of
                charge every minute. Used for live peak alerts, since Octopus
                meter data usually arrives the next day. Authorize your Tesla
                app with energy_device_data and offline_access.
              </p>
            </div>
          </section>
          <section
            className="panel"
            aria-labelledby="h-tariff"
            style={{ padding: 22, gap: 18 }}
          >
            <div>
              <h2 id="h-tariff" style={{ fontSize: 18 }}>
                Pricing
              </h2>
              <p className="sub" style={{ margin: "4px 0 0" }}>
                How each half-hour gets its unit rate.
              </p>
            </div>
            <ol className="steps">
              {[
                [
                  "Tariff unit rates by time",
                  "Fetched from Octopus for the tariff active on that date, with rate validity times, so price changes and tariff switches are respected.",
                ],
                [
                  "Smart-charge dispatches",
                  "Extra cheap slots Octopus schedules for EV charging outside the normal window are fetched and override the peak rate for those half-hours.",
                ],
                [
                  "Standing charges",
                  "Added per day for electricity and gas, from the same tariff data.",
                ],
              ].map(([title, sub], i) => (
                <li key={title}>
                  <span className="n">{i + 1}</span>
                  <div>
                    <div style={{ fontWeight: 600 }}>{title}</div>
                    <div className="sub">{sub}</div>
                  </div>
                </li>
              ))}
            </ol>
            <Toggle
              name="priceDispatchAtOffPeak"
              label="Price dispatch slots at off-peak rate"
              checked={s.priceDispatchAtOffPeak}
              sub="Turn off to see what the day would have cost without them."
            />
            <Toggle
              name="dispatchAllowanceEnabled"
              label="Apply smart-charge allowance"
              checked={s.dispatchAllowanceEnabled}
              sub="Opt in after checking your account’s rollout and active charging records. Planned dispatch snapshots are estimates."
            />
            <div className="fields">
              {f("dispatchAllowanceSlots", "Allowance (half-hours)")}
              {f("smartFlexDeviceId", "SmartFlex device ID", "text")}
            </div>
            <Check
              name="countScheduledWindowDispatches"
              label="Count smart charging in the normal off-peak window"
              checked={s.countScheduledWindowDispatches}
            />
            <div className="fields">
              {f("nightStart", "Dual-register night starts", "time")}
              {f("nightEnd", "Dual-register night ends", "time")}
            </div>
            {f("gasUnit", "Gas meter reports in", "text", undefined, [
              ["m3", "Cubic metres (convert to kWh with calorific value)"],
              ["kWh", "kWh"],
            ])}
            {f("calorificValue", "Calorific value (MJ/m³)", "number", ".1")}
            {f("historyDays", "Initial history backfill (days)")}
          </section>
          <section
            className="panel"
            aria-labelledby="h-alerts"
            style={{ padding: 22, gap: 20 }}
          >
            <div>
              <h2 id="h-alerts" style={{ fontSize: 18 }}>
                Alerts
              </h2>
              <p className="sub" style={{ margin: "4px 0 0" }}>
                Rules are evaluated by the worker using the same half-hour
                pricing.
              </p>
            </div>
            <div className="block">
              <Toggle
                name="peakAlertEnabled"
                label="Peak-time usage"
                checked={s.peakAlertEnabled}
              />
              <div className="fields">
                {f(
                  "peakLimitKwh",
                  "Alert when one half-hour exceeds (kWh)",
                  "number",
                  ".05",
                )}
                {f("peakSource", "Check using", "text", undefined, [
                  ["tesla", "Tesla Powerwall · live"],
                  ["octopus", "Octopus meter · next day"],
                ])}
              </div>
              <Check
                name="ignoreDispatch"
                label="Ignore smart-charge dispatch slots (they are billed off-peak)"
                checked={s.ignoreDispatch}
              />
              <Check
                name="earlyWarning"
                label="Warn mid-slot when the running total passes 80% of the limit"
                checked={s.earlyWarning}
              />
              <Check
                name="renotifyReconciliation"
                label="Notify again if metered data changes a live alert"
                checked={s.renotifyReconciliation}
              />
            </div>
            <div className="block">
              <Toggle
                name="gasAlertEnabled"
                label="Daily gas cost"
                checked={s.gasAlertEnabled}
              />
              {f(
                "gasDailyLimitPounds",
                "Alert when a day’s gas cost exceeds (£ incl. standing)",
                "number",
                ".25",
              )}
              <p className="sub" style={{ margin: 0 }}>
                Checked when the day’s gas readings arrive from Octopus, usually
                the following morning.
              </p>
            </div>
            <div className="block" style={{ gap: 10 }}>
              <h3>Send alerts by</h3>
              <div className="row" style={{ gap: "4px 24px" }}>
                <Check
                  name="notifyPush"
                  label="Push notification (ntfy)"
                  checked={s.notifyPush}
                />
                <Check
                  name="notifyEmail"
                  label="Email"
                  checked={s.notifyEmail}
                />
              </div>
              {f(
                "cooldownMinutes",
                "Don’t repeat the same alert within",
                "text",
                undefined,
                [
                  ["0", "Always alert"],
                  ["60", "1 hour"],
                  ["180", "3 hours"],
                ],
              )}
            </div>
          </section>
          <section
            className="panel"
            aria-labelledby="label-weeklyEnabled"
            style={{ padding: 22, gap: 18 }}
          >
            <Toggle
              name="weeklyEnabled"
              label="Weekly report"
              checked={s.weeklyEnabled}
              sub="Covers Monday to Sunday."
            />
            <div className="sub">
              <Link href="/weekly">Preview last week’s report</Link>
            </div>
            <div className="fields">
              {f("weekDay", "Send on", "text", undefined, [
                ["mon", "Monday"],
                ["tue", "Tuesday"],
                ["wed", "Wednesday"],
                ["thu", "Thursday"],
                ["fri", "Friday"],
                ["sat", "Saturday"],
                ["sun", "Sunday evening"],
              ])}
              {f("weekTime", "At (Europe/London)", "time")}
            </div>
            {f("recipients", "Email to (comma-separated)", "text")}
            <fieldset
              className="field"
              style={{ margin: 0, padding: 0, border: 0, gap: 2 }}
            >
              <legend style={{ padding: "0 0 6px" }}>Include</legend>
              <Check
                name="includeCosts"
                label="Cost by day and by rate"
                checked={s.includeCosts}
              />
              <Check
                name="includeAlerts"
                label="Peak-time alerts list"
                checked={s.includeAlerts}
              />
              <Check
                name="includeGas"
                label="Gas against daily limit"
                checked={s.includeGas}
              />
              <Check
                name="includeBattery"
                label="Powerwall saving"
                checked={s.includeBattery}
              />
              <Check
                name="attachCsv"
                label="CSV of all half-hours attached"
                checked={s.attachCsv}
              />
            </fieldset>
            <p className="sub">
              Reports use the most recent completed Monday–Sunday week.
              Incomplete weeks are held until meter data arrives.
            </p>
            <button className="btn primary" form={formId} disabled={pending}>
              {pending ? "Saving…" : "Save changes"}
            </button>
          </section>
        </div>
      </main>
    </>
  );
}
