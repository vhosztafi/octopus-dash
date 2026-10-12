import "server-only";
import { cookies } from "next/headers";
import { redirect } from "next/navigation";
import { sessionCookie, validSession } from "./session";

export async function requireSession(): Promise<void> {
  if (!(await validSession((await cookies()).get(sessionCookie)?.value)))
    redirect("/login");
}
