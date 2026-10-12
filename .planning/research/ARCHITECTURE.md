# Architecture
Authenticated browser → Next.js Server Components/Actions/CSV route → bearer-authenticated ASP.NET API → SQL Server. Quartz worker shares Core and Infrastructure, including encrypted Data Protection key ring. Heavy source/pricing commands are serialized through one pipeline JobKey; live telemetry and report scheduling have independent non-overlapping JobKeys. OAuth rotation and weekly send concurrency are locked through SQL application locks.

Core has no HTTP/EF/framework dependencies. EF configurations preserve UTC datetime2(0), decimal(18,6), natural-key indexes. Materialization replaces both fuels inside one transaction per London day. SQL command queue is single-worker. Explicit fixture mode implements the same dashboard contract without contacting suppliers.
