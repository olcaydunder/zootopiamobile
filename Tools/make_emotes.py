"""Renders the lobby emotes (Noto Color Emoji, SIL Open Font License 1.1) to 128x128 PNGs in
Assets/Resources/UI/Emotes/<name>.png. Run: python3 Tools/make_emotes.py (needs the font installed)."""
import os
from PIL import Image, ImageDraw, ImageFont

FONT = "/usr/share/fonts/truetype/noto/NotoColorEmoji.ttf"
OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "Assets", "Resources", "UI", "Emotes")
EMOTES = [("laugh", "\U0001F602"), ("cool", "\U0001F60E"), ("angry", "\U0001F621"), ("cry", "\U0001F62D"),
          ("clown", "\U0001F921"), ("poop", "\U0001F4A9"), ("fire", "\U0001F525"), ("thumbs", "\U0001F44D"),
          ("wave", "\U0001F44B"), ("chicken", "\U0001F414"), ("party", "\U0001F389"), ("sleep", "\U0001F634")]
os.makedirs(OUT, exist_ok=True)
font = ImageFont.truetype(FONT, 109)
for name, ch in EMOTES:
    im = Image.new("RGBA", (160, 160), (0, 0, 0, 0))
    ImageDraw.Draw(im).text((80, 80), ch, font=font, embedded_color=True, anchor="mm")
    im = im.crop(im.getbbox())
    side = max(im.size)
    sq = Image.new("RGBA", (side, side), (0, 0, 0, 0))
    sq.paste(im, ((side - im.size[0]) // 2, (side - im.size[1]) // 2))
    sq.resize((128, 128), Image.LANCZOS).save(os.path.join(OUT, name + ".png"), optimize=True)
    print(name)
