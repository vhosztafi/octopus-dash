"use client";
import { useRouter } from "next/navigation";
import type { components } from "@/lib/api/schema";
import { color, number } from "@/lib/format";
type Slot = components["schemas"]["SlotDto"];
export function HalfHourChart({
  slots,
  selected,
  date,
}: {
  slots: Slot[];
  selected: string;
  date: string;
}) {
  const router = useRouter();
  const max = Math.max(6, ...slots.map((s) => s.kwh ?? 0));
  return (
    <div className="scroll">
      <div className="chart">
        <div className="plot">
          {[1, 2, 3].map((n) => (
            <div key={n}>
              <div className="gridline" style={{ bottom: (n / 3) * 230 }} />
              <div className="ylab" style={{ bottom: (n / 3) * 230 + 2 }}>
                {number((max * n) / 3, 0)} kWh
              </div>
            </div>
          ))}
          <div className="bars">
            {slots.map((slot) => (
              <button
                key={slot.startUtc}
                aria-pressed={slot.startUtc === selected}
                aria-label={`${slot.startLocal} ${slot.localOffset}, ${slot.kwh == null ? "missing reading" : number(slot.kwh) + " kWh"}, ${number(slot.costPence, 1)}p`}
                title={`${slot.startLocal} ${slot.localOffset}`}
                onClick={() =>
                  router.push(
                    `/?date=${date}&slot=${slot.startLocal}&slotUtc=${encodeURIComponent(slot.startUtc)}`,
                    { scroll: false },
                  )
                }
                style={{
                  height:
                    slot.kwh == null ? 8 : Math.max(3, (slot.kwh / max) * 230),
                  background:
                    slot.kwh == null
                      ? "repeating-linear-gradient(45deg,var(--line-3),var(--line-3) 2px,transparent 2px,transparent 4px)"
                      : color(slot.band, slot.overLimit),
                }}
              />
            ))}
          </div>
        </div>
        <div className="ribbon">
          {slots.map((slot) => (
            <div
              key={slot.startUtc}
              style={{ background: color(slot.band, true) }}
            />
          ))}
        </div>
        <div className="xlab">
          {[
            "00:00",
            "03:00",
            "06:00",
            "09:00",
            "12:00",
            "15:00",
            "18:00",
            "21:00",
            "24:00",
          ].map((t) => (
            <span key={t}>{t}</span>
          ))}
        </div>
      </div>
    </div>
  );
}
