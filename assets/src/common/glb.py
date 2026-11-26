"""Low-poly mesh builder and glTF 2.0 binary (.glb) writer: stdlib + numpy only.

Conventions (docs/asset-pipeline.md): metres, +Y up, north -Z, front faces +Z, origin at the footprint
centre on the ground. Every face is flat-shaded (its own vertices) and has ONE palette colour: its UVs all
point at the centre of that colour's swatch in the shared 256 x 256 palette atlas, which is embedded in the
.glb with a NEAREST sampler and no mipmaps. Godot imports it as one StandardMaterial3D named "Palette";
no import script needed. (Vertex colours are NOT used: Godot's importer leaves vertex_color_use_as_albedo
off, so they render white.)

    from common import glb
    m = glb.Mesh()
    m.add_box((0, 0, 0), (8, 0.3, 6), "Packed earth")              # pos = centre of the base, size = (x, y, z)
    m.add_box((0, 0.3, 0), (7.6, 2.5, 5.6), "Limewash")
    m.add_gable((0, 2.8, 0), 7.8, 5.6, 3.2, "thatch", gable="Limewash", overhang=0.4)
    m.add_cylinder((3, 0, 3), 0.3, 2.0, "Mud", segments=6)
    m.add_cone(...), m.add_frustum(...), m.add_sphere(...), m.add_extrude(...), m.add_poly(...), m.append(other, pos, yaw)
    stats = glb.write_glb("assets/models/cottage.glb", m, max_tris=500)
    stats = glb.write_glb(path, [("body", body), ("head", head), ("item", item)])   # separate nodes (villager parts)

Outline: NOT modelled (guide: "do not model outlines into meshes"). The ink line is the screen-space edge pass
in assets/src/common/outline_screen.gdshader, used by tools/render-contact-sheet.sh and later by the game.
Positions and angles: `pos` is always (x, y, z) of the base centre; `yaw` is in radians, about +Y, turning +X
towards -Z (counter-clockwise seen from above).
"""
import json
import math
import struct
from pathlib import Path

import numpy as np

from . import palette as P


def _v(p):
    return np.asarray(p, float)


def _rot_y(p, yaw):
    c, s = math.cos(yaw), math.sin(yaw)
    return np.array([c * p[0] + s * p[2], p[1], -s * p[0] + c * p[2]])


