import bpy
import bmesh
import gzip
import json
import math
import os
import shutil
import sys
from mathutils import Vector, kdtree
from mathutils.bvhtree import BVHTree

argv = sys.argv[sys.argv.index("--") + 1:]
MPFB = argv[0]
ASSETS = argv[1]
OUT_DIR = argv[2]
OUT_BLEND = argv[3]
PREVIEW = argv[4] if len(argv) > 4 else ""

TEX_DIR = os.path.join(OUT_DIR, "Textures")
os.makedirs(TEX_DIR, exist_ok=True)

bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene

MACRO = os.path.join(MPFB, "targets", "macrodetails")
TARGETS = [
    (os.path.join(MACRO, "universal-male-young-averagemuscle-averageweight.target.gz"), 0.5),
    (os.path.join(MACRO, "universal-male-young-maxmuscle-averageweight.target.gz"), 0.5),
    (os.path.join(MACRO, "caucasian-male-young.target.gz"), 0.4),
    (os.path.join(MACRO, "asian-male-young.target.gz"), 0.35),
    (os.path.join(MACRO, "african-male-young.target.gz"), 0.25),
    (os.path.join(MACRO, "proportions", "male-young-averagemuscle-averageweight-idealproportions.target.gz"), 0.5),
    (os.path.join(MACRO, "proportions", "male-young-maxmuscle-averageweight-idealproportions.target.gz"), 0.5),
    ]

verts = []
uvs = []
faces = []
group = None
with open(os.path.join(MPFB, "3dobjs", "base.obj")) as f:
    for line in f:
        if line.startswith("v "):
            verts.append([float(x) for x in line.split()[1:4]])
        elif line.startswith("vt "):
            uvs.append([float(x) for x in line.split()[1:3]])
        elif line.startswith("g "):
            group = line.split()[1]
        elif line.startswith("f "):
            corners = []
            for c in line.split()[1:]:
                parts = c.split("/")
                corners.append((int(parts[0]) - 1, int(parts[1]) - 1 if len(parts) > 1 and parts[1] else -1))
            faces.append((group, corners))


def load_target(path, weight):
    if not os.path.exists(path):
        print("MISSING TARGET", path)
        return
    with gzip.open(path, "rt") as f:
        for line in f:
            line = line.strip()
            if not line or line.startswith("#"):
                continue
            p = line.split()
            i = int(p[0])
            verts[i][0] += float(p[1]) * weight
            verts[i][1] += float(p[2]) * weight
            verts[i][2] += float(p[3]) * weight


for path, w in TARGETS:
    load_target(path, w)

groups = json.load(open(os.path.join(MPFB, "mesh_metadata", "basemesh_vertex_groups.json")))


def group_indices(name):
    out = []
    for a, b in groups[name]:
        out.extend(range(a, b + 1))
    return out


min_y = min(verts[i][1] for i in group_indices("body"))


def bl(i):
    v = verts[i]
    return Vector((v[0] * 0.1, -v[2] * 0.1, (v[1] - min_y) * 0.1))


BL = [bl(i) for i in range(len(verts))]


def material(name, color=(0.8, 0.8, 0.8), rough=0.6, texture=None, alpha=False, normal=None):
    mat = bpy.data.materials.new(name)
    if mat.node_tree is None:
        try:
            mat.use_nodes = True
        except Exception:
            pass
    mat.diffuse_color = (*color, 1.0)
    nt = mat.node_tree
    if nt is not None:
        bsdf = next((n for n in nt.nodes if n.type == "BSDF_PRINCIPLED"), None)
        if bsdf is None:
            bsdf = nt.nodes.new("ShaderNodeBsdfPrincipled")
            out = next((n for n in nt.nodes if n.type == "OUTPUT_MATERIAL"), None) or nt.nodes.new("ShaderNodeOutputMaterial")
            nt.links.new(bsdf.outputs[0], out.inputs[0])
        bsdf.inputs["Base Color"].default_value = (*color, 1.0)
        bsdf.inputs["Roughness"].default_value = rough
        if texture:
            img = bpy.data.images.load(texture)
            tex = nt.nodes.new("ShaderNodeTexImage")
            tex.image = img
            nt.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
            if alpha:
                nt.links.new(tex.outputs["Alpha"], bsdf.inputs["Alpha"])
                try:
                    mat.surface_render_method = "DITHERED"
                except Exception:
                    pass
        if normal:
            nimg = bpy.data.images.load(normal)
            nimg.colorspace_settings.name = "Non-Color"
            ntex = nt.nodes.new("ShaderNodeTexImage")
            ntex.image = nimg
            nmap = nt.nodes.new("ShaderNodeNormalMap")
            nt.links.new(ntex.outputs["Color"], nmap.inputs["Color"])
            nt.links.new(nmap.outputs["Normal"], bsdf.inputs["Normal"])
    return mat


