<!-- GSD:project-start source:PROJECT.md -->

## Project

**Home Energy Dashboard**

A single-user, self-hosted home energy dashboard for an Octopus smart-meter account and Tesla Powerwall. Four screens reproduce docs/prototype/home-energy-prototype.html while all production prices come from dated supplier agreements and VAT-inclusive rates.

**Core Value:** Price every London half-hour accurately, preserving missing data and daylight-saving transitions.

### Constraints

- .NET 10, SQL Server/EF Core, Quartz, Next.js App Router, TypeScript strict, Tailwind v4.
- Pure Core pricing using decimal and NodaTime. UTC storage; London grouping. Missing readings never become zero.
- Next.js BFF is the only published service; signed secure session and internal bearer authentication.
- Shared encrypted Data Protection key ring with certificate protection.
- Match prototype layout, tokens, fonts, interactions and responsive behavior.

<!-- GSD:project-end -->

<!-- GSD:stack-start source:codebase/STACK.md -->

## Technology Stack

# Stack

.NET 10.0.12 runtime/EF, NodaTime 3.2.2, SQL Server 2025 Express x64, Quartz 3.18.2, Next.js 16.4.0/React 19.3.0, Tailwind 4.3.3, TypeScript 5.9 (compatible with openapi-typescript 7.13). All resolved web packages locked in package-lock.json. OpenAPI 2.12 and SSH.NET 2026 override vulnerable transitive versions. xUnit and Testcontainers for backend; vitest for frontend logic/security.
<!-- GSD:stack-end -->

<!-- GSD:conventions-start source:CONVENTIONS.md -->

## Conventions

# Conventions: pure decimal domain math; UTC database timestamps; NodaTime London grouping; explicit EF entity configurations; generated web DTO aliases; Server Components and server-only BFF; labelled sample mode. Execute plans autonomously and record external verification gaps in PROGRESS.md.

<!-- GSD:conventions-end -->

<!-- GSD:architecture-start source:ARCHITECTURE.md -->

## Architecture

# Architecture

Authenticated browser → Next.js Server Components/Actions/CSV route → bearer-authenticated ASP.NET API → SQL Server. Quartz worker shares Core and Infrastructure, including encrypted Data Protection key ring. Heavy source/pricing commands are serialized through one pipeline JobKey; live telemetry and report scheduling have independent non-overlapping JobKeys. OAuth rotation and weekly send concurrency are locked through SQL application locks.

Core has no HTTP/EF/framework dependencies. EF configurations preserve UTC datetime2(0), decimal(18,6), natural-key indexes. Materialization replaces both fuels inside one transaction per London day. SQL command queue is single-worker. Explicit fixture mode implements the same dashboard contract without contacting suppliers.
<!-- GSD:architecture-end -->

<!-- GSD:skills-start source:skills/ -->

## Project Skills

No project skills found. Add skills to any of: `.claude/skills/`, `.agents/skills/`, `.cursor/skills/`, `.github/skills/`, or `.codex/skills/` with a `SKILL.md` index file.
<!-- GSD:skills-end -->

<!-- GSD:workflow-start source:GSD defaults -->

## GSD Workflow Enforcement

Before using Edit, Write, or other file-changing tools, start work through a GSD command so planning artifacts and execution context stay in sync.

Use these entry points:

- `$gsd-quick` for small fixes, doc updates, and ad-hoc tasks
- `$gsd-debug` for investigation and bug fixing
- `$gsd-execute-phase` for planned phase work

Do not make direct repo edits outside a GSD workflow unless the user explicitly asks to bypass it.
<!-- GSD:workflow-end -->

<!-- GSD:profile-start -->

## Developer Profile

> Profile not yet configured. Run `/gsd-profile-user` to generate your developer profile.
> This section is managed by `generate-claude-profile` -- do not edit manually.
<!-- GSD:profile-end -->
