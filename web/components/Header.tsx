import Link from "next/link";
export function Header({
  active,
  children,
}: {
  active: string;
  children?: React.ReactNode;
}) {
  return (
    <header className="row between">
      <div className="row" style={{ gap: 28 }}>
        <Link href="/" className="brand">
          Home energy
        </Link>
        <nav className="nav" aria-label="Sections">
          {[
            ["/", "Day", "day"],
            ["/half-hours", "Half-hours", "slots"],
            ["/weekly", "Weekly report", "weekly"],
            ["/settings", "Settings & alerts", "settings"],
          ].map(([href, label, id]) => (
            <Link
              key={id}
              href={href}
              aria-current={active === id ? "page" : undefined}
            >
              {label}
            </Link>
          ))}
        </nav>
      </div>
      <div className="hdr-right">{children}</div>
    </header>
  );
}
export function Kpi({
  label,
  value,
  sub,
  tone,
}: {
  label: string;
  value: string;
  sub: string;
  tone?: string;
}) {
  return (
    <div className="kpi">
      <div className="l">{label}</div>
      <div className="v" style={{ color: tone ? `var(--${tone})` : undefined }}>
        {value}
      </div>
      <div className="s">{sub}</div>
    </div>
  );
}
export function Notice({ message }: { message?: string }) {
  return message ? (
    <div className="note" role="status">
      {message}
    </div>
  ) : null;
}
