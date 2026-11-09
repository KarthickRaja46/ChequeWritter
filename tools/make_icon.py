"""Draws the Cheque Writer icon and saves src/ChequeWriter/app.ico (16-256 px).
Each size is drawn separately so small sizes stay sharp.
Usage: python tools/make_icon.py   (needs Pillow)"""
from pathlib import Path
from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parent.parent
OUT = ROOT / 'src' / 'ChequeWriter' / 'app.ico'
SIZES = [16, 20, 24, 32, 40, 48, 64, 128, 256]

BLUE_TOP = (18, 108, 224)
BLUE_BOTTOM = (8, 58, 140)
INK = (30, 41, 59)
LINE = (148, 163, 184)
BOX = (219, 234, 254)
BOX_EDGE = (59, 130, 246)
GOLD = (245, 179, 38)
GOLD_DARK = (180, 120, 10)


def draw(size: int) -> Image.Image:
    s = size * 4  # supersample, then shrink for smooth edges
    img = Image.new('RGBA', (s, s), (0, 0, 0, 0))

    # rounded-square background with a vertical gradient
    grad = Image.new('RGBA', (s, s))
    gd = ImageDraw.Draw(grad)
    for y in range(s):
        t = y / (s - 1)
        gd.line([(0, y), (s, y)], fill=tuple(int(a + (b - a) * t) for a, b in zip(BLUE_TOP, BLUE_BOTTOM)) + (255,))
    mask = Image.new('L', (s, s), 0)
    ImageDraw.Draw(mask).rounded_rectangle([0, 0, s - 1, s - 1], radius=int(s * 0.2), fill=255)
    img.paste(grad, (0, 0), mask)

    d = ImageDraw.Draw(img)
    u = s / 100  # 1 unit = 1% of the icon

    # the cheque
    cx0, cy0, cx1, cy1 = 10 * u, 27 * u, 90 * u, 73 * u
    d.rounded_rectangle([cx0, cy0, cx1, cy1], radius=4 * u, fill=(255, 255, 255, 255))

    if size <= 24:
        # tiny sizes: just two bold lines and the amount box
        d.rectangle([17 * u, 39 * u, 55 * u, 45 * u], fill=INK)
        d.rectangle([17 * u, 54 * u, 48 * u, 59 * u], fill=LINE)
        d.rectangle([61 * u, 49 * u, 83 * u, 63 * u], fill=BOX_EDGE)
    else:
        lw = max(u * 2.2, 2)
        d.rectangle([34 * u, 33 * u, 54 * u, 33 * u + lw * 1.4], fill=INK)              # bank name
        d.rectangle([62 * u, 33 * u, 84 * u, 33 * u + lw], fill=LINE)                    # date
        d.rectangle([16 * u, 44 * u, 70 * u, 44 * u + lw], fill=LINE)                    # pay to
        d.rectangle([16 * u, 53 * u, 56 * u, 53 * u + lw], fill=LINE)                    # amount in words
        d.rectangle([16 * u, 62 * u, 46 * u, 62 * u + lw], fill=LINE)
        d.rounded_rectangle([60 * u, 51 * u, 84 * u, 65 * u], radius=2 * u,
                            fill=BOX, outline=BOX_EDGE, width=max(int(u * 1.5), 2))      # amount box
        if size >= 48:
            # two crossing lines in the top-left corner (A/C Payee)
            w = max(int(u * 1.2), 2)
            d.line([(12 * u, 41 * u), (25 * u, 28 * u)], fill=BOX_EDGE, width=w)
            d.line([(12 * u, 47 * u), (31 * u, 28 * u)], fill=BOX_EDGE, width=w)

        # pen crossing the bottom-right corner
        pen = Image.new('RGBA', (s, s), (0, 0, 0, 0))
        pd = ImageDraw.Draw(pen)
        body = [(56 * u, 92 * u), (90 * u, 58 * u), (97 * u, 65 * u), (63 * u, 99 * u)]
        pd.polygon(body, fill=GOLD, outline=GOLD_DARK)
        pd.polygon([(56 * u, 92 * u), (63 * u, 99 * u), (50 * u, 104 * u)], fill=(51, 65, 85))   # nib
        pd.line([(85 * u, 63 * u), (92 * u, 70 * u)], fill=GOLD_DARK, width=max(int(u * 1.5), 2))
        clip = Image.new('L', (s, s), 0)
        ImageDraw.Draw(clip).rounded_rectangle([0, 0, s - 1, s - 1], radius=int(s * 0.2), fill=255)
        img.paste(pen, (0, 0), Image.composite(pen.split()[3], Image.new('L', (s, s), 0), clip))

    return img.resize((size, size), Image.LANCZOS)


def main():
    images = [draw(sz) for sz in SIZES]
    images[-1].save(OUT, format='ICO', sizes=[(sz, sz) for sz in SIZES], append_images=images[:-1])
    (ROOT / 'docs').mkdir(exist_ok=True)
    images[-1].save(ROOT / 'docs' / 'icon.png')
    print('wrote', OUT)


if __name__ == '__main__':
    main()
