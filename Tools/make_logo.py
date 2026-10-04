"""Draws the game logo: "ZOOTOPIA" with the first O as a reticle around a paw print, a "MOBILE" banner.

  Assets/Resources/UI/Logo.png   1024 x 512, transparent (loading and title screens)

Fonts: Russo One and Teko (SIL Open Font License, see Tools/fonts/*-OFL.txt).
Run:  python3 Tools/make_logo.py
"""
import math, os
import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageFont

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, "..", "Assets", "Resources", "UI", "Logo.png")
W, H = 2048, 1024          # drawn at 2x, scaled down at the end
word = ImageFont.truetype(os.path.join(HERE, "fonts", "RussoOne.woff2"), 300)
sub = ImageFont.truetype(os.path.join(HERE, "fonts", "Teko.woff2"), 150)

GOLD_TOP = np.array([255, 236, 140], np.float32)
GOLD_MID = np.array([255, 196, 52], np.float32)
GOLD_BOT = np.array([232, 112, 18], np.float32)
INK = (16, 18, 24)


def text_mask(s, font):
    l, t, r, b = font.getbbox(s)
    m = Image.new("L", (r - l + 4, b - t + 4), 0)
    ImageDraw.Draw(m).text((2 - l, 2 - t), s, font=font, fill=255)
    return m


def emblem_mask(d):
    """Reticle ring with four ticks and a paw print in the middle, d pixels across."""
    s = 4  # supersample
    D = d * s
    m = Image.new("L", (D, D), 0)
    g = ImageDraw.Draw(m)
    c = D / 2
    ring_w = D * 0.085
    r_out = D * 0.40
    g.ellipse([c - r_out, c - r_out, c + r_out, c + r_out], fill=255)
    r_in = r_out - ring_w
    g.ellipse([c - r_in, c - r_in, c + r_in, c + r_in], fill=0)
    tick_w = D * 0.06
    for a in range(4):
        ang = a * math.pi / 2
        x0, y0 = c + math.cos(ang) * (r_in - D * 0.02), c + math.sin(ang) * (r_in - D * 0.02)
        x1, y1 = c + math.cos(ang) * (D * 0.5), c + math.sin(ang) * (D * 0.5)
        g.line([(x0, y0), (x1, y1)], fill=255, width=int(tick_w))
    # paw: main pad + four toes
    pad_w, pad_h = D * 0.34, D * 0.26
    py = c + D * 0.095
    g.ellipse([c - pad_w / 2, py - pad_h / 2, c + pad_w / 2, py + pad_h / 2], fill=255)
    g.polygon([(c - pad_w * 0.42, py - pad_h * 0.05), (c, py - pad_h * 0.75), (c + pad_w * 0.42, py - pad_h * 0.05)], fill=255)
    toes = [(-0.158, -0.055, 0.082, 0.104), (-0.06, -0.158, 0.088, 0.112), (0.06, -0.158, 0.088, 0.112), (0.158, -0.055, 0.082, 0.104)]
    for dx, dy, rx, ry in toes:
        x, y = c + dx * D, c + dy * D
        g.ellipse([x - rx * D / 2 * 1.0, y - ry * D / 2, x + rx * D / 2, y + ry * D / 2], fill=255)
    return m.resize((d, d), Image.LANCZOS)


def gradient(mask):
    """Gold gradient over the mask's box (light top → gold → orange bottom), a thin bevel line."""
    w, h = mask.size
    y = np.linspace(0, 1, h)[:, None, None]
    upper = GOLD_TOP + (GOLD_MID - GOLD_TOP) * np.clip(y / 0.5, 0, 1)
    lower = GOLD_MID + (GOLD_BOT - GOLD_MID) * np.clip((y - 0.5) / 0.5, 0, 1)
    col = np.broadcast_to(np.where(y < 0.5, upper, lower), (h, w, 3)).copy()
    col += np.exp(-((y - 0.47) / 0.012) ** 2) * 40
    img = np.dstack([np.clip(col, 0, 255), np.asarray(mask, np.float32)]).astype(np.uint8)
    return Image.fromarray(img, "RGBA")


def compose():
    canvas_mask = Image.new("L", (W, H), 0)
    # ---- wordmark: Z [emblem] OTOPIA
    z = text_mask("Z", word)
    rest = text_mask("OTOPIA", word)
    cap = z.size[1]
    em_d = int(cap * 1.18)
    em = emblem_mask(em_d)
    gap = 14
    total = z.size[0] + gap + em_d + gap + rest.size[0]
    x = (W - total) // 2
    y_text = 250
    canvas_mask.paste(z, (x, y_text), z)
    ex = x + z.size[0] + gap
    canvas_mask.paste(em, (ex, y_text + cap // 2 - em_d // 2), em)
    canvas_mask.paste(rest, (ex + em_d + gap, y_text), rest)
    word_box = (x, y_text, x + total, y_text + cap)

    # ---- outline, shadow, fill
    outline = canvas_mask.filter(ImageFilter.MaxFilter(23))
    shadow = outline.filter(ImageFilter.GaussianBlur(18))
    img = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    sh = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    sh.putalpha(shadow.point(lambda v: int(v * 0.75)))
    img.alpha_composite(sh, (0, 16))
    ol = Image.new("RGBA", (W, H), INK + (0,))
    ol.putalpha(outline)
    img.alpha_composite(ol)
    # thin gold rim inside the dark outline
    rim = canvas_mask.filter(ImageFilter.MaxFilter(7))
    rim_l = Image.new("RGBA", (W, H), (120, 64, 10, 0))
    rim_l.putalpha(rim)
    img.alpha_composite(rim_l)
    x0, y0, x1, y1 = word_box
    crop = canvas_mask.crop((x0 - 40, y0 - 60, x1 + 40, y1 + 60))
    fill = gradient(crop)
    img.alpha_composite(fill, (x0 - 40, y0 - 60))

    # ---- banner with MOBILE
    bw, bh = 760, 128
    bx, by = (W - bw) // 2, y1 + 70
    banner = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    bd = ImageDraw.Draw(banner)
    skew = 34
    poly = [(bx + skew, by), (bx + bw + skew, by), (bx + bw - skew, by + bh), (bx - skew, by + bh)]
    bd.polygon([(px_, py_ + 10) for px_, py_ in poly], fill=(0, 0, 0, 120))
    bd.polygon(poly, fill=INK + (235,))
    bd.line(poly + [poly[0]], fill=(255, 196, 52, 255), width=6)
    img.alpha_composite(banner.filter(ImageFilter.GaussianBlur(0.6)))
    t = text_mask("M O B I L E", sub)
    tx = (W - t.size[0]) // 2
    ty = by + (bh - t.size[1]) // 2 + 4
    tl = Image.new("RGBA", t.size, (255, 255, 255, 0))
    tl.putalpha(t)
    img.alpha_composite(tl, (tx, ty))
    # side strokes
    d = ImageDraw.Draw(img)
    for sgn in (-1, 1):
        cx = W // 2 + sgn * (bw // 2 + 150)
        d.line([(cx - 90, by + bh // 2), (cx + 90, by + bh // 2)], fill=(255, 196, 52, 255), width=8)
    return img


img = compose()
bbox = img.getbbox()
img = img.crop((0, max(0, bbox[1] - 30), W, min(H, bbox[3] + 30)))
canvas = Image.new("RGBA", (W, H), (0, 0, 0, 0))
canvas.alpha_composite(img, (0, (H - img.size[1]) // 2))
canvas.resize((W // 2, H // 2), Image.LANCZOS).save(OUT, optimize=True)
print("logo", OUT, os.path.getsize(OUT))
