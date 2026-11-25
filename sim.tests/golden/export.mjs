// Regenerates the toy comparison data for the C# port's tests from copies of the browser toy's source.
// Usage (from anywhere):
//   node sim.tests/golden/export.mjs --legacy <toy-r2-dir> [out.json]   legacy file, toy-golden.json (round 2, a057b50)
//   node sim.tests/golden/export.mjs <final-toy-dir> [out.json]          version 2, toy-golden-v2.json (the finished toy)
//   node sim.tests/golden/export.mjs --legacy <toy-r2-dir> <final-toy-dir>   both, into this folder
// A toy folder needs node_modules installed (it needs the toy's esbuild). The legacy file is made by the round-2
// exporter (export-toy-golden.ts, unchanged), so run on toy-r2 it is byte-equal to the file the old tests read.
// Version 2 (export-toy-golden-v2.ts) records the finished toy with its new rules off and on (see its header).
// The toy commit is read from <toy-dir>/COMMIT, or from git (the last commit that touched the folder), and recorded
// in the JSON with the command. Never edit the JSON by hand. Legacy takes about 4 minutes, version 2 about 15.
import { createRequire } from "node:module";
import { existsSync, readFileSync, mkdtempSync, rmSync } from "node:fs";
import { execFileSync } from "node:child_process";
import { tmpdir } from "node:os";
import { dirname, join, resolve } from "node:path";
import { fileURLToPath, pathToFileURL } from "node:url";

const here = dirname(fileURLToPath(import.meta.url));
const args = process.argv.slice(2);
const usage = () => { console.error("usage: node sim.tests/golden/export.mjs [--legacy <toy-r2-dir>] [<final-toy-dir>] [out.json]"); process.exit(2); };
let legacyDir = null, finalDir = null, outArg = null;
for (let i = 0; i < args.length; i++) {
  if (args[i] === "--legacy") { legacyDir = args[++i]; if (!legacyDir) usage(); }
  else if (!finalDir && existsSync(join(args[i], "src", "sim", "world.ts"))) finalDir = args[i];
  else if (!outArg) outArg = args[i];
  else usage();
}
if (!legacyDir && !finalDir) usage();
if (legacyDir && finalDir && outArg) { console.error("give no out.json when exporting both"); process.exit(2); }

function commitOf(toy) {
  if (existsSync(join(toy, "COMMIT"))) return readFileSync(join(toy, "COMMIT"), "utf8").trim();
  try { return execFileSync("git", ["-C", toy, "log", "-1", "--format=%h", "--", "."]).toString().trim() || "unknown"; } catch { return "unknown"; }
}

/** Bundles `entry` with the toy's own esbuild ("@toy/x" is <toy>/src/sim/x.ts, "@toytest/x" is <toy>/test/x.ts) and runs it. */
async function run(toy, entry, env) {
  toy = resolve(toy);
  if (!existsSync(join(toy, "src", "sim", "world.ts"))) { console.error(`${toy} has no src/sim/world.ts`); process.exit(2); }
  const esbuild = createRequire(join(toy, "package.json"))("esbuild");
  const tmp = mkdtempSync(join(tmpdir(), "toy-golden-"));
  const outfile = join(tmp, "export.mjs");
  await esbuild.build({
    entryPoints: [join(here, entry)], bundle: true, format: "esm", platform: "node", target: "node20",
    outfile, logLevel: "warning",
    plugins: [{ name: "toy", setup(b) {
      b.onResolve({ filter: /^@toy\// }, a => ({ path: join(toy, "src", "sim", a.path.slice(5) + ".ts") }));
      b.onResolve({ filter: /^@toytest\// }, a => ({ path: join(toy, "test", a.path.slice(9) + ".ts") }));
    } }],
  });
  Object.assign(process.env, env, { TOY_GOLDEN_COMMIT: commitOf(toy) });
  try { await import(pathToFileURL(outfile).href); } finally { rmSync(tmp, { recursive: true, force: true }); }
}

const legacyOut = resolve(legacyDir && !finalDir && outArg ? outArg : join(here, "toy-golden.json"));
if (legacyDir) {
  // The round-2 command string, so the legacy file stays byte-equal.
  await run(legacyDir, "export-toy-golden.ts", { TOY_GOLDEN_OUT: legacyOut, TOY_GOLDEN_COMMAND: "node sim.tests/golden/export.mjs <toy-dir>" });
}
if (finalDir) {
  await run(finalDir, "export-toy-golden-v2.ts", {
    TOY_GOLDEN_OUT: resolve(outArg ?? join(here, "toy-golden-v2.json")),
    TOY_GOLDEN_COMMAND: "node sim.tests/golden/export.mjs <final-toy-dir>",
    // Version 2 checks the finished toy, run with the round-2 values, against the legacy traces.
    TOY_GOLDEN_LEGACY: existsSync(legacyOut) ? legacyOut : "",
  });
}
