// The trunk-share gate (G3) with rain and house growth on at the player's
// defaults, on any seed sets, with how much of the time mud is on the map.
// Usage: node test/run-sim-tests.mjs test/g3-live.ts 1-20,101-120,201-220,601-620
import { Worker, isMainThread, parentPort } from "node:worker_threads";
import { availableParallelism } from "node:os";
import { seedRow, Row, SAMPLE_FROM, DAYS } from "./g3";

const pct = (v: number) => (v * 100).toFixed(1) + "%";
const mean = (a: number[]) => a.reduce((x, y) => x + y, 0) / Math.max(1, a.length);

async function main() {
  const arg = process.argv.slice(2).find(a => /^\d/.test(a)) ?? "601-620";
  const sets = arg.split(",").map(r => r.split("-").map(Number)).map(([a, b]) => [a, b ?? a] as [number, number]);
  const seeds = sets.flatMap(([a, b]) => Array.from({ length: b - a + 1 }, (_, i) => a + i));
  const threads = Math.max(1, Math.min(4, availableParallelism() - 1));
  const rows: Row[] = [];
  await new Promise<void>((resolve, reject) => {
    let next = 0;
    const ws: Worker[] = [];
    for (let i = 0; i < Math.min(threads, seeds.length); i++) {
      const wk = new Worker(new URL(import.meta.url));
      const feed = () => { if (next < seeds.length) wk.postMessage(seeds[next++]); };
      wk.on("message", (r: Row) => { rows.push(r); if (rows.length === seeds.length) { ws.forEach(x => void x.terminate()); resolve(); } else feed(); });
      wk.on("error", reject);
      ws.push(wk);
      feed();
    }
  });
  console.log(`G3 with rain and house growth on (player defaults, 200 walkers to start, every daily sample from day ${SAMPLE_FROM} to ${DAYS} within 30% to 70%)`);
  console.log("set       | mean   | lowest | highest | seeds out of band | rain days | time raining | time with mud | houses");
  const line = (name: string, r: Row[]) => {
    const out = r.filter(x => x.min < 0.3 || x.max > 0.7);
    console.log(`${name.padEnd(9)} | ${pct(mean(r.map(x => x.mean))).padStart(6)} | ${pct(Math.min(...r.map(x => x.min))).padStart(6)} | ${pct(Math.max(...r.map(x => x.max))).padStart(7)} | ${String(out.length).padStart(2)} of ${r.length}${out.length ? " (" + out.map(x => x.seed).join(", ") + ")" : ""} | ${mean(r.map(x => x.rainDays)).toFixed(1).padStart(9)} | ${pct(mean(r.map(x => x.rainTime))).padStart(12)} | ${pct(mean(r.map(x => x.mudTime))).padStart(13)} | ${mean(r.map(x => x.houses)).toFixed(1)}`);
    return out.length;
  };
  let bad = 0;
  for (const [a, b] of sets) bad += line(`${a}-${b}`, rows.filter(r => r.seed >= a && r.seed <= b));
  if (sets.length > 1) line("all", rows);
  console.log(bad ? `G3 FAILS on ${bad} seed(s)` : "G3 passes on every seed");
  if (bad) process.exitCode = 1;
}

if (!isMainThread) parentPort!.on("message", (seed: number) => parentPort!.postMessage(seedRow(seed, 200, {})));
else await main();
