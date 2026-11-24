using System.Collections.Generic;
using HadalWorks.Sim;
using UnityEngine;
using Terrain = HadalWorks.Sim.Terrain;

namespace HadalWorks.Game
{
    /// <summary>
    /// Gray-box 3D view of the seabed factory. Mirrors the World every frame; never changes it.
    /// One tile = 2 m. Tile (x, y) sits at world (x*2+1, 0, y*2+1).
    /// </summary>
    public class WorldView : MonoBehaviour
    {
        public const float T = 2f;
        World _w;

        class BView
        {
            public GameObject Root;
            public Transform Body;      // rotated to face
            public TextMesh Title;
            public TextMesh Status;
            public int Facing = -1;
            public int Recipe = -1;
        }

        readonly Dictionary<int, BView> _views = new Dictionary<int, BView>();
        readonly List<Transform> _itemPool = new List<Transform>();
        readonly List<TextMesh> _itemLabels = new List<TextMesh>();
        readonly List<Item> _itemKinds = new List<Item>();
        Transform _items;

        // Ghost (placement preview) and selection marker.
        GameObject _ghost;
        Transform _ghostBox;
        Transform _ghostArrow;
        TextMesh _ghostText;
        readonly List<GameObject> _tubeGhosts = new List<GameObject>();
        GameObject _select;

        public static Vector3 TileCenter(int x, int y) => new Vector3(x * T + T / 2f, 0f, y * T + T / 2f);
        public static Vector3 FootCenter(int x, int y, int size) => new Vector3(x * T + size * T / 2f, 0f, y * T + size * T / 2f);
        public static Vector3 DirVec(int d) => new Vector3(Dir.DX[d], 0f, Dir.DY[d]);
        public static Quaternion FacingRot(int d) => Quaternion.LookRotation(DirVec(d));

        public static Color ItemColor(Item i)
        {
            switch (i)
            {
                case Item.Ferrite: return new Color(0.6f, 0.3f, 0.2f);
                case Item.Silica: return new Color(0.85f, 0.82f, 0.7f);
                case Item.Kelp: return Look.KelpGlow;
                case Item.Plate: return new Color(0.7f, 0.72f, 0.75f);
                case Item.Glass: return new Color(0.6f, 0.85f, 0.95f);
                case Item.Sealant: return new Color(0.2f, 0.75f, 0.45f);
                case Item.Hull: return new Color(0.95f, 0.75f, 0.2f);
                case Item.Viewport: return new Color(0.55f, 0.65f, 1f);
                default: return Color.white;
            }
        }

        public void Build(World w)
        {
            _w = w;
            var root = transform;
            float size = w.W * T;

            Look.Box("Seabed", root, new Vector3(size / 2f, -0.15f, size / 2f), new Vector3(size, 0.3f, size), Look.Seabed);
            Look.Box("Abyss", root, new Vector3(size / 2f, -0.6f, size / 2f), new Vector3(size * 3f, 0.3f, size * 3f), new Color(0.05f, 0.07f, 0.09f));

            for (int y = 0; y < w.H; y++)
                for (int x = 0; x < w.W; x++)
                {
                    var t = w.GroundAt(x, y);
                    if (t == Terrain.Sand) continue;
                    BuildGroundTile(root, x, y, t);
                }
            foreach (var l in MapGen.Labels)
                Look.Label(l.text, root, TileCenter(l.x, l.y) + new Vector3(0f, 0.1f, 0f), 3.2f, new Color(0.85f, 0.9f, 0.95f));

            BuildLights(root);

            _items = new GameObject("Items").transform;
            _items.SetParent(root, false);

            _ghost = new GameObject("Ghost");
            _ghost.transform.SetParent(root, false);
            _ghostBox = Look.Box("GhostBox", _ghost.transform, Vector3.zero, Vector3.one, Look.Ok).transform;
            _ghostArrow = Look.Box("GhostArrow", _ghost.transform, Vector3.zero, new Vector3(0.5f, 0.3f, 0.8f), Look.Accent).transform;
            _ghostText = Look.Label("", _ghost.transform, new Vector3(0f, 3.2f, 0f), 1.4f, Color.white);
            _ghost.SetActive(false);

            _select = Look.Box("Selected", root, Vector3.zero, Vector3.one, Look.Select);
            _select.SetActive(false);
        }