def copy_tex(src, name):
    if not src or not os.path.exists(src):
        return None
    dst = os.path.join(TEX_DIR, name)
    shutil.copyfile(src, dst)
    return dst


def build_mesh(name, face_list, positions, uv_lookup, mat):
    used = sorted({vi for _, corners in face_list for vi, _ in corners})
    remap = {vi: n for n, vi in enumerate(used)}
    me = bpy.data.meshes.new(name)
    bm = bmesh.new()
    bverts = [bm.verts.new(positions(vi)) for vi in used]
    bm.verts.ensure_lookup_table()
    uv_layer = bm.loops.layers.uv.new("UVMap")
    for _, corners in face_list:
        try:
            face = bm.faces.new([bverts[remap[vi]] for vi, _ in corners])
        except ValueError:
            continue
        for loop, (_, ti) in zip(face.loops, corners):
            if ti >= 0:
                loop[uv_layer].uv = uv_lookup(ti)
        face.smooth = True
    bm.to_mesh(me)
    bm.free()
    ob = bpy.data.objects.new(name, me)
    scene.collection.objects.link(ob)
    me.materials.append(mat)
    return ob, used


skin_tex = copy_tex(os.path.join(ASSETS, "skins", "young_caucasian_male", "young_lightskinned_male_diffuse.png"), "explorer_skin_diffuse.png")
SKIN = material("Explorer_Skin", (0.8, 0.62, 0.5), 0.5, skin_tex)
body_faces = [f for f in faces if f[0] == "body"]
body, body_used = build_mesh("Body", body_faces, lambda i: BL[i], lambda t: uvs[t], SKIN)
body_index = {vi: n for n, vi in enumerate(body_used)}

rig_def = json.load(open(os.path.join(MPFB, "rigs", "standard", "rig.mixamo_unity.json")))


def joint(spec):
    s = spec["strategy"]
    if s == "CUBE":
        idx = group_indices(spec["cube_name"])
    elif s == "MEAN":
        idx = spec["vertex_indices"]
    else:
        idx = [spec["vertex_index"]]
    acc = Vector((0, 0, 0))
    for i in idx:
        acc += BL[i]
    return acc / len(idx)


arm_data = bpy.data.armatures.new("ExplorerRig")
rig = bpy.data.objects.new("Explorer", arm_data)
scene.collection.objects.link(rig)
bpy.context.view_layer.objects.active = rig
rig.select_set(True)
bpy.ops.object.mode_set(mode="EDIT")
for name, b in rig_def.items():
    eb = arm_data.edit_bones.new(name)
    eb.head = joint(b["head"])
    eb.tail = joint(b["tail"])
    if (eb.tail - eb.head).length < 1e-4:
        eb.tail = eb.head + Vector((0, 0, 0.02))
    eb.roll = b.get("roll", 0.0)
for name, b in rig_def.items():
    if b.get("parent"):
        arm_data.edit_bones[name].parent = arm_data.edit_bones[b["parent"]]
        arm_data.edit_bones[name].use_connect = False
bpy.ops.object.mode_set(mode="OBJECT")
bone_pos = {b.name: (b.head_local.copy(), b.tail_local.copy()) for b in arm_data.bones}

weights_def = json.load(open(os.path.join(MPFB, "rigs", "standard", "weights.mixamo_unity.json")))["weights"]
vertex_weights = {}
for bone_name, entries in weights_def.items():
    for vi, w in entries:
        vertex_weights.setdefault(vi, {})[bone_name] = w


