"""Draws the game logo: "DAVRAZ" with the first A as a snow-capped mountain peak (Davraz) inside a reticle, and a
"RISE OF" banner above it.

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


def peak_mask(w, h):
    """The first A of DAVRAZ: a mountain with a second, lower summit behind it (no crossbar), w x h pixels."""
    s = 4  # supersample
    W_, H_ = w * s, h * s
    m = Image.new("L", (W_, H_), 0)
    g = ImageDraw.Draw(m)
    g.polygon([(0, H_), (W_ * 0.5, 0), (W_, H_)], fill=255)                          # main summit
    g.polygon([(W_ * 0.56, H_), (W_ * 0.80, H_ * 0.38), (W_ * 1.04, H_)], fill=255)  # shoulder
    # a notch where the A's crossbar would be, so it still reads as a letter
    g.polygon([(W_ * 0.36, H_), (W_ * 0.5, H_ * 0.62), (W_ * 0.64, H_)], fill=0)
    return m.resize((w, h), Image.LANCZOS)


def snow_mask(w, h):
    """The snow cap on the peak (white, drawn over the gold)."""
    s = 4
    W_, H_ = w * s, h * s
    m = Image.new("L", (W_, H_), 0)
    g = ImageDraw.Draw(m)
    y = H_ * 0.34
    pts = [(W_ * 0.5, 0), (W_ * 0.5 + W_ * 0.17, y), (W_ * 0.58, y * 0.86), (W_ * 0.52, y * 1.06),
           (W_ * 0.45, y * 0.84), (W_ * 0.39, y * 0.98), (W_ * 0.5 - W_ * 0.17, y)]
    g.polygon(pts, fill=255)
    return m.resize((w, h), Image.LANCZOS)


def reticle_mask(d):
    """A thin reticle ring with four ticks, d pixels across."""
    s = 4
    D = d * s
    m = Image.new("L", (D, D), 0)
    g = ImageDraw.Draw(m)
    c = D / 2
    r_out, ring = D * 0.46, D * 0.035
    g.ellipse([c - r_out, c - r_out, c + r_out, c + r_out], fill=255)
    g.ellipse([c - r_out + ring, c - r_out + ring, c + r_out - ring, c + r_out - ring], fill=0)
    for a in range(4):
        ang = a * math.pi / 2
        x0, y0 = c + math.cos(ang) * (r_out - D * 0.09), c + math.sin(ang) * (r_out - D * 0.09)
        x1, y1 = c + math.cos(ang) * (D * 0.5), c + math.sin(ang) * (D * 0.5)
        g.line([(x0, y0), (x1, y1)], fill=255, width=int(D * 0.035))
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
    # ---- wordmark: D [peak] VRAZ
    d = text_mask("D", word)
    rest = text_mask("VRAZ", word)
    cap = d.size[1]
    pw, ph = int(cap * 1.02), cap
    peak = peak_mask(pw, ph)
    gap = 10
    total = d.size[0] + gap + pw + gap + rest.size[0]
    x = (W - total) // 2
    y_text = 420
    canvas_mask.paste(d, (x, y_text), d)
    px = x + d.size[0] + gap
    canvas_mask.paste(peak, (px, y_text), peak)
    canvas_mask.paste(rest, (px + pw + gap, y_text), rest)
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
    rim = canvas_mask.filter(ImageFilter.MaxFilter(7))
    rim_l = Image.new("RGBA", (W, H), (120, 64, 10, 0))
    rim_l.putalpha(rim)
    img.alpha_composite(rim_l)
    x0, y0, x1, y1 = word_box
    crop = canvas_mask.crop((x0 - 40, y0 - 60, x1 + 40, y1 + 60))
    fill = gradient(crop)
    img.alpha_composite(fill, (x0 - 40, y0 - 60))
    # snow on the summit
    snow = snow_mask(pw, ph)
    sl = Image.new("RGBA", (pw, ph), (244, 248, 255, 0))
    sl.putalpha(snow)
    img.alpha_composite(sl, (px, y_text))
    # a reticle round the summit
    rd = int(cap * 0.62)
    ret = reticle_mask(rd)
    rl = Image.new("RGBA", (rd, rd), INK + (0,))
    rl.putalpha(ret.filter(ImageFilter.MaxFilter(5)))
    img.alpha_composite(rl, (px + pw // 2 - rd // 2, y_text - rd // 2 + int(ph * 0.08)))
    rg = Image.new("RGBA", (rd, rd), (255, 236, 140, 0))
    rg.putalpha(ret)
    img.alpha_composite(rg, (px + pw // 2 - rd // 2, y_text - rd // 2 + int(ph * 0.08)))

    # ---- banner with RISE OF above the word
    bw, bh = 640, 120
    bx, by = (W - bw) // 2, y0 - bh - 120
    banner = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    bd = ImageDraw.Draw(banner)
    skew = 34
    poly = [(bx + skew, by), (bx + bw + skew, by), (bx + bw - skew, by + bh), (bx - skew, by + bh)]
    bd.polygon([(px_, py_ + 10) for px_, py_ in poly], fill=(0, 0, 0, 120))
    bd.polygon(poly, fill=INK + (235,))
    bd.line(poly + [poly[0]], fill=(255, 196, 52, 255), width=6)
    img.alpha_composite(banner.filter(ImageFilter.GaussianBlur(0.6)))
    t = text_mask("R I S E   O F", sub)
    tx = (W - t.size[0]) // 2
    ty = by + (bh - t.size[1]) // 2 + 4
    tl = Image.new("RGBA", t.size, (255, 255, 255, 0))
    tl.putalpha(t)
    img.alpha_composite(tl, (tx, ty))
    dr = ImageDraw.Draw(img)
    for sgn in (-1, 1):
        cx = W // 2 + sgn * (bw // 2 + 150)
        dr.line([(cx - 90, by + bh // 2), (cx + 90, by + bh // 2)], fill=(255, 196, 52, 255), width=8)
    return img


img = compose()
bbox = img.getbbox()
img = img.crop((0, max(0, bbox[1] - 30), W, min(H, bbox[3] + 30)))
canvas = Image.new("RGBA", (W, H), (0, 0, 0, 0))
canvas.alpha_composite(img, (0, (H - img.size[1]) // 2))
canvas.resize((W // 2, H // 2), Image.LANCZOS).save(OUT, optimize=True)
print("logo", OUT, os.path.getsize(OUT))
