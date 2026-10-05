"""Draws the inventory icons (SVG -> 256x256 PNG) into Assets/Resources/UI/Icons/ in the style of the other
icons (dark outline, gradients, a highlight): helmets, vests, boots, explosives, tactical and medical items,
perk badges, inventory categories and a few lobby icons (profile, trophy, wheel, modes, power, calendar).
Masks get their icons from the 3D models (Tools/blender/make_masks.py).   Run:  python3 Tools/make_gear_icons.py
"""
import os
import cairosvg

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "Assets", "Resources", "UI", "Icons")
INK = "#141820"
SHADOW = '<ellipse cx="128" cy="226" rx="84" ry="12" fill="#000" opacity="0.3"/>'


def svg(body, defs=""):
    return ('<svg xmlns="http://www.w3.org/2000/svg" width="256" height="256" viewBox="0 0 256 256">'
            "<defs>" + defs + "</defs>" + body + "</svg>")


def grad(gid, top, bottom, x2="0", y2="1"):
    return ('<linearGradient id="%s" x1="0" y1="0" x2="%s" y2="%s"><stop offset="0" stop-color="%s"/>'
            '<stop offset="1" stop-color="%s"/></linearGradient>' % (gid, x2, y2, top, bottom))


def stroke(w=10):
    return 'stroke="%s" stroke-width="%d" stroke-linejoin="round" stroke-linecap="round"' % (INK, w)


# ----- Helmets -----

def helmet(light, dark, extra="", visor=False):
    d = grad("h", light, dark)
    body = (SHADOW +
            '<path d="M40 168 C 34 92, 84 50, 128 50 C 172 50, 222 92, 216 168 Z" fill="url(#h)" %s/>' % stroke() +
            '<path d="M28 168 L 228 168 L 216 188 L 40 188 Z" fill="url(#h)" %s/>' % stroke() +
            '<path d="M70 96 C 88 72, 112 64, 132 64" fill="none" stroke="#fff" stroke-width="10" opacity="0.35" stroke-linecap="round"/>')
    if visor:
        body += ('<path d="M60 150 C 70 196, 186 196, 196 150 L 196 130 L 60 130 Z" fill="#2a3a4a" opacity="0.92" %s/>' % stroke(8) +
                 '<path d="M76 140 L 120 140" stroke="#9fd8ff" stroke-width="8" opacity="0.7" stroke-linecap="round"/>')
    return svg(body + extra, d)


HELMETS = {
    "gear_h_train": helmet("#8a9a62", "#4e5a34"),
    "gear_h_tactical": helmet("#d8c08a", "#9a7c44",
                              '<rect x="186" y="118" width="20" height="44" rx="4" fill="#3a3a3a" %s/>' % stroke(6) +
                              '<path d="M70 168 C 80 208, 176 208, 186 168" fill="none" stroke="%s" stroke-width="8"/>' % INK),
    "gear_h_night": helmet("#4a525e", "#20252c",
                           '<rect x="92" y="58" width="72" height="22" rx="6" fill="#2c3036" %s/>' % stroke(6) +
                           '<rect x="96" y="20" width="26" height="46" rx="8" fill="#3a4048" %s/>' % stroke(7) +
                           '<rect x="134" y="20" width="26" height="46" rx="8" fill="#3a4048" %s/>' % stroke(7) +
                           '<circle cx="109" cy="28" r="7" fill="#7cff8a"/><circle cx="147" cy="28" r="7" fill="#7cff8a"/>'),
    "gear_h_heavy": helmet("#5c6470", "#2a2f37", visor=True),
}

# ----- Vests -----


def vest(light, dark, extra="", shoulders=False):
    d = grad("v", light, dark)
    body = (SHADOW +
            '<path d="M70 40 L 104 40 C 108 62, 148 62, 152 40 L 186 40 L 196 92 L 196 212 L 60 212 L 60 92 Z" fill="url(#v)" %s/>' % stroke() +
            '<path d="M60 92 L 196 92" stroke="%s" stroke-width="7"/>' % INK +
            '<rect x="84" y="116" width="88" height="56" rx="8" fill="#000" opacity="0.18" %s/>' % stroke(6) +
            '<path d="M76 56 L 92 56" stroke="#fff" stroke-width="8" opacity="0.4" stroke-linecap="round"/>')
    if shoulders:
        body += ('<path d="M44 52 L 72 40 L 80 96 L 44 100 Z" fill="url(#v)" %s/>' % stroke(8) +
                 '<path d="M212 52 L 184 40 L 176 96 L 212 100 Z" fill="url(#v)" %s/>' % stroke(8))
    return svg(body + extra, d)


