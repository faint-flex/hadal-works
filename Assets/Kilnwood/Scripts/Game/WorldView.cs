using System.Collections.Generic;
using Kilnwood.Sim;
using UnityEngine;
using Terrain = Kilnwood.Sim.Terrain;

namespace Kilnwood.Game
{
    /// <summary>
    /// Gray-box 3D view of the valley factory. Mirrors the World every frame; never changes it.
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
            public string Shown;
            public int Facing = -1;
            public int Recipe = -1;
            public GameObject Fire;     // glow shown while the machine burns fuel (or a kiln smoulders)
        }

        readonly Dictionary<int, BView> _views = new Dictionary<int, BView>();
        // Trees and rocks per tile, hidden when something is built on that tile.
        readonly Dictionary<int, List<GameObject>> _deco = new Dictionary<int, List<GameObject>>();
        int _decoBuildingCount = -1;
        // Big field names on the ground; hidden once you build near them so they don't fight machine labels.
        readonly List<(int x, int y, GameObject go)> _fieldLabels = new List<(int x, int y, GameObject go)>();
        readonly List<Transform> _itemPool = new List<Transform>();
        readonly List<TextMesh> _itemLabels = new List<TextMesh>();
        readonly List<Item> _itemKinds = new List<Item>();
        Transform _items;

        // Ghost (placement preview) and selection marker.
        GameObject _ghost;
        Transform _ghostBox;
        Transform _ghostArrow;
        TextMesh _ghostText;
        readonly List<GameObject> _beltGhosts = new List<GameObject>();
        GameObject _select;

        public static Vector3 TileCenter(int x, int y) => new Vector3(x * T + T / 2f, 0f, y * T + T / 2f);
        public static Vector3 FootCenter(int x, int y, int size) => new Vector3(x * T + size * T / 2f, 0f, y * T + size * T / 2f);
        public static Vector3 DirVec(int d) => new Vector3(Dir.DX[d], 0f, Dir.DY[d]);
        public static Quaternion FacingRot(int d) => Quaternion.LookRotation(DirVec(d));

        public static Color ItemColor(Item i)
        {
            switch (i)
            {
                case Item.Wood: return new Color(0.72f, 0.52f, 0.3f);
                case Item.Ore: return new Color(0.55f, 0.32f, 0.25f);
                case Item.Stone: return new Color(0.7f, 0.7f, 0.68f);
                case Item.Charcoal: return new Color(0.16f, 0.16f, 0.17f);
                case Item.Iron: return new Color(0.62f, 0.66f, 0.72f);
                case Item.Brick: return new Color(0.75f, 0.33f, 0.22f);
                case Item.Gear: return new Color(0.95f, 0.78f, 0.25f);
                default: return Color.white;
            }
        }

        public void Build(World w)
        {
            _w = w;
            var root = transform;
            float size = w.W * T;

            Look.Box("Valley floor", root, new Vector3(size / 2f, -0.15f, size / 2f), new Vector3(size, 0.3f, size), Look.Grass);
            Look.Box("Hills", root, new Vector3(size / 2f, -0.6f, size / 2f), new Vector3(size * 3f, 0.3f, size * 3f), Look.FarHills);

            for (int y = 0; y < w.H; y++)
                for (int x = 0; x < w.W; x++)
                {
                    var t = w.GroundAt(x, y);
                    if (t == Terrain.Grass) continue;
                    BuildGroundTile(root, x, y, t);
                }
            foreach (var l in MapGen.Labels)
                _fieldLabels.Add((l.x, l.y, Look.Label(l.text, root, TileCenter(l.x, l.y) + new Vector3(0f, 0.1f, 0f), 3.2f, new Color(1f, 0.97f, 0.85f)).gameObject));

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
            float j = Mathf.Abs((x * 73856093) ^ (y * 19349663)) % 100 / 100f;
            switch (t)
            {
                case Terrain.Forest:
                    Look.Box("Leaf litter", root, c + new Vector3(0f, 0.02f, 0f), new Vector3(T, 0.06f, T), Look.ForestFloor);
                    // One or two trees per tile: trunk + layered crown.
                    float h = 3.5f + j * 2.5f;
                    var tc = c + new Vector3(-0.35f + j * 0.5f, 0f, 0.3f - j * 0.5f);
                    Deco(x, y, Look.Box("Trunk", root, tc + new Vector3(0f, h * 0.3f, 0f), new Vector3(0.3f, h * 0.6f, 0.3f), Look.Bark));
                    Deco(x, y, Look.Box("Crown", root, tc + new Vector3(0f, h * 0.7f, 0f), new Vector3(1.5f, h * 0.45f, 1.5f), j < 0.5f ? Look.Leaves : Look.LeavesDark));
                    Deco(x, y, Look.Box("Crown top", root, tc + new Vector3(0f, h * 1.0f, 0f), new Vector3(0.9f, h * 0.25f, 0.9f), j < 0.5f ? Look.Leaves : Look.LeavesDark));
                    break;
                case Terrain.IronOre:
                    Look.Box("Ore ground", root, c + new Vector3(0f, 0.02f, 0f), new Vector3(T, 0.06f, T), Look.OreGround);
                    Deco(x, y, Look.Box("Ore rock", root, c + new Vector3(-0.4f + j * 0.3f, 0.25f, 0.3f), new Vector3(0.7f, 0.5f, 0.6f), Look.OreRock));
                    Deco(x, y, Look.Box("Ore rock", root, c + new Vector3(0.45f, 0.18f, -0.35f + j * 0.4f), new Vector3(0.5f, 0.35f, 0.5f), Look.OreRock));
                    break;
                case Terrain.Stone:
                    Look.Box("Stone ground", root, c + new Vector3(0f, 0.04f, 0f), new Vector3(T, 0.1f, T), Look.StoneGround);
                    Deco(x, y, Look.Box("Boulder", root, c + new Vector3(0.3f - j * 0.4f, 0.35f, -0.2f), new Vector3(0.9f, 0.7f, 0.8f), Look.Boulder));
                    break;
                case Terrain.Water:
                    Look.Box("River", root, c + new Vector3(0f, 0.03f, 0f), new Vector3(T, 0.06f, T), Look.Water);
                    break;
            }
        }

        void Deco(int x, int y, GameObject go)
        {
            int key = y * _w.W + x;
            if (!_deco.TryGetValue(key, out var list)) _deco[key] = list = new List<GameObject>();
            list.Add(go);
        }

        void UpdateDeco()
        {
            _decoBuildingCount = _w.Buildings.Count;
            // Trees and rocks clear on built tiles and one tile around them, so belts stay visible in forests.
            foreach (var kv in _deco)
            {
                bool show = !BuiltNear(kv.Key % _w.W, kv.Key / _w.W, 1);
                foreach (var go in kv.Value) if (go.activeSelf != show) go.SetActive(show);
            }
            foreach (var fl in _fieldLabels)
            {
                bool show = !BuiltNear(fl.x, fl.y, 4);
                if (fl.go.activeSelf != show) fl.go.SetActive(show);
            }
        }

        bool BuiltNear(int x, int y, int r)
        {
            for (int dy = -r; dy <= r; dy++)
                for (int dx = -r; dx <= r; dx++)
                    if (_w.At(x + dx, y + dy) != null) return true;
            return false;
        }

        void BuildLights(Transform root)
        {
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.transform.SetParent(root, false);
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.93f, 0.8f);
            sun.intensity = 1.15f;
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(50f, -35f, 0f);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.55f, 0.65f, 0.78f);
            RenderSettings.ambientEquatorColor = new Color(0.45f, 0.48f, 0.42f);
            RenderSettings.ambientGroundColor = new Color(0.22f, 0.2f, 0.15f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(0.66f, 0.74f, 0.8f);
            RenderSettings.fogDensity = 0.0035f;

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
                case BuildingType.Belt:
                    Look.Box("Frame", v.Body, new Vector3(0f, 0.2f, 0f), new Vector3(1.5f, 0.3f, T), Look.BeltFrame);
                    Look.Box("Belt", v.Body, new Vector3(0f, 0.38f, 0f), new Vector3(1.2f, 0.06f, T), Look.Belt);
                    Look.Box("Chevron", v.Body, new Vector3(0f, 0.43f, 0.45f), new Vector3(0.35f, 0.04f, 0.35f), Look.Accent);
                    break;
                case BuildingType.Woodcutter:
                    Look.Box("Yard", v.Body, new Vector3(0f, 0.1f, 0f), new Vector3(s - 0.3f, 0.2f, s - 0.3f), Look.Planks);
                    Look.Box("Shed", v.Body, new Vector3(-0.6f, 1.2f, -0.6f), new Vector3(2f, 2f, 2f), Look.Timber);
                    Look.Box("Roof", v.Body, new Vector3(-0.6f, 2.4f, -0.6f), new Vector3(2.4f, 0.4f, 2.4f), Look.Roof);
                    Look.Box("Log pile", v.Body, new Vector3(0.9f, 0.45f, 0.4f), new Vector3(1.2f, 0.7f, 1.6f), Look.Bark);
                    Look.Box("Front", v.Body, new Vector3(0f, 0.5f, s / 2f - 0.15f), new Vector3(1.6f, 0.6f, 0.3f), Look.Accent);
                    break;
                case BuildingType.Mine:
                    Look.Box("Pit", v.Body, new Vector3(0f, 0.1f, 0f), new Vector3(s - 0.3f, 0.2f, s - 0.3f), Look.MineDark);
                    Look.Box("Headframe L", v.Body, new Vector3(-0.9f, 2f, -0.4f), new Vector3(0.3f, 4f, 0.3f), Look.Timber);
                    Look.Box("Headframe R", v.Body, new Vector3(0.9f, 2f, -0.4f), new Vector3(0.3f, 4f, 0.3f), Look.Timber);
                    Look.Box("Headframe top", v.Body, new Vector3(0f, 4f, -0.4f), new Vector3(2.2f, 0.35f, 0.6f), Look.Timber);
                    Look.Box("Boiler", v.Body, new Vector3(1.1f, 0.8f, -1.2f), new Vector3(1f, 1.4f, 1f), Look.Iron);
                    v.Fire = Look.Box("Firebox", v.Body, new Vector3(1.1f, 0.4f, -0.66f), new Vector3(0.6f, 0.4f, 0.1f), Look.Fire);
                    Look.Box("Front", v.Body, new Vector3(0f, 0.5f, s / 2f - 0.15f), new Vector3(1.6f, 0.6f, 0.3f), Look.Accent);
                    break;
                case BuildingType.Kiln:
                    // Beehive kiln: stepped rings that narrow toward the top.
                    Look.Box("Ring 1", v.Body, new Vector3(0f, 0.6f, 0f), new Vector3(s - 0.5f, 1.2f, s - 0.5f), Look.Brickwork);
                    Look.Box("Ring 2", v.Body, new Vector3(0f, 1.55f, 0f), new Vector3(s - 1.0f, 0.7f, s - 1.0f), Look.Brickwork);
                    Look.Box("Ring 3", v.Body, new Vector3(0f, 2.15f, 0f), new Vector3(s - 1.6f, 0.5f, s - 1.6f), Look.Brickwork);
                    Look.Box("Ring 4", v.Body, new Vector3(0f, 2.55f, 0f), new Vector3(s - 2.4f, 0.3f, s - 2.4f), Look.Brickwork);
                    Look.Box("Chimney", v.Body, new Vector3(0.9f, 3.2f, -0.9f), new Vector3(0.6f, 1.8f, 0.6f), Look.MineDark);
                    v.Fire = Look.Box("Fire mouth", v.Body, new Vector3(0f, 0.6f, s / 2f - 0.28f), new Vector3(1.1f, 0.8f, 0.1f), Look.Fire);
                    Look.Box("Front", v.Body, new Vector3(0f, 0.2f, s / 2f - 0.15f), new Vector3(1.6f, 0.3f, 0.3f), Look.Accent);
                    break;
                case BuildingType.Smelter:
                    Look.Box("Base", v.Body, new Vector3(0f, 0.6f, 0f), new Vector3(s - 0.4f, 1.2f, s - 0.4f), Look.StoneWall);
                    Look.Box("Furnace", v.Body, new Vector3(0f, 2.2f, -0.2f), new Vector3(1.8f, 2f, 1.8f), Look.StoneWall);
                    Look.Box("Stack", v.Body, new Vector3(0f, 4f, -0.2f), new Vector3(0.8f, 1.6f, 0.8f), Look.MineDark);
                    v.Fire = Look.Box("Glow", v.Body, new Vector3(0f, 1.6f, 0.72f), new Vector3(0.8f, 0.6f, 0.1f), Look.Fire);
                    Look.Box("Front", v.Body, new Vector3(0f, 0.5f, s / 2f - 0.15f), new Vector3(1.6f, 0.6f, 0.3f), Look.Accent);
                    break;
                case BuildingType.Workshop:
                    Look.Box("Walls", v.Body, new Vector3(0f, 1.2f, 0f), new Vector3(s - 0.4f, 2.4f, s - 0.6f), Look.Timber);
                    Look.Box("Roof", v.Body, new Vector3(0f, 2.6f, 0f), new Vector3(s, 0.4f, s - 0.2f), Look.Roof);
                    Look.Box("Forge chimney", v.Body, new Vector3(-1f, 3.2f, -0.8f), new Vector3(0.5f, 1.6f, 0.5f), Look.StoneWall);
                    Look.Box("Waterwheel gear", v.Body, new Vector3(1.75f, 1.4f, 0f), new Vector3(0.2f, 1.6f, 1.6f), Look.Iron);
                    v.Fire = Look.Box("Forge glow", v.Body, new Vector3(-0.6f, 0.9f, s / 2f - 0.28f), new Vector3(0.8f, 0.5f, 0.1f), Look.Fire);
                    Look.Box("Front", v.Body, new Vector3(0f, 0.2f, s / 2f - 0.15f), new Vector3(1.6f, 0.3f, 0.3f), Look.Accent);
                    break;
                case BuildingType.Lodge:
                    Look.Box("Stone base", v.Body, new Vector3(0f, 0.4f, 0f), new Vector3(s - 0.4f, 0.8f, s - 0.4f), Look.StoneWall);
                    Look.Box("Log walls", v.Body, new Vector3(0f, 2f, 0f), new Vector3(s - 1.4f, 2.4f, s - 1.4f), Look.Timber);
                    Look.Box("Roof", v.Body, new Vector3(0f, 3.6f, 0f), new Vector3(s - 0.6f, 0.8f, s - 0.6f), Look.Roof);
                    Look.Box("Roof ridge", v.Body, new Vector3(0f, 4.3f, 0f), new Vector3(s - 3.4f, 0.6f, s - 0.8f), Look.Roof);
                    Look.Box("Chimney", v.Body, new Vector3(2f, 4.6f, -1.6f), new Vector3(0.8f, 2f, 0.8f), Look.StoneWall);
                    Look.Box("Flag pole", v.Body, new Vector3(-3f, 3.5f, -3f), new Vector3(0.15f, 7f, 0.15f), Look.Bark);
                    Look.Box("Flag", v.Body, new Vector3(-2.4f, 6.4f, -3f), new Vector3(1.1f, 0.7f, 0.05f), Look.Accent);
                    break;
            }
            if (v.Fire != null)
            {
                v.Fire.GetComponent<Renderer>().sharedMaterial = Look.GlowMat(Look.Fire);
                var glow = new GameObject("Fire light").AddComponent<Light>();
                glow.transform.SetParent(v.Fire.transform, false);
                glow.transform.localPosition = new Vector3(0f, 0f, 4f);
                glow.type = LightType.Point;
                glow.color = Look.FireLight;
                glow.intensity = 1.6f;
                glow.range = 5f;
                v.Fire.SetActive(false);
            }
            if (b.Type != BuildingType.Belt)
            {
                // One upright label per machine: name on top, status (and fuel) underneath.
                // Neighbouring machines alternate label heights so their names don't stack on each other.
                float top = (b.Type == BuildingType.Mine || b.Type == BuildingType.Smelter ? 5.2f : 4.2f) + ((b.X / 2 + b.Y / 2) % 2) * 2f;
                if (b.Type == BuildingType.Lodge)
                {
                    // Lodge name sits in front (south side), clear of the machines behind it.
                    v.Title = Look.Billboard("", v.Root.transform, new Vector3(0f, 0.5f, -s / 2f - 0.6f), 2.0f, Color.white);
                    v.Title.anchor = TextAnchor.UpperCenter;
                }
                else v.Title = Look.Billboard("", v.Root.transform, new Vector3(0f, top, 0f), 1.3f, Color.white);
            }
            return v;
        }

        string TitleFor(Building b)
        {
            switch (b.Type)
            {
                case BuildingType.Woodcutter: return "WOODCUTTER W";
                case BuildingType.Mine: return "MINE " + Items.Letter(_w.ExtractorResource(b, out _));
                case BuildingType.Kiln:
                case BuildingType.Smelter:
                case BuildingType.Workshop:
                    return _w.B.Defs[b.Type].Name.ToUpperInvariant() + " " + Items.Letter(_w.RecipeOf(b).Out);
                case BuildingType.Lodge: return "LODGE\n<size=40>deliver here</size>";
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
                    // Calm machines show only their name (fire glow shows they're busy); problems get a "!" line.
                    string status = b.Type == BuildingType.Lodge || string.IsNullOrEmpty(b.Status) || b.Status == "Working" || b.Status.StartsWith("Full") ? "" : b.Status;
                    string text = TitleFor(b) + (status.Length > 0 ? "\n<size=56><color=#FFB070>! " + status + "</color></size>" : "");
                    if (v.Shown != text) { Look.SetText(v.Title, text); v.Shown = text; v.Recipe = b.Recipe; }
                }
            }

            // Fire glow: on while burning fuel, or while a charcoal kiln smoulders.
            foreach (var b in _w.Buildings.Values)
            {
                var v = _views[b.Id];
                if (v.Fire == null) continue;
                bool lit = b.Status == "Working" && (_w.BurnsFuel(b) || b.Type == BuildingType.Kiln || b.Type == BuildingType.Smelter);
                if (v.Fire.activeSelf != lit) v.Fire.SetActive(lit);
            }

            if (_w.Buildings.Count != _decoBuildingCount || gone != null) UpdateDeco();

            UpdateItems();

            var cam = Camera.main;
            if (cam != null) { Look.TurnFlatLabels(cam.transform.eulerAngles.y); Look.TurnBillboards(cam.transform.rotation); }
        }

        void UpdateItems()
        {
            int n = 0;
            foreach (var b in _w.Buildings.Values)
            {
                if (b.Type != BuildingType.Belt || b.BeltItem == Item.None) continue;
                var tr = ItemAt(n, b.BeltItem);
                var c = TileCenter(b.X, b.Y);
                tr.localPosition = c + DirVec(b.Facing) * ((b.BeltProgress * 2f - 1f) * 0.85f) + new Vector3(0f, 0.95f, 0f);
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
                var ic = ItemColor(kind);
                _itemLabels[i].color = (ic.r + ic.g + ic.b) / 3f < 0.45f ? Color.white : Color.black;
            }
            return tr;
        }

        // ------------------------------------------------------------------ ghost / selection

        public void HideGhost()
        {
            _ghost.SetActive(false);
            foreach (var g in _beltGhosts) g.SetActive(false);
        }

        public void ShowGhost(BuildingType type, int x, int y, int facing, string reason)
        {
            foreach (var g in _beltGhosts) g.SetActive(false);
            int size = _w.B.Defs[type].Size;
            _ghost.SetActive(true);
            _ghost.transform.localPosition = FootCenter(x, y, size);
            _ghostBox.localScale = new Vector3(size * T - 0.2f, 0.25f, size * T - 0.2f);
            _ghostBox.localPosition = new Vector3(0f, 0.15f, 0f);
            _ghostBox.GetComponent<Renderer>().sharedMaterial = Look.Mat(reason == null ? Look.Ok : Look.Bad);
            _ghostArrow.gameObject.SetActive(true);
            _ghostArrow.localPosition = DirVec(facing) * (size * T / 2f + 0.3f) + new Vector3(0f, 0.3f, 0f);
            _ghostArrow.localRotation = FacingRot(facing);
            _ghostText.text = (reason ?? "OK  ·  click to build") + "\nfacing " + Dir.Names[facing] + "  (R to turn)";
        }

        public void ShowBeltGhosts(List<(int x, int y, int d)> path, int okCount, string reason)
        {
            _ghost.SetActive(true);
            _ghostBox.localScale = Vector3.zero;
            _ghostArrow.gameObject.SetActive(false);
            var last = path[path.Count - 1];
            _ghost.transform.localPosition = TileCenter(last.x, last.y);
            _ghostText.text = okCount + " of " + path.Count + " belts" + (reason != null ? "  ·  " + reason : "  ·  release to build");
            while (_beltGhosts.Count < path.Count)
            {
                var g = new GameObject("BeltGhost");
                g.transform.SetParent(transform, false);
                Look.Box("Box", g.transform, new Vector3(0f, 0.25f, 0f), new Vector3(0.9f, 0.3f, T - 0.2f), Look.Ok);
                Look.Box("Chevron", g.transform, new Vector3(0f, 0.45f, 0.5f), new Vector3(0.4f, 0.08f, 0.4f), Look.Accent);
                _beltGhosts.Add(g);
            }
            for (int i = 0; i < _beltGhosts.Count; i++)
            {
                var g = _beltGhosts[i];
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
