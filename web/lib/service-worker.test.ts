import { readFileSync } from "node:fs";
import { runInNewContext } from "node:vm";
import { describe, expect, it, vi } from "vitest";

const source = readFileSync(new URL("../public/sw.js", import.meta.url), "utf8");

function worker() {
  const listeners: Record<string, (event: unknown) => void> = {};
  const fallback = new Response("public offline page");
  const cache = { add: vi.fn().mockResolvedValue(undefined), match: vi.fn().mockResolvedValue(fallback) };
  const caches = {
    open: vi.fn().mockResolvedValue(cache),
    keys: vi.fn().mockResolvedValue(["home-energy-offline-v0", "home-energy-offline-v1", "unrelated-cache"]),
    delete: vi.fn().mockResolvedValue(true),
  };
  const fetch = vi.fn().mockResolvedValue(new Response("live"));
  const self = {
    location: { origin: "https://energy.example" },
    addEventListener: (name: string, handler: (event: unknown) => void) => { listeners[name] = handler; },
    skipWaiting: vi.fn().mockResolvedValue(undefined),
    clients: { claim: vi.fn().mockResolvedValue(undefined) },
  };
  runInNewContext(source, { self, caches, fetch, URL, Request, Response });
  function request(path = "/", method = "GET", mode = "navigate") {
    const respondWith = vi.fn();
    const request = { url: new URL(path, self.location.origin).href, method, mode };
    listeners.fetch({ request, respondWith });
    return { request, respondWith };
  }
  return { listeners, cache, caches, fetch, self, fallback, request };
}

describe("PWA service worker privacy and offline behavior", () => {
  it("installs only the public offline page and activates immediately", async () => {
    const w = worker();
    const waitUntil = vi.fn();
    w.listeners.install({ waitUntil });
    await waitUntil.mock.calls[0][0];
    expect(w.cache.add).toHaveBeenCalledOnce();
    const request = w.cache.add.mock.calls[0][0] as Request;
    expect(request.url).toBe("https://energy.example/offline.html");
    expect(request.cache).toBe("reload");
    expect(w.self.skipWaiting).toHaveBeenCalledOnce();
  });

  it("removes only obsolete versions of its own cache", async () => {
    const w = worker();
    const waitUntil = vi.fn();
    w.listeners.activate({ waitUntil });
    await waitUntil.mock.calls[0][0];
    expect(w.caches.delete.mock.calls).toEqual([["home-energy-offline-v0"]]);
    expect(w.self.clients.claim).toHaveBeenCalledOnce();
  });

  it("uses the live navigation response without reading or writing a cache", async () => {
    const w = worker();
    const { request, respondWith } = w.request("/settings");
    expect(await (await respondWith.mock.calls[0][0]).text()).toBe("live");
    expect(w.fetch).toHaveBeenCalledWith(request, { cache: "no-store" });
    expect(w.caches.open).not.toHaveBeenCalled();
  });

  it("returns only the public fallback when navigation cannot reach the network", async () => {
    const w = worker();
    w.fetch.mockRejectedValue(new TypeError("offline"));
    const { respondWith } = w.request("/half-hours?date=2026-10-16");
    expect(await respondWith.mock.calls[0][0]).toBe(w.fallback);
    expect(w.cache.match).toHaveBeenCalledWith("/offline.html");
    expect(w.cache.add).not.toHaveBeenCalled();
  });

  it("preserves online authentication failures and server errors", async () => {
    const w = worker();
    const denied = new Response("sign in", { status: 401 });
    w.fetch.mockResolvedValue(denied);
    expect(await w.request().respondWith.mock.calls[0][0]).toBe(denied);
    expect(w.caches.open).not.toHaveBeenCalled();
  });

  it.each([
    ["/api/export/half-hours", "GET", "navigate"],
    ["/auth/tesla/callback?code=private", "GET", "navigate"],
    ["/settings", "POST", "navigate"],
    ["/weekly?_rsc=123", "GET", "cors"],
    ["https://auth.tesla.com/", "GET", "navigate"],
  ])("does not intercept sensitive/non-navigation request %s %s %s", (path, method, mode) => {
    const w = worker();
    expect(w.request(path, method, mode).respondWith).not.toHaveBeenCalled();
    expect(w.fetch).not.toHaveBeenCalled();
    expect(w.caches.open).not.toHaveBeenCalled();
  });
});
