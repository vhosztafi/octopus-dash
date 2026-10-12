"use server";
import { redirect } from "next/navigation";
import { revalidatePath } from "next/cache";
import { api, ApiError, type ConnectionTest } from "@/lib/api/client";
import { requireSession } from "@/lib/auth";
export async function syncNow() {
  await requireSession();
  let notice = "Sync queued. The worker will refresh readings and prices.";
  try {
    await api("/sync", { method: "POST" });
  } catch (error) {
    notice = error instanceof ApiError ? error.message : "Sync unavailable";
  }
  redirect(`/settings?notice=${encodeURIComponent(notice)}`);
}
export async function sendReport(form: FormData) {
  await requireSession();
  const week = String(form.get("week"));
  let notice = "Report queued for delivery.";
  try {
    await api(`/reports/weekly/send?week=${encodeURIComponent(week)}`, {
      method: "POST",
    });
  } catch (error) {
    notice = error instanceof ApiError ? error.message : "Report unavailable";
  }
  redirect(`/weekly?week=${week}&notice=${encodeURIComponent(notice)}`);
}
export async function testConnection(form: FormData) {
  await requireSession();
  const provider = String(form.get("provider"));
  if (!["octopus", "tesla"].includes(provider)) return;
  let notice = "Connection verified.";
  try {
    const response = await api<ConnectionTest>(
      `/connections/${provider}/test`,
      { method: "POST" },
    );
    notice = response.message;
  } catch (error) {
    notice = error instanceof ApiError ? error.message : "Connection failed";
  }
  revalidatePath("/settings");
  redirect(`/settings?notice=${encodeURIComponent(notice)}`);
}
