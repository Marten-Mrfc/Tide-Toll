# python3 docs/asset-gallery/build.py  ->  docs/asset-gallery/index.html, media/, files.json
# Builds the owner's review page for the Hamlet asset set from what is on disk: the ledger (source, licence),
# the .glb files (triangles, bounding box), the audio files (length, loudness, peak) and the saved previews.
# Run tools/build-assets.sh first so the spectrograms in build/audio-previews/ are current. Needs ffmpeg.
import csv
import html
import json
import shutil
import struct
import subprocess
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "docs" / "asset-gallery"
MEDIA = OUT / "media"
sys.path.insert(0, str(ROOT / "assets" / "src"))
from common import audio as A  # noqa: E402

E = html.escape
LEDGER = {r["item"]: r for r in csv.DictReader((ROOT / "assets" / "LEDGER.csv").open(encoding="utf-8"))}
FILES = {}


def put(src, dst):
    """Copy repo file src (relative path) to media/dst and record it for files.json."""
    s = ROOT / src
    d = MEDIA / dst
    d.parent.mkdir(parents=True, exist_ok=True)
    shutil.copyfile(s, d)
    FILES[f"media/{dst}"] = d.relative_to(ROOT).as_posix()
    return f"media/{dst}"


def ledger_facts(item):
    r = LEDGER[item]
    src = r["source"]
    return (f'<dl class="facts"><dt>Source</dt><dd>{E(src)}</dd><dt>Licence</dt><dd>{E(r["licence"])}'
            + (f' (<a href="{E(r["url"])}">{E(r["url"].replace("https://", ""))}</a>)' if r["url"] else "")
            + f'</dd><dt>Used in</dt><dd>{E(r["used_in"])}</dd><dt>Verified</dt><dd>{E(r["verified"])}, '
            f'{E(r["date_checked"])}</dd></dl>')


def glb_stats(path):
    b = path.read_bytes()
    jl = struct.unpack("<I", b[12:16])[0]
    j = json.loads(b[20:20 + jl])
    tris = 0
    lo, hi = [1e9] * 3, [-1e9] * 3
    for n in j["nodes"]:
        for p in j["meshes"][n["mesh"]]["primitives"]:
            acc = j["accessors"]
            tris += (acc[p["indices"]]["count"] if "indices" in p else acc[p["attributes"]["POSITION"]]["count"]) // 3
            pa = acc[p["attributes"]["POSITION"]]
            lo = [min(a, c) for a, c in zip(lo, pa["min"])]
            hi = [max(a, c) for a, c in zip(hi, pa["max"])]
    return tris, lo, hi, [n.get("name", "") for n in j["nodes"]]


def m(v):
    return f"{v:.2f}".rstrip("0").rstrip(".")


# ---------------------------------------------------------------- models
MODELS = [
    ("cottage-01", "Longhouse", "Daub walls in a timber frame under a hipped thatch roof; door on the south (+Z) side."),
    ("cottage-02", "Longhouse with byre", "Plank walls, owl gables at the ridge ends, a byre door on the east end and a hay rick at the back."),
    ("cottage-03", "Small hut", "3 x 2 cells; daub walls under a steep hipped thatch."),
    ("well", "Well", "Stone ring with water, windlass, crank, bucket and a narrow thatch roof."),
    ("market-stall", "Market stall", "Trestle counter under a striped Brick and Limewash awning, with baskets, a sack and cabbages; a crate and a stool behind."),
    ("field-sprouting", "Field, spring", "16 m plot that tiles: ten ridges running east-west with green rows."),
    ("field-ripe", "Field, summer", "The same plot with golden rows."),
    ("field-stubble", "Field, autumn", "Stubble rows and six stooks, all under 0.6 m."),
    ("fence-wattle", "Wattle fence", "One 2 m cell edge along X; posts at x = -1 and +1 are shared with the next segment."),
    ("fence-post", "Fence post", "Corner or end post."),
    ("fence-gate", "Hurdle gate", "Braced gate on two posts, one 2 m cell edge."),
    ("footbridge-span", "Footbridge span", "2 m module along Z: deck at 0.6 m, stringers, piles 0.8 m into the water, rails 1.2 m above the deck."),
    ("footbridge-end", "Footbridge landing", "Ramp from the bank (+Z) up to the deck (-Z); turn 180 degrees for the far bank."),
    ("ford-stones", "Ford stones", "Flat stepping stones along Z; y = 0 is the river bed, tops at 0.3 m."),
    ("rowboat", "Rowboat", "Drawn at 1.5x. Bow +Z, floor, two thwarts, stowed oars; y = 0 is the waterline."),
    ("tree-oak", "Oak", "Broad lumpy round canopy with a darker underside."),
    ("tree-willow", "Pollard willow", "Fat trunk, pale Bent-grass crown."),
    ("tree-poplar", "Poplar", "Tall narrow column."),
    ("reeds", "Reed clump", "Tussocks with leaning blades in straw and green, two cattails."),
    ("villager", "Villager", "Drawn at 1.5x. Nodes 'body' and 'head' for a bob-and-sway shader; the hood point at the back shows facing."),
    ("villager-cargo-grain", "Cargo: grain", "Round sack; origin at the bottom centre, attach at (0, 2.6, 0)."),
    ("villager-cargo-wool", "Cargo: wool", "Square bale; attach at (0, 2.6, 0)."),
    ("villager-cargo-timber", "Cargo: timber", "Long bar lying along X; attach at (0, 2.6, 0)."),
    ("villager-cargo-cloth", "Cargo: cloth", "Folded square; attach at (0, 2.6, 0)."),
]
BUDGET = {"cottage": 500, "well": 300, "market": 400, "field": 500, "fence-wattle": 100, "fence-post": 100,
          "fence-gate": 150, "footbridge": 300, "ford": 150, "rowboat": 300, "tree": 300, "reeds": 150,
          "villager": 300}


