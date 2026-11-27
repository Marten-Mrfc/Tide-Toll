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
        t = n.get("translation", [0.0, 0.0, 0.0])          # River set: hinged and moving nodes carry a translation
        for p in j["meshes"][n["mesh"]]["primitives"]:
            acc = j["accessors"]
            tris += (acc[p["indices"]]["count"] if "indices" in p else acc[p["attributes"]["POSITION"]]["count"]) // 3
            pa = acc[p["attributes"]["POSITION"]]
            lo = [min(a, c + o) for a, c, o in zip(lo, pa["min"], t)]
            hi = [max(a, c + o) for a, c, o in zip(hi, pa["max"], t)]
    return tris, lo, hi, [n.get("name", "") for n in j["nodes"]]


def m(v):
    return f"{v:.2f}".rstrip("0").rstrip(".")


# ---------------------------------------------------------------- models
MODELS = [
    ("cottage-01", "Longhouse", "Daub walls in a timber frame under a hipped thatch roof; door on the south (+Z) side."),
    ("cottage-02", "Longhouse with byre", "Plank walls, owl gables at the ridge ends, a byre door on the east end and a hay rick at the back."),
    ("cottage-03", "Small hut", "3 x 2 cells; daub walls under a steep GABLED thatch with limewashed gable ends and crossed gable horns, so from above it is a split rectangle with a full-length ridge, unlike the two hipped longhouses."),
    ("well", "Well", "Stone ring with water, windlass, crank, bucket and a narrow thatch roof."),
    ("market-stall", "Market stall", "Trestle counter under a striped Brick and Limewash awning, with baskets, a sack and cabbages; a crate and a stool behind."),
    ("field-sprouting", "Field, spring", "16 m plot that tiles: ten low ridges running east-west with green rows. Low-pitched profiles and soft ink, so the furrows stay light in low side views."),
    ("field-ripe", "Field, summer", "The same plot with golden rows combed to a ridge."),
    ("field-stubble", "Field, autumn", "Stubble rows and six stooks, all under 0.6 m."),
    ("fence-wattle", "Wattle fence", "One 2 m cell edge along X; posts at x = -1 and +1 are shared with the next segment."),
    ("fence-post", "Fence post", "Corner or end post."),
    ("fence-gate", "Hurdle gate", "Braced gate on two posts, one 2 m cell edge."),
    ("footbridge-span", "Footbridge span", "2 m module along Z: deck at 0.6 m, stringers, piles 0.8 m into the water; hand rail and posts end exactly 1.2 m above the deck, as the pipeline table now states (1.8 m above the water)."),
    ("footbridge-end", "Footbridge landing", "Ramp from the bank (+Z) up to the deck (-Z); turn 180 degrees for the far bank."),
    ("ford-stones", "Ford stones", "Flat stepping stones along Z; y = 0 is the river bed, tops at 0.3 m."),
    ("rowboat", "Rowboat", "Drawn at 1.5x. Bow +Z, floor, two thwarts, stowed oars; y = 0 is the waterline."),
    ("tree-oak", "Oak", "Broad lumpy round canopy with a darker underside. Soft ink: facet creases are no longer inked."),
    ("tree-willow", "Weeping willow", "Rebuilt: a rounded pale cap with a curtain of 14 broad fronds hanging to 1.5 to 2.1 m, tips splayed so the fringe shows round the cap from above."),
    ("tree-poplar", "Poplar", "Tall narrow column. Soft ink."),
    ("reeds", "Reed clump", "Rebuilt: thin upright stems in three drifts, nine dark seed heads at 1.2 to 1.5 m and a few narrow blades; no rosette. From above: dark heads over gold strokes."),
    ("villager", "Villager", "Drawn at 1.5x. Nodes 'body' and 'head' for a bob-and-sway shader. Facing cue from above: a pale pack on the back, with the head a little forward."),
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
    ("ground-grass", "Meadow", "wear 0 to 0.10", "Gouache patches (smaller and more of them, so no big blob marks the repeat), grass tufts, wildflower specks, paper grain."),
    ("ground-trampled", "Bent grass", "wear 0.10 to 0.30", "Laid grass in every direction (isotropic), so nothing streaks when the camera turns."),
    ("ground-trail", "Trail", "wear 0.30 to 0.55", "Bare trail earth with drifts of broken grass."),
    ("ground-track", "Track", "wear 0.55 to 0.80", "Packed earth with dry and damp patches and compaction lines."),
    ("ground-hollow-way", "Hollow way", "wear 0.80 to 1.00", "A dark, wet, sunken lane: Mud with small Deep-water puddles, a River glint in a few, sparse drier crumbs. No ink marks, no pale blotches."),
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
<article class="card tex" id="tex-{name}">
  <div class="swatch{' tiled' if tiled else ''}" style="background-image:url('{src}')" role="img" aria-label="{E(title)} texture{', tiled 2 x 2' if tiled else ''}"></div>
  <div class="body">
    <h3>{E(title)} <code>{name}.png</code></h3>
    <p><span class="tag">{E(stage)}</span> {E(what)}</p>
    {ledger_facts(item)}
  </div>
</article>""")
ramp = put("assets/textures/wear-ramp.png", "textures/wear-ramp.png")
ground_prev = put("assets/src/2d/previews/ground-preview.png", "textures/ground-preview.png")
ground_4x4 = put("assets/src/2d/previews/ground-tiled-4x4.png", "textures/ground-tiled-4x4.png")

# ---------------------------------------------------------------- icons
ICONS = [("tool-fence", "Fence"), ("tool-gate", "Gate"), ("tool-footbridge", "Footbridge"), ("tool-gravel", "Gravel"),
         ("tool-erase", "Erase"), ("ui-play", "Play"), ("ui-pause", "Pause"), ("ui-speed", "Speed"),
         ("ui-wear-overlay", "Wear overlay"), ("ui-flow-preview", "Flow preview"), ("ui-sound-on", "Sound on"),
         ("ui-sound-off", "Sound off"), ("ui-reset", "Reset"), ("ui-new-map", "New map")]
icon_rows = []
for name, label in ICONS:
    src = put(f"assets/icons/{name}.svg", f"icons/{name}.svg")
    cells = "".join(f'<img class="ico" src="{src}" width="{s}" height="{s}" alt="">' for s in (20, 40, 80))
    icon_rows.append(f"""<li id="{name}"><div class="pair" role="img" aria-label="{E(label)} icon at 20, 40 and 80 px on Paper and on Ink"><div class="on-paper">{cells}</div><div class="on-ink">{cells}</div></div>
<p><b>{E(label)}</b> <code>{name}.svg</code> <span class="muted">{E(LEDGER[f"assets/icons/{name}.svg"]["licence"])}, icons.py</span></p></li>""")
_ir = LEDGER["assets/icons/tool-fence.svg"]
icon_prev = put("assets/src/2d/previews/icons-preview.png", "icons/icons-preview.png")
icon_facts = (f'<dl class="facts"><dt>Source</dt><dd>{E(_ir["source"])}</dd><dt>Licence</dt><dd>{E(_ir["licence"])}</dd>'
              f'<dt>Verified</dt><dd>{E(_ir["verified"])}, {E(_ir["date_checked"])}</dd></dl>')

# ---------------------------------------------------------------- fonts
FONTS = [
    ("TideLegible-Regular.ttf", "Tide Legible (Atkinson Hyperlegible with a plain zero)", "f-atk", "400",
     "Body, UI and numbers",
     "Toll at the ford: 3 pennies. Wear 0.42. Villagers 1,280 / 12:05"),
    ("TideLegible-Bold.ttf", "Tide Legible Bold", "f-atk", "700", "Bold body and numbers",
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
<article class="card font" id="font-{fn.split('.')[0]}">
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
    ("bell", 6, "Chapel bell, gameplay strikes", "About 3 s each, tuned to the bell motif: 01 D4, 02 E4, 03 G4, 04 A4, 05 D4 soft, 06 A4 hard (the hum; the strike tone is an octave up), with bright strike partials at 2.2 to 2.8 kHz.", 4),
    ("bell-long", ["d4", "e4", "g4", "a4"], "Chapel bell, long strikes", "The full 6 s ring on each motif note, kept as named variants for big moments.", 4),
    ("place", 6, "Place a fence or bridge segment", "One soft knock of a wattle stake, pitched in D Dorian (D4, E4, G4, A4, B4, D5), with a short bright crack at 2.3 to 3.5 kHz in the UI lane.", 6),
    ("erase", 6, "Erase", "Pull, snap, rustle, now about 0.3 s: a quick brushing pull, one dry twig snap, a light rustle.", 8),
]
sfx_sections = []
n_audio = 0
for stem, count, title, what, fmax in SFX:
    rows = []
    suffixes = [f"{i:02d}" for i in range(1, count + 1)] if isinstance(count, int) else count
    for suf in suffixes:
        nm = f"{stem}-{suf}"
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
    <h3>{E(title)} <code>{stem}-{suffixes[0]}..{suffixes[-1]}.wav</code></h3>
    <p>{E(what)} WAV, 48 kHz, 16-bit, mono. Spectrograms run 0 to {fmax} kHz.</p>
    {ledger_facts(f"assets/audio/sfx/{stem}-{suffixes[0]}.wav")}
  </div>
  <ol class="takes">{"".join(rows)}</ol>
</article>""")