def apply_weights(ob, per_vertex):
    vgs = {}
    for v_index, wmap in enumerate(per_vertex):
        total = sum(wmap.values())
        if total <= 0:
            continue
        for bone_name, w in wmap.items():
            if w / total < 0.01:
                continue
            if bone_name not in vgs:
                vgs[bone_name] = ob.vertex_groups.new(name=bone_name)
            vgs[bone_name].add([v_index], w / total, "REPLACE")
    ob.parent = rig
    mod = ob.modifiers.new("Armature", "ARMATURE")
    mod.object = rig


apply_weights(body, [vertex_weights.get(vi, {}) for vi in body_used])

body_tree = kdtree.KDTree(len(body_used))
for n, vi in enumerate(body_used):
    body_tree.insert(BL[vi], n)
body_tree.balance()


def nearest_weights(p):
    _, n, _ = body_tree.find(p)
    return dict(vertex_weights.get(body_used[n], {}))


def parse_mhmat(path):
    d = {}
    if not os.path.exists(path):
        return d
    for line in open(path):
        p = line.strip().split(None, 1)
        if len(p) == 2:
            d[p[0]] = p[1]
    return d


def fit_proxy(name, folder, file_base, mat_name, color=(0.8, 0.8, 0.8), rough=0.7, alpha=False, extra_offset=0.0):
    mhclo = os.path.join(folder, file_base + ".mhclo")
    refs = []
    scales = {}
    obj_file = file_base + ".obj"
    mhmat_file = file_base + ".mhmat"
    reading = False
    for line in open(mhclo):
        s = line.strip()
        if not s or s.startswith("#"):
            continue
        p = s.split()
        if p[0] in ("x_scale", "y_scale", "z_scale"):
            scales[p[0][0]] = (int(p[1]), int(p[2]), float(p[3]))
            continue
        if p[0] == "obj_file":
            obj_file = p[1]
            continue
        if p[0] == "material":
            mhmat_file = p[1]
            continue
        if p[0] == "verts":
            reading = True
            continue
        if reading:
            if p[0].isalpha() or p[0] == "delete_verts":
                reading = False
                continue
            nums = [float(x) for x in p]
            if len(nums) == 1:
                refs.append(((int(nums[0]), 0, 0), (1.0, 0.0, 0.0), (0.0, 0.0, 0.0)))
            elif len(nums) >= 9:
                refs.append(((int(nums[0]), int(nums[1]), int(nums[2])), tuple(nums[3:6]), tuple(nums[6:9])))

    def axis_scale(key, axis):
        if key not in scales:
            return 1.0
        a, b, dist = scales[key]
        return abs(verts[a][axis] - verts[b][axis]) / dist if dist else 1.0

    sx, sy, sz = axis_scale("x", 0), axis_scale("y", 1), axis_scale("z", 2)
    positions = []
    proxy_weights = []
    for (a, b, c), (wa, wb, wc), (dx, dy, dz) in refs:
        mh = [verts[a][k] * wa + verts[b][k] * wb + verts[c][k] * wc for k in range(3)]
        mh = [mh[0] + dx * sx, mh[1] + dy * sy, mh[2] + dz * sz]
        positions.append(Vector((mh[0] * 0.1, -mh[2] * 0.1, (mh[1] - min_y) * 0.1)))
        wmap = {}
        for vi, w in ((a, wa), (b, wb), (c, wc)):
            if w == 0:
                continue
            src = vertex_weights.get(vi)
            if src is None:
                src = nearest_weights(BL[vi])
            for bone_name, bw in src.items():
                wmap[bone_name] = wmap.get(bone_name, 0.0) + bw * w
        proxy_weights.append(wmap)

    p_uvs = []
    p_faces = []
    for line in open(os.path.join(folder, obj_file)):
        if line.startswith("vt "):
            p_uvs.append([float(x) for x in line.split()[1:3]])
        elif line.startswith("f "):
            corners = []
            for c in line.split()[1:]:
                parts = c.split("/")
                corners.append((int(parts[0]) - 1, int(parts[1]) - 1 if len(parts) > 1 and parts[1] else -1))
            p_faces.append(("proxy", corners))

    mm = parse_mhmat(os.path.join(folder, mhmat_file))
    diffuse = copy_tex(os.path.join(folder, mm["diffuseTexture"]) if "diffuseTexture" in mm else None, f"{mat_name}_diffuse.png")
    normal = copy_tex(os.path.join(folder, mm["normalmapTexture"]) if "normalmapTexture" in mm else None, f"{mat_name}_normal.png")
    mat = material(mat_name, color, rough, diffuse, alpha, normal)
    ob, used = build_mesh(name, p_faces, lambda i: positions[i], lambda t: p_uvs[t], mat)
    if extra_offset:
        ob.data.update()
        for v in ob.data.vertices:
            v.co += v.normal * extra_offset
    apply_weights(ob, [proxy_weights[i] for i in used])
    return ob


