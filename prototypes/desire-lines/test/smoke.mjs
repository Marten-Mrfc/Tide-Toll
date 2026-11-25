// npm run smoke: the built page loads with no console errors, the map is not
// blank, every control works, the page contract holds, the layout fits at
// 400 px wide in both themes, the tools work by touch at 400 x 800 and 360 x 740
// (the map never moves, a tap lands on the cell under the finger, and a second
// finger does not disturb a drag or a press), sound stays off with no
// AudioContext until the Sound button is pressed and then runs a minute at 8x
// within its voice budget, and rain, mud and new houses are drawn. The page is
// loaded inside the same document skeleton the Artifact host gives it
// (host.mjs), so mobile emulation lays it out at the phone's real width.
// Screenshots go to test-results/.
import { createRequire } from "node:module";
import { readFileSync, mkdirSync } from "node:fs";
import { fileURLToPath } from "node:url";
import { dirname, join } from "node:path";
const require = createRequire(import.meta.url);
const { chromium } = require("playwright");
import * as esbuild from "esbuild";

import { hostedUrl, builtFile } from "./host.mjs";
const here = dirname(fileURLToPath(import.meta.url));
const file = builtFile;
const url = hostedUrl();
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

  // A gate on a diagonal fence (a staircase of cells), and a lone post that is refused with its reason.
  const open = await page.evaluate(() => {
    const w = __tt.world;
    for (let y = 8; y < 100; y += 2) for (let x = 8; x < 100; x += 2) {
      let ok = true;
      for (let yy = y - 2; yy < y + 22 && ok; yy++) for (let xx = x - 2; xx < x + 14 && ok; xx++) ok = w.canFence(yy * 128 + xx) && w.land[yy * 128 + xx] === 1 && !w.fence[yy * 128 + xx] && !w.gravel[yy * 128 + xx];
      if (ok) return { x, y };
    }
    return null;
  });
  if (open) {
    const stair = [];
    for (let i = 0; i < 10; i++) stair.push([open.x + i, open.y + i], [open.x + i + 1, open.y + i]);
    await page.evaluate(c => __tt.placeFence(c), stair);
    const g0 = (await stats()).gates;
    const mid = await page.evaluate(([x, y]) => __tt.cellToClient(x, y), [open.x + 5, open.y + 5]);
    await page.mouse.click(mid.x, mid.y);
    const placed = (await stats()).gates > g0 && (await page.textContent("#status")).includes("Gate placed");
    check(placed, `a gate goes on a diagonal fence ("${await page.textContent("#status")}")`);
    await page.evaluate(([x, y]) => __tt.placeFence([[x, y]]), [open.x + 5, open.y + 18]);
    const post = await page.evaluate(([x, y]) => __tt.cellToClient(x, y), [open.x + 5, open.y + 18]);
    await page.mouse.click(post.x, post.y);
    check((await page.textContent("#status")).includes("lone post"), `a gate on a lone post is refused with its reason ("${await page.textContent("#status")}")`);
    await page.evaluate(c => __tt.erase(c), [...stair, [open.x + 5, open.y + 18]]);
  } else check(false, "an open patch for the diagonal fence");

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
    const h1 = __tt.houses();
    const off = __tt.setRules({ rain: false, growth: false });
    await new Promise(r => setTimeout(r, 1500));
    const offState = { weather: __tt.weather(), houses: __tt.houses() };
    __tt.setRules({ rain: true, growth: true });
    return { wet, dry, houses: h1, off, offState };
  });
  check(wx.wet.rainOn && wx.wet.raining && wx.wet.wetness > 0.5 && wx.wet.mudCells > 0 && !wx.dry.raining && wx.dry.mudCells === 0,
    `startRain and stopRain work (raining, wetness ${wx.wet.wetness.toFixed(2)}, ${wx.wet.mudCells} mud cells; then dry)`);
  check(wx.houses.growthOn && wx.houses.grown.length >= 1 && wx.houses.population === 200 + 3 * wx.houses.grown.length,
    `houses grow on the page (${wx.houses.grown.length}, built on day ${wx.houses.grown.map(h => h.day).join(", ")}; population ${wx.houses.population})`);
  check(!wx.off.rain && !wx.off.growth && !wx.offState.weather.rainOn && wx.offState.houses.grown.length === 0 && (await page.evaluate(() => __tt.weather().rainOn && __tt.houses().growthOn)),
    "setRules switches rain and house growth off and on again (a new map each time)");
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
  const errors = [];
  page.on("pageerror", e => errors.push(String(e)));
  await page.goto(url);
  await page.waitForFunction(() => window.__tt);
  await page.evaluate(() => __tt.setSpeed(8));
  await page.waitForTimeout(3000);
  await page.evaluate(() => __tt.setSpeed(1));
  const cdp = await ctx.newCDPSession(page);
  const touch = (type, p) => cdp.send("Input.dispatchTouchEvent", { type, touchPoints: p ? [{ x: p.x, y: p.y }] : [] });
  // Several fingers: touchStart and touchEnd list the points that start or end; touchMove lists the points down.
  const touch2 = (type, pts) => cdp.send("Input.dispatchTouchEvent", { type, touchPoints: pts });
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
    for (const el of document.querySelectorAll("header button, header input")) { const b = el.getBoundingClientRect(); if (b.height && (b.height < 40 || (el.tagName === "BUTTON" && b.width < 40))) small.push(`${el.id} ${Math.round(b.width)}x${Math.round(b.height)}px`); }
    return { bottom: r.bottom, h: innerHeight, sw: document.documentElement.scrollWidth, iw: innerWidth, small };
  });
  check(first.iw === 400, `the page is laid out at the phone's width (innerWidth ${first.iw})`);
  check(first.small.length === 0, `buttons are at least 40 x 40 px and the slider 40 px high${first.small.length ? ": " + first.small.join(", ") : ""}`);
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

  // A second finger during a fence drag, a gravel stroke and a footbridge press is ignored.
  const fid = { a: 1, b: 2 };
  const pt = (p, id) => ({ x: p.x, y: p.y, id });
  const twoFinger = async (from, to, other, steps = 8) => {
    await touch2("touchStart", [pt(from, fid.a)]);
    for (let i = 1; i <= steps; i++) {
      const p = { x: from.x + ((to.x - from.x) * i) / steps, y: from.y + ((to.y - from.y) * i) / steps };
      if (i === 3) await touch2("touchStart", [pt(other, fid.b)]);
      if (i === 4 || i === 5) await touch2("touchMove", [pt(p, fid.a), pt({ x: other.x + 30 * (i - 3), y: other.y - 20 * (i - 3) }, fid.b)]);
      else await touch2("touchMove", [pt(p, fid.a)]);
      if (i === 6) await touch2("touchEnd", [pt({ x: other.x + 60, y: other.y - 40 }, fid.b)]);
      await page.waitForTimeout(30);
    }
    await touch2("touchEnd", [pt(to, fid.a)]); await frames(page);
  };
  const row2 = await page.evaluate(([x0, y0]) => {
    const w = __tt.world, N = 128;
    for (let y = y0 + 8; y < 124; y++) for (let x = 8; x < 100; x++) {
      let ok = true;
      for (let k = 0; k <= 12 && ok; k++) for (let d = -2; d <= 2 && ok; d++) ok = w.canFence((y + d) * N + x + k) && w.canGravel((y + d) * N + x + k) && w.wear[(y + d) * N + x + k] < 20 && Math.abs(x + k - w.map.riverX[y]) > 6;
      if (ok) return { x, y };
    }
    return null;
  }, [row.y, row.x]);
  {
    const f0 = (await stats()).fences;
    await page.evaluate(() => __tt.setTool("fence"));
    await twoFinger(await at(row2.x, row2.y), await at(row2.x + 12, row2.y), await at(row2.x + 4, row2.y - 14));
    const cells = await page.evaluate(() => { const w = __tt.world, out = []; for (let c = 0; c < 128 * 128; c++) if (w.fence[c]) out.push([c % 128, (c / 128) | 0]); return out; });
    const mine = cells.filter(([x, y]) => y === row2.y);
    check((await stats()).fences === f0 + 13 && mine.length === 13, `a second finger during a fence drag changes nothing: 13 cells on the dragged row (${(await stats()).fences - f0} placed)`);
    await page.evaluate(c => __tt.erase(c), mine);
  }
  {
    const g0 = (await stats()).gravel;
    await page.evaluate(() => __tt.setTool("gravel"));
    await twoFinger(await at(row2.x, row2.y + 2), await at(row2.x + 8, row2.y + 2), await at(row2.x + 4, row2.y - 14));
    const g = await page.evaluate(([y]) => { const w = __tt.world, out = []; for (let c = 0; c < 128 * 128; c++) if (w.gravel[c]) out.push((c / 128) | 0); return out; }, [row2.y]);
    const placed = (await stats()).gravel - g0;
    check(placed >= 7 && placed <= 9 && g.filter(y => Math.abs(y - (row2.y + 2)) > 1).length === g0, `a second finger during a gravel stroke changes nothing (${placed} cells, all on the stroke)`);
  }
  {
    await page.evaluate(() => __tt.setTool("bridge"));
    const rv = await page.evaluate(() => {
      const m = __tt.world.map;
      for (let y = 118; y > 10; y--) if (Math.abs(y - m.fordY) > 10 && __tt.world.bridgeCellsAt(m.riverX[y], y)) return { x: Math.floor(m.riverX[y]), y };
      return null;
    });
    const p = await at(rv.x, rv.y), b0 = (await stats()).bridges;
    await touch2("touchStart", [pt(p, fid.a)]);
    await page.waitForTimeout(100);
    await touch2("touchStart", [pt({ x: p.x - 80, y: p.y + 60 }, fid.b)]);
    await touch2("touchMove", [pt(p, fid.a), pt({ x: p.x - 60, y: p.y + 90 }, fid.b)]);
    await touch2("touchEnd", [pt({ x: p.x - 60, y: p.y + 90 }, fid.b)]);
    await page.waitForTimeout(100);
    const mid = (await stats()).bridges;
    await touch2("touchEnd", [pt(p, fid.a)]); await frames(page);
    check(mid === b0 && (await stats()).bridges === b0 + 1, `a second finger during a footbridge press neither builds nor cancels it (${mid - b0} while held, ${(await stats()).bridges - b0} after release)`);
    await page.evaluate(([x, y]) => __tt.erase([[x, y]]), [rv.x, rv.y]);
  }

  // A quick tap fences the tapped cell.
  const tapCell = { x: row.x + 10, y: row.y - 6 };
  await page.evaluate(() => __tt.setTool("fence"));
  const okTap = await page.evaluate(([x, y]) => __tt.world.canFence(y * 128 + x), [tapCell.x, tapCell.y]);
  await page.touchscreen.tap((await at(tapCell.x, tapCell.y)).x, (await at(tapCell.x, tapCell.y)).y); await frames(page);
  check(okTap && await page.evaluate(([x, y]) => __tt.world.fence[y * 128 + x] === 1, [tapCell.x, tapCell.y]) && (await stats()).fences === 22, "a tapped cell is the fenced cell");
  check((await page.textContent("#status")) === "Fence placed (1 cell).", `one cell is "1 cell" ("${await page.textContent("#status")}")`);

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

