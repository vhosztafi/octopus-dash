"use server";
import { cookies } from "next/headers";
import { redirect } from "next/navigation";
import { requireSession } from "@/lib/auth";
import { api } from "@/lib/api/client";
import type { components } from "@/lib/api/schema";
import { sessionCookie } from "@/lib/session";
import { signTeslaState, teslaStateCookie } from "@/lib/tesla-oauth-state";

export async function authorizeTesla(_: string, form: FormData): Promise<string> {
  await requireSession();
  const clientId = String(form.get("clientId") ?? "").trim();
  const clientSecret = String(form.get("clientSecret") ?? "").trim();
  if (!clientId || clientId.length > 500 || !clientSecret || clientSecret.length > 2000)
    return "Enter the Client ID and Client secret from your Tesla developer application.";
  let destination: string;
  try {
    const credentials: components["schemas"]["TeslaAppCredentials"] = { clientId, clientSecret };
    const result = await api<components["schemas"]["TeslaAuthorizationStart"]>("/connections/tesla/authorize", {
      method: "POST", body: JSON.stringify(credentials),
    });
    const url = new URL(result.authorizationUrl);
    if (url.origin !== "https://auth.tesla.com" || url.pathname !== "/oauth2/v3/authorize")
      throw new Error("Invalid authorization destination");
    const jar = await cookies();
    const session = jar.get(sessionCookie)?.value;
    if (!session) throw new Error("Session expired");
    jar.set(teslaStateCookie, await signTeslaState(result.state, session), {
      httpOnly: true, secure: true, sameSite: "lax", path: "/", maxAge: 900,
    });
    destination = url.toString();
  } catch {
    return "Tesla setup could not start. Check your app credentials, allowed origin and callback URL, then try again.";
  }
  redirect(destination);
}
