"""
prop_thung_phi.py – Thùng phi (Asset Bible mục 5)

  Cỡ Lớn · Ø 0.58 x 0.88 m · ≤ 800 tam giác · ô "xanh_khang" + gân/viền "kim_loai"
  3 đường gân ngang. Pivot = tâm khối (cầm hai tay trên đầu) → không xoay.
  thung_phi_col (trụ 8 cạnh) · thung_phi_dented (bản móp, đổi mesh khi trúng lần 1) · Empty thung_phi_grip
  GDD: Lớn / chỉ ném / ném 110 / bền 2 / Kim loại

Chạy: Blender -b --factory-startup -P ArtSource/Scripts/prop_thung_phi.py
"""
import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import prop_utils as pu

R = 0.29
H = 0.88
RIBS = (0.22, 0.44, 0.66)
RIB_OUT = 0.012
SIDES = 16
B, K = "xanh_khang", "kim_loai"


def profile():
    p = [(R - 0.015, 0.0, None), (R, 0.015, K), (R, 0.03, K)]
    for zc in RIBS:
        p += [(R, zc - 0.02, B), (R + RIB_OUT, zc - 0.012, K), (R + RIB_OUT, zc + 0.012, K), (R, zc + 0.02, K)]
    p += [(R, H - 0.03, B), (R, H - 0.015, K), (R - 0.015, H, K), (R - 0.03, H - 0.008, K)]
    return p


def build(coll):
    mb = pu.MeshBuilder()
    pr = profile()
    mb.lathe([(r, z) for r, z, _ in pr], SIDES, [c for _, _, c in pr[1:]], cap_bottom=K, cap_top=B)
    # nút vặn trên nắp
    mb.cylinder((0.16, 0, H - 0.008), (0.16, 0, H + 0.006), 0.03, 8, K)
    return mb.to_object("thung_phi", coll)


def main():
    pu.open_template()
    coll = pu.get_collection("Asset")
    vis = build(coll)
    col = pu.col_cylinder("thung_phi_col", R + RIB_OUT, 0, H, 8, coll)
    dented = pu.dent(vis, "thung_phi_dented", center=(R, -0.05, 0.50), radius=0.34, depth=0.14, collection=coll)
    # móp thêm mép trên
    d2 = pu.dent(dented, "_tmp", center=(-0.2, 0.2, H), radius=0.22, depth=0.05, collection=coll)
    dented.data, old = d2.data, dented.data
    import bpy
    bpy.data.objects.remove(d2)
    bpy.data.meshes.remove(old)
    dented.data.name = "thung_phi_dented"
    grip = (0, 0, H / 2)      # tâm khối
    root, rep = pu.assemble_prop(
        "thung_phi", vis, [col], grip, head=None, variants=[dented], size="lon",
        meta={"weapon_type": "none", "hit_dmg": 0, "throw_dmg": 110, "durability": 2, "traits": "metal"})
    dented.hide_set(True)
    fbx = pu.export_fbx(root, os.path.join(pu.UNITY_ART, "Props", "prop_thung_phi.fbx"))
    pu.save_blend(os.path.join(pu.ART_SRC, "Props", "prop_thung_phi.blend"))
    rep["renders"] = pu.render_prop_sheet(root, "thung_phi", grip)
    # render bản móp thay cho ảnh "broken"
    cam = pu.setup_render()
    for o in bpy.context.scene.objects:
        o.hide_render = True
    dented.hide_render = False
    root.location = grip
    pu.frame_camera(cam, [dented], pu.VIEWS["34"])
    rep["renders"].append(pu.render_to(os.path.join(pu.RENDERS, "thung_phi", "thung_phi_broken.png")))
    rep["fbx"] = fbx
    print("RESULT", rep)


main()
