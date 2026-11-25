// scratch: shower, houses, gravel pull. MODE=shower|houses|gravel
import { World } from "../src/sim/world";
import { PARAMS } from "../src/sim/params";
import { N, NC, line4 } from "../src/sim/grid";
const ov = JSON.parse(process.env.OV || "{}");
for (const k in ov) (PARAMS as any)[k] = ov[k];
const TPD = PARAMS.daySimSec * PARAMS.tickHz;
const run = (w: World, d: number) => { for (let i = 0; i < Math.round(d * TPD); i++) w.tick(); };
const mode = process.env.MODE || "shower";
const seeds = (process.env.SEEDS || "1,2,3,4,5,6").split(",").map(Number);
const ON = { rain: true, growth: true };
if (mode === "shower") {
  // natural rain off for control; forced shower at day 25 (season index of day 25 printed)
  const saved = PARAMS.rainChance.slice();
  for (const day of [24]) for (const seed of seeds) {
    const mk = () => { const w = new World(seed, 200, { rain: true, growth: false }); (PARAMS as any).rainChance = [0,0,0,0]; run(w, day); return w; };
    const a = mk(), b = mk();
    b.startRain();
    let mudTicks = 0, peak = 0, wet0 = 0, durDays = 0;
    const out: string[] = [];
    for (let d = 1; d <= 6; d++) {
      for (let i = 0; i < TPD; i++) { a.tick(); b.tick(); if (b.weather.wetness > 0) mudTicks++; peak = Math.max(peak, b.mudCells); }
      const sa = a.stats(), sb = b.stats();
      out.push(`d${d}: det ${sa.detourRatio.toFixed(3)}/${sb.detourRatio.toFixed(3)} trunk ${(sa.trunkShare*100).toFixed(1)}/${(sb.trunkShare*100).toFixed(1)} cov ${(sa.trailCoverage*100).toFixed(1)}/${(sb.trailCoverage*100).toFixed(1)} mud ${b.mudCells}`);
    }
    console.log(`seed ${seed} season ${Math.floor(day*15/60)%4} peakMud ${peak} wetSec ${(mudTicks/10).toFixed(1)}\n  ` + out.join("\n  "));
  }
}
if (mode === "houses") {
  for (const seed of seeds) {
    const w = new World(seed, 200, ON);
    run(w, 45);
    const gh = w.grown;
    // distance from each house to nearest door of other building; clustering: max houses within 8 cells
    const pts = gh.map(g => [g.x + 1, g.y + 1]);
    let maxClust = 0;
    for (const p of pts) maxClust = Math.max(maxClust, pts.filter(q => Math.hypot(p[0]-q[0], p[1]-q[1]) <= 8).length);
    // trail components reaching door? count houses whose front wear >= 50 now
    const fronted = gh.filter(g => w.wear[g.front] >= 50).length;
    // walkers stuck?
    const days = gh.map(g => g.day).join(",");
    console.log(`seed ${seed}: ${gh.length} houses days ${days}; maxCluster(8c) ${maxClust}; frontage still track ${fronted}/${gh.length}; positions ${gh.map(g=>g.x+","+g.y).join(" ")}; walkers out ${w.walkersOut()} pop ${w.walkers.length}`);
  }
}
if (mode === "gravel") {
  const off = +(process.env.OFF || 3), day = +(process.env.DAY || 20);
  let sumA = 0, sumB = 0, wins = 0, n = 0;
  for (const seed of seeds) {
    const mk = (g: boolean) => { const w = new World(seed, 200, { rain: false, growth: false }); run(w, day); return w; };
    const a = mk(true), b = mk(false);
    const fw = a.metrics.window();
    let best = -1;
    for (let c = 0; c < NC; c++) if (a.land[c] && a.wear[c] >= 25 && !a.map.crop[c] && (best < 0 || fw[c] > fw[best])) best = c;
    // direction: find best trunk's local direction via neighbors with wear
    let bx = best % N, by = (best / N) | 0;
    // pick trunk direction by wear-weighted PCA over 12 radius
    let sxx=0,syy=0,sxy=0,sw=0,mx=0,my=0;
    for (let y=by-10;y<=by+10;y++) for (let x=bx-10;x<=bx+10;x++){ if(x<0||y<0||x>=N||y>=N) continue; const c=y*N+x; if(a.wear[c]<25) continue; const wt=fw[c]; mx+=x*wt; my+=y*wt; sw+=wt; }
    mx/=sw; my/=sw;
    for (let y=by-10;y<=by+10;y++) for (let x=bx-10;x<=bx+10;x++){ if(x<0||y<0||x>=N||y>=N) continue; const c=y*N+x; if(a.wear[c]<25) continue; const wt=fw[c]; sxx+=wt*(x-mx)**2; syy+=wt*(y-my)**2; sxy+=wt*(x-mx)*(y-my); }
    const th = 0.5*Math.atan2(2*sxy, sxx-syy), ux=Math.cos(th), uy=Math.sin(th);
    let strip: number[] = [];
    for (const side of [1,-1]) {
      const px=-uy*off*side, py=ux*off*side;
      const line = line4(Math.round(bx-12*ux+px), Math.round(by-12*uy+py), Math.round(bx+12*ux+px), Math.round(by+12*uy+py));
      if (line.every(c => a.canGravel(c) && a.wear[c] < 25 && !a.map.crop[c])) { strip = line; break; }
    }
    if (!strip.length) continue;
    a.placeGravel(strip);
    run(a, 6); run(b, 6);
    const fa = a.metrics.window(), fb = b.metrics.window();
    const sa = strip.reduce((s,c)=>s+fa[c],0), sb = strip.reduce((s,c)=>s+fb[c],0);
    // share: strip footsteps / (strip + trunk footsteps) approx using top trunk cell neighborhood
    sumA += sa; sumB += sb; n++; if (sa >= 3*Math.max(1,sb)) wins++;
  }
  console.log(`gravel off=${off} day=${day} cost=${PARAMS.gravelCost}: strips ${n}, footsteps on strip with ${(sumA/n).toFixed(0)} without ${(sumB/n).toFixed(0)}, 3x on ${wins}`);
}
