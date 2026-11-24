# Ledger fragments

While builders work in parallel, nobody edits `assets/LEDGER.csv`. Each group writes its rows to its own fragment here, `assets/src/ledger-rows/<group>.csv` (`models.csv`, `textures.csv`, `icons.csv`, `fonts.csv`, `audio.csv`): the same eight columns as the ledger (`item,kind,source,licence,url,used_in,date_checked,verified`), **no header**, one row per file. A later step merges them into `assets/LEDGER.csv` with `python3 tools/merge-ledger-rows.py` and empties the fragments.

- Generators write their rows with `common.ledger.add(...)` (see `assets/src/README.md`); hand-made or downloaded files (fonts, licence-checked downloads) get a hand-written row in the same fragment.
- Check everything, fragments included: `python3 tools/check-ledger.py --fragments` (`tools/build-assets.sh` runs it).
- Only write to your own group's fragment.