def pouches(color, n=3, y=176):
    out = ""
    w = 120 / n
    for i in range(n):
        x = 68 + i * w
        out += '<rect x="%d" y="%d" width="%d" height="30" rx="5" fill="%s" %s/>' % (x + 3, y, w - 6, color, stroke(6))
    return out


VESTS = {
    "gear_b_light": vest("#9bb27a", "#56693e"),
    "gear_b_plate": vest("#d8bf8a", "#97793e", pouches("#b39a64")),
    "gear_b_commando": vest("#454c55", "#1f2328",
                            pouches("#2f353d", 3) +
                            '<circle cx="100" cy="146" r="13" fill="#6a8a3a" %s/><circle cx="156" cy="146" r="13" fill="#6a8a3a" %s/>' % (stroke(6), stroke(6))),
    "gear_b_heavy": vest("#6e7682", "#353b44", pouches("#4a515b", 2), shoulders=True),
}

# ----- Boots -----


def boot(light, dark, extra="", sole="#2a2a2a"):
    d = grad("b", light, dark)
    body = (SHADOW +
            '<path d="M86 30 L 150 30 L 152 132 C 186 138, 218 150, 222 182 L 222 196 L 52 196 L 54 120 C 64 100, 82 80, 86 30 Z" fill="url(#b)" %s/>' % stroke() +
            '<path d="M48 196 L 226 196 L 222 216 L 52 216 Z" fill="%s" %s/>' % (sole, stroke(8)) +
            '<path d="M98 52 L 140 52 M98 76 L 140 76 M98 100 L 140 100" stroke="%s" stroke-width="6" opacity="0.6"/>' % INK +
            '<path d="M96 40 L 96 120" stroke="#fff" stroke-width="7" opacity="0.3" stroke-linecap="round"/>')
    return svg(body + extra, d)


BOOTS = {
    "gear_l_field": boot("#a07a4a", "#5e4122"),
    "gear_l_runner": boot("#5aa0e8", "#215ea8",
                          '<path d="M60 168 C 110 150, 170 160, 212 178" fill="none" stroke="#fff" stroke-width="10" opacity="0.85"/>',
                          sole="#f0f0f0"),
    "gear_l_mountain": boot("#8a8f96", "#4a4f56",
                            '<path d="M60 216 l10 10 l10 -10 l10 10 l10 -10 l10 10 l10 -10 l10 10 l10 -10 l10 10 l10 -10 l10 10 l10 -10 l10 10 l10 -10 l10 10 l10 -10" fill="none" stroke="%s" stroke-width="6"/>' % INK),
    "gear_l_knee": boot("#3c434c", "#1a1d22",
                        '<rect x="74" y="20" width="84" height="56" rx="18" fill="#59616c" %s/>' % stroke(8) +
                        '<rect x="90" y="34" width="52" height="10" rx="5" fill="#fff" opacity="0.3"/>'),
}

# ----- Explosives -----

FRAG = svg(grad("g", "#86a85a", "#3f5a26") + SHADOW +
           '<ellipse cx="124" cy="146" rx="62" ry="72" fill="url(#g)" %s/>' % stroke() +
           '<path d="M70 120 L 178 120 M66 156 L 182 156 M78 190 L 170 190 M124 76 L 124 216" stroke="%s" stroke-width="6" opacity="0.55"/>' % INK +
           '<rect x="100" y="52" width="48" height="28" rx="6" fill="#9aa2aa" %s/>' % stroke(8) +
           '<path d="M146 60 C 196 44, 206 90, 176 104" fill="none" stroke="%s" stroke-width="10"/>' % INK +
           '<circle cx="186" cy="44" r="16" fill="none" stroke="#c7cdd4" stroke-width="8"/>' +
           '<path d="M88 110 C 92 96, 104 88, 116 86" stroke="#fff" stroke-width="9" opacity="0.4" fill="none" stroke-linecap="round"/>', "")

