"""Draws the newer UI icons (SVG → 256x256 PNG) into Assets/Resources/UI/Icons/, in the style of the
existing set (dark outline, gradients, a highlight): gift crates (bronze / silver / gold, and each one's
body and lid for the opening animation, in Resources/UI/Crates at 512 px), character, store, missions, gift, chat, follow. Run:  python3 Tools/make_icons.py   (needs cairosvg)
"""
import os
import cairosvg

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "Assets", "Resources", "UI", "Icons")
INK = "#141820"


def svg(body, defs=""):
    return ('<svg xmlns="http://www.w3.org/2000/svg" width="256" height="256" viewBox="0 0 256 256">'
            "<defs>" + defs + "</defs>" + body + "</svg>")


def grad(gid, top, bottom, x2="0", y2="1"):
    return ('<linearGradient id="%s" x1="0" y1="0" x2="%s" y2="%s"><stop offset="0" stop-color="%s"/>'
            '<stop offset="1" stop-color="%s"/></linearGradient>' % (gid, x2, y2, top, bottom))


def crate(name, light, dark, ribbon_l, ribbon_d, gem):
    defs = (grad("body", light, dark) + grad("lid", light, dark) + grad("rib", ribbon_l, ribbon_d, "1", "0") +
            '<radialGradient id="glow" cx="0.5" cy="0.45" r="0.5"><stop offset="0" stop-color="#ffffff" stop-opacity="0.55"/>'
            '<stop offset="1" stop-color="#ffffff" stop-opacity="0"/></radialGradient>')
    body = (
        '<ellipse cx="128" cy="222" rx="92" ry="14" fill="#000" opacity="0.35"/>'
        # box body
        '<rect x="40" y="104" width="176" height="114" rx="12" fill="url(#body)" stroke="%s" stroke-width="10"/>' % INK +
        '<rect x="52" y="116" width="152" height="18" rx="6" fill="#fff" opacity="0.18"/>'
        # lid
        '<rect x="28" y="76" width="200" height="44" rx="12" fill="url(#lid)" stroke="%s" stroke-width="10"/>' % INK +
        '<rect x="40" y="84" width="176" height="10" rx="5" fill="#fff" opacity="0.3"/>'
        # ribbon
        '<rect x="110" y="76" width="36" height="142" fill="url(#rib)" stroke="%s" stroke-width="8"/>' % INK +
        # bow
        '<path d="M128 78 C 96 30, 54 42, 70 70 C 80 86, 110 82, 128 78 Z" fill="url(#rib)" stroke="%s" stroke-width="9" stroke-linejoin="round"/>' % INK +
        '<path d="M128 78 C 160 30, 202 42, 186 70 C 176 86, 146 82, 128 78 Z" fill="url(#rib)" stroke="%s" stroke-width="9" stroke-linejoin="round"/>' % INK +
        '<circle cx="128" cy="76" r="15" fill="%s" stroke="%s" stroke-width="8"/>' % (gem, INK) +
        '<circle cx="123" cy="71" r="5" fill="#fff" opacity="0.8"/>'
        # sparkle
        '<path d="M206 40 l6 16 l16 6 l-16 6 l-6 16 l-6 -16 l-16 -6 l16 -6 z" fill="#fff" opacity="0.9"/>'
        '<ellipse cx="128" cy="150" rx="70" ry="40" fill="url(#glow)"/>'
    )
    return svg(body, defs)