// ---------------------------------------------------------------- phone, 360 px
console.log("\nPHONE (360x740, touch, dark)");
{
  const ctx = await browser.newContext({ viewport: { width: 360, height: 740 }, deviceScaleFactor: 2, colorScheme: "dark", hasTouch: true, isMobile: true });
  const page = await ctx.newPage();
  const errors = [];
  page.on("pageerror", e => errors.push(String(e)));
  await page.goto(url);
  await page.waitForFunction(() => window.__tt);
  await page.waitForTimeout(800);
  const m = await page.evaluate(() => {
    const r = document.getElementById("map").getBoundingClientRect(), small = [], tiny = [];
    for (const el of document.querySelectorAll("header button, header input")) { const b = el.getBoundingClientRect(); if (b.height && (b.height < 40 || (el.tagName === "BUTTON" && b.width < 40))) small.push(`${el.id} ${Math.round(b.width)}x${Math.round(b.height)}`); }
    for (const el of document.querySelectorAll("button, dd, dt, .statusbar span, label")) if (el.getClientRects().length && parseFloat(getComputedStyle(el).fontSize) < 12) tiny.push(el.id || el.tagName);
    const tools = document.getElementById("tool-erase").getBoundingClientRect();
    return { iw: innerWidth, ih: innerHeight, sw: document.documentElement.scrollWidth, top: r.top, bottom: r.bottom, left: r.left, right: r.right, tools: tools.bottom, small, tiny };
  });
  check(m.iw === 360, `the page is laid out at 360 px (innerWidth ${m.iw})`);
  check(m.small.length === 0, `360 px: buttons at least 40 x 40 px${m.small.length ? ": " + m.small.join(", ") : ""}`);
  check(m.sw <= m.iw && m.left >= 16 && m.right <= m.iw - 16 + 0.5, `360 px: no sideways scroll, 16 px gutters (map ${m.left.toFixed(0)} to ${m.right.toFixed(0)} of ${m.iw})`);
  check(m.tools < m.top && m.bottom <= m.ih, `360 px: the tools and the whole map are on the first screen (map bottom ${Math.round(m.bottom)} of ${m.ih})`);
  check(m.tiny.length === 0, `360 px: no text under 12 px${m.tiny.length ? ": " + m.tiny.join(", ") : ""}`);
  await page.screenshot({ path: join(shots, "phone-360-dark.png") });
  check(errors.length === 0, `360 px: no errors${errors.length ? ": " + errors.join(" | ") : ""}`);
  await ctx.close();
}

