import { SignJWT, jwtVerify } from "jose";

export const sessionCookie = "energy-session";
async function hashVersion(): Promise<string> {
  const digest = await crypto.subtle.digest(
    "SHA-256",
    new TextEncoder().encode(process.env.APP_PASSWORD_HASH ?? ""),
  );
  return Array.from(new Uint8Array(digest), (value) =>
    value.toString(16).padStart(2, "0"),
  ).join("");
}
function key(): Uint8Array {
  const secret = process.env.SESSION_SECRET;
  if (!secret || secret.length < 32)
    throw new Error("SESSION_SECRET must be at least 32 characters");
  return new TextEncoder().encode(secret);
}
export async function signSession(): Promise<string> {
  return new SignJWT({ version: await hashVersion() })
    .setProtectedHeader({ alg: "HS256" })
    .setIssuedAt()
    .setExpirationTime("8h")
    .setIssuer("home-energy")
    .setAudience("home-energy-web")
    .sign(key());
}
export async function validSession(
  token: string | undefined,
): Promise<boolean> {
  if (!token || !process.env.APP_PASSWORD_HASH) return false;
  try {
    const { payload } = await jwtVerify(token, key(), {
      algorithms: ["HS256"],
      issuer: "home-energy",
      audience: "home-energy-web",
    });
    return payload.version === (await hashVersion());
  } catch {
    return false;
  }
}
