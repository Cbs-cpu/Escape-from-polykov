#!/usr/bin/env python3
"""
Generates the game's UI font, "Polykov Grid": a condensed geometric sans in the spirit of the Escape from Tarkov UI
font (Bender), redrawn from scratch as low-poly strokes (every curve is a 45-degree chamfer).

Each glyph is a set of centre-line polylines on a 1000-unit em; strokes are expanded with square mitres, diagonal ends
are cut flat, then the outline is written as a TrueType font. Two weights: Regular and Bold.

Usage:  pip install fonttools shapely && python3 Tools/FontGen/make_polykov_font.py
Output: Assets/_Project/Resources/Fonts/PolykovGrid-Regular.ttf, PolykovGrid-Bold.ttf (+ docs preview if Pillow).
"""
import math
import pathlib

from fontTools.fontBuilder import FontBuilder
from fontTools.pens.ttGlyphPen import TTGlyphPen
from shapely import affinity
from shapely.geometry import LineString, MultiPolygon, Polygon, box
from shapely.geometry.polygon import orient
from shapely.ops import unary_union

ROOT = pathlib.Path(__file__).resolve().parents[2]
OUT = ROOT / "Assets" / "_Project" / "Resources" / "Fonts"

CAP, XH, ASC, DESC = 700, 510, 730, -210
SIDE = 46          # side bearing
GRID = 2           # snap outline points to this many units