def crate_parts(light, dark, ribbon_l, ribbon_d, gem):
    """The same box as crate(), in two aligned pieces for the opening animation: body and lid (+ bow)."""
    defs = grad("body", light, dark) + grad("lid", light, dark) + grad("rib", ribbon_l, ribbon_d, "1", "0")
    body = svg(
        '<ellipse cx="128" cy="222" rx="92" ry="14" fill="#000" opacity="0.35"/>'
        '<rect x="40" y="104" width="176" height="114" rx="12" fill="url(#body)" stroke="%s" stroke-width="10"/>' % INK +
        '<rect x="52" y="116" width="152" height="18" rx="6" fill="#fff" opacity="0.18"/>'
        '<rect x="110" y="104" width="36" height="114" fill="url(#rib)" stroke="%s" stroke-width="8"/>' % INK +
        '<rect x="46" y="104" width="164" height="10" fill="#000" opacity="0.35"/>', defs)
    lid = svg(
        '<rect x="28" y="76" width="200" height="44" rx="12" fill="url(#lid)" stroke="%s" stroke-width="10"/>' % INK +
        '<rect x="40" y="84" width="176" height="10" rx="5" fill="#fff" opacity="0.3"/>'
        '<rect x="110" y="76" width="36" height="44" fill="url(#rib)" stroke="%s" stroke-width="8"/>' % INK +
        '<path d="M128 78 C 96 30, 54 42, 70 70 C 80 86, 110 82, 128 78 Z" fill="url(#rib)" stroke="%s" stroke-width="9" stroke-linejoin="round"/>' % INK +
        '<path d="M128 78 C 160 30, 202 42, 186 70 C 176 86, 146 82, 128 78 Z" fill="url(#rib)" stroke="%s" stroke-width="9" stroke-linejoin="round"/>' % INK +
        '<circle cx="128" cy="76" r="15" fill="%s" stroke="%s" stroke-width="8"/>' % (gem, INK) +
        '<circle cx="123" cy="71" r="5" fill="#fff" opacity="0.8"/>', defs)
    return body, lid


def badge(inner, ring_l, ring_d, fill_l, fill_d):
    defs = grad("ring", ring_l, ring_d) + grad("fill", fill_l, fill_d)
    body = ('<circle cx="128" cy="132" r="106" fill="#000" opacity="0.3"/>'
            '<circle cx="128" cy="126" r="104" fill="url(#ring)" stroke="%s" stroke-width="10"/>' % INK +
            '<circle cx="128" cy="126" r="82" fill="url(#fill)" stroke="%s" stroke-width="6"/>' % INK +
            inner +
            '<path d="M60 92 A 80 80 0 0 1 150 48" stroke="#fff" stroke-width="10" fill="none" opacity="0.35" stroke-linecap="round"/>')
    return svg(body, defs)


