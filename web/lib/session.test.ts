import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { signSession, validSession } from "./session";
describe("signed sessions", () => {
  beforeEach(() => {
    vi.stubEnv(
      "SESSION_SECRET",
      "test-only-session-key-more-than-32-characters",
    );
    vi.stubEnv("APP_PASSWORD_HASH", "test-bcrypt-hash-not-a-real-credential");
  });
  afterEach(() => vi.unstubAllEnvs());
  it("accepts a valid signed token and rejects a changed signature", async () => {
    const token = await signSession();
    expect(await validSession(token)).toBe(true);
    const parts = token.split(".");
    parts[2] = "x" + parts[2].slice(1);
    expect(await validSession(parts.join("."))).toBe(false);
  });
  it("rejects missing sessions and revokes them when the password changes", async () => {
    const token = await signSession();
    expect(await validSession(undefined)).toBe(false);
    vi.stubEnv("APP_PASSWORD_HASH", "changed-password-hash");
    expect(await validSession(token)).toBe(false);
  });
  it("does not expose the password hash in its readable payload", async () => {
    const token = await signSession();
    const payload = JSON.parse(
      Buffer.from(token.split(".")[1], "base64url").toString(),
    );
    expect(payload.version).not.toBe(process.env.APP_PASSWORD_HASH);
    expect(payload.version).toHaveLength(64);
  });
});
