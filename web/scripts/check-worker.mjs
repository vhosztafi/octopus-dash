import { spawn } from "node:child_process";
import { createHash } from "node:crypto";
import { mkdtemp, readFile, rm, writeFile } from "node:fs/promises";
import { tmpdir } from "node:os";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { SignJWT } from "jose";

const root = fileURLToPath(new URL("../", import.meta.url));
const directory = await mkdtemp(path.join(tmpdir(), "octopus-worker-check-"));
const config = JSON.parse(await readFile(path.join(root, "wrangler.jsonc"), "utf8"));
const sessionKey = "synthetic-worker-smoke-session-key-at-least-32-characters";
const passwordHash = "synthetic-worker-smoke-password-hash";
const port = 9876;
const base = `http://127.0.0.1:${port}`;
config.name = "octopus-dash-ci-smoke";
config.main = path.join(root, config.main);
config.assets.directory = path.join(root, config.assets.directory);
config.services = [{ binding: "WORKER_SELF_REFERENCE", service: config.name }];
delete config.routes;
config.vars = {
  ...config.vars,
  API_BASE_URL: "https://octopus-worker-smoke.invalid",
  APP_PASSWORD_HASH: passwordHash,
  SESSION_SECRET: sessionKey,
  INTERNAL_API_TOKEN: "synthetic-worker-smoke-internal-token-at-least-32-characters",
};
await writeFile(path.join(directory, "wrangler.json"), JSON.stringify(config));
// Running from this temporary directory prevents loading the owner's .dev.vars.
const child = spawn(process.execPath, [path.join(root, "node_modules/wrangler/bin/wrangler.js"), "dev", "--local", "--config", path.join(directory, "wrangler.json"), "--port", String(port), "--ip", "127.0.0.1"], {
  cwd: directory,
  windowsHide: true,
  env: { ...process.env, WRANGLER_SEND_METRICS: "false", CLOUDFLARE_LOAD_DEV_VARS_FROM_DOT_ENV: "false" },
  stdio: ["ignore", "pipe", "pipe"],
});
let logs = "";
child.stdout.on("data", value => { logs = (logs + value).slice(-20000); });
child.stderr.on("data", value => { logs = (logs + value).slice(-20000); });
let spawnError;
child.on("error", error => { spawnError = error; });
const request = (url, options = {}) => fetch(url, { ...options, signal: AbortSignal.timeout(10000), redirect: "manual" });
try {
  const deadline = Date.now() + 60000;
  let ready = false;
  while (Date.now() < deadline) {
    if (spawnError) throw spawnError;
    if (child.exitCode !== null) throw new Error("Worker preview exited before becoming ready.");
    try { await request(base + "/login"); ready = true; break; } catch { await new Promise(resolve => setTimeout(resolve, 500)); }
  }
  if (!ready) throw new Error("Worker preview did not start within 60 seconds.");
  for (const route of ["/", "/settings", "/weekly", "/half-hours", "/auth/tesla/callback?state=bad&code=synthetic-code", "/api/export/half-hours?date=2026-10-11"]) {
    const response = await request(base + route);
    const location = response.headers.get("location");
    if (![307, 308].includes(response.status) || !location || new URL(location, base).pathname !== "/login") throw new Error(`Unauthorized route was not protected: ${route}`);
  }
  const login = await request(base + "/login");
  const loginHtml = await login.text();
  if (login.status !== 200 || !loginHtml.includes('type="password"')) throw new Error("Worker login page did not render.");
  if (!login.headers.has("content-security-policy")) throw new Error("Worker security headers are missing.");
  const domainKey = await request(base + "/.well-known/appspecific/com.tesla.3p.public-key.pem");
  if (domainKey.status !== 200 || !(await domainKey.text()).startsWith("-----BEGIN PUBLIC KEY-----")) throw new Error("Tesla public domain key is not anonymously accessible.");
  const version = createHash("sha256").update(passwordHash).digest("hex");
  const token = await new SignJWT({ version }).setProtectedHeader({ alg: "HS256" }).setIssuedAt().setExpirationTime("8h").setIssuer("home-energy").setAudience("home-energy-web").sign(new TextEncoder().encode(sessionKey));
  const authHeaders = { Cookie: "energy-session=" + token };
  const invalidTesla = await request(base + "/auth/tesla/callback?state=bad&code=synthetic-code", { headers: authHeaders });
  const teslaLocation = invalidTesla.headers.get("location");
  if (invalidTesla.status !== 303 || !teslaLocation || new URL(teslaLocation).pathname !== "/settings" || teslaLocation.includes("synthetic-code") || invalidTesla.headers.get("cache-control") !== "no-store" || invalidTesla.headers.get("referrer-policy") !== "no-referrer") throw new Error(`Tesla callback did not safely reject invalid state: status=${invalidTesla.status}, location=${teslaLocation}, cache=${invalidTesla.headers.get("cache-control")}, referrer=${invalidTesla.headers.get("referrer-policy")}.`);
  const clearedTeslaCookie = invalidTesla.headers.get("set-cookie") ?? "";
  if (!clearedTeslaCookie.includes("__Host-energy-tesla-oauth=") || !/max-age=0/i.test(clearedTeslaCookie) || !/; secure/i.test(clearedTeslaCookie)) throw new Error("Tesla callback did not clear its secure state cookie.");
  const invalidExport = await request(base + "/api/export/half-hours?date=bad", { headers: authHeaders });
  if (invalidExport.status !== 400) throw new Error("Worker did not accept its signed session in the BFF.");
  const authenticated = await request(base + "/", { headers: authHeaders });
  const html = await authenticated.text();
  // Next streams the error digest and renders the client error boundary on hydration.
  const scriptPaths = [...new Set([...html.matchAll(/<script[^>]+src="([^"]+)"/g)].map(match => match[1]))];
  const scripts = await Promise.all(scriptPaths.map(async url => (await request(new URL(url, base))).text()));
  if (authenticated.status !== 200 || authenticated.headers.get("location") || !html.includes("$RX(") || !scripts.some(source => source.includes("Energy data is unavailable"))) throw new Error("Worker unavailable-API error boundary or assets are missing.");
  console.log("Worker smoke passed: protected routes, login, security headers, signed session and unavailable API.");
} catch (error) {
  // This Worker uses synthetic credentials only.
  console.error(logs);
  console.error(error);
  throw error;
} finally {
  if (process.platform === "win32" && child.exitCode === null && child.pid) {
    const stop = spawn("taskkill", ["/pid", String(child.pid), "/t", "/f"], { stdio: "ignore", windowsHide: true });
    await new Promise(resolve => { stop.once("exit", resolve); stop.once("error", resolve); });
  } else child.kill("SIGTERM");
  await new Promise(resolve => { if (child.exitCode !== null || spawnError) resolve(); else { child.once("exit", resolve); setTimeout(() => { child.kill("SIGKILL"); resolve(); }, 5000).unref(); } });
  await rm(directory, { recursive: true, force: true, maxRetries: 10, retryDelay: 200 });
}
