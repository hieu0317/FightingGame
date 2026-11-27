"""make_sheet.py – ghép các ảnh render của 1 asset thành 1 sheet để duyệt (chạy bằng python3 hệ thống, cần Pillow).
    python3 ArtSource/Scripts/make_sheet.py ghe_nhua
"""
import os, sys
from PIL import Image, ImageDraw

ROOT = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "Renders")
ORDER = ["front", "side", "34", "inhand", "broken"]


def sheet(key, thumb=360):
    d = os.path.join(ROOT, key)
    files = [(v, os.path.join(d, f"{key}_{v}.png")) for v in ORDER]
    files = [(v, f) for v, f in files if os.path.exists(f)]
    out = Image.new("RGB", (thumb * len(files), thumb + 28), (20, 20, 30))
    dr = ImageDraw.Draw(out)
    for i, (v, f) in enumerate(files):
        im = Image.open(f).convert("RGB").resize((thumb, thumb))
        out.paste(im, (i * thumb, 28))
        dr.text((i * thumb + 8, 8), f"{key} · {v}", fill=(240, 240, 240))
    p = os.path.join(ROOT, f"{key}_sheet.png")
    out.save(p)
    return p


if __name__ == "__main__":
    for k in sys.argv[1:]:
        print(sheet(k))
