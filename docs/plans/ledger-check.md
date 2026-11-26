# Ledger and licence check (25 and 26 November 2026)

Started 2026-11-26 01:09 UTC. Read-only check; the only file written is this one. No git write commands were run.

## Verdict

**Clean for the 182 rows dated 2026-11-25. Two items need the lead's attention before the owner's zero-budget rule can be called fully met:**

1. **No ledger rows are dated 2026-11-26**, yet two things under `assets/` were committed on 26 November: `assets/src/models/town-props.py` (its 10 ledger rows say 2026-11-25) and 10 contact-sheet PNGs in `assets/src/contact-sheets/`. Check whether any 26 November asset is missing a row. The checker found none, but it only looks at shipped asset files.
2. **101 of the 25 November rows name free asset sites** (kenney.nl, quaternius.com, polyhaven.com, ambientcg.com, opengameart.org, freesound.org) in their `source` field. They say these sites were blocked from the build container, so the asset was made in code. No row or generator downloads from them. Listed below for a wording decision, not a licence problem.

## 1. Checker output

`python3 tools/check-ledger.py` exit code 0. Last line:

```
ledger: 307 rows, 281 asset files checked, 0 problems
```

## 2. Rows dated 2026-11-25 (182 rows)

- Licence field: all 182 read `self-made`. None has a URL.
- Files: all 182 `item` paths exist.
- Generators: all 182 `source` fields name a generator under `assets/src/` that exists. 30 distinct generators are used: `2d/icons-age2.py`, `2d/icons-river.py`, `2d/icons-town.py`, 19 under `audio/`, and 8 under `models/`.
- Network and download search across the generators committed on 25 and 26 November (36 files): no real hits for `http(s)://` fetches, `urllib`, `requests`, `wget`, `urlopen`, `socket`, `subprocess`, `os.system`, `pip`, `npm` or `git clone`. The only matches were benign: the SVG namespace string `http://www.w3.org/2000/svg` in `assets/src/2d/icons.py`, and the word "curls" in `assets/src/2d/icons-age2.py`.
- File reads: all reads go to `assets/` or `build/`, both inside the project. `sys.path` is set to `assets/src/` in each generator. No reads from outside `assets/` or `assets/src/`.
- Mentions of third-party sites: 101 rows (39 icon, 31 sound, 31 model) carry the sentence "free asset sites blocked from the build container (checked 2026-11-25)". The audio and model generators have a `BLOCKED` constant with the same list of six sites. They are strings that record why nothing was downloaded. Nothing is fetched from them. Wording decision for the lead: keep or trim.
- Generator `assets/src/models/town-props.py` was first committed 2026-11-26 (commit `3e7906f`), but its 10 rows are dated 2026-11-25. Probably the rows were written by an earlier version of the script. Not a licence issue; the dates disagree.

## 3. Files under `assets/` not in the ledger

`tools/check-ledger.py` exempts `assets/LEDGER.csv`, everything under `assets/src/`, and everything under `assets/licences/`. Outside those, there are **no unledgered files** in `assets/`.

Inside the exempt areas, these are not in the ledger:

- `assets/LEDGER.csv` itself.
- `assets/licences/Alegreya-OFL.txt`, `AtkinsonHyperlegible-OFL.txt`, `UncialAntiqua-OFL.txt`. These are the font licence texts. Dated 2026-11-24.
- `assets/src/contact-sheets/*.png`: 59 PNGs. Dated 25 Nov (24 files), 26 Nov (10 files) and 24 Nov (25 files). Generator output, not shipped. `assets/src/common/glb.py` says the sheets come from `tools/render-contact-sheet.sh`. Not checked row by row.
- `assets/src/2d/previews/*.png` (6 files). Generator output, dated 24 and 25 Nov.
- Source and config files with no row: `assets/src/README.md`, `assets/src/ledger-rows/README.md`, `assets/src/common/palette.json`, `assets/src/common/outline_screen.gdshader`, `assets/src/common/.gitignore`, `assets/src/2d/_fetch_fonts.sh`. All dated 24 Nov.
- `__pycache__` folders under `assets/src/`. Build residue; not committed content.

## 4. Fonts

Four font files in `assets/fonts/`, each with a ledger row dated 2026-11-24, all licence `OFL-1.1`:

| File | SHA-256 matches the hashes in `_fetch_fonts.sh` | Licence text |
|---|---|---|
| `Alegreya-Variable.ttf` | yes (ba5564...de3cf12) | `assets/licences/Alegreya-OFL.txt`, says SIL OFL 1.1 |
| `AtkinsonHyperlegible-Regular.ttf` | yes (7fb917...e97669) | `assets/licences/AtkinsonHyperlegible-OFL.txt`, says SIL OFL 1.1 |
| `AtkinsonHyperlegible-Bold.ttf` | yes (5a3b0c...bdab92f) | same file as above |
| `UncialAntiqua-Regular.ttf` | yes (33a512...366766) | `assets/licences/UncialAntiqua-OFL.txt`, says SIL OFL 1.1 |

Notes for the lead:

- The licence field says `OFL-1.1` (SPDX form), not "SIL Open Font License" spelled out. The licence texts do say SIL OFL 1.1. Acceptable under the rule.
- The fonts were downloaded from `github.com/google/fonts` by `assets/src/2d/_fetch_fonts.sh` on 24 November, not made in code. The owner's "made in code" rule is for the 25 and 26 November additions, so this is outside it. The licence is permitted, so this is noted, not flagged.
- `UncialAntiqua` has a Reserved Font Name ("Uncial Antiqua"). The ledger row says the file is unmodified. Shipping it unmodified is the condition.
- Nothing else third-party is in `assets/` apart from the 26 `npm`, `nuget` and `tool` rows, which are not asset files (see note below).

Ledger rows whose `item` is not a file: 26. All are `npm-dev-dependency`, `nuget` or `tool` (for example `numpy`, `xunit`, `GodotSharp`, `typescript`, `esbuild`). Expected, since they are code dependencies, not assets.

## 5. Row counts by kind

| Kind | 24 Nov | 25 Nov | 26 Nov |
|---|---:|---:|---:|
| model | 24 | 31 | 0 |
| icon | 14 | 39 | 0 |
| sound | 44 | 100 | 0 |
| music | 3 | 11 | 0 |
| ambience | 1 | 1 | 0 |
| texture | 9 | 0 | 0 |
| font | 4 | 0 | 0 |
| nuget | 18 | 0 | 0 |
| npm-dev-dependency | 4 | 0 | 0 |
| tool | 4 | 0 | 0 |
| **Total** | **125** | **182** | **0** |

## Items for the lead

1. Confirm there is nothing to ledger for 26 November: `town-props.py` (committed 26 Nov, rows dated 25 Nov) and the 10 contact-sheet PNGs committed 26 Nov.
2. Decide whether the "free asset sites blocked" wording in 101 rows and in the generators' `BLOCKED` constants should stay. It names third-party sites but does not use them.
3. Decide whether contact sheets and previews under `assets/src/` need ledger rows. `check-ledger.py` exempts them.