        void BuildGroundTile(Transform root, int x, int y, Terrain t)
        {
            var c = TileCenter(x, y);
            // Small deterministic jitter so patches don't look like a chessboard.
            float j = ((x * 73856093) ^ (y * 19349663)) % 100 / 100f;
            switch (t)
            {
                case Terrain.Ferrite:
                    Look.Box("Ferrite", root, c + new Vector3(0f, 0.02f, 0f), new Vector3(T, 0.06f, T), Look.FerriteGround);
                    Look.Box("Nodule", root, c + new Vector3(-0.4f + j * 0.3f, 0.25f, 0.3f), new Vector3(0.6f, 0.45f, 0.55f), new Color(0.32f, 0.16f, 0.12f));
                    Look.Box("Nodule", root, c + new Vector3(0.45f, 0.18f, -0.35f + j * 0.4f), new Vector3(0.45f, 0.3f, 0.5f), new Color(0.36f, 0.2f, 0.14f));
                    break;
                case Terrain.Silica:
                    Look.Box("Silica", root, c + new Vector3(0f, 0.03f + j * 0.05f, 0f), new Vector3(T, 0.1f, T), Look.SilicaGround);
                    break;
                case Terrain.Kelp:
                    Look.Box("KelpBed", root, c + new Vector3(0f, 0.02f, 0f), new Vector3(T, 0.06f, T), Look.KelpGround);
                    float h = 2.5f + j * 2.5f;
                    Look.Box("Kelp", root, c + new Vector3(-0.4f, h / 2f, 0.2f), new Vector3(0.15f, h, 0.15f), Look.KelpGlow);
                    Look.Box("Kelp", root, c + new Vector3(0.45f, h * 0.35f, -0.4f), new Vector3(0.15f, h * 0.7f, 0.15f), Look.KelpGlow);
                    break;
                case Terrain.Vent:
                    Look.Box("Vent", root, c + new Vector3(0f, 0.5f, 0f), new Vector3(1.6f, 1f, 1.6f), Look.Vent);
                    Look.Box("VentGlow", root, c + new Vector3(0f, 1.02f, 0f), new Vector3(0.7f, 0.05f, 0.7f), Look.VentGlow);
                    PointLight(root, c + new Vector3(0f, 2.5f, 0f), Look.VentGlow, 2.5f, 12f);
                    Look.Label("VENT", root, c + new Vector3(0f, 0.1f, -1.4f), 0.9f, Look.VentGlow);
                    break;
            }
        }

        void BuildLights(Transform root)
        {
            var sun = new GameObject("Surface light").AddComponent<Light>();
            sun.transform.SetParent(root, false);
            sun.type = LightType.Directional;
            sun.color = new Color(0.45f, 0.7f, 0.9f);
            sun.intensity = 0.55f;
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(70f, -20f, 0f);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.12f, 0.2f, 0.26f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(0.02f, 0.09f, 0.14f);
            RenderSettings.fogDensity = 0.0065f;

            var hub = FootCenter(MapGen.HubX, MapGen.HubY, 4);
            PointLight(root, hub + new Vector3(0f, 10f, 0f), new Color(0.9f, 0.95f, 1f), 2.5f, 40f);
        }

        static void PointLight(Transform root, Vector3 pos, Color c, float intensity, float range)
        {
            var l = new GameObject("Light").AddComponent<Light>();
            l.transform.SetParent(root, false);
            l.transform.localPosition = pos;
            l.type = LightType.Point;
            l.color = c;
            l.intensity = intensity;
            l.range = range;
        }

        // ------------------------------------------------------------------ buildings

