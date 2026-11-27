#!/usr/bin/env python3
"""
import_aerial_sheets.py – đổi các sheet vẽ tay trong ArtSource/PixelPack/AerialSheets thành clip Unity đúng quy chuẩn
BasicActions (khung 224×176, điểm chân (80, 160), PPU 64, .anim 60 tick/giây) + tách vật thể chiêu ra SkillObjects.

Sheet đầu vào (4 cột, ô chia đều; ảnh vẽ phân giải cao, nền trong suốt hoặc nhiễu alpha thấp):
  air_<nv>.png   6 hàng: đấm trên không · đấm khi rơi · đá trên không · đá khi rơi (đá bổ) · bị kéo · loạng choạng sau khi bị kéo
  cast_<nv>.png  5 hàng = 5 chiêu theo thứ tự catalog.json (không vẽ vật thể/hiệu ứng – engine vẽ riêng)
                 · nhân vật đang lơ lửng trong sheet (Long)  → AerialSkills/<nv>/<chiêu>/rise + fall
                 · nhân vật đứng đất trong sheet (Bảo)        → AerialSkills/<nv>/<chiêu>/ground

Xử lý mỗi ô: giữ cụm pixel chính (bỏ phần lấn từ ô bên cạnh, nhiễu nền), thu nhỏ về đúng chiều cao nhân vật trong
BasicActions (so tư thế đầu hàng với jump_air / idle_fight), ép về bảng màu sẵn có của nhân vật, đặt chân ở dòng 160.

Vật thể rời (SkillObjects/): giàn giáo + móc dây tách từ keyframe gốc của chiêu (phần nằm ngoài thân nhân vật),
đoạn dây cáp lấy màu từ chính cuộn cáp. Khang ném dây dưới đất dùng khung đấm của Khang (không có dây vẽ sẵn) và
cable_grips.json ghi vị trí bàn tay từng frame để dây mọc ra đúng tay.

Chạy từ thư mục gốc project:  python3 Tools/import_aerial_sheets.py
Rồi trong Unity: Loan Dau → PixelPack → Dựng dữ liệu + scene.
"""
import hashlib
import json
import os
import re

import numpy as np
from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SRC = os.path.join(ROOT, "ArtSource", "PixelPack", "AerialSheets")
CATALOG = os.path.join(ROOT, "ArtSource", "PixelPack", "catalog.json")
PACK = os.path.join(ROOT, "UnityProject", "Assets", "Art", "PixelPack")
FW, FH, PX, PY = 224, 176, 80, 160          # khung + điểm chân (toạ độ ảnh, gốc trên-trái)
PEOPLE = ["long", "bao", "linh", "mai", "khang", "ba_tu"]
FOOT_ROW = PY - 1                            # dòng pixel thấp nhất của chân trong BasicActions

# hàng của sheet air_<nv> → (clip, các frame, tick mỗi frame)
AIR_ROWS = {
    0: [("air_light", [0, 1, 2, 3], [2, 3, 4, 13])],
    1: [("fall_punch", [0, 1, 2, 3], [3, 5, 7, 7])],
    2: [("air_heavy", [0, 1, 2, 3], [4, 5, 12, 7])],
    3: [("fall_kick", [0, 1, 2, 3], [4, 6, 10, 8]),
        ("air_dive", [0, 1, 2], [3, 3, 6]),          # engine giữ frame cuối tới khi chạm đất
        ("air_dive_land", [3], [15])],
    4: [("pulled_front", [0, 1, 2, 3], [2, 3, 2, 3])],   # 10 tick = thời gian bị cuộn dây (FightSim)
    5: [("pulled_recover", [0, 1, 2, 3], [4, 4, 5, 6])],
}
SKILL_TICKS = [4, 4, 6, 6]
# sheet cast_<nv>: vẽ lúc đang bay → bản trên không (rise/fall); vẽ đứng đất → bản dưới đất (ground). Mặc định: ground.
CAST_PHASES = {"long": ("rise", "fall"), "bao": ("ground",)}   # clip chiêu: engine kéo giãn theo độ dài chiêu gốc


