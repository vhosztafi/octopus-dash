import { cookies } from "next/headers";
import { NextRequest, NextResponse } from "next/server";
import { api } from "@/lib/api/client";
import { requireSession } from "@/lib/auth";
import { sessionCookie } from "@/lib/session";
import { teslaCallback, teslaStateCookie, validTeslaState } from "@/lib/tesla-oauth-state";

export async function GET(request: NextRequest) {
  await requireSession();
  const jar = await cookies();
  const state = request.nextUrl.searchParams.get("state");
  const valid = await validTeslaState(jar.get(teslaStateCookie)?.value, state, jar.get(sessionCookie)?.value);
  jar.set(teslaStateCookie, "", { httpOnly: true, secure: true, sameSite: "lax", path: "/", maxAge: 0 });
  let notice = "Tesla authorization expired or did not match this session. Start again from Settings.";
  if (valid) {
    const error = request.nextUrl.searchParams.has("error");
    const code = request.nextUrl.searchParams.get("code");
    if (code || error) {
      try {
        await api("/connections/tesla/callback", { method: "POST", body: JSON.stringify({ state, code: error ? "" : code }) });
        notice = error ? "Tesla authorization was cancelled. Existing credentials were kept." : "Tesla authorized. Test the connection, select your energy site and save changes.";
      } catch {
        notice = "Tesla authorization could not finish. Start again from Settings; existing credentials were kept.";
      }
    }
  }
  const destination = new URL("/settings", teslaCallback);
  destination.searchParams.set("notice", notice);
  const response = NextResponse.redirect(destination, 303);
  response.headers.set("Cache-Control", "no-store");
  response.headers.set("Referrer-Policy", "no-referrer");
  return response;
}