MOLOTOV = svg(grad("bt", "#7ad06a", "#2a7a3a") + grad("fl", "#fff27a", "#ff4a1a") + SHADOW +
              '<g transform="translate(14 26) scale(0.89)">'
              '<path d="M104 90 L 104 60 L 152 60 L 152 90 C 186 110, 190 140, 186 200 C 184 216, 72 216, 70 200 C 66 140, 70 110, 104 90 Z" fill="url(#bt)" opacity="0.95" %s/>' % stroke() +
              '<rect x="98" y="44" width="60" height="22" rx="6" fill="#e8d8b0" %s/>' % stroke(8) +
              '<path d="M116 46 C 96 20, 140 4, 128 -6 C 160 10, 170 34, 140 46 Z" fill="url(#fl)" %s/>' % stroke(7) +
              '<path d="M82 150 C 110 162, 150 162, 176 150 L 176 196 C 150 206, 106 206, 80 196 Z" fill="#ffa53a" opacity="0.75"/>' +
              '<path d="M88 112 C 92 100, 100 96, 108 94" stroke="#fff" stroke-width="9" opacity="0.45" fill="none" stroke-linecap="round"/></g>', "")

CHARGE = svg(grad("c", "#d0b07a", "#8a6a34") + SHADOW +
             '<rect x="44" y="86" width="168" height="124" rx="12" fill="url(#c)" %s/>' % stroke() +
             '<rect x="44" y="118" width="168" height="18" fill="#2a2a2a" opacity="0.8"/>'
             '<rect x="44" y="164" width="168" height="18" fill="#2a2a2a" opacity="0.8"/>'
             '<rect x="96" y="104" width="64" height="44" rx="6" fill="#1c2226" %s/>' % stroke(7) +
             '<text x="128" y="136" font-family="monospace" font-weight="bold" font-size="26" text-anchor="middle" fill="#ff4a3a">0:05</text>' +
             '<path d="M100 104 C 80 60, 60 70, 52 40 M156 104 C 176 60, 200 70, 206 40" fill="none" stroke="#e03a2a" stroke-width="8"/>' +
             '<path d="M58 98 L 110 98" stroke="#fff" stroke-width="7" opacity="0.35" stroke-linecap="round"/>', "")

# ----- Tactical -----


def canister(light, dark, band, puff="", star=""):
    d = grad("cn", light, dark)
    return svg(d + SHADOW + puff +
               '<rect x="88" y="78" width="80" height="140" rx="14" fill="url(#cn)" %s/>' % stroke() +
               '<rect x="88" y="130" width="80" height="30" fill="%s" %s/>' % (band, stroke(7)) +
               '<rect x="104" y="58" width="48" height="24" rx="6" fill="#9aa2aa" %s/>' % stroke(8) +
               '<path d="M150 64 C 190 52, 196 90, 172 100" fill="none" stroke="%s" stroke-width="9"/>' % INK +
               '<path d="M102 96 L 102 120" stroke="#fff" stroke-width="8" opacity="0.4" stroke-linecap="round"/>' + star, "")


SMOKE = canister("#9aa0a8", "#4e545c", "#d8dce0",
                 puff='<circle cx="70" cy="70" r="34" fill="#c7cdd4" opacity="0.9"/><circle cx="186" cy="60" r="40" fill="#dfe3e8" opacity="0.9"/>'
                      '<circle cx="128" cy="40" r="30" fill="#eef1f4" opacity="0.9"/><circle cx="210" cy="120" r="26" fill="#c7cdd4" opacity="0.85"/>')
FLASH = canister("#f4f6f8", "#9aa4ae", "#ffd34a",
                 star='<path d="M200 40 l10 26 l26 10 l-26 10 l-10 26 l-10 -26 l-26 -10 l26 -10 z" fill="#fff6a0" %s/>' % stroke(6) +
                      '<path d="M56 48 l7 18 l18 7 l-18 7 l-7 18 l-7 -18 l-18 -7 l18 -7 z" fill="#fff6a0" %s/>' % stroke(5))
