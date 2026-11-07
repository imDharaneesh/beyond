using System.Collections.Generic;
using Beyond.Core;
using Beyond.Interaction;
using Beyond.Items;
using Beyond.Player;
using UnityEngine;
using UnityEngine.UI;

namespace Beyond.UI
{
    [DefaultExecutionOrder(110)]
    public class NavigationHud : MonoBehaviour
    {
        const float CompassWidth = 620f;
        const float CompassSpan = 180f;
        const float MarkerRange = 38f;
        const float LabelRange = 11f;

        static readonly Color Ink = new Color(0.94f, 0.95f, 0.96f, 1f);
        static readonly Color Muted = new Color(0.75f, 0.79f, 0.83f, 0.85f);
        static readonly Color Objective = new Color(1f, 0.74f, 0.32f, 1f);

        class Marker
        {
            public MonoBehaviour source;
            public RectTransform root;
            public Image icon;
            public Image ring;
            public Text label;
            public Text distance;
        }

        Camera cam;
        Transform player;
        Interactor interactor;
        Font font;
        Sprite diamond, ringSprite, dot, gradient, chevron;
        RectTransform canvasRect, compassStrip;
        readonly List<(RectTransform rt, float heading)> compassMarks = new List<(RectTransform, float)>();
        RectTransform compassObjective, waypoint;
        Text compassObjectiveDistance, waypointDistance, headingText;
        Image compassObjectiveIcon;
        readonly Dictionary<MonoBehaviour, Marker> markers = new Dictionary<MonoBehaviour, Marker>();
        float scanTimer;

        void Awake()
        {
            var p = GameObject.FindWithTag("Player");
            player = p != null ? p.transform : null;
            interactor = p != null ? p.GetComponent<Interactor>() : null;
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            diamond = MakeSprite(Shape.Diamond);
            ringSprite = MakeSprite(Shape.Ring);
            dot = MakeSprite(Shape.Dot);
            gradient = MakeSprite(Shape.Gradient);
            chevron = MakeSprite(Shape.Chevron);
            Build();
        }

        void LateUpdate()
        {
            if (cam == null) cam = Camera.main;
            if (cam == null || player == null) return;
            scanTimer -= Time.unscaledDeltaTime;
            if (scanTimer <= 0f)
            {
                scanTimer = 0.5f;
                Scan();
            }
            UpdateCompass();
            UpdateWaypoint();
            UpdateMarkers();
        }

        void Build()
        {
            var canvasGo = new GameObject("Navigation Canvas", typeof(Canvas), typeof(CanvasScaler));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 8;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            canvasRect = canvasGo.GetComponent<RectTransform>();

            var compass = NewImage(canvasRect, "Compass", gradient, new Color(0.03f, 0.05f, 0.07f, 0.55f));
            Anchor(compass.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -34f), new Vector2(CompassWidth, 34f));
            compassStrip = compass.rectTransform;
            var mask = compass.gameObject.AddComponent<RectMask2D>();
            mask.softness = new Vector2Int(60, 0);

            for (int deg = 0; deg < 360; deg += 15)
            {
                bool cardinal = deg % 90 == 0;
                bool inter = deg % 45 == 0;
                RectTransform rt;
                if (inter)
                {
                    var t = NewText(compassStrip, "Mark " + deg, cardinal ? 17 : 13, TextAnchor.MiddleCenter, cardinal ? Ink : Muted);
                    t.text = deg switch { 0 => "N", 45 => "NE", 90 => "E", 135 => "SE", 180 => "S", 225 => "SW", 270 => "W", _ => "NW" };
                    if (cardinal) t.fontStyle = FontStyle.Bold;
                    rt = t.rectTransform;
                    rt.sizeDelta = new Vector2(40f, 30f);
                }
                else
                {
                    var tick = NewImage(compassStrip, "Tick " + deg, null, new Color(1f, 1f, 1f, 0.35f));
                    rt = tick.rectTransform;
                    rt.sizeDelta = new Vector2(2f, 9f);
                }
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                compassMarks.Add((rt, deg));
            }

