#!/usr/bin/env python3
"""
analyze_skill_frames.py – đo hình vẽ các chiêu trong PixelPack để dựng hitbox khớp animation.

Với mỗi nhân vật:
  * idle   : khung thân ở tư thế đứng (Fighters/<id>.png) → hurtbox (vùng nhận đòn)
  * mỗi frame của mỗi chiêu (Characters/<id>/<clip>/<n>.png):
      front/back/top/bottom : biên pixel có màu, tính bằng px từ điểm chân (pivot), x dương = phía trước mặt
      ext                   : hộp bao các pixel VƯƠN RA trước thân đứng (tay, chân, vũ khí, vệt hiệu ứng)
                              [x0, y0, x1, y1] hoặc [] nếu frame không vươn ra
PixelPackBuilder (Unity) đọc file này: mỗi cụm frame liên tiếp có "ext" đủ lớn = 1 cửa sổ trúng đòn.

Chạy (từ thư mục gốc project, cần Pillow):
    python3 Tools/analyze_skill_frames.py
Đầu ra: UnityProject/Assets/Art/PixelPackDerived/SkillFrames/skill_frames.json
"""
import json
import os
import re

from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
PACK = os.path.join(ROOT, "UnityProject", "Assets", "Art", "PixelPack")
OUT = os.path.join(ROOT, "UnityProject", "Assets", "Art", "PixelPackDerived", "SkillFrames")
EXT_MARGIN = 4      # px: chỉ tính là "vươn ra" khi vượt khỏi thân đứng hơn mức này
MIN_EXT_PIXELS = 6  # bỏ qua vài pixel lẻ (tóc, bụi)


def pivot(png_path):
    meta = open(png_path + ".meta", encoding="utf-8").read()
    m = re.search(r"spritePivot: \{x: ([\d.eE-]+), y: ([\d.eE-]+)\}", meta)
    return (float(m.group(1)), float(m.group(2))) if m else (0.5, 0.0)


def measure(png_path, idle_front=None):
    im = Image.open(png_path).convert("RGBA")
    w, h = im.size
    px, py = pivot(png_path)
    ox, oy = px * w, (1 - py) * h          # toạ độ ảnh (gốc trên-trái) của điểm chân
    a = im.split()[3]
    bb = a.getbbox()
    if not bb:
        return None
    res = {
        "front": int(round(bb[2] - ox)), "back": int(round(bb[0] - ox)),
        "top": int(round(oy - bb[1])), "bottom": int(round(oy - bb[3])),
        "ext": [],
    }
    if idle_front is not None:
        cut = int(ox + idle_front + EXT_MARGIN)
        if cut < w:
            region = a.crop((cut, 0, w, h))
            rb = region.getbbox()
            if rb and sum(1 for v in region.getdata() if v > 0) >= MIN_EXT_PIXELS:
                x0, y0, x1, y1 = rb[0] + cut, rb[1], rb[2] + cut, rb[3]
                res["ext"] = [int(round(x0 - ox)), int(round(oy - y1)), int(round(x1 - ox)), int(round(oy - y0))]
    return res


BASIC_ATTACKS = ["light_1", "light_2", "light_3", "heavy", "punch", "kick", "air_light", "air_heavy", "air_dive"]


def strip_cells(png_path):
    """Các ô của dải sprite (spriteMode Multiple): [(tên, x, y, w, h, pivot_x, pivot_y)] theo thứ tự trong meta."""
    meta = open(png_path + ".meta", encoding="utf-8").read()
    pat = (r"- serializedVersion: 2\s+name: (\S+)\s+rect:\s+serializedVersion: 2\s+x: ([\d.]+)\s+y: ([\d.]+)\s+"
           r"width: ([\d.]+)\s+height: ([\d.]+)\s+alignment: \d+\s+pivot: \{x: ([\d.eE-]+), y: ([\d.eE-]+)\}")
    return [(n, float(x), float(y), float(w), float(h), float(px), float(py)) for n, x, y, w, h, px, py in re.findall(pat, meta)]


