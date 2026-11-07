using System.Collections.Generic;
using System.IO;
using System.Linq;
using Beyond.Items;
using UnityEditor;
using UnityEngine;

namespace Beyond.EditorTools
{
    public static class BuilderAssets
    {
        public const string Root = "Assets/_Beyond";
        public const string Generated = Root + "/Art/Generated";
        public const string PolyHaven = Root + "/Art/ThirdParty/PolyHaven";

        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        static readonly int BaseMap = Shader.PropertyToID("_BaseMap");
        static readonly int BumpMap = Shader.PropertyToID("_BumpMap");
        static readonly int Smoothness = Shader.PropertyToID("_Smoothness");
        static readonly int Metallic = Shader.PropertyToID("_Metallic");
        static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");

        public static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        public static T SaveAsset<T>(T asset, string path) where T : Object
        {
            EnsureFolder(Path.GetDirectoryName(path).Replace('\\', '/'));
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null)
            {
                EditorUtility.CopySerialized(asset, existing);
                EditorUtility.SetDirty(existing);
                return existing;
            }
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        public static Material Lit(string name, Color color, float smoothness = 0.2f, float metallic = 0f,
            Texture2D baseMap = null, Texture2D normalMap = null, Color? emission = null)
        {
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
            mat.SetColor(BaseColor, color);
            mat.SetFloat(Smoothness, smoothness);
            mat.SetFloat(Metallic, metallic);
            if (baseMap != null) mat.SetTexture(BaseMap, baseMap);
            if (normalMap != null)
            {
                mat.SetTexture(BumpMap, normalMap);
                mat.EnableKeyword("_NORMALMAP");
            }
            if (emission.HasValue)
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor(EmissionColor, emission.Value);
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            mat.enableInstancing = true;
            return SaveAsset(mat, $"{Generated}/Materials/{name}.mat");
        }

        public static Material PolyHavenLit(string folder, string asset, float smoothness = 0.15f)
        {
            string dir = $"{PolyHaven}/{folder}/{asset}";
            var diff = LoadTexture($"{dir}/{asset}_diff.jpg", false);
            var nor = LoadTexture($"{dir}/{asset}_nor_gl.jpg", true);
            return Lit("PH_" + asset, Color.white, smoothness, 0f, diff, nor);
        }

        public static Texture2D LoadTexture(string path, bool normalMap, int maxSize = 2048)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return null;
            bool dirty = false;
            var wantType = normalMap ? TextureImporterType.NormalMap : TextureImporterType.Default;
            if (importer.textureType != wantType) { importer.textureType = wantType; dirty = true; }
            if (importer.maxTextureSize != maxSize) { importer.maxTextureSize = maxSize; dirty = true; }
            if (dirty) importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        public static Texture2D SavePng(Texture2D tex, string name, bool alpha, bool clamp)
        {
            string path = $"{Generated}/Textures/{name}.png";
            EnsureFolder($"{Generated}/Textures");
            File.WriteAllBytes(path, tex.EncodeToPNG());
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.alphaIsTransparency = alpha;
            importer.wrapMode = clamp ? TextureWrapMode.Clamp : TextureWrapMode.Repeat;
            importer.mipmapEnabled = true;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        public static Mesh SaveMesh(Mesh mesh)
        {
            string path = $"{Generated}/Meshes/{mesh.name}.asset";
            EnsureFolder($"{Generated}/Meshes");
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        static readonly Dictionary<string, Mesh[]> modelCache = new Dictionary<string, Mesh[]>();

        public static void ResetCache() => modelCache.Clear();

        public static Mesh[] ModelMeshes(string asset)
        {
            if (modelCache.TryGetValue(asset, out var cached) && cached.All(m => m != null)) return cached;
            var meshes = LoadModelMeshes(asset);
            modelCache[asset] = meshes;
            return meshes;
        }

        static Mesh[] LoadModelMeshes(string asset)
        {
            string path = $"{PolyHaven}/Models/{asset}/{asset}.fbx";
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer != null && (!importer.bakeAxisConversion || importer.useFileScale ||
                                     importer.materialImportMode != ModelImporterMaterialImportMode.None))
            {
                importer.bakeAxisConversion = true;
                importer.useFileScale = false;
                importer.materialImportMode = ModelImporterMaterialImportMode.None;
                importer.SaveAndReimport();
            }

            var root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (root == null) return new Mesh[0];
            var result = new List<Mesh>();
            foreach (var mf in root.GetComponentsInChildren<MeshFilter>(true))
            {
                var src = mf.sharedMesh;
                if (src == null) continue;
                if (src.name.Contains("_LOD") && !src.name.EndsWith("_LOD1")) continue;
                result.Add(BakeMesh(src, Matrix4x4.Rotate(mf.transform.rotation), $"PH_{src.name}"));
            }
            return result.ToArray();
        }

        static Mesh BakeMesh(Mesh src, Matrix4x4 m, string name)
        {
            var mesh = Object.Instantiate(src);
            mesh.name = name;
            var verts = mesh.vertices;
            var normals = mesh.normals;
            var tangents = mesh.tangents;
            for (int i = 0; i < verts.Length; i++) verts[i] = m.MultiplyPoint3x4(verts[i]);
            if (verts.Length > 0)
            {
                var b = new Bounds(verts[0], Vector3.zero);
                foreach (var v in verts) b.Encapsulate(v);
                var shift = new Vector3(-b.center.x, 0f, -b.center.z);
                for (int i = 0; i < verts.Length; i++) verts[i] += shift;
            }
            for (int i = 0; i < normals.Length; i++) normals[i] = m.MultiplyVector(normals[i]).normalized;
            for (int i = 0; i < tangents.Length; i++)
            {
                Vector3 t = m.MultiplyVector(tangents[i]).normalized;
                tangents[i] = new Vector4(t.x, t.y, t.z, tangents[i].w);
            }
            mesh.vertices = verts;
            if (normals.Length > 0) mesh.normals = normals;
            if (tangents.Length > 0) mesh.tangents = tangents;
            mesh.RecalculateBounds();
            return SaveMesh(mesh);
        }

        public static ItemDefinition Item(string id, string name, string description, ItemCategory category, Color tint)
        {
            var item = ScriptableObject.CreateInstance<ItemDefinition>();
            item.name = id;
            item.Configure(id, name, description, category, tint);
            return SaveAsset(item, $"{Root}/Data/Items/{id}.asset");
        }
    }
}
