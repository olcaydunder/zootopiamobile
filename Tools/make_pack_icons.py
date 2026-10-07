"""Kredi pack icons (the five Google Play in-app products), in the style of the UI icon set: dark outline, gradients,
a highlight, the paw Kredi coin. Bigger packs show more coins: one coin, a stack, a pile, a sack, a treasure chest.

Writes
  Assets/Resources/UI/Icons/pack_<product id>.png   256x256, transparent (store cards in the game)
  Tools/store/products/<product id>.png             512x512, 32-bit PNG with background (Play Console > in-app
                                                    product > Icon; Google: 512x512, up to 1 MB, no text)
Run:  python3 Tools/make_pack_icons.py   (needs cairosvg)
"""
import math
import os

import cairosvg

HERE = os.path.dirname(os.path.abspath(__file__))
GAME = os.path.join(HERE, "..", "Assets", "Resources", "UI", "Icons")
STORE = os.path.join(HERE, "store", "products")
INK = "#141820"

DEFS = (
    '<linearGradient id="face" x1="0" y1="0" x2="0.3" y2="1"><stop offset="0" stop-color="#ffe98a"/>'
    '<stop offset="1" stop-color="#e2961a"/></linearGradient>'
    '<linearGradient id="inner" x1="0" y1="0" x2="0.2" y2="1"><stop offset="0" stop-color="#f7c843"/>'
    '<stop offset="1" stop-color="#d98d12"/></linearGradient>'
    '<linearGradient id="edge" x1="0" y1="0" x2="1" y2="0"><stop offset="0" stop-color="#9a5a0a"/>'
    '<stop offset="0.45" stop-color="#d9921c"/><stop offset="1" stop-color="#8a4f08"/></linearGradient>'
    '<linearGradient id="wood" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="#b8743a"/>'
    '<stop offset="1" stop-color="#6a3a18"/></linearGradient>'
    '<linearGradient id="woodDark" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="#6a3a18"/>'
    '<stop offset="1" stop-color="#3a1e0a"/></linearGradient>'
    '<linearGradient id="band" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="#ffe27a"/>'
    '<stop offset="1" stop-color="#c8840e"/></linearGradient>'
    '<linearGradient id="sack" x1="0" y1="0" x2="0.4" y2="1"><stop offset="0" stop-color="#9a7cf0"/>'
    '<stop offset="1" stop-color="#4a2a9a"/></linearGradient>'
    '<linearGradient id="rope" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="#ffe27a"/>'
    '<stop offset="1" stop-color="#c8840e"/></linearGradient>'
    '<radialGradient id="shine" cx="0.5" cy="0.5" r="0.5"><stop offset="0" stop-color="#fff6c0" stop-opacity="0.9"/>'
    '<stop offset="1" stop-color="#fff6c0" stop-opacity="0"/></radialGradient>'
)


def f(v):
    return "%.2f" % v


def paw(cx, cy, s, dark, light):
    """Paw print centred at (cx, cy), s = coin radius; embossed: dark copy offset down, light copy on top."""
    out = ""
    for col, dy in ((dark, 0.035 * s), (light, 0.0)):
        y = cy + dy
        out += '<ellipse cx="%s" cy="%s" rx="%s" ry="%s" fill="%s"/>' % (f(cx), f(y + 0.16 * s), f(0.27 * s), f(0.21 * s), col)
        for ox, oy, rr in ((-0.3, -0.1, 0.1), (-0.11, -0.3, 0.105), (0.11, -0.3, 0.105), (0.3, -0.1, 0.1)):
            out += '<ellipse cx="%s" cy="%s" rx="%s" ry="%s" fill="%s"/>' % (f(cx + ox * s), f(y + oy * s), f(rr * s), f(rr * 1.25 * s), col)
    return out


