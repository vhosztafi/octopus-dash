"use client";

import { useEffect } from "react";

export function ServiceWorkerRegistration() {
  useEffect(() => {
    if (process.env.NODE_ENV !== "production" || !("serviceWorker" in navigator)) return;

    void navigator.serviceWorker.register("/sw.js", {
      scope: "/",
      updateViaCache: "none",
    }).catch(() => {
      // Installation is progressive enhancement; the dashboard still works
      // when browser policy or connectivity prevents registration.
      console.warn("Home Energy offline support could not be registered.");
    });
  }, []);

  return null;
}
