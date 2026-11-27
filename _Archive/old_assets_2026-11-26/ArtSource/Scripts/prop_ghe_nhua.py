"""
prop_ghe_nhua.py – Ghế nhựa đỏ kiểu quán vỉa hè (Asset Bible mục 5)

  Cỡ Vừa · 0.40 x 0.40 x 0.45 m · ≤ 500 tam giác · ô palette "nhua_do"
  Mặt vuông bo góc, lỗ tròn giữa mặt, 4 chân vát ra ngoài, giằng chân.
  Pivot = mép trước mặt ghế (điểm cầm); đầu đánh (thân ghế) hướng +Z.
  ghe_nhua_col (1 hộp bao) · ghe_nhua_broken (6 mảnh) · Empty ghe_nhua_grip
  GDD: Vừa / Cùn / đánh 45 / ném 60 / bền 4

Chạy: Blender -b --factory-startup -P ArtSource/Scripts/prop_ghe_nhua.py
"""
import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import prop_utils as pu

# ---- tham số (sửa số rồi chạy lại)
W = 0.40           # cạnh mặt ghế
H = 0.45           # chiều cao
SEAT_T = 0.03      # độ dày mặt
SKIRT = 0.06       # viền váy dưới mặt
HOLE_R = 0.045     # lỗ tròn giữa
LEG_FOOT = 0.205   # nửa khoảng cách chân dưới sàn (vát ra)
LEG_TOP = 0.150    # nửa khoảng cách chân ở dưới mặt
SEG = 24           # số đoạn vòng mặt ghế
C = "nhua_do"


def build(coll):
    mb = pu.MeshBuilder()
    r = W / 2
    z_top, z_bot = H, H - SEAT_T
    z_skirt = H - SKIRT
    sq = lambda s, z: pu.superellipse_ring(r * s, r * s, 5, SEG, z)
    ci = lambda z: pu.circle_ring(HOLE_R, SEG, z)
    # mặt cắt khép kín, quay quanh trục: lỗ trên → mép ngoài trên → đáy váy → trong váy → dưới mặt → lỗ dưới
    rings = [ci(z_top), sq(1.0, z_top), sq(1.0, z_skirt), sq(0.92, z_skirt), sq(0.92, z_bot), ci(z_bot)]
    mb.loft(rings, [C], closed=True)
    # 4 chân vát
    zt = z_bot
    for sx in (-1, 1):
        for sy in (-1, 1):
            mb.frustum((sx * LEG_FOOT, sy * LEG_FOOT, 0), (sx * LEG_TOP, sy * LEG_TOP, zt),
                       0.050, 0.070, C)
    # giằng chân ở cao 0.10 m
    zb = 0.10
    t = zb / zt
    p = LEG_FOOT + (LEG_TOP - LEG_FOOT) * t
    for (cx, cy, sx, sy) in ((0, -p, 2 * p, 0.018), (0, p, 2 * p, 0.018),
                             (-p, 0, 0.018, 2 * p), (p, 0, 0.018, 2 * p)):
        mb.box((cx, cy, zb), (sx, sy, 0.022), C)
    return mb.to_object("ghe_nhua", coll)


def main():
    pu.open_template()
    coll = pu.get_collection("Asset")
    vis = build(coll)
    col = pu.col_box("ghe_nhua_col", (-LEG_FOOT - 0.02, -LEG_FOOT - 0.02, 0),
                     (LEG_FOOT + 0.02, LEG_FOOT + 0.02, H), coll)
    grip = (0, -W / 2, H)              # mép trước mặt ghế
    head = (0, 0.05, H * 0.35)         # khối thân ghế (phía chân) là phần đánh trúng
    root, rep = pu.assemble_prop(
        "ghe_nhua", vis, [col], grip, head, broken_counts=(3, 1, 2), broken_cap=C, size="vua",
        meta={"weapon_type": "blunt", "hit_dmg": 45, "throw_dmg": 60, "durability": 4, "traits": ""},
        seed=11, tilt=0.2)
    fbx = pu.export_fbx(root, os.path.join(pu.UNITY_ART, "Props", "prop_ghe_nhua.fbx"))
    pu.save_blend(os.path.join(pu.ART_SRC, "Props", "prop_ghe_nhua.blend"))
    rep["renders"] = pu.render_prop_sheet(root, "ghe_nhua", grip)
    rep["fbx"] = fbx
    print("RESULT", rep)


main()
