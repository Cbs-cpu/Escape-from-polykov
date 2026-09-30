#!/usr/bin/env python3
"""
Draws the Lobby IMGUI layer (same rects, colours and font sizes as LobbyController/LobbyTheme) on top of a
3D render from ArtSource/Tools/render_lobby_preview.py, to review the lobby look without Unity.

Usage: python3 Tools/lobby_ui_mockup.py <screen> <3d.png> <out.png> [stats.txt]
       screen = main | armorer | armorer_suppressor
"""
import sys
from PIL import Image, ImageDraw, ImageFont

FONT = "/usr/share/fonts/truetype/liberation/LiberationSans-Regular.ttf"
BOLD = "/usr/share/fonts/truetype/liberation/LiberationSans-Bold.ttf"

BACKGROUND = (0.035, 0.038, 0.04, 1)
PANEL = (0.075, 0.08, 0.085, 0.94)
PANEL_LIGHT = (0.115, 0.12, 0.125, 0.96)
BORDER = (0.27, 0.28, 0.285, 1)
TEXT = (0.84, 0.83, 0.78, 1)
TEXT_DIM = (0.52, 0.52, 0.5, 1)
ACCENT = (0.78, 0.68, 0.42, 1)
GOOD = (0.45, 0.76, 0.4, 1)
BAD = (0.86, 0.32, 0.27, 1)


def rgba(c):
    return tuple(int(round(v * 255)) for v in c[:3]) + (int(round((c[3] if len(c) > 3 else 1) * 255)),)


class Gui:
    def __init__(self, base):
        self.img = base.convert("RGBA")
        self.w, self.h = self.img.size

    def fill(self, r, c):
        x, y, w, h = r
        layer = Image.new("RGBA", self.img.size, (0, 0, 0, 0))
        ImageDraw.Draw(layer).rectangle([x, y, x + w - 1, y + h - 1], fill=rgba(c))
        self.img = Image.alpha_composite(self.img, layer)

    def frame(self, r, c, t=1):
        x, y, w, h = r
        self.fill((x, y, w, t), c)
        self.fill((x, y + h - t, w, t), c)
        self.fill((x, y, t, h), c)
        self.fill((x + w - t, y, t, h), c)

    def panel(self, r):
        self.fill(r, PANEL)
        self.frame(r, BORDER)

    def line(self, a, b, c, width):
        layer = Image.new("RGBA", self.img.size, (0, 0, 0, 0))
        ImageDraw.Draw(layer).line([a, b], fill=rgba(c), width=max(1, int(round(width))))
        self.img = Image.alpha_composite(self.img, layer)

    def text(self, r, s, size, color, bold=False, align="left"):
        font = ImageFont.truetype(BOLD if bold else FONT, size)
        x, y, w, h = r
        d = ImageDraw.Draw(self.img)
        lines = s.split("\n")
        lh = size * 1.18
        total = lh * len(lines)
        ty = y + (h - total) / 2
        for ln in lines:
            tw = d.textlength(ln, font=font)
            tx = x + 2 if align == "left" else (x + w - tw - 2 if align == "right" else x + (w - tw) / 2)
            d.text((tx, ty + (lh - size) / 2 - size * 0.08), ln, font=font, fill=rgba(color))
            ty += lh

    def button(self, r, text, kind="normal", subtitle=None, hover=False):
        enabled = kind not in ("locked", "blocked")
        fill = (0.2, 0.18, 0.11, 0.96) if kind == "primary" else (0.17, 0.155, 0.1, 0.96) if kind == "selected" else PANEL_LIGHT
        if hover:
            fill = tuple(a + (b - a) * 0.16 for a, b in zip(fill, ACCENT))
        if not enabled:
            fill = (0.06, 0.063, 0.066, 0.9)
        self.fill(r, fill)
        self.frame(r, ACCENT if kind in ("primary", "selected") or hover else BORDER)
        tc = (0.36, 0.36, 0.35, 1) if not enabled else ACCENT if kind == "primary" else TEXT
        x, y, w, h = r
        self.text((x + 14, y, w - 28, h if subtitle is None else h * 0.62), text, 17, tc, bold=True)
        if subtitle is not None:
            self.text((x + 14, y + h * 0.5, w - 28, h * 0.4), subtitle, 13, (0.4, 0.32, 0.3, 1) if not enabled else TEXT_DIM)


