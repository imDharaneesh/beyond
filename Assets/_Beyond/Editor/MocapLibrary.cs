using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Beyond.EditorTools
{
    public static class MocapLibrary
    {
        const string Dir = BuilderAssets.Root + "/Art/Animations/Explorer";
        const string ControllerPath = BuilderAssets.Generated + "/Animation/Explorer.controller";

        struct ClipSpec
        {
            public string file, name;
            public float first, last;
            public bool loop;
            public float fallbackSpeed;
            public float timeScale;
        }

        static readonly ClipSpec[] Specs =
        {
            new ClipSpec { file = "82_08", name = "Idle", first = 0, last = 240, loop = true, fallbackSpeed = 0f, timeScale = 1f },
            new ClipSpec { file = "16_15", name = "Walk", first = 0, last = 137, loop = true, fallbackSpeed = 1.1f, timeScale = 1.25f },
            new ClipSpec { file = "16_36", name = "Jog", first = 0, last = 94, loop = true, fallbackSpeed = 2.68f, timeScale = 1.12f },
            new ClipSpec { file = "09_06", name = "Run", first = 0, last = 89, loop = true, fallbackSpeed = 3.63f, timeScale = 1.3f },
            new ClipSpec { file = "77_29", name = "Creep", first = 0, last = 161, loop = true, fallbackSpeed = 0.9f, timeScale = 1f },
            new ClipSpec { file = "13_39", name = "Jump", first = 0, last = 86, loop = false, fallbackSpeed = 0f, timeScale = 1f }
        };

        public class Result
        {
            public RuntimeAnimatorController controller;
            public float walkSpeed, runSpeed, creepSpeed;
        }

        public static Result Build()
        {
            var clips = new Dictionary<string, AnimationClip>();
            var speeds = new Dictionary<string, float>();
            var scales = new Dictionary<string, float>();
            foreach (var spec in Specs)
            {
                var clip = ImportClip(spec);
                if (clip == null) continue;
                clips[spec.name] = clip;
                scales[spec.name] = spec.timeScale;
                float measured = new Vector2(clip.averageSpeed.x, clip.averageSpeed.z).magnitude;
                speeds[spec.name] = (measured > 0.2f ? measured : spec.fallbackSpeed) * spec.timeScale;
            }
            if (!clips.ContainsKey("Idle") || !clips.ContainsKey("Walk") || !clips.ContainsKey("Run")) return null;

            BuilderAssets.EnsureFolder(BuilderAssets.Generated + "/Animation");
            AssetDatabase.DeleteAsset(ControllerPath);
            var controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            controller.AddParameter("Grounded", AnimatorControllerParameterType.Bool);
            controller.AddParameter("Crouch", AnimatorControllerParameterType.Bool);
            controller.AddParameter("Jump", AnimatorControllerParameterType.Trigger);

            var layers = controller.layers;
            layers[0].iKPass = true;
            controller.layers = layers;
            var sm = controller.layers[0].stateMachine;

            var locomotion = controller.CreateBlendTreeInController("Locomotion", out var tree, 0);
            tree.blendType = BlendTreeType.Simple1D;
            tree.blendParameter = "Speed";
            tree.useAutomaticThresholds = false;
            tree.AddChild(clips["Idle"], 0f);
            foreach (var n in new[] { "Walk", "Jog", "Run" })
                if (clips.ContainsKey(n)) tree.AddChild(clips[n], speeds[n]);
            SortChildren(tree);
            var kids = tree.children;
            for (int i = 0; i < kids.Length; i++)
                kids[i].timeScale = scales.TryGetValue(kids[i].motion.name, out var ts) ? ts : 1f;
            tree.children = kids;
            sm.defaultState = locomotion;

            AnimatorState crouchState = null;
            if (clips.ContainsKey("Creep"))
            {
                crouchState = controller.CreateBlendTreeInController("Crouch", out var crouchTree, 0);
                crouchTree.blendType = BlendTreeType.Simple1D;
                crouchTree.blendParameter = "Speed";
                crouchTree.useAutomaticThresholds = false;
                crouchTree.AddChild(clips["Creep"], 0f);
                crouchTree.AddChild(clips["Creep"], speeds["Creep"]);
                var children = crouchTree.children;
                children[0].timeScale = 0.12f;
                crouchTree.children = children;

                var toCrouch = locomotion.AddTransition(crouchState);
                toCrouch.hasExitTime = false;
                toCrouch.duration = 0.25f;
                toCrouch.AddCondition(AnimatorConditionMode.If, 0, "Crouch");
                var fromCrouch = crouchState.AddTransition(locomotion);
                fromCrouch.hasExitTime = false;
                fromCrouch.duration = 0.25f;
                fromCrouch.AddCondition(AnimatorConditionMode.IfNot, 0, "Crouch");
            }

            if (clips.ContainsKey("Jump"))
            {
                var jump = sm.AddState("Jump");
                jump.motion = clips["Jump"];
                var toJump = sm.AddAnyStateTransition(jump);
                toJump.hasExitTime = false;
                toJump.duration = 0.08f;
                toJump.canTransitionToSelf = false;
                toJump.AddCondition(AnimatorConditionMode.If, 0, "Jump");
                var land = jump.AddTransition(locomotion);
                land.hasExitTime = true;
                land.exitTime = 0.8f;
                land.duration = 0.2f;
                land.AddCondition(AnimatorConditionMode.If, 0, "Grounded");
            }

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            return new Result
            {
                controller = controller,
                walkSpeed = speeds["Walk"],
                runSpeed = speeds["Run"],
                creepSpeed = speeds.TryGetValue("Creep", out var c) ? c : 0.9f
            };
        }

        static void SortChildren(BlendTree tree)
        {
            var children = tree.children.OrderBy(c => c.threshold).ToArray();
            tree.children = children;
        }

        static AnimationClip ImportClip(ClipSpec spec)
        {
            string path = $"{Dir}/Explorer_{spec.name}.fbx";
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null) return null;
            importer.animationType = ModelImporterAnimationType.Human;
            importer.sourceAvatar = null;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.importAnimation = true;
            importer.resampleCurves = true;
            importer.animationCompression = ModelImporterAnimationCompression.Optimal;

            var source = importer.defaultClipAnimations;
            var take = source.Length > 0 ? source[0] : new ModelImporterClipAnimation();
            take.name = spec.name;
            take.firstFrame = spec.first;
            take.lastFrame = spec.last;
            take.loopTime = spec.loop;
            take.loopPose = spec.loop;
            take.lockRootRotation = true;
            take.keepOriginalOrientation = false;
            take.lockRootHeightY = true;
            take.keepOriginalPositionY = false;
            take.heightFromFeet = true;
            take.lockRootPositionXZ = false;
            take.keepOriginalPositionXZ = false;
            take.rotationOffset = 0f;
            importer.clipAnimations = new[] { take };
            importer.SaveAndReimport();

            var clip = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().FirstOrDefault(c => c.name == spec.name);
            if (clip != null && spec.loop && new Vector2(clip.averageSpeed.x, clip.averageSpeed.z).magnitude > 0.3f)
            {
                float drift = Mathf.Atan2(clip.averageSpeed.x, clip.averageSpeed.z) * Mathf.Rad2Deg;
                if (Mathf.Abs(drift) > 2f)
                {
                    importer = (ModelImporter)AssetImporter.GetAtPath(path);
                    var clipsAnim = importer.clipAnimations;
                    clipsAnim[0].rotationOffset = drift;
                    importer.clipAnimations = clipsAnim;
                    importer.SaveAndReimport();
                    clip = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().FirstOrDefault(c => c.name == spec.name);
                }
            }
            return clip;
        }
    }
}