// ---------------------------------------------------------------- sound
console.log("\nSOUND (1280x860)");
{
  const page = await browser.newPage({ viewport: { width: 1280, height: 860 }, deviceScaleFactor: 1, colorScheme: "light" });
  const errors = [];
  page.on("pageerror", e => errors.push(String(e)));
  page.on("console", m => { if (m.type() === "error" || m.type() === "warning") errors.push(`${m.type()}: ${m.text()}`); });
  // Count every AudioContext made, and the event being handled when it was made.
  await page.addInitScript(() => {
    window.__ac = [];
    for (const name of ["AudioContext", "webkitAudioContext"]) {
      const C = window[name];
      if (!C) continue;
      window[name] = class extends C { constructor(...a) { super(...a); window.__ac.push({ during: window.event ? window.event.type : "none", state: this.state }); } };
    }
    // Live Web Audio nodes: made by a create call, released by disconnect() with no arguments.
    const live = new Set();
    let made = 0;
    window.__nodes = () => ({ live: live.size, made });
    const P = (window.BaseAudioContext || window.AudioContext).prototype;
    for (const k of ["createGain", "createBufferSource", "createStereoPanner", "createBiquadFilter", "createOscillator", "createConvolver", "createDynamicsCompressor", "createWaveShaper"]) {
      const f = P[k];
      P[k] = function (...a) { const n = f.apply(this, a); live.add(n); made++; return n; };
    }
    const d = AudioNode.prototype.disconnect;
    AudioNode.prototype.disconnect = function (...a) { if (a.length === 0) live.delete(this); return d.apply(this, a); };
  });
  await page.goto(url);
  await page.waitForFunction(() => window.__tt);
  await page.evaluate(() => { __tt.setSpeed(8); });
  await page.waitForTimeout(3000);
  const off = await page.evaluate(() => ({ made: window.__ac.length, s: __tt.sound(), slider: !document.getElementById("volume").closest("[hidden]") }));
  check(off.made === 0 && !off.s.created && off.s.work === 0 && !off.slider, `with sound off there is no AudioContext and no audio work (${off.made} contexts, ${off.s.work} working frames, volume slider ${off.slider ? "shown" : "hidden"})`);
  const btn = await page.locator("#btn-sound").boundingBox();
  await page.mouse.click(btn.x + btn.width / 2, btn.y + btn.height / 2);
  const on = await page.evaluate(() => ({ ac: window.__ac.slice(), s: __tt.sound(), pressed: document.getElementById("btn-sound").getAttribute("aria-pressed"), slider: !document.getElementById("volume").closest("[hidden]") }));
  check(on.ac.length === 1 && on.ac[0].during === "click" && on.s.on && on.pressed === "true" && on.slider, `pressing Sound makes one AudioContext inside the click (${on.ac.map(a => a.during + "/" + a.state).join(", ")}), and the volume slider appears`);
  await page.waitForTimeout(400);
  check((await page.evaluate(() => __tt.sound().ctxState)) === "running", "the context is running");
  // A minute at 8x with tools used and a shower.
  await page.evaluate(() => { __tt.reset(1); __tt.setSpeed(8); __tt.play(); });
  await page.waitForTimeout(20000);
  await page.evaluate(() => __tt.startRain(2));
  await page.keyboard.press("f");
  const spot = await page.evaluate(() => {
    const w = __tt.world;
    for (let y = 100; y > 20; y--) for (let x = 10; x < 60; x++) { let ok = true; for (let k = 0; k < 6 && ok; k++) ok = w.canFence(y * 128 + x + k) && !w.fence[y * 128 + x + k]; if (ok) return { x, y }; }
    return null;
  });
  const a = await page.evaluate(([x, y]) => __tt.cellToClient(x, y), [spot.x, spot.y]), b = await page.evaluate(([x, y]) => __tt.cellToClient(x, y), [spot.x + 5, spot.y]);
  await page.mouse.move(a.x, a.y); await page.mouse.down(); await page.mouse.move(b.x, b.y, { steps: 5 }); await page.mouse.up();
  await page.keyboard.press("e");
  await page.mouse.click(a.x, a.y);
  await page.keyboard.press("t");
  await page.mouse.click(10 + (await page.locator("#map").boundingBox()).x, 10 + (await page.locator("#map").boundingBox()).y);
  await page.keyboard.press("Escape");
  await page.waitForTimeout(6000);
  await page.screenshot({ path: join(shots, "sound-on-rain-1280.png") });
  await page.waitForTimeout(34000);
  const run = await page.evaluate(() => ({ s: __tt.sound(), wx: __tt.weather(), day: Math.floor(__tt.world.time / 15) }));
  const st = run.s.stats;
  console.log(`  sound after a minute at 8x: ${st.beats} beats, ${st.steps} steps (at most ${st.maxBeatSteps} a beat), ${st.cues} other cues (${st.bells} bells, ${st.themeNotes} theme notes), at most ${st.maxAlive} voices at once, ${st.dropped} dropped; day ${run.day}`);
  check(st.beats >= 50 && st.steps > 100 && st.cues > 5 && st.bells >= 3 && st.themeNotes > 0, "footsteps, tool cues, bells and the theme were scheduled");
  check(st.maxBeatSteps <= 8 && st.maxAlive <= 34, `the voice budget held (at most ${st.maxBeatSteps} step voices a beat, limit 8; ${st.maxAlive} voices at once, limit 34)`);
  const nodes = await page.evaluate(() => window.__nodes());
  check(nodes.live <= 300 && nodes.made > 4 * nodes.live, `audio nodes are released when their sounds end (${nodes.live} live of ${nodes.made} made after a minute; npm run soak runs 10 minutes)`);
  await page.click("#btn-pause");
  await page.waitForTimeout(500);
  const paused = await page.evaluate(() => __tt.sound());
  check(paused.muted && paused.ctxState === "suspended", `sound pauses with the game (context ${paused.ctxState})`);
  await page.click("#btn-pause");
  await page.waitForTimeout(300);
  check((await page.evaluate(() => __tt.sound().ctxState)) === "running", "and resumes with it");
  await page.keyboard.press("m");
  await page.waitForTimeout(400);
  const muted = await page.evaluate(() => __tt.sound());
  check(!muted.on && muted.ctxState === "suspended", "key M turns sound off");
  // Days pass while it is off (8x, 7 s: about 4 days, past the bell's 6 s gap); switched back on at 1x,
  // it rings no bell for them. Only a dawn after it is back on may ring.
  await page.evaluate(() => __tt.setSpeed(8));
  const d0 = await page.evaluate(() => Math.floor(__tt.world.time / 15));
  await page.waitForTimeout(7000);
  await page.evaluate(() => __tt.setSpeed(1));
  const before = await page.evaluate(() => ({ bells: __tt.sound().stats.bells, day: Math.floor(__tt.world.time / 15) }));
  await page.keyboard.press("m");
  await page.waitForTimeout(1200);
  const after = await page.evaluate(() => ({ bells: __tt.sound().stats.bells, day: Math.floor(__tt.world.time / 15), on: __tt.sound().on }));
  check(after.on && before.day - d0 >= 2 && after.bells - before.bells <= after.day - before.day, `no stray bell when sound comes back on after ${before.day - d0} silent days (${after.bells - before.bells} bells in the next 1.2 s, ${after.day - before.day} dawns meanwhile)`);
  await page.keyboard.press("m");
  await page.reload();
  await page.waitForFunction(() => window.__tt);
  await page.waitForTimeout(1000);
  const again = await page.evaluate(() => ({ made: window.__ac.length, s: __tt.sound() }));
  check(again.made === 0 && !again.s.on, "after a reload sound is off again, with no AudioContext");
  check(errors.length === 0, `sound: no console errors or warnings${errors.length ? ": " + errors.join(" | ") : ""}`);
  await page.close();
}