GAS = canister("#8ab84a", "#3f6a1e", "#2a3a1a",
               puff='<circle cx="66" cy="84" r="34" fill="#a8e05a" opacity="0.75"/><circle cx="190" cy="70" r="38" fill="#9ad04a" opacity="0.75"/>'
                    '<circle cx="128" cy="44" r="28" fill="#c0f070" opacity="0.7"/>')

# ----- Medical -----

MEDKIT = svg(grad("mk", "#ff6a5a", "#b8202a") + SHADOW +
             '<rect x="40" y="82" width="176" height="132" rx="18" fill="url(#mk)" %s/>' % stroke() +
             '<path d="M96 82 L 96 60 C 96 50, 160 50, 160 60 L 160 82" fill="none" %s/>' % stroke(12) +
             '<path d="M128 116 L 128 182 M95 149 L 161 149" stroke="#fff" stroke-width="22" stroke-linecap="round"/>' +
             '<path d="M58 100 L 112 100" stroke="#fff" stroke-width="8" opacity="0.35" stroke-linecap="round"/>', "")
SYRINGE = svg(grad("sy", "#e8f6ff", "#9ac4e0") + grad("lq", "#ffd34a", "#ff7a1a") + SHADOW +
              '<g transform="rotate(-40 128 128)">'
              '<rect x="52" y="104" width="128" height="48" rx="10" fill="url(#sy)" %s/>' % stroke() +
              '<rect x="60" y="112" width="84" height="32" rx="6" fill="url(#lq)"/>' +
              '<rect x="180" y="116" width="26" height="24" fill="#c7cdd4" %s/>' % stroke(7) +
              '<path d="M206 128 L 246 128" stroke="%s" stroke-width="7"/>' % INK +
              '<rect x="28" y="98" width="24" height="60" rx="5" fill="#c7cdd4" %s/>' % stroke(8) +
              '<path d="M90 120 L 90 136 M110 120 L 110 136 M130 120 L 130 136" stroke="%s" stroke-width="5" opacity="0.6"/></g>' % INK, "")
PACK = svg(grad("pk", "#f4f6f8", "#aab4be") + SHADOW +
           '<rect x="48" y="62" width="160" height="152" rx="16" fill="url(#pk)" %s/>' % stroke() +
           '<rect x="48" y="62" width="160" height="34" rx="12" fill="#3aa0e8" %s/>' % stroke(8) +
           '<path d="M128 116 L 128 196 M88 156 L 168 156" stroke="#e8303a" stroke-width="26" stroke-linecap="round"/>' +
           '<path d="M64 108 L 108 108" stroke="#fff" stroke-width="8" opacity="0.5" stroke-linecap="round"/>', "")

# ----- Perks: round badges with a symbol -----


def badge(inner, ring_l, ring_d, fill_l, fill_d):
    defs = grad("ring", ring_l, ring_d) + grad("fill", fill_l, fill_d)
    body = ('<circle cx="128" cy="132" r="106" fill="#000" opacity="0.3"/>'
            '<circle cx="128" cy="126" r="104" fill="url(#ring)" stroke="%s" stroke-width="10"/>' % INK +
            '<circle cx="128" cy="126" r="82" fill="url(#fill)" stroke="%s" stroke-width="6"/>' % INK +
            inner +
            '<path d="M60 92 A 80 80 0 0 1 150 48" stroke="#fff" stroke-width="10" fill="none" opacity="0.35" stroke-linecap="round"/>')
    return svg(body, defs)


