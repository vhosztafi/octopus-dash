import { NextRequest } from "next/server";
import { requireSession } from "@/lib/auth";
import { safeDate } from "@/lib/format";
export async function GET(request: NextRequest) {
  await requireSession();
  const date = request.nextUrl.searchParams.get("date");
  const filter = request.nextUrl.searchParams.get("filter") ?? "all";
  if (
    !date ||
    safeDate(date, "") !== date ||
    !["all", "peak", "off", "disp", "over"].includes(filter)
  )
    return Response.json({ error: "Invalid date or filter" }, { status: 400 });
  const response = await fetch(
    `${process.env.API_BASE_URL || "http://api:8080"}/api/days/${date}/slots.csv?filter=${filter}`,
    {
      headers: { Authorization: `Bearer ${process.env.INTERNAL_API_TOKEN}` },
      cache: "no-store",
      signal: AbortSignal.timeout(30000),
    },
  );
  if (!response.ok)
    return Response.json(
      { error: "Export unavailable" },
      { status: response.status },
    );
  return new Response(response.body, {
    headers: {
      "Content-Type": "text/csv; charset=utf-8",
      "Content-Disposition": `attachment; filename="half-hours-${date}.csv"`,
      "Cache-Control": "no-store",
    },
  });
}