eye_mat_dir = os.path.join(ASSETS, "eyes", "materials")
eyes = fit_proxy("Eyes", os.path.join(ASSETS, "eyes", "high-poly"), "high-poly", "Explorer_Eyes", (1, 1, 1), 0.2)
eye_tex = copy_tex(os.path.join(eye_mat_dir, "brown_eye.png"), "Explorer_Eyes_diffuse.png")
eyes.data.materials.clear()
eyes.data.materials.append(material("Explorer_Eyes", (1, 1, 1), 0.15, eye_tex, True))
fit_proxy("Eyebrows", os.path.join(ASSETS, "eyebrows", "eyebrow001"), "eyebrow001", "Explorer_Eyebrows", (0.2, 0.15, 0.1), 0.8, True)
fit_proxy("Eyelashes", os.path.join(ASSETS, "eyelashes", "eyelashes01"), "eyelashes01", "Explorer_Eyelashes", (0.1, 0.08, 0.06), 0.8, True)
fit_proxy("Hair", os.path.join(ASSETS, "hair", "short04"), "short04", "Explorer_Hair", (0.12, 0.09, 0.07), 0.55, True)
fit_proxy("Outfit_Boots", os.path.join(ASSETS, "clothes", "shoes03"), "shoes03", "Explorer_Boots", (0.3, 0.3, 0.3), 0.5)
suit = fit_proxy("Outfit_Suit", os.path.join(ASSETS, "clothes", "male_casualsuit05"), "male_casualsuit05", "Explorer_Outfit", (0.6, 0.6, 0.6), 0.85)

hips_z = bone_pos["mixamorig:Hips"][0].z
bpy.context.view_layer.objects.active = suit
bpy.ops.object.select_all(action="DESELECT")
suit.select_set(True)
bpy.ops.object.mode_set(mode="EDIT")
bpy.ops.mesh.select_all(action="SELECT")
bpy.ops.mesh.separate(type="LOOSE")
bpy.ops.object.mode_set(mode="OBJECT")
pieces = [o for o in bpy.context.selected_objects]
jacket_parts, jeans_parts = [], []
for o in pieces:
    zs = [v.co.z for v in o.data.vertices]
    centre = sum(zs) / len(zs)
    top = max(zs)
    (jacket_parts if centre > hips_z + 0.12 or top > hips_z + 0.45 else jeans_parts).append(o)


def join(parts, name):
    if not parts:
        return None
    bpy.ops.object.select_all(action="DESELECT")
    for o in parts:
        o.select_set(True)
    bpy.context.view_layer.objects.active = parts[0]
    if len(parts) > 1:
        bpy.ops.object.join()
    o = bpy.context.view_layer.objects.active
    o.name = name
    o.data.name = name
    return o


join(jacket_parts, "Outfit_Jacket")
join(jeans_parts, "Outfit_Jeans")

