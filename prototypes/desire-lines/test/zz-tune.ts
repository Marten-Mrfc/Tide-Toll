// scratch tuning driver (delete afterwards)
import { Worker, isMainThread, parentPort, workerData } from "node:worker_threads";
import { seedRow } from "./g3";
import { PARAMS } from "../src/sim/params";
const ov = JSON.parse(process.env.OV || "{}");
for (const k in ov) (PARAMS as any)[k] = ov[k];
const rain = process.env.RAIN !== "0", growth = process.env.GROWTH !== "0";
const seeds: number[] = [];
for (const a of [1, 101, 201]) for (let i = 0; i < 20; i++) seeds.push(a + i);
async function main() {
  const rows: any[] = [];
  await new Promise<void>((res, rej) => {
    let next = 0; const ws: Worker[] = [];
    for (let i = 0; i < 3; i++) {
      const wk = new Worker(new URL(import.meta.url), { workerData: { ov: process.env.OV, rain, growth } });
      const feed = () => { if (next < seeds.length) wk.postMessage(seeds[next++]); };
      wk.on("message", (r: any) => { rows.push(r); if (rows.length === seeds.length) { ws.forEach(x => void x.terminate()); res(); } else feed(); });
      wk.on("error", rej); ws.push(wk); feed();
    }
  });
  rows.sort((a, b) => a.seed - b.seed);
  const m = (f: (r: any) => number) => rows.reduce((s, r) => s + f(r), 0) / rows.length;
  if (process.env.V) for (const r of rows) console.log(r.seed, r.mean.toFixed(3), r.min.toFixed(3), r.max.toFixed(3), r.det.toFixed(3), r.cov.toFixed(3), r.width, r.houses, r.rainDays, r.finalWalkers);
  console.log(`rain=${rain} growth=${growth} ov=${process.env.OV || "{}"}`);
  console.log(`trunk mean ${(m(r => r.mean) * 100).toFixed(1)} lowest ${(Math.min(...rows.map(r => r.min)) * 100).toFixed(1)} highest ${(Math.max(...rows.map(r => r.max)) * 100).toFixed(1)} out ${rows.filter(r => r.below).length} detour ${m(r => r.det).toFixed(3)} cov ${(m(r => r.cov) * 100).toFixed(1)} width ${m(r => r.width).toFixed(2)} houses ${m(r => r.houses).toFixed(1)} rainDays ${m(r => r.rainDays).toFixed(1)} ft ${m(r => r.ft).toFixed(1)}`);
}
if (!isMainThread) { if (workerData.ov) for (const k in JSON.parse(workerData.ov)) (PARAMS as any)[k] = JSON.parse(workerData.ov)[k]; parentPort!.on("message", (s: number) => parentPort!.postMessage(seedRow(s, 200, { rain: workerData.rain, growth: workerData.growth } as any))); }
else await main();
