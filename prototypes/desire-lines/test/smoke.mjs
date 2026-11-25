// npm run smoke: the built page loads with no console errors, the map is not
// blank, every control works, the page contract holds, the layout fits at
// 400 px wide in both themes, and the tools work by touch at 400 x 800 (the map
// never moves, and a tap lands on the cell under the finger). Screenshots go
// to test-results/.
import { createRequire } from "node:module";
import { readFileSync, mkdirSync } from "node:fs";
import { fileURLToPath, pathToFileURL } from "node:url";
import { dirname, join } from "node:path";
const require = createRequire(import.meta.url);
const { chromium } = require("playwright");

const here = dirname(fileURLToPath(import.meta.url));
const file = join(here, "..", "dist", "index.html");
const url = pathToFileURL(file).href;
const shots = join(here, "..", "test-results");
mkdirSync(shots, { recursive: true });

const failures = [];
const check = (ok, what) => { console.log(`  ${ok ? "PASS" : "FAIL"}  ${what}`); if (!ok) failures.push(what); };
/** Resolves after the page has drawn two more frames (so state set by an event has been rendered). */
const frames = page => page.evaluate(() => new Promise(r => requestAnimationFrame(() => requestAnimationFrame(r))));

// ---------------------------------------------------------------- page contract (static)
console.log("PAGE CONTRACT");
const html = readFileSync(file, "utf8");
check(!/<!doctype|<(html|head|body)[\s>]/i.test(html), "no doctype, html, head or body tags");
check(/^<title>Tide &amp; Toll Prototype 1<\/title>\s*<meta name="claude:viewport-fit-sides" content="cover">\s*<style>/.test(html), "title, then viewport-fit meta, then style");
check((html.match(/<style[\s>]/g) || []).length === 1 && (html.match(/<script[\s>]/g) || []).length === 1, "exactly one style and one inline script");
check(!/<script[^>]+src=|<link[^>]+href=|<img[^>]+src=|https?:\/\//i.test(html.replace(/http:\/\/www\.w3\.org\/[^"']*/g, "")), "no external scripts, stylesheets, images or URLs");
check(!/\b(alert|confirm|prompt)\s*\(|window\.print|window\.open/.test(html), "no alert, confirm, prompt, print or window.open");
check(html.length < 300 * 1024, `size under 300 KB (${(html.length / 1024).toFixed(1)} KB)`);

const browser = await chromium.launch();

// ---------------------------------------------------------------- controls (desktop, light)
console.log("\nCONTROLS (1280x860, light)");
{
  const page = await browser.newPage({ viewport: { width: 1280, height: 860 }, deviceScaleFactor: 1, colorScheme: "light" });
  const errors = [];
  page.on("pageerror", e => errors.push(String(e)));
  page.on("console", m => { if (m.type() === "error" || m.type() === "warning") errors.push(`${m.type()}: ${m.text()}`); });
  await page.goto(url);
  await page.waitForFunction(() => window.__tt);
  const stats = () => page.evaluate(() => __tt.stats());
  const pressed = id => page.getAttribute(`#${id}`, "aria-pressed");
  await page.waitForTimeout(1500);

  const s0 = await stats();
  check(s0.simTime > 0.8 && !s0.paused, `simulation runs on load (sim time ${s0.simTime.toFixed(1)} s after 1.5 s)`);
  const blank = await page.evaluate(() => {
    const c = document.getElementById("map");
    const d = c.getContext("2d").getImageData(0, 0, c.width, c.height).data;
    const seen = new Set();
    for (let i = 0; i < d.length; i += 4 * 97) seen.add((d[i] >> 3) << 10 | (d[i + 1] >> 3) << 5 | (d[i + 2] >> 3));
    return { w: c.width, h: c.height, colours: seen.size };
  });
  check(blank.w > 300 && blank.colours > 20, `canvas is drawn (${blank.w}x${blank.h}, ${blank.colours} colour buckets)`);

  await page.click("#btn-pause");
  const p1 = await stats(); await page.waitForTimeout(600); const p2 = await stats();
  check(p2.paused && p1.simTime === p2.simTime && (await pressed("btn-pause")) === "true" && (await page.textContent("#btn-pause")) === "Pause", "Pause stops the clock; the button keeps its label and is pressed");
  await page.click("#btn-pause");
  await page.waitForTimeout(400);
  check(!(await stats()).paused && (await stats()).simTime > p2.simTime, "Play resumes");

  await page.click("#speed-8");
  const a = await stats(); await page.waitForTimeout(1000); const b = await stats();
  check(b.speed === 8 && (await pressed("speed-8")) === "true" && (await pressed("speed-1")) === "false" && b.simTime - a.simTime > 5, `8x speed (${(b.simTime - a.simTime).toFixed(1)} sim s in 1 s)`);
  await page.keyboard.press("1");
  check((await stats()).speed === 1 && (await pressed("speed-1")) === "true", "key 1 sets 1x");
  await page.click("#speed-2"); check((await stats()).speed === 2, "2x speed");
  await page.click("#speed-4"); check((await stats()).speed === 4, "4x speed");
  await page.click("#speed-1");

  await page.evaluate(() => { const r = document.getElementById("walkers"); r.value = "400"; r.dispatchEvent(new Event("input", { bubbles: true })); });
  check((await stats()).walkers === 400 && (await page.textContent("#walkers-out")) === "400", "walker slider sets 400 walkers");
  await page.focus("#walkers"); await page.keyboard.press("ArrowLeft");
  check((await stats()).walkers === 390, "walker slider works from the keyboard");

  // Fence: drag across open grass with the mouse.
  const spot = await page.evaluate(() => {
    const w = __tt.world, N = 128;
    for (let y = 20; y < 108; y++) for (let x = 10; x < 100; x++) {
      let ok = true;
      for (let k = 0; k < 10 && ok; k++) ok = w.canFence(y * N + x + k) && w.land[y * N + x + k] === 1;
      if (ok) return { x, y };
    }
    return null;
  });
  await page.click("#tool-fence");
  check((await pressed("tool-fence")) === "true", "Fence tool toggles on");
  const from = await page.evaluate(([x, y]) => __tt.cellToClient(x, y), [spot.x, spot.y]);
  const to = await page.evaluate(([x, y]) => __tt.cellToClient(x, y), [spot.x + 9, spot.y]);
  await page.mouse.move(from.x, from.y); await page.mouse.down();
  await page.mouse.move((from.x + to.x) / 2, from.y, { steps: 4 }); await page.mouse.move(to.x, to.y, { steps: 4 });
  await page.mouse.up();
  check((await stats()).fences === 10, `dragging draws a 10-cell fence (${(await stats()).fences})`);
  // Pointer mapping honours the canvas border: the fence starts in the cell under the pointer.
  check(await page.evaluate(([x, y]) => __tt.world.fence[y * 128 + x] === 1 && __tt.world.fence[y * 128 + x + 9] === 1, [spot.x, spot.y]), "the fence lands on the cells under the pointer");

  // Flow preview: dragging a fence across the busiest trail shows changed routes and a readout before release.
  check((await pressed("btn-preview")) === "true", "Flow preview is on by default");
  await page.evaluate(() => { __tt.setSpeed(8); });
  await page.waitForTimeout(4000);
  await page.evaluate(() => { __tt.setSpeed(1); });
  const busy = await page.evaluate(() => {
    const w = __tt.world, fw = w.metrics.window(); let b = -1;
    for (let c = 0; c < 128 * 128; c++) { const x = c % 128, y = (c / 128) | 0; if (!w.land[c] || w.fence[c] || Math.abs(x - w.map.riverX[y]) < 10 || x < 12 || x > 115 || y < 12 || y > 115) continue; if (b < 0 || fw[c] > fw[b]) b = c; }
    return [b % 128, (b / 128) | 0];
  });
  const f0 = await stats();
  const pa = await page.evaluate(([x, y]) => __tt.cellToClient(x - 6, y + 6), busy), pb = await page.evaluate(([x, y]) => __tt.cellToClient(x + 6, y - 6), busy);
  await page.mouse.move(pa.x, pa.y); await page.mouse.down(); await page.mouse.move(pb.x, pb.y, { steps: 10 });
  await page.waitForFunction(() => __tt.previewState().settled, null, { timeout: 3000 }).catch(() => {});
  const ps = await page.evaluate(() => __tt.previewState());
  check((await stats()).fences === f0.fences, "nothing is placed while dragging");
  check(ps.text.length > 0 && ps.tipVisible && (await page.textContent("#preview-line")).includes(ps.text), `the preview shows a readout near the pointer and in the status bar ("${ps.text}", ${ps.changes} routes)`);
  await page.screenshot({ path: join(shots, "preview-fence-drag.png") });
  await page.keyboard.press("Escape");
  await page.mouse.up();
  await frames(page);
  check((await stats()).fences === f0.fences && !(await page.evaluate(() => __tt.previewState().tipVisible)), "Escape cancels the drag and hides the preview");
  await page.keyboard.press("p");
  check((await pressed("btn-preview")) === "false", "key P turns the flow preview off");
  await page.click("#btn-preview");
  check((await pressed("btn-preview")) === "true", "the Flow preview button turns it back on");
  await page.keyboard.press("f");

  // Gravel: press G and brush a strip (it snaps to a trail within a cell, so this is on open grass); placed on release.
  await page.keyboard.press("g");
  check((await pressed("tool-gravel")) === "true" && (await pressed("tool-fence")) === "false", "key G selects Gravel");
  const gspot = await page.evaluate(([x, y]) => {
    const w = __tt.world, N = 128;
    for (let yy = y + 4; yy < 118; yy++) for (let xx = 10; xx < 100; xx++) {
      let ok = true;
      // Open grass with no trail within a cell, where the brush does not snap.
      for (let k = 0; k < 8 && ok; k++) for (let d = -1; d <= 1 && ok; d++) ok = w.canGravel((yy + d) * N + xx + k) && w.wear[(yy + d) * N + xx + k] < 25 && w.wear[(yy + d) * N + xx + k - 1] < 25 && w.wear[(yy + d) * N + xx + k + 1] < 25;
      if (ok) return { x: xx, y: yy };
    }
    return null;
  }, [spot.x, spot.y]);
  const g0 = await page.evaluate(([x, y]) => __tt.cellToClient(x, y), [gspot.x, gspot.y]), g1 = await page.evaluate(([x, y]) => __tt.cellToClient(x, y), [gspot.x + 7, gspot.y]);
  await page.mouse.move(g0.x, g0.y); await page.mouse.down(); await page.mouse.move(g1.x, g1.y, { steps: 8 });
  check((await stats()).gravel === 0, "nothing is laid while brushing gravel");
  await page.mouse.up();
  check((await stats()).gravel === 8 && await page.evaluate(([x, y]) => __tt.world.gravel[y * 128 + x] === 1 && __tt.world.gravel[y * 128 + x + 7] === 1, [gspot.x, gspot.y]), `brushing lays gravel under the pointer (${(await stats()).gravel} cells)`);

  // Gate: press T and click on the fence drawn earlier.
  await page.keyboard.press("t");
  check((await pressed("tool-gate")) === "true", "key T selects Gate");
  const gate = await page.evaluate(([x, y]) => __tt.cellToClient(x, y), [spot.x + 4, spot.y]);
  await page.mouse.move(gate.x, gate.y);
  await page.waitForFunction(() => __tt.previewState().settled, null, { timeout: 3000 }).catch(() => {});
  await page.mouse.click(gate.x, gate.y);
  check((await stats()).gates === 1 && await page.evaluate(([x, y]) => __tt.world.gate[y * 128 + x] === 1, [spot.x + 4, spot.y]), "clicking a fence puts a gate in it");
  await page.mouse.click(gate.x, gate.y);
  check((await stats()).gates === 0 && (await stats()).fences === 10, "clicking the gate again takes it out and leaves the fence");
  await page.mouse.click(gate.x, gate.y);
  const offFence = await page.evaluate(([x, y]) => __tt.cellToClient(x, y), [gspot.x + 3, gspot.y + 6]);
  await page.mouse.click(offFence.x, offFence.y);
  check((await stats()).gates === 1 && (await page.textContent("#status")).includes("Gates go on fences"), "a gate off a fence is refused with a message");

  // Footbridge: press B and click on the river away from the ford.
  await page.keyboard.press("b");
  check((await pressed("tool-bridge")) === "true" && (await pressed("tool-fence")) === "false", "key B selects Footbridge");
  const river = await page.evaluate(() => {
    const m = __tt.world.map;
    for (let y = 10; y < 118; y++) if (Math.abs(y - m.fordY) > 10 && __tt.world.bridgeCellsAt(m.riverX[y], y)) return __tt.cellToClient(Math.floor(m.riverX[y]), y);
    return null;
  });
  await page.mouse.click(river.x, river.y);
  check((await stats()).bridges === 1, "clicking the river builds a footbridge");

  // Erase all of it.
  await page.click("#tool-erase");
  check((await pressed("tool-erase")) === "true", "Erase tool toggles on");
  await page.mouse.move(from.x, from.y); await page.mouse.down(); await page.mouse.move(to.x, to.y, { steps: 12 }); await page.mouse.up();
  await page.mouse.click(river.x, river.y);
  await page.mouse.move(g0.x, g0.y); await page.mouse.down(); await page.mouse.move(g1.x, g1.y, { steps: 8 }); await page.mouse.up();
  const se = await stats();
  check(se.fences === 0 && se.gates === 0 && se.bridges === 0 && se.gravel === 0, `erasing removes the fence, its gate, the gravel and the bridge (${se.fences} fence cells, ${se.gates} gates, ${se.gravel} gravel, ${se.bridges} bridges left)`);
  await page.keyboard.press("Escape");
  check((await pressed("tool-erase")) === "false", "Escape clears the tool");

  await page.click("#btn-overlay");
  check((await pressed("btn-overlay")) === "true" && await page.isVisible("#legend-heat") && !(await page.isVisible("#legend-ground")), "Wear overlay toggles on with its legend");
  await page.waitForTimeout(300);
  await page.screenshot({ path: join(shots, "overlay-1280-light.png") });
  await page.keyboard.press("w");
  check((await pressed("btn-overlay")) === "false", "key W toggles the overlay off");

  await page.keyboard.press("Space");
  check((await stats()).paused, "Space pauses");
  await page.keyboard.press("Space");

  const before = await stats();
  await page.click("#btn-reset");
  const r = await stats();
  check(r.simTime < 1 && r.seed === before.seed && r.walkers === 390, "Reset restarts the same map with the same walker count");
  await page.click("#btn-newmap");
  check((await stats()).seed !== before.seed, "New map changes the seed");

  // Rain and house growth through the debug API (the page draws them in a later round).
  const wx = await page.evaluate(async () => {
    __tt.reset(1); __tt.setWalkers(200); __tt.setSpeed(8); __tt.play();
    __tt.startRain(1);
    await new Promise(r => setTimeout(r, 1500));
    const wet = __tt.weather();
    __tt.stopRain(true);
    await new Promise(r => setTimeout(r, 300));
    const dry = __tt.weather();
    await new Promise(r => setTimeout(r, 4500));
    return { wet, dry, houses: __tt.houses() };
  });
  check(wx.wet.rainOn && wx.wet.raining && wx.wet.wetness > 0.5 && wx.wet.mudCells > 0 && !wx.dry.raining && wx.dry.mudCells === 0,
    `startRain and stopRain work (raining, wetness ${wx.wet.wetness.toFixed(2)}, ${wx.wet.mudCells} mud cells; then dry)`);
  check(wx.houses.growthOn && wx.houses.grown.length >= 1 && wx.houses.population === 200 + 3 * wx.houses.grown.length,
    `houses grow on the page (${wx.houses.grown.length} by ${(await stats()).simTime.toFixed(0)} s, population ${wx.houses.population})`);
  await page.evaluate(() => { __tt.setSpeed(1); });

  // Focus is visible on keyboard navigation.
  await page.focus("#btn-reset");
  const outline = await page.evaluate(() => getComputedStyle(document.activeElement).outlineStyle);
  await page.keyboard.press("Tab");
  const outline2 = await page.evaluate(() => getComputedStyle(document.activeElement).outlineStyle);
  check(outline2 !== "none", `keyboard focus is visible (${outline} / ${outline2})`);

  await page.waitForTimeout(500);
  await page.screenshot({ path: join(shots, "page-1280-light.png") });
  check(errors.length === 0, `no console errors or warnings${errors.length ? ": " + errors.join(" | ") : ""}`);
  await page.close();
}

// ---------------------------------------------------------------- layouts and themes
console.log("\nLAYOUT AND THEMES");
for (const [width, scheme] of [[400, "light"], [400, "dark"], [1280, "dark"]]) {
  const page = await browser.newPage({ viewport: { width, height: 860 }, deviceScaleFactor: 1, colorScheme: scheme });
  const errors = [];
  page.on("pageerror", e => errors.push(String(e)));
  page.on("console", m => { if (m.type() === "error") errors.push(m.text()); });
  await page.goto(url);
  await page.waitForFunction(() => window.__tt);
  await page.evaluate(() => __tt.setSpeed(8));
  await page.waitForTimeout(3000);
  const m = await page.evaluate(() => ({
    sw: document.documentElement.scrollWidth, iw: window.innerWidth,
    canvas: document.getElementById("map").getBoundingClientRect().width,
    bg: getComputedStyle(document.body).backgroundColor,
    paper: getComputedStyle(document.documentElement).getPropertyValue("--map-paper").trim(),
  }));
  check(m.sw <= m.iw && m.canvas <= width - 32 + 1 && m.canvas > 200, `${width} px ${scheme}: no sideways scroll (${m.sw}/${m.iw}), map ${m.canvas.toFixed(0)} px, body ${m.bg}, map paper ${m.paper}`);
  check(errors.length === 0, `${width} px ${scheme}: no errors`);
  await page.screenshot({ path: join(shots, `page-${width}-${scheme}.png`) });
  await page.close();
}
// ---------------------------------------------------------------- phone, touch
console.log("\nPHONE (400x800, touch)");
{
  const ctx = await browser.newContext({ viewport: { width: 400, height: 800 }, deviceScaleFactor: 1, colorScheme: "light", hasTouch: true, isMobile: true });
  const page = await ctx.newPage();
  // A bare file has no viewport meta; the artifact host adds one.
  await page.addInitScript(() => document.addEventListener("DOMContentLoaded", () => { const m = document.createElement("meta"); m.name = "viewport"; m.content = "width=device-width, initial-scale=1"; document.head.appendChild(m); }));
  const errors = [];
  page.on("pageerror", e => errors.push(String(e)));
  await page.goto(url);
  await page.waitForFunction(() => window.__tt);
  await page.evaluate(() => __tt.setSpeed(8));
  await page.waitForTimeout(3000);
  await page.evaluate(() => __tt.setSpeed(1));
  const cdp = await ctx.newCDPSession(page);
  const touch = (type, p) => cdp.send("Input.dispatchTouchEvent", { type, touchPoints: p ? [{ x: p.x, y: p.y }] : [] });
  const hold = async (p, ms = 250) => { await touch("touchStart", p); await page.waitForTimeout(ms); await touch("touchEnd"); await frames(page); };
  const drag = async (a, b, steps = 10, ms = 40) => {
    await touch("touchStart", a);
    for (let i = 1; i <= steps; i++) { await touch("touchMove", { x: a.x + ((b.x - a.x) * i) / steps, y: a.y + ((b.y - a.y) * i) / steps }); await page.waitForTimeout(ms); }
  };
  const rect = () => page.evaluate(() => { const r = document.getElementById("map").getBoundingClientRect(); return [r.left, r.top, r.width, r.height].map(v => Math.round(v * 10) / 10).join(","); });
  const at = (x, y) => page.evaluate(([x, y]) => __tt.cellToClient(x, y), [x, y]);
  const stats = () => page.evaluate(() => __tt.stats());
  const box0 = await rect();
  const first = await page.evaluate(() => {
    const r = document.getElementById("map").getBoundingClientRect(), small = [];
    for (const el of document.querySelectorAll("header button, header input")) { const b = el.getBoundingClientRect(); if (b.height < 40) small.push(`${el.id} ${Math.round(b.height)}px`); }
    return { bottom: r.bottom, h: innerHeight, sw: document.documentElement.scrollWidth, iw: innerWidth, small };
  });
  check(first.small.length === 0, `buttons and the slider are at least 40 px high${first.small.length ? ": " + first.small.join(", ") : ""}`);
  check(first.bottom <= first.h && first.sw <= first.iw, `the tools and the whole map are on the first screen, no sideways scroll (map bottom ${Math.round(first.bottom)} of ${first.h})`);

  // A row of 21 open cells for the fence, away from the river.
  const row = await page.evaluate(() => {
    const w = __tt.world, N = 128;
    for (let y = 70; y < 120; y++) for (let x = 8; x < 100; x++) {
      let ok = true;
      for (let k = 0; k <= 20 && ok; k++) ok = w.canFence(y * N + x + k) && Math.abs(x + k - w.map.riverX[y]) > 6;
      if (ok) return { x, y };
    }
    return null;
  });
  await page.click("#tool-fence");
  const boxTool = await rect();
  // A held drag along the row: the readout appears in the status bar, and the map does not move.
  await drag(await at(row.x, row.y), await at(row.x + 20, row.y));
  await page.waitForFunction(() => __tt.previewState().settled && __tt.previewState().line.length > 0, null, { timeout: 4000 }).catch(() => {});
  const ps = await page.evaluate(() => __tt.previewState());
  const boxPreview = await rect();
  await page.screenshot({ path: join(shots, "phone-fence-drag.png") });
  await touch("touchEnd"); await frames(page);
  check(ps.line.length > 0 && !ps.tipVisible, `the readout shows in the status bar and not under the finger ("${ps.line}")`);
  check(box0 === boxTool && boxTool === boxPreview, `the map does not move or resize when a tool or the readout appears (${box0} / ${boxTool} / ${boxPreview})`);
  const fenced = await page.evaluate(([x, y]) => { const w = __tt.world, out = []; for (let c = 0; c < 128 * 128; c++) if (w.fence[c]) out.push([c % 128, (c / 128) | 0]); return out; }, [row.x, row.y]);
  check(fenced.length === 21 && fenced.every(([x, y]) => y === row.y && x >= row.x && x <= row.x + 20), `a touch drag fences exactly the cells under the finger (${fenced.length} cells, rows ${[...new Set(fenced.map(c => c[1]))].join(" ")})`);

  // A quick tap fences the tapped cell.
  const tapCell = { x: row.x + 10, y: row.y - 6 };
  await page.evaluate(() => __tt.setTool("fence"));
  const okTap = await page.evaluate(([x, y]) => __tt.world.canFence(y * 128 + x), [tapCell.x, tapCell.y]);
  await page.touchscreen.tap((await at(tapCell.x, tapCell.y)).x, (await at(tapCell.x, tapCell.y)).y); await frames(page);
  check(okTap && await page.evaluate(([x, y]) => __tt.world.fence[y * 128 + x] === 1, [tapCell.x, tapCell.y]) && (await stats()).fences === 22, "a tapped cell is the fenced cell");

  // Gate: a held press on the fence, released in place.
  await page.click("#tool-gate");
  await hold(await at(row.x + 5, row.y));
  check((await stats()).gates === 1 && await page.evaluate(([x, y]) => __tt.world.gate[y * 128 + x] === 1, [row.x + 5, row.y]), "a gate can be put in the fence by touch");

  // Footbridge: a held press on the river (the readout appears meanwhile), released in place.
  const river = await page.evaluate(() => {
    const m = __tt.world.map;
    for (let y = 10; y < 118; y++) if (Math.abs(y - m.fordY) > 10 && __tt.world.bridgeCellsAt(m.riverX[y], y)) return { x: Math.floor(m.riverX[y]), y };
    return null;
  });
  await page.click("#tool-bridge");
  const rp = await at(river.x, river.y);
  await touch("touchStart", rp);
  await page.waitForFunction(() => __tt.previewState().settled, null, { timeout: 3000 }).catch(() => {});
  const boxBridge = await rect();
  await touch("touchEnd"); await frames(page);
  check((await stats()).bridges === 1 && boxBridge === box0, `a footbridge can be built by touch (${(await stats()).bridges} built; status "${await page.textContent("#status")}")`);

  // Gravel: a touch drag brushes a strip.
  await page.click("#tool-gravel");
  const gy = row.y + 4;
  await drag(await at(row.x, gy), await at(row.x + 8, gy), 8);
  await touch("touchEnd"); await frames(page);
  const gravel = (await stats()).gravel;
  check(gravel >= 7, `a touch drag brushes gravel (${gravel} cells)`);

  // Erase by touch removes what was tapped, and nothing far from it.
  await page.click("#tool-erase");
  const e1 = await at(row.x + 15, row.y);
  await page.touchscreen.tap(e1.x, e1.y); await frames(page);
  const after = await page.evaluate(([x, y]) => ({ tapped: __tt.world.fence[y * 128 + x + 15], far: __tt.world.fence[y * 128 + x], gate: __tt.world.gate[y * 128 + x + 5] }), [row.x, row.y]);
  check(after.tapped === 0 && after.far === 1 && after.gate === 1, "erase by touch removes the tapped fence and leaves the rest");
  await page.touchscreen.tap(rp.x, rp.y); await frames(page);
  const e2 = await at(row.x + 4, gy);
  await page.touchscreen.tap(e2.x, e2.y); await frames(page);
  const st = await stats();
  check(st.bridges === 0 && st.gravel === gravel - 3 && await rect() === box0, `erase by touch removes the tapped bridge and the tapped gravel (${st.bridges} bridges, ${gravel - st.gravel} gravel cells removed)`);
  await page.screenshot({ path: join(shots, "phone-400-touch.png") });
  check(errors.length === 0, `phone: no errors${errors.length ? ": " + errors.join(" | ") : ""}`);
  await ctx.close();
}

// The data-theme override must also switch the palette.
{
  const page = await browser.newPage({ viewport: { width: 1280, height: 860 }, colorScheme: "light" });
  await page.goto(url);
  await page.waitForFunction(() => window.__tt);
  const c1 = await page.evaluate(() => getComputedStyle(document.body).backgroundColor);
  await page.evaluate(() => document.documentElement.setAttribute("data-theme", "dark"));
  await page.waitForTimeout(300);
  const c2 = await page.evaluate(() => getComputedStyle(document.body).backgroundColor);
  check(c1 !== c2, `data-theme="dark" switches the theme (${c1} -> ${c2})`);
  await page.close();
}
await browser.close();

console.log(`\n${failures.length ? `SMOKE FAILED: ${failures.length} check(s)` : "SMOKE PASSED"}; screenshots in test-results/`);
if (failures.length) process.exitCode = 1;