def glyphs(s):
    """Centre-line drawings for stroke width s. Returns {char: (strokes, fills, fixed_advance or None)}."""
    h = s / 2
    b, t, m = h, CAP - h, CAP * 0.5          # cap baseline / top / middle (centre lines)
    xt, xm = XH - h, XH * 0.5                # lowercase
    a_, d_ = ASC - h, DESC + h               # ascender / descender centre lines
    c = 82                                   # cap chamfer
    k = 64                                   # lowercase chamfer
    W, w = 300, 262                          # cap / lowercase inner width

    def octagon(x0, y0, x1, y1, ch):
        return [(x0 + ch, y0), (x1 - ch, y0), (x1, y0 + ch), (x1, y1 - ch), (x1 - ch, y1), (x0 + ch, y1),
                (x0, y1 - ch), (x0, y0 + ch), (x0 + ch, y0)]

    def dot(x, y, size=None):
        size = size or s * 1.02
        return [(x - size / 2, y), (x + size / 2, y), (x + size / 2, y + size), (x - size / 2, y + size)]

    G = {}
    # ------------------------------------------------------------------ capitals
    G["A"] = [[(0, b), (0, t - c), (c, t), (W - c, t), (W, t - c), (W, b)], [(0, m - 20), (W, m - 20)]]
    G["B"] = [[(0, b), (0, t), (W - c - 20, t), (W - 20, t - c), (W - 20, m + c * 0.6), (W - 20 - c * 0.6, m),
               (W - c * 0.6, m), (W, m - c * 0.6), (W, b + c), (W - c, b), (0, b)], [(0, m), (W - 20 - c * 0.6, m)]]
    G["C"] = [[(W, t), (c, t), (0, t - c), (0, b + c), (c, b), (W, b)]]
    G["D"] = [[(0, b), (0, t), (W - c, t), (W, t - c), (W, b + c), (W - c, b), (0, b)]]
    G["E"] = [[(W, t), (0, t), (0, b), (W, b)], [(0, m), (W - 50, m)]]
    G["F"] = [[(W, t), (0, t), (0, b)], [(0, m), (W - 50, m)]]
    G["G"] = [[(W, t), (c, t), (0, t - c), (0, b + c), (c, b), (W, b), (W, m - 10), (W * 0.45, m - 10)]]
    G["H"] = [[(0, b), (0, t)], [(W, b), (W, t)], [(0, m), (W, m)]]
    G["I"] = [[(0, b), (0, t)]]
    G["J"] = [[(W * 0.8, t), (W * 0.8, b + c), (W * 0.8 - c, b), (0, b)]]
    G["K"] = [[(0, b), (0, t)], [(0, m - 10), (W * 0.32, m - 10), (W, t - 40), (W, t)], [(W * 0.32, m - 10), (W, b + 40), (W, b)]]
    G["L"] = [[(0, t), (0, b), (W - 20, b)]]
    G["M"] = [[(0, b), (0, t), (W * 0.62, m + 40), (W * 1.24, t), (W * 1.24, b)]]
    G["N"] = [[(0, b), (0, t), (W, b), (W, t)]]
    G["O"] = [octagon(0, b, W, t, c)]
    G["P"] = [[(0, b), (0, t), (W - c, t), (W, t - c), (W, m + c * 0.7), (W - c * 0.7, m - 10), (0, m - 10)]]
    G["Q"] = [octagon(0, b, W, t, c), [(W * 0.55, b + 150), (W + 40, b - 110)]]
    G["R"] = [[(0, b), (0, t), (W - c, t), (W, t - c), (W, m + c * 0.7), (W - c * 0.7, m - 10), (0, m - 10)],
              [(W * 0.45, m - 10), (W, b + 40), (W, b)]]
    G["S"] = [[(W, t), (c, t), (0, t - c), (0, m + c * 0.6), (c * 0.6, m), (W - c * 0.6, m), (W, m - c * 0.6),
               (W, b + c), (W - c, b), (0, b)]]
    G["T"] = [[(0, t), (W + 20, t)], [(W / 2 + 10, t), (W / 2 + 10, b)]]
    G["U"] = [[(0, t), (0, b + c), (c, b), (W - c, b), (W, b + c), (W, t)]]
    G["V"] = [[(0, t), (0, b + 250), (W / 2, b), (W, b + 250), (W, t)]]
    G["W"] = [[(0, t), (0, b), (W * 0.65, b + 250), (W * 1.3, b), (W * 1.3, t)]]
    G["X"] = [[(0, t), (0, t - 60), (W, b + 60), (W, b)], [(W, t), (W, t - 60), (0, b + 60), (0, b)]]
    G["Y"] = [[(0, t), (0, m + c * 0.6), (c * 0.6, m - 10), (W - c * 0.6, m - 10), (W, m + c * 0.6), (W, t)],
              [(W / 2, m - 10), (W / 2, b)]]
    G["Z"] = [[(0, t), (W, t), (W, t - 40), (0, b + 40), (0, b), (W, b)]]
    # ------------------------------------------------------------------ lowercase
    G["a"] = [[(k, xt), (w - k, xt), (w, xt - k), (w, b)], [(w, xm - 10), (k, xm - 10), (0, xm - 10 - k), (0, b + k), (k, b), (w, b)]]
    G["b"] = [[(0, a_), (0, b), (w - k, b), (w, b + k), (w, xt - k), (w - k, xt), (0, xt)]]
    G["c"] = [[(w, xt), (k, xt), (0, xt - k), (0, b + k), (k, b), (w, b)]]
    G["d"] = [[(w, a_), (w, b), (k, b), (0, b + k), (0, xt - k), (k, xt), (w, xt)]]
    G["e"] = [[(w, b), (k, b), (0, b + k), (0, xt - k), (k, xt), (w - k, xt), (w, xt - k), (w, xm), (0, xm)]]
    G["f"] = [[(190, a_), (k, a_), (0, a_ - k), (0, b)], [(-70, xt), (170, xt)]]
    G["g"] = [[(w, xt), (w, d_ + k), (w - k, d_), (0, d_)], [(w, xt), (k, xt), (0, xt - k), (0, b + k), (k, b), (w, b)]]
    G["h"] = [[(0, a_), (0, b)], [(0, xt), (w - k, xt), (w, xt - k), (w, b)]]
    G["i"] = [[(0, xt), (0, b)]]
    G["j"] = [[(0, xt), (0, d_ + k), (-k, d_), (-130, d_)]]
    G["k"] = [[(0, a_), (0, b)], [(0, xm - 10), (w * 0.3, xm - 10), (w, xt - 30), (w, xt)], [(w * 0.3, xm - 10), (w, b + 30), (w, b)]]
    G["l"] = [[(0, a_), (0, b)]]
    G["m"] = [[(0, b), (0, xt), (w * 1.5 - k, xt), (w * 1.5, xt - k), (w * 1.5, b)], [(w * 0.75, xt), (w * 0.75, b)]]
    G["n"] = [[(0, b), (0, xt), (w - k, xt), (w, xt - k), (w, b)]]
    G["o"] = [octagon(0, b, w, xt, k)]
    G["p"] = [[(0, d_), (0, xt), (w - k, xt), (w, xt - k), (w, b + k), (w - k, b), (0, b)]]
    G["q"] = [[(w, d_), (w, xt), (k, xt), (0, xt - k), (0, b + k), (k, b), (w, b)]]
    G["r"] = [[(0, b), (0, xt), (190, xt)]]
    G["s"] = [[(w, xt), (k, xt), (0, xt - k), (0, xm + k * 0.5), (k * 0.5, xm), (w - k * 0.5, xm), (w, xm - k * 0.5),
               (w, b + k), (w - k, b), (0, b)]]
    G["t"] = [[(0, a_ - 70), (0, b + k), (k, b), (190, b)], [(-70, xt), (190, xt)]]
    G["u"] = [[(0, xt), (0, b + k), (k, b), (w, b)], [(w, xt), (w, b)]]
    G["v"] = [[(0, xt), (0, b + 170), (w / 2, b), (w, b + 170), (w, xt)]]
    G["w"] = [[(0, xt), (0, b), (w * 0.65, b + 180), (w * 1.3, b), (w * 1.3, xt)]]
    G["x"] = [[(0, xt), (0, xt - 40), (w, b + 40), (w, b)], [(w, xt), (w, xt - 40), (0, b + 40), (0, b)]]
    G["y"] = [[(0, xt), (0, b + k), (k, b), (w, b)], [(w, xt), (w, d_ + k), (w - k, d_), (0, d_)]]
    G["z"] = [[(0, xt), (w, xt), (w, xt - 30), (0, b + 30), (0, b), (w, b)]]
    # ------------------------------------------------------------------ digits (tabular)
    D = 290
    G["0"] = [octagon(0, b, D, t, c)]
    G["1"] = [[(D * 0.5 - c, t - c), (D * 0.5, t), (D * 0.5, b)]]
    G["2"] = [[(0, t), (D - c, t), (D, t - c), (D, m + c * 0.6), (D - c * 0.6, m), (c * 0.6, m), (0, m - c * 0.6), (0, b), (D, b)]]
    G["3"] = [[(0, t), (D - c, t), (D, t - c), (D, m + c * 0.6), (D - c * 0.6, m), (D, m - c * 0.6), (D, b + c), (D - c, b), (0, b)],
              [(D * 0.3, m), (D - c * 0.6, m)]]
    G["4"] = [[(0, t), (0, m - 30), (D, m - 30)], [(D - 50, t), (D - 50, b)]]
    G["5"] = [[(D, t), (0, t), (0, m + 20), (D - c * 0.6, m + 20), (D, m + 20 - c * 0.6), (D, b + c), (D - c, b), (0, b)]]
    G["6"] = [[(D, t), (c, t), (0, t - c), (0, b + c), (c, b), (D - c, b), (D, b + c), (D, m - c * 0.6), (D - c * 0.6, m + 10), (0, m + 10)]]
    G["7"] = [[(0, t), (D, t), (D, t - 90), (D * 0.35, b + 40), (D * 0.35, b)]]
    G["8"] = [octagon(0, b, D, m + 10, c * 0.8), octagon(20, m + 10, D - 20, t, c * 0.7)]
    G["9"] = [[(0, b), (D - c, b), (D, b + c), (D, t - c), (D - c, t), (c, t), (0, t - c), (0, m + c * 0.6), (c * 0.6, m - 10), (D, m - 10)]]
    fixed = {ch: D for ch in "0123456789"}
    # ------------------------------------------------------------------ punctuation
    F = {}
    F["."] = [dot(0, 0)]
    F[","] = [dot(0, 0) + [], [(-s / 2, 0), (s / 2, 0), (s / 2 - 30, -120), (-s / 2 - 10, -120)]]
    F[","] = [[(-s / 2, s), (s / 2, s), (s / 2, -30), (-s / 2 + 5, -140), (-s / 2 - 25, -140), (-s / 2, -30)]]
    F[":"] = [dot(0, 0), dot(0, XH - s)]
    F[";"] = [F[","][0], dot(0, XH - s)]
    G["!"] = [[(0, t), (0, 210)]]
    F["!"] = [dot(0, 0)]
    G["?"] = [[(0, t - c), (c, t), (W - c, t), (W, t - c), (W, m + c * 0.6), (W - c * 0.6, m), (W / 2, m), (W / 2, 210)]]
    F["?"] = [dot(W / 2, 0)]
    G["-"] = [[(0, 300), (190, 300)]]
    G["_"] = [[(0, -80), (W, -80)]]
    G["+"] = [[(0, 300), (280, 300)], [(140, 160), (140, 440)]]
    G["="] = [[(0, 220), (280, 220)], [(0, 400), (280, 400)]]
    G["/"] = [[(0, -60), (0, -20), (250, 720), (250, 760)]]
    G["\\"] = [[(250, -60), (250, -20), (0, 720), (0, 760)]]
    G["|"] = [[(0, -120), (0, 780)]]
    G["("] = [[(130, 780), (130, 760), (0, 600), (0, 60), (130, -100), (130, -120)]]
    G[")"] = [[(0, 780), (0, 760), (130, 600), (130, 60), (0, -100), (0, -120)]]
    G["["] = [[(150, 760), (0, 760), (0, -80), (150, -80)]]
    G["]"] = [[(0, 760), (150, 760), (150, -80), (0, -80)]]
    G["<"] = [[(260, 520), (0, 300), (260, 80)]]
    G[">"] = [[(0, 520), (260, 300), (0, 80)]]
    G["'"] = [[(0, t), (0, t - 160)]]
    G['"'] = [[(0, t), (0, t - 160)], [(140, t), (140, t - 160)]]
    G["*"] = [[(140, t), (140, t - 260)], [(20, t - 60), (260, t - 200)], [(260, t - 60), (20, t - 200)]]
    G["#"] = [[(90, b), (90, t)], [(250, b), (250, t)], [(0, 230), (340, 230)], [(0, 470), (340, 470)]]
    G["°"] = [octagon(0, t - 170, 170, t, 50)]
    G["%"] = [octagon(0, t - 230, 160, t, 50), octagon(250, b, 410, b + 230, 50), [(10, b), (10, b + 40), (400, t - 40), (400, t)]]
    F["·"] = [dot(0, 300 - s / 2)]
    G["×"] = [[(0, 480), (240, 180)], [(240, 480), (0, 180)]]
    G["&"] = [[(W + 20, b), (c, t - c * 0.2), (c, t - c * 0.1)], [(W + 20, m), (W, m), (W - 150, b), (c, b), (0, b + c), (0, m - c), (c + 40, m + 40)],
              [(c * 0.6, t), (W - c - 60, t), (W - 60, t - c), (W - 60, t - 120)]]
    G["$"] = G["S"] + [[(W / 2, t + 90), (W / 2, b - 90)]]
    G["₽"] = [[(0, b), (0, t), (W - c, t), (W, t - c), (W, m + c * 0.7 + 60), (W - c * 0.7, m + 50), (-90, m + 50)], [(-90, 200), (200, 200)]]
    G["€"] = [[(W, t), (c, t), (0, t - c), (0, b + c), (c, b), (W, b)], [(-90, m + 70), (200, m + 70)], [(-90, m - 70), (200, m - 70)]]
    G["@"] = [octagon(0, -40, 520, t, c), [(380, 120), (380, 460), (170, 460), (140, 430), (140, 170), (170, 140), (380, 140), (430, 120)]]
    G["~"] = [[(0, 280), (70, 350), (170, 250), (240, 320)]]
    # ------------------------------------------------------------------ assemble
    out = {}
    for ch, strokes in G.items():
        out[ch] = (strokes, F.get(ch, []), fixed.get(ch))
    for ch, fills in F.items():
        if ch not in G:
            out[ch] = ([], fills, None)
    # dotted i / j
    out["i"] = (G["i"], [dot(0, XH + 90)], None)
    out["j"] = (G["j"], [dot(0, XH + 90)], None)
    # inverted Spanish marks
    out["¡"] = ("rot", "!")
    out["¿"] = ("rot", "?")
    # accents: (base, kind)
    for base, acc in (("a", "á"), ("e", "é"), ("i", "í"), ("o", "ó"), ("u", "ú"),
                      ("A", "Á"), ("E", "É"), ("I", "Í"), ("O", "Ó"), ("U", "Ú")):
        out[acc] = ("acute", base)
    out["ñ"] = ("tilde", "n")
    out["Ñ"] = ("tilde", "N")
    out["ü"] = ("dier", "u")
    out["Ü"] = ("dier", "U")
    out["ç"] = ("cedilla", "c")
    return out


