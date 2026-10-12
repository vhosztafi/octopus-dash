# Pitfalls
- DST: derive day bounds with NodaTime, identity by UTC; repeated 01:00 labels require offset/UTC URL.
- Missing data: nullable readings/rates and partial totals, not zeros; completeness includes both fuels and standing.
- Dispatch: partial overlap affects a whole billed slot; planned windows do not prove active charging. Cap stays opt-in with TODO(verify).
- Gas: retain raw units; recalculation uses current calorific value without overwriting original measurements.
- Tesla: Wh versus W, reject coarser-than-half-hour history, split live trapezoids at boundaries and flag gaps.
- Secrets: never return raw values; lock rotating refresh tokens across processes; guard pagination hosts; redact error payloads.
- Scheduling: heavy historical work must not block 60-second live polling. Dedupe alerts before sending; lock scheduled/manual weekly sends.
- Evidence: do not label synthetic fixtures as real recordings or skipped Docker tests as passed.