        BView MakeView(Building b)
        {
            var v = new BView();
            v.Root = new GameObject(b.Type + " #" + b.Id);
            v.Root.transform.SetParent(transform, false);
            v.Root.transform.localPosition = FootCenter(b.X, b.Y, b.Size);
            v.Body = new GameObject("Body").transform;
            v.Body.SetParent(v.Root.transform, false);
            float s = b.Size * T;
            switch (b.Type)
            {
                case BuildingType.Tube:
                    Look.Box("Tube", v.Body, new Vector3(0f, 0.35f, 0f), new Vector3(0.7f, 0.5f, T), Look.Tube);
                    Look.Box("Chevron", v.Body, new Vector3(0f, 0.62f, 0.45f), new Vector3(0.35f, 0.06f, 0.35f), Look.Accent);
                    break;
                case BuildingType.Extractor:
                    Look.Box("Base", v.Body, new Vector3(0f, 0.5f, 0f), new Vector3(s - 0.4f, 1f, s - 0.4f), Look.MachineDark);
                    Look.Box("Drill", v.Body, new Vector3(0f, 2f, -0.4f), new Vector3(1.1f, 3f, 1.1f), Look.Machine);
                    Look.Box("Front", v.Body, new Vector3(0f, 0.6f, s / 2f - 0.15f), new Vector3(1.6f, 0.8f, 0.3f), Look.Accent);
                    break;
                case BuildingType.Processor:
                    Look.Box("Body", v.Body, new Vector3(0f, 1f, 0f), new Vector3(s - 0.4f, 2f, s - 0.4f), Look.Machine);
                    Look.Box("Stack", v.Body, new Vector3(-0.9f, 2.6f, -0.9f), new Vector3(0.7f, 1.4f, 0.7f), Look.MachineDark);
                    Look.Box("Front", v.Body, new Vector3(0f, 0.6f, s / 2f - 0.15f), new Vector3(1.6f, 0.8f, 0.3f), Look.Accent);
                    break;
                case BuildingType.Fabricator:
                    Look.Box("Body", v.Body, new Vector3(0f, 1.3f, 0f), new Vector3(s - 0.3f, 2.6f, s - 0.3f), new Color(0.48f, 0.55f, 0.65f));
                    Look.Box("Arm", v.Body, new Vector3(0.8f, 3f, 0f), new Vector3(0.4f, 1f, 2.4f), Look.MachineDark);
                    Look.Box("Front", v.Body, new Vector3(0f, 0.6f, s / 2f - 0.1f), new Vector3(1.6f, 0.8f, 0.3f), Look.Accent);
                    break;
                case BuildingType.VentTap:
                    Look.Box("Tap", v.Body, new Vector3(0f, 1.3f, 0f), new Vector3(1.5f, 2.6f, 1.5f), new Color(0.6f, 0.45f, 0.3f));
                    Look.Box("Coil", v.Body, new Vector3(0f, 2.8f, 0f), new Vector3(1.8f, 0.3f, 1.8f), Look.VentGlow);
                    break;
                case BuildingType.Hub:
                    Look.Box("Dome", v.Body, new Vector3(0f, 1.6f, 0f), new Vector3(s - 0.6f, 3.2f, s - 0.6f), Look.Hub);
                    Look.Box("Top", v.Body, new Vector3(0f, 3.8f, 0f), new Vector3(s * 0.55f, 1.2f, s * 0.55f), new Color(0.6f, 0.75f, 0.9f));
                    Look.Box("Mast", v.Body, new Vector3(0f, 6f, 0f), new Vector3(0.3f, 3.5f, 0.3f), Look.MachineDark);
                    break;
            }
            if (b.Type != BuildingType.Tube)
            {
                float top = b.Type == BuildingType.Hub ? 4.5f : b.Type == BuildingType.Fabricator ? 3.6f : b.Type == BuildingType.Extractor ? 3.6f : 3.4f;
                v.Title = Look.Label("", v.Root.transform, new Vector3(0f, top, 0f), b.Type == BuildingType.Hub ? 2.2f : 1.3f, Color.white);
                v.Status = Look.Label("", v.Root.transform, new Vector3(0f, top, -1.1f * b.Size / 2f - 0.4f), 0.9f, new Color(0.8f, 0.9f, 1f));
            }
            return v;
        }

        string TitleFor(Building b)
        {
            switch (b.Type)
            {
                case BuildingType.Extractor:
                    var res = _w.ExtractorResource(b.X, b.Y, b.Size, out _);
                    return "EXTRACT " + Items.Letter(res);
                case BuildingType.Processor:
                case BuildingType.Fabricator:
                    var r = _w.RecipeOf(b);
                    return (b.Type == BuildingType.Processor ? "PROC " : "FAB ") + Items.Letter(r.Out) + "\n" + r.Name;
                case BuildingType.VentTap: return "POWER +" + _w.B.Defs[b.Type].Supply;
                case BuildingType.Hub: return "HUB";
                default: return "";
            }
        }

        void LateUpdate()
        {
            if (_w == null) return;

            // Create / remove building views.
            foreach (var b in _w.Buildings.Values)
                if (!_views.ContainsKey(b.Id)) _views[b.Id] = MakeView(b);
            List<int> gone = null;
            foreach (var kv in _views)
                if (!_w.Buildings.ContainsKey(kv.Key)) (gone ??= new List<int>()).Add(kv.Key);
            if (gone != null) foreach (int id in gone) { Destroy(_views[id].Root); _views.Remove(id); }

            foreach (var b in _w.Buildings.Values)
            {
                var v = _views[b.Id];
                if (v.Facing != b.Facing) { v.Body.localRotation = FacingRot(b.Facing); v.Facing = b.Facing; }
                if (v.Title != null)
                {
                    if (v.Recipe != b.Recipe || v.Title.text.Length == 0) { v.Title.text = TitleFor(b); v.Recipe = b.Recipe; }
                    if (b.Type != BuildingType.Hub) v.Status.text = b.Status;
                }
            }

            UpdateItems();

            var cam = Camera.main;
            if (cam != null) Look.TurnFlatLabels(cam.transform.eulerAngles.y);
        }