def measure_cell(im, cell, idle_front=None):
    """Đo 1 ô của dải sprite (rect Unity tính từ góc dưới-trái ảnh)."""
    _, x, y, w, h, px, py = cell
    top = im.height - (y + h)
    sub = im.crop((int(x), int(top), int(x + w), int(top + h)))
    a = sub.split()[3]
    bb = a.getbbox()
    if not bb:
        return None
    ox, oy = px * w, (1 - py) * h
    res = {"front": int(round(bb[2] - ox)), "back": int(round(bb[0] - ox)),
           "top": int(round(oy - bb[1])), "bottom": int(round(oy - bb[3])), "ext": []}
    if idle_front is not None:
        cut = int(ox + idle_front + EXT_MARGIN)
        if cut < w:
            region = a.crop((cut, 0, int(w), int(h)))
            rb = region.getbbox()
            if rb and sum(1 for v in region.getdata() if v > 0) >= MIN_EXT_PIXELS:
                x0, y0, x1, y1 = rb[0] + cut, rb[1], rb[2] + cut, rb[3]
                res["ext"] = [int(round(x0 - ox)), int(round(oy - y1)), int(round(x1 - ox)), int(round(oy - y0))]
    return res


def measure_basic(fid):
    """Đòn thường trong BasicActions/<nv>/ – so với tư thế idle_fight của chính bộ clip này."""
    folder = os.path.join(PACK, "BasicActions", fid)
    idle_png = os.path.join(folder, "idle_fight.png")
    if not os.path.exists(idle_png):
        return None, []
    idle_im = Image.open(idle_png).convert("RGBA")
    idle = measure_cell(idle_im, strip_cells(idle_png)[0])
    clips = []
    for clip in BASIC_ATTACKS:
        png = os.path.join(folder, f"{clip}.png")
        if not os.path.exists(png):
            continue
        im = Image.open(png).convert("RGBA")
        ms = [measure_cell(im, c, idle["front"]) for c in strip_cells(png)]
        clips.append({"clip": clip, "frames": ms})
        reach = [(m["ext"][2] - idle["front"]) if m and m["ext"] else 0 for m in ms]
        print(f"{fid:6s} {clip:20s} tầm vươn theo frame {reach}")
    return idle, clips


def play_order(folder, clip):
    """Thứ tự file frame theo keyframe của <clip>.anim, kèm tick (60/giây) của từng key."""
    guid_to_idx = {}
    for f in os.listdir(folder):
        if f.endswith(".png.meta"):
            m = re.search(r"guid: (\w+)", open(os.path.join(folder, f), encoding="utf-8").read())
            if m:
                guid_to_idx[m.group(1)] = int(f.split(".")[0])
    anim = os.path.join(folder, f"{clip}.anim")
    if not os.path.exists(anim):
        n = len(guid_to_idx)
        return list(range(n)), list(range(0, 6 * n, 6))
    keys = re.findall(r"time: ([\d.]+)\s+value: \{fileID: -?\d+, guid: (\w+)", open(anim, encoding="utf-8").read())
    order, ticks = [], []
    for t, g in keys:
        if g in guid_to_idx and (not order or round(float(t) * 60) > ticks[-1]):
            order.append(guid_to_idx[g])
            ticks.append(round(float(t) * 60))
    return order, ticks


def main():
    os.makedirs(OUT, exist_ok=True)
    data = {"ext_margin": EXT_MARGIN, "fighters": []}
    chars = os.path.join(PACK, "Characters")
    for fid in sorted(d for d in os.listdir(chars) if os.path.isdir(os.path.join(chars, d))):
        idle = measure(os.path.join(PACK, "Fighters", f"{fid}.png"))
        entry = {"id": fid, "idle": idle, "clips": []}
        for clip in sorted(d for d in os.listdir(os.path.join(chars, fid)) if os.path.isdir(os.path.join(chars, fid, d))):
            folder = os.path.join(chars, fid, clip)
            frames = sorted((f for f in os.listdir(folder) if f.endswith(".png")), key=lambda f: int(f[:-4]))
            ms = [measure(os.path.join(folder, f), idle["front"]) for f in frames]
            entry["clips"].append({"clip": clip, "frames": ms})
            # tóm tắt theo THỨ TỰ PHÁT (.anim) – PixelPack xếp keyframe gốc ở file 0–3, frame nội suy ở 4–15
            order, ticks = play_order(folder, clip)
            reach = [(ms[i]["ext"][2] - idle["front"]) if ms[i] and ms[i]["ext"] else 0 for i in order]
            peak = max(reach) if reach else 0
            thr = max(10, peak * 60 // 100)
            active = [ticks[k] for k, r in enumerate(reach) if r >= thr]
            print(f"{fid:6s} {clip:20s} thứ tự phát {order}  vươn đỉnh {peak:2d}px  ra đòn tick {active[0] if active else '-'}–{active[-1] if active else '-'}")
        entry["basicIdle"], entry["basicClips"] = measure_basic(fid)
        data["fighters"].append(entry)
    with open(os.path.join(OUT, "skill_frames.json"), "w", encoding="utf-8") as f:
        json.dump(data, f, ensure_ascii=False, indent=1)


if __name__ == "__main__":
    main()
