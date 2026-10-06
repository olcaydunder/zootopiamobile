"""Draws the app icon and the Play Store graphics for Rise of Davraz (own design, no third-party art):
a snow-capped gold summit (Davraz) inside a gold reticle, over a dusk sky with dark mountain ridges.

  Assets/Icon/AppIcon.png          1024 x 1024  legacy launcher icon (all sizes are made from it)
  Assets/Icon/AdaptiveBack.png     1024 x 1024  adaptive icon background (sky and ridges)
  Assets/Icon/AdaptiveFore.png     1024 x 1024  adaptive icon foreground (emblem inside the 66% safe zone)
  Tools/store/icon_512.png         512 x 512    Play Store listing icon
  Tools/store/feature_1024x500.png 1024 x 500   Play Store feature graphic (sky, ridges, the logo)

Run after Tools/make_logo.py:  python3 Tools/make_icon.py
"""
import math, os
import numpy as np
from PIL import Image, ImageDraw, ImageFilter

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.join(HERE, "..")
S = 1024
GOLD_TOP = np.array([255, 236, 140], np.float32)
GOLD_MID = np.array([255, 196, 52], np.float32)
GOLD_BOT = np.array([232, 112, 18], np.float32)
INK = (16, 18, 24)


def sky(w, h):
    y = np.linspace(0, 1, h)[:, None]
    top = np.array([18, 24, 44], np.float32)
    mid = np.array([92, 52, 78], np.float32)
    low = np.array([238, 128, 52], np.float32)
    col = np.where(y < 0.55, top + (mid - top) * (y / 0.55), mid + (low - mid) * ((y - 0.55) / 0.45))
    img = np.broadcast_to(col[:, None, :], (h, w, 3)).astype(np.uint8)
    return Image.fromarray(img, "RGB").convert("RGBA")


def ridges(img, seed, layers):
    """Dark mountain ridges along the bottom (farther = lighter and higher)."""
    w, h = img.size
    rng = np.random.default_rng(seed)
    for i, (base, amp, col) in enumerate(layers):
        pts = [(0, h)]
        n = 9
        xs = np.linspace(-0.05, 1.05, n)
        ys = base + rng.uniform(-amp, amp, n)
        fine = np.linspace(-0.05, 1.05, 160)
        yy = np.interp(fine, xs, ys) + 0.012 * np.sin(fine * 40 + i)
        for fx, fy in zip(fine, yy):
            pts.append((fx * w, fy * h))
        pts.append((w, h))
        layer = Image.new("RGBA", (w, h), (0, 0, 0, 0))
        ImageDraw.Draw(layer).polygon(pts, fill=col)
        img.alpha_composite(layer)
    return img


def gold(mask):
    w, h = mask.size
    y = np.linspace(0, 1, h)[:, None, None]
    upper = GOLD_TOP + (GOLD_MID - GOLD_TOP) * np.clip(y / 0.5, 0, 1)
    lower = GOLD_MID + (GOLD_BOT - GOLD_MID) * np.clip((y - 0.5) / 0.5, 0, 1)
    col = np.broadcast_to(np.where(y < 0.5, upper, lower), (h, w, 3)).copy()
    return Image.fromarray(np.dstack([np.clip(col, 0, 255), np.asarray(mask, np.float32)]).astype(np.uint8), "RGBA")


def dilate(mask, r):
    """Grows a mask by about r pixels (blur and threshold: fast for big radii)."""
    return mask.filter(ImageFilter.GaussianBlur(r)).point(lambda v: 255 if v > 18 else int(v * 255 / 18))


