import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { signTeslaState, validTeslaState } from "./tesla-oauth-state";

describe("Tesla authorization state", () => {
  const state = "a".repeat(64);
  beforeEach(() => vi.stubEnv("SESSION_SECRET", "synthetic-tesla-state-signing-key-more-than-32-characters"));
  afterEach(() => { vi.unstubAllEnvs(); vi.useRealTimers(); });
  it("binds consent to the originating dashboard session and state", async () => {
    const cookie = await signTeslaState(state, "session-one");
    expect(await validTeslaState(cookie, state, "session-one")).toBe(true);
    expect(await validTeslaState(cookie, state, "session-two")).toBe(false);
    expect(await validTeslaState(cookie, "b".repeat(64), "session-one")).toBe(false);
    expect(await validTeslaState(undefined, state, "session-one")).toBe(false);
    expect(await validTeslaState(cookie, null, "session-one")).toBe(false);
  });
  it("rejects tampering and expires after fifteen minutes", async () => {
    vi.useFakeTimers();
    const cookie = await signTeslaState(state, "session-one");
    const parts = cookie.split(".");
    const signature = Buffer.from(parts[2], "base64url"); signature[0] ^= 1;
    parts[2] = signature.toString("base64url");
    expect(await validTeslaState(parts.join("."), state, "session-one")).toBe(false);
    vi.setSystemTime(Date.now() + 901_000);
    expect(await validTeslaState(cookie, state, "session-one")).toBe(false);
  });
});
