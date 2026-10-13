import { defineCloudflareConfig } from "@opennextjs/cloudflare";

// Dashboard data is fetched with no-store; no shared ISR cache is needed.
export default defineCloudflareConfig();