W = "#f4f6f8"
PERKS = {
    "gear_p_reload": badge('<rect x="104" y="70" width="44" height="96" rx="8" fill="%s" %s/>' % (W, stroke(8)) +
                           '<path d="M168 92 A 46 46 0 0 1 168 162 M88 162 A 46 46 0 0 1 88 92" fill="none" stroke="#ffd34a" stroke-width="10"/>' +
                           '<path d="M160 156 l12 10 l4 -16 M96 98 l-12 -10 l-4 16" fill="none" stroke="#ffd34a" stroke-width="8"/>',
                           "#c7cdd4", "#6a737c", "#3a7ad0", "#1a3a70"),
    "gear_p_agile": badge('<path d="M84 162 L 112 98 L 150 118 L 176 92" fill="none" stroke="%s" stroke-width="16" stroke-linecap="round" stroke-linejoin="round"/>' % W +
                          '<path d="M70 120 L 100 120 M60 140 L 92 140 M74 160 L 96 160" stroke="#9ad8ff" stroke-width="8" stroke-linecap="round"/>',
                          "#c7cdd4", "#6a737c", "#2a9a7a", "#145a46"),
    "gear_p_sharp": badge('<circle cx="128" cy="126" r="44" fill="none" stroke="%s" stroke-width="10"/>' % W +
                          '<path d="M128 64 L 128 98 M128 154 L 128 188 M66 126 L 100 126 M156 126 L 190 126" stroke="%s" stroke-width="10" stroke-linecap="round"/>' % W +
                          '<circle cx="128" cy="126" r="10" fill="#ff4a3a"/>', "#ffd34a", "#b0780e", "#c03030", "#601414"),
    "gear_p_armor": badge('<path d="M128 64 L 180 84 C 180 140, 160 172, 128 190 C 96 172, 76 140, 76 84 Z" fill="%s" %s/>' % (W, stroke(8)) +
                          '<path d="M128 100 L 128 156 M100 128 L 156 128" stroke="#3a7ad0" stroke-width="14" stroke-linecap="round"/>',
                          "#c7cdd4", "#6a737c", "#3a4a5a", "#1a222a"),
    "gear_p_regen": badge('<path d="M128 184 C 60 140, 70 76, 112 82 C 120 84, 126 90, 128 98 C 130 90, 136 84, 144 82 C 186 76, 196 140, 128 184 Z" fill="#ff5a6a" %s/>' % stroke(8) +
                          '<path d="M128 110 L 128 150 M108 130 L 148 130" stroke="%s" stroke-width="12" stroke-linecap="round"/>' % W,
                          "#c7cdd4", "#6a737c", "#2a9a4a", "#145a24"),
    "gear_p_bomber": badge('<circle cx="122" cy="138" r="46" fill="#2a2f36" %s/>' % stroke(8) +
                           '<path d="M150 104 L 166 88" stroke="%s" stroke-width="12"/>' % INK +
                           '<path d="M166 88 C 176 70, 192 76, 186 60" fill="none" stroke="#ffb03a" stroke-width="8"/>' +
                           '<circle cx="104" cy="122" r="10" fill="#fff" opacity="0.5"/>', "#d0a0ff", "#6a2ab0", "#e8603a", "#802a14"),
    "gear_p_medic": badge('<path d="M128 70 L 128 182 M72 126 L 184 126" stroke="%s" stroke-width="34" stroke-linecap="round"/>' % W +
                          '<path d="M128 80 L 128 172 M82 126 L 174 126" stroke="#e8303a" stroke-width="18" stroke-linecap="round"/>',
                          "#d0a0ff", "#6a2ab0", "#2a7ad0", "#143a70"),
    "gear_p_back": badge('<path d="M76 150 C 76 90, 180 90, 180 150 Z" fill="#7aa04a" %s/>' % stroke(8) +
                         '<path d="M100 150 L 108 112 L 148 112 L 156 150 M128 100 L 128 150" fill="none" stroke="%s" stroke-width="6"/>' % INK +
                         '<rect x="68" y="146" width="120" height="16" rx="8" fill="#d8c08a" %s/>' % stroke(6),
                         "#d0a0ff", "#6a2ab0", "#3a6a2a", "#1a3a14"),
    "gear_p_lastbreath": badge('<path d="M128 186 C 60 142, 70 78, 112 84 C 122 86, 126 92, 128 100 C 130 92, 136 86, 144 84 C 186 78, 196 142, 128 186 Z" fill="#c02030" %s/>' % stroke(8) +
                               '<path d="M128 100 L 116 130 L 136 138 L 122 172" fill="none" stroke="%s" stroke-width="7"/>' % INK +
                               '<path d="M150 78 C 160 56, 176 64, 170 44 C 190 60, 186 84, 168 92" fill="#ffb03a" %s/>' % stroke(5),
                               "#ffd34a", "#b0780e", "#ff6a1a", "#7a1a0a"),
    "gear_p_hunter": badge('<path d="M88 76 C 100 110, 100 150, 84 182 M124 70 C 138 110, 138 150, 122 186 M160 76 C 172 110, 172 150, 156 182" fill="none" stroke="%s" stroke-width="13" stroke-linecap="round"/>' % W +
                           '<path d="M88 76 C 100 110, 100 150, 84 182 M124 70 C 138 110, 138 150, 122 186 M160 76 C 172 110, 172 150, 156 182" fill="none" stroke="#ff4a3a" stroke-width="5" stroke-linecap="round"/>',
                           "#ffd34a", "#b0780e", "#3a2a2a", "#140a0a"),
}

