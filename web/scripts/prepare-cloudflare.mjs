import { readFile, writeFile } from "node:fs/promises";

// Temporary upstream fix: Next 16.4 moved draft-mode keys into preview-props.json.
// https://github.com/opennextjs/opennextjs-cloudflare/pull/1356
// Remove this shim when upgrading to an adapter release containing that fix.
const root = new URL("../node_modules/@opennextjs/cloudflare/", import.meta.url);
const { version } = JSON.parse(await readFile(new URL("package.json", root), "utf8"));
if (version !== "1.20.10") {
  throw new Error("Recheck the OpenNext preview-props compatibility shim before changing adapter versions.");
}
const path = new URL("dist/cli/build/patches/plugins/load-manifest.js", root);
const source = await readFile(path, "utf8");
const original = "**/{*-manifest,required-server-files,prefetch-hints}.json";
const patched = "**/{*-manifest,required-server-files,prefetch-hints,preview-props}.json";
if (!source.includes(patched)) {
  if (source.split(original).length !== 2) throw new Error("Unexpected OpenNext manifest patch layout.");
  await writeFile(path, source.replace(original, patched));
}
console.log("OpenNext Next.js 16.4 preview-props manifest compatibility fix verified.");
