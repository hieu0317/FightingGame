"""
stage_chodem_blockout.py – Blockout stage Chợ Đêm cho bản thử (GDD mục 5, Asset Bible mục 6)

Quy chuẩn: lưới 1 m · vùng chiến đấu rộng 24 m (X -12..12), sâu 3 m (Y -1.5..1.5) · mặt sàn Z = 0
           (Unity Y = 0) · bục cao 1.5–2.5 m · 3 lớp Gameplay / Trung cảnh / Hậu cảnh.
Camera nhìn từ -Y. Mọi module ≤ 1.000 tam giác.

Xuất:
  Assets/Art/Stages/ChoDem/stage_chodem_blockout.fbx  (toàn bộ sân + marker spawn)
  Assets/Art/Stages/ChoDem/stage_chodem_kiosk_{hong,xanh,vang}.fbx  (module quầy hàng 2x1 m)
Sau khi xuất, script import ngược các FBX prop/nhân vật để render xem trước (kiểm tra khứ hồi FBX).

Chạy: Blender -b --factory-startup -P ArtSource/Scripts/stage_chodem_blockout.py
"""
import sys, os, math
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bpy
from mathutils import Vector, Euler
import prop_utils as pu

ARENA_W, ARENA_D = 24, 3
PLATFORM_Z = 2.0
PLATFORM_X = (-6, 6)       # tâm 2 mái hiên, mỗi cái rộng 4 m
FACADE_Y = 5.0
OUT = os.path.join(pu.UNITY_ART, "Stages", "ChoDem")
report = {}


def empty(name, coll, parent=None, loc=(0, 0, 0), display="PLAIN_AXES", size=0.5):
    e = bpy.data.objects.new(name, None)
    e.empty_display_type = display
    e.empty_display_size = size
    e.location = loc
    coll.objects.link(e)
    e.parent = parent
    return e


def obj_done(obj, parent, budget=pu.TRI_BUDGET["stage"]):
    obj.parent = parent
    t = pu.tri_count(obj)
    report[obj.name] = t
    assert t <= budget, f"{obj.name}: {t} tam giác > {budget}"
    return obj


# ----------------------------------------------------------------- module quầy hàng
def build_kiosk(name, tarp_cell, coll):
    """Quầy hàng 2 x 1 x 1 m, mái bạt xéo cao 2.2 m. Pivot góc trái-trước-dưới (x=0, y=0, z=0), sâu theo +Y."""
    mb = pu.MeshBuilder()
    K, G = "kim_loai", "go"
    leg = 0.05
    for x in (leg / 2, 2 - leg / 2):
        for y in (leg / 2, 1 - leg / 2):
            mb.box((x, y, 0.475), (leg, leg, 0.95), K)                      # chân bàn
    mb.box((1, 0.5, 0.97), (2.04, 1.04, 0.05), G)                            # mặt bàn gỗ
    mb.box((1, 0.02, 0.55), (1.96, 0.02, 0.7), G)                            # ván chắn trước
    mb.box((1, 0.5, 0.25), (1.9, 0.9, 0.03), G)                              # kệ dưới
    # cột mái phía sau cao hơn → bạt xéo về trước
    for x in (0.03, 1.97):
        mb.box((x, 0.97, 1.6), (0.04, 0.04, 1.2), K)
        mb.box((x, 0.03, 1.5), (0.04, 0.04, 1.0), K)
    tilt = math.atan2(0.3, 1.2)
    mb.box((1, 0.45, 2.05), (2.3, 1.3, 0.04), tarp_cell, Euler((tilt, 0, 0)))   # bạt xéo (2.2 m ở sau)
    for i in range(6):                                                        # diềm bạt sọc
        mb.box((0.19 + i * 0.385, -0.18, 1.83), (0.19, 0.02, 0.14), "trang" if i % 2 else tarp_cell)
    # hàng hoá trên bàn
    mb.box((0.45, 0.5, 1.07), (0.5, 0.4, 0.14), "cam_mai")
    mb.box((1.45, 0.45, 1.06), (0.6, 0.5, 0.12), "neon_xanh")
    obj = mb.to_object(name, coll)
    return obj