ICONS = {
    "crate_bronze": crate("bronze", "#e0a46a", "#8a4f22", "#f2d36b", "#b07a18", "#ff5a4a"),
    "crate_silver": crate("silver", "#eef3f8", "#8f9cab", "#5aa8ff", "#1f5fc0", "#4ad0ff"),
    "crate_gold": crate("gold", "#ffe680", "#d08a10", "#b04cff", "#5a1fa8", "#ff4ad6"),
    # character: helmeted head and shoulders
    "skin": badge('<path d="M78 196 C 80 158, 104 146, 128 146 C 152 146, 176 158, 178 196 Z" fill="#3c4b38" stroke="%s" stroke-width="8"/>'
                  '<circle cx="128" cy="112" r="34" fill="#e8c09a" stroke="%s" stroke-width="8"/>'
                  '<path d="M90 108 C 90 70, 166 70, 166 108 L 166 100 C 158 84, 98 84, 90 100 Z" fill="#556b3a" stroke="%s" stroke-width="8" stroke-linejoin="round"/>'
                  '<rect x="104" y="108" width="48" height="12" rx="6" fill="#20262e"/>' % (INK, INK, INK),
                  "#f4f6f8", "#9aa4ae", "#6fa8e8", "#24508f"),
    # store: shopping bag with a paw
    "store": svg(grad("bag", "#ffd34a", "#e08a10") +
                 '<ellipse cx="128" cy="224" rx="84" ry="12" fill="#000" opacity="0.3"/>'
                 '<path d="M92 84 C 92 40, 164 40, 164 84" fill="none" stroke="%s" stroke-width="14" stroke-linecap="round"/>' % INK +
                 '<path d="M92 84 C 92 46, 164 46, 164 84" fill="none" stroke="#fff" stroke-width="5" opacity="0.6" stroke-linecap="round"/>'
                 '<path d="M50 84 L206 84 L192 214 L64 214 Z" fill="url(#bag)" stroke="%s" stroke-width="10" stroke-linejoin="round"/>' % INK +
                 '<ellipse cx="128" cy="160" rx="22" ry="18" fill="%s"/>' % INK +
                 '<circle cx="104" cy="132" r="9" fill="%s"/><circle cx="121" cy="122" r="9" fill="%s"/>' % (INK, INK) +
                 '<circle cx="138" cy="122" r="9" fill="%s"/><circle cx="154" cy="132" r="9" fill="%s"/>' % (INK, INK) +
                 '<path d="M62 96 L 194 96" stroke="#fff" stroke-width="8" opacity="0.35"/>'),
    # missions: clipboard with a check
    "missions": svg(grad("pad", "#7ec8ff", "#2c6fc8") +
                    '<ellipse cx="128" cy="226" rx="78" ry="12" fill="#000" opacity="0.3"/>'
                    '<rect x="56" y="44" width="144" height="178" rx="16" fill="url(#pad)" stroke="%s" stroke-width="10"/>' % INK +
                    '<rect x="74" y="66" width="108" height="140" rx="8" fill="#f4f6f8" stroke="%s" stroke-width="6"/>' % INK +
                    '<rect x="96" y="30" width="64" height="34" rx="10" fill="#c7d0da" stroke="%s" stroke-width="8"/>' % INK +
                    '<path d="M92 132 L 118 160 L 166 102" fill="none" stroke="#2fbf5a" stroke-width="18" stroke-linecap="round" stroke-linejoin="round"/>'
                    '<path d="M92 132 L 118 160 L 166 102" fill="none" stroke="%s" stroke-width="5" stroke-linecap="round" stroke-linejoin="round" opacity="0.4"/>' % INK +
                    '<rect x="88" y="178" width="80" height="8" rx="4" fill="#9aa4ae"/>'),
    # gift: small present (for buttons)
    "gift": crate("gift", "#ff7a7a", "#c02040", "#ffe27a", "#d79a10", "#ffffff"),
    # chat: two speech bubbles
    "chat": svg(grad("b1", "#7ee0a0", "#24a050") + grad("b2", "#ffffff", "#c7d0da") +
                '<path d="M40 60 h120 a18 18 0 0 1 18 18 v58 a18 18 0 0 1 -18 18 h-64 l-34 28 v-28 h-22 a18 18 0 0 1 -18 -18 v-58 a18 18 0 0 1 18 -18 z" fill="url(#b1)" stroke="%s" stroke-width="10" stroke-linejoin="round"/>' % INK +
                '<path d="M110 120 h98 a16 16 0 0 1 16 16 v46 a16 16 0 0 1 -16 16 h-10 v26 l-30 -26 h-58 a16 16 0 0 1 -16 -16 v-46 a16 16 0 0 1 16 -16 z" fill="url(#b2)" stroke="%s" stroke-width="10" stroke-linejoin="round"/>' % INK +
                '<circle cx="140" cy="160" r="8" fill="%s"/><circle cx="164" cy="160" r="8" fill="%s"/><circle cx="188" cy="160" r="8" fill="%s"/>' % (INK, INK, INK)),
    # follow: person with a plus
    "follow": badge('<circle cx="116" cy="108" r="28" fill="#f4f6f8" stroke="%s" stroke-width="8"/>'
                    '<path d="M70 190 C 72 156, 94 142, 116 142 C 138 142, 160 156, 162 190 Z" fill="#f4f6f8" stroke="%s" stroke-width="8"/>'
                    '<circle cx="170" cy="150" r="26" fill="#2fbf5a" stroke="%s" stroke-width="8"/>'
                    '<path d="M170 136 v28 M156 150 h28" stroke="#fff" stroke-width="9" stroke-linecap="round"/>' % (INK, INK, INK),
                    "#ffd34a", "#c88a10", "#ff7a5a", "#c03a2a"),
}

CRATE_COLOURS = {"bronze": ("#e0a46a", "#8a4f22", "#f2d36b", "#b07a18", "#ff5a4a"),
                 "silver": ("#eef3f8", "#8f9cab", "#5aa8ff", "#1f5fc0", "#4ad0ff"),
                 "gold": ("#ffe680", "#d08a10", "#b04cff", "#5a1fa8", "#ff4ad6")}
for name, data in ICONS.items():
    path = os.path.join(OUT, name + ".png")
    cairosvg.svg2png(bytestring=data.encode(), write_to=path, output_width=256, output_height=256)
    print("icon", name, os.path.getsize(path))

# Box pieces for the opening animation: bigger, in Resources/UI/Crates/<id>_body.png and <id>_lid.png.
CRATES = os.path.join(OUT, "..", "Crates")
os.makedirs(CRATES, exist_ok=True)
for cid, cols in CRATE_COLOURS.items():
    for part, data in zip(("body", "lid"), crate_parts(*cols)):
        path = os.path.join(CRATES, "%s_%s.png" % (cid, part))
        cairosvg.svg2png(bytestring=data.encode(), write_to=path, output_width=512, output_height=512)
        print("crate", cid, part, os.path.getsize(path))
