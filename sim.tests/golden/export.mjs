// Regenerates sim.tests/golden/toy-golden.json from a copy of the browser toy's source.
// Usage (from anywhere):
//   node sim.tests/golden/export.mjs <toy-dir> [out.json]
// <toy-dir> is a toy folder with node_modules installed (it needs the toy's esbuild), for example a
// snapshot of prototypes/desire-lines at commit a057b50. The toy commit is read from <toy-dir>/COMMIT,
// or from git in <toy-dir>, and recorded in the JSON with the command. Takes about 4 minutes.
import { createRequire } from "node:module";
import { existsSync, readFileSync, mkdtempSync, rmSync } from "node:fs";
import { execFileSync } from "node:child_process";
import { tmpdir } from "node:os";
import { dirname, join, resolve } from "node:path";
import { fileURLToPath, pathToFileURL } from "node:url";

const here = dirname(fileURLToPath(import.meta.url));
if (!process.argv[2]) { console.error("usage: node sim.tests/golden/export.mjs <toy-dir> [out.json]"); process.exit(2); }
const toy = resolve(process.argv[2]);
const out = resolve(process.argv[3] ?? join(here, "toy-golden.json"));
if (!existsSync(join(toy, "src", "sim", "world.ts"))) { console.error(`${toy} has no src/sim/world.ts`); process.exit(2); }

let commit = "unknown";
if (existsSync(join(toy, "COMMIT"))) commit = readFileSync(join(toy, "COMMIT"), "utf8").trim();
else { try { commit = execFileSync("git", ["-C", toy, "rev-parse", "--short", "HEAD"]).toString().trim(); } catch { /* not a git folder */ } }

// Bundle the exporter with the toy's own esbuild; "@toy/x" resolves to <toy-dir>/src/sim/x.ts.
const esbuild = createRequire(join(toy, "package.json"))("esbuild");
const tmp = mkdtempSync(join(tmpdir(), "toy-golden-"));
const outfile = join(tmp, "export.mjs");
await esbuild.build({
  entryPoints: [join(here, "export-toy-golden.ts")], bundle: true, format: "esm", platform: "node", target: "node20",
  outfile, logLevel: "warning",
  plugins: [{ name: "toy", setup(b) { b.onResolve({ filter: /^@toy\// }, a => ({ path: join(toy, "src", "sim", a.path.slice(5) + ".ts") })); } }],
});
process.env.TOY_GOLDEN_OUT = out;
process.env.TOY_GOLDEN_COMMIT = commit;
process.env.TOY_GOLDEN_COMMAND = "node sim.tests/golden/export.mjs <toy-dir>";
try { await import(pathToFileURL(outfile).href); } finally { rmSync(tmp, { recursive: true, force: true }); }