# ------------------------------------------------------------------ gán nhãn / giãn vùng (thuần numpy, không cần scipy)
def dilate(mask, iterations=1):
    m = mask.copy()
    for _ in range(iterations):
        g = m.copy()
        g[1:] |= m[:-1]; g[:-1] |= m[1:]; g[:, 1:] |= m[:, :-1]; g[:, :-1] |= m[:, 1:]
        m = g
    return m


def label(mask):
    """Gán nhãn cụm 8 hướng → (mảng nhãn 1..n, n, kích thước từng cụm)."""
    lab = np.zeros(mask.shape, np.int32)
    h, w = mask.shape
    sizes, n = [], 0
    for y0, x0 in zip(*np.nonzero(mask)):
        if lab[y0, x0]:
            continue
        n += 1
        stack, cnt = [(y0, x0)], 0
        lab[y0, x0] = n
        while stack:
            y, x = stack.pop()
            cnt += 1
            for dy in (-1, 0, 1):
                for dx in (-1, 0, 1):
                    yy, xx = y + dy, x + dx
                    if 0 <= yy < h and 0 <= xx < w and mask[yy, xx] and not lab[yy, xx]:
                        lab[yy, xx] = n
                        stack.append((yy, xx))
        sizes.append(cnt)
    return lab, n, np.array(sizes)


# ------------------------------------------------------------------ Unity asset text
def guid_for(path):
    return hashlib.md5(("loandau-aerial:" + os.path.relpath(path, ROOT)).encode()).hexdigest()


def existing_guid(meta_path):
    if os.path.exists(meta_path):
        m = re.search(r"^guid: ([0-9a-f]{32})", open(meta_path, encoding="utf-8").read(), re.M)
        if m:
            return m.group(1)
    return None


def ensure_folder(path):
    if os.path.isdir(path):
        return
    ensure_folder(os.path.dirname(path))
    os.makedirs(path)
    with open(path + ".meta", "w", encoding="utf-8") as f:
        f.write(f"fileFormatVersion: 2\nguid: {guid_for(path)}\nfolderAsset: yes\nDefaultImporter:\n"
                "  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n")


TEMPLATE_META = os.path.join(PACK, "BasicActions", "khang", "jump_air.png.meta")


def write_texture_meta(png_path, names, frame_w, frame_h, pivot):
    """Meta sprite nhiều ô (cắt ngang) – chép mọi thiết lập từ meta BasicActions có sẵn, chỉ thay guid + bảng ô."""
    meta = open(TEMPLATE_META, encoding="utf-8").read()
    guid = existing_guid(png_path + ".meta") or guid_for(png_path)
    meta = re.sub(r"^guid: [0-9a-f]+", "guid: " + guid, meta, flags=re.M)
    ids = [21300000 + 2 * i for i in range(len(names))]
    table = "".join(f"  - first:\n      213: {i}\n    second: {n}\n" for n, i in zip(names, ids))
    meta = re.sub(r"  internalIDToNameTable:\n(?:  - first:\n.*\n.*\n)*", "  internalIDToNameTable:\n" + table, meta)
    multi = len(names) > 1
    meta = meta.replace("  spriteMode: 2", "  spriteMode: " + ("2" if multi else "1"))
    sprites = ""
    for k, (n, i) in enumerate(zip(names, ids)):
        sid = hashlib.md5((guid + n).encode()).hexdigest()
        sprites += (f"    - serializedVersion: 2\n      name: {n}\n      rect:\n        serializedVersion: 2\n"
                    f"        x: {k * frame_w}\n        y: 0\n        width: {frame_w}\n        height: {frame_h}\n"
                    f"      alignment: 9\n      pivot: {{x: {pivot[0]:.8f}, y: {pivot[1]:.8f}}}\n"
                    "      border: {x: 0, y: 0, z: 0, w: 0}\n      customData: \n      outline: []\n"
                    "      physicsShape: []\n      tessellationDetail: -1\n      bones: []\n"
                    f"      spriteID: {sid}\n      internalID: {i}\n      vertices: []\n      indices: \n"
                    "      edges: []\n      weights: []\n")
    meta = re.sub(r"    sprites:\n(?:    - serializedVersion: 2\n(?:      .*\n|        .*\n)*)*",
                  "    sprites:\n" + (sprites if multi else "") , meta)
    meta = re.sub(r"  spritePivot: \{[^}]*\}", f"  spritePivot: {{x: {pivot[0]:.8f}, y: {pivot[1]:.8f}}}", meta)
    meta = meta.replace("  alignment: 9\n  spritePivot", "  alignment: 9\n  spritePivot")
    table2 = "".join(f"      {n}: {i}\n" for n, i in zip(names, ids)) if multi else ""
    meta = re.sub(r"    nameFileIdTable:\n(?:      .*\n)*", "    nameFileIdTable:\n" + table2, meta)
    if not multi:
        meta = re.sub(r"  internalIDToNameTable:\n(?:  - first:\n.*\n.*\n)*", "  internalIDToNameTable: []\n", meta)
    with open(png_path + ".meta", "w", encoding="utf-8") as f:
        f.write(meta)
    return guid, ids


