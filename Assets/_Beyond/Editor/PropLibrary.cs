using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Beyond.EditorTools
{
    public static class PropLibrary
    {
        const string PropsDir = BuilderAssets.PolyHaven + "/Props";

        static readonly Dictionary<string, string> Alias = new Dictionary<string, string>
        {
            { "military_crate_m_01", "old_military_crate" },
            { "wooden_picnic_table_table", "wooden_picnic_table_top" }
        };

        public class PropMesh
        {
            public Mesh body;
            public Material[] bodyMaterials;
            public Mesh lid;
            public Material[] lidMaterials;
            public Vector3 lidHinge;
            public Bounds bounds;
        }

        static readonly Dictionary<string, PropMesh> cache = new Dictionary<string, PropMesh>();
        static readonly Dictionary<string, Material> materialCache = new Dictionary<string, Material>();

        public static void ClearCache()
        {
            cache.Clear();
            materialCache.Clear();
        }

        public static PropMesh Load(string asset, Func<string, bool> include = null, Func<string, bool> lid = null, string variant = "")
        {
            string key = asset + variant;
            if (cache.TryGetValue(key, out var cached)) return cached;

            string path = $"{PropsDir}/{asset}/{asset}.fbx";
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null) return null;
            if (importer.useFileScale || !importer.bakeAxisConversion ||
                importer.materialImportMode != ModelImporterMaterialImportMode.ImportViaMaterialDescription)
            {
                importer.useFileScale = false;
                importer.bakeAxisConversion = true;
                importer.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
                importer.SaveAndReimport();
            }

            var root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var bodyGroups = new Dictionary<string, List<CombineInstance>>();
            var lidGroups = new Dictionary<string, List<CombineInstance>>();
            float scale = 0f;

            foreach (var mf in root.GetComponentsInChildren<MeshFilter>(true))
            {
                var mesh = mf.sharedMesh;
                var mr = mf.GetComponent<MeshRenderer>();
                if (mesh == null || mr == null || mesh.bounds.size.sqrMagnitude < 1e-10f) continue;
                if (include != null && !include(mf.name)) continue;
                if (scale <= 0f) scale = 1f / Mathf.Max(1e-4f, mf.transform.lossyScale.x);
                var matrix = Matrix4x4.Scale(Vector3.one * scale) * mf.transform.localToWorldMatrix;
                var target = lid != null && lid(mf.name) ? lidGroups : bodyGroups;
                for (int s = 0; s < mesh.subMeshCount && s < mr.sharedMaterials.Length; s++)
                {
                    var src = mr.sharedMaterials[s];
                    string matName = src != null ? src.name : "default";
                    if (matName == "Lit") continue;
                    if (!target.TryGetValue(matName, out var list)) target[matName] = list = new List<CombineInstance>();
                    list.Add(new CombineInstance { mesh = mesh, subMeshIndex = s, transform = matrix });
                }
            }

            var body = Combine(bodyGroups, out var bodyNames);
            Mesh lidMesh = lidGroups.Count > 0 ? Combine(lidGroups, out var lidNames) : null;

            var b = body.bounds;
            if (lidMesh != null) b.Encapsulate(lidMesh.bounds);
            var shift = new Vector3(-b.center.x, -b.min.y, -b.center.z);
            Shift(body, shift);
            Vector3 hinge = Vector3.zero;
            if (lidMesh != null)
            {
                Shift(lidMesh, shift);
                var lb = lidMesh.bounds;
                hinge = new Vector3(lb.center.x, lb.min.y, lb.max.z);
                Shift(lidMesh, -hinge);
            }
            b.center += shift;

            body.name = $"Prop_{asset}{variant}";
            var result = new PropMesh
            {
                body = BuilderAssets.SaveMesh(body),
                bodyMaterials = bodyNames.Select(n => MaterialFor(asset, n)).ToArray(),
                bounds = new Bounds(new Vector3(0f, b.size.y * 0.5f, 0f), b.size),
                lidHinge = hinge
            };
            if (lidMesh != null)
            {
                lidMesh.name = $"Prop_{asset}{variant}_Lid";
                result.lid = BuilderAssets.SaveMesh(lidMesh);
                result.lidMaterials = lidGroups.Keys.Select(n => MaterialFor(asset, n)).ToArray();
            }
            cache[key] = result;
            return result;
        }

        static Mesh Combine(Dictionary<string, List<CombineInstance>> groups, out List<string> names)
        {
            names = new List<string>();
            var parts = new List<CombineInstance>();
            foreach (var g in groups)
            {
                var merged = new Mesh { indexFormat = IndexFormat.UInt32 };
                merged.CombineMeshes(g.Value.ToArray(), true, true);
                parts.Add(new CombineInstance { mesh = merged, transform = Matrix4x4.identity });
                names.Add(g.Key);
            }
            var mesh = new Mesh { indexFormat = IndexFormat.UInt32 };
            mesh.CombineMeshes(parts.ToArray(), false, false);
            foreach (var p in parts) Object.DestroyImmediate(p.mesh);
            mesh.RecalculateBounds();
            return mesh;
        }

        static void Shift(Mesh mesh, Vector3 offset)
        {
            var verts = mesh.vertices;
            for (int i = 0; i < verts.Length; i++) verts[i] += offset;
            mesh.vertices = verts;
            mesh.RecalculateBounds();
        }

        static Material MaterialFor(string asset, string materialName)
        {
            string prefix = Alias.TryGetValue(materialName, out var alias) ? alias : materialName;
            string key = asset + "/" + prefix;
            if (materialCache.TryGetValue(key, out var m)) return m;

            string dir = $"{PropsDir}/{asset}";
            var diff = AssetDatabase.LoadAssetAtPath<Texture2D>($"{dir}/{prefix}_diff.jpg");
            if (diff == null && prefix.Contains("glass"))
                m = BuilderAssets.Lit($"Prop {prefix}", new Color(0.12f, 0.14f, 0.15f), 0.92f, 0f);
            else if (diff == null)
                m = BuilderAssets.Lit($"Prop {prefix}", new Color(0.4f, 0.4f, 0.4f), 0.3f, 0f);
            else
                m = Pbr($"Prop {prefix}", dir, prefix, Color.white);
            materialCache[key] = m;
            return m;
        }

        public static Material Pbr(string name, string dir, string prefix, Color tint, Vector2? tiling = null)
        {
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
            mat.SetColor("_BaseColor", tint);
            mat.SetTexture("_BaseMap", BuilderAssets.LoadTexture($"{dir}/{prefix}_diff.jpg", false));
            var nor = BuilderAssets.LoadTexture($"{dir}/{prefix}_nor_gl.jpg", true);
            if (nor != null)
            {
                mat.SetTexture("_BumpMap", nor);
                mat.EnableKeyword("_NORMALMAP");
            }
            var ms = LinearTexture($"{dir}/{prefix}_ms.png");
            if (ms != null)
            {
                mat.SetTexture("_MetallicGlossMap", ms);
                mat.EnableKeyword("_METALLICSPECGLOSSMAP");
                mat.SetFloat("_Smoothness", 1f);
                mat.SetFloat("_SmoothnessTextureChannel", 0f);
            }
            else mat.SetFloat("_Smoothness", 0.2f);
            var ao = LinearTexture($"{dir}/{prefix}_ao.jpg");
            if (ao != null)
            {
                mat.SetTexture("_OcclusionMap", ao);
                mat.SetFloat("_OcclusionStrength", 1f);
                mat.EnableKeyword("_OCCLUSIONMAP");
            }
            if (tiling.HasValue)
            {
                foreach (var prop in new[] { "_BaseMap", "_BumpMap", "_MetallicGlossMap", "_OcclusionMap" })
                    mat.SetTextureScale(prop, tiling.Value);
            }
            mat.enableInstancing = true;
            return BuilderAssets.SaveAsset(mat, $"{BuilderAssets.Generated}/Materials/{name}.mat");
        }

        static Texture2D LinearTexture(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return null;
            if (importer.sRGBTexture)
            {
                importer.sRGBTexture = false;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        public static GameObject Spawn(PropMesh prop, string name, Transform parent, out Transform lidHinge, bool collider = true)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = prop.body;
            go.AddComponent<MeshRenderer>().sharedMaterials = prop.bodyMaterials;
            lidHinge = null;
            if (prop.lid != null)
            {
                lidHinge = new GameObject("Lid Hinge").transform;
                lidHinge.SetParent(go.transform, false);
                lidHinge.localPosition = prop.lidHinge;
                var lid = new GameObject("Lid");
                lid.transform.SetParent(lidHinge, false);
                lid.AddComponent<MeshFilter>().sharedMesh = prop.lid;
                lid.AddComponent<MeshRenderer>().sharedMaterials = prop.lidMaterials;
            }
            if (collider)
            {
                var box = go.AddComponent<BoxCollider>();
                box.center = prop.bounds.center;
                box.size = prop.bounds.size;
            }
            return go;
        }

        public static GameObject Spawn(PropMesh prop, string name, Transform parent, bool collider = true) =>
            Spawn(prop, name, parent, out _, collider);
    }
}