def budget(name):
    for k, v in BUDGET.items():
        if name.startswith(k):
            return v
    return 500


model_cards = []
total_tris = 0
for name, title, what in MODELS:
    item = f"assets/models/{name}.glb"
    tris, lo, hi, nodes = glb_stats(ROOT / item)
    total_tris += tris
    img = put(f"assets/src/contact-sheets/{name}.png", f"models/{name}.png")
    w, h, d = hi[0] - lo[0], hi[1], hi[2] - lo[2]
    below = f", reaches {m(-lo[1])} m below y = 0" if lo[1] < -0.01 else ""
    model_cards.append(f"""
<article class="card model" id="{name}">
  <figure><img src="{img}" alt="Contact sheet of {E(title)}: top view, 45-degree views from south, east and north, and four side views" loading="lazy" width="1920" height="960"></figure>
  <div class="body">
    <h3>{E(title)} <code>{name}.glb</code></h3>
    <p>{E(what)}</p>
    <dl class="nums">
      <dt>Footprint</dt><dd>{m(w)} x {m(d)} m</dd>
      <dt>Height</dt><dd>{m(h)} m{below}</dd>
      <dt>Triangles</dt><dd>{tris} <span class="muted">of {budget(name)}</span></dd>
      {"<dt>Nodes</dt><dd>" + E(", ".join(nodes)) + "</dd>" if len(nodes) > 1 else ""}
    </dl>
    {ledger_facts(item)}
  </div>
</article>""")
scene = put("assets/src/contact-sheets/hamlet-scene.png", "models/hamlet-scene.png")

# ---------------------------------------------------------------- textures
TEX = [
    ("ground-grass", "Meadow", "wear 0 to 0.10", "Gouache patches, grass tufts, wildflower specks, paper grain."),
    ("ground-trampled", "Bent grass", "wear 0.10 to 0.30", "Strokes run along +X, the walking direction; the shader turns the UVs."),
    ("ground-trail", "Trail", "wear 0.30 to 0.55", "Bare trail earth with drifts of broken grass."),
    ("ground-track", "Track", "wear 0.55 to 0.80", "Packed earth with dry and damp patches and compaction lines."),
    ("ground-hollow-way", "Hollow way", "wear 0.80 to 1.00", "Dark compacted mud with a wet sheen; rain puddles are left to the shader."),
    ("water-river", "River water", "scrolls", "River slate blue with engraved Deep-water lines; periodic, so it can scroll."),
    ("paper-page", "Book page", "page ground", "Paper with Limewash sizing, fibres and grain."),
    ("palette", "Palette atlas", "256 x 256", "One 16 px swatch per palette colour; every model's UVs point here."),
]
tex_cards = []
for name, title, stage, what in TEX:
    item = f"assets/textures/{name}.png"
    src = put(item, f"textures/{name}.png")
    tiled = name != "palette"
    tex_cards.append(f"""
<article class="card tex">
  <div class="swatch{' tiled' if tiled else ''}" style="background-image:url('{src}')" role="img" aria-label="{E(title)} texture{', tiled 2 x 2' if tiled else ''}"></div>
  <div class="body">
    <h3>{E(title)} <code>{name}.png</code></h3>
    <p><span class="tag">{E(stage)}</span> {E(what)}</p>
    {ledger_facts(item)}
  </div>
</article>""")
ramp = put("assets/textures/wear-ramp.png", "textures/wear-ramp.png")
ground_prev = put("assets/src/2d/previews/ground-preview.png", "textures/ground-preview.png")

