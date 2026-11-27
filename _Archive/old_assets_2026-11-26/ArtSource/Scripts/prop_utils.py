"""
prop_utils.py – thư viện dùng chung cho mọi asset của "Loạn Đấu Phố Đêm".

Chạy kèm Blender (headless):
    /Applications/Blender.app/Contents/MacOS/Blender -b --factory-startup -P ArtSource/Scripts/<script>.py

Quy chuẩn (Asset Bible mục 1, 2, 5):
  * 1 BU = 1 m, flat shading, không normal map
  * Một vật liệu M_Palette dùng palette.png (4x4 ô, 32 px/ô)
  * Pivot = điểm cầm, đầu đánh hướng +Z (Blender)
  * Mỗi prop: mesh hiển thị, <ten>_col, <ten>_broken (nếu vỡ), Empty <ten>_grip
  * Export FBX: Forward -Z, Up Y, Apply Transform, FBX Scale All
"""
import bpy
import bmesh
import os
import math
import random
import itertools
from mathutils import Vector, Matrix, Quaternion, Euler

# --------------------------------------------------------------------------- paths
SCRIPTS_DIR = os.path.dirname(os.path.abspath(__file__))
ART_SRC = os.path.dirname(SCRIPTS_DIR)
PROJECT = os.path.dirname(ART_SRC)
UNITY_ART = os.path.join(PROJECT, "UnityProject", "Assets", "Art")
PALETTE_PNG = os.path.join(UNITY_ART, "Textures", "palette.png")
TEMPLATE_BLEND = os.path.join(ART_SRC, "template_asset.blend")
RENDERS = os.path.join(ART_SRC, "Renders")

# --------------------------------------------------------------------------- palette
# 4x4, trái → phải, trên → dưới (Asset Bible mục 2)
PALETTE = [
    ("do_long", "D7263D"), ("vang_bao", "F4B400"), ("tim_linh", "7B2CBF"), ("cam_mai", "F77F00"),
    ("xanh_khang", "1E88E5"), ("xanh_batu", "43A047"), ("da_sang", "F1C9A5"), ("da_tram", "C68B59"),
    ("nhua_do", "E53935"), ("kim_loai", "8D99AE"), ("go", "8B5E3C"), ("be_tong", "6C757D"),
    ("neon_hong", "FF3CAC"), ("neon_xanh", "00F5D4"), ("dem", "1B1B2F"), ("trang", "F8F9FA"),
]
CELL = {n: i for i, (n, _) in enumerate(PALETTE)}

TRI_BUDGET = {"nho": 200, "vua": 500, "lon": 800, "sieu_lon": 1500,
              "nhan_vat": 5000, "stage": 1000}


def hex_rgb(h):
    return tuple(int(h[i:i + 2], 16) / 255.0 for i in (0, 2, 4))


def cell_uv(name):
    """UV tâm ô màu (gốc UV của Blender ở góc dưới-trái)."""
    r, c = divmod(CELL[name], 4)
    return Vector(((c + 0.5) / 4.0, 1.0 - (r + 0.5) / 4.0))


