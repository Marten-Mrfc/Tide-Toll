#!/usr/bin/env bash
# Rebuilds every generated asset, then checks the ledger.
#
#   tools/build-assets.sh                 palette, then every generator in every group under assets/src/
#   tools/build-assets.sh models audio    palette, then only these groups (assets/src/models, assets/src/audio)
#   tools/build-assets.sh --sheets        also render a contact sheet (with side views) for every assets/models/*.glb
#                                         into build/contact-sheets/ (serialised; about 10 s each)
#   tools/build-assets.sh --no-check      skip the ledger check
#
# A group is a folder directly under assets/src/ (models, textures, icons, audio, ...); its generators are the
# *.py files directly in it (not in sub-folders, and not names starting with "_"), run in name order from the
# repository root with PYTHONPATH=assets/src, so `from common import ...` works. Executable *.sh files there
# run too. A group with no generators yet is skipped. Not groups: common, ledger-rows, contact-sheets.
# The ledger check counts the not-yet-merged fragment rows in assets/src/ledger-rows/ (check-ledger.py --fragments).
# Exits non-zero if a generator or the ledger check fails (every generator still runs).
set -uo pipefail
ROOT=$(cd "$(dirname "$0")/.." && pwd)
cd "$ROOT"
export PYTHONPATH="$ROOT/assets/src${PYTHONPATH:+:$PYTHONPATH}"
export PYTHONDONTWRITEBYTECODE=1
CHECK=1; SHEETS=0; GRPS=()
for a in "$@"; do
  case "$a" in
    --no-check) CHECK=0 ;;
    --sheets) SHEETS=1 ;;
    -h|--help) sed -n '2,17p' "$0"; exit 0 ;;
    --*) echo "unknown option $a"; exit 2 ;;
    *) GRPS+=("${a%/}") ;;
  esac
done
if [ ${#GRPS[@]} -eq 0 ]; then
  for d in assets/src/*/; do
    g=$(basename "$d")
    case "$g" in common|ledger-rows|contact-sheets|__pycache__) continue ;; esac
    GRPS+=("$g")
  done
fi

FAILED=0; RAN=0
run() {  # run GENERATOR
  local t0=$SECONDS
  echo "-- $1"
  if [[ "$1" == *.py ]]; then python3 "$1"; else "./$1"; fi
  local rc=$?
  RAN=$((RAN + 1))
  if [ $rc -ne 0 ]; then echo "   FAILED ($1, exit $rc)"; FAILED=1; else echo "   ok ($((SECONDS - t0)) s)"; fi
}

run assets/src/common/palette.py
for g in ${GRPS[@]+"${GRPS[@]}"}; do
  dir="assets/src/$g"
  if [ ! -d "$dir" ]; then echo "-- group $g: no folder $dir (skipped)"; continue; fi
  shopt -s nullglob
  gens=()
  for f in "$dir"/*.py "$dir"/*.sh; do
    b=$(basename "$f")
    [[ "$b" == _* ]] && continue
    [[ "$f" == *.sh && ! -x "$f" ]] && continue
    gens+=("$f")
  done
  shopt -u nullglob
  if [ ${#gens[@]} -eq 0 ]; then echo "-- group $g: no generators yet (skipped)"; continue; fi
  IFS=$'\n' gens=($(printf '%s\n' "${gens[@]}" | sort)); unset IFS
  for f in "${gens[@]}"; do run "$f"; done
done

if [ "$SHEETS" = 1 ]; then
  shopt -s nullglob
  for m in assets/models/*.glb assets/models/*/*.glb; do
    tools/render-contact-sheet.sh "$m" --sides || FAILED=1
  done
  shopt -u nullglob
fi

if [ "$CHECK" = 1 ]; then
  echo "-- ledger (with fragments)"
  python3 tools/check-ledger.py --fragments || FAILED=1
fi
echo
if [ "$FAILED" = 0 ]; then echo "BUILD ASSETS PASSED ($RAN generators)"; else echo "BUILD ASSETS FAILED"; fi
exit $FAILED