# ---------------------------------------------------------------- icons
ICONS = [("tool-fence", "Fence"), ("tool-gate", "Gate"), ("tool-footbridge", "Footbridge"), ("tool-gravel", "Gravel"),
         ("tool-erase", "Erase"), ("ui-play", "Play"), ("ui-pause", "Pause"), ("ui-speed", "Speed"),
         ("ui-wear-overlay", "Wear overlay"), ("ui-flow-preview", "Flow preview"), ("ui-sound-on", "Sound on"),
         ("ui-sound-off", "Sound off"), ("ui-reset", "Reset"), ("ui-new-map", "New map")]
icon_rows = []
for name, label in ICONS:
    src = put(f"assets/icons/{name}.svg", f"icons/{name}.svg")
    cells = "".join(f'<span class="ico s{s}" style="--src:url(\'{src}\')"></span>' for s in (20, 32, 48))
    icon_rows.append(f"""<li><div class="pair"><div class="on-paper">{cells}</div><div class="on-ink">{cells}</div></div>
<p><b>{E(label)}</b> <code>{name}.svg</code> <span class="muted">{E(LEDGER[f"assets/icons/{name}.svg"]["licence"])}, icons.py</span></p></li>""")
_ir = LEDGER["assets/icons/tool-fence.svg"]
icon_facts = (f'<dl class="facts"><dt>Source</dt><dd>{E(_ir["source"])}</dd><dt>Licence</dt><dd>{E(_ir["licence"])}</dd>'
              f'<dt>Verified</dt><dd>{E(_ir["verified"])}, {E(_ir["date_checked"])}</dd></dl>')

# ---------------------------------------------------------------- fonts
FONTS = [
    ("AtkinsonHyperlegible-Regular.ttf", "Atkinson Hyperlegible", "f-atk", "400", "Body, UI and numbers",
     "Toll at the ford: 3 pennies. Wear 0.42. Villagers 1,280 / 12:05"),
    ("AtkinsonHyperlegible-Bold.ttf", "Atkinson Hyperlegible Bold", "f-atk", "700", "Bold body and numbers",
     "Place fence. Open gate. 16 x 16 m field, 0.6 m crops"),
    ("Alegreya-Variable.ttf", "Alegreya (variable, 400 to 900)", "f-ale", "500", "Headings and brand serif",
     "The Hamlet by the river, in the year 1100"),
    ("UncialAntiqua-Regular.ttf", "Uncial Antiqua", "f-unc", "400", "Hamlet display titles, 24 px and up",
     "Tide and Toll"),
]
font_faces = []
font_rows = []
seen = set()
for fn, label, fam, wt, use, sample in FONTS:
    src = put(f"assets/fonts/{fn}", f"fonts/{fn}")
    if (fam, wt) not in seen:
        seen.add((fam, wt))
        rng = "400 900" if fam == "f-ale" else wt
        font_faces.append(f"@font-face{{font-family:'{fam}';src:url('{src}') format('truetype');font-weight:{rng};font-display:swap}}")
    font_rows.append(f"""
<article class="card font">
  <p class="specimen {fam}" style="font-weight:{wt}">{E(sample)}</p>
  <div class="body">
    <h3>{E(label)} <code>{fn}</code></h3>
    <p>{E(use)}.</p>
    {ledger_facts(f"assets/fonts/{fn}")}
  </div>
</article>""")
for lic in ("AtkinsonHyperlegible-OFL.txt", "Alegreya-OFL.txt", "UncialAntiqua-OFL.txt"):
    put(f"assets/licences/{lic}", f"fonts/{lic}")


# ---------------------------------------------------------------- audio
def measure(item):
    r = A.check(ROOT / item)
    return r


def fmt_audio(r):
    li = r["lufs_i"]
    loud = f'{r["momentary_max_lufs"]:.1f} LUFS momentary max'
    if li is not None and li > -69:
        loud += f", {li:.1f} LUFS integrated"
    ch = "stereo" if r.get("channels") == 2 else "mono"
    return f'{r["duration_s"]:.2f} s, {ch}', loud, f'{r["true_peak_dbtp"]:.1f} dBTP'


