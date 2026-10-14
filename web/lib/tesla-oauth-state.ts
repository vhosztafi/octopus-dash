import { SignJWT, jwtVerify } from "jose";

export const teslaStateCookie = "__Host-energy-tesla-oauth";
export const teslaCallback = "https://octopus-dash.vilmos.co.uk/auth/tesla/callback";
async function sessionHash(session: string): Promise<string> {
  const bytes = await crypto.subtle.digest("SHA-256", new TextEncoder().encode(session));
  return Array.from(new Uint8Array(bytes), (b) => b.toString(16).padStart(2, "0")).join("");
}
function key() {
  const value = process.env.SESSION_SECRET;
  if (!value || value.length < 32) throw new Error("Session signing key is not configured");
  return new TextEncoder().encode(value);
}
export async function signTeslaState(state: string, session: string) {
  if (!/^[a-f0-9]{64}$/.test(state) || !session) throw new Error("Invalid authorization state");
  return new SignJWT({ state, session: await sessionHash(session) })
    .setProtectedHeader({ alg: "HS256" }).setIssuedAt().setExpirationTime("15m")
    .setIssuer("home-energy-tesla").setAudience("home-energy-web").sign(key());
}
export async function validTeslaState(cookie: string | undefined, state: string | null, session: string | undefined) {
  if (!cookie || !state || !session || !/^[a-f0-9]{64}$/.test(state)) return false;
  try {
    const { payload } = await jwtVerify(cookie, key(), {
      algorithms: ["HS256"], issuer: "home-energy-tesla", audience: "home-energy-web",
    });
    return payload.state === state && payload.session === await sessionHash(session);
  } catch { return false; }
}