def coin(cx, cy, r, sparkle=False):
    """A coin facing the viewer."""
    w = max(2.0, 0.075 * r)
    out = '<circle cx="%s" cy="%s" r="%s" fill="url(#edge)" stroke="%s" stroke-width="%s"/>' % (f(cx), f(cy + 0.1 * r), f(r), INK, f(w))
    out += '<circle cx="%s" cy="%s" r="%s" fill="url(#face)" stroke="%s" stroke-width="%s"/>' % (f(cx), f(cy), f(r), INK, f(w))
    out += '<circle cx="%s" cy="%s" r="%s" fill="url(#inner)" stroke="#b8720e" stroke-width="%s"/>' % (f(cx), f(cy), f(0.76 * r), f(0.035 * r))
    for i in range(18):
        a = i / 18.0 * 2 * math.pi
        out += '<circle cx="%s" cy="%s" r="%s" fill="#b8720e" opacity="0.55"/>' % (f(cx + math.cos(a) * 0.87 * r), f(cy + math.sin(a) * 0.87 * r), f(0.028 * r))
    out += paw(cx, cy + 0.02 * r, r, "#8a4c06", "#e39a1a")
    out += ('<path d="M%s %s A %s %s 0 0 1 %s %s" stroke="#fff" stroke-width="%s" fill="none" opacity="0.45" stroke-linecap="round"/>'
            % (f(cx - 0.72 * r), f(cy - 0.2 * r), f(0.76 * r), f(0.76 * r), f(cx + 0.1 * r), f(cy - 0.75 * r), f(0.07 * r)))
    if sparkle:
        out += star(cx + 0.62 * r, cy - 0.62 * r, 0.2 * r)
    return out


def flat(cx, cy, r, squash=0.36):
    """A coin lying flat, seen from above at an angle (for stacks and heaps)."""
    ry = r * squash
    t = 0.2 * r
    w = max(2.0, 0.07 * r)
    out = ('<path d="M%s %s L%s %s A %s %s 0 0 0 %s %s L%s %s A %s %s 0 0 1 %s %s Z" fill="url(#edge)" stroke="%s" stroke-width="%s" stroke-linejoin="round"/>'
           % (f(cx - r), f(cy), f(cx - r), f(cy + t), f(r), f(ry), f(cx + r), f(cy + t), f(cx + r), f(cy), f(r), f(ry), f(cx - r), f(cy), INK, f(w)))
    for i in range(1, 8):
        x = cx - r + 2 * r * i / 8.0
        dy = ry * math.sqrt(max(0.0, 1 - ((x - cx) / r) ** 2))
        out += '<line x1="%s" y1="%s" x2="%s" y2="%s" stroke="#7a4006" stroke-width="%s" opacity="0.45"/>' % (f(x), f(cy + dy + 0.03 * r), f(x), f(cy + dy + t - 0.03 * r), f(0.035 * r))
    out += '<ellipse cx="%s" cy="%s" rx="%s" ry="%s" fill="url(#face)" stroke="%s" stroke-width="%s"/>' % (f(cx), f(cy), f(r), f(ry), INK, f(w))
    out += '<ellipse cx="%s" cy="%s" rx="%s" ry="%s" fill="url(#inner)" stroke="#b8720e" stroke-width="%s"/>' % (f(cx), f(cy), f(0.74 * r), f(0.74 * ry), f(0.03 * r))
    out += '<g transform="translate(%s %s) scale(1 %s)">%s</g>' % (f(cx), f(cy), f(squash), paw(0, 0, 0.8 * r, "#8a4c06", "#e39a1a"))
    return out


def stack(cx, base, r, n):
    t = 0.2 * r
    return "".join(flat(cx + (0.04 * r if i % 2 else -0.03 * r), base - i * t * 1.02, r) for i in range(n))


def star(x, y, s):
    return ('<path d="M%s %s Q%s %s %s %s Q%s %s %s %s Q%s %s %s %s Q%s %s %s %s Z" fill="#fff"/>'
            % (f(x), f(y - s), f(x + 0.18 * s), f(y - 0.18 * s), f(x + s), f(y), f(x + 0.18 * s), f(y + 0.18 * s), f(x), f(y + s),
               f(x - 0.18 * s), f(y + 0.18 * s), f(x - s), f(y), f(x - 0.18 * s), f(y - 0.18 * s), f(x), f(y - s)))


def shadow(cx, cy, rx, ry=None):
    return '<ellipse cx="%s" cy="%s" rx="%s" ry="%s" fill="#000" opacity="0.35"/>' % (f(cx), f(cy), f(rx), f(ry or rx * 0.16))