// ---------------------------------------------------------------- rain, mud and new houses on screen
console.log("\nRAIN, MUD AND NEW HOUSES (1280x860, both themes)");
for (const scheme of ["light", "dark"]) {
  const page = await browser.newPage({ viewport: { width: 1280, height: 860 }, deviceScaleFactor: 1, colorScheme: scheme });
  const errors = [];
  page.on("pageerror", e => errors.push(String(e)));
  await page.goto(url);
  await page.waitForFunction(() => window.__tt);
  // Grow a town (ticks run directly, as playing would), then rain.
  const r = await page.evaluate(() => {
    __tt.reset(1); __tt.pause();
    const w = __tt.world;
    while (w.grown.length < 3) w.tick();
    const mapPix = () => { const c = document.getElementById("map"), d = c.getContext("2d").getImageData(0, 0, c.width, c.height).data; return d; };
    return { houses: w.grown.length, day: w.grown[w.grown.length - 1].day, before: Array.from(mapPix().subarray(0, 4)) };
  });
  await page.waitForTimeout(400);
  const houseText = await page.textContent("#hud-houses");
  check(houseText.includes(`Houses: ${r.houses + (await page.evaluate(() => __tt.world.houses.length - __tt.world.grown.length))}`), `${scheme}: the village panel lists the houses ("${houseText}")`);
  // The newest house is being raised or marked: pixels round it change over the next second.
  const ring = await page.evaluate(async () => {
    const w = __tt.world, h = w.grown[w.grown.length - 1], c = document.getElementById("map"), k = c.width / 128;
    const grab = () => Array.from(c.getContext("2d").getImageData(Math.max(0, (h.x - 6) * k), Math.max(0, (h.y - 6) * k), 15 * k, 15 * k).data);
    const a = grab(); await new Promise(r => setTimeout(r, 700)); const b = grab();
    let diff = 0; for (let i = 0; i < a.length; i += 4) if (Math.abs(a[i] - b[i]) + Math.abs(a[i + 1] - b[i + 1]) + Math.abs(a[i + 2] - b[i + 2]) > 30) diff++;
    return diff;
  });
  check(ring > 20, `${scheme}: a new house has a raising moment and a mark that moves (${ring} pixels changed near it while paused)`);
  // Every 257th pixel's red + green + blue, before and during the rain.
  const sample = () => page.evaluate(() => { const c = document.getElementById("map"), d = c.getContext("2d").getImageData(0, 0, c.width, c.height).data, out = []; for (let i = 0; i < d.length; i += 4 * 257) out.push(d[i] + d[i + 1] + d[i + 2]); return out; });
  await page.evaluate(() => __tt.stopRain(true)); // the grown town may be in a shower already
  await page.waitForTimeout(400);
  const dry = await sample();
  await page.evaluate(() => { __tt.play(); __tt.setSpeed(1); __tt.startRain(1); });
  await page.waitForTimeout(3500);
  const wet = await page.evaluate(() => ({ w: __tt.weather(), text: document.getElementById("hud-weather").textContent }));
  const wetPx = await sample();
  let darker = 0;
  const n = dry.length;
  for (let i = 0; i < n; i++) if (wetPx[i] < dry[i] - 6) darker++;
  check(wet.w.raining && wet.text.startsWith("Rain, day") && darker > n * 0.3, `${scheme}: rain shows: "${wet.text}", ${wet.w.mudCells} mud cells, ground darker on ${Math.round((100 * darker) / n)}% of samples`);
  await page.screenshot({ path: join(shots, `rain-1280-${scheme}.png`) });
  await page.evaluate(() => __tt.setOverlay(true));
  await page.waitForTimeout(400);
  check(await page.isVisible("#legend-heat"), `${scheme}: the wear overlay still works in rain`);
  await page.screenshot({ path: join(shots, `rain-overlay-1280-${scheme}.png`) });
  check(errors.length === 0, `${scheme}: no errors${errors.length ? ": " + errors.join(" | ") : ""}`);
  await page.close();
}

