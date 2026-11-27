"""
render_sprites.py – prerender prop + nhân vật tạm thành sprite 2D (chạy sau khi đã dựng .blend).

Prop (mỗi key) → UnityProject/Assets/Art/Sprites/Props/<key>/
  <key>.png            nằm trên sàn; pivot = điểm chạm sàn (giữa đáy)
  <key>_held.png       tư thế cầm tay (đầu đánh hướng lên); pivot = điểm cầm (_grip)
  <key>_broken_NN.png  từng mảnh vỡ; pivot = tâm mảnh; offset_m = vị trí mảnh so với pivot sprite gốc
  <key>_dented.png     bản móp (nếu có)
  <key>.sprites.json   metadata cho Unity (pivot, kích thước, offset)
Nhân vật tạm → Assets/Art/Sprites/Characters/char_proto_p{1,2}/ (quay mặt sang phải, chéo 3/4 về camera)

Chạy: Blender -b --factory-startup -P ArtSource/Scripts/render_sprites.py
"""
import sys, os, math
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bpy
from mathutils import Vector, Euler
import prop_utils as pu
import sprite_utils as su

OUT = os.path.join(pu.UNITY_ART, "Sprites")
PROPS = ["ghe_nhua", "chai_thuy_tinh", "thung_phi"]
report = {}


def centroid(o):
    bpy.context.view_layer.update()
    vs = [o.matrix_world @ v.co for v in o.data.vertices]
    return sum(vs, Vector()) / len(vs)


def bottom_center(o):
    vs = su.world_points([o])
    return Vector(((min(v.x for v in vs) + max(v.x for v in vs)) / 2, 0.0, min(v.z for v in vs)))


def render_prop(key):
    bpy.ops.wm.open_mainfile(filepath=os.path.join(pu.ART_SRC, "Props", f"prop_{key}.blend"))
    root = bpy.data.objects[f"prop_{key}"]
    vis = bpy.data.objects[key]
    rest = Euler([math.radians(float(a)) for a in root["rest_euler_blender_deg"].split(",")], "XYZ")
    grip_nat = Vector([float(a) for a in root["rest_offset_blender"].split(",")])
    d = os.path.join(OUT, "Props", key)
    meta = {"key": key, "ppu": su.PPU, "tilt_deg": su.TILT_DEG, "sprites": []}
    for k in ("prop_size", "weapon_type", "hit_dmg", "throw_dmg", "durability", "traits"):
        if k in root:
            meta[k] = root[k]

    # 1) nằm trên sàn
    root.rotation_mode = "XYZ"
    root.rotation_euler = rest
    root.location = grip_nat
    bpy.context.view_layer.update()
    base = bottom_center(vis)
    m = su.render_sprite([vis], os.path.join(d, f"{key}.png"), pivot_world=base,
                         extra_points={"grip": grip_nat})
    m["role"] = "rest"
    meta["sprites"].append(m)
    base2d = su.to_2d(base)

    # 2) mảnh vỡ / bản móp (ở tư thế nằm sàn)
    for o in sorted([o for o in bpy.data.objects if o.name.startswith(f"{key}_broken_")], key=lambda o: o.name):
        o.hide_set(False)
        c = centroid(o)
        mm = su.render_sprite([o], os.path.join(d, f"{o.name}.png"), pivot_world=c)
        c2 = su.to_2d(c)
        mm["role"] = "piece"
        mm["offset_m"] = [round(c2[0] - base2d[0], 4), round(c2[1] - base2d[1], 4)]
        meta["sprites"].append(mm)
    dented = bpy.data.objects.get(f"{key}_dented")
    if dented:
        dented.hide_set(False)
        mm = su.render_sprite([dented], os.path.join(d, f"{key}_dented.png"), pivot_world=bottom_center(dented))
        mm["role"] = "dented"
        meta["sprites"].append(mm)

    # 3) tư thế cầm tay: asset space, điểm cầm ở gốc, đầu đánh hướng lên
    root.rotation_euler = (0, 0, 0)
    root.location = (0, 0, 0)
    bpy.context.view_layer.update()
    mm = su.render_sprite([vis], os.path.join(d, f"{key}_held.png"), pivot_world=Vector((0, 0, 0)))
    mm["role"] = "held"
    meta["sprites"].append(mm)

    su.write_json(os.path.join(d, f"{key}.sprites.json"), meta)
    report[key] = [(s["file"], s["size_px"]) for s in meta["sprites"]]


def render_chars():
    bpy.ops.wm.open_mainfile(filepath=os.path.join(pu.ART_SRC, "Characters", "char_proto.blend"))
    for p in ("p1", "p2"):
        name = f"char_proto_{p}"
        root = bpy.data.objects[f"{name}_root"]
        root.location = (0, 0, 0)
        root.rotation_mode = "XYZ"
        root.rotation_euler = (0, 0, math.radians(65))   # mặt (-Y) quay sang +X, chéo 25° về camera
        bpy.context.view_layer.update()
        body = bpy.data.objects[name]
        hand_r = bpy.data.objects[f"{name}_hand_r_socket"].matrix_world.translation
        hand_l = bpy.data.objects[f"{name}_hand_l_socket"].matrix_world.translation
        d = os.path.join(OUT, "Characters", name)
        m = su.render_sprite([body], os.path.join(d, f"{name}_idle.png"), pivot_world=Vector((0, 0, 0)),
                             extra_points={"hand_r": hand_r, "hand_l": hand_l})
        m["role"] = "idle"
        meta = {"key": name, "ppu": su.PPU, "tilt_deg": su.TILT_DEG, "facing": "right", "sprites": [m]}
        su.write_json(os.path.join(d, f"{name}.sprites.json"), meta)
        report[name] = [(m["file"], m["size_px"])]


for k in PROPS:
    render_prop(k)
render_chars()
print("RESULT", report)