def expand(polyline, s):
    """Stroke a centre line: square mitres; diagonal ends are extended so they can be cut flat by the clip box."""
    pts = [tuple(p) for p in polyline]
    closed = pts[0] == pts[-1] and len(pts) > 2
    if not closed:
        def extend(p, q):
            dx, dy = p[0] - q[0], p[1] - q[1]
            n = math.hypot(dx, dy)
            if n < 1e-6 or abs(dx) < 1e-6 or abs(dy) < 1e-6:
                return p
            return (p[0] + dx / n * s, p[1] + dy / n * s)
        pts[0] = extend(pts[0], pts[1])
        pts[-1] = extend(pts[-1], pts[-2])
    line = LineString(pts)
    return line.buffer(s / 2, cap_style="flat", join_style="mitre", mitre_limit=2.2)


def shape_of(ch, table, s):
    entry = table[ch]
    if entry[0] == "rot":
        g = shape_of(entry[1], table, s)
        return affinity.rotate(g, 180, origin=((g.bounds[0] + g.bounds[2]) / 2, (CAP - 210) / 2))
    if entry[0] in ("acute", "tilde", "dier", "cedilla"):
        base = shape_of(entry[1], table, s)
        x0, _, x1, y1 = base.bounds
        cx = (x0 + x1) / 2
        top = (CAP if entry[1].isupper() else XH) + 70
        if entry[0] == "acute":
            mark = Polygon([(cx - 40, top), (cx + 30, top), (cx + 130, top + 130), (cx + 40, top + 130)])
        elif entry[0] == "tilde":
            mark = LineString([(cx - 130, top + 10), (cx - 70, top + 80), (cx + 40, top + 20), (cx + 110, top + 90)]).buffer(
                s * 0.36, cap_style="flat", join_style="mitre")
        elif entry[0] == "dier":
            q = s * 0.9
            mark = unary_union([box(cx - 110 - q / 2, top, cx - 110 + q / 2, top + q), box(cx + 110 - q / 2, top, cx + 110 + q / 2, top + q)])
        else:
            mark = Polygon([(cx - 20, 0), (cx + 60, 0), (cx + 60, -90), (cx - 60, -170), (cx - 90, -140), (cx - 20, -90)])
        return unary_union([base, mark])
    strokes, fills, _ = entry
    parts = [expand(p, s) for p in strokes] + [Polygon(f) for f in fills]
    if not parts:
        return Polygon()
    g = unary_union(parts)
    # Flat-cut the extended diagonal ends: clip to the vertical span of the centre lines +- half a stroke.
    ys = [p[1] for poly in strokes for p in poly]
    if ys:
        lo, hi = min(ys) - s / 2, max(ys) + s / 2
        for f in fills:
            lo = min(lo, min(p[1] for p in f))
            hi = max(hi, max(p[1] for p in f))
        g = g.intersection(box(-2000, lo, 3000, hi))
    return g


