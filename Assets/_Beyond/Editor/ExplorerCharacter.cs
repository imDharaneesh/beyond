using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Beyond.EditorTools
{
    public static class ExplorerCharacter
    {
        public const string Dir = BuilderAssets.Root + "/Art/Characters/Explorer";
        public const string FbxPath = Dir + "/Explorer.fbx";
        const string Tex = Dir + "/Textures";
        const string Linen = BuilderAssets.PolyHaven + "/Textures/rough_linen";

        public static GameObject Import()
        {
            var importer = AssetImporter.GetAtPath(FbxPath) as ModelImporter;
            if (importer == null) return null;

            bool dirty = false;
            if (importer.animationType != ModelImporterAnimationType.Human)
            {
                importer.animationType = ModelImporterAnimationType.Human;
                importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                dirty = true;
            }
            if (importer.importAnimation)
            {
                importer.importAnimation = false;
                dirty = true;
            }
            if (importer.materialImportMode != ModelImporterMaterialImportMode.ImportViaMaterialDescription)
            {
                importer.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
                dirty = true;
            }
            if (importer.importBlendShapes)
            {
                importer.importBlendShapes = false;
                dirty = true;
            }
            if (dirty) importer.SaveAndReimport();

            var model = AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath);
            var names = model.GetComponentsInChildren<Renderer>(true)
                .SelectMany(r => r.sharedMaterials).Where(m => m != null).Select(m => m.name).Distinct().ToList();
            var map = importer.GetExternalObjectMap();
            foreach (var kv in map)
                if (kv.Key.type == typeof(Material) && !names.Contains(kv.Key.name)) names.Add(kv.Key.name);
            var wanted = new Dictionary<string, Material>();
            foreach (var n in names)
            {
                var source = map.Keys.FirstOrDefault(k => k.type == typeof(Material) && map[k] != null && map[k].name == n).name ?? n;
                var mat = MaterialFor(source);
                if (mat != null) wanted[source] = mat;
            }
            AssetDatabase.SaveAssets();
            importer = AssetImporter.GetAtPath(FbxPath) as ModelImporter;
            map = importer.GetExternalObjectMap();
            bool remapped = false;
            foreach (var kv in wanted)
            {
                var id = new AssetImporter.SourceAssetIdentifier(typeof(Material), kv.Key);
                if (map.TryGetValue(id, out var existing) && existing == kv.Value) continue;
                importer.AddRemap(id, kv.Value);
                remapped = true;
            }
            if (remapped) importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath);
        }

        static Material MaterialFor(string sourceName)
        {
            string n = sourceName;
            if (n.StartsWith("Explorer_Skin"))
                return Lit("Explorer Skin", Texture($"{Tex}/explorer_skin_diffuse.png"), null, new Color(0.96f, 0.87f, 0.8f), 0.38f, false, 0f);
            if (n.StartsWith("Explorer_Eyes"))
                return Lit("Explorer Eyes", Texture($"{Tex}/Explorer_Eyes_diffuse.png"), null, Color.white, 0.85f, true, 0.5f);
            if (n.StartsWith("Explorer_Eyebrows"))
                return Lit("Explorer Eyebrows", Texture($"{Tex}/Explorer_Eyebrows_diffuse.png"), null, new Color(0.25f, 0.2f, 0.16f), 0.2f, true, 0.3f, true);
            if (n.StartsWith("Explorer_Eyelashes"))
                return Lit("Explorer Eyelashes", Texture($"{Tex}/Explorer_Eyelashes_diffuse.png"), null, new Color(0.15f, 0.12f, 0.1f), 0.2f, true, 0.3f, true);
            if (n.StartsWith("Explorer_Hair"))
                return Lit("Explorer Hair", Texture($"{Tex}/Explorer_Hair_diffuse.png"), null, new Color(0.55f, 0.5f, 0.47f), 0.42f, true, 0.35f, true);
            if (n.StartsWith("Explorer_Briefs"))
                return Fabric("Explorer Briefs", new Color(0.08f, 0.09f, 0.11f));
            if (n.StartsWith("Explorer_Undershirt"))
                return Fabric("Explorer Undershirt", new Color(0.88f, 0.88f, 0.86f));
            if (n.StartsWith("Explorer_Outfit"))
                return Lit("Explorer Outfit", Texture($"{Tex}/Explorer_Outfit_diffuse.png"), Normal($"{Tex}/Explorer_Outfit_normal.png"), Color.white, 0.18f, false, 0f);
            if (n.StartsWith("Explorer_Boots"))
                return Lit("Explorer Boots", Texture($"{Tex}/Explorer_Boots_diffuse.png"), null, Color.white, 0.45f, false, 0f);
            return null;
        }

        static Material Fabric(string name, Color color)
        {
            var mat = Lit(name, null, Normal($"{Linen}/rough_linen_nor_gl.jpg"), color, 0.12f, false, 0f);
            mat.SetTextureScale("_BaseMap", new Vector2(12f, 12f));
            mat.SetTextureScale("_BumpMap", new Vector2(12f, 12f));
            mat.SetFloat("_BumpScale", 0.6f);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static Texture2D Texture(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return null;
            if (!importer.alphaIsTransparency)
            {
                importer.alphaIsTransparency = true;
                importer.mipMapsPreserveCoverage = true;
                importer.alphaTestReferenceValue = 0.4f;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        static Texture2D Normal(string path) => BuilderAssets.LoadTexture(path, true);

        static Material Lit(string name, Texture2D albedo, Texture2D normal, Color tint, float smoothness, bool cutout, float cutoff, bool twoSided = false)
        {
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
            mat.SetColor("_BaseColor", tint);
            if (albedo != null) mat.SetTexture("_BaseMap", albedo);
            if (normal != null)
            {
                mat.SetTexture("_BumpMap", normal);
                mat.EnableKeyword("_NORMALMAP");
            }
            mat.SetFloat("_Smoothness", smoothness);
            if (cutout)
            {
                mat.SetFloat("_AlphaClip", 1f);
                mat.SetFloat("_Cutoff", cutoff);
                mat.EnableKeyword("_ALPHATEST_ON");
                mat.SetOverrideTag("RenderType", "TransparentCutout");
                mat.renderQueue = (int)RenderQueue.AlphaTest;
            }
            if (twoSided) mat.SetFloat("_Cull", (float)CullMode.Off);
            return BuilderAssets.SaveAsset(mat, $"{BuilderAssets.Generated}/Materials/{name}.mat");
        }
    }
}