def top_bar(g, where):
    g.fill((0, 0, g.w, 64), (0.05, 0.052, 0.055, 0.97))
    g.fill((0, 64, g.w, 1), BORDER)
    g.text((28, 12, 600, 40), "ESCAPE FROM POLYKOV", 26, TEXT, bold=True)
    g.text((g.w - 520, 12, 490, 40), where, 16, ACCENT, align="right")


def slot(g, r, name, content=None, hover=False):
    g.fill(r, (0.13, 0.12, 0.085, 0.9) if hover else (0.07, 0.075, 0.08, 0.82))
    g.frame(r, (0.2, 0.21, 0.215, 1) if content is None else ACCENT)
    x, y, w, h = r
    g.text((x + 10, y + 6, w - 20, 20), name, 13, TEXT_DIM)
    if content is not None:
        g.text((x + 10, y + h * 0.4, w - 20, 40), content, 16, TEXT)
        g.text((x + 10, y + h - 22, w - 20, 18), "clic: armero", 13, TEXT_DIM)
    else:
        g.text((x + 10, y + h * 0.5, w - 20, 24), "vacío · bloqueado", 13, (0.33, 0.33, 0.32, 1))


def main_screen(g):
    w, h = g.w, g.h
    x, y, bw, bh, gap = 40, 110, 330, 68, 12
    g.button((x, y, bw, bh + 8), "ENTRAR AL JUEGO", "primary", "Arena de pruebas")
    y += bh + 8 + gap
    g.button((x, y, bw, bh), "ARMERO", "normal", "Modificar la M1911A1")
    y += bh + gap * 2
    for locked in ("PERSONAJE", "ALIJO", "COMERCIANTES", "MISIONES"):
        g.button((x, y, bw, 52), locked, "locked", "Próximamente")
        y += 52 + gap
    top, sh, gp, sw = 150, 86, 14, 210
    for i, name in enumerate(("CASCO", "CARA", "CHALECO", "MOCHILA")):
        slot(g, (w * 0.5 - 440, top + i * (sh + gp), sw, sh), name)
    for i, name in enumerate(("ARMA PRINCIPAL", "ARMA SECUNDARIA", "PISTOLERA", "CUERPO A CUERPO")):
        slot(g, (w * 0.5 + 230, top + i * (sh + gp), sw, sh), name, "M1911A1" if i == 2 else None)
    g.text((40, h - 44, 700, 28), "Nivel 1  ·  Sin conexión  ·  v0.1 (Fase 1)", 13, TEXT_DIM)
    top_bar(g, "MENÚ PRINCIPAL")


def fmt(kind, v):
    unit = {"Ergonomics": "", "Recoil": "°", "Weight": " kg", "Length": " cm"}.get(kind, " %")
    if kind in ("Recoil", "Weight"):
        return f"{v:.2f}{unit}"
    if kind == "Length":
        return f"{v:.1f}{unit}"
    return f"{v:.0f}{unit}"


LABEL = {"Ergonomics": "Ergonomía", "Recoil": "Retroceso vertical", "Weight": "Peso", "Length": "Longitud",
         "Loudness": "Sonoridad", "MuzzleFlash": "Fogonazo"}


