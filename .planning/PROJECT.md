# Home Energy Dashboard

## What This Is
A single-user, self-hosted home energy dashboard for an Octopus smart-meter account and Tesla Powerwall. Four screens reproduce docs/prototype/home-energy-prototype.html while all production prices come from dated supplier agreements and VAT-inclusive rates.

## Core Value
Price every London half-hour accurately, preserving missing data and daylight-saving transitions.

## Requirements
### Validated
None yet; automated verification and live commissioning are tracked separately.
### Active
All requirements in REQUIREMENTS.md and the eight milestones in the implementation brief.
### Out of Scope
Multi-user accounts, meter control, Home Mini implementation, local Tesla gateway and web push; extension interfaces are retained.

## Context
Greenfield repository with an HTML prototype and implementation brief. User authorizes autonomous planning and implementation, with blockers logged for follow-up. No private supplier credentials are supplied. Docker CLI is installed but its Linux daemon is unavailable at initialization.

## Constraints
- .NET 10, SQL Server/EF Core, Quartz, Next.js App Router, TypeScript strict, Tailwind v4.
- Pure Core pricing using decimal and NodaTime. UTC storage; London grouping. Missing readings never become zero.
- Next.js BFF is the only published service; signed secure session and internal bearer authentication.
- Shared encrypted Data Protection key ring with certificate protection.
- Match prototype layout, tokens, fonts, interactions and responsive behavior.

## Key Decisions
| Decision | Rationale | Outcome |
|---|---|---|
| Sequential autonomous execution | User requested no human validation stops | Accepted |
| DB command queue | API and worker run independently; no public worker control port | Planned |
| Explicit sample mode | Demonstrate real API shapes without inventing production prices | Planned |
| Allowance opt-in and isolated | Active charging versus planned dispatch cannot be inferred reliably | Requires live verification |

## Evolution
Update requirements, decisions, roadmap and state after each phase. Record evidence in PROGRESS.md; do not mark unrun checks passed.

Last updated: 2026-10-12 after initialization.
