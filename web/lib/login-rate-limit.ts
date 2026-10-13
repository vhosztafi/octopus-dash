import "server-only";
import { getCloudflareContext } from "@opennextjs/cloudflare";

const attempts = { at: 0, count: 0 };

export async function allowLoginAttempt(): Promise<boolean> {
  if (process.env.DEPLOYMENT_TARGET === "cloudflare") {
    // Cloudflare's counters are shared across isolates within each location.
    // A constant owner key also bounds distributed password guessing there.
    try {
      const { env } = getCloudflareContext();
      return (await env.LOGIN_RATE_LIMITER.limit({ key: "octopus-dash:owner-login" })).success;
    } catch {
      return false;
    }
  }
  // The Docker deployment remains a single web process.
  if (Date.now() - attempts.at > 15 * 60 * 1000) {
    attempts.at = Date.now();
    attempts.count = 0;
  }
  if (attempts.count >= 10) return false;
  attempts.count++;
  return true;
}

export function resetLocalLoginAttempts(): void {
  attempts.count = 0;
}