def write_anim(anim_path, name, guid, ids, frame_order, ticks, loop=False):
    keys, t = [], 0
    for fi, tk in zip(frame_order, ticks):
        keys.append((t, ids[fi]))
        t += tk
    keys.append((t - 1, ids[frame_order[-1]]))     # khoá cuối (như .anim gốc của PixelPack)
    curve = "".join(f"    - time: {k / 60:.8f}\n      value: {{fileID: {i}, guid: {guid}, type: 3}}\n" for k, i in keys)
    mapping = "".join(f"    - {{fileID: {i}, guid: {guid}, type: 3}}\n" for _, i in keys)
    text = f"""%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!74 &7400000
AnimationClip:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_Name: {name}
  serializedVersion: 8
  m_Legacy: 0
  m_Compressed: 0
  m_UseHighQualityCurve: 1
  m_RotationCurves: []
  m_CompressedRotationCurves: []
  m_EulerCurves: []
  m_PositionCurves: []
  m_ScaleCurves: []
  m_FloatCurves: []
  m_PPtrCurves:
  - serializedVersion: 2
    curve:
{curve}    attribute: m_Sprite
    path:
    classID: 212
    script: {{fileID: 0}}
    flags: 2
  m_SampleRate: 60
  m_WrapMode: 0
  m_Bounds:
    m_Center: {{x: 0, y: 0, z: 0}}
    m_Extent: {{x: 0, y: 0, z: 0}}
  m_ClipBindingConstant:
    genericBindings:
    - serializedVersion: 2
      path: 0
      attribute: 0
      script: {{fileID: 0}}
      typeID: 212
      customType: 23
      isPPtrCurve: 1
      isIntCurve: 0
      isSerializeReferenceCurve: 0
      metaData: 0
    pptrCurveMapping:
{mapping}  m_AnimationClipSettings:
    serializedVersion: 2
    m_AdditiveReferencePoseClip: {{fileID: 0}}
    m_AdditiveReferencePoseTime: 0
    m_StartTime: 0
    m_StopTime: {t / 60:.8f}
    m_OrientationOffsetY: 0
    m_Level: 0
    m_CycleOffset: 0
    m_HasAdditiveReferencePose: 0
    m_LoopTime: {1 if loop else 0}
    m_LoopBlend: 0
    m_LoopBlendOrientation: 0
    m_LoopBlendPositionY: 0
    m_LoopBlendPositionXZ: 0
    m_KeepOriginalOrientation: 0
    m_KeepOriginalPositionY: 1
    m_KeepOriginalPositionXZ: 0
    m_HeightFromFeet: 0
    m_Mirror: 0
  m_EditorCurves: []
  m_EulerEditorCurves: []
  m_HasGenericRootTransform: 0
  m_HasMotionFloatCurves: 0
  m_Events: []
"""
    with open(anim_path, "w", encoding="utf-8") as f:
        f.write(text)
    if not os.path.exists(anim_path + ".meta"):
        with open(anim_path + ".meta", "w", encoding="utf-8") as f:
            f.write(f"fileFormatVersion: 2\nguid: {guid_for(anim_path)}\nNativeFormatImporter:\n  externalObjects: {{}}\n"
                    "  mainObjectFileID: 7400000\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n")


