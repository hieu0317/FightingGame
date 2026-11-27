"""
make_template.py – Giai đoạn 1: palette.png + template_asset.blend

  * palette.png 128x128, 16 ô 32 px (Asset Bible mục 2) → UnityProject/Assets/Art/Textures/
  * template_asset.blend: Metric, scale 1.0, vật liệu M_Palette, người mẫu cao 1.8 m (A-pose,
    mặt nhìn -Y) trong collection "Reference" – không bao giờ export.

Chạy: Blender -b --factory-startup -P ArtSource/Scripts/make_template.py
"""
import sys, os, math
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bpy
from mathutils import Euler
import prop_utils as pu


def make_ref_human(height=1.8, collection=None):
    """Người khối 1.8 m làm thước đo tỷ lệ. Pivot giữa hai bàn chân, mặt nhìn -Y."""
    s = height / 1.8
    mb = pu.MeshBuilder()
    g = "be_tong"
    for side in (-1, 1):
        x = 0.1 * side * s
        mb.box((x, -0.03 * s, 0.04 * s), (0.11 * s, 0.25 * s, 0.08 * s), "dem")          # bàn chân
        mb.frustum((x, 0, 0.08 * s), (x, 0, 0.48 * s), 0.11 * s, 0.13 * s, g)             # cẳng
        mb.frustum((x, 0, 0.48 * s), (x, 0, 0.90 * s), 0.13 * s, 0.17 * s, g)             # đùi
        # tay A-pose 45°
        sh = (0.25 * side * s, 0, 1.40 * s)
        ux, uz = 0.30 * math.sin(math.radians(45)) * s, 0.30 * math.cos(math.radians(45)) * s
        rot = Euler((0, -math.radians(45) * side, 0))   # trục dài của khối song song hướng tay
        mb.box((sh[0] + side * ux / 2, 0, sh[2] - uz / 2), (0.10 * s, 0.11 * s, 0.30 * s), g, rot)
        el = (sh[0] + side * ux, 0, sh[2] - uz)
        mb.box((el[0] + side * ux * 0.45, 0, el[2] - uz * 0.45), (0.09 * s, 0.10 * s, 0.27 * s), g, rot)
        hd = (el[0] + side * ux * 1.0, 0, el[2] - uz * 1.0)
        mb.box((hd[0] + side * 0.03 * s, 0, hd[2] - 0.04 * s), (0.10 * s, 0.10 * s, 0.11 * s), "da_sang", rot)
    mb.box((0, 0, 0.95 * s), (0.34 * s, 0.20 * s, 0.16 * s), g)                             # hông
    mb.frustum((0, 0, 1.00 * s), (0, 0, 1.44 * s), 0.32 * s, 0.44 * s, g, 0.20 * s, 0.23 * s)  # thân
    mb.frustum((0, 0, 1.44 * s), (0, 0, 1.52 * s), 0.11 * s, 0.10 * s, "da_sang")           # cổ
    mb.box((0, 0, 1.665 * s), (0.23 * s, 0.25 * s, 0.27 * s), "da_sang")                    # đầu → đỉnh 1.80
    mb.box((0, -0.126 * s, 1.68 * s), (0.16 * s, 0.01 * s, 0.04 * s), "dem")                # mắt/mặt (-Y)
    obj = mb.to_object("ref_human_1m80", collection)
    obj.hide_select = True
    return obj


def main():
    pu.clear_scene()
    for img in list(bpy.data.images):
        bpy.data.images.remove(img)
    for m in list(bpy.data.materials):
        bpy.data.materials.remove(m)
    pu.make_palette_png()
    pu.palette_material()
    ref = pu.get_collection("Reference")
    human = make_ref_human(1.8, ref)
    pu.get_collection("Asset")
    bpy.context.view_layer.update()
    mn, mx = pu.bounds([human])
    pu.save_blend(pu.TEMPLATE_BLEND)
    cam = pu.setup_render(res=512)
    pu.frame_camera(cam, [human], pu.VIEWS["front"])
    pu.render_to(os.path.join(pu.RENDERS, "template", "ref_human_front.png"))
    pu.frame_camera(cam, [human], pu.VIEWS["side"])
    pu.render_to(os.path.join(pu.RENDERS, "template", "ref_human_side.png"))
    print("RESULT template", {"height": round(mx.z - mn.z, 3), "tris": pu.tri_count(human),
                              "palette": pu.PALETTE_PNG, "blend": pu.TEMPLATE_BLEND})


main()
