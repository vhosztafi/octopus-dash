"use server";
import bcrypt from "bcryptjs";
import { cookies } from "next/headers";
import { redirect } from "next/navigation";
import { sessionCookie, signSession } from "@/lib/session";
// Single-user/single-web-process deployment. Bound attempts globally, avoiding proxy header spoofing.
const attempts: { at: number; count: number } = { at: 0, count: 0 };
export async function login(_: string, form: FormData): Promise<string> {
  const now = Date.now();
  if (now - attempts.at > 15 * 60 * 1000) {
    attempts.at = now;
    attempts.count = 0;
  }
  if (attempts.count >= 10)
    return "Too many attempts. Try again in 15 minutes.";
  attempts.count++;
  const hash = process.env.APP_PASSWORD_HASH;
  const password = String(form.get("password") ?? "");
  if (!hash || password.length > 256 || !(await bcrypt.compare(password, hash)))
    return "Incorrect password.";
  attempts.count = 0;
  (await cookies()).set(sessionCookie, await signSession(), {
    httpOnly: true,
    secure: true,
    sameSite: "lax",
    path: "/",
    maxAge: 8 * 60 * 60,
  });
  redirect("/");
}
export async function logout() {
  (await cookies()).delete(sessionCookie);
  redirect("/login");
}
