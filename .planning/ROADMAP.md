# Home Energy roadmap

Each requirement maps to exactly one phase in REQUIREMENTS.md. All phases are autonomous MVP slices, with tests and PROGRESS.md evidence.

1. **Scaffold and sample dashboard** — UI-01, API-01, DEP-01. Four navigable responsive screens use the API contract; Docker structure exists.
2. **SQL model and Octopus ingest** — DB-01, ING-01..03. Migration/schema and repeatable ingestion; sync queue and schedules.
3. **Exact pricing** — PRC-01..03, TST-01. DST/dispatch/rate cases pass; real queries, ledger and export.
4. **Dispatch persistence** — DSP-01..03. Schema checked at connection, snapshots retained, allowance isolated.
5. **Powerwall integration** — BAT-01..02. Token rotation, site picker, history and live integration.
6. **Weekly and gas** — WK-01, GAS-01. Real aggregates with coverage and previous-week comparison.
7. **Alerts and reports** — ALT-01, RPT-01, NTF-01. Dedupe/cooldown and scheduled delivery with shared weekly data.
8. **Hardening and verification** — SEC-01..03, TST-02. Secure deployment, CI/tests and honest commissioning checklist.

Status: Phase 1 in progress. All later phases planned.