# ----- Categories and lobby icons -----

BACKPACK = svg(grad("bp", "#8a9a62", "#4e5a34") + SHADOW +
               '<path d="M96 54 C 96 26, 160 26, 160 54" fill="none" %s/>' % stroke(14) +
               '<rect x="56" y="50" width="144" height="168" rx="30" fill="url(#bp)" %s/>' % stroke() +
               '<rect x="78" y="130" width="100" height="70" rx="14" fill="#6a7a48" %s/>' % stroke(8) +
               '<path d="M78 150 L 178 150" stroke="%s" stroke-width="6"/>' % INK +
               '<path d="M76 74 L 110 74" stroke="#fff" stroke-width="8" opacity="0.35" stroke-linecap="round"/>', "")
PROFILE = badge('<circle cx="128" cy="104" r="30" fill="%s" %s/>' % (W, stroke(8)) +
                '<path d="M78 182 C 80 148, 104 136, 128 136 C 152 136, 176 148, 178 182 Z" fill="%s" %s/>' % (W, stroke(8)),
                "#ffd34a", "#b0780e", "#3a7ad0", "#1a3a70")
TROPHY = svg(grad("tr", "#ffe680", "#d08a10") + SHADOW +
             '<path d="M72 42 L 184 42 L 178 118 C 174 150, 150 166, 128 166 C 106 166, 82 150, 78 118 Z" fill="url(#tr)" %s/>' % stroke() +
             '<path d="M74 60 C 30 60, 34 118, 82 120 M182 60 C 226 60, 222 118, 174 120" fill="none" %s/>' % stroke(10) +
             '<rect x="112" y="164" width="32" height="26" fill="url(#tr)" %s/>' % stroke(8) +
             '<rect x="78" y="188" width="100" height="28" rx="6" fill="#8a5a1a" %s/>' % stroke(8) +
             '<path d="M100 60 L 100 110" stroke="#fff" stroke-width="10" opacity="0.4" stroke-linecap="round"/>', "")
WHEEL_SEGS = ""
import math
cols = ["#ff5a4a", "#ffd34a", "#4ad06a", "#3aa0e8", "#b04cff", "#ff8a2a", "#2ad0c0", "#ff4ad6"]
for i in range(8):
    a0 = math.radians(i * 45 - 90)
    a1 = math.radians((i + 1) * 45 - 90)
    x0, y0 = 128 + 92 * math.cos(a0), 132 + 92 * math.sin(a0)
    x1, y1 = 128 + 92 * math.cos(a1), 132 + 92 * math.sin(a1)
    WHEEL_SEGS += '<path d="M128 132 L %.1f %.1f A 92 92 0 0 1 %.1f %.1f Z" fill="%s" stroke="%s" stroke-width="5"/>' % (x0, y0, x1, y1, cols[i], INK)
WHEEL = svg(SHADOW + '<circle cx="128" cy="132" r="102" fill="#2a2f36" %s/>' % stroke() + WHEEL_SEGS +
            '<circle cx="128" cy="132" r="20" fill="#f4f6f8" %s/>' % stroke(7) +
            '<path d="M128 20 L 146 52 L 110 52 Z" fill="#f4f6f8" %s/>' % stroke(7), "")
MODES = svg(grad("sw", "#e8eef4", "#8a96a4") + SHADOW +
            '<g transform="rotate(45 128 128)"><rect x="118" y="24" width="20" height="150" rx="6" fill="url(#sw)" %s/>' % stroke(8) +
            '<rect x="92" y="170" width="72" height="16" rx="6" fill="#d03a2a" %s/>' % stroke(7) +
            '<rect x="118" y="186" width="20" height="40" rx="6" fill="#6a4a2a" %s/></g>' % stroke(7) +
            '<g transform="rotate(-45 128 128)"><rect x="118" y="24" width="20" height="150" rx="6" fill="url(#sw)" %s/>' % stroke(8) +
            '<rect x="92" y="170" width="72" height="16" rx="6" fill="#3a6ad0" %s/>' % stroke(7) +
            '<rect x="118" y="186" width="20" height="40" rx="6" fill="#6a4a2a" %s/></g>' % stroke(7), "")
