"""
render_stage_layers.py – prerender stage Chợ Đêm thành các lớp parallax 2D + layout JSON.

Đầu vào: ArtSource/Stages/stage_chodem_blockout.blend (dựng bởi stage_chodem_blockout.py)
Đầu ra:  UnityProject/Assets/Art/Sprites/Stages/ChoDem/
  chodem_bg.png    hậu cảnh: nhà phố, dây điện, đèn lồng
  chodem_mid.png   trung cảnh: vỉa hè, quầy hàng
  chodem_play.png  lớp gameplay: mặt đường làn đấu, 2 mái hiên (bục nhảy)
  chodem_fg.png    tiền cảnh: mặt đường phía trước làn đấu
  chodem.sprites.json  pivot chung (= gốc toạ độ game) cho mọi lớp
  chodem_layout.json   bố cục 2D (mét): vùng đấu, sàn, bục, spawn, hazard, hệ số parallax gợi ý
Mọi lớp cùng khung hình → đặt cùng vị trí (0,0) trong Unity là khớp nhau.

Chạy: Blender -b --factory-startup -P ArtSource/Scripts/render_stage_layers.py
"""
import sys, os, math
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bpy
from mathutils import Vector
import prop_utils as pu
import sprite_utils as su

OUT = os.path.join(pu.UNITY_ART, "Sprites", "Stages", "ChoDem")
HALF_W = 16.0        # khung ngang ±16 m → 8192 px ở PPU 256 (giới hạn texture an toàn)


def meshes_under(group):
    out = []
    for o in pu.hierarchy(bpy.data.objects[group]):
        if o.type == "MESH" and not o.name.endswith("_col"):
            out.append(o)
    return out


def main():
    bpy.ops.wm.open_mainfile(filepath=os.path.join(pu.ART_SRC, "Stages", "stage_chodem_blockout.blend"))
    for o in bpy.data.objects:
        if o.name.startswith("stage_chodem_kiosk_"):       # bản gốc module đặt ngoài sân
            o.hide_render = True
    layers = {
        "bg": meshes_under("Background"),
        "mid": [o for o in meshes_under("Midground") if o.name != "stage_chodem_duong_truoc"],
        "play": meshes_under("Gameplay"),
        "fg": [bpy.data.objects["stage_chodem_duong_truoc"]],
    }
    allp = [su.to_2d(p) for p in su.world_points(layers["bg"] + layers["mid"] + layers["play"])]
    top = max(p[1] for p in allp) + 0.3
    bottom = su.to_2d((0, -1.5, 0))[1] - 1.2              # mép trước làn đấu − 1.2 m
    frame = (-HALF_W, HALF_W, bottom, top)
    origin = Vector((0, 0, 0))
    sprites = []
    for name, objs in layers.items():
        m = su.render_sprite(objs, os.path.join(OUT, f"chodem_{name}.png"), pivot_world=origin, frame=frame, pad=0)
        m["role"] = name
        sprites.append(m)
    su.write_json(os.path.join(OUT, "chodem.sprites.json"),
                  {"key": "chodem", "ppu": su.PPU, "tilt_deg": su.TILT_DEG, "sprites": sprites})

    # ---- layout 2D (đơn vị mét trên mặt phẳng game; y = độ cao đã chiếu)
    def y2(z):
        return round(su.to_2d((0, 0, z))[1], 4)

    def box2d(o):
        vs = [o.matrix_world @ v.co for v in o.data.vertices]
        return [round(min(v.x for v in vs), 4), round(max(v.x for v in vs), 4),
                y2(min(v.z for v in vs)), y2(max(v.z for v in vs))]

    root = bpy.data.objects["stage_chodem_blockout"]
    layout = {
        "stage": "chodem", "ppu": su.PPU, "tilt_deg": su.TILT_DEG,
        "arena": {"x_min": -root["arena_width_m"] / 2, "x_max": root["arena_width_m"] / 2, "floor_y": 0.0},
        "platforms": [], "walls": [], "player_spawns": [], "prop_spawns": [], "hazards": [],
        "layers": [{"file": f"chodem_{n}.png", "order": i, "parallax": p}
                   for i, (n, p) in enumerate((("bg", 0.35), ("mid", 0.12), ("play", 0.0), ("fg", -0.15)))],
    }
    for o in bpy.data.objects:
        n = o.name
        if n.startswith("stage_chodem_maihien_") and n.endswith("_col"):
            x0, x1, _, yt = box2d(o)
            layout["platforms"].append({"name": n[:-4], "x_min": x0, "x_max": x1, "top_y": yt, "one_way": True})
        elif n.startswith("stage_chodem_bien_") and n.endswith("_col"):
            x0, x1, yb, yt = box2d(o)
            layout["walls"].append({"name": n[:-4], "x_min": x0, "x_max": x1})
        elif n in ("spawn_p1", "spawn_p2"):
            p = o.matrix_world.translation
            layout["player_spawns"].append({"player": int(n[-1]), "x": round(p.x, 4), "y": y2(p.z),
                                            "facing": "right" if p.x < 0 else "left"})
        elif n.startswith("spawn_prop_"):
            p = o.matrix_world.translation
            layout["prop_spawns"].append({"name": n, "x": round(p.x, 4), "y": y2(p.z),
                                          "size": o.get("prop_size"), "default_prop": o.get("default_prop")})
        elif n.startswith("hazard_"):
            p = o.matrix_world.translation
            layout["hazards"].append({"name": n, "x": round(p.x, 4), "y": y2(p.z), "interval_s": o.get("interval_s")})
    for k in ("platforms", "walls", "player_spawns", "prop_spawns", "hazards"):
        layout[k].sort(key=lambda e: (e.get("name") or str(e.get("player")), e.get("x", 0)))
    su.write_json(os.path.join(OUT, "chodem_layout.json"), layout)
    print("RESULT", {s["file"]: s["size_px"] for s in sprites}, "platforms", layout["platforms"],
          "spawns", len(layout["prop_spawns"]))


main()
