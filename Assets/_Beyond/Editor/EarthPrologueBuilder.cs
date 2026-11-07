using System.Collections.Generic;
using System.Linq;
using Beyond.Interaction;
using Beyond.Items;
using Beyond.Player;
using Beyond.UI;
using Beyond.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Beyond.EditorTools
{
    public static class EarthPrologueBuilder
    {
        public const string ScenePath = BuilderAssets.Root + "/Scenes/Earth_Prologue.unity";

        const float Size = 320f;
        const float MaxHeight = 60f;
        const int HeightRes = 513;
        const int SplatRes = 512;
        const int DetailRes = 640;
        const float CampRadius = 16f;
        const int IgnoreRaycastLayer = 2;

        static readonly Vector2[] Trail =
        {
            new Vector2(40f, 36f), new Vector2(68f, 64f), new Vector2(94f, 108f), new Vector2(138f, 126f),
            new Vector2(176f, 150f), new Vector2(202f, 194f), new Vector2(234f, 224f)
        };

        static readonly Vector2 Camp = new Vector2(244f, 236f);

        static Terrain terrain;
        static float[,,] splat;
        static System.Random rng;
        static readonly List<(Vector2 centre, float radius)> clearings = new List<(Vector2, float)>();
        static readonly Dictionary<string, Material> materials = new Dictionary<string, Material>();
        static readonly Dictionary<string, ItemDefinition> items = new Dictionary<string, ItemDefinition>();

        [MenuItem("Beyond/Build Earth Prologue Scene", priority = 1)]
        public static void BuildMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Build();
        }

        [MenuItem("Beyond/Open Earth Prologue Scene", priority = 2)]
        public static void OpenMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null) Build();
            else EditorSceneManager.OpenScene(ScenePath);
        }

        [MenuItem("Beyond/Play Earth Prologue", priority = 3)]
        public static void PlayMenu()
        {
            OpenMenu();
            EditorApplication.isPlaying = true;
        }

        public static void Build()
        {
            rng = new System.Random(1971);
            clearings.Clear();
            materials.Clear();
            items.Clear();
            PropLibrary.ClearCache();
            BuilderAssets.ResetCache();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            ConfigurePipeline();
            CreateItems();
            var sun = BuildLighting();
            BuildTerrain();

            var world = new GameObject("World").transform;
            var gameplay = new GameObject("Gameplay").transform;

            BuildCamp(world, gameplay, out var zone, out var radio, out var transmission);
            BuildTrail(world, gameplay);
            BuildForest(world);
            BuildRocks(world);
            BuildUndergrowth(world);
            PaintDetails();

            var cam = BuildCamera();
            BuildPlayer(cam);

            var hud = new GameObject("HUD");
            hud.AddComponent<GameHud>();
            hud.AddComponent<NavigationHud>();

            var director = new GameObject("Earth Prologue Director").AddComponent<EarthPrologueDirector>();
            director.Configure(zone, radio, transmission);

            RenderSettings.sun = sun;

            BuilderAssets.EnsureFolder(BuilderAssets.Root + "/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);
            BakeEnvironment(scene);
            AddToBuildSettings();
            AssetDatabase.SaveAssets();
            Debug.Log($"[Beyond] Earth prologue scene built at {ScenePath}");
        }

        static void ConfigurePipeline()
        {
            var urp = (QualitySettings.renderPipeline ?? GraphicsSettings.defaultRenderPipeline) as UniversalRenderPipelineAsset;
            if (urp == null) return;
            urp.shadowDistance = 130f;
            urp.shadowCascadeCount = 4;
            urp.cascade4Split = new Vector3(0.05f, 0.14f, 0.38f);
            var so = new SerializedObject(urp);
            var res = so.FindProperty("m_MainLightShadowmapResolution");
            if (res != null) res.intValue = 4096;
            var soft = so.FindProperty("m_SoftShadowQuality");
            if (soft != null) soft.intValue = 3;
            var hdr = so.FindProperty("m_SupportsHDR");
            if (hdr != null) hdr.boolValue = true;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(urp);
        }

        static void BakeEnvironment(UnityEngine.SceneManagement.Scene scene)
        {
            string path = $"{BuilderAssets.Generated}/Settings/EarthPrologue_Lighting.lighting";
            AssetDatabase.DeleteAsset(path);
            var settings = new LightingSettings { name = "EarthPrologue_Lighting", bakedGI = false, realtimeGI = false };
            AssetDatabase.CreateAsset(settings, path);
            Lightmapping.SetLightingSettingsForScene(scene, settings);
            Lightmapping.Bake();
            EditorSceneManager.SaveScene(scene, ScenePath);
        }

        static void AddToBuildSettings()
        {
            var scenes = EditorBuildSettings.scenes.Where(s => s.path != ScenePath).ToList();
            scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        static void CreateItems()
        {
            AddItem("scrap_metal", "Scrap Metal", "Bent aluminium panels. Useful raw material.", ItemCategory.Resource, new Color(0.55f, 0.56f, 0.58f));
            AddItem("copper_wire", "Copper Wire", "A spool of insulated copper wire.", ItemCategory.Component, new Color(0.85f, 0.45f, 0.2f));
            AddItem("battery_cell", "Battery Cell", "Heavy rechargeable field battery. Still holds a charge.", ItemCategory.Component, new Color(0.95f, 0.75f, 0.15f));
            AddItem("circuit_board", "Radio Circuit Board", "Transmitter board pulled from the camp radio.", ItemCategory.Component, new Color(0.1f, 0.5f, 0.25f));
            AddItem("multitool", "Field Multitool", "Pliers, screwdrivers and a blade. Every engineer's best friend.", ItemCategory.Tool, new Color(0.8f, 0.12f, 0.1f));
            AddItem("meridian_badge", "Meridian Insignia", "A brass patch: a crescent moon over a rising arrow.", ItemCategory.Artifact, new Color(0.85f, 0.68f, 0.3f));
            AddItem("leather_boots", "Leather Boots", "Sturdy black boots. Your feet will thank you.", ItemCategory.Clothing, new Color(0.08f, 0.07f, 0.06f));
            AddItem("field_jeans", "Field Jeans", "Worn denim with deep pockets.", ItemCategory.Clothing, new Color(0.22f, 0.34f, 0.52f));
            AddItem("field_jacket", "Field Jacket", "An olive expedition jacket over a flannel shirt.", ItemCategory.Clothing, new Color(0.22f, 0.26f, 0.2f));
        }

        static void AddItem(string id, string name, string desc, ItemCategory cat, Color tint) =>
            items[id] = BuilderAssets.Item(id, name, desc, cat, tint);

        static Material Mat(string name, Color color, float smoothness = 0.2f, float metallic = 0f, Color? emission = null)
        {
            if (materials.TryGetValue(name, out var m)) return m;
            m = BuilderAssets.Lit(name, color, smoothness, metallic, null, null, emission);
            materials[name] = m;
            return m;
        }

        static Material PhMat(string folder, string asset, float smoothness = 0.15f)
        {
            string key = "PH_" + asset;
            if (materials.TryGetValue(key, out var m)) return m;
            m = BuilderAssets.PolyHavenLit(folder, asset, smoothness);
            materials[key] = m;
            return m;
        }

        static Light BuildLighting()
        {
            var sunGo = new GameObject("Sun");
            var sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 2.4f;
            sun.color = new Color(1f, 0.92f, 0.8f);
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.92f;
            sun.shadowNormalBias = 0.6f;
            sunGo.transform.rotation = Quaternion.Euler(31f, 212f, 0f);

            var hdr = BuilderAssets.LoadTexture($"{BuilderAssets.PolyHaven}/HDRI/kloofendal_48d_partly_cloudy_puresky.hdr", false, 4096);
            var sky = new Material(Shader.Find("Skybox/Panoramic")) { name = "Sky_Kloofendal" };
            sky.SetTexture("_MainTex", hdr);
            sky.SetFloat("_Mapping", 1f);
            sky.SetFloat("_ImageType", 0f);
            sky.SetFloat("_Exposure", 1.05f);
            sky.SetFloat("_Rotation", 40f);
            sky = BuilderAssets.SaveAsset(sky, $"{BuilderAssets.Generated}/Materials/Sky_Kloofendal.mat");
            RenderSettings.skybox = sky;

            RenderSettings.ambientMode = AmbientMode.Skybox;
            RenderSettings.ambientIntensity = 1.0f;
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Skybox;
            RenderSettings.reflectionIntensity = 0.8f;

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.0055f;
            RenderSettings.fogColor = new Color(0.64f, 0.72f, 0.8f);

            BuildPostProcessing();
            return sun;
        }

        static void BuildPostProcessing()
        {
            string path = $"{BuilderAssets.Generated}/Settings/EarthPrologue_Volume.asset";
            BuilderAssets.EnsureFolder($"{BuilderAssets.Generated}/Settings");
            AssetDatabase.DeleteAsset(path);
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, path);

            var tone = profile.Add<Tonemapping>(true);
            tone.mode.Override(TonemappingMode.ACES);
            var bloom = profile.Add<Bloom>(true);
            bloom.threshold.Override(1.2f);
            bloom.intensity.Override(0.25f);
            bloom.scatter.Override(0.65f);
            var color = profile.Add<ColorAdjustments>(true);
            color.postExposure.Override(0.2f);
            color.contrast.Override(16f);
            color.saturation.Override(-4f);
            color.colorFilter.Override(Color.white);
            color.hueShift.overrideState = false;
            var white = profile.Add<WhiteBalance>(true);
            white.temperature.Override(4f);
            white.tint.Override(2f);
            var smh = profile.Add<ShadowsMidtonesHighlights>(true);
            smh.shadows.Override(new Vector4(0.96f, 1f, 1.06f, 0f));
            smh.highlights.Override(new Vector4(1.04f, 1f, 0.95f, 0f));
            var vignette = profile.Add<Vignette>(true);
            vignette.intensity.Override(0.2f);
            vignette.smoothness.Override(0.5f);

            foreach (var c in profile.components) AssetDatabase.AddObjectToAsset(c, profile);
            EditorUtility.SetDirty(profile);

            var volumeGo = new GameObject("Post Processing");
            var volume = volumeGo.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = profile;
        }

        static void BuildTerrain()
        {
            string dir = $"{BuilderAssets.Generated}/Terrain";
            BuilderAssets.EnsureFolder(dir);
            string dataPath = $"{dir}/EarthPrologue_TerrainData.asset";
            AssetDatabase.DeleteAsset(dataPath);

            var data = new TerrainData { heightmapResolution = HeightRes };
            data.size = new Vector3(Size, MaxHeight, Size);
            AssetDatabase.CreateAsset(data, dataPath);
            var heights = new float[HeightRes, HeightRes];
            for (int z = 0; z < HeightRes; z++)
            for (int x = 0; x < HeightRes; x++)
                heights[z, x] = HeightAt(x / (float)(HeightRes - 1) * Size, z / (float)(HeightRes - 1) * Size);
            data.SetHeights(0, 0, heights);

            data.terrainLayers = new[]
            {
                Layer("leafy_grass", 4f, new Color(0.78f, 0.92f, 0.62f)), Layer("forest_leaves_02", 3f), Layer("dirt_floor", 3f, new Color(0.62f, 0.55f, 0.47f)), Layer("mossy_rock", 6f)
            };
            data.alphamapResolution = SplatRes;

            var go = Terrain.CreateTerrainGameObject(data);
            go.name = "Terrain";
            go.isStatic = true;
            terrain = go.GetComponent<Terrain>();
            terrain.heightmapPixelError = 3f;
            terrain.basemapDistance = 600f;
            terrain.drawInstanced = true;
            var terrainShader = Shader.Find("Universal Render Pipeline/Terrain/Lit");
            if (terrainShader != null)
                terrain.materialTemplate = BuilderAssets.SaveAsset(new Material(terrainShader) { name = "Terrain_Lit" },
                    $"{BuilderAssets.Generated}/Materials/Terrain_Lit.mat");

            PaintSplat(data);
            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssetIfDirty(data);
        }

        static TerrainLayer Layer(string asset, float tile, Color? tint = null)
        {
            string dir = $"{BuilderAssets.PolyHaven}/Textures/{asset}";
            var layer = new TerrainLayer
            {
                name = "TL_" + asset,
                diffuseTexture = BuilderAssets.LoadTexture($"{dir}/{asset}_diff.jpg", false),
                normalMapTexture = BuilderAssets.LoadTexture($"{dir}/{asset}_nor_gl.jpg", true),
                tileSize = new Vector2(tile, tile),
                normalScale = 1f,
                smoothness = 0.05f,
                diffuseRemapMax = tint.HasValue ? (Vector4)tint.Value : Vector4.one
            };
            return BuilderAssets.SaveAsset(layer, $"{BuilderAssets.Generated}/Terrain/TL_{asset}.terrainlayer");
        }

        static void PaintSplat(TerrainData data)
        {
            var maps = new float[SplatRes, SplatRes, 4];
            for (int z = 0; z < SplatRes; z++)
            for (int x = 0; x < SplatRes; x++)
            {
                float wx = (x + 0.5f) / SplatRes * Size;
                float wz = (z + 0.5f) / SplatRes * Size;
                float n = Mathf.PerlinNoise(wx * 0.11f + 3f, wz * 0.11f + 7f);
                float steep = data.GetSteepness(wx / Size, wz / Size);

                float rock = Smooth(24f, 36f, steep + n * 6f);
                float dirt = Mathf.Max(1f - Smooth(0.7f, 2.1f, TrailDistance(wx, wz) + (n - 0.5f) * 1.6f),
                    1f - Smooth(CampRadius * 0.2f, CampRadius * 0.5f, Vector2.Distance(new Vector2(wx, wz), Camp) + (n - 0.5f) * 5f));
                float leavesMask = Smooth(0.42f, 0.62f, Mathf.PerlinNoise(wx * 0.025f + 11f, wz * 0.025f + 5f));

                float rest = 1f - rock;
                float d = dirt * rest;
                rest -= d;
                float l = leavesMask * rest;
                float g = rest - l;

                maps[z, x, 0] = g;
                maps[z, x, 1] = l;
                maps[z, x, 2] = d;
                maps[z, x, 3] = rock;
            }
            data.SetAlphamaps(0, 0, maps);
            splat = maps;
        }

        static void PaintDetails()
        {
            var data = terrain.terrainData;
            var grassA = FoliageBuilder.GrassMaterial("Grass A", new Color(0.82f, 0.9f, 0.72f));
            var grassB = FoliageBuilder.GrassMaterial("Grass B", new Color(0.92f, 0.9f, 0.68f));
            var fern = FoliageBuilder.FernMaterial();

            var prototypes = new[]
            {
                Detail(FoliageBuilder.DetailPrefab("Detail_GrassA", FoliageBuilder.GrassClump(3, false), grassA), 0.8f, 1.3f),
                Detail(FoliageBuilder.DetailPrefab("Detail_GrassB", FoliageBuilder.GrassClump(8, false), grassB), 0.8f, 1.25f),
                Detail(FoliageBuilder.DetailPrefab("Detail_GrassTall", FoliageBuilder.GrassClump(5, true), grassA), 0.75f, 1.2f),
                Detail(FoliageBuilder.DetailPrefab("Detail_FernA", FoliageBuilder.Fern(2), fern), 0.7f, 1.3f),
                Detail(FoliageBuilder.DetailPrefab("Detail_FernB", FoliageBuilder.Fern(9), fern), 0.7f, 1.2f)
            };
            data.SetDetailScatterMode(DetailScatterMode.InstanceCountMode);
            data.SetDetailResolution(DetailRes, 32);
            data.detailPrototypes = prototypes;

            var layers = new int[prototypes.Length][,];
            for (int i = 0; i < layers.Length; i++) layers[i] = new int[DetailRes, DetailRes];
            var r = new System.Random(77);
            for (int z = 0; z < DetailRes; z++)
            for (int x = 0; x < DetailRes; x++)
            {
                int sx = Mathf.Min(SplatRes - 1, x * SplatRes / DetailRes);
                int sz = Mathf.Min(SplatRes - 1, z * SplatRes / DetailRes);
                float grass = splat[sz, sx, 0];
                float leaves = splat[sz, sx, 1];
                float wx = (x + 0.5f) / DetailRes * Size, wz = (z + 0.5f) / DetailRes * Size;
                float patch = Mathf.PerlinNoise(wx * 0.08f + 19f, wz * 0.08f + 41f);
                float tallPatch = Mathf.PerlinNoise(wx * 0.05f + 3f, wz * 0.05f + 77f);
                if (Vector2.Distance(new Vector2(wx, wz), Camp) < CampRadius * 0.4f) continue;

                float lush = grass + leaves * 0.35f;
                float grassCount = lush * (0.5f + patch * 1.9f);
                int total = Mathf.FloorToInt(grassCount + (float)r.NextDouble());
                for (int i = 0; i < total; i++)
                {
                    double pick = r.NextDouble();
                    int layer = tallPatch > 0.6f && pick < 0.35 ? 2 : pick < 0.6 ? 0 : 1;
                    layers[layer][z, x]++;
                }
                float fernChance = leaves * 0.22f * Smooth(0.35f, 0.7f, patch) + grass * 0.015f;
                if (r.NextDouble() < fernChance) layers[r.NextDouble() < 0.5 ? 3 : 4][z, x] = 1;
            }
            for (int i = 0; i < layers.Length; i++) data.SetDetailLayer(0, 0, i, layers[i]);

            terrain.detailObjectDistance = 85f;
            terrain.detailObjectDensity = 1f;
            EditorUtility.SetDirty(data);
        }

        static DetailPrototype Detail(GameObject prefab, float min, float max) => new DetailPrototype
        {
            prototype = prefab,
            usePrototypeMesh = true,
            useInstancing = true,
            renderMode = DetailRenderMode.VertexLit,
            minWidth = min,
            maxWidth = max,
            minHeight = min,
            maxHeight = max,
            noiseSpread = 0.4f,
            positionJitter = 1f,
            alignToGround = 0.25f
        };

        static float HeightAt(float x, float z)
        {
            float hills = Mathf.Pow(Fbm(x * 0.007f + 13f, z * 0.007f + 29f, 4), 1.7f) * 0.26f;
            float detail = (Fbm(x * 0.05f + 71f, z * 0.05f + 3f, 3) - 0.5f) * 0.015f;
            float mask = Mathf.Min(Smooth(5f, 30f, TrailDistance(x, z)),
                Smooth(CampRadius, CampRadius + 26f, Vector2.Distance(new Vector2(x, z), Camp)));
            float baseH = 0.06f + Mathf.PerlinNoise(x * 0.0035f + 5f, z * 0.0035f + 9f) * 0.05f;

            float edgeDist = Mathf.Min(Mathf.Min(x, z), Mathf.Min(Size - x, Size - z));
            float edge = Mathf.Clamp01((48f - edgeDist) / 46f);
            edge = edge * edge * (0.55f + 0.45f * Fbm(x * 0.02f, z * 0.02f, 3));

            return baseH + hills * Mathf.Lerp(0.12f, 1f, mask) + detail * Mathf.Lerp(0.3f, 1f, mask) + edge * 0.42f;
        }

        static float Fbm(float x, float z, int octaves)
        {
            float sum = 0f, amp = 1f, norm = 0f, freq = 1f;
            for (int i = 0; i < octaves; i++)
            {
                sum += Mathf.PerlinNoise(x * freq, z * freq) * amp;
                norm += amp;
                amp *= 0.5f;
                freq *= 2.03f;
            }
            return sum / norm;
        }

        static float Smooth(float a, float b, float v)
        {
            float t = Mathf.Clamp01((v - a) / (b - a));
            return t * t * (3f - 2f * t);
        }

        static float TrailDistance(float x, float z)
        {
            var p = new Vector2(x, z);
            float best = float.MaxValue;
            for (int i = 0; i < Trail.Length - 1; i++)
            {
                Vector2 a = Trail[i], b = Trail[i + 1], ab = b - a;
                float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
                best = Mathf.Min(best, Vector2.Distance(p, a + ab * t));
            }
            return best;
        }

        static Vector2 TrailSide(int i, float side)
        {
            Vector2 prev = Trail[Mathf.Max(0, i - 1)], next = Trail[Mathf.Min(Trail.Length - 1, i + 1)];
            Vector2 dir = (next - prev).normalized;
            return Trail[i] + new Vector2(dir.y, -dir.x) * side;
        }

        static float Ground(Vector2 p) => terrain.SampleHeight(new Vector3(p.x, 0f, p.y)) + terrain.transform.position.y;

        static Vector3 OnGround(Vector2 p, float lift = 0f) => new Vector3(p.x, Ground(p) + lift, p.y);

        static float Steepness(Vector2 p) => terrain.terrainData.GetSteepness(p.x / Size, p.y / Size);

        static float Rand(float min, float max) => min + (float)rng.NextDouble() * (max - min);

        static bool InClearing(Vector2 p, float pad = 0f)
        {
            foreach (var c in clearings)
                if (Vector2.Distance(p, c.centre) < c.radius + pad) return true;
            return false;
        }

        static GameObject Primitive(PrimitiveType type, string name, Transform parent, Vector3 localPos, Vector3 scale, Material mat,
            Quaternion? localRot = null, bool keepCollider = false)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = localRot ?? Quaternion.identity;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            if (!keepCollider) Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }

        static GameObject MeshObject(string name, Transform parent, Mesh mesh, Material[] mats, bool meshCollider)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterials = mats;
            if (meshCollider) go.AddComponent<MeshCollider>().sharedMesh = mesh;
            go.isStatic = true;
            return go;
        }

        static GameObject PlaceModel(string name, Transform parent, Mesh mesh, Material mat, Vector2 p, float yaw, float scale, float sink, bool collider)
        {
            var go = MeshObject(name, parent, mesh, new[] { mat }, collider);
            go.transform.localScale = Vector3.one * scale;
            go.transform.SetPositionAndRotation(OnGround(p, -mesh.bounds.min.y * scale - sink), Quaternion.Euler(0f, yaw, 0f));
            return go;
        }

        static Transform Anchor(string name, Transform parent, Vector2 p, float yaw, float lift = 0f)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.SetPositionAndRotation(OnGround(p, lift), Quaternion.Euler(0f, yaw, 0f));
            return t;
        }

        static GameObject BuildCamp(Transform world, Transform gameplay, out ZoneTrigger zone, out RepairableDevice radio, out JournalNote transmission)
        {
            clearings.Add((Camp, CampRadius + 6f));
            var camp = new GameObject("Meridian Camp").transform;
            camp.SetParent(world, false);
            camp.position = OnGround(Camp);

            var stone = PhMat("Models", "stone_fire_pit", 0.2f);
            var pit = BuilderAssets.ModelMeshes("stone_fire_pit");
            if (pit.Length > 0)
            {
                foreach (var m in pit) PlaceModel("Fire Pit", camp, m, stone, Camp, 0f, 1f, 0.02f, true);
            }
            var fireLight = new GameObject("Fire Light").AddComponent<Light>();
            fireLight.transform.SetParent(camp, false);
            fireLight.transform.position = OnGround(Camp, 0.6f);
            fireLight.type = LightType.Point;
            fireLight.color = new Color(1f, 0.55f, 0.22f);
            fireLight.intensity = 3f;
            fireLight.range = 7f;
            var ember = Primitive(PrimitiveType.Sphere, "Embers", camp, Vector3.zero, new Vector3(0.5f, 0.12f, 0.5f),
                Mat("Embers", new Color(0.15f, 0.05f, 0.02f), 0.1f, 0f, new Color(2.5f, 0.7f, 0.15f)));
            ember.transform.position = OnGround(Camp, 0.08f);
            fireLight.gameObject.AddComponent<LightAnimator>().Configure(LightAnimator.Mode.Flicker, fireLight,
                ember.GetComponent<Renderer>(), new Color(2.5f, 0.7f, 0.15f), 1f);

            var logMeshes = BuilderAssets.ModelMeshes("dead_tree_trunk");
            var logMat = PhMat("Models", "dead_tree_trunk");
            for (int i = 0; i < 3 && logMeshes.Length > 0; i++)
            {
                float a = i * 120f + 20f;
                Vector2 lp = Camp + new Vector2(Mathf.Sin(a * Mathf.Deg2Rad), Mathf.Cos(a * Mathf.Deg2Rad)) * 2.5f;
                PlaceModel("Log Seat", camp, logMeshes[0], logMat, lp, a, 0.72f, 0.03f, true);
            }
            var hatchet = PropLibrary.Spawn(PropLibrary.Load("hatchet"), "Hatchet", camp, false);
            hatchet.transform.SetPositionAndRotation(OnGround(Camp + new Vector2(1.6f, -1.9f), 0.01f), Quaternion.Euler(0f, 35f, 90f));

            var tent = Anchor("Tent", camp, Camp + new Vector2(-7f, 5f), 120f);
            var tentMesh = BuilderAssets.SaveMesh(TentMesh(2.4f, 1.65f, 3f));
            var canvas = PropLibrary.Pbr("Tent Canvas", $"{BuilderAssets.PolyHaven}/Textures/rough_linen", "rough_linen",
                new Color(0.62f, 0.64f, 0.46f), new Vector2(1.6f, 1.6f));
            MeshObject("Canvas", tent, tentMesh, new[] { canvas }, true);

            var hale = Anchor("Hale's Case", tent, Vector2.zero, 0f);
            hale.localPosition = new Vector3(0.25f, 0f, 0.55f);
            hale.localRotation = Quaternion.Euler(0f, 170f, 0f);
            LootProp("Hale's Equipment Case", PropLibrary.Load("vintage_suitcase", n => n.Contains("_01"), n => n.Contains("_top"), "_01"),
                hale, new List<ItemStack> { new ItemStack(items["circuit_board"], 1) }, new Vector3(-100f, 0f, 0f));

            PlaceProp("medical_box", "Medical Box", camp, Camp + new Vector2(-4.6f, 6.8f), 75f);
            PlaceProp("metal_toolbox", "Toolbox", camp, Camp + new Vector2(-5.2f, 2.6f), 20f);

            var table = Anchor("Radio Table", camp, Camp + new Vector2(6f, -3f), 250f);
            var tableProp = PropLibrary.Load("wooden_picnic_table");
            PropLibrary.Spawn(tableProp, "Picnic Table", table);
            float top = tableProp.bounds.size.y;
            Vector3 along = tableProp.bounds.size.x >= tableProp.bounds.size.z ? Vector3.right : Vector3.forward;

            radio = BuildRadio(table, top);

            var lantern = PropLibrary.Spawn(PropLibrary.Load("wooden_lantern_01"), "Lantern", table, false);
            lantern.transform.localPosition = along * -0.75f + Vector3.up * top;
            var lanternLight = new GameObject("Lantern Light").AddComponent<Light>();
            lanternLight.transform.SetParent(lantern.transform, false);
            lanternLight.transform.localPosition = new Vector3(0f, 0.2f, 0f);
            lanternLight.type = LightType.Point;
            lanternLight.color = new Color(1f, 0.68f, 0.35f);
            lanternLight.intensity = 1.6f;
            lanternLight.range = 4f;
            lanternLight.gameObject.AddComponent<LightAnimator>().Configure(LightAnimator.Mode.Flicker, lanternLight, null, Color.black, 0.6f);

            var logPaper = NotePaper(table, along * 0.42f + Vector3.up * (top + 0.003f), 25f);
            transmission = logPaper.AddComponent<JournalNote>();
            transmission.Configure("Recovered Transmission",
                "[AUTO-PRINT  -  BUFFERED TRANSMISSION  -  RECEIVED 3 DAYS AGO]\n\n" +
                "...CONTROL, THIS IS MERIDIAN CAMP. HALE WAS RIGHT. THE SIGNAL IS NOT TERRESTRIAL. IT IS COMING FROM THE MOON. " +
                "SOUTH POLE REGION. IT REPEATS EVERY 27 MINUTES.\n\n" +
                "THE OLD SITE K-7 FACILITY HAS THE ONLY DISH BIG ENOUGH TO ANSWER. ILVES FOUND THE ACCESS CODES IN THE ARCHIVE. WE ARE GOING IN.\n\n" +
                "BUNKER ENTRANCE IS BEHIND THE NORTH RIDGE.\n\n" +
                "IF WE DO NOT CHECK IN WITHIN 48 HOURS... SEND NO ONE.\n\n[END OF BUFFER]");
            logPaper.transform.SetParent(gameplay, true);

            var campNote = NotePaper(table, along * -0.4f + Vector3.up * (top + 0.003f) + Vector3.Cross(Vector3.up, along) * 0.15f, -10f);
            campNote.AddComponent<JournalNote>().Configure("Camp Log - Radio",
                "Day 22.\n\nHale stripped the radio for his receiver experiment. To get the transmitter working again you need:\n\n" +
                "  -  3 x Copper Wire (spools were packed in the supply crates)\n" +
                "  -  1 x Battery Cell (Ilves hid the spare somewhere)\n" +
                "  -  1 x Radio Circuit Board (Hale keeps it in his case, in the tent)\n\n" +
                "Hale and Ilves left at dawn toward the coordinates. I'm staying with the camp.\n\n-  R. Okafor");
            campNote.transform.SetParent(gameplay, true);

            PlaceProp("portable_generator", "Generator", camp, Camp + new Vector2(7.6f, -5.4f), 200f);

            var supplies = Anchor("Camp Supplies", camp, Camp + new Vector2(3f, 7f), 200f);
            LootProp("Supply Crate", MilitaryCrate(), supplies, new List<ItemStack>
            {
                new ItemStack(items["copper_wire"], 1), new ItemStack(items["scrap_metal"], 2)
            }, new Vector3(110f, 0f, 0f));
            for (int i = 0; i < 3; i++)
                PlaceProp("barrel_03", "Barrel", camp, Camp + new Vector2(4.9f + i * 0.72f, 8.3f - i * 0.28f), Rand(0f, 360f));
            PlaceProp("metal_jerrycan_green", "Jerrycan", camp, Camp + new Vector2(2.2f, 8.4f), 15f);
            PlaceProp("metal_jerrycan_green", "Jerrycan", camp, Camp + new Vector2(1.7f, 8.0f), 40f);

            var mast = Anchor("Antenna Mast", camp, Camp + new Vector2(9f, 1f), 0f);
            var steel = Mat("Steel", new Color(0.6f, 0.62f, 0.64f), 0.5f, 0.85f);
            Primitive(PrimitiveType.Cylinder, "Pole", mast, new Vector3(0f, 6f, 0f), new Vector3(0.12f, 6f, 0.12f), steel, null, true);
            var beaconGlow = new Color(4f, 0.25f, 0.15f);
            var beacon = Primitive(PrimitiveType.Sphere, "Beacon", mast, new Vector3(0f, 12.1f, 0f), Vector3.one * 0.25f,
                Mat("Beacon", new Color(0.3f, 0.02f, 0.02f), 0.6f, 0f, beaconGlow));
            var beaconLight = new GameObject("Beacon Light").AddComponent<Light>();
            beaconLight.transform.SetParent(mast, false);
            beaconLight.transform.localPosition = new Vector3(0f, 12.1f, 0f);
            beaconLight.type = LightType.Point;
            beaconLight.color = new Color(1f, 0.15f, 0.1f);
            beaconLight.intensity = 4f;
            beaconLight.range = 18f;
            beacon.AddComponent<LightAnimator>().Configure(LightAnimator.Mode.Blink, beaconLight, beacon.GetComponent<Renderer>(), beaconGlow, 0.7f);

            Pickup(items["field_jacket"], 1, OnGround(Camp + new Vector2(-2.46f, -0.43f), 0.2f), 80f, gameplay);
            var badge = Pickup(items["meridian_badge"], 1, OnGround(Camp + new Vector2(-1.4f, 1.6f)), 30f, gameplay);
            badge.name = "Pickup_Insignia";

            var zoneGo = new GameObject("Camp Zone");
            zoneGo.transform.SetParent(gameplay, false);
            zoneGo.transform.position = OnGround(Camp, 2f);
            var box = zoneGo.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(CampRadius * 1.6f, 6f, CampRadius * 1.6f);
            zone = zoneGo.AddComponent<ZoneTrigger>();
            zone.Configure("meridian_camp");

            return camp.gameObject;
        }

        static RepairableDevice BuildRadio(Transform table, float top)
        {
            var prop = PropLibrary.Load("vintage_radio_transceiver");
            var root = PropLibrary.Spawn(prop, "Field Radio", table, false);
            root.transform.localPosition = Vector3.up * top;
            root.transform.localRotation = Quaternion.identity;
            float h = prop.bounds.size.y;

            var fault = Primitive(PrimitiveType.Sphere, "Fault Lamp", root.transform, new Vector3(0f, h + 0.015f, 0f), Vector3.one * 0.035f,
                Mat("Lamp Red", new Color(0.3f, 0.02f, 0.02f), 0.7f, 0f, new Color(3f, 0.1f, 0.05f)));
            var ok = Primitive(PrimitiveType.Sphere, "Power Lamp", root.transform, new Vector3(0f, h + 0.015f, 0f), Vector3.one * 0.035f,
                Mat("Lamp Green", new Color(0.02f, 0.3f, 0.05f), 0.7f, 0f, new Color(0.2f, 3f, 0.4f)));
            var glow = new GameObject("Radio Glow").AddComponent<Light>();
            glow.transform.SetParent(root.transform, false);
            glow.transform.localPosition = new Vector3(0f, h * 0.6f, -0.35f);
            glow.type = LightType.Point;
            glow.color = new Color(1f, 0.8f, 0.45f);
            glow.intensity = 1.2f;
            glow.range = 2.5f;
            ok.SetActive(false);
            glow.gameObject.SetActive(false);

            var col = root.AddComponent<BoxCollider>();
            col.center = prop.bounds.center;
            col.size = prop.bounds.size + new Vector3(0.1f, 0.1f, 0.1f);

            var device = root.AddComponent<RepairableDevice>();
            device.Configure("Field Radio", new List<ItemStack>
            {
                new ItemStack(items["copper_wire"], 3), new ItemStack(items["battery_cell"], 1), new ItemStack(items["circuit_board"], 1)
            }, new[] { ok, glow.gameObject }, new[] { fault });
            return device;
        }

        static PropLibrary.PropMesh MilitaryCrate() =>
            PropLibrary.Load("old_military_crate", n => n.EndsWith("_a"), n => n.Contains("lid"), "_a");

        static GameObject PlaceProp(string asset, string name, Transform parent, Vector2 p, float yaw)
        {
            var go = PropLibrary.Spawn(PropLibrary.Load(asset), name, parent);
            go.transform.SetPositionAndRotation(OnGround(p, -0.01f), Quaternion.Euler(0f, yaw, 0f));
            go.isStatic = true;
            return go;
        }

        static LootContainer LootProp(string displayName, PropLibrary.PropMesh prop, Transform anchor, List<ItemStack> contents, Vector3 openEuler)
        {
            var go = PropLibrary.Spawn(prop, displayName, anchor, out var hinge);
            var loot = go.AddComponent<LootContainer>();
            loot.Configure(displayName, hinge, contents);
            var so = new SerializedObject(loot);
            so.FindProperty("lidOpenEuler").vector3Value = openEuler;
            so.ApplyModifiedPropertiesWithoutUndo();
            return loot;
        }

        static Mesh TentMesh(float width, float height, float length)
        {
            var mb = new MeshBuilder(1);
            float hw = width * 0.5f, hl = length * 0.5f;
            Vector3 rf = new Vector3(0f, height, hl), rb = new Vector3(0f, height, -hl);
            Vector3 lf = new Vector3(-hw, 0f, hl), lb = new Vector3(-hw, 0f, -hl);
            Vector3 rfB = new Vector3(hw, 0f, hl), rbB = new Vector3(hw, 0f, -hl);

            void Tri(Vector3 a, Vector3 b, Vector3 c, Vector3 n)
            {
                Vector3 nn = n.normalized;
                Vector3 u = Vector3.Cross(nn, Mathf.Abs(nn.y) > 0.9f ? Vector3.forward : Vector3.up).normalized;
                Vector3 v = Vector3.Cross(u, nn);
                Vector2 Uv(Vector3 q) => new Vector2(Vector3.Dot(q, u), Vector3.Dot(q, v)) * 0.5f;
                for (int side = 0; side < 2; side++)
                {
                    Vector3 o = side == 0 ? n : -n;
                    Vector3 inset = side == 0 ? Vector3.zero : -n.normalized * 0.01f;
                    int ia = mb.Vertex(a + inset, Uv(a));
                    int ib = mb.Vertex(b + inset, Uv(b));
                    int ic = mb.Vertex(c + inset, Uv(c));
                    mb.Triangle(0, ia, ib, ic, o);
                }
            }

            Vector3 leftN = new Vector3(-height, hw, 0f), rightN = new Vector3(height, hw, 0f);
            Tri(lf, lb, rb, leftN);
            Tri(lf, rb, rf, leftN);
            Tri(rfB, rbB, rb, rightN);
            Tri(rfB, rb, rf, rightN);
            Tri(lb, rbB, rb, Vector3.back);
            Vector3 flapL = new Vector3(-hw * 0.45f, height * 0.55f, hl), flapR = new Vector3(hw * 0.45f, height * 0.55f, hl);
            Tri(lf, flapL, rf, Vector3.forward);
            Tri(rfB, flapR, rf, Vector3.forward);
            return mb.Build("Tent_AFrame");
        }

        static GameObject NotePaper(Transform parent, Vector3 localPos, float yaw)
        {
            var paper = Primitive(PrimitiveType.Cube, "Note", parent, localPos, new Vector3(0.21f, 0.004f, 0.29f),
                Mat("Paper", new Color(0.92f, 0.9f, 0.82f), 0.05f), Quaternion.Euler(0f, yaw, 0f));
            var trigger = paper.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.size = new Vector3(2f, 60f, 1.6f);
            return paper;
        }

        static GameObject Pickup(ItemDefinition item, int amount, Vector3 pos, float yaw, Transform parent)
        {
            var root = new GameObject("Pickup_" + item.Id);
            root.transform.SetParent(parent, false);
            root.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
            var mat = Mat("Item " + item.DisplayName, item.Tint, 0.45f, item.Id == "copper_wire" || item.Id == "meridian_badge" ? 0.85f : 0.1f);
            switch (item.Id)
            {
                case "copper_wire":
                    Primitive(PrimitiveType.Cylinder, "Spool", root.transform, new Vector3(0f, 0.06f, 0f), new Vector3(0.24f, 0.06f, 0.24f), mat);
                    break;
                case "battery_cell":
                    Primitive(PrimitiveType.Cube, "Cell", root.transform, new Vector3(0f, 0.12f, 0f), new Vector3(0.16f, 0.24f, 0.12f), mat);
                    Primitive(PrimitiveType.Cube, "Cap", root.transform, new Vector3(0f, 0.25f, 0f), new Vector3(0.1f, 0.03f, 0.06f),
                        Mat("Battery Cap", new Color(0.1f, 0.1f, 0.1f), 0.3f));
                    break;
                case "scrap_metal":
                    Primitive(PrimitiveType.Cube, "Panel", root.transform, new Vector3(0f, 0.04f, 0f), new Vector3(0.45f, 0.03f, 0.3f), mat,
                        Quaternion.Euler(8f, 0f, 12f));
                    break;
                case "multitool":
                    Primitive(PrimitiveType.Cube, "Tool", root.transform, new Vector3(0f, 0.02f, 0f), new Vector3(0.05f, 0.03f, 0.18f), mat);
                    break;
                case "meridian_badge":
                    Primitive(PrimitiveType.Cylinder, "Badge", root.transform, new Vector3(0f, 0.01f, 0f), new Vector3(0.1f, 0.006f, 0.1f), mat);
                    break;
                case "leather_boots":
                    Primitive(PrimitiveType.Cube, "Boot L", root.transform, new Vector3(-0.08f, 0.07f, 0f), new Vector3(0.11f, 0.14f, 0.28f), mat);
                    Primitive(PrimitiveType.Cube, "Boot R", root.transform, new Vector3(0.08f, 0.07f, 0.02f), new Vector3(0.11f, 0.14f, 0.28f), mat,
                        Quaternion.Euler(0f, 12f, 0f));
                    break;
                case "field_jeans":
                case "field_jacket":
                    Primitive(PrimitiveType.Cube, "Folded", root.transform, new Vector3(0f, 0.045f, 0f), new Vector3(0.42f, 0.09f, 0.32f), mat);
                    Primitive(PrimitiveType.Cube, "Fold", root.transform, new Vector3(0f, 0.1f, -0.02f), new Vector3(0.4f, 0.03f, 0.26f), mat,
                        Quaternion.Euler(0f, 4f, 0f));
                    break;
                default:
                    Primitive(PrimitiveType.Cube, "Board", root.transform, new Vector3(0f, 0.02f, 0f), new Vector3(0.22f, 0.02f, 0.16f), mat);
                    break;
            }
            var col = root.AddComponent<BoxCollider>();
            col.isTrigger = true;
            col.center = new Vector3(0f, 0.25f, 0f);
            col.size = new Vector3(0.7f, 0.6f, 0.7f);
            root.AddComponent<Pickup>().Configure(item, amount);
            return root;
        }

        static void BuildTrail(Transform world, Transform gameplay)
        {
            var trail = new GameObject("Trail").transform;
            trail.SetParent(world, false);
            var post = Mat("Post Wood", new Color(0.36f, 0.27f, 0.18f), 0.1f);
            var tape = Mat("Marker Tape", new Color(1f, 0.38f, 0.05f), 0.3f, 0f, new Color(0.35f, 0.1f, 0f));

            float travelled = 0f;
            int side = 1;
            for (int i = 0; i < Trail.Length - 1; i++)
            {
                Vector2 a = Trail[i], b = Trail[i + 1];
                float len = Vector2.Distance(a, b);
                for (float d = 0f; d < len; d += 1f)
                {
                    travelled += 1f;
                    if (travelled % 20f > 0.5f) continue;
                    Vector2 dir = (b - a).normalized;
                    Vector2 p = Vector2.Lerp(a, b, d / len) + new Vector2(dir.y, -dir.x) * (2.2f * side);
                    side = -side;
                    var t = Anchor("Marker", trail, p, Rand(0f, 360f));
                    Primitive(PrimitiveType.Cube, "Post", t, new Vector3(0f, 0.55f, 0f), new Vector3(0.09f, 1.3f, 0.09f), post);
                    Primitive(PrimitiveType.Cube, "Tape", t, new Vector3(0.0f, 1.05f, 0.12f), new Vector3(0.02f, 0.08f, 0.24f), tape,
                        Quaternion.Euler(0f, 0f, Rand(-15f, 15f)));
                }
            }

            Vector2 start = Trail[0];
            clearings.Add((start, 8f));
            var sign = Anchor("Trailhead Sign", trail, start + new Vector2(2.5f, 3.2f), 200f);
            Primitive(PrimitiveType.Cube, "Post L", sign, new Vector3(-0.5f, 0.75f, 0f), new Vector3(0.1f, 1.5f, 0.1f), post, null, true);
            Primitive(PrimitiveType.Cube, "Post R", sign, new Vector3(0.5f, 0.75f, 0f), new Vector3(0.1f, 1.5f, 0.1f), post, null, true);
            Primitive(PrimitiveType.Cube, "Board", sign, new Vector3(0f, 1.3f, 0f), new Vector3(1.3f, 0.55f, 0.05f), Mat("Sign Board", new Color(0.55f, 0.45f, 0.3f), 0.15f));
            Primitive(PrimitiveType.Cube, "Stripe", sign, new Vector3(0f, 1.45f, -0.03f), new Vector3(1.2f, 0.08f, 0.01f), tape);
            var notice = NotePaper(sign, new Vector3(0f, 1.25f, -0.035f), 0f);
            notice.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            notice.AddComponent<JournalNote>().Configure("Expedition Notice",
                "MERIDIAN EXPEDITION  -  AUTHORISED PERSONNEL ONLY\n\n" +
                "Base camp is north-east of this point. Follow the orange trail markers.\n\n" +
                "Radio check-ins at 06:00 and 18:00. If a check-in is missed for more than 48 hours, do NOT proceed alone. Contact Site Control.\n\n" +
                "-  R. Okafor, Logistics");
            notice.transform.SetParent(gameplay, true);

            Pickup(items["leather_boots"], 1, OnGround(start + new Vector2(-1.8f, 3.6f)), 140f, gameplay);
            Pickup(items["copper_wire"], 1, OnGround(TrailSide(1, 3f)), 40f, gameplay);
            Pickup(items["scrap_metal"], 1, OnGround(TrailSide(2, -2.6f)), 10f, gameplay);

            Vector2 drop = TrailSide(3, 10f);
            clearings.Add((drop, 6f));
            var dropAnchor = Anchor("Abandoned Supply Drop", world, drop, 160f);
            LootProp("Supply Crate", MilitaryCrate(), dropAnchor, new List<ItemStack>
            {
                new ItemStack(items["copper_wire"], 1), new ItemStack(items["scrap_metal"], 3), new ItemStack(items["field_jeans"], 1)
            }, new Vector3(110f, 0f, 0f));
            var dropped = PropLibrary.Spawn(PropLibrary.Load("vintage_suitcase", n => n.Contains("_02"), null, "_02"), "Abandoned Case", dropAnchor);
            dropped.transform.SetPositionAndRotation(OnGround(drop + new Vector2(1.4f, -0.7f), -0.02f), Quaternion.Euler(0f, 62f, 0f));
            PlaceProp("metal_jerrycan_green", "Jerrycan", dropAnchor, drop + new Vector2(-1.2f, 0.9f), 120f);
            var journal = NotePaper(dropAnchor, Vector3.zero, 0f);
            journal.transform.position = OnGround(drop + new Vector2(0.9f, -1.3f), 0.02f);
            journal.transform.rotation = Quaternion.Euler(0f, 70f, 0f);
            journal.AddComponent<JournalNote>().Configure("Field Journal - Day 19",
                "The signal came back last night. Same pattern, same frequency, and Hale swears it isn't terrestrial. " +
                "He wants to take the camp radio apart and rebuild it as a receiver.\n\n" +
                "I told him we can't afford to lose contact with Site Control. He did it anyway.\n\n" +
                "I've hidden the spare battery out by the big boulder on the far side of the trail, past the next bend, so he can't cannibalise that too.\n\n" +
                "The camp is just ahead.\n\n-  M. Ilves");
            journal.transform.SetParent(gameplay, true);

            Vector2 boulderSpot = TrailSide(4, -15f);
            clearings.Add((boulderSpot, 7f));
            var boulderMeshes = BuilderAssets.ModelMeshes("boulder_01");
            if (boulderMeshes.Length > 0)
            {
                var bm = boulderMeshes[0];
                float scale = 3.2f;
                PlaceModel("Landmark Boulder", world, bm, PhMat("Models", "boulder_01"), boulderSpot, 30f, scale, 0.4f, true);
                float r = Mathf.Max(bm.bounds.extents.x, bm.bounds.extents.z) * scale + 0.35f;
                Vector2 toTrail = (Trail[4] - boulderSpot).normalized;
                Pickup(items["battery_cell"], 1, OnGround(boulderSpot + toTrail * r), 15f, gameplay);
                Pickup(items["multitool"], 1, OnGround(boulderSpot + (toTrail + new Vector2(toTrail.y, -toTrail.x) * 0.5f).normalized * (r + 0.3f)), 70f, gameplay);
            }
            else
            {
                Pickup(items["battery_cell"], 1, OnGround(boulderSpot), 15f, gameplay);
            }
        }

        static void BuildForest(Transform world)
        {
            var forest = new GameObject("Forest").transform;
            forest.SetParent(world, false);
            var bark = FoliageBuilder.BarkMaterial();
            var needles = FoliageBuilder.NeedleMaterials();
            var core = FoliageBuilder.CoreMaterial();
            var meshes = new[] { FoliageBuilder.FirTree(11), FoliageBuilder.FirTree(23), FoliageBuilder.FirTree(37), FoliageBuilder.FirTree(41), FoliageBuilder.FirTree(53) };

            int placed = 0;
            for (int attempt = 0; attempt < 9000 && placed < 640; attempt++)
            {
                var p = new Vector2(Rand(6f, Size - 6f), Rand(6f, Size - 6f));
                float trailD = TrailDistance(p.x, p.y);
                if (trailD < 5f || InClearing(p)) continue;
                if (Steepness(p) > 40f) continue;
                float density = Mathf.PerlinNoise(p.x * 0.02f + 40f, p.y * 0.02f + 90f);
                if (trailD < 14f) density *= 0.6f;
                if (rng.NextDouble() > density * 1.3f) continue;

                float scale = Rand(0.75f, 1.25f);
                var t = MeshObject("Fir", forest, meshes[rng.Next(meshes.Length)], new[] { bark, needles[rng.Next(needles.Length)], core }, false);
                t.transform.SetPositionAndRotation(OnGround(p, -0.15f), Quaternion.Euler(Rand(-2f, 2f), Rand(0f, 360f), Rand(-2f, 2f)));
                t.transform.localScale = new Vector3(scale, scale * Rand(0.92f, 1.12f), scale);
                var cap = t.AddComponent<CapsuleCollider>();
                cap.radius = 0.32f;
                cap.height = 6f;
                cap.center = new Vector3(0f, 3f, 0f);
                placed++;
            }
        }

        static void BuildUndergrowth(Transform world)
        {
            var root = new GameObject("Undergrowth").transform;
            root.SetParent(world, false);
            var shrubMeshes = BuilderAssets.ModelMeshes("shrub_02");
            var shrubMat = FoliageBuilder.ShrubMaterial();
            int placed = 0;
            for (int attempt = 0; attempt < 2000 && placed < 45 && shrubMeshes.Length > 0; attempt++)
            {
                var p = new Vector2(Rand(10f, Size - 10f), Rand(10f, Size - 10f));
                float trailD = TrailDistance(p.x, p.y);
                if (trailD < 3.5f || InClearing(p, -1f) || Steepness(p) > 30f) continue;
                if (Mathf.PerlinNoise(p.x * 0.02f + 40f, p.y * 0.02f + 90f) < 0.45f) continue;
                PlaceModel("Shrub", root, shrubMeshes[0], shrubMat, p, Rand(0f, 360f), Rand(0.7f, 1.3f), 0.05f, false);
                placed++;
            }
        }

        static Mesh PineMesh(int seed)
        {
            var r = new System.Random(seed);
            var mb = new MeshBuilder(2);
            float h = 10f + (float)r.NextDouble() * 5f;
            mb.Cylinder(0, Vector3.down * 0.4f, h * 0.85f + 0.4f, 0.34f, 0.07f, 10, 0.5f);
            int tiers = 6 + r.Next(3);
            for (int i = 0; i < tiers; i++)
            {
                float k = i / (float)(tiers - 1);
                float y = Mathf.Lerp(h * 0.25f, h * 0.84f, k);
                float radius = Mathf.Lerp(2.7f, 0.65f, k) * (0.85f + (float)r.NextDouble() * 0.3f);
                float coneH = Mathf.Lerp(2.9f, 1.9f, k);
                mb.Cone(1, new Vector3(((float)r.NextDouble() - 0.5f) * 0.2f, y, ((float)r.NextDouble() - 0.5f) * 0.2f),
                    radius, coneH, 12, 0.5f, 0.2f, r);
            }
            return BuilderAssets.SaveMesh(mb.Build("Pine_" + seed));
        }

        static void BuildRocks(Transform world)
        {
            var rocks = new GameObject("Rocks & Deadwood").transform;
            rocks.SetParent(world, false);

            var set = BuilderAssets.ModelMeshes("rock_moss_set_01");
            var setMat = PhMat("Models", "rock_moss_set_01");
            var boulder = BuilderAssets.ModelMeshes("boulder_01");
            var boulderMat = PhMat("Models", "boulder_01");
            var stump = BuilderAssets.ModelMeshes("tree_stump_01");
            var stumpMat = PhMat("Models", "tree_stump_01");
            var trunk = BuilderAssets.ModelMeshes("dead_tree_trunk");
            var trunkMat = PhMat("Models", "dead_tree_trunk");

            var pool = new List<(Mesh mesh, Material mat, float min, float max, float weight)>();
            foreach (var m in set) pool.Add((m, setMat, 0.35f, 1.1f, 3f));
            foreach (var m in boulder) pool.Add((m, boulderMat, 0.6f, 1.6f, 2f));
            foreach (var m in stump) pool.Add((m, stumpMat, 0.8f, 1.2f, 1f));
            foreach (var m in trunk) pool.Add((m, trunkMat, 0.9f, 1.4f, 0.8f));
            if (pool.Count == 0) return;
            float total = pool.Sum(x => x.weight);

            int placed = 0;
            for (int attempt = 0; attempt < 3000 && placed < 170; attempt++)
            {
                var p = new Vector2(Rand(8f, Size - 8f), Rand(8f, Size - 8f));
                float trailD = TrailDistance(p.x, p.y);
                if (trailD < 3.2f || InClearing(p, -2f)) continue;
                float pick = Rand(0f, total);
                var entry = pool[0];
                foreach (var e in pool)
                {
                    pick -= e.weight;
                    if (pick <= 0f) { entry = e; break; }
                }
                float s = Rand(entry.min, entry.max);
                PlaceModel(entry.mesh.name, rocks, entry.mesh, entry.mat, p, Rand(0f, 360f), s, s * 0.08f, true);
                placed++;
            }
        }

        static Camera BuildCamera()
        {
            var go = new GameObject("Main Camera");
            go.tag = "MainCamera";
            var cam = go.AddComponent<Camera>();
            cam.fieldOfView = 62f;
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 1200f;
            go.AddComponent<AudioListener>();
            var data = cam.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            data.antialiasingQuality = AntialiasingQuality.High;
            return cam;
        }

        static void BuildPlayer(Camera cam)
        {
            Vector2 start = Trail[0];
            Vector2 dir = Trail[1] - Trail[0];
            float yaw = Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg;

            var player = new GameObject("Player");
            player.tag = "Player";
            player.transform.SetPositionAndRotation(OnGround(start, 0.1f), Quaternion.Euler(0f, yaw, 0f));

            var cc = player.AddComponent<CharacterController>();
            cc.height = 1.8f;
            cc.radius = 0.3f;
            cc.center = new Vector3(0f, 0.9f, 0f);
            cc.slopeLimit = 46f;
            cc.stepOffset = 0.4f;
            cc.skinWidth = 0.04f;

            player.AddComponent<PlayerInputReader>();
            player.AddComponent<Inventory>();
            player.AddComponent<PlayerMotor>();
            var rig = player.AddComponent<PlayerCameraRig>();
            player.AddComponent<Interactor>();
            var wardrobe = player.AddComponent<Wardrobe>();

            var modelAsset = ExplorerCharacter.Import();
            var model = (GameObject)PrefabUtility.InstantiatePrefab(modelAsset);
            PrefabUtility.UnpackPrefabInstance(model, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            model.name = "Explorer";
            model.transform.SetParent(player.transform, false);
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            var bounds = new Bounds(model.transform.position, Vector3.zero);
            foreach (var r in model.GetComponentsInChildren<Renderer>(true)) bounds.Encapsulate(r.bounds);
            float height = bounds.size.y;
            if (height > 0.5f) model.transform.localScale = Vector3.one * (1.78f / height);
            model.AddComponent<HumanoidLocomotion>();
            foreach (var smr in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                smr.updateWhenOffscreen = true;
                smr.shadowCastingMode = ShadowCastingMode.On;
            }

            GameObject Part(string name)
            {
                foreach (var t in model.GetComponentsInChildren<Transform>(true))
                    if (t.name == name) return t.gameObject;
                return null;
            }

            wardrobe.Configure(new List<Wardrobe.Garment>
            {
                new Wardrobe.Garment { item = items["leather_boots"], show = new[] { Part("Outfit_Boots") }, hide = new[] { Part("Body_Feet") } },
                new Wardrobe.Garment { item = items["field_jeans"], show = new[] { Part("Outfit_Jeans") }, hide = new[] { Part("Underwear_Briefs") } },
                new Wardrobe.Garment { item = items["field_jacket"], show = new[] { Part("Outfit_Jacket") }, hide = new[] { Part("Underwear_Undershirt") } }
            });
            foreach (var n in new[] { "Outfit_Boots", "Outfit_Jeans", "Outfit_Jacket" })
            {
                var go = Part(n);
                if (go != null) go.SetActive(false);
            }

            foreach (var t in player.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = IgnoreRaycastLayer;

            var rigSo = new SerializedObject(rig);
            rigSo.FindProperty("cameraTransform").objectReferenceValue = cam.transform;
            rigSo.ApplyModifiedPropertiesWithoutUndo();

            cam.transform.position = player.transform.position + Quaternion.Euler(0f, yaw, 0f) * new Vector3(0.5f, 1.75f, -3f);
            cam.transform.rotation = Quaternion.Euler(10f, yaw, 0f);
        }
    }
}