class Mesh:
    """A list of coloured triangles: (p0, p1, p2, palette_name), counter-clockwise seen from outside."""

    def __init__(self):
        self.tris = []

    # ------------------------------------------------------------ core
    def _face(self, pts, colour, outward):
        """Polygon (3+ points, convex, in order). Winding is fixed so the normal points along `outward`."""
        pts = [_v(p) for p in pts]
        n = np.cross(pts[1] - pts[0], pts[2] - pts[0])
        if np.dot(n, outward) < 0:
            pts = pts[::-1]
        colour = P.resolve(colour)
        for i in range(1, len(pts) - 1):
            self.tris.append((pts[0], pts[i], pts[i + 1], colour))

    @staticmethod
    def _place(pts, pos, yaw):
        """Rotate local points about the local origin, then move to pos."""
        return [_rot_y(_v(p), yaw) + _v(pos) for p in pts]

    def add_poly(self, pts, colour, facing=(0, 1, 0)):
        """One flat convex polygon in world coordinates, facing `facing` (paths, water sheets, decals)."""
        self._face(pts, colour, _v(facing))

    # ------------------------------------------------------------ primitives
    def add_box(self, pos, size, colour, yaw=0.0, top=None, bottom=None):
        """Box with its base centre at pos. size = (sx, sy, sz). colour is a name, or a dict per side
        {'+x','-x','+y','-y','+z','-z'} (None = omit that side; omitted keys use the 'default' key or are skipped).
        top / bottom override the +y / -y colour. Bottom faces are kept by default only when the box floats
        (pos.y > 0); pass bottom=... to force one, or bottom=False to drop it."""
        hx, sy, hz = size[0] / 2, size[1], size[2] / 2
        faces = {
            "+x": [(hx, 0, -hz), (hx, 0, hz), (hx, sy, hz), (hx, sy, -hz)],
            "-x": [(-hx, 0, -hz), (-hx, 0, hz), (-hx, sy, hz), (-hx, sy, -hz)],
            "+y": [(-hx, sy, -hz), (hx, sy, -hz), (hx, sy, hz), (-hx, sy, hz)],
            "-y": [(-hx, 0, -hz), (hx, 0, -hz), (hx, 0, hz), (-hx, 0, hz)],
            "+z": [(-hx, 0, hz), (hx, 0, hz), (hx, sy, hz), (-hx, sy, hz)],
            "-z": [(-hx, 0, -hz), (hx, 0, -hz), (hx, sy, -hz), (-hx, sy, -hz)],
        }
        if isinstance(colour, dict):
            cols = {k: colour.get(k, colour.get("default")) for k in faces}
        else:
            cols = {k: colour for k in faces}
            if pos[1] <= 1e-6:
                cols["-y"] = None            # sits on the ground: nobody sees the bottom
        if top is not None:
            cols["+y"] = top
        if bottom is False:
            cols["-y"] = None
        elif bottom is not None:
            cols["-y"] = bottom
        centre = self._place([(0, sy / 2, 0)], pos, yaw)[0]
        for k, pts in faces.items():
            if cols[k] is None:
                continue
            w = self._place(pts, pos, yaw)
            self._face(w, cols[k], np.mean(w, axis=0) - centre)

    def add_gable(self, pos, length, width, height, colour, gable=None, yaw=0.0, ridge_axis="x", overhang=0.0,
                  bottom=False):
        """Pitched roof (triangular prism). Base length (along the ridge) x width sits at pos.y, ridge `height`
        above it. overhang pushes the eaves out by that much on both long sides (same pitch, so the eaves drop
        below pos.y) - the footprint grows by 2 * overhang, so size walls to keep the roof inside the cells.
        gable = colour of the two end triangles (default: same as slopes). ridge_axis 'x' or 'z'."""
        L, W = length / 2, width / 2 + overhang
        drop = overhang * height / (width / 2) if width else 0.0
        A = [(-L, -drop, -W), (L, -drop, -W), (L, -drop, W), (-L, -drop, W)]
        R0, R1 = (-L, height, 0), (L, height, 0)

        def T(p):
            return (p[2], p[1], p[0]) if ridge_axis == "z" else p
        gable = gable or colour
        faces = [([A[0], A[1], R1, R0], colour), ([A[3], A[2], R1, R0], colour),
                 ([A[0], A[3], R0], gable), ([A[1], A[2], R1], gable)]
        if bottom:
            faces.append((A, bottom if isinstance(bottom, str) else gable))
        centre = self._place([T((0, height / 3, 0))], pos, yaw)[0]
        for f, c in faces:
            w = self._place([T(p) for p in f], pos, yaw)
            self._face(w, c, np.mean(w, axis=0) - centre)

    def add_hip(self, pos, length, width, height, colour, yaw=0.0, ridge_frac=0.4, overhang=0.0):
        """Hipped roof: four slopes, ridge along X of length ridge_frac * length (0 = pyramid)."""
        L, W = length / 2 + overhang, width / 2 + overhang
        drop = overhang * height / (width / 2) if width else 0.0
        r = max(length * ridge_frac / 2, 0.0)
        a = [(-L, -drop, -W), (L, -drop, -W), (L, -drop, W), (-L, -drop, W)]
        r0, r1 = (-r, height, 0), (r, height, 0)
        faces = [[a[0], a[1], r1, r0], [a[3], a[2], r1, r0], [a[0], a[3], r0], [a[1], a[2], r1]]
        centre = self._place([(0, height / 3, 0)], pos, yaw)[0]
        for f in faces:
            if r < 1e-6 and len(f) == 4:
                f = [f[0], f[1], f[2]]
            w = self._place(f, pos, yaw)
            self._face(w, colour, np.mean(w, axis=0) - centre)

    def add_frustum(self, pos, r_bottom, r_top, height, colour, segments=8, yaw=0.0, cap_top=True,
                    cap_bottom=None, cap_colour=None):
        """Tapered post / cylinder (r_bottom == r_top) / cone (r_top == 0), flat-shaded sides.
        cap_bottom defaults to True only when it floats (pos.y > 0)."""
        n = segments
        if cap_bottom is None:
            cap_bottom = pos[1] > 1e-6
        ang = [2 * math.pi * (i + 0.5) / n + yaw for i in range(n)]   # half step: a 4-sided one is axis-aligned
        bot = [_v(pos) + (r_bottom * math.cos(a), 0, r_bottom * math.sin(a)) for a in ang]
        top = [_v(pos) + (r_top * math.cos(a), height, r_top * math.sin(a)) for a in ang]
        centre = _v(pos) + (0, height / 2, 0)
        cc = cap_colour or colour
        for i in range(n):
            j = (i + 1) % n
            quad = [bot[i], bot[j], top[j], top[i]] if r_top > 1e-6 else [bot[i], bot[j], _v(pos) + (0, height, 0)]
            self._face(quad, colour, np.mean(quad, axis=0) - centre)
        if cap_top and r_top > 1e-6:
            self._face(top, cc, _v((0, 1, 0)))
        if cap_bottom and r_bottom > 1e-6:
            self._face(bot, cc, _v((0, -1, 0)))

    def add_cylinder(self, pos, radius, height, colour, segments=8, **kw):
        self.add_frustum(pos, radius, radius, height, colour, segments, **kw)

    def add_cone(self, pos, radius, height, colour, segments=8, **kw):
        self.add_frustum(pos, radius, 0.0, height, colour, segments, **kw)

    def add_sphere(self, centre, radius, colour, rings=3, segments=7, squash=1.0):
        """Chunky low-poly ball (tree canopy, head). centre = its middle; squash scales Y."""
        c = _v(centre)
        rows = []
        for r in range(1, rings + 1):
            phi = math.pi * r / (rings + 1)
            rows.append([c + (radius * math.sin(phi) * math.cos(2 * math.pi * (i + .5) / segments),
                              radius * math.cos(phi) * squash,
                              radius * math.sin(phi) * math.sin(2 * math.pi * (i + .5) / segments))
                         for i in range(segments)])
        top, bot = c + (0, radius * squash, 0), c - (0, radius * squash, 0)
        for i in range(segments):
            j = (i + 1) % segments
            self._face([top, rows[0][i], rows[0][j]], colour, top - c)
            for r in range(rings - 1):
                q = [rows[r][i], rows[r + 1][i], rows[r + 1][j], rows[r][j]]
                self._face(q, colour, np.mean(q, axis=0) - c)
            self._face([bot, rows[-1][i], rows[-1][j]], colour, bot - c)

    def add_extrude(self, pos, outline_xz, height, colour, top=None, yaw=0.0, cap_bottom=None):
        """Prism from a CONVEX 2D outline [(x, z), ...] (any winding), extruded up by height from pos.y.
        Boat hulls, irregular plinths, field plots. top = colour of the top cap (default: colour)."""
        pts = [(float(x), float(z)) for x, z in outline_xz]
        area = sum(x0 * z1 - x1 * z0 for (x0, z0), (x1, z1) in zip(pts, pts[1:] + pts[:1]))
        if area > 0:
            pts = pts[::-1]
        if cap_bottom is None:
            cap_bottom = pos[1] > 1e-6
        b = self._place([(x, 0, z) for x, z in pts], pos, yaw)
        t = self._place([(x, height, z) for x, z in pts], pos, yaw)
        centre = np.mean(b + t, axis=0)
        for i in range(len(pts)):
            j = (i + 1) % len(pts)
            q = [b[i], b[j], t[j], t[i]]
            self._face(q, colour, np.mean(q, axis=0) - centre)
        self._face(t, top or colour, _v((0, 1, 0)))
        if cap_bottom:
            self._face(b, colour, _v((0, -1, 0)))

    def append(self, other, pos=(0, 0, 0), yaw=0.0):
        """Copy another Mesh in, rotated by yaw about its origin, then moved to pos (instancing)."""
        for a, b, c, col in other.tris:
            self.tris.append((*(self._place([a, b, c], pos, yaw)), col))
        return self

    # ------------------------------------------------------------ info
    def tri_count(self):
        return len(self.tris)

    def bounds(self):
        a = np.array([p for t in self.tris for p in t[:3]])
        return a.min(0), a.max(0)

    def colours(self):
        return sorted({t[3] for t in self.tris})