// ---------------------------------------------------------------- readouts, the dawn mark, drying mud
console.log("\nREADOUTS, DAWN MARK AND DRYING MUD (1280x860, both themes)");
for (const scheme of ["light", "dark"]) {
  const page = await browser.newPage({ viewport: { width: 1280, height: 860 }, deviceScaleFactor: 1, colorScheme: scheme });
  const errors = [];
  page.on("pageerror", e => errors.push(String(e)));
  await page.goto(url);
  await page.waitForFunction(() => window.__tt);
  // The dawn mark at its peak and through its fade: the day line's text colour stays put and
  // reads at 4.5 to 1 or better against whatever is behind it.
  const dawn = await page.evaluate(() => {
    const el = document.getElementById("hud-time");
    const rgba = c => { const m = c.match(/[\d.]+/g).map(Number); return [m[0], m[1], m[2], m.length > 3 ? m[3] : 1]; };
    const lum = ([r, g, b]) => { const f = v => { v /= 255; return v <= 0.03928 ? v / 12.92 : ((v + 0.055) / 1.055) ** 2.4; }; return 0.2126 * f(r) + 0.7152 * f(g) + 0.0722 * f(b); };
    const page = rgba(getComputedStyle(document.body).backgroundColor);
    el.classList.remove("dawn"); void el.offsetWidth; el.classList.add("dawn");
    const anim = el.getAnimations()[0];
    const out = [];
    for (const t of [0, 200, 400, 800, 1200, 1599]) {
      anim.pause(); anim.currentTime = t;
      const cs = getComputedStyle(el), fg = rgba(cs.color), bg = rgba(cs.backgroundColor);
      const mixed = [0, 1, 2].map(i => bg[i] * bg[3] + page[i] * (1 - bg[3]));
      const a = lum(fg), b = lum(mixed);
      out.push({ t, ratio: (Math.max(a, b) + 0.05) / (Math.min(a, b) + 0.05), fg: cs.color, bg: cs.backgroundColor, line: cs.textDecorationColor });
    }
    return { found: !!anim, out };
  });
  const worst = Math.min(...dawn.out.map(o => o.ratio)), fgFixed = new Set(dawn.out.map(o => o.fg)).size === 1;
  check(dawn.found && worst >= 4.5 && fgFixed, `${scheme}: the dawn mark keeps the day line readable (lowest contrast ${worst.toFixed(2)} to 1 over the fade, ${dawn.out[0].ratio.toFixed(2)} at the peak; text colour ${fgFixed ? "fixed" : "changes"}; tint ${dawn.out[0].bg}, underline ${dawn.out[0].line})`);
  // The town readout's two parts stay apart in copied text.
  await page.evaluate(() => { __tt.reset(1); __tt.pause(); const w = __tt.world; while (w.grown.length < 2) w.tick(); });
  await page.waitForTimeout(700);
  const town = await page.evaluate(() => ({ inner: document.getElementById("hud-houses").innerText, text: document.getElementById("hud-houses").textContent }));
  check(/^Houses: \d+;\s*newest on day \d+$/.test(town.text) && !/\dnewest/.test(town.inner), `the town readout reads "${town.text}" (innerText ${JSON.stringify(town.inner)})`);
  // A footbridge that saves under 5% of the walk (seed 1, row 88, day 10: about 2%) says so.
  await page.evaluate(() => { __tt.reset(1); __tt.pause(); const w = __tt.world; for (let i = 0; i < 1500; i++) w.tick(); });
  await page.keyboard.press("b");
  const rx = await page.evaluate(() => Math.floor(__tt.world.map.riverX[88]));
  const at = await page.evaluate(([x, y]) => __tt.cellToClient(x, y), [rx, 88]);
  await page.mouse.move(at.x - 3, at.y); await page.mouse.move(at.x, at.y);
  await page.waitForFunction(() => __tt.previewState().text !== "", null, { timeout: 5000 }).catch(() => {});
  const bridge = await page.evaluate(() => __tt.previewState());
  check(bridge.text.startsWith("Saves under 5% of the walk") && bridge.line.startsWith("Saves under 5%") && bridge.tipVisible, `a footbridge that saves little says so: "${bridge.text}" (status line and pointer)`);
  await page.screenshot({ path: join(shots, `bridge-small-saving-1280-${scheme}.png`) });
  await page.keyboard.press("Escape");
  // Drying: a third of the way through the mud's drying window (the simulation's own: the last
  // 7.5 s of the mud, while the wetness that slows walkers falls) the least-worn mud is drawn dry
  // again while the busiest is still wet (drawing only). Compared with the same cells once the
  // ground is dry. (Before round 6 this was halfway through the mud's last full day.)
  await page.evaluate(() => { const w = __tt.world; while (w.time < 600) w.tick(); __tt.startRain(1); while (w.weather.raining) w.tick(); while (w.time < w.weather.mudUntil - 5) w.tick(); });
  await page.waitForTimeout(600);
  const grab = () => page.evaluate(() => {
    const w = __tt.world, c = document.getElementById("map"), k = c.width / 128, g = c.getContext("2d");
    const light = [], heavy = [];
    for (let i = 0; i < 128 * 128; i++) if (w.mud[i] && !w.walkers.some(v => Math.abs((v.cell % 128) - (i % 128)) + Math.abs(((v.cell / 128) | 0) - ((i / 128) | 0)) < 3)) {
      if (w.wear[i] < 32 && light.length < 40) light.push(i); else if (w.wear[i] >= 85 && heavy.length < 40) heavy.push(i);
    }
    const px = cells => cells.map(i => { const d = g.getImageData(((i % 128) + 0.5) * k, (((i / 128) | 0) + 0.5) * k, 1, 1).data; return d[0] + d[1] + d[2]; });
    return { light, heavy, l: px(light), h: px(heavy), dp: 1 - (w.weather.mudUntil - w.time) / 7.5, wet: w.weather.wetness };
  });
  const mid = await grab();
  await page.screenshot({ path: join(shots, `drying-1280-${scheme}.png`) });
  await page.evaluate(() => __tt.stopRain(true));
  await page.waitForTimeout(600);
  const dryPx = await page.evaluate(({ light, heavy }) => {
    const c = document.getElementById("map"), k = c.width / 128, g = c.getContext("2d");
    const px = cells => cells.map(i => { const d = g.getImageData(((i % 128) + 0.5) * k, (((i / 128) | 0) + 0.5) * k, 1, 1).data; return d[0] + d[1] + d[2]; });
    return { l: px(light), h: px(heavy) };
  }, { light: mid.light, heavy: mid.heavy });
  const avg = a => a.reduce((x, y) => x + y, 0) / Math.max(1, a.length);
  const dl = avg(dryPx.l) - avg(mid.l), dh = avg(dryPx.h) - avg(mid.h);
  check(mid.light.length >= 10 && mid.heavy.length >= 10 && dh > dl + 15, `${scheme}: mud dries from the least-worn cells first (${(mid.dp * 100).toFixed(0)}% through the drying window, wetness ${mid.wet.toFixed(2)}: light trail ${dl.toFixed(0)} below its dry brightness, busy track ${dh.toFixed(0)} below; ${mid.light.length} and ${mid.heavy.length} cells)`);
  check(errors.length === 0, `${scheme}: no errors${errors.length ? ": " + errors.join(" | ") : ""}`);
  await page.close();
}

