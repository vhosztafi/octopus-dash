---
id: 261013-tnd
status: in-progress
description: Configure Cloudflare production login and API connection
---

GSD quick workflow, executed inline with existing autonomous authorization.

Resumed 2026-10-14: the owner explicitly approved transferring the existing production API bearer from IIS through a one-time RSA encrypted envelope in TeamCity to Cloudflare Worker octopus-dash. The initial approval-review rejection is resolved by this specific authorization. Keep the transfer temporary and remove the step/helpers/private key after verified upload.

1. Reuse the localhost bcrypt password hash as explicitly selected by the user. Generate an independent production session signing secret. Configure runtime secrets through the existing authenticated Wrangler CLI using stdin; never print values or commit them.
2. Connect the Worker to the deployed API using its existing production bearer token if secure access is available. Keep supplier credentials, SQL settings and Data Protection material on the backend. Never substitute the unrelated local API token or disclose the production token in logs/artifacts.
   Use a temporary, one-time RSA-OAEP-SHA256 envelope if needed: only the public key goes to the agent; its ciphertext is safe to return through TeamCity. The private key stays in the owner's ignored, ACL-protected secrets directory. Decrypt locally and feed Wrangler via stdin, then remove the transfer step/helpers and ephemeral private key.
3. Verify production login, session protection and backend access; record completed work or concrete connection blockers in SUMMARY.md, STATE.md and PROGRESS.md.