def armorer(g, rows, anchors, suppressed):
    w, h = g.w, g.h
    left_w, right_w = 360, 400
    orbit = (left_w + 20, 80, w - left_w - right_w - 40, h - 100)
    ox, oy, ow, oh = orbit
    # LobbyController.BoxOffsets: each box next to its part, clamped inside the orbit area.
    offsets = {"Muzzle": (130, -190), "Barrel": (-170, -200), "Grips": (-270, 230), "Magazine": (230, 130)}
    centers = {}
    for k, (dx, dy) in offsets.items():
        ax, ay = anchors[k]
        centers[k] = (min(max(ax + dx, ox + 130), ox + ow - 130), min(max(ay + dy, oy + 40), oy + oh - 70))
    names = {"Muzzle": "BOCA", "Barrel": "CAÑÓN", "Grips": "CACHAS", "Magazine": "CARGADOR"}
    parts = {"Muzzle": "Silenciador .45 ACP" if suppressed else "Ninguno",
             "Barrel": "Cañón roscado" if suppressed else "Cañón estándar",
             "Grips": "Cachas de madera", "Magazine": "Cargador estándar (7)"}
    for k in ("Muzzle", "Barrel", "Grips", "Magazine"):
        sel = k == "Muzzle"
        cx, cy = centers[k]
        box = (cx - 120, cy - 30, 240, 60)
        ax, ay = anchors[k]
        g.line((cx, cy), (ax, ay), ACCENT if sel else (0.55, 0.55, 0.52, 0.8), 2 if sel else 1.2)
        g.fill((ax - 4, ay - 4, 8, 8), ACCENT if sel else TEXT)
        g.button(box, names[k], "selected" if sel else "normal", parts[k])

    r = (28, 92, left_w, 470)
    g.panel(r)
    g.text((r[0] + 18, r[1] + 10, r[2] - 36, 32), "CARACTERÍSTICAS", 17, ACCENT, bold=True)
    g.fill((r[0] + 18, r[1] + 46, r[2] - 36, 1), BORDER)
    y = r[1] + 58
    for kind, cur, delta, verdict in rows:
        c = GOOD if verdict == "Better" else BAD if verdict == "Worse" else TEXT_DIM
        g.text((r[0] + 18, y, 170, 30), LABEL[kind], 16, TEXT)
        g.text((r[0] + 160, y, 100, 30), fmt(kind, cur), 16, c, align="right")
        if verdict != "Same":
            g.text((r[0] + 262, y, r[2] - 280, 30), ("+" if delta > 0 else "") + fmt(kind, delta), 16, c, align="right")
        y += 34
    g.fill((r[0] + 18, y + 6, r[2] - 36, 1), BORDER)
    g.text((r[0] + 18, y + 14, r[2] - 36, 60), "Verde: mejora respecto a la configuración de fábrica.\nRojo: empeora.", 13, TEXT_DIM)

    r = (w - right_w - 28, 92, right_w, h - 190)
    g.panel(r)
    g.text((r[0] + 18, r[1] + 10, r[2] - 36, 32), "PIEZAS · BOCA", 17, ACCENT, bold=True)
    g.fill((r[0] + 18, r[1] + 46, r[2] - 36, 1), BORDER)
    options = [("Ninguno", "normal" if suppressed else "selected", "Disponible" if suppressed else "MONTADO"),
               ("Silenciador .45 ACP", "selected" if suppressed else "normal",
                "MONTADO" if suppressed else "monta también: Cañón roscado")]
    y = r[1] + 60
    for name, kind, sub in options:
        g.button((r[0] + 18, y, r[2] - 36, 66), name, kind, sub)
        y += 76

    g.button((28, h - 84, 200, 52), "< VOLVER")
    g.button((240, h - 84, 220, 52), "RESTABLECER")
    if suppressed:
        g.text((480, h - 84, 300, 52), "Configuración guardada", 13, GOOD)
    g.text((ox, h - 40, ow, 24), "Arrastra para girar · Rueda para acercar · Esc para volver", 13, TEXT_DIM)
    top_bar(g, "ARMERO  ·  M1911A1")


def read_rows(path, suppressed):
    rows, block = [], -1
    for ln in open(path):
        if ln.startswith("BUILD"):
            block += 1
        elif ln.startswith("ROW") and block == (1 if suppressed else 0):
            _, kind, cur, delta, verdict = ln.split()
            rows.append((kind, float(cur), float(delta), verdict))
    return rows


if __name__ == "__main__":
    screen, src, out = sys.argv[1:4]
    g = Gui(Image.open(src))
    if screen == "main":
        main_screen(g)
    else:
        suppressed = screen == "armorer_suppressor"
        anchors = {}
        for ln in open(src.rsplit(".", 1)[0] + "_anchors.txt"):
            k, x, y = ln.split()
            anchors[k] = (float(x), float(y))
        armorer(g, read_rows(sys.argv[4], suppressed), anchors, suppressed)
    g.img.convert("RGB").save(out)
    print("wrote", out)
