import fs from "node:fs/promises";
import openapiTS, { astToString } from "openapi-typescript";
const url = `${process.env.API_BASE_URL || "http://localhost:5080"}/openapi/v1.json`;
const response = await fetch(url, {
  headers: { Authorization: `Bearer ${process.env.INTERNAL_API_TOKEN}` },
});
if (!response.ok) throw new Error(`OpenAPI fetch failed: ${response.status}`);
const json = await response.json();
await fs.mkdir("lib/api", { recursive: true });
await fs.writeFile(
  "lib/api/openapi.json",
  JSON.stringify(json, null, 2) + "\n",
);
await fs.writeFile("lib/api/schema.d.ts", astToString(await openapiTS(json)));
console.log(
  "Generated schema.d.ts from the running ASP.NET Core OpenAPI contract.",
);
