"""The night-3 review page's style and script (used by build_review.py). No external fonts, scripts or styles."""

CSS = r"""
/* A ledger of numbered plates: before and after side by side, the question under them, Yes and No at hand.
   Colours are the game's own palette: paper, ink, leather, gilt, river, meadow, brick. */
:root {
  --paper: #F2EAD8; --page: #FBF6EA; --ink: #2B2620; --faded: #6B6257; --rule: #D6CBB3;
  --leather: #4A241A; --gilt: #8A6620; --river: #2F5670; --meadow: #4F7A3A; --brick: #A4553A;
  --yes-bg: #DDE9D2; --no-bg: #F1D9CF; --chip: #EAE0C9; --shade: rgba(20, 14, 10, .88);
  --display: "Iowan Old Style", "Palatino Linotype", Palatino, Georgia, "Times New Roman", serif;
  --body: system-ui, -apple-system, "Segoe UI", Roboto, "Helvetica Neue", Arial, sans-serif;
  color-scheme: light;
}
@media (prefers-color-scheme: dark) { :root:not([data-theme="light"]) {
  --paper: #1E1612; --page: #2A1F19; --ink: #EFE6D3; --faded: #B5A892; --rule: #4A3A2E;
  --leather: #E7C98A; --gilt: #D9B45A; --river: #9CC0D6; --meadow: #A9C98F; --brick: #E09A80;
  --yes-bg: #2F4226; --no-bg: #4B2A20; --chip: #3A2C23; --shade: rgba(0, 0, 0, .9); color-scheme: dark } }
:root[data-theme="dark"] {
  --paper: #1E1612; --page: #2A1F19; --ink: #EFE6D3; --faded: #B5A892; --rule: #4A3A2E;
  --leather: #E7C98A; --gilt: #D9B45A; --river: #9CC0D6; --meadow: #A9C98F; --brick: #E09A80;
  --yes-bg: #2F4226; --no-bg: #4B2A20; --chip: #3A2C23; --shade: rgba(0, 0, 0, .9); color-scheme: dark }

html { -webkit-text-size-adjust: 100%; }
body { margin: 0; background: var(--paper); color: var(--ink); font-family: var(--body); font-size: 17px; line-height: 1.5;
  overflow-x: hidden; }
.wrap { max-width: 1240px; margin: 0 auto; padding-block: 28px 64px;
  padding-left: max(16px, env(safe-area-inset-left, 0px)); padding-right: max(16px, env(safe-area-inset-right, 0px));
  display: flex; flex-direction: column; gap: 36px; }
h1, h2, h3 { font-family: var(--display); font-weight: 700; line-height: 1.15; text-wrap: balance; margin: 0; }
h1 { font-size: clamp(1.9rem, 5vw, 2.9rem); color: var(--leather); }
h2 { font-size: 1.65rem; padding-bottom: 6px; border-bottom: 2px solid var(--rule); }
h3 { font-size: 1.15rem; }
p { margin: 0; max-width: 72ch; }
a { color: var(--river); overflow-wrap: anywhere; }
code { font-size: .9em; overflow-wrap: anywhere; }
:focus-visible { outline: 3px solid var(--river); outline-offset: 2px; }
.lede { display: flex; flex-direction: column; gap: 12px; }
.eyebrow { font-size: .8rem; letter-spacing: .12em; text-transform: uppercase; color: var(--faded); }
.facts, .toc { display: flex; flex-wrap: wrap; gap: 8px 10px; padding: 0; margin: 0; list-style: none; }
.facts li { background: var(--chip); border-radius: 4px; padding: 3px 10px; font-size: .92rem; }
.toc { gap: 6px 16px; font-size: .95rem; }

.bar { position: sticky; top: 0; z-index: 5; background: var(--page); border: 1px solid var(--rule); border-radius: 6px;
  padding: 8px 12px; display: flex; flex-wrap: wrap; align-items: center; gap: 8px 14px; }
.bar .count { font-variant-numeric: tabular-nums; font-weight: 700; }
.bar .grow { flex: 1 1 16ch; min-width: 0; }
button { font: inherit; color: var(--ink); background: var(--chip); border: 1px solid var(--rule); border-radius: 4px;
  padding: 6px 14px; cursor: pointer; min-height: 36px; }
button:hover { border-color: var(--faded); }
.copyout { box-sizing: border-box; width: 100%; min-height: 7em; font: .9rem/1.4 ui-monospace, Menlo, Consolas, monospace;
  background: var(--page); color: var(--ink); border: 1px solid var(--rule); border-radius: 4px; padding: 8px; }
.hint { color: var(--faded); font-size: .9rem; }

section { display: flex; flex-direction: column; gap: 22px; min-width: 0; }
.plate { display: flex; flex-direction: column; gap: 12px; background: var(--page); border: 1px solid var(--rule);
  border-radius: 6px; padding: 12px; min-width: 0; }
.pair { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 12px; }
.side { display: flex; flex-direction: column; gap: 8px; min-width: 0; }
.side > .figs { display: grid; grid-template-columns: repeat(auto-fit, minmax(min(100%, 240px), 1fr)); gap: 10px; }
.side > .tag { font-family: var(--display); font-weight: 700; font-size: 1.05rem; }
.side.after > .tag { color: var(--meadow); }
.row { display: grid; grid-template-columns: repeat(auto-fit, minmax(min(100%, 300px), 1fr)); gap: 12px; }
.row.wide { grid-template-columns: minmax(0, 1fr); }
figure { margin: 0; display: flex; flex-direction: column; gap: 4px; min-width: 0; }
figcaption { font-size: .85rem; color: var(--faded); }
figcaption .stand { color: var(--brick); }
.shot { display: block; padding: 0; border: 1px solid var(--ink); background: var(--ink); line-height: 0; cursor: zoom-in;
  width: 100%; min-height: 0; border-radius: 0; }
.shot img { display: block; width: 100%; height: auto; }
.cap { display: grid; grid-template-columns: auto minmax(0, 1fr); gap: 4px 14px; align-items: baseline; }
.num { font-family: var(--display); font-weight: 700; font-size: 1.6rem; color: var(--gilt); font-variant-numeric: tabular-nums;
  min-width: 2.2ch; text-align: right; }
.shows { min-width: 0; }
.ask { grid-column: 2; min-width: 0; display: flex; flex-wrap: wrap; align-items: center; gap: 8px 14px; }
.q { font-family: var(--display); font-size: 1.15rem; font-weight: 700; flex: 1 1 22ch; min-width: 0; }
.yn { display: inline-flex; gap: 6px; }
.yn button { min-width: 64px; }
.yn button[aria-pressed="true"].yes { background: var(--yes-bg); border-color: var(--meadow); font-weight: 700; }
.yn button[aria-pressed="true"].no { background: var(--no-bg); border-color: var(--brick); font-weight: 700; }

.notes { display: flex; flex-direction: column; gap: 14px; }
.notes ul, .notes ol { margin: 0; padding-left: 1.3em; display: flex; flex-direction: column; gap: 6px; max-width: 80ch; }
.two { display: grid; grid-template-columns: repeat(auto-fit, minmax(min(100%, 300px), 1fr)); gap: 22px; }
.two > div { min-width: 0; display: flex; flex-direction: column; gap: 10px; }
.tablewrap { overflow-x: auto; }
table { border-collapse: collapse; width: 100%; font-size: .95rem; }
th, td { text-align: left; vertical-align: top; padding: 7px 10px; border-bottom: 1px solid var(--rule); }
th { font-family: var(--display); font-size: 1rem; }
td.n { font-variant-numeric: tabular-nums; color: var(--faded); white-space: nowrap; width: 3ch; }
.tunes { display: flex; flex-direction: column; gap: 12px; padding: 0; margin: 0; list-style: none; }
.tunes li { display: grid; grid-template-columns: minmax(0, 1fr) minmax(0, 320px); gap: 6px 16px; align-items: center;
  border-bottom: 1px solid var(--rule); padding-bottom: 10px; }
.tunes audio { width: 100%; }
.tunes small { color: var(--faded); }

.zoom { position: fixed; inset: 0; z-index: 20; background: var(--shade); overflow: auto; display: none; cursor: zoom-out;
  -webkit-overflow-scrolling: touch; }
.zoom.open { display: block; }
.zoom img { display: block; max-width: none; margin: 0 auto; }
.zoom .close { position: fixed; top: 10px; right: 10px; z-index: 21; }

@media (max-width: 700px) {
  body { font-size: 16px; }
  .pair { grid-template-columns: minmax(0, 1fr); }
  .bar { padding: 6px 10px; gap: 6px 10px; }
  .bar .grow { display: none; }
  .bar button { padding: 4px 8px; font-size: .9rem; }
  .bar .count { font-size: .9rem; }
  .bar .wd { display: none; }
  .tunes li, .cap { grid-template-columns: minmax(0, 1fr); }
  .ask { grid-column: 1; }
  .num { text-align: left; }
}
@media (prefers-reduced-motion: no-preference) { button { transition: border-color .12s; } }
"""

