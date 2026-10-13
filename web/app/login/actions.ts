"use server";
import bcrypt from "bcryptjs";
import { cookies } from "next/headers";
import { redirect } from "next/navigation";
import { sessionCookie, signSession } from "@/lib/session";
import { allowLoginAttempt, resetLocalLoginAttempts } from "@/lib/login-rate-limit";
export async function login(_: string, form: FormData): Promise<string> {
  if (!(await allowLoginAttempt()))
    return "Too many attempts. Wait a few minutes and try again.";
  const hash = process.env.APP_PASSWORD_HASH;
  const password = String(form.get("password") ?? "");
  if (!hash || password.length > 256 || !(await bcrypt.compare(password, hash)))
    return "Incorrect password.";
  resetLocalLoginAttempts();
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
