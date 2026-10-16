---
id: 261016-rna
mode: quick
description: Make the app a PWA, commit and deploy
---

# PWA support

1. Add the Next.js web manifest, standalone/Apple metadata and branded PNG icons. Expose only exact public PWA assets through authentication middleware.
2. Register a production service worker with a versioned, public offline page. Cache only that page; leave household data, BFF requests, OAuth and mutations on the network. Set service-worker update/security headers for Next.js and Cloudflare static assets. Add meaningful worker behavior tests and extend the workerd smoke checks.
3. Run TypeScript, frontend tests, production OpenNext build/workerd smoke and real-browser installability/offline/authentication checks. Commit the implementation, push master to deploy through Cloudflare Builds, verify the public deployment, and record evidence in SUMMARY.md, STATE.md and PROGRESS.md.

Execution is inline under the skill's Codex spawn restriction; no subagents or worktrees.
