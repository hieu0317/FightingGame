#!/usr/bin/env python3
"""
extract_floor_props.py – tạo sprite vật dụng nằm trên sàn ĐÚNG TỶ LỆ NHÂN VẬT từ chính bộ PixelPack.

Vì sao: ảnh trong PixelPack/Props vẽ kiểu icon (ghế nhựa cao 75 px ≈ 3/4 người) nên đặt lên sàn bị quá khổ.
Các clip tương tác của PixelPack đã vẽ sẵn vật dụng đúng tỷ lệ cạnh nhân vật → tách vật dụng ra khỏi các frame đó:
  * "diff"    : frame 0 clip nhặt (nhat) / đẩy (day) trừ tư thế chỉ-có-người InteractionLayers/<nv>/00.png
  * "landed"  : frame cuối clip ném (nem) – vật đã rơi xuống sàn, nằm tách khỏi người
  * "puddle"  : như landed nhưng vật đổ chất lỏng → cắt trên vũng, vẽ lại viền đáy 1 px
Sprite hư hỏng (DamagedProps) được thu nhỏ theo cùng tỷ lệ của từng vật.

Chạy (từ thư mục gốc project, cần Pillow):
    python3 Tools/extract_floor_props.py
Đầu ra: UnityProject/Assets/Art/PixelPackDerived/FloorProps/<id>.png, <id>_damaged.png, floor_props.json
"""
import colorsys
import json
import os
import sys

from PIL import Image, ImageChops

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
PACK = os.path.join(ROOT, "UnityProject", "Assets", "Art", "PixelPack")
LAYERS = os.path.join(ROOT, "ArtSource", "PixelPack", "InteractionLayers")
OUT = os.path.join(ROOT, "UnityProject", "Assets", "Art", "PixelPackDerived", "FloorProps")
SOURCE_FIGHTER = "long"
CELL_W, CELL_H = 224, 176          # canvas clip tương tác (hand-grips.json: canvas 224x176, floor 160)
LANDED_MIN_X = 150                 # frame cuối clip ném: vật nằm bên phải người, x >= 150

METHOD = {
    "binh_gas": "landed", "thung_phi": "landed",
    "noi_pho": "puddle", "thung_son": "puddle",
    "xe_may": "diff_day",
}  # còn lại: "diff"


def frame(strip_path, index):
    strip = Image.open(strip_path).convert("RGBA")
    return strip.crop((CELL_W * index, 0, CELL_W * index + CELL_W, CELL_H))


def is_puddle(px):
    r, g, b, a = px
    if a == 0:
        return False
    h, s, v = colorsys.rgb_to_hsv(r / 255, g / 255, b / 255)
    return 0.53 < h < 0.70 and s > 0.55 and v > 0.45


def extract_diff(pid, clip):
    f0 = frame(os.path.join(PACK, "Interactions", SOURCE_FIGHTER, pid, f"{clip}.png"), 0)
    body = Image.open(os.path.join(LAYERS, SOURCE_FIGHTER, "00.png")).convert("RGBA")
    mask = ImageChops.difference(f0, body).convert("L").point(lambda v: 255 if v > 24 else 0)
    obj = Image.new("RGBA", f0.size, (0, 0, 0, 0))
    obj.paste(f0, (0, 0), mask)
    return obj.crop(obj.getbbox())


