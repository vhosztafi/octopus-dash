import { afterEach, describe, expect, it, vi } from "vitest";

vi.mock("server-only", () => ({}));
const { limit } = vi.hoisted(() => ({ limit: vi.fn() }));
vi.mock("@opennextjs/cloudflare", () => ({
  getCloudflareContext: () => ({ env: { LOGIN_RATE_LIMITER: { limit } } }),
}));
import { allowLoginAttempt, resetLocalLoginAttempts } from "./login-rate-limit";

afterEach(() => { vi.unstubAllEnvs(); vi.restoreAllMocks(); resetLocalLoginAttempts(); });
describe("login attempt limits", () => {
  it("uses Cloudflare counters and denies exhausted or unavailable limits", async () => {
    vi.stubEnv("DEPLOYMENT_TARGET", "cloudflare");
    limit.mockResolvedValueOnce({ success: true }).mockResolvedValueOnce({ success: false }).mockRejectedValueOnce(new Error("unavailable"));
    expect(await allowLoginAttempt()).toBe(true);
    expect(limit).toHaveBeenCalledWith({ key: "octopus-dash:owner-login" });
    expect(await allowLoginAttempt()).toBe(false);
    expect(await allowLoginAttempt()).toBe(false);
  });
  it("keeps Docker's ten-attempt window and resets after a successful login", async () => {
    vi.stubEnv("DEPLOYMENT_TARGET", "docker");
    for (let i = 0; i < 10; i++) expect(await allowLoginAttempt()).toBe(true);
    expect(await allowLoginAttempt()).toBe(false);
    resetLocalLoginAttempts();
    expect(await allowLoginAttempt()).toBe(true);
  });
});