def gem(x, y, s, light, dark):
    return ('<path d="M%s %s L%s %s L%s %s L%s %s L%s %s Z" fill="%s" stroke="%s" stroke-width="%s" stroke-linejoin="round"/>'
            % (f(x - s), f(y - 0.3 * s), f(x - 0.5 * s), f(y - s), f(x + 0.5 * s), f(y - s), f(x + s), f(y - 0.3 * s), f(x), f(y + s),
               light, INK, f(0.16 * s)) +
            '<path d="M%s %s L%s %s L%s %s Z" fill="%s" opacity="0.8"/>' % (f(x - s * 0.95), f(y - 0.3 * s), f(x + s * 0.95), f(y - 0.3 * s), f(x), f(y + s * 0.95), dark) +
            '<path d="M%s %s L%s %s" stroke="#fff" stroke-width="%s" opacity="0.7" stroke-linecap="round"/>' % (f(x - 0.45 * s), f(y - 0.75 * s), f(x - 0.1 * s), f(y - 0.75 * s), f(0.14 * s)))


# ----- the five packs (256 x 256 canvas) -----

def pack1():
    return shadow(128, 222, 80) + coin(128, 124, 92, sparkle=True)


def pack2():
    return (shadow(128, 224, 104) +
            stack(96, 196, 58, 6) +
            coin(162, 146, 66) +
            star(206, 54, 16))


def pack3():
    return (shadow(128, 226, 116) +
            stack(70, 204, 46, 7) +
            stack(186, 206, 46, 4) +
            flat(128, 214, 44) +
            coin(128, 140, 60) +
            coin(196, 168, 42) +
            coin(62, 176, 40) +
            star(212, 52, 16) + star(44, 70, 10))


def pack4():
    # coin sack: purple bag, gold rope, coins spilling out at the top and the bottom
    bag = ('<path d="M92 92 C 40 120, 26 178, 46 206 C 62 228, 194 228, 210 206 C 230 178, 216 120, 164 92 Z" '
           'fill="url(#sack)" stroke="%s" stroke-width="10" stroke-linejoin="round"/>' % INK +
           '<path d="M70 124 C 54 150, 52 184, 66 204" stroke="#fff" stroke-width="9" fill="none" opacity="0.3" stroke-linecap="round"/>'
           # neck and frill
           '<path d="M98 94 C 86 70, 80 54, 92 46 C 108 56, 148 56, 164 46 C 176 54, 170 70, 158 94 Z" '
           'fill="url(#sack)" stroke="%s" stroke-width="9" stroke-linejoin="round"/>' % INK +
           '<rect x="88" y="86" width="80" height="18" rx="9" fill="url(#rope)" stroke="%s" stroke-width="8"/>' % INK +
           '<path d="M150 100 C 160 118, 168 126, 176 130" stroke="%s" stroke-width="14" fill="none" stroke-linecap="round"/>' % INK +
           '<path d="M150 100 C 160 118, 168 126, 176 130" stroke="#f2c040" stroke-width="7" fill="none" stroke-linecap="round"/>'
           # paw emblem on the bag
           '<circle cx="128" cy="164" r="34" fill="#2a1660" opacity="0.55"/>' +
           paw(128, 164, 40, "#1a0c44", "#f2c040"))
    return (shadow(128, 226, 116) +
            coin(108, 46, 26) + coin(146, 40, 24) +
            bag +
            flat(56, 214, 30) + flat(206, 212, 32) + coin(214, 186, 26) + coin(40, 192, 22) +
            star(214, 64, 15) + star(40, 84, 10))


