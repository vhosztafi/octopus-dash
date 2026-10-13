---
id: 261013-tnd
status: incomplete
---

# Cloudflare production login and API connection

The owner explicitly selected the existing localhost password for production. Uploaded its bcrypt hash as APP_PASSWORD_HASH and a separately generated, stable production SESSION_SECRET through authenticated Wrangler stdin. Values remain in ignored owner-private files and were never printed, committed or included in command arguments.

Wrangler confirmed both secrets deployed; secret listing exposes names only. Browser sign-in with the selected password succeeded and redirected to the protected dashboard. The backend remains unavailable because production INTERNAL_API_TOKEN is not yet configured in the Worker.

Prepared and synthetically tested a temporary RSA4096 / OAEP-SHA256 transfer helper: the production server would encrypt its existing API bearer with a public key, return ciphertext through TeamCity, and the ignored local private key would decrypt it for Wrangler stdin upload. PowerShell 5.1 and Node interoperability passed with synthetic input; AST parsing and diff checks passed. No production token was read or transferred.

Automatic approval review rejected committing/pushing this helper because the specific transfer of the production API bearer through TeamCity to Cloudflare had not been explicitly authorized. A concise approval question is pending. The helper remains uncommitted and must not be executed until approval; remove it and the ephemeral key after use or if declined.

Production supplier setup and the Quartz Windows service remain independent follow-up work. The local supplier credentials/data and backend SQL/Data Protection secrets were not transferred.