def cut(ob, field):
    bm = bmesh.new()
    bm.from_mesh(ob.data)
    for _ in range(4):
        vals = {v: field(v.co) for v in bm.verts}
        moves = {}
        for v in bm.verts:
            if vals[v] <= 1e-4:
                continue
            best = None
            for e in v.link_edges:
                n = e.other_vert(v)
                fn = vals[n]
                if fn <= 1e-4:
                    t = vals[v] / max(1e-9, vals[v] - fn)
                    cand = v.co.lerp(n.co, min(1.0, t))
                    if best is None or (cand - v.co).length < (best - v.co).length:
                        best = cand
            if best is not None:
                moves[v] = best
        for v, co in moves.items():
            v.co = co
    vals = {v: field(v.co) for v in bm.verts}
    drop = [f for f in bm.faces if all(vals[v] > 1e-4 for v in f.verts)]
    bmesh.ops.delete(bm, geom=drop, context="FACES")
    loose = [v for v in bm.verts if not v.link_faces]
    bmesh.ops.delete(bm, geom=loose, context="VERTS")
    boundary = [v for v in bm.verts if v.is_boundary]
    for _ in range(4):
        moves = {}
        for v in boundary:
            ring = [e.other_vert(v) for e in v.link_edges if e.other_vert(v).is_boundary and e.is_boundary]
            if len(ring) == 2:
                moves[v] = v.co * 0.5 + (ring[0].co + ring[1].co) * 0.25
        for v, co in moves.items():
            v.co = co
    bm.to_mesh(ob.data)
    bm.free()
    ob.data.update()


body_bvh = None


def push_out(bm, offset):
    for v in bm.verts:
        loc, normal, _, _ = body_bvh.find_nearest(v.co)
        if loc is None:
            continue
        if (v.co - loc).dot(normal) < offset:
            v.co = loc + normal * offset


def finish_garment(ob, offset):
    global body_bvh
    if body_bvh is None:
        body_bvh = BVHTree.FromPolygons([v.co.copy() for v in body.data.vertices], [list(p.vertices) for p in body.data.polygons])
    bm = bmesh.new()
    bm.from_mesh(ob.data)
    push_out(bm, offset)
    inner = [v for v in bm.verts if not v.is_boundary]
    for _ in range(3):
        for _ in range(4):
            bmesh.ops.smooth_vert(bm, verts=inner, factor=0.5, use_axis_x=True, use_axis_y=True, use_axis_z=True)
        push_out(bm, offset)
    bm.to_mesh(ob.data)
    bm.free()
    ob.data.update()
    apply_weights(ob, [nearest_weights(v.co) for v in ob.data.vertices])


up_leg_z = bone_pos["mixamorig:LeftUpLeg"][0].z
neck_z = bone_pos["mixamorig:Neck"][0].z
arm_x = abs(bone_pos["mixamorig:LeftArm"][0].x)

brief_lo, brief_hi = up_leg_z - 0.16, hips_z + 0.06
rough_briefs = [f for f in body_faces
                if brief_lo - 0.05 < sum(BL[vi].z for vi, _ in f[1]) / len(f[1]) < brief_hi + 0.05
                and abs(sum(BL[vi].x for vi, _ in f[1]) / len(f[1])) < arm_x]
BRIEFS = material("Explorer_Briefs", (0.07, 0.08, 0.1), 0.85)
briefs, _ = build_mesh("Underwear_Briefs", rough_briefs, lambda i: BL[i], lambda t: uvs[t], BRIEFS)
cut(briefs, lambda p: max(p.z - brief_hi, (brief_lo - p.z) - 0.12 * p.y))
finish_garment(briefs, 0.004)


tee = fit_proxy("Tee_Suit", os.path.join(ASSETS, "clothes", "male_casualsuit06"), "male_casualsuit06", "Explorer_Tee", (0.9, 0.9, 0.9), 0.9)
bpy.context.view_layer.objects.active = tee
bpy.ops.object.select_all(action="DESELECT")
tee.select_set(True)
bpy.ops.object.mode_set(mode="EDIT")
bpy.ops.mesh.select_all(action="SELECT")
bpy.ops.mesh.separate(type="LOOSE")
bpy.ops.object.mode_set(mode="OBJECT")
tee_top, tee_rest = [], []
for o in list(bpy.context.selected_objects):
    zs = [v.co.z for v in o.data.vertices]
    centre = sum(zs) / len(zs)
    (tee_top if centre > hips_z + 0.12 or max(zs) > hips_z + 0.45 else tee_rest).append(o)
for o in tee_rest:
    bpy.data.objects.remove(o, do_unlink=True)
join(tee_top, "Underwear_Undershirt")

