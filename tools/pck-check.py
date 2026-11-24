#!/usr/bin/env python3
"""Lists a Godot 4 .pck (format 2 or 3) and checks that the exported game holds its assets.

    tools/pck-check.py build/windows/TideToll.pck game

For every asset under game/assets (models, textures, icons, fonts, audio) that the export preset does not exclude,
the pack must hold its .import file and the imported data it points to. Prints a summary by kind and exits 1 if anything is
missing. Uses only the standard library.
"""
import os
import struct
import sys
from collections import defaultdict


def read_pck(path):
    with open(path, "rb") as f:
        d = f.read()
    if d[:4] != b"GDPC":
        raise SystemExit(f"{path}: not a Godot pack")
    fmt, major, minor, patch, flags = struct.unpack_from("<5I", d, 4)
    if fmt >= 3:
        base, diroff = struct.unpack_from("<QQ", d, 24)
    else:
        base, diroff = 0, 4 + 4 * 4 + 16 * 4
    if flags & 1:
        raise SystemExit("encrypted directory: cannot list")
    off = diroff
    (count,) = struct.unpack_from("<I", d, off)
    off += 4
    files = {}
    for _ in range(count):
        (n,) = struct.unpack_from("<I", d, off)
        off += 4
        name = d[off:off + n].rstrip(b"\0").decode()
        off += n
        _, size = struct.unpack_from("<QQ", d, off)
        off += 16 + 16 + 4
        files[name.removeprefix("res://")] = size
    return (major, minor, patch), files


def excluded(rel, preset):
    import fnmatch
    for line in open(preset, encoding="utf-8"):
        if line.startswith("exclude_filter="):
            pats = [p.strip() for p in line.split("=", 1)[1].strip().strip('"').split(",") if p.strip()]
            return any(fnmatch.fnmatch(rel, p) for p in pats)
    return False


def main():
    pck, game = sys.argv[1], sys.argv[2]
    ver, files = read_pck(pck)
    imported = defaultdict(list)
    for name in files:
        if name.startswith(".godot/imported/"):
            imported[name[len(".godot/imported/"):].rsplit("-", 1)[0]].append(name)
    kinds = {"models": (".glb",), "textures": (".png",), "icons": (".svg",), "fonts": (".ttf", ".otf"), "audio": (".wav", ".ogg")}
    missing, summary = [], []
    for kind, exts in kinds.items():
        root = os.path.join(game, "assets", kind)
        n = size = 0
        for dirpath, _, names in os.walk(root):
            for fn in sorted(names):
                if not fn.endswith(exts) or "_palette" in fn:
                    continue
                rel = os.path.relpath(os.path.join(dirpath, fn), game).replace(os.sep, "/")
                if excluded(rel, os.path.join(game, "export_presets.cfg")):
                    continue
                if rel + ".import" not in files:
                    missing.append(rel + ".import")
                data = imported.get(fn, [])
                if not data:
                    missing.append(f"{rel} (imported data)")
                n += 1
                size += sum(files[x] for x in data) + files.get(rel, 0)
        summary.append(f"{kind} {n} ({size / 1024:.0f} KB)")
    for need in ("shaders/ground.gdshader", "shaders/route.gdshader", "shaders/ghost.gdshader"):
        if not any(k.startswith(need) for k in files):
            missing.append(need)
    total = sum(files.values())
    print(f"PCK {pck}: Godot {ver[0]}.{ver[1]}.{ver[2]}, {len(files)} entries, {total / 1048576:.1f} MB of data; " + ", ".join(summary))
    if missing:
        print("PCK MISSING:\n  " + "\n  ".join(missing))
        sys.exit(1)
    print("PCK OK: every model, texture, icon, font and sound is in the pack")


if __name__ == "__main__":
    main()
