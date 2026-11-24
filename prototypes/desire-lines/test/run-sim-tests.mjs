// Bundles the TypeScript sim tests with esbuild and runs them in Node.
// Usage: node test/run-sim-tests.mjs [entry.ts] [args...]
import * as esbuild from "esbuild";
import { mkdirSync } from "node:fs";
import { dirname, join, basename } from "node:path";
import { fileURLToPath, pathToFileURL } from "node:url";

const here = dirname(fileURLToPath(import.meta.url));
const entry = process.argv[2] && process.argv[2].endsWith(".ts") ? process.argv[2] : join(here, "sim.test.ts");
const outDir = join(here, "..", ".cache");
mkdirSync(outDir, { recursive: true });
const outfile = join(outDir, basename(entry).replace(/\.ts$/, ".mjs"));
await esbuild.build({ entryPoints: [entry], bundle: true, format: "esm", platform: "node", target: "node20", outfile, logLevel: "warning" });
await import(pathToFileURL(outfile).href + "?t=" + Date.now());
