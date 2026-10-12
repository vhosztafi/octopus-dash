import { describe, expect, it } from "vitest";
import { gbp, mondayFor, safeDate, shiftDate } from "./format";
describe("presentation dates and units", () => {
  it("formats pence only at display", () => {
    expect(gbp(123.456)).toBe("£1.23");
    expect(gbp(null)).toBe("—");
  });
  it("steps dates across both DST transitions", () => {
    expect(shiftDate("2026-10-25", 1)).toBe("2026-10-26");
    expect(shiftDate("2026-03-29", -1)).toBe("2026-03-28");
  });
  it("resolves Sunday to the prior Monday", () => {
    expect(mondayFor("2026-10-11")).toBe("2026-10-05");
  });
  it("rejects impossible dates", () => {
    expect(safeDate("2026-02-30", "fallback")).toBe("fallback");
    expect(safeDate("invalid", "fallback")).toBe("fallback");
  });
});