def extract_landed(pid, puddle):
    strip_path = os.path.join(PACK, "Interactions", SOURCE_FIGHTER, pid, "nem.png")
    n = Image.open(strip_path).width // CELL_W
    f = frame(strip_path, n - 1)
    obj = f.crop((LANDED_MIN_X, 0, CELL_W, CELL_H))
    obj = obj.crop(obj.getbbox())
    if not puddle:
        return obj
    # cắt ngay trên hàng đầu tiên (từ trên xuống) có pixel vũng ở phần đáy
    px = obj.load()
    w, h = obj.size
    cut = h
    for y in range(h // 2, h):
        if any(is_puddle(px[x, y]) for x in range(w)):
            cut = y
            break
    body = obj.crop((0, 0, w, cut))
    body = body.crop(body.getbbox())
    # vẽ lại viền đáy: sao hàng cuối thành hàng mới, tô màu viền tối của pack
    w, h = body.size
    out = Image.new("RGBA", (w, h + 1), (0, 0, 0, 0))
    out.paste(body, (0, 0))
    bp, op = body.load(), out.load()
    for x in range(w):
        if bp[x, h - 1][3] > 0:
            op[x, h] = (24, 20, 28, 255)
    return out


def scaled_damaged(pid, ratio):
    src = Image.open(os.path.join(PACK, "DamagedProps", f"{pid}.png")).convert("RGBA")
    src = src.crop(src.getbbox())
    w, h = max(1, round(src.width * ratio)), max(1, round(src.height * ratio))
    # thu nhỏ chất lượng cao rồi làm sắc alpha (không bán trong suốt) để giữ chất pixel art
    small = src.resize((w, h), Image.LANCZOS)
    r, g, b, a = small.split()
    a = a.point(lambda v: 255 if v >= 128 else 0)
    return Image.merge("RGBA", (r, g, b, a))


def outline(img, color=(255, 240, 160, 255)):
    """Viền sáng 1 px bao quanh sprite (GDD: vật nhặt được có viền sáng khi đứng gần) – cùng cỡ sprite + 1 px mỗi bên."""
    w, h = img.size
    a = img.split()[3].load()
    out = Image.new("RGBA", (w + 2, h + 2), (0, 0, 0, 0))
    o = out.load()
    for y in range(-1, h + 1):
        for x in range(-1, w + 1):
            inside = 0 <= x < w and 0 <= y < h and a[x, y] > 0
            if inside:
                continue
            near = any(0 <= x + dx < w and 0 <= y + dy < h and a[x + dx, y + dy] > 0
                       for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)))
            if near:
                o[x + 1, y + 1] = color
    return out


def scaled(src, ratio):
    w, h = max(1, round(src.width * ratio)), max(1, round(src.height * ratio))
    small = src.resize((w, h), Image.LANCZOS)
    r, g, b, a = small.split()
    return Image.merge("RGBA", (r, g, b, a.point(lambda v: 255 if v >= 128 else 0)))


def main():
    if not os.path.isdir(LAYERS):
        sys.exit(f"Thiếu {LAYERS}")
    os.makedirs(OUT, exist_ok=True)
    report = {"source_fighter": SOURCE_FIGHTER, "props": {}}
    props = sorted(d for d in os.listdir(os.path.join(PACK, "Interactions", SOURCE_FIGHTER))
                   if os.path.isdir(os.path.join(PACK, "Interactions", SOURCE_FIGHTER, d)))
    for pid in props:
        method = METHOD.get(pid, "diff")
        if method == "diff":
            img = extract_diff(pid, "nhat")
        elif method == "diff_day":
            img = extract_diff(pid, "day")
        else:
            img = extract_landed(pid, method == "puddle")
        img.save(os.path.join(OUT, f"{pid}.png"))
        icon = Image.open(os.path.join(PACK, "Props", f"{pid}.png")).convert("RGBA")
        icon = icon.crop(icon.getbbox())
        ratio = img.height / icon.height
        dmg = scaled_damaged(pid, ratio)
        dmg.save(os.path.join(OUT, f"{pid}_damaged.png"))
        outline(img).save(os.path.join(OUT, f"{pid}_outline.png"))
        # mảnh vỡ (PixelPack/Debris/<id>/<n>.png) thu nhỏ cùng tỷ lệ
        debris_dir = os.path.join(PACK, "Debris", pid)
        n_debris = 0
        if os.path.isdir(debris_dir):
            for f in sorted(os.listdir(debris_dir)):
                if f.endswith(".png"):
                    piece = Image.open(os.path.join(debris_dir, f)).convert("RGBA")
                    if piece.getbbox():
                        scaled(piece.crop(piece.getbbox()), ratio).save(os.path.join(OUT, f"{pid}_debris_{n_debris}.png"))
                        n_debris += 1
        report["props"][pid] = {"method": method, "size_px": list(img.size), "icon_px": list(icon.size),
                                "ratio": round(ratio, 3), "damaged_px": list(dmg.size), "debris": n_debris}
        print(f"{pid:15s} {method:9s} {img.size[0]:3d}x{img.size[1]:<3d} (icon {icon.size[0]}x{icon.size[1]}, ×{ratio:.2f})")
    with open(os.path.join(OUT, "floor_props.json"), "w", encoding="utf-8") as f:
        json.dump(report, f, ensure_ascii=False, indent=2)


if __name__ == "__main__":
    main()
