export const gbp = (pence: number | null | undefined) =>
  pence == null ? "—" : "£" + (pence / 100).toFixed(2);
export const number = (value: number | null | undefined, digits = 2) =>
  value == null ? "—" : value.toFixed(digits);
export function shiftDate(day: string, amount: number): string {
  const date = new Date(day + "T12:00:00Z");
  date.setUTCDate(date.getUTCDate() + amount);
  return date.toISOString().slice(0, 10);
}
export const dayLabel = (day: string) =>
  new Intl.DateTimeFormat("en-GB", {
    weekday: "short",
    day: "numeric",
    month: "short",
    year: "numeric",
    timeZone: "Europe/London",
  })
    .format(new Date(day + "T12:00:00Z"))
    .replace(",", "");
export function londonToday(): string {
  return new Intl.DateTimeFormat("en-CA", {
    timeZone: "Europe/London",
    year: "numeric",
    month: "2-digit",
    day: "2-digit",
  }).format(new Date());
}
export function mondayFor(day: string): string {
  const weekday = new Date(day + "T12:00:00Z").getUTCDay();
  return shiftDate(day, -(weekday === 0 ? 6 : weekday - 1));
}
export function safeDate(value: string | undefined, fallback: string): string {
  if (!value || !/^\d{4}-\d{2}-\d{2}$/.test(value)) return fallback;
  const parsed = new Date(value + "T12:00:00Z");
  return !Number.isNaN(parsed.getTime()) &&
    parsed.toISOString().slice(0, 10) === value
    ? value
    : fallback;
}
export const bandLabel = (band: string) =>
  band === "off"
    ? "Off-peak window"
    : band === "disp"
      ? "Smart-charge dispatch"
      : band === "std"
        ? "Standard"
        : "Peak";
export const color = (band: string, over = false) =>
  `var(--${band === "off" ? "offpeak" : band === "disp" ? "dispatch" : band === "peak" ? (over ? "peak" : "peak-dim") : band})`;