def save_strip(folder, clip, frames, order, ticks):
    """frames: list ảnh RGBA 224×176 (đã đặt chân) → <clip>.png + meta + .anim"""
    ensure_folder(folder)
    strip = Image.new("RGBA", (FW * len(frames), FH), (0, 0, 0, 0))
    for i, f in enumerate(frames):
        strip.paste(f, (i * FW, 0))
    png = os.path.join(folder, clip + ".png")
    strip.save(png)
    names = [f"{clip}_{i:02d}" for i in range(len(frames))]
    guid, ids = write_texture_meta(png, names, FW, FH, (PX / FW, (FH - PY) / FH))
    write_anim(os.path.join(folder, clip + ".anim"), clip, guid, ids, order, ticks)


# ------------------------------------------------------------------ xử lý hình
def strip_frames(path):
    im = Image.open(path).convert("RGBA")
    n = im.width // FW
    return [im.crop((i * FW, 0, (i + 1) * FW, FH)) for i in range(n)]


def palette_of(fighter):
    cols = []
    for clip in ("idle_fight", "jump_air", "hit_heavy", "punch", "kick"):
        p = os.path.join(PACK, "BasicActions", fighter, clip + ".png")
        if os.path.exists(p):
            a = np.array(Image.open(p).convert("RGBA")).reshape(-1, 4)
            cols.append(a[a[:, 3] > 200][:, :3])
    pal = np.unique(np.concatenate(cols), axis=0)
    return pal.astype(np.int32)


def opaque_height(img):
    a = np.array(img)[..., 3] > 128
    ys = np.nonzero(a.any(1))[0]
    return ys[-1] - ys[0] + 1


def clean_cell(cell):
    """Giữ cụm pixel chính của ô (thân nhân vật + chi tiết rời sát bên), bỏ nhiễu nền và phần lấn từ ô cạnh."""
    a = np.array(cell).astype(np.int32)
    solid = a[..., 3] >= 128
    lab, n, sizes = label(solid)
    if n == 0:
        return None
    main = lab == (int(np.argmax(sizes)) + 1)
    near_ids = set(np.unique(lab[dilate(main, 8)]))
    keep = np.isin(lab, [i + 1 for i in range(n) if sizes[i] >= 12 and (i + 1) in near_ids])
    keep = dilate(keep, 2) & (a[..., 3] > 40)   # giữ viền chống răng cưa
    a[~keep] = 0
    return a


def downscale(arr, scale, pal):
    """Thu nhỏ RGBA (trung bình có trọng số alpha), alpha nhị phân, ép màu về bảng màu nhân vật."""
    h, w = arr.shape[:2]
    nw, nh = max(1, round(w * scale)), max(1, round(h * scale))
    alpha = arr[..., 3:4] / 255.0
    pre = np.concatenate([arr[..., :3] * alpha, alpha * 255], axis=2).astype(np.float32)
    chans = [np.array(Image.fromarray(pre[..., c]).resize((nw, nh), Image.BOX)) for c in range(4)]
    out = np.stack(chans, axis=2)
    a = out[..., 3] / 255.0
    solid = a >= 0.5
    rgb = np.zeros((nh, nw, 3), np.float32)
    rgb[solid] = out[..., :3][solid] / a[solid][:, None]
    res = np.zeros((nh, nw, 4), np.uint8)
    if solid.any():
        px = rgb[solid].astype(np.int32)
        d = ((px[:, None, :] - pal[None, :, :]) ** 2).sum(2)
        res[..., :3][solid] = pal[d.argmin(1)]
        res[..., 3][solid] = 255
    return res


