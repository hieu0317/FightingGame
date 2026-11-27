"""
char_proto.py – Nhân vật tạm cho bản thử (GDD mục 1: "Nhân vật để cuối, prototype dùng capsule")

  char_proto_p1 (Đỏ Long) và char_proto_p2 (Xanh Khang): capsule cao 1.8 m, bán kính 0.3 m.
  Kính mặt nhìn -Y để thấy hướng mặt; pivot giữa hai bàn chân.
  Empty hand_r_socket / hand_l_socket: điểm gắn _grip của prop (thay xương tay khi chưa có rig Mixamo).

Chạy: Blender -b --factory-startup -P ArtSource/Scripts/char_proto.py
"""
import sys, os, math
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bpy
import prop_utils as pu

H, R, SIDES = 1.8, 0.30, 12


def build(name, cell, coll):
    mb = pu.MeshBuilder()
    prof = []
    for i in range(4):                           # bán cầu dưới
        a = math.radians(-90 + 22.5 * (i + 1))
        prof.append((R * math.cos(a), R + R * math.sin(a)))
    for i in range(4):                           # bán cầu trên
        a = math.radians(22.5 * i)
        prof.append((R * math.cos(a), H - R + R * math.sin(a)))
    # nắp phẳng của 2 bán cầu không chạm z=0 / z=H → kéo giãn để chân đúng ở 0 và đỉnh đúng 1.8 m
    z0, z1 = prof[0][1], prof[-1][1]
    prof = [(r, (z - z0) * H / (z1 - z0)) for r, z in prof]
    mb.lathe(prof, SIDES, [cell], cap_bottom=cell, cap_top=cell)
    mb.box((0, -R + 0.02, 1.55), (0.36, 0.10, 0.10), "dem")         # kính mặt (-Y)
    mb.box((0, -R + 0.01, 1.05), (0.30, 0.06, 0.06), "trang")       # đai
    obj = mb.to_object(name, coll)
    root = bpy.data.objects.new(f"{name}_root", None)
    root.empty_display_type = "PLAIN_AXES"
    coll.objects.link(root)
    obj.parent = root
    for side, sx in (("r", -1), ("l", 1)):          # mặt nhìn -Y → tay phải nằm phía -X
        e = bpy.data.objects.new(f"{name}_hand_{side}_socket", None)
        e.empty_display_type = "ARROWS"
        e.empty_display_size = 0.12
        e.location = (sx * (R + 0.08), -0.12, 1.0)
        e.rotation_euler = (math.radians(-90), 0, 0)   # +Z của socket hướng ra trước (-Y) như cánh tay đưa ra
        coll.objects.link(e)
        e.parent = root
    root["hp"] = 1000
    return root, obj


def main():
    pu.open_template()
    coll = pu.get_collection("Asset")
    out = {}
    roots = []
    for name, cell in (("char_proto_p1", "do_long"), ("char_proto_p2", "xanh_khang")):
        root, obj = build(name, cell, coll)
        roots.append((root, obj))
        out[name] = {"tris": pu.tri_count(obj),
                     "fbx": pu.export_fbx(root, os.path.join(pu.UNITY_ART, "Characters", f"{name}.fbx"))}
    pu.save_blend(os.path.join(pu.ART_SRC, "Characters", "char_proto.blend"))
    # render hai capsule cạnh người mẫu 1.8 m
    roots[0][0].location = (-0.9, 0, 0)
    roots[1][0].location = (0.9, 0, 0)
    cam = pu.setup_render(res=640)
    vis = [o for o in bpy.context.scene.objects if o.type == "MESH" and not o.name.endswith("_col")]
    pu.frame_camera(cam, vis, pu.VIEWS["34"], margin=1.15)
    out["render"] = pu.render_to(os.path.join(pu.RENDERS, "char_proto", "char_proto_34.png"))
    print("RESULT", out)


main()