SFX = [
    ("step-grass", 8, "Footsteps on grass", "Heel swish, quieter toe swish, a few blade crackles, a faint thud. Soft leather shoes.", 12),
    ("step-dirt", 8, "Footsteps on a dirt trail", "Drier and shorter than grass, more low-mid body, heel and toe roll with a little grit.", 12),
    ("water-ford", 6, "Wading at the ford", "Soft slap, a sloshing body that falls away, rising bubble plinks, late drips.", 12),
    ("bell", 6, "Chapel bell in D", "Prime D5, hum D4, minor-third tierce; six strikes of the same bell at forces 0.7 to 1.15.", 4),
    ("place", 6, "Place a fence or bridge segment", "One soft knock of a wattle stake, pitched in D Dorian: D4, E4, G4, A4, B4, D5.", 6),
    ("erase", 6, "Erase", "Pull, snap, rustle: a brushing pull rising in pitch, one dry twig snap, a light rustle.", 8),
]
sfx_sections = []
n_audio = 0
for stem, count, title, what, fmax in SFX:
    rows = []
    for i in range(1, count + 1):
        nm = f"{stem}-{i:02d}"
        item = f"assets/audio/sfx/{nm}.wav"
        snd = put(item, f"audio/{nm}.wav")
        spec = put(f"build/audio-previews/{nm}.png", f"spectrograms/{nm}.png")
        length, loud, peak = fmt_audio(measure(item))
        n_audio += 1
        src = LEDGER[item]["source"]
        seed = src[src.find("(seed"):src.find(")") + 1] if "(seed" in src else ""
        rows.append(f"""<li class="take">
  <div class="take-head"><b>{nm}</b> <span class="muted">{E(seed)}</span></div>
  <audio controls preload="none" src="{snd}" aria-label="Play {nm}"></audio>
  <img src="{spec}" alt="Waveform and spectrogram of {nm}, 0 to {fmax} kHz" loading="lazy" width="800" height="360">
  <p class="meta">{length}; {loud}; peak {peak}</p>
</li>""")
    sfx_sections.append(f"""
<article class="card sfx" id="{stem}">
  <div class="body">
    <h3>{E(title)} <code>{stem}-01..{count:02d}.wav</code></h3>
    <p>{E(what)} WAV, 48 kHz, 16-bit, mono. Spectrograms run 0 to {fmax} kHz.</p>
    {ledger_facts(f"assets/audio/sfx/{stem}-01.wav")}
  </div>
  <ol class="takes">{"".join(rows)}</ol>
</article>""")

LOOPS = [
    ("assets/audio/music/theme-hamlet-draft.ogg", "theme-hamlet-draft", "Theme, Hamlet arrangement (draft)",
     "Eight bars in D Dorian, 3/4 at 64 BPM. Wooden pipe over a river drone (D2, D3, A3), a small lyre on the open fifth, "
     "a frame drum in bars 5 to 8. The bell motif is the first four notes, D E G A. Seamless loop.",
     "D5 E5 G5 | A5 | B5 A5 G5 | A5 E5 | G5 A5 G5 | E5 F5 | E5 D5 E5 | D5", 6),
    ("assets/audio/ambience/water-river-loop.ogg", "water-river-loop", "River loop",
     "Calm river bed: a flowing low body, laps against the bank, bubble plinks, and a soft D drone about 13 dB under the water. "
     "Ten seconds, seamless by construction.", "", 12),
]
loop_cards = []
for item, stem, title, what, melody, fmax in LOOPS:
    snd = put(item, f"audio/{stem}.ogg")
    spec = put(f"build/audio-previews/{stem}.png", f"spectrograms/{stem}.png")
    seam = put(f"build/audio-previews/{stem}-seam.png", f"spectrograms/{stem}-seam.png")
    length, loud, peak = fmt_audio(measure(item))
    n_audio += 1
    mel = f'<p class="melody"><span class="label">Melody</span> {E(melody)}</p>' if melody else ""
    loop_cards.append(f"""
<article class="card loop" id="{stem}">
  <div class="body">
    <h3>{E(title)} <code>{Path(item).name}</code></h3>
    <p>{E(what)}</p>
    {mel}
    <audio controls preload="none" src="{snd}" aria-label="Play {E(title)}"></audio>
    <dl class="nums"><dt>Length</dt><dd>{length}, Ogg Vorbis 48 kHz</dd><dt>Loudness</dt><dd>{loud}</dd><dt>Peak</dt><dd>{peak}</dd></dl>
  </div>
  <div class="pics">
    <figure><img src="{spec}" alt="Waveform and spectrogram of {E(title)}, 0 to {fmax} kHz" loading="lazy" width="800" height="360"><figcaption>Whole loop, 0 to {fmax} kHz</figcaption></figure>
    <figure><img src="{seam}" alt="Loop seam of {E(title)}: the end joined to the start" loading="lazy" width="800" height="360"><figcaption>The seam: end joined to start</figcaption></figure>
  </div>
  <div class="body">{ledger_facts(item)}</div>
</article>""")
score_note = ledger_facts("assets/audio/music/theme-hamlet-draft-score.json")