JS = r"""
(function () {
  var KEY = 'tide-toll-review-n3-answers-v1';
  var answers = {};
  try { answers = JSON.parse(localStorage.getItem(KEY) || '{}') || {}; } catch (e) { answers = {}; }
  var plates = Array.prototype.slice.call(document.querySelectorAll('.plate'));
  var count = document.getElementById('count');
  var out = document.getElementById('copyout');
  var status = document.getElementById('copystatus');
  function save() { try { localStorage.setItem(KEY, JSON.stringify(answers)); } catch (e) {} }
  function paint() {
    var n = 0;
    plates.forEach(function (p) {
      var id = p.getAttribute('data-id'), a = answers[id];
      if (a) n++;
      p.querySelector('button.yes').setAttribute('aria-pressed', a === 'yes' ? 'true' : 'false');
      p.querySelector('button.no').setAttribute('aria-pressed', a === 'no' ? 'true' : 'false');
    });
    count.innerHTML = n + ' of ' + plates.length + '<span class="wd"> answered</span>';
  }
  function text() {
    var lines = [];
    plates.forEach(function (p) { var id = p.getAttribute('data-id'); if (answers[id]) lines.push(p.getAttribute('data-n') + ' ' + answers[id]); });
    return lines.length ? lines.join('\n') + '\ncontinue' : '';
  }
  plates.forEach(function (p) {
    var id = p.getAttribute('data-id');
    ['yes', 'no'].forEach(function (v) {
      p.querySelector('button.' + v).addEventListener('click', function () {
        if (answers[id] === v) { delete answers[id]; } else { answers[id] = v; }
        save(); paint();
      });
    });
  });
  var zoom = document.getElementById('zoom'), zimg = document.getElementById('zoomimg'), last = null;
  function closeZoom() { zoom.classList.remove('open'); zimg.removeAttribute('src'); if (last) { last.focus(); last = null; } }
  Array.prototype.forEach.call(document.querySelectorAll('.shot'), function (b) {
    b.addEventListener('click', function () {
      last = b; zimg.src = b.getAttribute('data-full'); zimg.alt = b.querySelector('img').alt;
      zoom.classList.add('open'); zoom.scrollTop = 0; zoom.scrollLeft = 0; document.getElementById('zoomclose').focus();
    });
  });
  zoom.addEventListener('click', closeZoom);
  document.addEventListener('keydown', function (e) { if (e.key === 'Escape' && zoom.classList.contains('open')) closeZoom(); });
  document.getElementById('copy').addEventListener('click', function () {
    var t = text();
    if (!t) { status.textContent = 'Nothing answered yet.'; return; }
    out.hidden = false; out.value = t;
    function fallback() { out.focus(); out.select(); status.textContent = 'Select the text below and copy it.'; }
    try {
      if (navigator.clipboard && navigator.clipboard.writeText) {
        navigator.clipboard.writeText(t).then(function () { status.textContent = 'Copied. Paste it into the chat.'; }, fallback);
      } else { fallback(); }
    } catch (e) { fallback(); }
  });
  document.getElementById('clear').addEventListener('click', function () {
    if (!window.confirm || window.confirm('Clear all your answers?')) { answers = {}; save(); paint(); out.hidden = true; status.textContent = 'Cleared.'; }
  });
  paint();
})();
"""
