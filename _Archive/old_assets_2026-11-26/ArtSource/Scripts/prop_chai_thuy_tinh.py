"""
prop_chai_thuy_tinh.py – Chai thủy tinh (Asset Bible mục 5)

  Cỡ Nhỏ · Ø 0.07 x 0.25 m · ≤ 200 tam giác · ô palette "neon_xanh" (độ trong làm ở Unity)
  Pivot = cổ chai (điểm cầm); đầu đánh (thân chai) hướng +Z.
  chai_thuy_tinh_col (trụ 6 cạnh) · chai_thuy_tinh_broken (5 mảnh) · Empty chai_thuy_tinh_grip
  GDD: Nhỏ / chỉ ném / ném 70 / bền 1 / Dễ vỡ

Chạy: Blender -b --factory-startup -P ArtSource/Scripts/prop_chai_thuy_tinh.py
"""
import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import prop_utils as pu

R = 0.035        # bán kính thân
H = 0.25         # cao
SIDES = 10
G, K = "neon_xanh", "kim_loai"

# (bán kính, z, ô màu của dải đi tới điểm này)
PROFILE = [
    (0.033, 0.000, None),
    (R,     0.006, G),
    (R,     0.150, G),
    (0.031, 0.170, G),
    (0.016, 0.195, G),
    (0.014, 0.225, G),
    (0.0165, 0.232, K),   # nắp kim loại
    (0.0165, 0.245, K),
    (0.012, H,     K),
]


def build(coll):
    mb = pu.MeshBuilder()
    mb.lathe([(r, z) for r, z, _ in PROFILE], SIDES, [c for _, _, c in PROFILE[1:]],
             cap_bottom=G, cap_top=K)
    return mb.to_object("chai_thuy_tinh", coll)


def main():
    pu.open_template()
    coll = pu.get_collection("Asset")
    vis = build(coll)
    col = pu.col_cylinder("chai_thuy_tinh_col", R, 0, H, 6, coll)
    grip = (0, 0, 0.21)       # cổ chai
    head = (0, 0, 0.07)       # thân chai
    root, rep = pu.assemble_prop(
        "chai_thuy_tinh", vis, [col], grip, head, broken_counts=(1, 1, 5), broken_cap=G, size="nho",
        meta={"weapon_type": "none", "hit_dmg": 0, "throw_dmg": 70, "durability": 1, "traits": "fragile"},
        seed=5, tilt=0.35)
    fbx = pu.export_fbx(root, os.path.join(pu.UNITY_ART, "Props", "prop_chai_thuy_tinh.fbx"))
    pu.save_blend(os.path.join(pu.ART_SRC, "Props", "prop_chai_thuy_tinh.blend"))
    rep["renders"] = pu.render_prop_sheet(root, "chai_thuy_tinh", grip)
    rep["fbx"] = fbx
    print("RESULT", rep)


main()
