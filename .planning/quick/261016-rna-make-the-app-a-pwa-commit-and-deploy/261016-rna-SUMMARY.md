---
id: 261016-rna
status: complete
date: 2026-10-16
implementation_commit: 89654e1
---

# Installable Home Energy PWA

Added the App Router manifest, standalone launch metadata, theme colour, Apple touch icon and 192/512/maskable PNG icons, with a code-native SVG source. Production registers a root-scoped service worker. Only the public offline page is cached; household pages, RSC, prices, readings, API exports, OAuth and mutations are not cached by the worker. Network navigation preserves online authentication/server responses and falls back to a reconnect notice only when the network fails. Registration failures leave the dashboard usable. Existing sessions and server-only BFF authentication remain in place.

Added exact anonymous PWA asset paths, explicit worker MIME/update/CSP headers for Next.js and Cloudflare, and version-scoped offline cache cleanup. The workerd smoke test found Cloudflare's default HTML extension redirect; `assets.html_handling: none` keeps `/offline.html` consistent with Next.js/Docker. No new dependencies or backend changes.

## Verification

- Strict TypeScript and all 21 frontend tests passed on Windows and Linux, including ten service-worker privacy/lifecycle/failure tests.
- Complete `npm run ci:cloudflare` passed in the existing Node 24 Linux build image: Next/OpenNext production compilation and workerd smoke of protected routes, signed sessions, login, manifest/icons, worker headers, offline asset, Tesla callback security and unavailable-API handling.
- The Windows sandbox build could not download Google Fonts; the equivalent full Linux production build succeeded. There is no remaining build blocker.
- Implementation 89654e1 pushed to master. GitHub frontend/backend checks passed in run 38951823814. Cloudflare hosted build 6d9a6ef5-3249-4d5f-8a29-a4b846764a03 passed and deployed Worker version 01abfa20-fd5a-4aac-8e38-89aa11cb679c at 100% traffic.
- Live production Chrome with a fresh persistent test profile reports no manifest or installability errors, standalone launch configuration, active service-worker control, Apple icon metadata, and no console errors/warnings. The initial isolated incognito check reported only Chrome's expected incognito install restriction; a normal profile confirmed installability.
- Cache Storage contains exactly `home-energy-offline-v1`, with exactly `/offline.html`. Taking the browser offline and navigating to `/settings` displays the public fallback. At 390×844 the content and retry link fit; screenshot inspected at ignored `output/playwright/pwa-offline-mobile.png`. Restoring connectivity and choosing Try again returns through the normal login gate.
- Actual iOS home-screen installation was not exercised; Apple metadata/icon and installation instructions are supplied. No OS-level app was installed during verification.

Operations and install/update instructions are in docs/CLOUDFLARE.md. Quick planning/execution ran inline under the skill's Codex spawn restriction.
