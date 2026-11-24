// npm run smoke: the built page loads with no console errors, the map is not
// blank, every control works, the page contract holds, and the layout fits at
// 400 px wide in both themes. Screenshots go to test-results/.
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
  check((await stats()).fences === f0.fences && !(await page.evaluate(() => __tt.previewState().tipVisible)), "Escape cancels the drag and hides the preview");
  await page.keyboard.press("p");
  check((await pressed("btn-preview")) === "false", "key P turns the flow preview off");
  await page.click("#btn-preview");
  check((await pressed("btn-preview")) === "true", "the Flow preview button turns it back on");
  await page.keyboard.press("f");

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

  // Erase both.
  await page.click("#tool-erase");
  check((await pressed("tool-erase")) === "true", "Erase tool toggles on");
  await page.mouse.move(from.x, from.y); await page.mouse.down(); await page.mouse.move(to.x, to.y, { steps: 12 }); await page.mouse.up();
  await page.mouse.click(river.x, river.y);
  const se = await stats();
  check(se.fences === 0 && se.bridges === 0, `erasing removes the fence and the bridge (${se.fences} fence cells, ${se.bridges} bridges left)`);
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
