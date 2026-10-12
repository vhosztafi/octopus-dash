"use server";
import { revalidatePath } from "next/cache";
import {
  api,
  ApiError,
  getSettings,
  getStatus,
  type Settings,
  type ConnectionTest,
} from "@/lib/api/client";
import { requireSession } from "@/lib/auth";
export type FormState = {
  message: string;
  errors: Record<string, string[]>;
  ok: boolean;
};
const result = (error: unknown): FormState => ({
  message:
    error instanceof ApiError
      ? error.message
      : "Unable to save. Check the API service.",
  errors: error instanceof ApiError ? error.errors : {},
  ok: false,
});
export async function saveSettings(
  _: FormState,
  form: FormData,
): Promise<FormState> {
  await requireSession();
  try {
    const [original, status] = await Promise.all([getSettings(), getStatus()]);
    const value = { ...original };
    const booleans = [
      "priceDispatchAtOffPeak",
      "dispatchAllowanceEnabled",
      "countScheduledWindowDispatches",
      "peakAlertEnabled",
      "ignoreDispatch",
      "earlyWarning",
      "renotifyReconciliation",
      "gasAlertEnabled",
      "notifyPush",
      "notifyEmail",
      "weeklyEnabled",
      "includeCosts",
      "includeAlerts",
      "includeGas",
      "includeBattery",
      "attachCsv",
    ] as const;
    const numbers = [
      "dispatchAllowanceSlots",
      "calorificValue",
      "peakLimitKwh",
      "gasDailyLimitPounds",
      "cooldownMinutes",
      "historyDays",
    ] as const;
    const strings = [
      "nightStart",
      "nightEnd",
      "gasUnit",
      "peakSource",
      "weekDay",
      "weekTime",
      "recipients",
      "teslaSiteId",
      "smartFlexDeviceId",
    ] as const;
    for (const key of booleans) value[key] = form.has(key);
    for (const key of numbers) {
      const text = String(form.get(key) ?? "");
      const parsed = Number(text);
      if (!text.trim() || !Number.isFinite(parsed))
        return {
          message: "Check the highlighted fields.",
          errors: { [key]: ["Enter a valid number."] },
          ok: false,
        };
      value[key] = parsed;
    }
    for (const key of strings) value[key] = String(form.get(key) ?? "").trim();
    await api<Settings>("/settings", {
      method: "PUT",
      body: JSON.stringify(value),
    });
    for (const path of ["/", "/half-hours", "/weekly", "/settings"])
      revalidatePath(path);
    return {
      message: status.sample
        ? "Sample settings saved for this preview."
        : "Changes saved. Real data is queued for re-pricing.",
      errors: {},
      ok: true,
    };
  } catch (error) {
    return result(error);
  }
}
export async function saveConnection(
  _: FormState,
  form: FormData,
): Promise<FormState> {
  await requireSession();
  const provider = String(form.get("provider"));
  if (!["octopus", "tesla"].includes(provider))
    return { message: "Invalid provider", errors: {}, ok: false };
  try {
    const body =
      provider === "octopus"
        ? {
            apiKey: String(form.get("apiKey")),
            accountNumber: String(form.get("accountNumber")),
          }
        : {
            refreshToken: String(form.get("refreshToken")),
            clientId: String(form.get("clientId")),
            clientSecret: String(form.get("clientSecret") ?? ""),
          };
    const response = await api<ConnectionTest>(`/connections/${provider}`, {
      method: "PUT",
      body: JSON.stringify(body),
    });
    revalidatePath("/settings");
    return { message: response.message, errors: {}, ok: true };
  } catch (error) {
    return result(error);
  }
}