# ---------------------------------------------------------------- counts
counts = {}
for r in LEDGER.values():
    counts[r["kind"]] = counts.get(r["kind"], 0) + 1
asset_kinds = ["model", "texture", "icon", "font", "sound", "music", "ambience"]
LABEL = {"model": "models", "texture": "textures", "icon": "icons", "font": "fonts", "sound": "sound effects",
         "music": "music files", "ambience": "ambience loop"}
count_line = ", ".join(f"{counts.get(k, 0)} {LABEL[k]}" for k in asset_kinds)
n_files = sum(counts.get(k, 0) for k in asset_kinds)

PAL = [("Paper", "#F2EAD8"), ("Ink", "#2B2620"), ("Meadow", "#7FA35B"), ("Bent grass", "#A9B86E"),
       ("Trail earth", "#B89568"), ("Packed earth", "#8C6A48"), ("Mud", "#5E4A36"), ("River", "#5B7F95"),
       ("Deep water", "#3F5F75"), ("Reed and thatch", "#C9A55A"), ("Brick", "#A4553A"), ("Slate roof", "#5E6B78"),
       ("Soot brick", "#7A4436"), ("Cast-iron green", "#44604F"), ("Concrete", "#A9A69C"), ("Wet mudflat", "#6F6450"),
       ("Limewash", "#E9E2CF")]
pal_tokens = "".join(f"--p{i}:{h};" for i, (_, h) in enumerate(PAL))
pal_chips = "".join(f'<li><span class="chip" style="background:var(--p{i})"></span>{E(n)}</li>' for i, (n, _) in enumerate(PAL))