def pack5():
    # open treasure chest overflowing with coins, a few gems
    lid = ('<path d="M44 108 L58 44 C 60 36, 196 36, 198 44 L212 108 Z" fill="url(#woodDark)" stroke="%s" stroke-width="10" stroke-linejoin="round"/>' % INK +
           '<path d="M62 52 L196 52" stroke="#fff" stroke-width="6" opacity="0.18"/>'
           '<path d="M70 104 L80 44 M186 104 L176 44" stroke="url(#band)" stroke-width="16"/>'
           '<path d="M70 104 L80 44 M186 104 L176 44" stroke="%s" stroke-width="3" opacity="0.6"/>' % INK)
    glow = '<ellipse cx="128" cy="104" rx="104" ry="58" fill="url(#shine)"/>'
    heap = (flat(84, 110, 34) + flat(172, 110, 34) + flat(128, 98, 36) +
            coin(96, 84, 30) + coin(160, 80, 30) + coin(128, 64, 34) +
            gem(70, 92, 15, "#ff5a6a", "#b01a30") + gem(190, 90, 14, "#4ae0a0", "#108a50") + gem(150, 54, 12, "#5ab0ff", "#1a5ac0"))
    body = ('<rect x="38" y="112" width="180" height="104" rx="12" fill="url(#wood)" stroke="%s" stroke-width="10"/>' % INK +
            '<path d="M48 146 H208 M48 180 H208" stroke="#4a2408" stroke-width="5" opacity="0.6"/>'
            '<rect x="38" y="104" width="180" height="22" rx="8" fill="url(#band)" stroke="%s" stroke-width="9"/>' % INK +
            '<rect x="62" y="112" width="20" height="104" fill="url(#band)" stroke="%s" stroke-width="7"/>' % INK +
            '<rect x="174" y="112" width="20" height="104" fill="url(#band)" stroke="%s" stroke-width="7"/>' % INK +
            '<rect x="110" y="128" width="36" height="44" rx="8" fill="url(#band)" stroke="%s" stroke-width="8"/>' % INK +
            '<circle cx="128" cy="146" r="6" fill="%s"/><rect x="125" y="148" width="6" height="14" rx="3" fill="%s"/>' % (INK, INK) +
            '<rect x="50" y="120" width="156" height="6" rx="3" fill="#fff" opacity="0.35"/>')
    return (shadow(128, 228, 116) + lid + glow + heap + body +
            coin(214, 196, 24) + flat(44, 214, 24) +
            star(222, 40, 16) + star(30, 60, 11) + star(118, 30, 9))


PACKS = [
    ("kredi_1000", pack1, "#f2b640"),
    ("kredi_2750", pack2, "#f2a030"),
    ("kredi_6000", pack3, "#ff7a30"),
    ("kredi_13000", pack4, "#a070ff"),
    ("kredi_30000", pack5, "#40d0ff"),
]


def game_svg(art):
    return ('<svg xmlns="http://www.w3.org/2000/svg" width="256" height="256" viewBox="0 0 256 256">'
            "<defs>" + DEFS + "</defs>" + art + "</svg>")


def store_svg(art, tint):
    # full square background (the store crops it to its own shape), a soft glow in the pack's colour, no text
    bg = ('<radialGradient id="bg" cx="0.5" cy="0.42" r="0.75"><stop offset="0" stop-color="#2a3448"/>'
          '<stop offset="1" stop-color="#0c1018"/></radialGradient>'
          '<radialGradient id="tint" cx="0.5" cy="0.5" r="0.5"><stop offset="0" stop-color="%s" stop-opacity="0.55"/>'
          '<stop offset="1" stop-color="%s" stop-opacity="0"/></radialGradient>' % (tint, tint))
    rays = "".join('<path d="M256 256 L%s %s L%s %s Z" fill="#ffffff" opacity="0.035"/>'
                   % (f(256 + 400 * math.cos(a)), f(256 + 400 * math.sin(a)), f(256 + 400 * math.cos(a + 0.16)), f(256 + 400 * math.sin(a + 0.16)))
                   for a in [i * math.pi / 8 for i in range(16)])
    return ('<svg xmlns="http://www.w3.org/2000/svg" width="512" height="512" viewBox="0 0 512 512">'
            "<defs>" + DEFS + bg + "</defs>"
            '<rect width="512" height="512" fill="url(#bg)"/>' + rays +
            '<circle cx="256" cy="250" r="230" fill="url(#tint)"/>'
            '<g transform="translate(51 44) scale(1.6)">' + art + "</g></svg>")


if __name__ == "__main__":
    os.makedirs(STORE, exist_ok=True)
    for pid, fn, tint in PACKS:
        art = fn()
        g = os.path.join(GAME, "pack_%s.png" % pid)
        cairosvg.svg2png(bytestring=game_svg(art).encode(), write_to=g, output_width=256, output_height=256)
        s = os.path.join(STORE, "%s.png" % pid)
        cairosvg.svg2png(bytestring=store_svg(art, tint).encode(), write_to=s, output_width=512, output_height=512)
        print(pid, os.path.getsize(g), os.path.getsize(s))
