#!/usr/bin/env bash
# Re-fetches the UI fonts (OFL-1.1) from github.com/google/fonts and checks them against the hashes recorded on
# 2026-11-24. Not run by tools/build-assets.sh (leading underscore: builds stay offline). Run by hand:
#   assets/src/2d/_fetch_fonts.sh            -> assets/fonts/*.ttf, assets/licences/*-OFL.txt
# Fonts ship unmodified (Uncial Antiqua has a Reserved Font Name). The @fontsource npm packages (5.3.0, OFL-1.1)
# hold the same families but only as WOFF2; the pipeline guide asks for TTF, so the google/fonts files are used.
set -euo pipefail
ROOT=$(cd "$(dirname "$0")/../../.." && pwd)
B=https://raw.githubusercontent.com/google/fonts/main/ofl
TMP=$(mktemp -d)
get() { curl -sfL -o "$TMP/$2" "$B/$1"; }
get atkinsonhyperlegible/AtkinsonHyperlegible-Regular.ttf AtkinsonHyperlegible-Regular.ttf
get atkinsonhyperlegible/AtkinsonHyperlegible-Bold.ttf AtkinsonHyperlegible-Bold.ttf
get "alegreya/Alegreya%5Bwght%5D.ttf" Alegreya-Variable.ttf
get uncialantiqua/UncialAntiqua-Regular.ttf UncialAntiqua-Regular.ttf
get atkinsonhyperlegible/OFL.txt AtkinsonHyperlegible-OFL.txt
get alegreya/OFL.txt Alegreya-OFL.txt
get uncialantiqua/OFL.txt UncialAntiqua-OFL.txt
(cd "$TMP" && sha256sum -c --quiet <<'EOF'
7fb917c89019896d0b52ee84b7cbb3304c18cb90b19a62f5e32712bd23e97669  AtkinsonHyperlegible-Regular.ttf
5a3b0c8cc8ca545155150b4512a1fa248298df121c50d6557e651e61fbdab92f  AtkinsonHyperlegible-Bold.ttf
ba5564634b93a8f8ba57b48cd4f1ae7417d2b4656fbac779028679b00de3cf12  Alegreya-Variable.ttf
33a5128b59d1c95d4f3788164f4ab3a1196f0a982263cb2cd278c47418366766  UncialAntiqua-Regular.ttf
EOF
) || echo "WARNING: upstream fonts changed since 2026-11-24; re-check the licence and the ledger rows"
mkdir -p "$ROOT/assets/fonts" "$ROOT/assets/licences"
mv "$TMP"/*.ttf "$ROOT/assets/fonts/"
mv "$TMP"/*-OFL.txt "$ROOT/assets/licences/"
rmdir "$TMP"
echo "fonts fetched into assets/fonts/"
