// Prints the exact per-seed G3 tables of the five old seed sets (rain and house
// growth off), in the format of test/g3-baseline.txt.
// Usage: node test/run-sim-tests.mjs test/g3-tables.ts > out.txt
import { Worker, isMainThread, parentPort } from "node:worker_threads";
import { availableParallelism } from "node:os";
import { seedRow, exactTable, Row } from "./g3";

export const OLD_SETS: [string, number][] = [["seeds 401-420", 401], ["seeds 1-20", 1], ["seeds 101-120", 101], ["seeds 201-220", 201], ["seeds 301-320", 301]];

async function main() {
  const seeds = OLD_SETS.flatMap(([, a]) => Array.from({ length: 20 }, (_, i) => a + i));
  const threads = Math.max(1, Math.min(4, availableParallelism() - 1));
  const rows: Row[] = [];
  await new Promise<void>((resolve, reject) => {
    let next = 0;
    const ws: Worker[] = [];
    for (let i = 0; i < threads; i++) {
      const wk = new Worker(new URL(import.meta.url));
      const feed = () => { if (next < seeds.length) wk.postMessage(seeds[next++]); };
      wk.on("message", (r: Row) => { rows.push(r); if (rows.length === seeds.length) { ws.forEach(x => void x.terminate()); resolve(); } else feed(); });
      wk.on("error", reject);
      ws.push(wk);
      feed();
    }
  });
  for (const [name, a] of OLD_SETS) console.log(exactTable(`G3 ${name} (200 walkers, rain and growth off)`, rows.filter(r => r.seed >= a && r.seed < a + 20)) + "\n");
}

if (!isMainThread) parentPort!.on("message", (seed: number) => parentPort!.postMessage(seedRow(seed, 200)));
else await main();