CSS = """
/* Layout: one column of book pages; each section is a gathering of cards on Paper, ruled with Ink. */
:root{
  --paper:#F2EAD8; --sheet:#F8F3E7; --ink:#2B2620; --ink-soft:#5E4A36; --rule:#C9BFA8; --river:#3F5F75; --brick:#A4553A;
  --tag-bg:#E9E2CF; --chip-paper:#F2EAD8; --chip-ink:#2B2620; --focus:#3F5F75;
  PALTOKENS
  --f-display:'f-unc', 'Alegreya', Georgia, serif;
  --f-head:'f-ale', Georgia, 'Times New Roman', serif;
  --f-body:'f-atk', system-ui, -apple-system, 'Segoe UI', sans-serif;
  --f-mono:ui-monospace, 'SF Mono', Menlo, Consolas, monospace;
}
@media (prefers-color-scheme: dark){ :root:not([data-theme="light"]){
  --paper:#221E19; --sheet:#2B2620; --ink:#EDE4D0; --ink-soft:#BFAF92; --rule:#4A4137; --river:#8FB1C6; --brick:#D9876A;
  --tag-bg:#3A332B; --chip-paper:#F2EAD8; --chip-ink:#2B2620; --focus:#8FB1C6; PALTOKENS color-scheme:dark } }
:root[data-theme="dark"]{
  --paper:#221E19; --sheet:#2B2620; --ink:#EDE4D0; --ink-soft:#BFAF92; --rule:#4A4137; --river:#8FB1C6; --brick:#D9876A;
  --tag-bg:#3A332B; --chip-paper:#F2EAD8; --chip-ink:#2B2620; --focus:#8FB1C6; PALTOKENS color-scheme:dark }
FONTFACES
body{background:var(--paper);color:var(--ink);font-family:var(--f-body);font-size:16px;line-height:1.5}
.wrap{max-width:1180px;margin:0 auto;padding-block:40px 64px;padding-left:max(16px, env(safe-area-inset-left, 0px));padding-right:max(16px, env(safe-area-inset-right, 0px))}
img{max-width:100%;height:auto;display:block}
a{color:var(--river)} a:focus-visible,audio:focus-visible,summary:focus-visible{outline:2px solid var(--focus);outline-offset:2px}
code{font-family:var(--f-mono);font-size:.8em;color:var(--ink-soft);font-weight:400;word-break:break-all}
h1,h2,h3{font-family:var(--f-head);text-wrap:balance;margin:0;line-height:1.15}
header.title{display:grid;gap:12px;border-bottom:2px solid var(--ink);padding-bottom:24px}
.eyebrow{font-size:.8rem;letter-spacing:.08em;text-transform:uppercase;color:var(--ink-soft);margin:0}
h1{font-family:var(--f-display);font-weight:400;font-size:clamp(2.2rem,6vw,3.6rem)}
.lead{font-size:1.15rem;max-width:62ch;margin:0}
.counts{display:flex;flex-wrap:wrap;gap:8px 20px;margin:0;padding:0;list-style:none;font-variant-numeric:tabular-nums;color:var(--ink-soft);font-size:.92rem}
.counts b{color:var(--ink)}
nav.toc{display:flex;flex-wrap:wrap;gap:6px 18px;font-size:.95rem}
section{margin-top:56px;display:grid;gap:20px}
section>header{display:grid;gap:6px;border-bottom:1px solid var(--rule);padding-bottom:10px}
h2{font-size:clamp(1.6rem,3.5vw,2.1rem);font-weight:600}
section>header p{margin:0;max-width:70ch;color:var(--ink-soft)}
h3{font-size:1.2rem;font-weight:600;display:flex;flex-wrap:wrap;align-items:baseline;gap:4px 10px}
.grid{display:grid;gap:20px;grid-template-columns:repeat(auto-fill,minmax(min(100%,340px),1fr))}
.card{background:var(--sheet);border:1px solid var(--rule);border-radius:3px;display:grid;align-content:start;min-width:0;overflow:hidden}
.card .body{padding:14px 16px 16px;display:grid;gap:10px;min-width:0}
.card p{margin:0}
.card figure{margin:0;border-bottom:1px solid var(--rule);background:var(--chip-paper)}
dl{margin:0;display:grid;grid-template-columns:max-content 1fr;gap:3px 12px;font-size:.88rem}
dt{color:var(--ink-soft)} dd{margin:0;min-width:0;overflow-wrap:anywhere}
dl.nums dd{font-variant-numeric:tabular-nums}
dl.facts{border-top:1px dashed var(--rule);padding-top:10px}
.muted{color:var(--ink-soft);font-weight:400}
.tag{display:inline-block;background:var(--tag-bg);border-radius:2px;padding:0 6px;font-size:.82rem;margin-right:4px}
.wide{background:var(--sheet);border:1px solid var(--rule);border-radius:3px;margin:0;overflow:hidden}
.wide img{width:100%;background:var(--chip-paper)}
.wide figcaption{padding:10px 16px;font-size:.9rem;color:var(--ink-soft)}
.palette{list-style:none;margin:0;padding:0;display:grid;grid-template-columns:repeat(auto-fill,minmax(150px,1fr));gap:6px 14px;font-size:.88rem}
.palette li{display:flex;align-items:center;gap:8px}
.chip{width:22px;height:22px;border:1px solid var(--rule);border-radius:2px;flex:none}
.swatch{aspect-ratio:2/1;max-width:100%;background-size:50% auto;background-repeat:repeat;image-rendering:auto;border-bottom:1px solid var(--rule)}
.swatch:not(.tiled){background-size:contain;background-repeat:no-repeat;background-position:center;background-color:var(--chip-paper);image-rendering:pixelated}
.ramp{display:grid;gap:6px}
.ramp img{width:100%;height:40px;image-rendering:auto;border:1px solid var(--rule)}
.ramp .scale{display:flex;justify-content:space-between;font-size:.8rem;color:var(--ink-soft);font-variant-numeric:tabular-nums}
.icons{list-style:none;margin:0;padding:0;display:grid;gap:12px;grid-template-columns:repeat(auto-fill,minmax(min(100%,260px),1fr))}
.icons li{background:var(--sheet);border:1px solid var(--rule);border-radius:3px;overflow:hidden;min-width:0}
.icons p{margin:0;padding:8px 12px;font-size:.92rem;display:flex;flex-wrap:wrap;gap:4px 8px;align-items:baseline}
.pair{display:grid;grid-template-columns:1fr 1fr}
.pair>div{display:flex;align-items:center;justify-content:center;gap:12px;padding:14px 8px}
.on-paper{background:var(--chip-paper);color:var(--chip-ink)}
.on-ink{background:var(--chip-ink);color:var(--chip-paper)}
.ico{display:inline-block;background:currentColor;-webkit-mask:var(--src) center/contain no-repeat;mask:var(--src) center/contain no-repeat}
.s20{width:20px;height:20px}.s32{width:32px;height:32px}.s48{width:48px;height:48px}
.specimen{padding:22px 16px;border-bottom:1px solid var(--rule);font-size:1.6rem;line-height:1.25;overflow-wrap:anywhere}
.f-atk{font-family:'f-atk',system-ui,sans-serif}.f-ale{font-family:'f-ale',Georgia,serif}.f-unc{font-family:'f-unc',Georgia,serif;font-size:2.2rem}
.sfx{grid-column:1/-1}
.takes{list-style:none;margin:0;padding:0 16px 16px;display:grid;gap:14px;grid-template-columns:repeat(auto-fill,minmax(min(100%,250px),1fr))}
.take{display:grid;gap:6px;align-content:start;min-width:0}
.take-head{font-size:.92rem;display:flex;gap:8px;flex-wrap:wrap}
.take img{border:1px solid var(--rule);width:100%}
audio{width:100%;max-width:100%;height:36px}
.meta{font-size:.8rem;color:var(--ink-soft);font-variant-numeric:tabular-nums}
.loop{grid-column:1/-1}
.pics{display:grid;gap:12px;padding:0 16px;grid-template-columns:repeat(auto-fit,minmax(min(100%,320px),1fr))}
.pics figure{margin:0;border:0;background:none}
.pics img{border:1px solid var(--rule);width:100%}
.pics figcaption{font-size:.82rem;color:var(--ink-soft);padding-top:4px}
.melody{font-family:var(--f-mono);font-size:.85rem;background:var(--tag-bg);padding:6px 10px;border-radius:2px;overflow-wrap:anywhere}
.label{font-family:var(--f-body);color:var(--ink-soft);margin-right:6px}
.notes{display:grid;gap:8px;max-width:75ch;margin:0;padding-left:20px}
footer{margin-top:56px;border-top:2px solid var(--ink);padding-top:16px;font-size:.88rem;color:var(--ink-soft);display:grid;gap:6px}
footer p{margin:0}
@media (prefers-reduced-motion: reduce){*{scroll-behavior:auto}}
""".replace("PALTOKENS", pal_tokens).replace("FONTFACES", "\n".join(font_faces))