        void UpdateItems()
        {
            int n = 0;
            foreach (var b in _w.Buildings.Values)
            {
                if (b.Type != BuildingType.Tube || b.TubeItem == Item.None) continue;
                var tr = ItemAt(n, b.TubeItem);
                var c = TileCenter(b.X, b.Y);
                tr.localPosition = c + DirVec(b.Facing) * ((b.TubeProgress * 2f - 1f) * 0.85f) + new Vector3(0f, 0.95f, 0f);
                n++;
            }
            for (int i = n; i < _itemPool.Count; i++)
                if (_itemPool[i].gameObject.activeSelf) _itemPool[i].gameObject.SetActive(false);
        }

        Transform ItemAt(int i, Item kind)
        {
            while (_itemPool.Count <= i)
            {
                var go = new GameObject("Item");
                go.transform.SetParent(_items, false);
                var box = Look.Box("Crate", go.transform, Vector3.zero, new Vector3(0.6f, 0.6f, 0.6f), Color.white);
                var label = Look.Label("", go.transform, new Vector3(0f, 0.32f, 0f), 0.7f, Color.black);
                _itemPool.Add(go.transform);
                _itemLabels.Add(label);
                _itemKinds.Add(Item.None);
            }
            var tr = _itemPool[i];
            if (!tr.gameObject.activeSelf) tr.gameObject.SetActive(true);
            if (_itemKinds[i] != kind)
            {
                _itemKinds[i] = kind;
                tr.GetChild(0).GetComponent<Renderer>().sharedMaterial = Look.Mat(ItemColor(kind));
                _itemLabels[i].text = Items.Letter(kind);
            }
            return tr;
        }

        // ------------------------------------------------------------------ ghost / selection

        public void HideGhost()
        {
            _ghost.SetActive(false);
            foreach (var g in _tubeGhosts) g.SetActive(false);
        }

        public void ShowGhost(BuildingType type, int x, int y, int facing, string reason)
        {
            foreach (var g in _tubeGhosts) g.SetActive(false);
            int size = _w.B.Defs[type].Size;
            _ghost.SetActive(true);
            _ghost.transform.localPosition = FootCenter(x, y, size);
            _ghostBox.localScale = new Vector3(size * T - 0.2f, 0.25f, size * T - 0.2f);
            _ghostBox.localPosition = new Vector3(0f, 0.15f, 0f);
            _ghostBox.GetComponent<Renderer>().sharedMaterial = Look.Mat(reason == null ? Look.Ok : Look.Bad);
            _ghostArrow.gameObject.SetActive(type != BuildingType.VentTap);
            _ghostArrow.localPosition = DirVec(facing) * (size * T / 2f + 0.3f) + new Vector3(0f, 0.3f, 0f);
            _ghostArrow.localRotation = FacingRot(facing);
            _ghostText.text = (reason ?? "OK  ·  click to build") + "\nfacing " + Dir.Names[facing] + "  (R to turn)";
        }

        public void ShowTubeGhosts(List<(int x, int y, int d)> path, int okCount, string reason)
        {
            _ghost.SetActive(true);
            _ghostBox.localScale = Vector3.zero;
            _ghostArrow.gameObject.SetActive(false);
            var last = path[path.Count - 1];
            _ghost.transform.localPosition = TileCenter(last.x, last.y);
            _ghostText.text = okCount + " of " + path.Count + " tubes" + (reason != null ? "  ·  " + reason : "  ·  release to build");
            while (_tubeGhosts.Count < path.Count)
            {
                var g = new GameObject("TubeGhost");
                g.transform.SetParent(transform, false);
                Look.Box("Box", g.transform, new Vector3(0f, 0.25f, 0f), new Vector3(0.9f, 0.3f, T - 0.2f), Look.Ok);
                Look.Box("Chevron", g.transform, new Vector3(0f, 0.45f, 0.5f), new Vector3(0.4f, 0.08f, 0.4f), Look.Accent);
                _tubeGhosts.Add(g);
            }
            for (int i = 0; i < _tubeGhosts.Count; i++)
            {
                var g = _tubeGhosts[i];
                if (i >= path.Count) { g.SetActive(false); continue; }
                g.SetActive(true);
                g.transform.localPosition = TileCenter(path[i].x, path[i].y);
                g.transform.localRotation = FacingRot(path[i].d);
                g.transform.GetChild(0).GetComponent<Renderer>().sharedMaterial = Look.Mat(i < okCount ? Look.Ok : Look.Bad);
            }
        }

        public void ShowSelection(Building b)
        {
            if (b == null) { _select.SetActive(false); return; }
            _select.SetActive(true);
            _select.transform.localPosition = FootCenter(b.X, b.Y, b.Size) + new Vector3(0f, 0.04f, 0f);
            _select.transform.localScale = new Vector3(b.Size * T + 0.6f, 0.05f, b.Size * T + 0.6f);
        }
    }
}