LOOPS = [
    ("assets/audio/music/theme-hamlet-draft.ogg", "theme-hamlet-draft", "Theme, Hamlet arrangement (draft)",
     "Eight bars in D Dorian, 3/4 at 64 BPM, played twice: the second pass varies bars 11, 14 and 15 and the frame drum "
     "answers throughout, so the loop is 45 s. Wooden pipe over a river drone (D2, D3, A3), a small lyre on the open fifth. "
     "The bell motif is the first four notes, D E G A. Seamless loop: the last D is held into the downbeat, so the level "
     "50 ms either side of the join differs by 0.5 dB (was 9 dB).",
     "D5 E5 G5 | A5 | B5 A5 G5 | A5 E5 | G5 A5 G5 | E5 F5 | E5 D5 E5 | D5 || D5 E5 G5 | A5 | B5 A5 | A5 E5 | G5 A5 G5 | E5 G5 F5 | E5 D5 | D5", 6),
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


# ---------------------------------------------------------------- River set (third round, 2026-11-25)
def node_info(path):
    b = path.read_bytes()
    jl = struct.unpack("<I", b[12:16])[0]
    j = json.loads(b[20:20 + jl])
    out = []
    for n in j["nodes"]:
        acc = j["accessors"][j["meshes"][n["mesh"]]["primitives"][0]["indices"]]
        t = n.get("translation")
        out.append((n["name"], acc["count"] // 3, t))
    return out


RIVER_MODELS = [
    ("barge", "Masted sailing barge", 600,
     "Drawn at 1.5x like the rowboat and almost twice its length: a bluff dark hull with a Cast-iron green gunwale, "
     "leeboards, an open hold, a limewashed deckhouse aft and a big rudder. Bow +Z, y = 0 is the waterline. From above "
     "the furled sail on its yard makes a white cross-bar over the hull: the mast mark that a low bridge blocks.",
     {"mast": "pivot at the mast step; rotate about X by -80 degrees to lower the mast aft",
      "cargo": "goods in the hold; hide the node for an empty barge"}, ["barge-mast-lowered"]),
    ("ferry-raft", "Ferry raft", 500,
     "Plank raft on three log floats, rails on both long sides, boarding flaps at both ends, a ferryman's pole. "
     "The rope runs through two guides on the +X side: the rope line is x = +1.95, y = 1.3 at every z.", {}, []),
    ("ferry-post", "Ferry landing post", 300,
     "One per bank: the rope is made fast at y = 1.3 and leaves towards -Z (the river) from (0, 1.3, -0.3); a small "
     "hand bell hangs from a bracket to call the ferry.", {}, []),
    ("drawbridge-lift", "Drawbridge, lifting section", 1500,
     "White timber Dutch drawbridge: a 2 m pier with the gantry on the near bank and a 6 m leaf that spans a 3-cell "
     "river, deck 0.9 m above the water (masts cannot pass while it is down). Raised to 80 degrees the leaf shows its "
     "dark underside and leaves a 4.4 m channel. Rotate leaf and beam by the same angle; move the chains with the beam "
     "tip to (0, 5.9 + 6 sin a, 2 - 6 cos a).",
     {"leaf": "hinge; rotate about X by +a (0 to 80 degrees) to raise",
      "beam": "pivot above the hinge; rotate about X by the same +a",
      "chains": "do not rotate; move with the beam tip",
      "signal": "arm pivot; rotate about Z by +90 degrees for vertical (boats' turn)"}, ["drawbridge-lift-raised"]),
    ("drawbridge-span", "Drawbridge fixed span", 400,
     "2 m module, 4 m wide, deck 0.9, on piles, for rivers wider than 6 m (boats still pass only under the leaf).", {}, []),
    ("drawbridge-end", "Drawbridge bank ramp", 500,
     "2 x 4 m ramp from the ground (+Z) to the deck, with the sill the leaf tip rests on; turn the far one 180 degrees.",
     {"barrier": "pole pivot; the pole lies across the ramp (closed to walkers); rotate about Z by +80 degrees to lift it"}, []),
    ("landing", "Landing", 500,
     "Short timber jetty from the bank (+Z) over the water: deck 2.4 x 6 m at 0.5 m, four tall mooring posts, a bollard "
     "with a coiled line, crates, sacks and a wool bale waiting for a boat.", {}, []),
    ("keeper-hut", "Bridge keeper's hut", 500,
     "Tiny limewashed hut under a steep thatch, a bell-cote with the warning bell on the ridge (a small slate cross-roof "
     "from above) and a lantern beside the door (+Z). Stands by the drawbridge ramp.", {}, []),
    ("ferry-rope", "Ferry rope", 50,
     "A 1 m rope piece from the origin to (0, 0, -1): place it at the near post's rope point, face the far post, scale Z "
     "by the distance.", {}, []),
]
RIVER_EXTRA_TITLE = {"barge-mast-lowered": "Mast lowered (pose preview)",
                     "drawbridge-lift-raised": "Raised to 80 degrees (pose preview)"}
river_cards = []
river_tris = 0
for name, title, bud, what, notes, extras in RIVER_MODELS:
    item = f"assets/models/{name}.glb"
    tris, lo, hi, nodes = glb_stats(ROOT / item)
    river_tris += tris
    sheet = ROOT / f"assets/src/contact-sheets/{name}.png"
    figs = ""
    if sheet.exists():
        img = put(f"assets/src/contact-sheets/{name}.png", f"models/{name}.png")
        figs += (f'<figure><img src="{img}" alt="Contact sheet of {E(title)}: top view, 45-degree views from south, east '
                 f'and north, and four side views" loading="lazy" width="1920" height="960"></figure>')
    for ex in extras:
        img = put(f"assets/src/contact-sheets/{ex}.png", f"models/{ex}.png")
        figs += (f'<figure><img src="{img}" alt="Contact sheet of {E(title)}, {E(RIVER_EXTRA_TITLE[ex])}" loading="lazy" '
                 f'width="1920" height="960"><figcaption class="muted" style="padding:6px 12px">{E(RIVER_EXTRA_TITLE[ex])}</figcaption></figure>')
    w, h, d = hi[0] - lo[0], hi[1], hi[2] - lo[2]
    below = f", reaches {m(-lo[1])} m below y = 0" if lo[1] < -0.01 else ""
    rows = ""
    for nn, nt, tr in node_info(ROOT / item):
        org = f"origin ({', '.join(m(v) for v in tr)})" if tr else "origin (0, 0, 0)"
        rows += f"<li><code>{E(nn)}</code> {nt} tris, {org}{'; ' + E(notes[nn]) if nn in notes else ''}</li>"
    node_list = f'<ul class="nodes">{rows}</ul>' if len(nodes) > 1 else ""
    river_cards.append(f"""
<article class="card model" id="{name}">
  {figs}
  <div class="body">
    <h3>{E(title)} <code>{name}.glb</code></h3>
    <p>{E(what)}</p>
    <dl class="nums">
      <dt>Footprint</dt><dd>{m(w)} x {m(d)} m (x by z)</dd>
      <dt>Height</dt><dd>{m(h)} m{below}</dd>
      <dt>Triangles</dt><dd>{tris} <span class="muted">of {bud}</span></dd>
    </dl>
    {node_list}
    {ledger_facts(item)}
  </div>
</article>""")
river_scene = put("assets/src/contact-sheets/river-scene.png", "models/river-scene.png")
river_scene_open = put("assets/src/contact-sheets/river-scene-open.png", "models/river-scene-open.png")

RIVER_ICONS = [("tool-ferry", "Ferry"), ("tool-drawbridge", "Drawbridge"), ("ui-raise-bridge", "Raise the bridge"),
               ("ui-lower-bridge", "Lower the bridge"), ("ui-hire-keeper", "Hire the keeper"), ("ui-coin", "Coin"),
               ("ui-boat", "Boat"), ("ui-reputation", "Reputation")]


def icon_row(name, label, gen):
    src = put(f"assets/icons/{name}.svg", f"icons/{name}.svg")
    cells = "".join(f'<img class="ico" src="{src}" width="{s}" height="{s}" alt="">' for s in (20, 40, 80))
    return (f'<li id="{name}"><div class="pair" role="img" aria-label="{E(label)} icon at 20, 40 and 80 px on Paper and on Ink">'
            f'<div class="on-paper">{cells}</div><div class="on-ink">{cells}</div></div>\n'
            f'<p><b>{E(label)}</b> <code>{name}.svg</code> <span class="muted">{E(LEDGER[f"assets/icons/{name}.svg"]["licence"])}, {gen}</span></p></li>')


river_icon_rows = [icon_row(n, l, "icons-river.py") for n, l in RIVER_ICONS]
river_icon_prev = put("assets/src/2d/previews/icons-river-preview.png", "icons/icons-river-preview.png")
_rr = LEDGER["assets/icons/tool-ferry.svg"]
river_icon_facts = (f'<dl class="facts"><dt>Source</dt><dd>{E(_rr["source"])}</dd><dt>Licence</dt><dd>{E(_rr["licence"])}</dd>'
                    f'<dt>Verified</dt><dd>{E(_rr["verified"])}, {E(_rr["date_checked"])}</dd></dl>')


# ------------------------------------------------------------------ Town set (incremental town, 2026-11-25)
TOWN_MODELS = [
    ("mill", "Post mill", 1500,
     "Timber post mill on a trestle with stone piers; door, ladder and tail pole at the back. The sails are their own "
     "node: it spins about its local Z axis at the hub.", {"Sails": "spins about local Z"}),
    ("bell-post", "Bell post", 500,
     "The town bell in a timber frame with a narrow thatch cap, for the green. The bell is its own node, pivoting at "
     "its hanger.", {"Bell": "swings about local X"}),
    ("plot-stakes", "Plot stakes", 300,
     "Four stakes and a pale cord around an 8 x 6 m plot, a red tag on the south-west stake. Every stake and cord is "
     "its own node, so the game moves the stakes to any plot and scales each cord along its own axis only.", {}),
    ("charter-stone", "Charter stone", 300,
     "A standing stone with a carved panel (a ring and three lines) on its front, on two footing stones.", {}),
    ("villager-cargo-water", "Cargo: Water", 100,
     "A yoke across the head with a bucket of water at each side. Attach at the top of the head like the other cargo; "
     "the buckets hang below the origin, so the contact sheet's ground hides them.", {}),
    ("villager-cargo-flour", "Cargo: Flour", 100, "A tall pale flour sack with a tied neck.", {}),
]


def town_card(name, title, bud, what, notes):
    item = f"assets/models/{name}.glb"
    tris, lo, hi, nodes = glb_stats(ROOT / item)
    figs = ""
    if (ROOT / f"assets/src/contact-sheets/{name}.png").exists():
        img = put(f"assets/src/contact-sheets/{name}.png", f"models/{name}.png")
        figs = (f'<figure><img src="{img}" alt="Contact sheet of {E(title)}: top view, 45-degree views from south, east '
                f'and north, and four side views" loading="lazy" width="1920" height="960"></figure>')
    w, h, d = hi[0] - lo[0], hi[1], hi[2] - lo[2]
    below = f", reaches {m(-lo[1])} m below y = 0" if lo[1] < -0.01 else ""
    rows = ""
    for nn, nt, tr in node_info(ROOT / item):
        org = f"origin ({', '.join(m(v) for v in tr)})" if tr else "origin (0, 0, 0)"
        rows += f"<li><code>{E(nn)}</code> {nt} tris, {org}{'; ' + E(notes[nn]) if nn in notes else ''}</li>"
    node_list = f'<ul class="nodes">{rows}</ul>' if len(nodes) > 1 else ""
    return tris, f"""
<article class="card model" id="{name}">
  {figs}
  <div class="body">
    <h3>{E(title)} <code>{name}.glb</code></h3>
    <p>{E(what)}</p>
    <dl class="nums">
      <dt>Footprint</dt><dd>{m(w)} x {m(d)} m (x by z)</dd>
      <dt>Height</dt><dd>{m(h)} m{below}</dd>
      <dt>Triangles</dt><dd>{tris} <span class="muted">of {bud}</span></dd>
    </dl>
    {node_list}
    {ledger_facts(item)}
  </div>
</article>"""


town_cards, town_tris = [], 0
for _n, _t, _b, _w, _no in TOWN_MODELS:
    _tr, _c = town_card(_n, _t, _b, _w, _no)
    town_tris += _tr
    town_cards.append(_c)
TOWN_ICONS = [("up-villager", "Villager"), ("up-pace", "Pace"), ("up-load", "Load"), ("up-pave", "Pave"),
              ("up-building", "Building"), ("ui-bell", "Bell"), ("ui-achievement", "Achievement"),
              ("ui-settings", "Settings"), ("ui-display", "Display"), ("ui-keys", "Keys"), ("ui-save", "Save"),
              ("ui-quit", "Quit"), ("ui-lock", "Locked"), ("ui-check", "Done"), ("good-water", "Water"),
              ("good-grain", "Grain"), ("good-flour", "Flour")]
town_icon_rows = [icon_row(n, l, "icons-town.py") for n, l in TOWN_ICONS]
town_icon_prev = put("assets/src/2d/previews/icons-town-preview.png", "icons/icons-town-preview.png")
_tw = LEDGER["assets/icons/ui-bell.svg"]
town_icon_facts = (f'<dl class="facts"><dt>Source</dt><dd>{E(_tw["source"])}</dd><dt>Licence</dt><dd>{E(_tw["licence"])}</dd>'
                   f'<dt>Verified</dt><dd>{E(_tw["verified"])}, {E(_tw["date_checked"])}</dd></dl>')

AGE2_MODELS = [
    ("pasture", "Sheep pasture", 500,
     "16 x 16 m like a field: low wattle fence with a hurdle gate in the south side, trough, hay rack, worn patches "
     "and grass tufts. Empty: the game scatters sheep inside (x, z within -6.6..6.6).", {}),
    ("sheep", "Sheep", 150, "One sheep, 1.5x like the villagers: white fleece, dark head and legs, head to +Z.",
     {"Head": "dips about its local X for grazing"}),
    ("weaver-cottage", "Weaver's cottage", 500,
     "8 x 6 m like cottage-02: plank walls and hipped thatch, an open lean-to on the east holding a floor loom with "
     "red cloth, a bench, wool and bolts of cloth.", {}),
    ("crane", "Treadwheel crane", 500, "Small treadwheel crane for the landing, post 4.4 m.",
     {"Jib": "turns about its local Y at the post top; at rest the arm points +Z"}),
    ("seal-stone", "Borough seal stone", 600,
     "The borough seal on a carved plinth on a two-step platform, 4 x 4 m: a red disc with a ship in relief, "
     "tilted back so it reads from above and from the front.", {}),
    ("tide-post", "Tide post", 200,
     "A marked oak post in the shallows: bands every 0.5 m, collars at whole metres; origin on the mean water level, "
     "the post reaches 1.25 m below it.", {}),
]
age2_cards, age2_tris = [], 0
for _n, _t, _b, _w, _no in AGE2_MODELS:
    _tr, _c = town_card(_n, _t, _b, _w, _no)
    age2_tris += _tr
    age2_cards.append(_c)
PROP_MODELS = [
    ("prop-woodpile", "Woodpile", 150, "Six split logs with pale end grain, a chopping block with an axe in it.", {}),
    ("prop-washing-line", "Washing line", 150, "Two posts 2 m apart and a line with three cloths folded over it.", {}),
    ("prop-flower-bed", "Flower bed", 150, "A raised bed in a plank edge, 1.8 x 1 m, six clumps of leaves and flowers.", {}),
    ("prop-hen-coop", "Hen coop", 150, "A plank coop on skids with a ramp, and two hens drawn 1.5x like the sheep.", {}),
    ("prop-bench", "Bench", 150, "A plank bench with a back rail, facing +Z.", {}),
    ("prop-beehives", "Beehives", 150, "Three banded straw skeps on a plank stand.", {}),
    ("prop-haystack", "Haystack", 150, "A round stack with a thatch cap and a ladder on its south side.", {}),
    ("prop-apple-tree", "Apple tree", 150, "Small round crown with red apples standing out of it and one windfall. Soft ink.", {}),
    ("prop-handcart", "Handcart", 150, "Two-wheeled cart parked with its shafts down to the south, a sack in it.", {}),
    ("prop-signpost", "Signpost", 150, "One post, two blank arms pointing different ways.", {}),
]
prop_cards, prop_tris = [], 0
for _n, _t, _b, _w, _no in PROP_MODELS:
    _tr, _c = town_card(_n, _t, _b, _w, _no)
    prop_tris += _tr
    prop_cards.append(_c)
AGE2_ICONS = [("good-wool", "Wool"), ("good-cloth", "Cloth"), ("ui-tide-high", "Tide high"), ("ui-tide-low", "Tide low"),
              ("ui-boat-berth", "Berth"), ("ui-crane", "Crane"), ("ui-ferry", "Ferry"), ("ui-bridge", "Bridge"),
              ("ui-toll", "Toll"), ("ui-request", "Request"), ("ui-hint", "Hint"), ("ui-extend", "Extend"),
              ("ui-tools", "Tools"), ("ui-value", "Value")]
age2_icon_rows = [icon_row(n, l, "icons-age2.py") for n, l in AGE2_ICONS]
age2_icon_prev = put("assets/src/2d/previews/icons-age2-preview.png", "icons/icons-age2-preview.png")
_a2 = LEDGER["assets/icons/good-wool.svg"]
age2_icon_facts = (f'<dl class="facts"><dt>Source</dt><dd>{E(_a2["source"])}</dd><dt>Licence</dt><dd>{E(_a2["licence"])}</dd>'
                   f'<dt>Verified</dt><dd>{E(_a2["verified"])}, {E(_a2["date_checked"])}</dd></dl>')


def sfx_card(stem, count, title, what, fmax):
    global n_audio
    rows = []
    suffixes = [f"{i:02d}" for i in range(1, count + 1)]
    for suf in suffixes:
        nm = f"{stem}-{suf}"
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
    return f"""
<article class="card sfx" id="{stem}">
  <div class="body">
    <h3>{E(title)} <code>{stem}-{suffixes[0]}..{suffixes[-1]}.wav</code></h3>
    <p>{E(what)} WAV, 48 kHz, 16-bit, mono. Spectrograms run 0 to {fmax} kHz.</p>
    {ledger_facts(f"assets/audio/sfx/{stem}-{suffixes[0]}.wav")}
  </div>
  <ol class="takes">{"".join(rows)}</ol>
</article>"""


RIVER_SFX = [
    ("drawbridge-raise", 3, "Drawbridge raising", "About 2.3 s: the winch pawl clicking steadily (8 to 9 a second), the timber leaf creaking under load, chains taking up slack, and the pawl locking with a clunk at the top.", 12),
    ("drawbridge-lower", 3, "Drawbridge lowering", "About 2.3 s: the drum freewheeling faster under the brake, chains paying out, creaks, and the leaf landing on its sill with a heavy thud at 2.0 s.", 12),
    ("boat-call", 6, "Boat calling for the bridge or ferry", "01 to 03: a small hand bell shaken two or three times (hum D5, A4, E5); 04 to 06: a short cow-horn call on A3, D4 and G3.", 6),
    ("coin", 6, "Trade income", "One to three small coins clinking, under half a second, the lowest partials in the 2 to 4 kHz UI lane, so the game can quantise and stack them.", 12),
    ("ferry-rope", 4, "Ferry rope pull", "A low stick-slip creak of the rope on its guide, a soft rasp through the hands and a hollow plank knock.", 12),
    ("oar-splash", 6, "Oar stroke and wake", "01 to 04: a thole-pin creak, then the blade dipping with a slap, slosh and bubbles; 05 and 06: bigger wake washes against the bank.", 12),
    ("boat-turn-away", 3, "Boat turns back", "Two muffled low bell strikes falling by a third (G3 to E3, D4 to B3, C4 to A3): a disappointed sigh, not an alarm.", 4),
]
river_sfx = [sfx_card(*r) for r in RIVER_SFX]

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
nav.jump{font-size:.85rem;display:flex;flex-wrap:wrap;gap:4px 12px;color:var(--ink-soft)}
nav.jump b{font-weight:600;color:var(--ink-soft);margin-right:2px}
a.top{font-size:.82rem}
.changes{background:var(--sheet);border:1px solid var(--rule);border-left:4px solid var(--brick);border-radius:3px;padding:12px 16px}
.changes h2{font-size:1.25rem}
.changes ul{margin:8px 0 0;padding-left:20px;display:grid;gap:4px;font-size:.93rem}
.listen{background:var(--tag-bg);border-radius:3px;padding:10px 14px;margin:0;font-size:.95rem}
[id]{scroll-margin-top:12px}
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
.ico{display:block;flex:none;max-width:none}
.pair>div{flex-wrap:wrap}
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
.nodes{margin:0;padding-left:18px;font-size:.86rem;display:grid;gap:2px}
.river-new{border-left-color:var(--river)}
footer{margin-top:56px;border-top:2px solid var(--ink);padding-top:16px;font-size:.88rem;color:var(--ink-soft);display:grid;gap:6px}
footer p{margin:0}
@media (prefers-reduced-motion: reduce){*{scroll-behavior:auto}}
""".replace("PALTOKENS", pal_tokens).replace("FONTFACES", "\n".join(font_faces))

def jump(items):
    links = "".join(f'<a href="#{i}">{E(t)}</a>' for i, t in items)
    return f'<nav class="jump" aria-label="Jump to"><b>Jump to:</b>{links}<a class="top" href="#top">Back to top</a></nav>'


JUMP_MODELS = jump([(n, t) for n, t, _ in MODELS])
JUMP_TEX = jump([("tex-" + n, t) for n, t, _, _ in TEX] + [("wear-ramp", "Wear ramp"), ("palette", "Palette")])
JUMP_ICONS = jump([(n, t) for n, t in ICONS])
JUMP_FONTS = jump([("font-" + fn.split(".")[0], lbl.split(" (")[0]) for fn, lbl, *_ in FONTS])
JUMP_SFX = jump([(st, t) for st, _, t, _, _ in SFX])
JUMP_TOWN = jump([(n, t) for n, t, *_ in TOWN_MODELS] + [("town-icons", "Icons")])
JUMP_RIVER = jump([(n, t) for n, t, *_ in RIVER_MODELS] + [("river-icons", "Icons"), ("river-sfx", "Sounds")])
JUMP_LOOPS = jump([(st, t) for _, st, t, _, _, _ in LOOPS] + [("score", "Score data")])
CHANGES = [
    "Icons redrawn in one language: crisp closed shapes, Paper fill inside a 2-unit Ink outline, one stroke weight. The same files now read on Paper and on Ink; this page shows the real SVGs (no recolouring mask). The footbridge icon is a bold deck with a hand rail over water.",
    "Weeping willow rebuilt with a curtain of hanging fronds (224 triangles).",
    "Small hut (cottage-03) has a gabled roof with crossed gable horns, so it differs from the longhouses from above.",
    "Footbridge rails end exactly 1.2 m above the deck; the pipeline table now says rails are measured from the deck.",
    "Reeds rebuilt as thin upright stems with dark seed heads; villager has a pack on the back as a facing cue from above.",
    "Trees, fields and reeds use a soft ink pass (creases inked only above 1.5 instead of 0.45, by stencil), so they carry no more ink than the houses; field furrows are low-pitched and no longer go near-black from the side.",
    "Hollow way is now a dark wet lane with puddles and no ink specks; trampled grass is isotropic; grass has smaller patches so the 4 x 4 repeat is less obvious. The wear ramp's blends are documented as intentional.",
    "Bells: six 3 s gameplay strikes on D4, E4, G4 and A4 with 2 to 3 kHz strike partials (highest correlation between takes 0.53, was 0.89), plus four long 6 s strikes as named variants.",
    "Place has 2 to 4 kHz content (13 to 17 % of its energy, was under 1 %); erase is about 0.3 s (was 0.5 s).",
    "Theme: 45 s loop (the 8 bars twice, with a variation); the seam level step is 0.5 dB (was 9 dB).",
    "Housekeeping: asset sync keeps Godot's import files (rsync installed, and the fallback keeps them too); generators no longer rewrite tracked ledger files on every rebuild; this page has a jump list in each section.",
]
CHANGES_HTML = "".join(f"<li>{E(c)}</li>" for c in CHANGES)

PAGE = f"""<title>Tide &amp; Toll Asset Gallery</title>
<meta name="claude:viewport-fit-sides" content="cover">
<style>{CSS}</style>
<div class="wrap" id="top">
<header class="title">
  <p class="eyebrow">Tide &amp; Toll &middot; Hamlet era, about 1100 &middot; review copy, 2026-11-25</p>
  <h1>The Hamlet asset set</h1>
  <p class="lead">Every model, texture, icon, font and sound made for the first era, now with the River set for the river prototype, with how each was made and under what licence. Everything except the fonts was generated in code by scripts under <code>assets/src/</code>; nothing came from a paid or unclear source.</p>
  <ul class="counts"><li><b>{n_files}</b> files in the ledger</li><li>{count_line}</li></ul>
  <nav class="toc" aria-label="Sections"><a href="#age2">Second age</a><a href="#town">Town set</a><a href="#river">River set</a><a href="#models">Models</a><a href="#ground">Ground and textures</a><a href="#icons">Icons</a><a href="#fonts">Fonts</a><a href="#sfx">Sound effects</a><a href="#theme">Theme and river</a><a href="#review">For review</a></nav>
  <aside class="changes river-new" id="new-river"><h2>New: the River set</h2><p class="muted">Third round, 2026-11-25, for the river prototype (boats, crossings, trade).</p><ul><li>{len(RIVER_MODELS)} models ({river_tris:,} triangles): masted barge with a lowering mast and a cargo node, rope ferry (raft, posts, rope), Dutch drawbridge with a hinged leaf, balance beam, chains and signal arm, its span and ramp with a barrier, the landing and the keeper's hut.</li><li>{len(RIVER_ICONS)} icons in the house language; {sum(r[1] for r in RIVER_SFX)} sounds in {len(RIVER_SFX)} families.</li><li>The six free asset sites are blocked from the build container (checked 2026-11-25), so everything was made in code; every file has a ledger row.</li></ul></aside>
  <aside class="changes" id="changes"><h2>Changed since the first version</h2><p class="muted">Second round, after the art and audio review.</p><ul>{CHANGES_HTML}</ul></aside>
</header>

<section id="age2">
  <header><h2>Second age: The River (town)</h2>
  <p>New for the second age (2026-11-25, docs/plans/age2-design.md): the pasture and its sheep, the weaver's cottage, the landing's crane, the borough seal stone and the tide post, and {len(AGE2_ICONS)} icons. {len(AGE2_MODELS)} models, {age2_tris:,} triangles. Built by <code>assets/src/models/river-town.py</code> and <code>assets/src/2d/icons-age2.py</code>; the free asset sites are blocked from the build container, so all of it was made in code.</p></header>
  <div class="grid">{"".join(age2_cards)}</div>
  <h3 id="age2-icons">Icons</h3>
  <ul class="icons">{"".join(age2_icon_rows)}</ul>
  <figure class="wide"><img src="{age2_icon_prev}" alt="The second-age icons at 20, 40 and 80 px, on Paper above and on Ink below, rendered by Godot" loading="lazy" width="1330" height="392"><figcaption>The same files rendered by Godot at 20, 40 and 80 px, on Paper and on Ink.</figcaption></figure>
  <article class="card"><div class="body"><h3>Ledger, the same for all second-age icons</h3>{age2_icon_facts}</div></article>
</section>

<section id="props">
  <header><h2>Town props</h2>
  <p>Small props the town dresses itself with as it prospers (2026-11-26). {len(PROP_MODELS)} models, {prop_tris:,} triangles, each under 150. Built by <code>assets/src/models/town-props.py</code>; the free asset sites are blocked from the build container, so all of it was made in code.</p></header>
  <div class="grid">{"".join(prop_cards)}</div>
</section>

<section id="town">
  <header><h2>Town set</h2>
  <p>New for the incremental town (2026-11-25): the mill, the bell post, plot stakes, the charter stone, Water and Flour cargo, and {len(TOWN_ICONS)} icons for upgrades, the book's menus and the three goods. {len(TOWN_MODELS)} models, {town_tris:,} triangles. Built by <code>assets/src/models/town.py</code> and <code>assets/src/2d/icons-town.py</code>; the free asset sites are blocked from the build container, so all of it was made in code.</p>{JUMP_TOWN}</header>
  <div class="grid">{"".join(town_cards)}</div>
  <h3 id="town-icons">Icons</h3>
  <ul class="icons">{"".join(town_icon_rows)}</ul>
  <figure class="wide"><img src="{town_icon_prev}" alt="The town icons at 20, 40 and 80 px, on Paper above and on Ink below, rendered by Godot" loading="lazy" width="1612" height="392"><figcaption>The same files rendered by Godot at 20, 40 and 80 px, on Paper and on Ink.</figcaption></figure>
  <article class="card"><div class="body"><h3>Ledger, the same for all town icons</h3>{town_icon_facts}</div></article>
</section>

<section id="river">
  <header><h2>River set</h2>
  <p>Everything the river prototype needs: boats, crossings and their moving parts, icons and sounds. Same Living Map style and palette atlas as the Hamlet set; built by <code>assets/src/models/barge.py</code>, <code>ferry.py</code>, <code>drawbridge.py</code>, <code>landing.py</code>, <code>keeper-hut.py</code>, <code>assets/src/2d/icons-river.py</code> and six generators in <code>assets/src/audio/</code>. Nodes that move carry a translation in the file: their origin is the hinge or pivot, so the game rotates the node itself.</p>{JUMP_RIVER}</header>
  <figure class="wide"><img src="{river_scene}" alt="The River set at true scale by a 6 m river: drawbridge lowered with a barge waiting, ferry, landing with a rowboat, keeper's hut" width="2880" height="720"><figcaption>Bridge down: the barge waits west of the drawbridge; ferry on its rope, the landing with a moored rowboat, the keeper's hut (preview scene built by <code>assets/src/models/_river_scene.py</code>).</figcaption></figure>
  <figure class="wide"><img src="{river_scene_open}" alt="The same scene with the drawbridge raised and the barge passing through" loading="lazy" width="2880" height="720"><figcaption>Bridge up: leaf and beam at 80 degrees, signal arm vertical, barriers down, the barge in the gap.</figcaption></figure>
  <div class="grid">{"".join(river_cards)}</div>
  <h3 id="river-icons">Icons</h3>
  <ul class="icons">{"".join(river_icon_rows)}</ul>
  <figure class="wide"><img src="{river_icon_prev}" alt="The eight river icons at 20, 40 and 80 px, on Paper above and on Ink below, rendered by Godot" loading="lazy" width="766" height="392"><figcaption>The same files rendered by Godot at 20, 40 and 80 px, on Paper and on Ink.</figcaption></figure>
  <article class="card"><div class="body"><h3>Ledger, the same for all eight river icons</h3>{river_icon_facts}</div></article>
  <h3 id="river-sfx">Sounds</h3>
  <p class="listen"><b>Measured, not yet listened to.</b> Judged from spectrograms, loudness and peak numbers only.</p>
  <div class="grid">{"".join(river_sfx)}</div>
</section>

<section id="models">
  <header><h2>Models</h2>
  <p>{len(MODELS)} glTF binary models, {total_tris:,} triangles in all. One cell is 2 m; front faces +Z (south). Each contact sheet shows, top row: from above, then tilted 45 degrees from the south, east and north; bottom row: low side views at yaw 0, 90, 180 and 270. Triangle budgets are from the pipeline guide.</p>{JUMP_MODELS}</header>
  <figure class="wide"><img src="{scene}" alt="All Hamlet models together at true scale" width="2880" height="720"><figcaption>All models together at true scale (preview scene built by <code>assets/src/models/_scene.py</code>).</figcaption></figure>
  <div class="grid">{"".join(model_cards)}</div>
</section>

<section id="ground">
  <header><h2>Ground and textures</h2>
  <p>512 px tiles, shown here tiled 2 x 2 so seams would show. Every pixel is an exact palette colour except in the wear ramp, whose blends are soft on purpose. The five ground tiles are the wear stages a path passes through as villagers walk it.</p>{JUMP_TEX}</header>
  <figure class="wide"><img src="{ground_prev}" alt="Ground preview: the five wear stages, a trail crossing grass, water, paper, a tiled grass sample and the wear ramp" loading="lazy" width="1536" height="1088"><figcaption>Generator preview: the five stages side by side, a trail crossing grass, water, paper, a 2 x 2 grass tiling and the ramp.</figcaption></figure>
  <figure class="wide"><img src="{ground_4x4}" alt="The five ground tiles, each laid 4 x 4 at quarter size, to show how visible the repeat is" loading="lazy" width="2624" height="512"><figcaption>Repeat check: grass, trampled, trail, track and hollow way, each tiled 4 x 4.</figcaption></figure>
  <div class="grid">{"".join(tex_cards)}</div>
  <article class="card" id="wear-ramp"><div class="body">
    <h3>Wear ramp <code>wear-ramp.png</code></h3>
    <div class="ramp"><img src="{ramp}" alt="Wear ramp from Meadow through Bent grass, Trail earth, Packed earth to Mud"><div class="scale"><span>0</span><span>0.10</span><span>0.30</span><span>0.55</span><span>0.80</span><span>1.00</span></div></div>
    <p>256 x 8; u is wear from 0 to 1. Colours blend over 0.035 either side of each stage boundary. Import with linear filtering, clamp, no mipmaps.</p>
    <p><b>Not snapped to the palette, on purpose.</b> It is a lookup gradient for the wear shader, not a picture: the five plateaus are exact palette colours and only the narrow transitions (63 colours) are interpolated, so wear changes smoothly instead of popping a stage. Recorded in the generator and in the pipeline guide.</p>
    {ledger_facts("assets/textures/wear-ramp.png")}
  </div></article>
  <article class="card" id="palette"><div class="body"><h3>Palette</h3><ul class="palette">{pal_chips}</ul></div></article>
</section>

<section id="icons">
  <header><h2>Icons</h2>
  <p>{len(ICONS)} SVGs on a 24-unit grid in one drawing language: closed shapes with a Paper fill inside a 2-unit Ink outline, one stroke weight. Each is the real file, shown at 20, 40 and 80 px on Paper and on Ink with no recolouring. Placement tools are named <code>tool-*</code>, the rest <code>ui-*</code>.</p>{JUMP_ICONS}</header>
  <ul class="icons">{"".join(icon_rows)}</ul>
  <figure class="wide"><img src="{icon_prev}" alt="All 14 icons at 20, 40 and 80 px, on Paper above and on Ink below, rendered by Godot from the same files" loading="lazy" width="1386" height="392"><figcaption>The same files rendered by Godot (the game's SVG renderer) at 20, 40 and 80 px, on Paper and on Ink.</figcaption></figure>
  <article class="card"><div class="body"><h3>Ledger, the same for all 14 icons</h3>{icon_facts}</div></article>
</section>

<section id="fonts">
  <header><h2>Fonts</h2>
  <p>Three OFL families, unmodified TTF files from github.com/google/fonts. Each OFL text is saved in <code>assets/licences/</code> and ships with the game.</p>{JUMP_FONTS}</header>
  <div class="grid">{"".join(font_rows)}</div>
</section>

<section id="sfx">
  <header><h2>Sound effects</h2>
  <p>Synthesised one-shots, played round-robin by the game with small random changes in level and pitch. Each take shows its waveform above a spectrogram. Loudness is the loudest 400 ms block; peak is true peak.</p>
  <p class="listen"><b>Measured, not yet listened to.</b> Nobody has listened to these sounds; they were judged from spectrograms, loudness and peak numbers only.</p>{JUMP_SFX}</header>
  <div class="grid">{"".join(sfx_sections)}</div>
</section>

<section id="theme">
  <header><h2>Theme and river</h2>
  <p>The two loops. Both are seamless by construction; the seam pictures show the end of the file joined back to its start. The theme also exists as a 16-bit WAV master and as score data (<code>theme-hamlet-draft-score.json</code>), not played here.</p>
  <p class="listen"><b>Measured, not yet listened to.</b> The theme most needs a listening pass.</p>{JUMP_LOOPS}</header>
  <div class="grid">{"".join(loop_cards)}</div>
  <article class="card" id="score"><div class="body"><h3>Theme score data <code>theme-hamlet-draft-score.json</code></h3><p>Key, mode, metre, tempo, bell motif and every note of the pipe, drum, lyre and drone parts.</p>{score_note}</div></article>
</section>

<section id="review">
  <header><h2>For review</h2></header>
  <ul class="notes">
    <li>Nobody has listened to the audio yet; tone and mix were judged from spectrograms and loudness numbers only. The theme most needs a listening pass.</li>
    <li>Fields still show one ink line per row in the lowest side views across the rows (silhouettes, not creases), but no longer go near-black.</li>
    <li>The soft ink pass needs the game's outline to be drawn twice with a stencil test (as the contact-sheet tool does); see <code>assets/src/common/outline_screen.gdshader</code>.</li>
    <li>Alegreya was added for headings beyond the two faces asked for; drop its file and row if only two are wanted.</li>
    <li>Icons now carry their own Paper fill, so they work on light and dark grounds as they are; a tinted state (hover, disabled) can still use modulate.</li>
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