PAGE = f"""<title>Tide &amp; Toll Asset Gallery</title>
<meta name="claude:viewport-fit-sides" content="cover">
<style>{CSS}</style>
<div class="wrap">
<header class="title">
  <p class="eyebrow">Tide &amp; Toll &middot; Hamlet era, about 1100 &middot; review copy, 2026-11-24</p>
  <h1>The Hamlet asset set</h1>
  <p class="lead">Every model, texture, icon, font and sound made for the first era, with how it was made and under what licence. Everything except the fonts was generated in code by scripts under <code>assets/src/</code>; nothing came from a paid or unclear source.</p>
  <ul class="counts"><li><b>{n_files}</b> files in the ledger</li><li>{count_line}</li></ul>
  <nav class="toc" aria-label="Sections"><a href="#models">Models</a><a href="#ground">Ground and textures</a><a href="#icons">Icons</a><a href="#fonts">Fonts</a><a href="#sfx">Sound effects</a><a href="#theme">Theme and river</a></nav>
</header>

<section id="models">
  <header><h2>Models</h2>
  <p>{len(MODELS)} glTF binary models, {total_tris:,} triangles in all. One cell is 2 m; front faces +Z (south). Each contact sheet shows, top row: from above, then tilted 45 degrees from the south, east and north; bottom row: low side views at yaw 0, 90, 180 and 270. Triangle budgets are from the pipeline guide.</p></header>
  <figure class="wide"><img src="{scene}" alt="All Hamlet models together at true scale" width="2880" height="720"><figcaption>All models together at true scale (preview scene built by <code>assets/src/models/_scene.py</code>).</figcaption></figure>
  <div class="grid">{"".join(model_cards)}</div>
</section>

<section id="ground">
  <header><h2>Ground and textures</h2>
  <p>512 px tiles, shown here tiled 2 x 2 so seams would show. Every pixel is an exact palette colour except in the wear ramp, whose blends are soft on purpose. The five ground tiles are the wear stages a path passes through as villagers walk it.</p></header>
  <figure class="wide"><img src="{ground_prev}" alt="Ground preview: the five wear stages, a trail crossing grass, water, paper, a tiled grass sample and the wear ramp" loading="lazy" width="1536" height="1088"><figcaption>Generator preview: the five stages side by side, a trail crossing grass, water, paper, a 2 x 2 grass tiling and the ramp.</figcaption></figure>
  <div class="grid">{"".join(tex_cards)}</div>
  <article class="card"><div class="body">
    <h3>Wear ramp <code>wear-ramp.png</code></h3>
    <div class="ramp"><img src="{ramp}" alt="Wear ramp from Meadow through Bent grass, Trail earth, Packed earth to Mud"><div class="scale"><span>0</span><span>0.10</span><span>0.30</span><span>0.55</span><span>0.80</span><span>1.00</span></div></div>
    <p>256 x 8; u is wear from 0 to 1. Colours blend over 0.035 either side of each stage boundary. Import with linear filtering, clamp, no mipmaps.</p>
    {ledger_facts("assets/textures/wear-ramp.png")}
  </div></article>
  <article class="card"><div class="body"><h3>Palette</h3><ul class="palette">{pal_chips}</ul></div></article>
</section>

<section id="icons">
  <header><h2>Icons</h2>
  <p>{len(ICONS)} SVGs on a 24-unit grid, one colour (Ink) with 2-unit round strokes. Shown at 20, 32 and 48 px on Paper and on Ink. Placement tools are named <code>tool-*</code>, the rest <code>ui-*</code>.</p></header>
  <ul class="icons">{"".join(icon_rows)}</ul>
  <article class="card"><div class="body"><h3>Ledger, the same for all 14 icons</h3>{icon_facts}</div></article>
</section>

<section id="fonts">
  <header><h2>Fonts</h2>
  <p>Three OFL families, unmodified TTF files from github.com/google/fonts. Each OFL text is saved in <code>assets/licences/</code> and ships with the game.</p></header>
  <div class="grid">{"".join(font_rows)}</div>
</section>

<section id="sfx">
  <header><h2>Sound effects</h2>
  <p>Synthesised one-shots, played round-robin by the game with small random changes in level and pitch. Each take shows its waveform above a spectrogram. Loudness is the loudest 400 ms block; peak is true peak.</p></header>
  <div class="grid">{"".join(sfx_sections)}</div>
</section>

<section id="theme">
  <header><h2>Theme and river</h2>
  <p>The two loops. Both are seamless by construction; the seam pictures show the end of the file joined back to its start. The theme also exists as a 16-bit WAV master and as score data (<code>theme-hamlet-draft-score.json</code>), not played here.</p></header>
  <div class="grid">{"".join(loop_cards)}</div>
  <article class="card"><div class="body"><h3>Theme score data <code>theme-hamlet-draft-score.json</code></h3><p>Key, mode, metre, tempo, bell motif and every note of the pipe, drum, lyre and drone parts.</p>{score_note}</div></article>
</section>

<section id="review">
  <header><h2>For review</h2></header>
  <ul class="notes">
    <li>Nobody has listened to the audio yet; tone and mix were judged from spectrograms and loudness numbers only. The theme most needs a listening pass.</li>
    <li>Seen from straight above, reed blades are too thin to show, so the reed clump reads as tussocks.</li>
    <li>Fields and fences show many ink lines in low side views; at game camera tilts they read cleanly.</li>
    <li>Alegreya was added for headings beyond the two faces asked for; drop its file and row if only two are wanted.</li>
    <li>Icons are one colour, so in Godot they can only be darkened with modulate; swap the hex at load time or use an alpha-mask shader to draw them light.</li>
  </ul>
</section>

<footer>
  <p>Built from <code>assets/LEDGER.csv</code> and the files in <code>assets/</code> by <code>docs/asset-gallery/build.py</code>. Contact sheets: <code>tools/render-contact-sheet.sh --sides</code>. Spectrograms: <code>assets/src/audio/_synth.py</code>.</p>
</footer>
</div>
"""

if __name__ == "__main__":
    (OUT / "index.html").write_text(PAGE, encoding="utf-8")
    (OUT / "files.json").write_text(json.dumps(dict(sorted(FILES.items())), indent=2) + "\n", encoding="utf-8")
    size = sum(p.stat().st_size for p in MEDIA.rglob("*") if p.is_file())
    big = [p for p in MEDIA.rglob("*") if p.is_file() and p.stat().st_size > 10_000_000]
    print(f"index.html {len(PAGE):,} bytes; {len(FILES)} media files, {size / 1e6:.1f} MB; audio measured: {n_audio}; over 10 MB: {big}")