def place(arr, anchor_x):
    """Đặt hình (đã thu nhỏ) vào khung 224×176: đáy hình ở dòng chân, anchor_x (cột trong hình) trùng điểm chân."""
    frame = np.zeros((FH, FW, 4), np.uint8)
    ys = np.nonzero(arr[..., 3].any(1))[0]
    if not len(ys):
        return Image.fromarray(frame)
    arr = arr[: ys[-1] + 1]
    h, w = arr.shape[:2]
    top, left = FOOT_ROW + 1 - h, PX - anchor_x
    sy0, sx0 = max(0, -top), max(0, -left)
    dy0, dx0 = max(0, top), max(0, left)
    hh, ww = min(h - sy0, FH - dy0), min(w - sx0, FW - dx0)
    frame[dy0:dy0 + hh, dx0:dx0 + ww] = arr[sy0:sy0 + hh, sx0:sx0 + ww]
    if sx0 or sy0 or ww < w - sx0:
        print(f"    ! frame bị cắt mép khung ({w}×{h})")
    return Image.fromarray(frame)


def split_points(profile, count, length):
    """Ranh giới giữa các ô: dải trống thật của sheet gần vị trí chia đều nhất (sheet vẽ tay không chia đều tuyệt đối)."""
    blank = profile <= 2
    bands, i = [], 0
    while i < length:
        if blank[i]:
            j = i
            while j < length and blank[j]:
                j += 1
            bands.append((i, j))
            i = j
        else:
            i += 1
    cuts = [0]
    for k in range(1, count):
        want = k * length / count
        near = [b for b in bands if abs((b[0] + b[1]) / 2 - want) < length / count / 2.5]
        if near:
            b = min(near, key=lambda b: abs((b[0] + b[1]) / 2 - want))
            cuts.append((b[0] + b[1]) // 2)
        else:                                   # không có khe trống: chỗ ít pixel nhất quanh vị trí chia đều
            lo, hi = int(want - length / count / 4), int(want + length / count / 4)
            cuts.append(lo + int(np.argmin(profile[lo:hi])))
    return cuts + [length]


def sheet_cells(path, rows):
    im = Image.open(path).convert("RGBA")
    solid = np.array(im)[..., 3] >= 128
    ys = split_points(solid.sum(1), rows, im.height)
    cells = []
    for r in range(rows):
        band = solid[ys[r]:ys[r + 1]]
        xs = split_points(band.sum(0), 4, im.width)
        cells.append([clean_cell(im.crop((xs[c], ys[r], xs[c + 1], ys[r + 1]))) for c in range(4)])
    return cells


def drawn_height(cell):
    ys = np.nonzero((cell[..., 3] > 0).any(1))[0]
    return ys[-1] - ys[0] + 1


def basic_scale(fighter):
    """Tỷ lệ thu nhỏ mà BasicActions hiện có đã dùng: dáng đứng ô đầu BasicSheets/<nv>.png → idle_fight.
    Sheet mới cùng người vẽ, cùng cỡ nét → dùng đúng tỷ lệ này (đo dáng nhảy của sheet mới không tin được: mỗi sheet
    vẽ độ co chân khác nhau)."""
    sheet = Image.open(os.path.join(ROOT, "ArtSource", "PixelPack", "BasicSheets", fighter + ".png")).convert("RGBA")
    cell = clean_cell(sheet.crop((0, 0, sheet.width // 4, sheet.height // 6)))
    idle = strip_frames(os.path.join(PACK, "BasicActions", fighter, "idle_fight.png"))
    return float(np.median([opaque_height(f) for f in idle])) / drawn_height(cell)


def sheet_rows(path, rows, fighter, neutral_clip=None):
    """→ list hàng, mỗi hàng 4 frame 224×176 đã chuẩn hoá.
    neutral_clip: frame đầu mọi hàng là dáng thủ của clip này (sheet chiêu) → khớp chiều cao trực tiếp;
    không có → dùng tỷ lệ của BasicSheets."""
    cells = sheet_cells(path, rows)
    if neutral_clip:
        ref = strip_frames(os.path.join(PACK, "BasicActions", fighter, neutral_clip + ".png"))
        scale = float(np.median([opaque_height(f) for f in ref])) / float(np.median([drawn_height(r[0]) for r in cells]))
    else:
        scale = basic_scale(fighter)
    pal = palette_of(fighter)
    out = []
    for r in range(rows):
        row_imgs = [downscale(c, scale, pal) for c in cells[r]]
        xs = np.nonzero(row_imgs[0][..., 3].any(0))[0]
        anchor = (xs[0] + xs[-1]) // 2          # tâm frame đầu hàng = điểm chân; các frame sau giữ chuyển động tương đối
        out.append([place(img, anchor) for img in row_imgs])
    print(f"  {os.path.basename(path)}: thu nhỏ ×{scale:.3f}")
    return out


def hand_points(frames):
    """Vị trí bàn tay phía trước (px từ điểm chân, x hướng mặt, y lên) – điểm xa nhất ở tầm ngực/vai."""
    pts = []
    for f in frames:
        a = np.array(f)[..., 3] > 0
        band = a[PY - 75:PY - 30]
        cols = np.nonzero(band.any(0))[0]
        x = cols[-1]
        rows = np.nonzero(band[:, x])[0]
        y = PY - 75 + int(rows.mean())
        pts.append({"x": int(x - PX), "y": int(PY - y)})
    return pts


# ------------------------------------------------------------------ vật thể rời
def skill_keyframe(fighter, clip, i):
    return Image.open(os.path.join(PACK, "Characters", fighter, clip, f"{i}.png")).convert("RGBA")


def cut_box(img, box, drop_blue=False):
    """Cắt vật thể khỏi keyframe chiêu theo hộp (đo tay trên keyframe gốc), chỉ giữ cụm pixel chính."""
    a = np.array(img.crop(box)).astype(np.int32)
    if drop_blue:                                       # tia điện xanh quanh móc: engine tự vẽ hiệu ứng điện
        a[(a[..., 2] > a[..., 0] + 30) & (a[..., 3] > 0)] = 0
    lab, n, sizes = label(a[..., 3] > 0)
    if n == 0:
        return None
    a[~np.isin(lab, [i + 1 for i in range(n) if sizes[i] >= 0.25 * sizes.max()])] = 0
    im = Image.fromarray(a.astype(np.uint8))
    return im.crop(im.getbbox())


def save_object(name, img, pivot=(0.5, 0.5)):
    folder = os.path.join(PACK, "SkillObjects")
    ensure_folder(folder)
    png = os.path.join(folder, name + ".png")
    img.save(png)
    write_texture_meta(png, [name], img.width, img.height, pivot)
    print(f"  SkillObjects/{name}.png {img.width}×{img.height}")


# hộp cắt (x0, y0, x1, y1) trên keyframe 144×144 của chiêu – phần vật thể nằm ngoài thân nhân vật
SCAFFOLD_BOX = ("bao", "sap_gian_giao", 2, (86, 42, 133, 109))    # giàn giáo đang đổ (bỏ đám bụi dưới chân)
HOOK_BOX = ("khang", "day_cap", 2, (103, 59, 131, 80))           # vòng thòng lọng đầu dây
CABLE_BOX = ("khang", "day_cap", 2, (86, 72, 102, 79))           # đoạn dây đang căng → lấy màu


def build_objects():
    fighter, clip, kf, box = SCAFFOLD_BOX
    save_object("scaffold", cut_box(skill_keyframe(fighter, clip, kf), box))
    fighter, clip, kf, box = HOOK_BOX
    save_object("hook", cut_box(skill_keyframe(fighter, clip, kf), box, drop_blue=True))
    # đoạn dây 8×3 lặp lại: viền tối – thân vàng (vạch xoắn mỗi 4 px) – viền tối, màu lấy từ dây vẽ sẵn
    fighter, clip, kf, box = CABLE_BOX
    a = np.array(skill_keyframe(fighter, clip, kf).crop(box)).reshape(-1, 4).astype(int)
    cols = a[a[:, 3] > 0][:, :3]
    yellow = cols[(cols[:, 0] > 180) & (cols[:, 1] > 140) & (cols[:, 2] < 120)]
    dark = cols[cols.sum(1) < 200]
    ys = yellow[yellow.sum(1).argsort()]
    rope = ys[len(ys) // 2] if len(ys) else np.array([240, 200, 60])
    shade = ys[len(ys) // 6] if len(ys) else rope * 3 // 4
    edge = dark[dark.sum(1).argsort()][len(dark) // 2] if len(dark) else np.array([40, 30, 30])
    seg = Image.new("RGBA", (8, 3))
    for x in range(8):
        seg.putpixel((x, 0), (*map(int, edge), 255))
        seg.putpixel((x, 1), (*map(int, shade if x % 4 == 0 else rope), 255))
        seg.putpixel((x, 2), (*map(int, edge), 255))
    save_object("cable", seg)


# ------------------------------------------------------------------ main
def main():
    catalog = json.load(open(CATALOG, encoding="utf-8"))
    skill_clips = {}
    for cid, ch in zip(PEOPLE, catalog["characters"]):     # catalog xếp nhân vật theo thứ tự này
        skill_clips[cid] = [s["clip"] for s in ch["skills"] if s.get("clip")]
    done = []
    for fn in sorted(os.listdir(SRC)):
        m = re.match(r"(air|cast)_(\w+)\.png$", fn)
        if not m:
            continue
        kind, fighter = m.groups()
        path = os.path.join(SRC, fn)
        if kind == "air":
            rows = sheet_rows(path, 6, fighter)
            for r, outs in AIR_ROWS.items():
                for clip, order, ticks in outs:
                    frames = [rows[r][i] for i in order]
                    save_strip(os.path.join(PACK, "BasicActions", fighter), clip, frames, list(range(len(frames))), ticks)
                    done.append(f"{fighter}/{clip}")
        else:
            phases = CAST_PHASES.get(fighter, ("ground",))
            rows = sheet_rows(path, 5, fighter, "idle_fight" if phases == ("ground",) else "jump_air")
            for r, clip in enumerate(skill_clips[fighter][:5]):
                for phase in phases:
                    save_strip(os.path.join(PACK, "AerialSkills", fighter, clip), phase, rows[r], [0, 1, 2, 3], SKILL_TICKS)
                    done.append(f"{fighter}/{clip}/{phase}")
    # Khang ném dây: dưới đất = khung đấm (giữ tay duỗi trong lúc dây bay), trên không = đấm trên không mới vẽ
    punch = strip_frames(os.path.join(PACK, "BasicActions", "khang", "punch.png"))
    ext = [np.nonzero(np.array(f)[..., 3].any(0))[0][-1] for f in punch]
    peak = int(np.argmax(ext))
    order = list(range(peak + 1)) + list(range(peak + 1, len(punch)))
    ticks = [2] * peak + [18] + [3] * (len(punch) - peak - 1)
    folder = os.path.join(PACK, "AerialSkills", "khang", "day_cap")
    save_strip(folder, "ground", punch, order, ticks)
    grips = {"ground": hand_points(punch)}
    air_punch = os.path.join(PACK, "BasicActions", "khang", "air_light.png")
    if os.path.exists(air_punch):
        air = strip_frames(air_punch)
        for phase in ("rise", "fall"):
            save_strip(folder, phase, air, [0, 1, 2, 3], [3, 4, 4, 18])
        grips["rise"] = grips["fall"] = hand_points(air)
    build_objects()
    grip_path = os.path.join(PACK, "SkillObjects", "cable_grips.json")
    json.dump(grips, open(grip_path, "w", encoding="utf-8"), indent=1)
    if not os.path.exists(grip_path + ".meta"):
        with open(grip_path + ".meta", "w", encoding="utf-8") as f:
            f.write(f"fileFormatVersion: 2\nguid: {guid_for(grip_path)}\nTextScriptImporter:\n  externalObjects: {{}}\n"
                    "  userData: \n  assetBundleName: \n  assetBundleVariant: \n")
    print(f"Đã tạo {len(done)} clip:", ", ".join(done))


if __name__ == "__main__":
    main()
