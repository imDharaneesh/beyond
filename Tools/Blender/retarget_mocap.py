import bpy
import os
import sys
from mathutils import Matrix, Vector

argv = sys.argv[sys.argv.index("--") + 1:]
BLEND = argv[0]
SRC = argv[1]
OUT = argv[2]
JOBS = [a.split(":") for a in argv[3:]]

P = "mixamorig:"
DIRECTION = [
    ("LowerBack", "Spine"), ("Spine", "Spine1"),
    ("Neck", "Neck"), ("Head", "Head"),
    ("LeftShoulder", "LeftShoulder"), ("LeftArm", "LeftArm"), ("LeftForeArm", "LeftForeArm"),
    ("RightShoulder", "RightShoulder"), ("RightArm", "RightArm"), ("RightForeArm", "RightForeArm"),
    ("LeftUpLeg", "LeftUpLeg"), ("LeftLeg", "LeftLeg"), ("LeftFoot", "LeftFoot"), ("LeftToeBase", "LeftToeBase"),
    ("RightUpLeg", "RightUpLeg"), ("RightLeg", "RightLeg"), ("RightFoot", "RightFoot"), ("RightToeBase", "RightToeBase"),
]
FRAME_BONES = [("Hips", "Hips"), ("Spine1", "Spine2")]


def frame(side, up):
    side = side.normalized()
    up = (up - side * up.dot(side)).normalized()
    fwd = side.cross(up)
    return Matrix((side, up, fwd)).transposed()


def run(job):
    clip, name, first, last = job[0], job[1], int(job[2]), int(job[3])
    bpy.ops.wm.open_mainfile(filepath=BLEND)
    scene = bpy.context.scene
    tgt = bpy.data.objects["Explorer"]
    for ob in list(scene.objects):
        if ob != tgt:
            bpy.data.objects.remove(ob, do_unlink=True)
    scene.render.fps = 120
    bpy.ops.import_anim.bvh(filepath=os.path.join(SRC, clip + ".bvh"), global_scale=0.056444, frame_start=0,
                            use_fps_scale=False, update_scene_fps=False, rotate_mode="NATIVE", axis_forward="-Z", axis_up="Y")
    src = bpy.context.view_layer.objects.active

    tb = tgt.data.bones
    sw = src.matrix_world

    def s_rest(name):
        b = src.data.bones[name]
        return sw @ b.head_local, sw @ b.tail_local

    def t_rest(name):
        b = tb[P + name]
        return b.head_local.copy(), b.tail_local.copy()

    def s_frame_rest(kind):
        if kind == "Hips":
            return frame(s_rest("RightUpLeg")[0] - s_rest("LeftUpLeg")[0], s_rest("Spine1")[0] - s_rest("Hips")[0])
        return frame(s_rest("RightArm")[0] - s_rest("LeftArm")[0], s_rest("Neck")[1] - s_rest("Spine1")[0])

    def t_frame_rest(kind):
        if kind == "Hips":
            return frame(t_rest("RightUpLeg")[0] - t_rest("LeftUpLeg")[0], t_rest("Spine2")[0] - t_rest("Hips")[0])
        return frame(t_rest("RightArm")[0] - t_rest("LeftArm")[0], t_rest("Neck")[0] - t_rest("Spine2")[0])

    C = t_frame_rest("Hips") @ s_frame_rest("Hips").inverted()
    s_hip_h = s_rest("Hips")[0].z - min(s_rest("LeftFoot")[0].z, s_rest("RightFoot")[0].z)
    t_hip_h = t_rest("Hips")[0].z - min(t_rest("LeftFoot")[0].z, t_rest("RightFoot")[0].z)
    ratio = t_hip_h / s_hip_h
    rest_frames = {k: (s_frame_rest(k), t_frame_rest(k)) for k in ("Hips", "Chest")}

    order = [b for b in tgt.pose.bones]
    order.sort(key=lambda pb: len(pb.parent_recursive))
    direction_map = {P + t: s for s, t in DIRECTION}
    frame_map = {P + "Hips": "Hips", P + "Spine2": "Chest"}

    for pb in tgt.pose.bones:
        pb.rotation_mode = "QUATERNION"
        pb.matrix_basis = Matrix.Identity(4)
    action = bpy.data.actions.new(name)
    tgt.animation_data_create()
    tgt.animation_data.action = action

    scene.frame_set(first)
    origin = sw @ src.pose.bones["Hips"].head
    rest_hips = t_rest("Hips")[0]

    for f in range(first, last + 1):
        scene.frame_set(f)
        sp = src.pose.bones

        def s_pose(name):
            b = sp[name]
            return sw @ b.head, sw @ b.tail

        def s_frame_now(kind):
            if kind == "Hips":
                return frame(s_pose("RightUpLeg")[0] - s_pose("LeftUpLeg")[0], s_pose("Spine1")[0] - s_pose("Hips")[0])
            return frame(s_pose("RightArm")[0] - s_pose("LeftArm")[0], s_pose("Neck")[1] - s_pose("Spine1")[0])

        world = {}
        for pb in order:
            bone = pb.bone
            rest_rel = (bone.parent.matrix_local.inverted() @ bone.matrix_local) if bone.parent else bone.matrix_local
            parent_m = world[bone.parent.name] if bone.parent else Matrix.Identity(4)
            base = parent_m @ rest_rel
            rot = base.to_3x3()
            head = base.translation.copy()
            if pb.name in frame_map:
                kind = frame_map[pb.name]
                s_rest_f, t_rest_f = rest_frames[kind]
                delta = C @ s_frame_now(kind) @ s_rest_f.inverted() @ C.inverted()
                rot = (delta @ bone.matrix_local.to_3x3()).normalized()
                if kind == "Hips":
                    offset = (sw @ sp["Hips"].head) - origin
                    head = rest_hips + C @ offset * ratio
            elif pb.name in direction_map:
                h, t = s_pose(direction_map[pb.name])
                want = (C @ (t - h)).normalized()
                have = rot.col[1].normalized()
                rot = (have.rotation_difference(want).to_matrix() @ rot).normalized()
            m = Matrix.Translation(head) @ rot.to_4x4()
            world[pb.name] = m
            basis = base.inverted() @ m if not bone.parent else (parent_m @ rest_rel).inverted() @ m
            pb.matrix_basis = basis
            k = f - first
            pb.keyframe_insert("rotation_quaternion", frame=k)
            if pb.name == P + "Hips":
                pb.keyframe_insert("location", frame=k)

    bpy.data.objects.remove(src, do_unlink=True)
    for ob in list(scene.objects):
        if ob != tgt:
            bpy.data.objects.remove(ob, do_unlink=True)
    scene.frame_start = 0
    scene.frame_end = last - first
    bpy.ops.object.select_all(action="DESELECT")
    tgt.select_set(True)
    bpy.context.view_layer.objects.active = tgt
    bpy.ops.export_scene.fbx(
        filepath=os.path.join(OUT, f"Explorer_{name}.fbx"),
        use_selection=True,
        object_types={"ARMATURE"},
        apply_scale_options="FBX_SCALE_ALL",
        axis_forward="-Z",
        axis_up="Y",
        add_leaf_bones=False,
        primary_bone_axis="Y",
        secondary_bone_axis="X",
        use_armature_deform_only=False,
        bake_anim=True,
        bake_anim_use_all_actions=False,
        bake_anim_use_nla_strips=False,
        bake_anim_force_startend_keying=True,
        bake_anim_simplify_factor=0.0,
    )
    print("RETARGET_DONE", name, last - first)


for job in JOBS:
    run(job)