POWER = badge('<path d="M140 58 L 92 136 L 124 136 L 112 194 L 166 108 L 132 108 Z" fill="#ffd34a" %s/>' % stroke(8),
              "#ff9a5a", "#b0400e", "#7a2ad0", "#2a0a60")
CALENDAR = svg(grad("cal", "#f4f6f8", "#aab4be") + SHADOW +
               '<rect x="44" y="52" width="168" height="164" rx="18" fill="url(#cal)" %s/>' % stroke() +
               '<rect x="44" y="52" width="168" height="44" rx="14" fill="#e8303a" %s/>' % stroke(8) +
               '<rect x="80" y="34" width="16" height="36" rx="6" fill="#9aa4ae" %s/><rect x="160" y="34" width="16" height="36" rx="6" fill="#9aa4ae" %s/>' % (stroke(6), stroke(6)) +
               '<text x="128" y="186" font-family="sans-serif" font-weight="bold" font-size="76" text-anchor="middle" fill="%s">7</text>' % INK, "")
FREE = svg(grad("fr", "#7ee0a0", "#24a050") + SHADOW +
           '<path d="M128 26 l26 54 l60 8 l-44 42 l10 60 l-52 -28 l-52 28 l10 -60 l-44 -42 l60 -8 z" fill="url(#fr)" %s/>' % stroke() +
           '<path d="M104 92 L 124 82" stroke="#fff" stroke-width="9" opacity="0.5" stroke-linecap="round"/>', "")

STAR_PATH = "M128 18 L 160 92 L 240 98 L 178 150 L 198 230 L 128 186 L 58 230 L 78 150 L 16 98 L 96 92 Z"
STAR = svg(grad("st", "#fff07a", "#f0a010") + '<path d="%s" fill="url(#st)" %s/>' % (STAR_PATH, stroke(12)) +
           '<path d="M104 104 L 124 62" stroke="#fff" stroke-width="12" opacity="0.6" stroke-linecap="round"/>', "")
STAR_EMPTY = svg('<path d="%s" fill="#2a2f38" opacity="0.85" %s/>' % (STAR_PATH, stroke(12)), "")
UP = svg(grad("up", "#7ef09a", "#24a050") + '<circle cx="128" cy="128" r="108" fill="url(#up)" %s/>' % stroke(12) +
         '<path d="M128 58 L 190 132 L 152 132 L 152 196 L 104 196 L 104 132 L 66 132 Z" fill="#fff" %s/>' % stroke(8), "")

ICONS = {}
ICONS.update(HELMETS)
ICONS.update(VESTS)
ICONS.update(BOOTS)
ICONS.update({"gear_x_frag": FRAG, "gear_x_molotov": MOLOTOV, "gear_x_charge": CHARGE,
              "gear_t_smoke": SMOKE, "gear_t_flash": FLASH, "gear_t_gas": GAS,
              "gear_m_kit": MEDKIT, "gear_m_adrenaline": SYRINGE, "gear_m_pack": PACK})
ICONS.update(PERKS)
ICONS.update({"inv_head": HELMETS["gear_h_tactical"], "inv_body": VESTS["gear_b_plate"], "inv_legs": BOOTS["gear_l_field"],
              "inv_explosive": FRAG, "inv_tactical": SMOKE, "inv_medical": MEDKIT, "inv_perk": PERKS["gear_p_armor"],
              "inventory": BACKPACK, "profile": PROFILE, "trophy": TROPHY, "wheel": WHEEL, "modes": MODES, "power": POWER,
              "calendar": CALENDAR, "free": FREE, "star": STAR, "star_empty": STAR_EMPTY, "up": UP})

if __name__ == "__main__":
    for name, data in ICONS.items():
        path = os.path.join(OUT, name + ".png")
        cairosvg.svg2png(bytestring=data.encode(), write_to=path, output_width=256, output_height=256)
        print("icon", name, os.path.getsize(path))
