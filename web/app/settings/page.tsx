import { SettingsForm } from "@/components/SettingsForm";
import { getSettings, getConnections, getSites } from "@/lib/api/client";
export default async function SettingsPage({
  searchParams,
}: {
  searchParams: Promise<{ notice?: string }>;
}) {
  const [settings, connections, query] = await Promise.all([
    getSettings(),
    getConnections(),
    searchParams,
  ]);
  let sites: Awaited<ReturnType<typeof getSites>> = [];
  let siteNotice = "";
  if (connections.tesla.configured) {
    try {
      sites = await getSites();
    } catch {
      siteNotice =
        "Tesla site discovery failed. Check the connection and retry.";
    }
  }
  return (
    <SettingsForm
      settings={settings}
      connections={connections}
      sites={sites}
      notice={query.notice || siteNotice}
    />
  );
}
