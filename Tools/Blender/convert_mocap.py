import bpy
import os
import sys

argv = sys.argv[sys.argv.index("--") + 1:]
SRC = argv[0]
OUT = argv[1]
CLIPS = argv[2:]

os.makedirs(OUT, exist_ok=True)
for clip in CLIPS:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    scene.render.fps = 120
    bpy.ops.import_anim.bvh(
        filepath=os.path.join(SRC, clip + ".bvh"),
        global_scale=0.056444,
        frame_start=0,
        use_fps_scale=False,
        update_scene_fps=True,
        update_scene_duration=True,
        rotate_mode="NATIVE",
        axis_forward="-Z",
        axis_up="Y",
    )
    rig = bpy.context.view_layer.objects.active
    rig.name = "Mocap"
    rig.data.name = "Mocap"
    if rig.animation_data and rig.animation_data.action:
        rig.animation_data.action.name = clip
    bpy.ops.object.select_all(action="DESELECT")
    rig.select_set(True)
    bpy.ops.export_scene.fbx(
        filepath=os.path.join(OUT, f"Mocap_{clip}.fbx"),
        use_selection=True,
        object_types={"ARMATURE"},
        apply_scale_options="FBX_SCALE_ALL",
        axis_forward="-Z",
        axis_up="Y",
        add_leaf_bones=False,
        bake_anim=True,
        bake_anim_use_all_actions=False,
        bake_anim_use_nla_strips=False,
        bake_anim_force_startend_keying=True,
        bake_anim_simplify_factor=0.0,
        bake_anim_step=1.0,
    )
    print("MOCAP_DONE", clip)