# ----------------------------------------------------------------- sân
def build_stage():
    pu.open_template()
    coll = pu.get_collection("Stage_ChoDem")
    root = empty("stage_chodem_blockout", coll, size=1.0)
    g_play = empty("Gameplay", coll, root)
    g_mid = empty("Midground", coll, root)
    g_back = empty("Background", coll, root)
    g_mark = empty("Markers", coll, root)

    # --- Gameplay: sàn đường nhựa 4 x 3 m x 6 ô
    for i in range(6):
        x0 = -ARENA_W / 2 + i * 4
        mb = pu.MeshBuilder()
        mb.box((x0 + 2, 0, -0.1), (4, ARENA_D, 0.2), "be_tong")
        mb.box((x0 + 1, -1.0, 0.003), (2, 0.12, 0.006), "trang")      # vạch kẻ đứt
        obj_done(mb.to_object(f"stage_chodem_san_{i + 1:02d}", coll), g_play)
    pu.col_box("stage_chodem_san_col", (-ARENA_W / 2, -ARENA_D / 2, -0.2), (ARENA_W / 2, ARENA_D / 2, 0), coll).parent = g_play

    # --- Gameplay: 2 mái hiên bạt (bục nhảy)
    for k, cx in enumerate(PLATFORM_X):
        mb = pu.MeshBuilder()
        mb.box((cx, 0, PLATFORM_Z - 0.075), (4, ARENA_D, 0.15), "neon_hong")          # mặt bục
        for i in range(10):                                                             # diềm sọc
            mb.box((cx - 1.8 + i * 0.4, -ARENA_D / 2 - 0.01, PLATFORM_Z - 0.25), (0.4, 0.02, 0.2),
                   "trang" if i % 2 else "neon_hong")
        for sx in (-1, 1):                                                              # cột trước
            mb.box((cx + sx * 1.95, -ARENA_D / 2 + 0.03, PLATFORM_Z / 2), (0.06, 0.06, PLATFORM_Z), "kim_loai")
        # bạt nối từ mép sau bục lên tường nhà phố
        run, rise = FACADE_Y - ARENA_D / 2, 0.8
        a = math.atan2(rise, run)
        L = math.hypot(run, rise)
        mb.box((cx, ARENA_D / 2 + run / 2, PLATFORM_Z + rise / 2 - 0.05), (4, L, 0.06), "neon_hong", Euler((a, 0, 0)))
        obj_done(mb.to_object(f"stage_chodem_maihien_{k + 1:02d}", coll), g_play)
        pu.col_box(f"stage_chodem_maihien_{k + 1:02d}_col", (cx - 2, -ARENA_D / 2, PLATFORM_Z - 0.15),
                   (cx + 2, ARENA_D / 2, PLATFORM_Z), coll).parent = g_play
    for nm, x in (("l", -ARENA_W / 2), ("r", ARENA_W / 2)):                            # tường biên vô hình
        pu.col_box(f"stage_chodem_bien_{nm}_col", (x - 0.5 + (0.5 if x > 0 else -0.5) * 0, -ARENA_D / 2, 0),
                   (x + 0.5, ARENA_D / 2, 8), coll).parent = g_play

    # --- Tiền cảnh: mặt đường nối dài về phía camera (không collider, chỉ để khung hình không hụt)
    mb = pu.MeshBuilder()
    mb.box((0, -ARENA_D / 2 - 8, -0.1), (ARENA_W + 16, 16, 0.2), "be_tong")
    mb.box((0, -ARENA_D / 2 - 0.05, 0.003), (ARENA_W + 8, 0.1, 0.006), "dem")
    obj_done(mb.to_object("stage_chodem_duong_truoc", coll), g_mid)

    # --- Midground: vỉa hè + quầy hàng
    mb = pu.MeshBuilder()
    mb.box((0, ARENA_D / 2 + 1.75, 0.075), (ARENA_W + 4, 3.5, 0.15), "be_tong")
    mb.box((0, ARENA_D / 2 + 0.05, 0.08), (ARENA_W + 4, 0.1, 0.16), "trang")          # bó vỉa
    obj_done(mb.to_object("stage_chodem_viahe", coll), g_mid)
    kiosks = {}
    for tag, cell in (("hong", "neon_hong"), ("xanh", "neon_xanh"), ("vang", "vang_bao")):
        kiosks[tag] = build_kiosk(f"stage_chodem_kiosk_{tag}", cell, coll)
    placements = [("hong", -10.5), ("xanh", -2.5), ("vang", 0.5), ("hong", 9.0)]
    for i, (tag, x) in enumerate(placements):
        inst = bpy.data.objects.new(f"kiosk_inst_{i + 1:02d}", kiosks[tag].data)
        inst.location = (x, ARENA_D / 2 + 1.0, 0.15)
        coll.objects.link(inst)
        inst.parent = g_mid

    # --- Background: mặt tiền nhà phố ống 4 m ngang (chỉ mặt trước)
    heights = [8, 10, 7, 9, 11, 8, 9]
    signs = ["neon_hong", "neon_xanh", "vang_bao", "neon_hong", "neon_xanh", "cam_mai", "neon_xanh"]
    for i, h in enumerate(heights):
        x0 = -14 + i * 4
        mb = pu.MeshBuilder()
        mb.box((x0 + 2, FACADE_Y + 0.25, h / 2), (4, 0.5, h), "dem")
        mb.box((x0 + 2, FACADE_Y - 0.02, 2.9), (3.2, 0.06, 0.7), signs[i])               # biển hiệu
        for f in range(1, int((h - 3.5) // 2.5) + 1):                                    # cửa sổ tầng trên
            for wx in (1.0, 3.0):
                mb.box((x0 + wx, FACADE_Y - 0.01, 2.0 + f * 2.5 + 0.8), (1.0, 0.04, 1.1),
                       "vang_bao" if (i + f + int(wx)) % 3 == 0 else "be_tong")
        mb.box((x0 + 2, FACADE_Y - 0.01, 1.1), (3.0, 0.04, 2.2), "kim_loai")             # cửa cuốn
        obj_done(mb.to_object(f"stage_chodem_nhapho_{i + 1:02d}", coll), g_back)
    # dây điện chằng chịt + đèn lồng
    mb = pu.MeshBuilder()
    for k, (z0, z1) in enumerate(((4.2, 4.6), (4.8, 4.3), (5.1, 5.4))):
        mb.cylinder((-14, FACADE_Y - 0.3 - 0.2 * k, z0), (14, FACADE_Y - 0.3 - 0.2 * k, z1), 0.015, 4, "dem", cap=False)
    for i in range(11):
        x = -10 + i * 2
        mb.lathe([(0.12, 3.6), (0.18, 3.75), (0.18, 3.95), (0.12, 4.1)], 6, ["do_long", "do_long", "do_long"],
                 "vang_bao", "vang_bao", center=(x, FACADE_Y - 0.5, 0))
    obj_done(mb.to_object("stage_chodem_daydien_denlong", coll), g_back)

    # --- Markers (Unity đọc để đặt spawn / hazard)
    m = empty("spawn_p1", g_mark.users_collection[0], g_mark, (-4, 0, 0), "SINGLE_ARROW", 1.0)
    m.rotation_euler = (0, math.radians(90), 0)
    m = empty("spawn_p2", coll, g_mark, (4, 0, 0), "SINGLE_ARROW", 1.0)
    m.rotation_euler = (0, math.radians(-90), 0)
    spawns = [("nho", "chai_thuy_tinh", 9.0, 0.0), ("vua", "ghe_nhua", 4.8, 0.0),
              ("vua", "ghe_nhua", 6.0, PLATFORM_Z), ("lon", "thung_phi", 1.3, 0.0)]
    k = 0
    for size, prop, x, z in spawns:                                  # đối xứng hai nửa sân
        for sx in (-1, 1):
            k += 1
            e = empty(f"spawn_prop_{k:02d}", coll, g_mark, (sx * x, 0, z), "CUBE", 0.25)
            e["prop_size"] = size
            e["default_prop"] = f"prop_{prop}"
    for nm, x in (("start", -15), ("end", 15)):
        empty(f"hazard_xemay_{nm}", coll, g_mark, (x, 0, 0), "CONE", 0.6)["interval_s"] = 30
    root["arena_width_m"] = ARENA_W
    root["arena_depth_m"] = ARENA_D
    return root, kiosks


def export_kiosks(kiosks):
    files = []
    for tag, obj in kiosks.items():
        t = pu.tri_count(obj)
        assert t <= 600, f"kiosk {t} > 600"
        for o in bpy.context.view_layer.objects:
            o.select_set(False)
        obj.select_set(True)
        bpy.context.view_layer.objects.active = obj
        p = os.path.join(OUT, f"{obj.name}.fbx")
        bpy.ops.export_scene.fbx(filepath=p, use_selection=True, object_types={"MESH"}, axis_forward="-Z",
                                 axis_up="Y", bake_space_transform=True, apply_scale_options="FBX_SCALE_ALL",
                                 mesh_smooth_type="FACE", add_leaf_bones=False, bake_anim=False, path_mode="STRIP")
        files.append((p, t))
    return files


def preview(root):
    """Import ngược FBX prop + nhân vật, đặt vào marker và render."""
    imported = {}
    for f in ("prop_ghe_nhua", "prop_chai_thuy_tinh", "prop_thung_phi"):
        before = set(bpy.data.objects)
        bpy.ops.import_scene.fbx(filepath=os.path.join(pu.UNITY_ART, "Props", f + ".fbx"),
                                 axis_forward="-Z", axis_up="Y", use_custom_props=True)
        new = [o for o in bpy.data.objects if o not in before]
        imported[f] = new
    roundtrip = {}
    for f, objs in imported.items():
        r = next(o for o in objs if o.name.startswith(f))
        for o in objs:
            if o.name.endswith("_col") or "_broken" in o.name or o.name.endswith("_dented"):
                o.hide_render = True
        roundtrip[f] = {"objs": len(objs), "rest": r.get("rest_euler_blender_deg"),
                        "loc": [round(v, 3) for v in r.location], "rot": [round(math.degrees(v), 1) for v in r.rotation_euler],
                        "scale": [round(v, 3) for v in r.scale]}
    for p in ("p1", "p2"):
        bpy.ops.import_scene.fbx(filepath=os.path.join(pu.UNITY_ART, "Characters", f"char_proto_{p}.fbx"),
                                 axis_forward="-Z", axis_up="Y")
    bpy.data.objects["char_proto_p1_root"].location = bpy.data.objects["spawn_p1"].location
    bpy.data.objects["char_proto_p2_root"].location = bpy.data.objects["spawn_p2"].location
    # FBX không mang theo đường dẫn ảnh (path_mode STRIP) → gán lại M_Palette như Unity sẽ làm
    mat = pu.palette_material()
    for o in bpy.data.objects:
        if o.type == "MESH" and o.data.materials and o.data.materials[0] != mat:
            o.data.materials[0] = mat
    # đặt prop vào marker ở tư thế nằm sàn: world = T(marker + offset) · R(rest) · world_lúc_import
    from mathutils import Matrix
    bpy.context.view_layer.update()
    for e in [o for o in bpy.data.objects if o.name.startswith("spawn_prop_")]:
        src = bpy.data.objects[e["default_prop"]]
        rest = Euler([math.radians(float(a)) for a in src["rest_euler_blender_deg"].split(",")], "XYZ")
        off = Vector([float(a) for a in src["rest_offset_blender"].split(",")])
        place = Matrix.Translation(e.matrix_world.to_translation() + off) @ rest.to_matrix().to_4x4()
        for o in pu.hierarchy(src):
            if o.type != "MESH" or o.hide_render:
                continue
            inst = bpy.data.objects.new(o.name + "_" + e.name, o.data)
            bpy.context.scene.collection.objects.link(inst)
            inst.matrix_world = place @ o.matrix_world
    for f, objs in imported.items():
        for o in objs:
            o.hide_render = True
    bpy.data.objects["ref_human_1m80"].hide_render = True
    cam = pu.setup_render(res=1280)
    s = bpy.context.scene
    s.render.resolution_y = 640
    cam.data.type = "PERSP"
    cam.data.lens = 35
    cam.location = (0, -21, 4.2)
    cam.rotation_mode = "XYZ"
    cam.rotation_euler = (math.radians(82), 0, 0)
    out = [pu.render_to(os.path.join(pu.RENDERS, "stage_chodem", "stage_chodem_wide.png"))]
    cam.data.lens = 35
    cam.location = (-4.5, -8.0, 1.8)
    cam.rotation_euler = (math.radians(84), 0, 0)
    out.append(pu.render_to(os.path.join(pu.RENDERS, "stage_chodem", "stage_chodem_close.png")))
    # góc camera game đối kháng: nhìn ngang, khóa trục, 16:9, bao 2 nhân vật (cách nhau 8 m)
    s.render.resolution_x, s.render.resolution_y = 1280, 720
    cam.data.type = "PERSP"
    cam.data.lens = 40
    cam.location = (0, -13.0, 1.9)
    cam.rotation_euler = (math.radians(87), 0, 0)
    out.append(pu.render_to(os.path.join(pu.RENDERS, "stage_chodem", "stage_chodem_gamecam_persp.png")))
    cam.data.type = "ORTHO"
    cam.data.ortho_scale = 13
    cam.location = (0, -30, 2.6)
    cam.rotation_euler = (math.radians(90), 0, 0)
    out.append(pu.render_to(os.path.join(pu.RENDERS, "stage_chodem", "stage_chodem_gamecam_ortho.png")))
    return roundtrip, out


def main():
    root, kiosks = build_stage()
    os.makedirs(OUT, exist_ok=True)
    # quầy hàng gốc đặt ngoài sân, không vào FBX blockout (blockout dùng instance)
    for k in kiosks.values():
        k.location = (0, 20, 0)
    report["kiosk_fbx"] = export_kiosks(kiosks)
    report["blockout_fbx"] = pu.export_fbx(root, os.path.join(OUT, "stage_chodem_blockout.fbx"))
    pu.save_blend(os.path.join(pu.ART_SRC, "Stages", "stage_chodem_blockout.blend"))
    for k in kiosks.values():
        k.hide_render = True
    report["roundtrip"], report["renders"] = preview(root)
    print("RESULT", report)


main()
