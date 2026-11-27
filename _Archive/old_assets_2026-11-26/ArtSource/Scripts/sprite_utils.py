"""
sprite_utils.py – render model 3D thành sprite 2D (prerender) cho "Loạn Đấu Phố Đêm".

Quy chuẩn sprite:
  * Camera orthographic nhìn từ -Y, nghiêng xuống TILT_DEG (thấy mặt trên của vật → cảm giác 2.5D).
  * Mật độ cố định PPU = 256 px / mét (Unity: Pixels Per Unit = 256) → mọi sprite cùng tỷ lệ.
  * Nền trong suốt, màu phẳng từ palette (view transform "Standard" để không bị bạc màu), viền đen.
  * Toạ độ 2D của game = hình chiếu lên mặt phẳng camera: x = X Blender, y = (Y,Z) chiếu lên trục up.
    Một điểm cao z trên làn đấu (Y = 0) có y_2d = z·cos(TILT).
  * Mỗi sprite kèm pivot (0..1, gốc dưới-trái) ghi trong <nhóm>.sprites.json cạnh ảnh.
"""
import bpy
import json
import math
import os
from mathutils import Vector

PPU = 256
TILT_DEG = 12.0
PAD = 4
SPRITES = None  # gán bởi init()


def cam_basis():
    t = math.radians(TILT_DEG)
    d = Vector((0, -math.cos(t), math.sin(t)))            # từ vật → camera
    right = Vector((1, 0, 0))
    up = d.cross(right).normalized() * -1                  # (0, sin t, cos t)
    if up.z < 0:
        up = -up
    return d, right, up


def to_2d(p):
    """Toạ độ 2D (mét) của điểm thế giới p trên mặt phẳng camera."""
    _, right, up = cam_basis()
    p = Vector(p)
    return (p.dot(right), p.dot(up))


def setup(scene=None):
    s = scene or bpy.context.scene
    s.render.engine = "BLENDER_WORKBENCH"
    s.render.film_transparent = True
    s.render.image_settings.file_format = "PNG"
    s.render.image_settings.color_mode = "RGBA"
    s.render.resolution_percentage = 100
    s.view_settings.view_transform = "Standard"
    s.view_settings.look = "None"
    sh = s.display.shading
    sh.light = "STUDIO"
    sh.color_type = "TEXTURE"
    sh.show_specular_highlight = False
    sh.show_object_outline = True
    sh.object_outline_color = (0.06, 0.05, 0.1)
    sh.show_cavity = True
    sh.cavity_type = "WORLD"
    sh.cavity_ridge_factor = 0.6
    sh.cavity_valley_factor = 1.0
    s.render.use_freestyle = False
    cam = bpy.data.objects.get("_SpriteCam")
    if cam is None:
        cam = bpy.data.objects.new("_SpriteCam", bpy.data.cameras.new("_SpriteCam"))
        s.collection.objects.link(cam)
    cam.data.type = "ORTHO"
    cam.data.sensor_fit = "AUTO"
    cam.data.clip_start = 0.01
    cam.data.clip_end = 1000
    d, _, _ = cam_basis()
    cam.rotation_mode = "QUATERNION"
    cam.rotation_quaternion = (-d).to_track_quat("-Z", "Y")
    s.camera = cam
    return cam


def world_points(objs):
    bpy.context.view_layer.update()
    pts = []
    for o in objs:
        if o.type == "MESH":
            mw = o.matrix_world
            pts += [mw @ v.co for v in o.data.vertices]
    return pts


def render_sprite(objs, path, pivot_world=None, frame=None, extra_points=None, pad=PAD):
    """Render objs vào path. frame = (minx, maxx, miny, maxy) theo mét 2D để cố định khung (stage).
    Trả về metadata: kích thước px, pivot chuẩn hoá, toạ độ px của các điểm extra_points."""
    s = bpy.context.scene
    cam = setup(s)
    visible = set(objs)
    saved = {o.name: o.hide_render for o in s.objects}
    for o in s.objects:
        o.hide_render = o not in visible
    if frame is None:
        p2 = [to_2d(p) for p in world_points(objs)]
        frame = (min(p[0] for p in p2), max(p[0] for p in p2), min(p[1] for p in p2), max(p[1] for p in p2))
    minx, maxx, miny, maxy = frame
    rx = int(math.ceil((maxx - minx) * PPU)) + 2 * pad
    ry = int(math.ceil((maxy - miny) * PPU)) + 2 * pad
    s.render.resolution_x, s.render.resolution_y = rx, ry
    cam.data.ortho_scale = max(rx, ry) / PPU
    # tâm ảnh (mét 2D) → vị trí camera
    cx = minx - pad / PPU + rx / PPU / 2
    cy = miny - pad / PPU + ry / PPU / 2
    d, right, up = cam_basis()
    cam.location = right * cx + up * cy + d * 200
    os.makedirs(os.path.dirname(path), exist_ok=True)
    s.render.filepath = path
    bpy.ops.render.render(write_still=True)
    for n, h in saved.items():
        if n in bpy.data.objects:
            bpy.data.objects[n].hide_render = h

    def px(p):
        x, y = to_2d(p)
        return [round((x - minx) * PPU + pad, 2), round((y - miny) * PPU + pad, 2)]

    meta = {"file": os.path.basename(path), "size_px": [rx, ry], "ppu": PPU,
            "size_m": [round((maxx - minx), 4), round((maxy - miny), 4)]}
    if pivot_world is not None:
        p = px(pivot_world)
        meta["pivot"] = [round(p[0] / rx, 5), round(p[1] / ry, 5)]
    for k, p in (extra_points or {}).items():
        meta[k + "_px"] = px(p)
    return meta


def write_json(path, data):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, "w", encoding="utf-8") as f:
        json.dump(data, f, ensure_ascii=False, indent=2)
    return path
