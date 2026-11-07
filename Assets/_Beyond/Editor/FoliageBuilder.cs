using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Beyond.EditorTools
{
    public static class FoliageBuilder
    {
        const string FirDir = BuilderAssets.PolyHaven + "/Foliage/fir_tree_01";
        const string GrassDir = BuilderAssets.PolyHaven + "/Foliage/grass_medium_01";
        const string FernDir = BuilderAssets.PolyHaven + "/Models/fern_02";
        const string ShrubDir = BuilderAssets.PolyHaven + "/Models/shrub_02";

        static readonly Rect[] Twigs =
        {
            new Rect(0.314f, 0.225f, 0.324f, 0.373f), new Rect(0.635f, 0.17f, 0.314f, 0.371f),
            new Rect(0.654f, 0.631f, 0.283f, 0.314f), new Rect(0.193f, 0.688f, 0.236f, 0.266f)
        };

        static readonly Rect[] GrassClumps =
        {
            new Rect(0.217f, 0.096f, 0.279f, 0.156f), new Rect(0.605f, 0.139f, 0.219f, 0.104f),
            new Rect(0.254f, 0.0f, 0.217f, 0.107f), new Rect(0.604f, 0.0f, 0.184f, 0.133f), new Rect(0.059f, 0.0f, 0.123f, 0.129f)
        };

        static readonly Rect TallGrass = new Rect(0.766f, 0.342f, 0.193f, 0.59f);

        static readonly Rect[] Fronds =
        {
            new Rect(0.09f, 0.051f, 0.146f, 0.836f), new Rect(0.314f, 0.236f, 0.141f, 0.738f), new Rect(0.715f, 0.104f, 0.123f, 0.787f),
            new Rect(0.543f, 0.5f, 0.117f, 0.473f), new Rect(0.514f, 0.033f, 0.115f, 0.455f)
        };

        class MeshData
        {
            public readonly List<Vector3> v = new List<Vector3>();
            public readonly List<Vector3> n = new List<Vector3>();
            public readonly List<Vector2> uv = new List<Vector2>();
            public readonly List<int>[] tris;

            public MeshData(int subMeshes)
            {
                tris = new List<int>[subMeshes];
                for (int i = 0; i < subMeshes; i++) tris[i] = new List<int>();
            }

            public int Add(Vector3 pos, Vector3 normal, Vector2 texcoord)
            {
                v.Add(pos);
                n.Add(normal);
                uv.Add(texcoord);
                return v.Count - 1;
            }

            public void Quad(int sub, int a, int b, int c, int d)
            {
                tris[sub].AddRange(new[] { a, b, c, a, c, d });
            }

            public Mesh Build(string name)
            {
                var mesh = new Mesh { name = name, indexFormat = IndexFormat.UInt32 };
                mesh.SetVertices(v);
                mesh.SetNormals(n);
                mesh.SetUVs(0, uv);
                mesh.subMeshCount = tris.Length;
                for (int i = 0; i < tris.Length; i++) mesh.SetTriangles(tris[i], i);
                mesh.RecalculateTangents();
                mesh.RecalculateBounds();
                return BuilderAssets.SaveMesh(mesh);
            }
        }

        public static Texture2D FoliageTexture(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return null;
            if (!importer.alphaIsTransparency || !importer.mipMapsPreserveCoverage)
            {
                importer.alphaIsTransparency = true;
                importer.alphaSource = TextureImporterAlphaSource.FromInput;
                importer.mipmapEnabled = true;
                importer.mipMapsPreserveCoverage = true;
                importer.alphaTestReferenceValue = 0.45f;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.maxTextureSize = 2048;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        public static Material FoliageMaterial(string name, Texture2D albedo, Texture2D normal, Color tint, float cutoff, float normalScale = 1f)
        {
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
            mat.SetTexture("_BaseMap", albedo);
            mat.SetColor("_BaseColor", tint);
            if (normal != null)
            {
                mat.SetTexture("_BumpMap", normal);
                mat.SetFloat("_BumpScale", normalScale);
                mat.EnableKeyword("_NORMALMAP");
            }
            mat.SetFloat("_AlphaClip", 1f);
            mat.SetFloat("_Cutoff", cutoff);
            mat.EnableKeyword("_ALPHATEST_ON");
            mat.SetFloat("_Cull", (float)CullMode.Off);
            mat.SetFloat("_Smoothness", 0.12f);
            mat.SetFloat("_SpecularHighlights", 0f);
            mat.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
            mat.SetFloat("_EnvironmentReflections", 0f);
            mat.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
            mat.SetOverrideTag("RenderType", "TransparentCutout");
            mat.renderQueue = (int)RenderQueue.AlphaTest;
            mat.doubleSidedGI = true;
            mat.enableInstancing = true;
            return BuilderAssets.SaveAsset(mat, $"{BuilderAssets.Generated}/Materials/{name}.mat");
        }

        public static Material BarkMaterial() => BuilderAssets.Lit("Fir Bark", new Color(0.85f, 0.8f, 0.75f), 0.08f, 0f,
            BuilderAssets.LoadTexture($"{FirDir}/fir_tree_01_bark_diff.jpg", false),
            BuilderAssets.LoadTexture($"{FirDir}/fir_tree_01_bark_nor_gl.jpg", true));

        public static Material[] NeedleMaterials()
        {
            var tex = FoliageTexture($"{FirDir}/fir_tree_01_twig_rgba.png");
            var nor = BuilderAssets.LoadTexture($"{FirDir}/fir_tree_01_twig_nor_gl.jpg", true);
            return new[]
            {
                FoliageMaterial("Fir Needles A", tex, nor, new Color(0.6f, 0.7f, 0.52f), 0.45f),
                FoliageMaterial("Fir Needles B", tex, nor, new Color(0.54f, 0.66f, 0.5f), 0.45f),
                FoliageMaterial("Fir Needles C", tex, nor, new Color(0.66f, 0.7f, 0.5f), 0.45f)
            };
        }

        public static Material GrassMaterial(string name, Color tint) => FoliageMaterial(name,
            FoliageTexture($"{GrassDir}/grass_medium_01_rgba.png"),
            BuilderAssets.LoadTexture($"{GrassDir}/grass_medium_01_nor_gl.jpg", true), tint, 0.45f, 0.6f);

        public static Material FernMaterial() => FoliageMaterial("Fern", FoliageTexture($"{FernDir}/fern_02_rgba.png"),
            BuilderAssets.LoadTexture($"{FernDir}/fern_02_nor_gl.jpg", true), new Color(0.8f, 0.9f, 0.75f), 0.45f);

        public static Material ShrubMaterial() => FoliageMaterial("Shrub", FoliageTexture($"{ShrubDir}/shrub_02_rgba.png"),
            BuilderAssets.LoadTexture($"{ShrubDir}/shrub_02_nor_gl.jpg", true), new Color(0.8f, 0.85f, 0.75f), 0.45f);

        public static Mesh FirTree(int seed)
        {
            var r = new System.Random(seed);
            float R() => (float)r.NextDouble();
            var md = new MeshData(3);

            float height = 13f + R() * 7f;
            float width = 0.85f + R() * 0.3f;
            float crownBase = height * (0.1f + R() * 0.12f);

            float TrunkRadius(float y)
            {
                float k = Mathf.Clamp01(y / height);
                float rad = Mathf.Lerp(0.3f, 0.03f, Mathf.Pow(k, 0.75f));
                if (y < 0.6f) rad *= 1f + (0.6f - y) * 0.6f;
                return rad;
            }

            const int sides = 10, rings = 8;
            for (int ring = 0; ring <= rings; ring++)
            {
                float y = Mathf.Lerp(-0.6f, height, ring / (float)rings);
                float rad = TrunkRadius(Mathf.Max(0f, y));
                for (int i = 0; i <= sides; i++)
                {
                    float a = i / (float)sides * Mathf.PI * 2f;
                    var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                    md.Add(dir * rad + Vector3.up * y, dir, new Vector2(i / (float)sides * 2f, y / 2.2f));
                }
            }
            for (int ring = 0; ring < rings; ring++)
            for (int i = 0; i < sides; i++)
            {
                int a = ring * (sides + 1) + i, b = a + 1, c = a + sides + 2, d = a + sides + 1;
                md.tris[0].AddRange(new[] { a, d, c, a, c, b });
            }

            float yLevel = crownBase;
            int tier = 0;
            while (yLevel < height - 0.2f)
            {
                float t = (yLevel - crownBase) / (height - crownBase);
                float crownRadius = Mathf.Lerp(3.1f, 0.3f, Mathf.Pow(t, 0.8f)) * width;
                int count = t < 0.75f ? 6 + r.Next(3) : 4 + r.Next(2);
                float baseAngle = tier * 137.5f;
                for (int b = 0; b < count; b++)
                {
                    float ang = (baseAngle + b * 360f / count + (R() - 0.5f) * 30f) * Mathf.Deg2Rad;
                    var dir = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang));
                    float len = crownRadius * (0.75f + R() * 0.4f);
                    float pitch = (Mathf.Lerp(-16f, 28f, t) + (R() - 0.5f) * 14f) * Mathf.Deg2Rad;
                    var bdir = (dir * Mathf.Cos(pitch) + Vector3.up * Mathf.Sin(pitch)).normalized;
                    var start = new Vector3(0f, yLevel, 0f) + dir * TrunkRadius(yLevel);
                    float tipDroop = len * Mathf.Lerp(0.3f, 0.04f, t);
                    int segs = len > 2.2f ? 3 : len > 1.0f ? 2 : 1;
                    float segLen = len / segs;
                    for (int s = 0; s < segs; s++)
                    {
                        float f0 = s / (float)segs, f1 = (s + 1) / (float)segs;
                        var p0 = start + bdir * (len * f0 * 0.9f) + Vector3.down * (tipDroop * f0 * f0);
                        var p1 = start + bdir * (len * f1 * 0.9f + segLen * 0.35f) + Vector3.down * (tipDroop * f1 * f1);
                        float w = segLen * 1.75f * (1f - 0.2f * f0);
                        float roll = 28f + R() * 22f;
                        Card(md, p0, p1, w, roll, Twigs[r.Next(Twigs.Length)]);
                        Card(md, p0, p1, w * 0.9f, -roll, Twigs[r.Next(Twigs.Length)]);
                        Card(md, p0, p1, w * 1.05f, (R() - 0.5f) * 16f, Twigs[r.Next(2)]);
                    }
                }
                yLevel += Mathf.Lerp(0.42f, 0.26f, t) * (0.85f + R() * 0.3f);
                tier++;
            }

            Core(md, crownBase + 0.6f, height - 0.6f, Mathf.Lerp(3.1f, 0.3f, 0.05f) * width * 0.5f, r);

            var top = new Vector3(0f, height - 0.4f, 0f);
            Card(md, top, top + Vector3.up * 1.1f, 0.8f, 0f, Twigs[0]);
            Card(md, top, top + Vector3.up * 1.1f, 0.8f, 90f, Twigs[1]);

            return md.Build($"FirTree_{seed}");
        }

        static void Core(MeshData md, float y0, float y1, float radius, System.Random r)
        {
            const int sides = 9, rings = 6;
            int start = md.v.Count;
            for (int ring = 0; ring <= rings; ring++)
            {
                float k = ring / (float)rings;
                float y = Mathf.Lerp(y0, y1, k);
                float rad = radius * Mathf.Pow(1f - k, 0.9f) + 0.05f;
                for (int i = 0; i <= sides; i++)
                {
                    float a = i / (float)sides * Mathf.PI * 2f;
                    float jitter = 0.8f + (float)r.NextDouble() * 0.35f;
                    var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                    md.Add(dir * rad * (i == sides ? 1f : jitter) + Vector3.up * y, (dir + Vector3.up * 0.5f).normalized, new Vector2(i / (float)sides, k));
                }
            }
            for (int ring = 0; ring < rings; ring++)
            for (int i = 0; i < sides; i++)
            {
                int a = start + ring * (sides + 1) + i, b = a + 1, c = a + sides + 2, d = a + sides + 1;
                md.tris[2].AddRange(new[] { a, d, c, a, c, b });
            }
        }

        public static Material CoreMaterial() => BuilderAssets.Lit("Fir Crown Core", new Color(0.05f, 0.085f, 0.045f), 0.05f);

        static void Card(MeshData md, Vector3 p0, Vector3 p1, float width, float rollDeg, Rect uv)
        {
            Vector3 axis = (p1 - p0).normalized;
            Vector3 side = Vector3.Cross(Vector3.up, axis);
            if (side.sqrMagnitude < 1e-4f) side = Vector3.right;
            side = Quaternion.AngleAxis(rollDeg, axis) * side.normalized;
            Vector3 h = side * (width * 0.5f);
            Vector3 cardNormal = Vector3.Cross(axis, side).normalized;
            if (cardNormal.y < 0f) cardNormal = -cardNormal;

            Vector3 N(Vector3 p)
            {
                var radial = new Vector3(p.x, 0f, p.z);
                radial = radial.sqrMagnitude > 1e-4f ? radial.normalized : Vector3.zero;
                return (radial * 0.75f + Vector3.up * 0.55f + cardNormal * 0.35f).normalized;
            }

            Vector3 a = p0 - h, b = p0 + h, c = p1 + h, d = p1 - h;
            int ia = md.Add(a, N(a), new Vector2(uv.xMin, uv.yMin));
            int ib = md.Add(b, N(b), new Vector2(uv.xMax, uv.yMin));
            int ic = md.Add(c, N(c), new Vector2(uv.xMax, uv.yMax));
            int id = md.Add(d, N(d), new Vector2(uv.xMin, uv.yMax));
            md.Quad(1, ia, ib, ic, id);
        }

        public static Mesh GrassClump(int seed, bool tall)
        {
            var r = new System.Random(seed);
            var md = new MeshData(1);
            int planes = 3;
            for (int i = 0; i < planes; i++)
            {
                Rect uv = tall ? TallGrass : GrassClumps[r.Next(GrassClumps.Length)];
                float aspect = uv.width / uv.height;
                float h = tall ? 0.85f + (float)r.NextDouble() * 0.25f : 0.38f + (float)r.NextDouble() * 0.18f;
                float w = tall ? h * aspect * 1.6f : h * aspect;
                float ang = (i * 180f / planes + (float)r.NextDouble() * 20f) * Mathf.Deg2Rad;
                var dir = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * (w * 0.5f);
                var offset = new Vector3(((float)r.NextDouble() - 0.5f) * 0.15f, -0.03f, ((float)r.NextDouble() - 0.5f) * 0.15f);
                var lean = new Vector3(((float)r.NextDouble() - 0.5f) * 0.12f, 0f, ((float)r.NextDouble() - 0.5f) * 0.12f);
                int a = md.Add(offset - dir, Vector3.up, new Vector2(uv.xMin, uv.yMin));
                int b = md.Add(offset + dir, Vector3.up, new Vector2(uv.xMax, uv.yMin));
                int c = md.Add(offset + dir + Vector3.up * h + lean, Vector3.up, new Vector2(uv.xMax, uv.yMax));
                int d = md.Add(offset - dir + Vector3.up * h + lean, Vector3.up, new Vector2(uv.xMin, uv.yMax));
                md.Quad(0, a, b, c, d);
            }
            return md.Build($"Grass_{(tall ? "Tall" : "Clump")}_{seed}");
        }

        public static Mesh Fern(int seed)
        {
            var r = new System.Random(seed);
            var md = new MeshData(1);
            int fronds = 7 + r.Next(4);
            for (int i = 0; i < fronds; i++)
            {
                Rect uv = Fronds[r.Next(Fronds.Length)];
                float len = 0.55f + (float)r.NextDouble() * 0.45f;
                float w = len * (uv.width / uv.height) * 1.1f;
                float ang = (i * 360f / fronds + ((float)r.NextDouble() - 0.5f) * 25f) * Mathf.Deg2Rad;
                var dir = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang));
                var side = Vector3.Cross(Vector3.up, dir) * (w * 0.5f);
                float rise = 0.55f + (float)r.NextDouble() * 0.3f;
                Vector3 p0 = Vector3.zero;
                Vector3 p1 = dir * (len * 0.45f) + Vector3.up * (len * rise * 0.7f);
                Vector3 p2 = dir * (len * 0.95f) + Vector3.up * (len * rise * 0.45f);
                Vector3 n0 = (Vector3.up * 0.8f + dir * 0.3f).normalized;
                float vMid = uv.yMin + uv.height * 0.5f;
                int a = md.Add(p0 - side * 0.4f, n0, new Vector2(uv.xMin, uv.yMin));
                int b = md.Add(p0 + side * 0.4f, n0, new Vector2(uv.xMax, uv.yMin));
                int c = md.Add(p1 + side, n0, new Vector2(uv.xMax, vMid));
                int d = md.Add(p1 - side, n0, new Vector2(uv.xMin, vMid));
                int e = md.Add(p2 + side * 0.7f, n0, new Vector2(uv.xMax, uv.yMax));
                int f = md.Add(p2 - side * 0.7f, n0, new Vector2(uv.xMin, uv.yMax));
                md.Quad(0, a, b, c, d);
                md.Quad(0, d, c, e, f);
            }
            return md.Build($"Fern_{seed}");
        }

        public static GameObject DetailPrefab(string name, Mesh mesh, Material mat)
        {
            string path = $"{BuilderAssets.Generated}/Prefabs/{name}.prefab";
            BuilderAssets.EnsureFolder($"{BuilderAssets.Generated}/Prefabs");
            var go = new GameObject(name);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return prefab;
        }
    }
}