// ---------------------------------------------------------------- round 6 fixes: readouts, dawn mark, labels
console.log("\nRESET, JUMPS, THE DAWN MARK AND THE PLACE LABELS (round 6 fixes)");
{
  const page = await browser.newPage({ viewport: { width: 1280, height: 860 }, deviceScaleFactor: 1, colorScheme: "light" });
  const errors = [];
  page.on("pageerror", e => errors.push(String(e)));
  await page.goto(url);
  await page.waitForFunction(() => window.__tt);
  await page.evaluate(() => { __tt.reset(1); __tt.pause(); });
  // The time and weather lines follow Reset, New map and a debug jump at once, in the same task.
  const jump = await page.evaluate(() => {
    const t = () => document.getElementById("hud-time").textContent, wx = () => document.getElementById("hud-weather").textContent;
    __tt.advance(6 * 15 + 1);
    const day7 = t();
    __tt.startRain(1);
    const rain = wx();
    __tt.stopRain(true);
    const dry = wx();
    document.getElementById("btn-reset").click();
    const reset = t();
    __tt.advance(3 * 15 + 1);
    document.getElementById("btn-newmap").click();
    const fresh = t();
    return { day7, rain, dry, reset, fresh };
  });
  check(jump.day7.startsWith("Day 7,") && jump.rain.startsWith("Rain, day 7") && jump.dry === "Dry" && jump.reset.startsWith("Day 1,") && jump.fresh.startsWith("Day 1,"),
    `the time and weather lines update at once after a debug jump ("${jump.day7}"), rain ("${jump.rain}") and its end ("${jump.dry}"), Reset ("${jump.reset}") and New map ("${jump.fresh}")`);
  // Ticks run straight on the world (a jump with no refresh call): the next frame shows them.
  await page.evaluate(() => { const w = __tt.world; while (w.time < 2 * 15 + 1) w.tick(); });
  await frames(page);
  check((await page.textContent("#hud-time")).startsWith("Day 3,"), `a jump of the clock shows on the next frame ("${await page.textContent("#hud-time")}")`);
  // The dawn mark after Reset: day 9 is reached, the map is reset, and its very next dawn is marked.
  const dawn = await page.evaluate(async () => {
    const el = document.getElementById("hud-time"), next = () => new Promise(r => requestAnimationFrame(() => requestAnimationFrame(r)));
    __tt.reset(1); __tt.pause();
    __tt.advance(8 * 15 + 1);
    await new Promise(r => setTimeout(r, 1800)); // the mark of day 9 has ended
    document.getElementById("btn-reset").click();
    const afterReset = el.classList.contains("dawn");
    await next();
    __tt.advance(15 + 1);
    await next();
    const marked = el.classList.contains("dawn") && el.getAnimations().length > 0;
    document.getElementById("btn-newmap").click();
    const afterNew = el.classList.contains("dawn");
    __tt.advance(15 + 1);
    await next();
    return { afterReset, marked, afterNew, markedNew: el.classList.contains("dawn") && el.getAnimations().length > 0, text: el.textContent };
  });
  check(!dawn.afterReset && dawn.marked && !dawn.afterNew && dawn.markedNew, `Reset and New map clear the dawn mark's day count: the first dawn after each is marked (after Reset ${dawn.marked ? "marked" : "not marked"}, after New map ${dawn.markedNew ? "marked" : "not marked"}; "${dawn.text}")`);
  check(errors.length === 0, `round 6 readouts: no errors${errors.length ? ": " + errors.join(" | ") : ""}`);
  await page.close();
}
{
  // Reduced motion: the dawn underline shows without a fade and ends after a fixed time, even while paused.
  const page = await browser.newPage({ viewport: { width: 1280, height: 860 }, deviceScaleFactor: 1, colorScheme: "light", reducedMotion: "reduce" });
  await page.goto(url);
  await page.waitForFunction(() => window.__tt);
  const rm = await page.evaluate(async () => {
    const el = document.getElementById("hud-time"), line = () => getComputedStyle(el).textDecorationColor;
    __tt.reset(1); __tt.pause();
    await new Promise(r => requestAnimationFrame(() => requestAnimationFrame(r)));
    const before = line();
    __tt.advance(15 + 1);
    await new Promise(r => requestAnimationFrame(r));
    const on = line(), bg = getComputedStyle(el).backgroundColor;
    await new Promise(r => setTimeout(r, 2200));
    return { reduce: matchMedia("(prefers-reduced-motion: reduce)").matches, before, on, bg, after: line(), anims: el.getAnimations().length, paused: __tt.stats().paused };
  });
  const clear = c => /rgba\(\d+, \d+, \d+, 0\)|transparent/.test(c);
  check(rm.reduce && rm.paused && clear(rm.before) && !clear(rm.on) && clear(rm.bg) && clear(rm.after) && rm.anims === 0,
    `reduced motion: the dawn underline shows (${rm.on}, no tint) and is gone 2.2 s later while paused (${rm.after})`);
  await page.close();
}
{
  // At 360 px the place labels are drawn over the walkers: with walkers crowded onto the "Ford"
  // label, the pixels of its letters keep the label colour.
  const ctx = await browser.newContext({ viewport: { width: 360, height: 740 }, deviceScaleFactor: 2, colorScheme: "dark", hasTouch: true, isMobile: true });
  const page = await ctx.newPage();
  await page.goto(url);
  await page.waitForFunction(() => window.__tt);
  await page.evaluate(() => { __tt.reset(1); __tt.pause(); __tt.advance(3 * 15); });
  await page.waitForTimeout(500);
  const box = await page.evaluate(() => {
    const r = __tt.renderer, l = r.labels.find(x => x.text === "Ford"), c = document.getElementById("map");
    const hex = r.pal.css.label.trim(), m = hex.match(/^#([0-9a-f]{2})([0-9a-f]{2})([0-9a-f]{2})$/i);
    const col = m ? [1, 2, 3].map(i => parseInt(m[i], 16)) : hex.match(/\d+/g).slice(0, 3).map(Number);
    return { x: l.x, y: l.y, px: l.px, s: c.width / 128, col, x0: Math.round(l.x - 1.6 * l.px), y0: Math.round(l.y - 0.7 * l.px), w: Math.round(3.2 * l.px), h: Math.round(1.4 * l.px) };
  });
  const grab = () => page.evaluate(b => Array.from(document.getElementById("map").getContext("2d").getImageData(b.x0, b.y0, b.w, b.h).data), box);
  const clearOf = await grab();
  const moved = await page.evaluate(b => {
    const w = __tt.world;
    let k = 0;
    for (const v of w.walkers) {
      if (!w.isVisible(v)) continue;
      const fx = (k % 8) / 7 - 0.5, fy = ((k >> 3) % 4) / 3 - 0.5;
      v.x = v.px = (b.x + fx * 2.6 * b.px) / b.s; v.y = v.py = (b.y + fy * 1.1 * b.px) / b.s;
      k++;
    }
    return k;
  }, box);
  await frames(page);
  const covered = await grab();
  await page.screenshot({ path: join(shots, "labels-over-walkers-360.png") });
  // The letters: pixels within 12 of the label colour with no walker near; how many keep it with walkers on them.
  const near = (d, i) => Math.abs(d[i] - box.col[0]) + Math.abs(d[i + 1] - box.col[1]) + Math.abs(d[i + 2] - box.col[2]) <= 12;
  let letters = 0, kept = 0, changed = 0;
  for (let i = 0; i < clearOf.length; i += 4) {
    if (Math.abs(clearOf[i] - covered[i]) + Math.abs(clearOf[i + 1] - covered[i + 1]) + Math.abs(clearOf[i + 2] - covered[i + 2]) > 30) changed++;
    if (near(clearOf, i)) { letters++; if (near(covered, i)) kept++; }
  }
  check(letters > 20 && moved >= 32 && changed > 100 && kept >= 0.95 * letters, `360 px: the "Ford" label is drawn over the walkers (${kept} of ${letters} letter pixels keep the label colour with ${moved} walkers on the label; ${changed} pixels round them changed)`);
  await ctx.close();
}

// ---------------------------------------------------------------- the sound engine offline (round 6 fixes)
console.log("\nSOUND ENGINE OFFLINE (src/ui/audio.ts in an OfflineAudioContext; round 6 fixes)");
{
  const bundle = await esbuild.build({ entryPoints: [join(here, "..", "src", "ui", "audio.ts")], bundle: true, format: "iife", globalName: "DA", write: false, target: "es2020" });
  const page = await browser.newPage();
  const errors = [];
  page.on("pageerror", e => errors.push(String(e)));
  page.on("console", m => { if (m.type() === "error" || m.type() === "warning") errors.push(`${m.type()}: ${m.text()}`); });
  await page.setContent("<p>engine</p>");
  await page.addScriptTag({ content: bundle.outputFiles[0].text });
  // The step budget's shares: 8 areas of 6 walkers each want 16 steps a beat, twice the budget of 8.
  // Each area must get its share (an eighth of the steps), whatever its place in the list.
  const fair = await page.evaluate(async () => {
    const ctx = new OfflineAudioContext(2, 24000 * 160, 24000);
    const au = DA.create(ctx, { volume: 0.7, bpm: 240 });
    const areas = Array.from({ length: 8 }, (_, i) => ({ pan: ((i % 4) + 0.5) / 4 * 1.6 - 0.8, walkers: 6, surface: "dirt", ford: 0 }));
    for (let t = 0.05; t < 159.9; t += 0.05) { const at = t; ctx.suspend(at).then(() => { au.beat(areas, 8); ctx.resume(); }); }
    au.beat(areas, 8);
    await ctx.startRendering();
    const s = au.stats();
    return { per: s.areaSteps.slice(0, 8), beats: s.beats, steps: s.steps, maxBeatSteps: s.maxBeatSteps, dropped: s.dropped };
  });
  const meanSteps = fair.per.reduce((a, b) => a + b, 0) / 8, least = Math.min(...fair.per);
  console.log(`  ${fair.beats} beats, ${fair.steps} steps (at most ${fair.maxBeatSteps} a beat, ${fair.dropped} dropped by the budget); steps by area: ${fair.per.join(", ")}`);
  check(fair.beats >= 500 && fair.maxBeatSteps <= 8 && fair.dropped > 100 && least >= 0.85 * meanSteps,
    `when the step budget is hit every area gets its share: the least-served area has ${((100 * least) / meanSteps).toFixed(0)}% of the mean (at least 85%)`);
  // Mute and a hidden tab: nothing scheduled before either may sound after it.
  const mute = await page.evaluate(async () => {
    const all = new Set();
    let phase = 1;
    for (const P of [AudioScheduledSourceNode.prototype, AudioBufferSourceNode.prototype]) {
      if (Object.prototype.hasOwnProperty.call(P, "start")) { const f = P.start; P.start = function (when = 0, ...a) { this.__t0 = when; this.__phase = phase; all.add(this); return f.call(this, when, ...a); }; }
      if (Object.prototype.hasOwnProperty.call(P, "stop")) { const f = P.stop; P.stop = function (when = 0) { this.__stop = when; return f.call(this, when); }; }
    }
    const d0 = AudioNode.prototype.disconnect;
    AudioNode.prototype.disconnect = function (...a) { if (a.length === 0) this.__released = true; return d0.apply(this, a); };
    const ctx = new OfflineAudioContext(2, 48000 * 20, 48000);
    const au = DA.create(ctx, { volume: 0.7 });
    const areas = Array.from({ length: 8 }, (_, i) => ({ pan: ((i % 4) + 0.5) / 4 * 1.6 - 0.8, walkers: 6, surface: "dirt", ford: i === 2 ? 3 : 0 }));
    au.river(0.5); au.theme(true);
    const T1 = 10, T2 = 15;
    let live = true, future1 = 0, future2 = 0, notes1 = 0;
    const end = s => {
      const nat = s instanceof AudioBufferSourceNode && !s.loop ? s.__t0 + s.buffer.duration / s.playbackRate.value : Infinity;
      return Math.min(s.__stop ?? Infinity, nat);
    };
    const ahead = (ph, t) => [...all].filter(s => s.__phase === ph && s.__t0 > t).length;
    for (let t = 0.05; t < 19.9; t += 0.05) {
      const at = Math.round(t * 100) / 100;
      ctx.suspend(at).then(() => {
        if (at === T1) { au.beat(areas, 8); future1 = ahead(1, T1 + 0.12); notes1 = au.stats().themeNotes; au.setMuted(true); live = false; phase = 2; }
        else if (at === T1 + 0.3) { au.setMuted(false); live = true; }
        else if (at === T2) { au.beat(areas, 8); future2 = ahead(2, T2); au.hidden(true); live = false; phase = 3; }
        else if (at === T2 + 0.3) { au.hidden(false); live = true; }
        if (live) au.beat(areas, 8);
        ctx.resume();
      });
    }
    au.beat(areas, 8);
    await ctx.startRendering();
    await new Promise(r => setTimeout(r, 300));
    const src = [...all].filter(s => !(s.loop && s.__stop === undefined)); // not the river bed
    const late1 = src.filter(s => s.__phase === 1 && end(s) > T1 + 0.2 && end(s) > s.__t0).length;
    const late2 = src.filter(s => s.__phase === 2 && end(s) > T2 + 0.01 && end(s) > s.__t0).length;
    const unreleased = src.filter(s => s.__phase < 3 && !s.__released && end(s) < 19).length;
    const s = au.stats();
    return { future1, future2, late1, late2, unreleased, cancelled: s.cancelled, notesBefore: notes1, notesAfter: s.themeNotes - notes1, stepsAfter: src.filter(x => x.__phase === 3).length, sources: src.length };
  });
  console.log(`  ${mute.sources} sources; at the mute ${mute.future1} were scheduled ahead, at the hidden tab ${mute.future2}; ${mute.cancelled} dropped; sounding after: ${mute.late1} after the mute, ${mute.late2} after the hidden tab; ${mute.notesAfter} theme notes after the mute (${mute.notesBefore} before)`);
  check(mute.future1 > 0 && mute.future2 > 0 && mute.late1 === 0 && mute.late2 === 0 && mute.cancelled >= mute.future1 + mute.future2,
    "theme notes and footsteps scheduled before a mute or a hidden tab never sound after it (they are stopped or dropped)");
  check(mute.notesAfter > 0 && mute.stepsAfter > 0 && mute.unreleased === 0, `sound carries on once it is back (theme notes and steps scheduled again), and every dropped source is released (${mute.unreleased} not released)`);
  check(errors.length === 0, `sound engine offline: no errors${errors.length ? ": " + errors.join(" | ") : ""}`);
  await page.close();
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
