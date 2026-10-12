import "server-only";
import { requireSession } from "../auth";
import type { components } from "./schema";

export type Day = components["schemas"]["DayDto"];
export type Week = components["schemas"]["WeekDto"];
export type Slot = components["schemas"]["SlotDto"];
export type Settings = components["schemas"]["DashboardSettings"];
export type Ledger = components["schemas"]["LedgerDto"];
export type Connections = components["schemas"]["ConnectionsDto"];
export type Status = components["schemas"]["StatusDto"];
export type Powerwall = components["schemas"]["PowerwallDto"];
export type Site = components["schemas"]["EnergySiteDto"];
export type ConnectionTest = components["schemas"]["ConnectionTestDto"];
export class ApiError extends Error {
  constructor(
    public status: number,
    message: string,
    public errors: Record<string, string[]> = {},
  ) {
    super(message);
  }
}
export async function api<T>(path: string, init?: RequestInit): Promise<T> {
  await requireSession();
  const token = process.env.INTERNAL_API_TOKEN;
  if (!token || token.length < 32)
    throw new Error("INTERNAL_API_TOKEN is not configured");
  const response = await fetch(
    `${process.env.API_BASE_URL || "http://api:8080"}/api${path}`,
    {
      ...init,
      cache: "no-store",
      signal: AbortSignal.timeout(30000),
      headers: {
        ...init?.headers,
        "Content-Type": "application/json",
        Authorization: `Bearer ${token}`,
      },
    },
  );
  if (!response.ok) {
    const problem = (await response.json().catch(() => ({}))) as {
      title?: string;
      errors?: Record<string, string[]>;
    };
    throw new ApiError(
      response.status,
      problem.title || "Energy API is unavailable",
      problem.errors,
    );
  }
  return (await response.json()) as T;
}
export const getDay = (date: string) => api<Day>(`/days/${date}`);
export const getWeek = (week: string) => api<Week>(`/weeks/${week}`);
export const getLedger = (date: string, filter: string) =>
  api<Ledger>(`/days/${date}/slots?filter=${filter}`);
export const getSettings = () => api<Settings>("/settings");
export const getStatus = () => api<Status>("/status");
export const getConnections = () => api<Connections>("/connections");
export const getSites = () => api<Site[]>("/tesla/sites");
export const latestDay = () =>
  api<components["schemas"]["LatestDayDto"]>("/days/latest-complete");
