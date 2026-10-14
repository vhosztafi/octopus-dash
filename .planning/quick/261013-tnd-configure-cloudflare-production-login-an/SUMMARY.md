---
id: 261013-tnd
status: complete
---

# Cloudflare production login and API connection

The owner explicitly selected the existing localhost password for production. Uploaded its bcrypt hash as APP_PASSWORD_HASH and a separately generated, stable production SESSION_SECRET through authenticated Wrangler stdin. Values remain in ignored owner-private files and were never printed, committed or included in command arguments.

Wrangler confirmed both login secrets deployed; secret listing exposes names only. Browser sign-in with the selected password succeeded. On 2026-10-14, the owner explicitly approved the encrypted production API-token transfer. Uploaded INTERNAL_API_TOKEN through Wrangler stdin after local decryption, verified authenticated API status 200, and confirmed all three production runtime secret names. Fresh browser login rendered the production Day dashboard and protected Settings successfully.

The temporary RSA4096 / OAEP-SHA256 transfer helper returned only ciphertext through TeamCity; the local private key decrypted it for Wrangler stdin upload. PowerShell 5.1 and Node interoperability passed with synthetic input before execution. CI #8 (7429) and deployment #7 (7431) passed for revision 4374b34. Transfer removal commit 7a86097 deletes the TeamCity step and both public helper files. The exact ignored private-key and ciphertext files were deleted after successful upload. No plaintext bearer was logged, printed or committed.

The initial automatic approval-review rejection was resolved by the owner's explicit approval on 2026-10-14. No rejected action was retried before that approval.

Cleanup revision 7a86097 also passed CI #9 (7434) and automatic deployment #8 (7436), confirming the normal pipeline is restored and existing production configuration survives redeployment.

CI #7 (7428) failed because NuGet added October 13 advisories for PKCS and XML cryptography dependencies. Upgraded DataProtection.EntityFrameworkCore and the PKCS override to 10.0.13; the former resolves patched Xml without a redundant prunable reference. Self-contained Windows publish explicitly uses .NET/ASP.NET Core 10.0.13. All 52 local backend tests passed including real SQL/key-ring sharing, the Windows package published, and the transitive vulnerability audit found no vulnerable packages. Audit warnings remain fatal. Patch commit: 4374b34.

Unauthenticated production web routes redirect to /login; the public API remains bearer protected. Browser evidence is in ignored output/cloudflare/production-api-connected.png. Production connections report Not connected, with 96 missing/unpriced slots for the selected empty day; no supplier credentials or local database contents were copied.

Production supplier setup and the Quartz Windows service remain independent follow-up work. The local supplier credentials/data and backend SQL/Data Protection secrets were not transferred.