            var centre = NewImage(canvasRect, "Compass Needle", chevron, Ink);
            Anchor(centre.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -56f), new Vector2(12f, 8f));
            centre.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 180f);

            compassObjectiveIcon = NewImage(compassStrip, "Objective", diamond, Objective);
            compassObjective = compassObjectiveIcon.rectTransform;
            compassObjective.anchorMin = compassObjective.anchorMax = new Vector2(0.5f, 0.5f);
            compassObjective.sizeDelta = new Vector2(16f, 16f);
            compassObjectiveDistance = NewText(canvasRect, "Objective Distance", 14, TextAnchor.UpperCenter, Objective);
            Anchor(compassObjectiveDistance.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -72f), new Vector2(120f, 20f));

            headingText = NewText(canvasRect, "Heading", 12, TextAnchor.UpperCenter, Muted);
            Anchor(headingText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -4f), new Vector2(80f, 16f));

            waypoint = new GameObject("Waypoint", typeof(RectTransform)).GetComponent<RectTransform>();
            waypoint.SetParent(canvasRect, false);
            waypoint.anchorMin = waypoint.anchorMax = Vector2.zero;
            waypoint.sizeDelta = new Vector2(30f, 30f);
            var wr = NewImage(waypoint, "Ring", ringSprite, new Color(Objective.r, Objective.g, Objective.b, 0.9f));
            Stretch(wr.rectTransform);
            var wi = NewImage(waypoint, "Core", diamond, Objective);
            wi.rectTransform.sizeDelta = new Vector2(12f, 12f);
            waypointDistance = NewText(waypoint, "Distance", 14, TextAnchor.UpperCenter, Objective);
            waypointDistance.rectTransform.anchoredPosition = new Vector2(0f, -28f);
            waypointDistance.rectTransform.sizeDelta = new Vector2(120f, 20f);
        }

        float Heading()
        {
            Vector3 f = cam.transform.forward;
            return Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg;
        }

        void UpdateCompass()
        {
            float heading = Heading();
            foreach (var (rt, deg) in compassMarks)
            {
                float d = Mathf.DeltaAngle(heading, deg);
                bool visible = Mathf.Abs(d) < CompassSpan * 0.5f;
                rt.gameObject.SetActive(visible);
                if (visible) rt.anchoredPosition = new Vector2(d / CompassSpan * CompassWidth, 0f);
            }
            headingText.text = Mathf.RoundToInt(Mathf.Repeat(heading, 360f)).ToString("000") + "°";

            var target = Objectives.Target;
            bool has = target.HasValue;
            compassObjective.gameObject.SetActive(has);
            compassObjectiveDistance.gameObject.SetActive(has);
            if (!has) return;
            Vector3 to = target.Value - player.position;
            float bearing = Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg;
            float delta = Mathf.Clamp(Mathf.DeltaAngle(heading, bearing), -CompassSpan * 0.5f + 6f, CompassSpan * 0.5f - 6f);
            compassObjective.anchoredPosition = new Vector2(delta / CompassSpan * CompassWidth, 0f);
            float dist = new Vector2(to.x, to.z).magnitude;
            compassObjectiveDistance.text = FormatDistance(dist);
            compassObjectiveDistance.rectTransform.anchoredPosition = new Vector2(delta / CompassSpan * CompassWidth, -72f);
        }

        void UpdateWaypoint()
        {
            var target = Objectives.Target;
            if (!target.HasValue)
            {
                waypoint.gameObject.SetActive(false);
                return;
            }
            Vector3 world = target.Value + Vector3.up * 1.2f;
            float dist = Vector3.Distance(player.position, target.Value);
            Vector3 sp = cam.WorldToScreenPoint(world);
            bool show = sp.z > 0f && dist > 4f;
            waypoint.gameObject.SetActive(show);
            if (!show) return;
            Vector2 local = ScreenToCanvas(sp);
            waypoint.anchoredPosition = local;
            waypointDistance.text = FormatDistance(dist);
            float a = Mathf.Clamp01(1f - Mathf.Abs(local.x - canvasRect.rect.width * 0.5f) / (canvasRect.rect.width * 0.6f)) * 0.6f + 0.4f;
            foreach (var g in waypoint.GetComponentsInChildren<Graphic>())
            {
                var c = g.color;
                c.a = g is Text ? a : a * 0.95f;
                g.color = c;
            }
        }

        void Scan()
        {
            foreach (var mb in FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (!(mb is IInteractable) || markers.ContainsKey(mb)) continue;
                markers[mb] = CreateMarker(mb);
            }
            var dead = new List<MonoBehaviour>();
            foreach (var kv in markers)
                if (kv.Key == null) dead.Add(kv.Key);
            foreach (var k in dead)
            {
                if (markers[k].root != null) Destroy(markers[k].root.gameObject);
                markers.Remove(k);
            }
        }

        Marker CreateMarker(MonoBehaviour source)
        {
            var root = new GameObject("Marker", typeof(RectTransform)).GetComponent<RectTransform>();
            root.SetParent(canvasRect, false);
            root.anchorMin = root.anchorMax = Vector2.zero;
            root.sizeDelta = new Vector2(24f, 24f);
            var ring = NewImage(root, "Ring", ringSprite, Color.white);
            Stretch(ring.rectTransform);
            var icon = NewImage(root, "Icon", diamond, Color.white);
            icon.rectTransform.sizeDelta = new Vector2(10f, 10f);
            var label = NewText(root, "Label", 15, TextAnchor.LowerCenter, Ink);
            label.rectTransform.anchoredPosition = new Vector2(0f, 26f);
            label.rectTransform.sizeDelta = new Vector2(260f, 22f);
            var distance = NewText(root, "Distance", 12, TextAnchor.UpperCenter, Muted);
            distance.rectTransform.anchoredPosition = new Vector2(0f, -22f);
            distance.rectTransform.sizeDelta = new Vector2(100f, 18f);
            return new Marker { source = source, root = root, icon = icon, ring = ring, label = label, distance = distance };
        }

        void UpdateMarkers()
        {
            var current = interactor != null ? interactor.Current as MonoBehaviour : null;
            Vector2 screenCentre = new Vector2(canvasRect.rect.width * 0.5f, canvasRect.rect.height * 0.5f);
            foreach (var m in markers.Values)
            {
                if (m.source == null || m.root == null) continue;
                Describe(m.source, out string name, out Color color, out bool done, out Sprite shape);
                Vector3 pos = MarkerPosition(m.source);
                float dist = Vector3.Distance(player.position, pos);
                Vector3 sp = cam.WorldToScreenPoint(pos);
                bool visible = !done && m.source.isActiveAndEnabled && dist < MarkerRange && sp.z > 0f && m.source != current;
                m.root.gameObject.SetActive(visible);
                if (!visible) continue;

                Vector2 local = ScreenToCanvas(sp);
                m.root.anchoredPosition = local;
                float t = Mathf.InverseLerp(MarkerRange, 6f, dist);
                float scale = Mathf.Lerp(0.65f, 1.05f, t);
                m.root.localScale = Vector3.one * scale;
                float alpha = Mathf.Lerp(0.35f, 1f, t);
                bool focus = dist < LabelRange || Vector2.Distance(local, screenCentre) < 110f;

                m.icon.sprite = shape;
                m.icon.color = new Color(color.r, color.g, color.b, alpha);
                m.ring.color = new Color(color.r, color.g, color.b, alpha * (focus ? 0.9f : 0.45f));
                m.label.gameObject.SetActive(focus);
                m.distance.gameObject.SetActive(focus);
                if (focus)
                {
                    m.label.text = name.ToUpperInvariant();
                    m.label.color = new Color(Ink.r, Ink.g, Ink.b, alpha);
                    m.distance.text = FormatDistance(dist);
                }
            }
        }

        static Vector3 MarkerPosition(MonoBehaviour source)
        {
            var col = source.GetComponentInChildren<Collider>();
            Vector3 basePos = col != null ? col.bounds.center : source.transform.position;
            float top = col != null ? col.bounds.max.y : basePos.y;
            return new Vector3(basePos.x, top + 0.35f, basePos.z);
        }

        void Describe(MonoBehaviour source, out string name, out Color color, out bool done, out Sprite shape)
        {
            done = false;
            shape = diamond;
            switch (source)
            {
                case Pickup p when p.Item != null:
                    name = p.Item.DisplayName;
                    color = CategoryColor(p.Item.Category);
                    shape = p.Item.Category == ItemCategory.Tool || p.Item.Category == ItemCategory.Clothing ? dot : diamond;
                    break;
                case LootContainer l:
                    name = l.ContainerName;
                    color = new Color(1f, 0.74f, 0.32f);
                    done = l.IsOpened;
                    shape = dot;
                    break;
                case JournalNote n:
                    name = n.Title;
                    color = new Color(0.93f, 0.88f, 0.74f);
                    done = n.HasBeenRead;
                    break;
                case RepairableDevice d:
                    name = d.DeviceName;
                    color = new Color(1f, 0.55f, 0.3f);
                    done = d.IsRepaired;
                    break;
                default:
                    name = source.name;
                    color = Ink;
                    break;
            }
        }

        static Color CategoryColor(ItemCategory c) => c switch
        {
            ItemCategory.Component => new Color(1f, 0.74f, 0.32f),
            ItemCategory.Resource => new Color(0.8f, 0.84f, 0.88f),
            ItemCategory.Tool => new Color(0.4f, 0.85f, 0.95f),
            ItemCategory.Clothing => new Color(0.62f, 0.9f, 0.55f),
            _ => new Color(0.95f, 0.82f, 0.45f),
        };

        static string FormatDistance(float d) => d >= 1000f ? $"{d / 1000f:0.0} km" : $"{Mathf.RoundToInt(d)} m";

        Vector2 ScreenToCanvas(Vector3 sp)
        {
            float k = canvasRect.rect.width / Screen.width;
            return new Vector2(sp.x * k, sp.y * k);
        }

        Text NewText(RectTransform parent, string name, int size, TextAnchor anchor, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text), typeof(Shadow));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<Text>();
            t.font = font;
            t.fontSize = size;
            t.alignment = anchor;
            t.color = color;
            t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            go.GetComponent<Shadow>().effectColor = new Color(0f, 0f, 0f, 0.65f);
            return t;
        }

        static Image NewImage(RectTransform parent, string name, Sprite sprite, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        static void Anchor(RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        enum Shape { Diamond, Ring, Dot, Gradient, Chevron }

        static Sprite MakeSprite(Shape shape)
        {
            int w = shape == Shape.Gradient ? 256 : 64, h = shape == Shape.Gradient ? 8 : 64;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float u = (x + 0.5f) / w * 2f - 1f, v = (y + 0.5f) / h * 2f - 1f;
                float a;
                switch (shape)
                {
                    case Shape.Diamond:
                        a = Edge(1f - (Mathf.Abs(u) + Mathf.Abs(v)), 0.06f);
                        break;
                    case Shape.Ring:
                        float r = Mathf.Sqrt(u * u + v * v);
                        a = Edge(0.08f - Mathf.Abs(r - 0.86f), 0.05f);
                        break;
                    case Shape.Dot:
                        a = Edge(0.8f - Mathf.Sqrt(u * u + v * v), 0.08f);
                        break;
                    case Shape.Chevron:
                        a = Edge(Mathf.Min(1f - v, v + 1f - Mathf.Abs(u) * 2f), 0.08f);
                        break;
                    default:
                        float edge = 1f - Mathf.Abs(u);
                        a = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(edge * 2.2f));
                        break;
                }
                px[y * w + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(a) * 255));
            }
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
        }

        static float Edge(float d, float soft) => Mathf.Clamp01(d / soft);
    }
}