def polygons(g):
    if g.is_empty:
        return []
    if isinstance(g, Polygon):
        return [g]
    return [p for p in getattr(g, "geoms", []) if isinstance(p, Polygon)]


def draw(g, dx):
    pen = TTGlyphPen(None)
    for poly in polygons(g):
        poly = orient(poly.simplify(0.8), sign=-1.0)  # TrueType: outer contours clockwise, holes counter-clockwise
        for ring in [poly.exterior] + list(poly.interiors):
            pts = []
            for x, y in list(ring.coords)[:-1]:
                p = (int(round((x + dx) / GRID) * GRID), int(round(y / GRID) * GRID))
                if not pts or pts[-1] != p:
                    pts.append(p)
            if len(pts) > 1 and pts[0] == pts[-1]:
                pts.pop()
            if len(pts) < 3:
                continue
            pen.moveTo(pts[0])
            for p in pts[1:]:
                pen.lineTo(p)
            pen.closePath()
    return pen.glyph()


def build(weight, s):
    table = glyphs(s)
    names = {" ": "space", " ": "nbspace"}
    order = [".notdef", "space", "nbspace"]
    cmap = {0x20: "space", 0xA0: "nbspace"}
    glyf, metrics = {}, {}

    nd = TTGlyphPen(None)
    for ring in ([(60, 0), (60, CAP), (440, CAP), (440, 0)], [(120, 60), (380, 60), (380, CAP - 60), (120, CAP - 60)]):
        nd.moveTo(ring[0])
        for p in ring[1:]:
            nd.lineTo(p)
        nd.closePath()
    glyf[".notdef"] = nd.glyph()
    metrics[".notdef"] = (500, 60)
    space = int(round(250 + s * 0.4))
    for n in ("space", "nbspace"):
        glyf[n] = TTGlyphPen(None).glyph()
        metrics[n] = (space, 0)

    for ch in sorted(table, key=ord):
        g = shape_of(ch, table, s)
        if g.is_empty:
            continue
        name = f"uni{ord(ch):04X}"
        x0, _, x1, _ = g.bounds
        entry = table[ch]
        fixed = entry[2] if entry[0] not in ("rot", "acute", "tilde", "dier", "cedilla") else None
        if fixed is not None:
            advance = int(round(fixed + s + 2 * SIDE))
            dx = (advance - (x1 - x0)) / 2 - x0
        else:
            advance = int(round(x1 - x0 + 2 * SIDE))
            dx = SIDE - x0
        glyf[name] = draw(g, dx)
        metrics[name] = (advance, int(round(x0 + dx)))
        order.append(name)
        cmap[ord(ch)] = name

    family = "Polykov Grid"
    fb = FontBuilder(1000, isTTF=True)
    fb.setupGlyphOrder(order)
    fb.setupCharacterMap(cmap)
    fb.setupGlyf(glyf)
    fb.setupHorizontalMetrics(metrics)
    fb.setupHorizontalHeader(ascent=900, descent=-240)
    fb.setupNameTable({"familyName": family, "styleName": weight, "uniqueFontIdentifier": f"PolykovGrid-{weight}",
                       "fullName": f"{family} {weight}", "psName": f"PolykovGrid-{weight}", "version": "Version 1.000",
                       "copyright": "Escape from Polykov. Original design."})
    fb.setupOS2(sTypoAscender=900, sTypoDescender=-240, sTypoLineGap=0, usWinAscent=920, usWinDescent=260,
                sxHeight=XH, sCapHeight=CAP, usWeightClass=700 if weight == "Bold" else 400,
                fsSelection=0x20 if weight == "Bold" else 0x40)
    fb.setupPost()
    fb.setupHead(unitsPerEm=1000)
    OUT.mkdir(parents=True, exist_ok=True)
    path = OUT / f"PolykovGrid-{weight}.ttf"
    fb.save(str(path))
    print("wrote", path.relative_to(ROOT), len(order), "glyphs")
    return path


if __name__ == "__main__":
    build("Regular", 84)
    build("Bold", 118)