foot_z = bone_pos["mixamorig:LeftFoot"][0].z + 0.035
bpy.ops.object.select_all(action="DESELECT")
bpy.context.view_layer.objects.active = body
body.select_set(True)
bpy.ops.object.mode_set(mode="EDIT")
bpy.ops.mesh.select_all(action="DESELECT")
bpy.ops.object.mode_set(mode="OBJECT")
for poly in body.data.polygons:
    poly.select = poly.center.z < foot_z
bpy.ops.object.mode_set(mode="EDIT")
bpy.ops.mesh.separate(type="SELECTED")
bpy.ops.object.mode_set(mode="OBJECT")
for o in bpy.context.selected_objects:
    if o != body:
        o.name = "Body_Feet"
        o.data.name = "Body_Feet"

for ob in scene.objects:
    if ob.type == "MESH":
        for poly in ob.data.polygons:
            poly.use_smooth = True

bpy.ops.object.select_all(action="DESELECT")
for ob in scene.objects:
    ob.select_set(True)
bpy.context.view_layer.objects.active = rig
fbx_path = os.path.join(OUT_DIR, "Explorer.fbx")
bpy.ops.export_scene.fbx(
    filepath=fbx_path,
    use_selection=False,
    apply_scale_options="FBX_SCALE_ALL",
    axis_forward="-Z",
    axis_up="Y",
    object_types={"ARMATURE", "MESH"},
    use_mesh_modifiers=True,
    add_leaf_bones=False,
    primary_bone_axis="Y",
    secondary_bone_axis="X",
    use_armature_deform_only=False,
    bake_anim=False,
    path_mode="STRIP",
    embed_textures=False,
)

if PREVIEW:
    outfit = ["Outfit_Jacket", "Outfit_Jeans", "Outfit_Boots"]
    for n in outfit:
        if n in scene.objects:
            scene.objects[n].hide_render = True
    cam_data = bpy.data.cameras.new("PreviewCam")
    cam = bpy.data.objects.new("PreviewCam", cam_data)
    scene.collection.objects.link(cam)
    scene.camera = cam
    for nm, energy, rot in (("Key", 3.2, (55, 0, -35)), ("Fill", 1.0, (65, 0, 150)), ("Rim", 1.5, (110, 0, 180))):
        ld = bpy.data.lights.new(nm, "SUN")
        ld.energy = energy
        lo_ = bpy.data.objects.new(nm, ld)
        lo_.rotation_euler = tuple(math.radians(a) for a in rot)
        scene.collection.objects.link(lo_)
    world = bpy.data.worlds.new("PreviewWorld")
    scene.world = world
    world.color = (0.3, 0.32, 0.35)
    for engine in ("BLENDER_EEVEE_NEXT", "BLENDER_EEVEE", "BLENDER_WORKBENCH"):
        try:
            scene.render.engine = engine
            break
        except Exception:
            continue
    scene.render.resolution_x = 1200
    scene.render.resolution_y = 1200
    head_z = bone_pos["mixamorig:Head"][0].z + 0.08
    shots = [
        ("front", (0.0, -4.4, 0.95), (90, 0, 0), 50, False),
        ("three_quarter", (2.7, -3.4, 1.0), (90, 0, 38), 50, False),
        ("face", (0.32, -0.9, head_z), (90, 0, 20), 85, False),
        ("outfit", (2.7, -3.4, 1.0), (90, 0, 38), 50, True),
    ]
    base, ext = os.path.splitext(PREVIEW)
    for label, loc, rot, lens, show_outfit in shots:
        for n in outfit:
            if n in scene.objects:
                scene.objects[n].hide_render = not show_outfit
        cam.location = loc
        cam.rotation_euler = tuple(math.radians(a) for a in rot)
        cam_data.lens = lens
        scene.render.filepath = f"{base}_{label}{ext}"
        bpy.ops.render.render(write_still=True)
    for n in outfit:
        if n in scene.objects:
            scene.objects[n].hide_render = False

bpy.ops.wm.save_as_mainfile(filepath=OUT_BLEND)
print("EXPLORER_DONE", fbx_path, "height", max(v.co.z for v in body.data.vertices))