def emblem(d):
    """Reticle ring with ticks around a snow-capped summit, d x d, transparent."""
    s = 2
    D = d * s
    c = D / 2
    ring = Image.new("L", (D, D), 0)
    g = ImageDraw.Draw(ring)
    r_out, rw = D * 0.47, D * 0.055
    g.ellipse([c - r_out, c - r_out, c + r_out, c + r_out], fill=255)
    g.ellipse([c - r_out + rw, c - r_out + rw, c + r_out - rw, c + r_out - rw], fill=0)
    for a in range(4):
        ang = a * math.pi / 2 + math.pi / 2
        x0, y0 = c + math.cos(ang) * (r_out - D * 0.13), c + math.sin(ang) * (r_out - D * 0.13)
        x1, y1 = c + math.cos(ang) * (D * 0.5), c + math.sin(ang) * (D * 0.5)
        g.line([(x0, y0), (x1, y1)], fill=255, width=int(D * 0.05))
    peak = Image.new("L", (D, D), 0)
    p = ImageDraw.Draw(peak)
    base_y = c + D * 0.27
    p.polygon([(c - D * 0.33, base_y), (c - D * 0.02, c - D * 0.27), (c + D * 0.30, base_y)], fill=255)
    p.polygon([(c + D * 0.02, base_y), (c + D * 0.20, c - D * 0.06), (c + D * 0.37, base_y)], fill=255)
    snow = Image.new("L", (D, D), 0)
    sn = ImageDraw.Draw(snow)
    ty = c - D * 0.27
    sy = ty + D * 0.17
    sn.polygon([(c - D * 0.02, ty), (c + D * 0.09, sy), (c + D * 0.035, sy - D * 0.025), (c - D * 0.01, sy + D * 0.015),
                (c - D * 0.06, sy - D * 0.03), (c - D * 0.10, sy + D * 0.005), (c - D * 0.13, sy)], fill=255)
    out = Image.new("RGBA", (D, D), (0, 0, 0, 0))
    both = Image.fromarray(np.maximum(np.asarray(ring), np.asarray(peak)))
    ol = Image.new("RGBA", (D, D), INK + (0,))
    grown = dilate(both, D * 0.014)
    ol.putalpha(grown)
    sh = Image.new("RGBA", (D, D), (0, 0, 0, 0))
    sh.putalpha(grown.filter(ImageFilter.GaussianBlur(D * 0.02)).point(lambda v: int(v * 0.6)))
    out.alpha_composite(sh, (0, int(D * 0.015)))
    out.alpha_composite(ol)
    out.alpha_composite(gold(both))
    sl = Image.new("RGBA", (D, D), (246, 249, 255, 0))
    sl.putalpha(Image.fromarray(np.minimum(np.asarray(snow), np.asarray(peak))))
    out.alpha_composite(sl)
    return out.resize((d, d), Image.LANCZOS)


def background(w, h):
    img = sky(w, h)
    glow = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    ImageDraw.Draw(glow).ellipse([w * 0.2, h * 0.45, w * 0.8, h * 1.05], fill=(255, 170, 70, 110))
    img.alpha_composite(glow.filter(ImageFilter.GaussianBlur(w * 0.08)))
    return ridges(img, 7, [(0.80, 0.05, (58, 40, 62, 255)), (0.86, 0.04, (34, 26, 40, 255)), (0.93, 0.03, (14, 12, 20, 255))])


back = background(S, S)
back.convert("RGB").save(os.path.join(ROOT, "Assets", "Icon", "AdaptiveBack.png"))
fore = Image.new("RGBA", (S, S), (0, 0, 0, 0))
em = emblem(int(S * 0.62))
fore.alpha_composite(em, ((S - em.size[0]) // 2, (S - em.size[1]) // 2))
fore.save(os.path.join(ROOT, "Assets", "Icon", "AdaptiveFore.png"))
legacy = back.copy()
em2 = emblem(int(S * 0.80))
legacy.alpha_composite(em2, ((S - em2.size[0]) // 2, (S - em2.size[1]) // 2 - int(S * 0.01)))
legacy.convert("RGB").save(os.path.join(ROOT, "Assets", "Icon", "AppIcon.png"))
os.makedirs(os.path.join(HERE, "store"), exist_ok=True)
legacy.resize((512, 512), Image.LANCZOS).convert("RGB").save(os.path.join(HERE, "store", "icon_512.png"))

fw, fh = 1024, 500
feat = background(fw, fh)
logo = Image.open(os.path.join(ROOT, "Assets", "Resources", "UI", "Logo.png")).convert("RGBA")
bbox = logo.getbbox()
logo = logo.crop(bbox)
scale = min(fw * 0.78 / logo.size[0], fh * 0.62 / logo.size[1])
logo = logo.resize((int(logo.size[0] * scale), int(logo.size[1] * scale)), Image.LANCZOS)
feat.alpha_composite(logo, ((fw - logo.size[0]) // 2, int(fh * 0.12)))
feat.convert("RGB").save(os.path.join(HERE, "store", "feature_1024x500.png"))
print("icons and store graphics written")