# ---------------------------------------------------------------- writer
def _pad4(b, pad=b"\0"):
    return b + pad * (-len(b) % 4)


INK_SOFT = 1.5     # crease threshold for ink="soft" models (the default pass uses 0.45; see outline_screen.gdshader)


def write_glb(path, parts, max_tris=None, generator="tide-toll assets/src/common/glb.py", ink=None):
    """Write a .glb. parts = a Mesh, or a list of (node_name, Mesh) or (node_name, Mesh, ink) for separate nodes
    (all at the origin, transforms applied), or (node_name, Mesh, ink, (x, y, z)) for a node that the game moves or
    rotates: its mesh is in the node's local frame (pivot at the local origin) and the node carries that translation
    (a hinged drawbridge leaf, a mast that lowers). ink None = the file's default. Returns {'tris', 'min', 'max', 'size', 'colours'}. Raises if max_tris
    is exceeded. Output bytes depend only on the input, so generators are reproducible.
    ink="soft" marks the nodes (glTF node extras {"ink": "soft"}, which Godot imports as node metadata "extras")
    for the soft-crease ink pass: many-faceted organic shapes (tree crowns, crop rows) whose facet creases would
    otherwise carry more ink than the houses. The renderer draws them into stencil 1 and inks creases there only
    above INK_SOFT; silhouettes are inked as usual."""
    if isinstance(parts, Mesh):
        parts = [(Path(path).stem, parts)]
    parts = [(p[0], p[1], p[2] if len(p) > 2 and p[2] is not None else ink, p[3] if len(p) > 3 else None)
             for p in parts]
    total = sum(p[1].tri_count() for p in parts if p[1] is not None)
    if max_tris is not None and total > max_tris:
        raise ValueError(f"{path}: {total} triangles, budget {max_tris}")
    bin_chunks, views, accessors, meshes, nodes = [], [], [], [], []
    offset = [0]

    def add_view(data, target=None):
        data = _pad4(data)
        v = {"buffer": 0, "byteOffset": offset[0], "byteLength": len(data)}
        if target:
            v["target"] = target
        views.append(v)
        bin_chunks.append(data)
        offset[0] += len(data)
        return len(views) - 1

    def add_acc(arr, ctype, target, minmax=False):
        arr = np.ascontiguousarray(arr)
        comp = {np.dtype("float32"): 5126, np.dtype("uint16"): 5123, np.dtype("uint32"): 5125}[arr.dtype]
        a = {"bufferView": add_view(arr.tobytes(), target), "componentType": comp, "count": int(len(arr)),
             "type": ctype}
        if minmax:
            a["min"] = [float(x) for x in arr.min(0)]
            a["max"] = [float(x) for x in arr.max(0)]
        accessors.append(a)
        return len(accessors) - 1

    lo, hi = np.full(3, np.inf), np.full(3, -np.inf)
    for name, m, node_ink, trans in parts:
        if m is None:   # an empty node (a point the game uses, such as a chimney's "Smoke"): name and translation only
            nodes.append({"name": name, "translation": [float(v) for v in (trans or (0, 0, 0))]})
            continue
        if not m.tris:
            raise ValueError(f"part {name!r} is empty")
        pos = np.array([p for t in m.tris for p in t[:3]], np.float32)
        nrm = []
        for t in m.tris:
            n = np.cross(t[1] - t[0], t[2] - t[0])
            ln = np.linalg.norm(n)
            nrm += [n / ln if ln > 0 else np.array([0, 1, 0.])] * 3
        nrm = np.array(nrm, np.float32)
        uv = np.array([P.uv(t[3]) for t in m.tris for _ in range(3)], np.float32)
        idx = np.arange(len(pos), dtype=np.uint32 if len(pos) > 65535 else np.uint16)
        off = np.zeros(3, np.float32) if trans is None else np.asarray(trans, np.float32)
        lo, hi = np.minimum(lo, pos.min(0) + off), np.maximum(hi, pos.max(0) + off)
        prim = {"attributes": {"POSITION": add_acc(pos, "VEC3", 34962, True), "NORMAL": add_acc(nrm, "VEC3", 34962),
                               "TEXCOORD_0": add_acc(uv, "VEC2", 34962)},
                "indices": add_acc(idx, "SCALAR", 34963), "material": 0, "mode": 4}
        meshes.append({"name": name, "primitives": [prim]})
        node = {"name": name, "mesh": len(meshes) - 1}
        if trans is not None:
            node["translation"] = [float(v) for v in trans]
        if node_ink:
            node["extras"] = {"ink": node_ink}
        nodes.append(node)
    img_view = add_view(P.atlas_png_bytes())
    gltf = {"asset": {"version": "2.0", "generator": generator}, "scene": 0,
            "scenes": [{"nodes": list(range(len(nodes)))}], "nodes": nodes, "meshes": meshes,
            "materials": [{"name": "Palette", "doubleSided": False,
                           "pbrMetallicRoughness": {"baseColorTexture": {"index": 0}, "metallicFactor": 0.0,
                                                    "roughnessFactor": 1.0}}],
            "textures": [{"sampler": 0, "source": 0}],
            "samplers": [{"magFilter": 9728, "minFilter": 9728, "wrapS": 33071, "wrapT": 33071}],   # NEAREST, clamp
            "images": [{"bufferView": img_view, "mimeType": "image/png", "name": "palette"}],
            "accessors": accessors, "bufferViews": views, "buffers": [{"byteLength": offset[0]}]}
    js = _pad4(json.dumps(gltf, separators=(",", ":")).encode(), b" ")
    binb = b"".join(bin_chunks)
    path = Path(path)
    path.parent.mkdir(parents=True, exist_ok=True)
    with open(path, "wb") as f:
        f.write(struct.pack("<III", 0x46546C67, 2, 12 + 8 + len(js) + 8 + len(binb)))
        f.write(struct.pack("<II", len(js), 0x4E4F534A) + js)
        f.write(struct.pack("<II", len(binb), 0x004E4942) + binb)
    used = sorted({t[3] for p in parts if p[1] is not None for t in p[1].tris})
    return {"tris": total, "min": [round(float(x), 3) for x in lo], "max": [round(float(x), 3) for x in hi],
            "size": [round(float(x), 3) for x in hi - lo], "colours": used}


def report(path, stats):
    """One-line summary for generator output."""
    s = stats
    return (f"{path}: {s['tris']} tris, size {s['size'][0]} x {s['size'][1]} x {s['size'][2]} m "
            f"(x, y, z), colours {', '.join(s['colours'])}")