def make_palette_png(path=PALETTE_PNG, size=128):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    cell = size // 4
    img = bpy.data.images.new("_palette_gen", size, size, alpha=True)
    px = [0.0] * (size * size * 4)
    for y in range(size):              # y = 0 là hàng dưới cùng
        row = 3 - y // cell
        for x in range(size):
            rgb = hex_rgb(PALETTE[row * 4 + x // cell][1])
            k = (y * size + x) * 4
            px[k:k + 4] = [rgb[0], rgb[1], rgb[2], 1.0]
    img.pixels.foreach_set(px)
    img.filepath_raw = path
    img.file_format = "PNG"
    img.save()
    bpy.data.images.remove(img)
    return path


def palette_material():
    mat = bpy.data.materials.get("M_Palette")
    if mat:
        return mat
    if not os.path.exists(PALETTE_PNG):
        make_palette_png()
    img = bpy.data.images.load(PALETTE_PNG, check_existing=True)
    img.name = "palette.png"
    mat = bpy.data.materials.new("M_Palette")
    try:
        mat.use_nodes = True
    except Exception:
        pass
    nt = mat.node_tree
    bsdf = next((n for n in nt.nodes if n.type == "BSDF_PRINCIPLED"), None)
    out = next((n for n in nt.nodes if n.type == "OUTPUT_MATERIAL"), None)
    if out is None:
        out = nt.nodes.new("ShaderNodeOutputMaterial")
    if bsdf is None:
        bsdf = nt.nodes.new("ShaderNodeBsdfPrincipled")
        nt.links.new(bsdf.outputs[0], out.inputs["Surface"])
    tex = nt.nodes.new("ShaderNodeTexImage")
    tex.image = img
    tex.interpolation = "Closest"
    tex.location = (-400, 200)
    nt.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
    bsdf.inputs["Roughness"].default_value = 0.8
    nt.nodes.active = tex
    return mat


# --------------------------------------------------------------------------- scene
def clear_scene():
    for coll in (bpy.data.objects, bpy.data.meshes, bpy.data.cameras, bpy.data.lights):
        for d in list(coll):
            coll.remove(d)
    s = bpy.context.scene
    s.unit_settings.system = "METRIC"
    s.unit_settings.scale_length = 1.0
    s.unit_settings.length_unit = "METERS"
    return s


def open_template():
    """Mở template_asset.blend (Metric, M_Palette, người mẫu 1.8 m)."""
    if os.path.exists(TEMPLATE_BLEND):
        bpy.ops.wm.open_mainfile(filepath=TEMPLATE_BLEND)
    else:
        clear_scene()
        palette_material()
    return bpy.context.scene


def get_collection(name, parent=None):
    c = bpy.data.collections.get(name)
    if c is None:
        c = bpy.data.collections.new(name)
        (parent or bpy.context.scene.collection).children.link(c)
    return c


# --------------------------------------------------------------------------- mesh builder
class MeshBuilder:
    """Gom nhiều khối vào một bmesh; mỗi mặt được UV vào một ô palette."""

    def __init__(self):
        self.bm = bmesh.new()
        self.uv = self.bm.loops.layers.uv.new("UVMap")

    # -- helpers
    def paint(self, faces, cell):
        uv = cell_uv(cell)
        for f in faces:
            for l in f.loops:
                l[self.uv].uv = uv
        return faces

    def _fix_normals(self, faces):
        faces = [f for f in faces if f.is_valid]
        if faces:
            bmesh.ops.recalc_face_normals(self.bm, faces=faces)

    def v(self, co):
        return self.bm.verts.new(Vector(co))

    # -- primitives
    def box(self, center, size, cell, rot=None):
        m = Matrix.Translation(Vector(center))
        if rot is not None:
            m = m @ (rot.to_matrix().to_4x4() if hasattr(rot, "to_matrix") else rot)
        m = m @ Matrix.Diagonal((size[0], size[1], size[2], 1.0))
        res = bmesh.ops.create_cube(self.bm, size=1.0, matrix=m)
        faces = list({f for v in res["verts"] for f in v.link_faces})
        self.paint(faces, cell)
        self._fix_normals(faces)
        return faces

    def frustum(self, pb, pt, wb, wt, cell, db=None, dt=None):
        """Khối hộp vát: đáy tâm pb (wb x db), đỉnh tâm pt (wt x dt), mặt cắt song song XY."""
        db = wb if db is None else db
        dt = wt if dt is None else dt

        def sq(p, w, d):
            p = Vector(p)
            return [self.v(p + Vector(o)) for o in
                    ((-w / 2, -d / 2, 0), (w / 2, -d / 2, 0), (w / 2, d / 2, 0), (-w / 2, d / 2, 0))]
        b, t = sq(pb, wb, db), sq(pt, wt, dt)
        faces = [self.bm.faces.new(list(reversed(b))), self.bm.faces.new(t)]
        for i in range(4):
            j = (i + 1) % 4
            faces.append(self.bm.faces.new((b[i], b[j], t[j], t[i])))
        self.paint(faces, cell)
        self._fix_normals(faces)
        return faces

    def loft(self, rings, band_cells, cap_start=None, cap_end=None, closed=False):
        """rings: danh sách vòng điểm (cùng số điểm, CCW nhìn từ trên).
        band_cells[i] là ô màu của dải nối ring i -> i+1."""
        vr = [[self.v(c) for c in ring] for ring in rings]
        n = len(rings[0])
        faces = []
        pairs = list(zip(range(len(vr) - 1), range(1, len(vr))))
        if closed:
            pairs.append((len(vr) - 1, 0))
        for k, (a, b) in enumerate(pairs):
            band = []
            for i in range(n):
                j = (i + 1) % n
                band.append(self.bm.faces.new((vr[a][i], vr[a][j], vr[b][j], vr[b][i])))
            self.paint(band, band_cells[k % len(band_cells)])
            faces += band
        if cap_start:
            faces += self.paint([self.bm.faces.new(list(reversed(vr[0])))], cap_start)
        if cap_end:
            faces += self.paint([self.bm.faces.new(vr[-1])], cap_end)
        self._fix_normals(faces)
        return faces

    def lathe(self, profile, sides, band_cells, cap_bottom=None, cap_top=None,
              center=(0, 0, 0), phase=0.0):
        """Tiện tròn: profile = [(bán kính, z), ...] từ dưới lên."""
        c = Vector(center)
        rings = []
        for r, z in profile:
            ring = []
            for i in range(sides):
                a = phase + 2 * math.pi * i / sides
                ring.append(c + Vector((r * math.cos(a), r * math.sin(a), z)))
            rings.append(ring)
        return self.loft(rings, band_cells, cap_bottom, cap_top)

    def cylinder(self, p0, p1, radius, sides, cell, cap=True):
        """Trụ nối 2 điểm bất kỳ."""
        p0, p1 = Vector(p0), Vector(p1)
        axis = p1 - p0
        q = Vector((0, 0, 1)).rotation_difference(axis.normalized())
        rings = []
        for p in (p0, p1):
            rings.append([p + q @ Vector((radius * math.cos(2 * math.pi * i / sides),
                                           radius * math.sin(2 * math.pi * i / sides), 0))
                          for i in range(sides)])
        return self.loft(rings, [cell], cell if cap else None, cell if cap else None)

    # -- output
    def to_object(self, name, collection=None, material=True):
        me = bpy.data.meshes.new(name)
        self.bm.to_mesh(me)
        self.bm.free()
        for p in me.polygons:
            p.use_smooth = False
        if material:
            me.materials.append(palette_material())
        obj = bpy.data.objects.new(name, me)
        (collection or bpy.context.scene.collection).objects.link(obj)
        return obj


def superellipse_ring(rx, ry, n, count, z, center=(0, 0), phase=None):
    """Vòng hình vuông bo góc (siêu elip bậc n)."""
    phase = math.pi / count if phase is None else phase
    pts = []
    for i in range(count):
        a = phase + 2 * math.pi * i / count
        c, s = math.cos(a), math.sin(a)
        x = math.copysign(abs(c) ** (2.0 / n), c) * rx
        y = math.copysign(abs(s) ** (2.0 / n), s) * ry
        pts.append(Vector((center[0] + x, center[1] + y, z)))
    return pts


def circle_ring(r, count, z, center=(0, 0), phase=None):
    phase = math.pi / count if phase is None else phase
    return [Vector((center[0] + r * math.cos(phase + 2 * math.pi * i / count),
                    center[1] + r * math.sin(phase + 2 * math.pi * i / count), z))
            for i in range(count)]


def tri_count(obj):
    return sum(len(p.vertices) - 2 for p in obj.data.polygons)


def bake(obj, M):
    obj.data.transform(M)
    obj.data.update()


# --------------------------------------------------------------------------- collider
def col_box(name, bmin, bmax, collection=None):
    mb = MeshBuilder()
    bmin, bmax = Vector(bmin), Vector(bmax)
    mb.box((bmin + bmax) / 2, bmax - bmin, "trang")
    obj = mb.to_object(name, collection, material=False)
    obj.display_type = "WIRE"
    obj.hide_render = True
    return obj


def col_cylinder(name, radius, z0, z1, sides=8, collection=None, center=(0, 0)):
    mb = MeshBuilder()
    mb.lathe([(radius, z0), (radius, z1)], sides, ["trang"], "trang", "trang",
             center=(center[0], center[1], 0))
    obj = mb.to_object(name, collection, material=False)
    obj.display_type = "WIRE"
    obj.hide_render = True
    return obj


def bounds(objs, local=False):
    pts = []
    for o in objs:
        m = Matrix.Identity(4) if local else o.matrix_world
        pts += [m @ v.co for v in o.data.vertices]
    mn = Vector((min(p.x for p in pts), min(p.y for p in pts), min(p.z for p in pts)))
    mx = Vector((max(p.x for p in pts), max(p.y for p in pts), max(p.z for p in pts)))
    return mn, mx


# --------------------------------------------------------------------------- fracture
def _planes(axis, lo, hi, n, rng, tilt):
    """n-1 mặt cắt chia đoạn [lo,hi] theo trục axis, lệch ngẫu nhiên nhẹ."""
    planes = []
    for k in range(1, n):
        t = k / n + rng.uniform(-0.12, 0.12) / n
        co = Vector((0, 0, 0))
        co[axis] = lo + (hi - lo) * t
        no = Vector((0, 0, 0))
        no[axis] = 1.0
        if tilt:
            no += Vector([rng.uniform(-tilt, tilt) if i != axis else 0 for i in range(3)])
            no.normalize()
        planes.append((co, no))
    return planes


def fracture(src, prefix, counts, cap_cell, seed=7, tilt=0.25, collection=None):
    """Cắt mesh thành các mảnh theo lưới counts=(nx,ny,nz) với mặt cắt lệch/nghiêng ngẫu nhiên.
    Mỗi mảnh là object riêng, giữ nguyên pivot gốc của src."""
    rng = random.Random(seed)
    mn, mx = bounds([src], local=True)
    cx = mn + (mx - mn) * 0.5
    axis_planes = []
    for ax in range(3):
        pl = _planes(ax, mn[ax], mx[ax], counts[ax], rng, tilt)
        # dịch tâm mặt cắt về giữa bbox trên 2 trục còn lại để độ nghiêng đều
        pl = [(Vector([co[i] if i == ax else cx[i] for i in range(3)]), no) for co, no in pl]
        axis_planes.append(pl)
    pieces = []
    k = 0
    for idx in itertools.product(*[range(c) for c in counts]):
        bm = bmesh.new()
        bm.from_mesh(src.data)
        uv = bm.loops.layers.uv.active
        orig = bm.faces.layers.int.new("orig")
        for f in bm.faces:
            f[orig] = 1
        for ax in range(3):
            i = idx[ax]
            cuts = []
            if i > 0:
                cuts.append((axis_planes[ax][i - 1], "lower"))
            if i < counts[ax] - 1:
                cuts.append((axis_planes[ax][i], "upper"))
            for (co, no), side in cuts:
                geom = bm.verts[:] + bm.edges[:] + bm.faces[:]
                res = bmesh.ops.bisect_plane(bm, geom=geom, dist=1e-5, plane_co=co, plane_no=no,
                                             clear_inner=(side == "lower"),
                                             clear_outer=(side == "upper"))
                edges = [e for e in res["geom_cut"] if isinstance(e, bmesh.types.BMEdge) and e.is_valid]
                if edges:
                    bmesh.ops.holes_fill(bm, edges=edges, sides=0)
        if len(bm.faces) < 4:
            bm.free()
            continue
        caps = [f for f in bm.faces if f[orig] == 0]
        if caps:
            res = bmesh.ops.triangulate(bm, faces=caps, quad_method="BEAUTY", ngon_method="BEAUTY")
            caps = res["faces"]
            u = cell_uv(cap_cell)
            for f in caps:
                for l in f.loops:
                    l[uv].uv = u
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
        k += 1
        me = bpy.data.meshes.new(f"{prefix}_broken_{k:02d}")
        bm.to_mesh(me)
        bm.free()
        for p in me.polygons:
            p.use_smooth = False
        me.materials.append(palette_material())
        ob = bpy.data.objects.new(me.name, me)
        (collection or bpy.context.scene.collection).objects.link(ob)
        pieces.append(ob)
    return pieces


def dent(src, name, center, radius, depth, collection=None):
    """Tạo bản móp: đẩy đỉnh trong bán kính vào trong theo hướng về trục Z."""
    me = src.data.copy()
    me.name = name
    c = Vector(center)
    for v in me.vertices:
        d = (v.co - c).length
        if d < radius:
            w = (1 - d / radius) ** 1.5
            inward = Vector((-v.co.x, -v.co.y, 0))
            if inward.length > 1e-6:
                v.co += inward.normalized() * depth * w
    me.update()
    ob = bpy.data.objects.new(name, me)
    (collection or bpy.context.scene.collection).objects.link(ob)
    return ob


# --------------------------------------------------------------------------- assembly
def _empty(name, display, size, collection):
    e = bpy.data.objects.new(name, None)
    e.empty_display_type = display
    e.empty_display_size = size
    collection.objects.link(e)
    return e


def assemble_prop(key, visual, colliders, grip, head=None, broken_counts=None, broken_cap=None,
                  variants=(), size="vua", meta=None, seed=7, tilt=0.25):
    """Đóng gói một prop theo quy chuẩn Asset Bible mục 5.

    key       : tên không dấu, vd 'ghe_nhua' → root 'prop_ghe_nhua'
    visual    : object mesh dựng ở tư thế tự nhiên (đứng trên sàn)
    colliders : list object _col dựng cùng hệ toạ độ tự nhiên
    grip      : điểm cầm (toạ độ tự nhiên) → sẽ thành pivot
    head      : điểm 'đầu đánh'; prop được xoay để grip→head trùng +Z. None = không xoay.
    """
    coll = visual.users_collection[0]
    pieces = []
    if broken_counts:
        pieces = fracture(visual, key, broken_counts, broken_cap, seed=seed, tilt=tilt, collection=coll)

    grip = Vector(grip)
    q = Quaternion()
    if head is not None:
        q = (Vector(head) - grip).normalized().rotation_difference(Vector((0, 0, 1)))
    M = q.to_matrix().to_4x4() @ Matrix.Translation(-grip)
    for o in [visual, *colliders, *pieces, *variants]:
        bake(o, M)

    root = _empty(f"prop_{key}", "PLAIN_AXES", 0.15, coll)
    visual.name = key
    visual.data.name = key
    g = _empty(f"{key}_grip", "ARROWS", 0.12, coll)
    for o in [visual, g, *colliders, *variants]:
        o.parent = root
    if pieces:
        br = _empty(f"{key}_broken", "PLAIN_AXES", 0.05, coll)
        br.parent = root
        for p in pieces:
            p.parent = br
            p.hide_set(True)

    # tư thế nằm trên sàn (rest) = nghịch đảo phép xoay cầm tay, lưu để Unity/spawner dùng
    rest = q.inverted()
    e = rest.to_euler("XYZ")
    root["prop_size"] = size
    root["rest_euler_blender_deg"] = ",".join(f"{math.degrees(a):.1f}" for a in e)
    root["rest_offset_blender"] = ",".join(f"{c:.3f}" for c in grip)
    root["tri_count"] = tri_count(visual)
    for k, v in (meta or {}).items():
        root[k] = v

    report = {
        "root": root.name,
        "tris_visual": tri_count(visual),
        "budget": TRI_BUDGET[size],
        "pieces": len(pieces),
        "pieces_tris": [tri_count(p) for p in pieces],
        "colliders": [c.name for c in colliders],
        "variants": [(v.name, tri_count(v)) for v in variants],
        "rest_euler_deg": root["rest_euler_blender_deg"],
    }
    bpy.context.view_layer.update()
    mn, mx = bounds([visual])
    report["dims_m_asset_space"] = [round(x, 3) for x in (mx - mn)]
    return root, report


def hierarchy(root):
    out = [root]
    for c in root.children:
        out += hierarchy(c)
    return out


# --------------------------------------------------------------------------- export
def _flatten(root):
    """Exporter FBX của Blender (Apply Transform) chỉ bake đúng phép đổi trục cho con trực tiếp của root;
    object sâu hơn bị dư xoay 90° X trong Unity. Tạm đưa mọi object lên làm con trực tiếp của root,
    ghi tên cha gốc vào custom property 'fbx_parent' → ArtAssetPostprocessor dựng lại cây trong Unity."""
    moved = []
    for o in hierarchy(root):
        if o is root or o.parent is root or o.parent is None:
            continue
        mw = o.matrix_world.copy()
        moved.append((o, o.parent, mw))
        o["fbx_parent"] = o.parent.name
    for o, _, mw in moved:
        o.parent = root
        o.matrix_world = mw
    return moved


def _unflatten(moved):
    for o, parent, mw in moved:
        o.parent = parent
        o.matrix_world = mw
        del o["fbx_parent"]


def export_fbx(root, path):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    bpy.context.view_layer.update()
    moved = _flatten(root)
    bpy.context.view_layer.update()
    objs = hierarchy(root)
    for o in bpy.context.view_layer.objects:
        o.select_set(False)
    hidden = [o for o in objs if o.hide_get()]
    for o in objs:
        o.hide_set(False)
        o.select_set(True)
    bpy.context.view_layer.objects.active = root
    bpy.ops.export_scene.fbx(
        filepath=path, use_selection=True, object_types={"EMPTY", "MESH"},
        axis_forward="-Z", axis_up="Y", bake_space_transform=True,
        apply_unit_scale=True, apply_scale_options="FBX_SCALE_ALL", global_scale=1.0,
        mesh_smooth_type="FACE", use_mesh_modifiers=True, add_leaf_bones=False,
        use_custom_props=True, bake_anim=False, path_mode="STRIP")
    _unflatten(moved)
    bpy.context.view_layer.update()
    for o in hidden:
        o.hide_set(True)
    return path


def save_blend(path):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    bpy.context.preferences.filepaths.save_version = 0     # không tạo .blend1
    bpy.ops.wm.save_as_mainfile(filepath=path)
    try:
        bpy.ops.file.make_paths_relative()
        bpy.ops.wm.save_mainfile()
    except Exception:
        pass
    return path


# --------------------------------------------------------------------------- render
VIEWS = {
    "front": Vector((0, -1, 0.0)),
    "side": Vector((1, 0, 0.0)),
    "34": Vector((1.0, -1.25, 0.75)),
}


def setup_render(res=640, bg=(0.11, 0.11, 0.18)):
    s = bpy.context.scene
    s.render.engine = "BLENDER_WORKBENCH"
    s.render.resolution_x = res
    s.render.resolution_y = res
    s.render.resolution_percentage = 100
    s.render.film_transparent = False
    sh = s.display.shading
    sh.light = "STUDIO"
    sh.color_type = "TEXTURE"
    sh.show_object_outline = True
    sh.object_outline_color = (0.0, 0.0, 0.0)
    sh.show_cavity = False
    if s.world is None:
        s.world = bpy.data.worlds.new("World")
    s.world.color = bg
    cam = bpy.data.objects.get("_RenderCam")
    if cam is None:
        cam = bpy.data.objects.new("_RenderCam", bpy.data.cameras.new("_RenderCam"))
        s.collection.objects.link(cam)
    cam.data.type = "ORTHO"
    cam.data.clip_end = 500
    s.camera = cam
    return cam


def frame_camera(cam, objs, direction, margin=1.2, aspect=1.0):
    bpy.context.view_layer.update()
    pts = []
    for o in objs:
        pts += [o.matrix_world @ Vector(c) for c in o.bound_box]
    mn = Vector((min(p.x for p in pts), min(p.y for p in pts), min(p.z for p in pts)))
    mx = Vector((max(p.x for p in pts), max(p.y for p in pts), max(p.z for p in pts)))
    center = (mn + mx) / 2
    d = direction.normalized()
    rot = (-d).to_track_quat("-Z", "Y")
    right, up = rot @ Vector((1, 0, 0)), rot @ Vector((0, 1, 0))
    w = max(abs((p - center).dot(right)) for p in pts) * 2
    h = max(abs((p - center).dot(up)) for p in pts) * 2
    cam.data.ortho_scale = max(w, h * aspect) * margin
    cam.rotation_mode = "QUATERNION"
    cam.rotation_quaternion = rot
    cam.location = center + d * ((mx - mn).length * 3 + 5)


def render_to(path):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    bpy.context.scene.render.filepath = path
    bpy.ops.render.render(write_still=True)
    return path


def render_prop_sheet(root, key, grip_natural, out_dir=RENDERS):
    """Render 3 góc ở tư thế đặt trên sàn + 1 ảnh mảnh vỡ tách rời."""
    cam = setup_render()
    objs = hierarchy(root)
    visual = bpy.data.objects[key]
    pieces = [o for o in objs if "_broken_" in o.name]
    # ẩn mọi thứ khác (ví dụ người mẫu tham chiếu)
    saved = {o.name: o.hide_render for o in bpy.context.scene.objects}
    for o in bpy.context.scene.objects:
        o.hide_render = True
    visual.hide_render = False
    old_rot = root.rotation_mode, root.rotation_euler.copy(), root.location.copy()
    rest = Euler([math.radians(float(a)) for a in root["rest_euler_blender_deg"].split(",")], "XYZ")
    root.rotation_mode = "XYZ"
    root.rotation_euler = rest
    root.location = Vector(grip_natural)
    files = []
    for vname, d in VIEWS.items():
        frame_camera(cam, [visual], d)
        files.append(render_to(os.path.join(out_dir, key, f"{key}_{vname}.png")))
    # tư thế cầm tay (asset space): đầu đánh hướng +Z
    root.rotation_euler = (0, 0, 0)
    root.location = (0, 0, 0)
    frame_camera(cam, [visual], VIEWS["side"])
    files.append(render_to(os.path.join(out_dir, key, f"{key}_inhand.png")))
    if pieces:
        root.rotation_euler = rest
        root.location = Vector(grip_natural)
        visual.hide_render = True
        bpy.context.view_layer.update()
        c = sum((p.matrix_world @ (sum((v.co for v in p.data.vertices), Vector()) / len(p.data.vertices))
                 for p in pieces), Vector()) / len(pieces)
        moved = []
        for p in pieces:
            pc = p.matrix_world @ (sum((v.co for v in p.data.vertices), Vector()) / len(p.data.vertices))
            off = (pc - c) * 0.6
            p.hide_render = False
            moved.append((p, p.matrix_world.copy()))
            p.matrix_world = Matrix.Translation(off) @ p.matrix_world
        frame_camera(cam, pieces, VIEWS["34"])
        files.append(render_to(os.path.join(out_dir, key, f"{key}_broken.png")))
        for p, m in moved:
            p.matrix_world = m
            p.hide_render = True
    root.rotation_mode, root.rotation_euler, root.location = old_rot[0], old_rot[1], old_rot[2]
    for n, h in saved.items():
        bpy.data.objects[n].hide_render = h
    return files
