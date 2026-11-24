using System.Collections.Generic;
using Kilnwood.Sim;
using UnityEngine;
using Terrain = Kilnwood.Sim.Terrain;

namespace Kilnwood.Game
{
    /// <summary>
    /// 3D view of the valley factory (low-poly models built in code by Art). Mirrors the World every frame; never changes it.
    /// One tile = 2 m. Tile (x, y) sits at world (x*2+1, 0, y*2+1).
    /// </summary>
    public partial class WorldView : MonoBehaviour
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
            public Light FireLight;
            public ParticleSystem Smoke;
            public Transform Spin;      // wheel or gear that turns while working (around its local y)
            public float SpinSpeed, SpinAngle;
            public float Seed;
            public GameObject Extra;    // hopper: the heap inside
            public Transform Post;      // thin stick from the roof up to the label
            public float BaseTop;       // label height before decluttering
            public float Raise;         // extra height this frame so it clears other labels
            public BuildingType Type;
            public Transform Anim, Anim2;  // water power: wheel / blade / helve / cam that move
            public float AnimAngle;
            public GameObject[] Arms;   // line shafts: rod toward each side (N, E, S, W), shown where something connects
        }

        readonly Dictionary<int, BView> _views = new Dictionary<int, BView>();
        // Trees and rocks per tile, hidden when something is built on that tile.
        readonly Dictionary<int, List<GameObject>> _deco = new Dictionary<int, List<GameObject>>();
        int _decoBuildingCount = -1;
        // Trees per forest tile: shrink as they're cut, a stump when bare, grow back from saplings.
        readonly Dictionary<int, (Transform tree, GameObject stump)> _trees = new Dictionary<int, (Transform, GameObject)>();
        // Big field names on the ground; hidden once you build near them so they don't fight machine labels.
        readonly List<(int x, int y, GameObject go)> _fieldLabels = new List<(int x, int y, GameObject go)>();
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
                case Item.Fish: return new Color(0.55f, 0.7f, 0.78f);
                case Item.Plank: return new Color(0.86f, 0.7f, 0.46f);
                case Item.Plate: return new Color(0.5f, 0.54f, 0.6f);
                case Item.Coal: return new Color(0.09f, 0.09f, 0.1f);
                case Item.Steel: return new Color(0.72f, 0.76f, 0.82f);
                default: return Color.white;
            }
        }

        Material _beltMat, _waterMat;
        GrassView _grass;
        HillHeight _hills;

        public void Build(World w)
        {
            _w = w;
            var root = transform;
            float size = w.W * T;

            _ground = new GroundView(root, w);
            _grass = new GrassView(w);
            _grass.Rebuild();
            float riverX = (MapGen.RiverX0 + MapGen.RiverX1) / 2f * T + T / 2f;
            _hills = Art.Hills(root, size, riverX).GetComponent<HillHeight>();
            BuildRiver(root, size);
            BuildFarForest(root, size, riverX);

            for (int y = 0; y < w.H; y++)
                for (int x = 0; x < w.W; x++)
                {
                    var t = w.GroundAt(x, y);
                    if (t == Terrain.Grass || t == Terrain.Water) continue;
                    BuildGroundTile(root, x, y, t);
                }
            foreach (var l in MapGen.Labels)
                _fieldLabels.Add((l.x, l.y, Look.Label(l.text, root, TileCenter(l.x, l.y) + new Vector3(0f, 0.1f, 0f), 3.2f, new Color(1f, 0.97f, 0.85f)).gameObject));

            BuildLights(root);

            _beltMat = Art.TexturedMat(Art.BeltTexture, Color.white, 0.18f);

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

        /// <summary>One water mesh for the river tiles, plus the river running on out of the valley both ways.</summary>
        void BuildRiver(Transform root, float size)
        {
            var v = new List<Vector3>();
            var uv = new List<Vector2>();
            var tri = new List<int>();
            // Smooth strip: one left/right edge point per row (at the row's middle), joined row to row,
            // so the banks run diagonally instead of in tile steps.
            void Strip(float z0, float l0, float r0, float z1, float l1, float r1)
            {
                int i = v.Count;
                v.Add(new Vector3(l0, 0.06f, z0)); v.Add(new Vector3(r0, 0.06f, z0)); v.Add(new Vector3(r1, 0.06f, z1)); v.Add(new Vector3(l1, 0.06f, z1));
                for (int q = i; q < i + 4; q++) uv.Add(new Vector2(v[q].x / 8f, v[q].z / 8f));
                tri.Add(i); tri.Add(i + 2); tri.Add(i + 1); tri.Add(i); tri.Add(i + 3); tri.Add(i + 2);
            }
            var rows = new List<(float z, float l, float r)>();
            for (int y = 0; y < _w.H; y++)
            {
                int x0 = -1, x1 = -1;
                for (int x = 0; x < _w.W; x++)
                    if (_w.GroundAt(x, y) == Terrain.Water) { if (x0 < 0) x0 = x; x1 = x; }
                if (x0 >= 0) rows.Add((y * T + T / 2f, x0 * T - 0.25f, x1 * T + T + 0.25f));
            }
            if (rows.Count == 0) return;
            // Soften the wobble: average each row's banks with two rows either side.
            var smooth = new List<(float z, float l, float r)>();
            for (int i = 0; i < rows.Count; i++)
            {
                float l = 0f, rr = 0f; int n = 0;
                for (int d = -2; d <= 2; d++)
                {
                    int q = Mathf.Clamp(i + d, 0, rows.Count - 1);
                    l += rows[q].l; rr += rows[q].r; n++;
                }
                smooth.Add((rows[i].z, l / n, rr / n));
            }
            rows = smooth;
            var first = rows[0];
            var last = rows[rows.Count - 1];
            Strip(-size * 1.2f, first.l, first.r, first.z, first.l, first.r);   // out of the valley to the south
            for (int i = 0; i + 1 < rows.Count; i++)
                Strip(rows[i].z, rows[i].l, rows[i].r, rows[i + 1].z, rows[i + 1].l, rows[i + 1].r);
            Strip(last.z, last.l, last.r, size * 2.2f, last.l, last.r);          // and on to the north
            var m = new Mesh { name = "River" };
            m.SetVertices(v); m.SetUVs(0, uv); m.SetTriangles(tri, 0);
            m.RecalculateNormals(); m.RecalculateBounds();
            var go = new GameObject("River");
            go.transform.SetParent(root, false);
            go.AddComponent<MeshFilter>().sharedMesh = m;
            var r = go.AddComponent<MeshRenderer>();
            _waterMat = Art.TexturedMat(Art.WaterTexture, Color.white, 0.85f);
            r.sharedMaterial = _waterMat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        /// <summary>Woods on the hills around the valley, in clumps, kept off the river corridor.</summary>
        void BuildFarForest(Transform root, float size, float riverX)
        {
            var far = new GameObject("Far forest").transform;
            far.SetParent(root, false);
            const float step = 5f;
            for (float z = -size * 0.75f; z < size * 1.75f; z += step)
                for (float x = -size * 0.75f; x < size * 1.75f; x += step)
                {
                    bool inside = x > -12f && x < size + 12f && z > -12f && z < size + 12f;
                    if (inside || Mathf.Abs(x - riverX) < 12f) continue;
                    int ix = Mathf.RoundToInt(x), iz = Mathf.RoundToInt(z);
                    float clump = Mathf.PerlinNoise(x * 0.025f + 11f, z * 0.025f + 5f);
                    if (Art.Hash(ix, iz, 1) > clump * 1.25f - 0.2f) continue;
                    float px = x + (Art.Hash(ix, iz, 2) - 0.5f) * step * 0.8f, pz = z + (Art.Hash(ix, iz, 3) - 0.5f) * step * 0.8f;
                    var t = new GameObject("Far tree").transform;
                    t.SetParent(far, false);
                    t.localPosition = new Vector3(px, _hills.At(px, pz) - 0.2f, pz);
                    t.localRotation = Quaternion.Euler(0f, Art.Hash(ix, iz, 4) * 360f, 0f);
                    Art.FarTree(t, Art.Hash(ix, iz, 5), Art.Hash(ix, iz, 6));
                }
        }

        void BuildGroundTile(Transform root, int x, int y, Terrain t)
        {
            var c = TileCenter(x, y);
            int key = y * _w.W + x;
            float j = Art.Hash(x, y), k = Art.Hash(x, y, 7);
            switch (t)
            {
                case Terrain.Forest:
                {
                    // One tree per tile, pushed off-centre so the wood doesn't look planted in rows.
                    var tc = c + new Vector3((j - 0.5f) * 0.9f, 0f, (k - 0.5f) * 0.9f);
                    var tree = new GameObject("Tree").transform;
                    tree.SetParent(root, false);
                    tree.localPosition = tc;
                    tree.localRotation = Quaternion.Euler(0f, j * 360f, 0f);
                    Art.Tree(tree, Art.Hash(x, y, 9), k);
                    var stump = new GameObject("Stump");
                    stump.transform.SetParent(root, false);
                    stump.transform.localPosition = tc;
                    Art.Cyl("Stump", stump.transform, Vector3.zero, 0.42f, 0.32f, Look.Bark);
                    Art.Cyl("Cut", stump.transform, new Vector3(0f, 0.32f, 0f), 0.36f, 0.02f, new Color(0.78f, 0.64f, 0.45f));
                    stump.SetActive(false);
                    _trees[key] = (tree, stump);
                    if (j > 0.55f) Deco(x, y, Art.Ball("Bush", root, c + new Vector3(0.55f - k, 0.25f, j - 0.8f), new Vector3(0.9f, 0.6f, 0.8f), Art.LeafGreens[(int)(k * 3.99f)] * 0.85f));
                    break;
                }
                case Terrain.IronOre:
                    // Dark rock studded with rusty orange nuggets.
                    Deco(x, y, Art.RockAt("Ore rock", root, c + new Vector3(-0.4f + j * 0.3f, -0.05f, 0.3f), new Vector3(1.2f, 0.5f + k * 0.2f, 1.0f), new Color(0.32f, 0.24f, 0.22f), j));
                    Deco(x, y, Art.RockAt("Ore nugget", root, c + new Vector3(-0.3f + j * 0.3f, 0.3f, 0.45f), new Vector3(0.35f, 0.25f, 0.3f), new Color(0.72f, 0.36f, 0.18f), k));
                    Deco(x, y, Art.RockAt("Ore nugget", root, c + new Vector3(0.5f, -0.02f, -0.45f + k * 0.4f), new Vector3(0.5f, 0.28f, 0.45f), new Color(0.66f, 0.32f, 0.17f), j));
                    if (k > 0.45f) Deco(x, y, Art.RockAt("Ore nugget", root, c + new Vector3(0.15f, -0.02f, 0.7f), new Vector3(0.3f, 0.18f, 0.3f), new Color(0.76f, 0.4f, 0.2f), j));
                    break;
                case Terrain.Stone:
                    // Pale grey boulders: wide and low, with a smaller one beside.
                    Deco(x, y, Art.RockAt("Boulder", root, c + new Vector3(0.3f - j * 0.5f, -0.05f, -0.2f), new Vector3(1.5f, 0.7f + k * 0.3f, 1.25f), Look.Boulder, k));
                    if (j > 0.35f) Deco(x, y, Art.RockAt("Stone", root, c + new Vector3(0.6f, -0.03f, 0.6f), new Vector3(0.7f, 0.35f, 0.6f), Look.Boulder * 0.88f, j));
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
                bool show = !NeedsClearing(kv.Key % _w.W, kv.Key / _w.W);
                foreach (var go in kv.Value) if (go.activeSelf != show) go.SetActive(show);
            }
            foreach (var key in _trees.Keys) UpdateTree(key);
            foreach (var fl in _fieldLabels)
            {
                bool show = !BuiltNear(fl.x, fl.y, 6);
                if (fl.go.activeSelf != show) fl.go.SetActive(show);
            }
        }

        void UpdateTree(int key)
        {
            var (tree, stump) = _trees[key];
            int x = key % _w.W, y = key / _w.W;
            bool clear = NeedsClearing(x, y);
            int logs = _w.Logs[key];
            bool showTree = !clear && logs > 0;
            if (tree.gameObject.activeSelf != showTree) tree.gameObject.SetActive(showTree);
            if (showTree) tree.localScale = Vector3.one * ((0.3f + 0.7f * logs / _w.B.LogsPerTile) * (NextToMachine(x, y) ? 0.42f : 1f));
            bool showStump = !clear && logs == 0;
            if (stump.activeSelf != showStump) stump.SetActive(showStump);
        }

        /// <summary>Trees and rocks go on built tiles, and next to belts and splitters so those stay visible.
        /// Not around woodcutters: you should see the forest they're cutting thin out.</summary>
        bool NeedsClearing(int x, int y)
        {
            if (_w.At(x, y) != null || _w.ShaftAt(x, y) != null) return true;
            if (_w.Units[y * _w.W + x] != 0) return true;
            for (int d = 0; d < 4; d++)
            {
                var n = _w.At(x + Dir.DX[d], y + Dir.DY[d]);
                if (n != null && (n.Type == BuildingType.Belt || n.Type == BuildingType.Splitter)) return true;
            }
            return false;
        }

        /// <summary>Trees right next to a machine grow lower, so the machine isn't hidden in the wood.</summary>
        bool NextToMachine(int x, int y)
        {
            for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    var n = _w.At(x + dx, y + dy);
                    if (n != null && n.Type != BuildingType.Belt && n.Type != BuildingType.Splitter) return true;
                }
            return false;
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
            sun.color = new Color(1f, 0.92f, 0.78f);
            sun.intensity = 1.25f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.8f;
            sun.shadowBias = 0.04f;
            sun.shadowNormalBias = 0.3f;
            sun.transform.rotation = Quaternion.Euler(42f, -40f, 0f);
            RenderSettings.sun = sun;

            QualitySettings.shadows = ShadowQuality.All;
            QualitySettings.shadowResolution = ShadowResolution.VeryHigh;
            QualitySettings.shadowDistance = 180f;
            QualitySettings.shadowCascades = 4;
            QualitySettings.shadowProjection = ShadowProjection.StableFit;
            QualitySettings.antiAliasing = 4;
            QualitySettings.anisotropicFiltering = AnisotropicFiltering.ForceEnable;
            QualitySettings.pixelLightCount = 8;

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.58f, 0.68f, 0.82f);
            RenderSettings.ambientEquatorColor = new Color(0.5f, 0.52f, 0.46f);
            RenderSettings.ambientGroundColor = new Color(0.25f, 0.22f, 0.17f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = CameraRig.Horizon;
            RenderSettings.fogDensity = 0.0032f;
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
            var v = new BView { Type = b.Type };
            v.Root = new GameObject(b.Type + " #" + b.Id);
            v.Root.transform.SetParent(transform, false);
            v.Root.transform.localPosition = FootCenter(b.X, b.Y, b.Size);
            v.Body = new GameObject("Body").transform;
            v.Body.SetParent(v.Root.transform, false);
            float s = b.Size * T;
            float h = s / 2f;
            var B = v.Body;
            v.Seed = Art.Hash(b.X, b.Y, 3) * 10f;
            Color planks = Look.Planks, timber = Look.Timber, dark = Look.MineDark, stone = Look.StoneWall;
            Color logEnd = new Color(0.74f, 0.6f, 0.42f);
            switch (b.Type)
            {
                case BuildingType.Belt:
                    Art.Cube("Rail L", B, new Vector3(-0.72f, 0.3f, 0f), new Vector3(0.14f, 0.24f, T), Look.BeltFrame);
                    Art.Cube("Rail R", B, new Vector3(0.72f, 0.3f, 0f), new Vector3(0.14f, 0.24f, T), Look.BeltFrame);
                    Art.Cube("Cross back", B, new Vector3(0f, 0.12f, -0.6f), new Vector3(1.4f, 0.16f, 0.18f), dark);
                    Art.Cube("Cross front", B, new Vector3(0f, 0.12f, 0.6f), new Vector3(1.4f, 0.16f, 0.18f), dark);
                    BeltDeck(B, new Vector3(0f, 0.38f, 0f), new Vector3(1.32f, 1f, T));
                    // Arrow heads on the rails: direction reads even when the belt is still (and in screenshots).
                    Art.Arrow("Arrow L", B, new Vector3(-0.72f, 0.43f, 0.3f), 0.3f, 0.45f, Look.Accent);
                    Art.Arrow("Arrow R", B, new Vector3(0.72f, 0.43f, 0.3f), 0.3f, 0.45f, Look.Accent);
                    break;
                case BuildingType.Splitter:
                    Art.Cube("Frame", B, new Vector3(0f, 0.18f, 0f), new Vector3(1.85f, 0.36f, 1.85f), Look.BeltFrame);
                    BeltDeck(B, new Vector3(0f, 0.37f, 0f), new Vector3(1.45f, 1f, 1.45f));
                    Art.Arrow("Out front", B, new Vector3(0f, 0.4f, 0.55f), 0.4f, 0.38f, Look.Accent);
                    Art.Arrow("Out right", B, new Vector3(0.55f, 0.4f, 0f), 0.4f, 0.38f, Look.Accent, 90f);
                    Art.Arrow("Out left", B, new Vector3(-0.55f, 0.4f, 0f), 0.4f, 0.38f, Look.Accent, -90f);
                    for (int i = 0; i < 4; i++)
                        Art.Cyl("Post", B, new Vector3(i % 2 == 0 ? -0.82f : 0.82f, 0f, i < 2 ? -0.82f : 0.82f), 0.16f, 0.62f, Look.Iron);
                    break;
                case BuildingType.Woodcutter:
                    Art.Cube("Yard", B, new Vector3(0f, 0.05f, 0f), new Vector3(s - 0.3f, 0.1f, s - 0.3f), planks * 0.85f);
                    Art.Cube("Shed", B, new Vector3(-0.7f, 0.95f, -0.7f), new Vector3(2.0f, 1.8f, 1.8f), timber);
                    Art.Cube("Shed door", B, new Vector3(-0.7f, 0.75f, 0.21f), new Vector3(0.7f, 1.2f, 0.04f), Look.Roof);
                    Art.RoofAt("Roof", B, new Vector3(-0.7f, 1.85f, -0.7f), 2.5f, 2.3f, 0.95f, Look.Roof, 90f);
                    // Log pile: 3 on the bottom, 2, then 1.
                    for (int i = 0; i < 6; i++)
                    {
                        int row = i < 3 ? 0 : i < 5 ? 1 : 2;
                        int col = i < 3 ? i : i < 5 ? i - 3 : 0;
                        float lz = (col - (row == 0 ? 1f : row == 1 ? 0.5f : 0f)) * 0.36f;
                        Art.Log("Log", B, new Vector3(0.95f, 0.28f + row * 0.31f, lz - 0.2f), 1.5f, 0.34f, i % 2 == 0 ? Look.Bark : Look.Bark * 1.1f, 90f);
                    }
                    Art.Cyl("Chopping block", B, new Vector3(-0.3f, 0.1f, 1.1f), 0.55f, 0.45f, Look.Bark);
                    Art.Cyl("Block top", B, new Vector3(-0.3f, 0.55f, 1.1f), 0.5f, 0.02f, logEnd);
                    Art.Cube("Axe handle", B, new Vector3(-0.3f, 0.8f, 1.1f), new Vector3(0.06f, 0.55f, 0.06f), Look.Bark * 1.3f, new Vector3(0f, 0f, 18f));
                    Art.Cube("Axe head", B, new Vector3(-0.22f, 0.6f, 1.1f), new Vector3(0.2f, 0.1f, 0.05f), Look.Iron, new Vector3(0f, 0f, 18f));
                    Front(B, h);
                    break;
                case BuildingType.Mine:
                    Art.Cube("Pad", B, new Vector3(0f, 0.04f, 0f), new Vector3(s - 0.3f, 0.08f, s - 0.3f), new Color(0.36f, 0.32f, 0.28f));
                    Art.Cube("Shaft", B, new Vector3(0f, 0.06f, 0.4f), new Vector3(1.3f, 0.06f, 1.3f), dark * 0.5f);
                    for (int side = -1; side <= 1; side += 2)
                    {
                        Art.Cube("Post", B, new Vector3(side * 0.8f, 2f, 0.4f), new Vector3(0.22f, 4f, 0.22f), timber);
                        Art.Cube("Brace", B, new Vector3(side * 0.8f, 1.95f, -0.35f), new Vector3(0.2f, 4.2f, 0.2f), timber * 0.9f, new Vector3(21f, 0f, 0f));
                    }
                    Art.Cube("Crossbeam", B, new Vector3(0f, 4.0f, 0.4f), new Vector3(2.0f, 0.28f, 0.4f), timber);
                    {
                        var pivot = new GameObject("Wheel pivot").transform;
                        pivot.SetParent(B, false);
                        pivot.localPosition = new Vector3(0f, 4.55f, 0.4f);
                        pivot.localRotation = Quaternion.Euler(0f, 0f, 90f);
                        v.Spin = Art.Gear("Wheel", pivot, Vector3.zero, 1.3f, 0.12f, Look.Iron, 6);
                        v.SpinSpeed = 120f;
                    }
                    Art.Cube("Cable", B, new Vector3(0f, 2.35f, 1.0f), new Vector3(0.05f, 4.2f, 0.05f), dark);
                    Art.Log("Boiler", B, new Vector3(1.15f, 0.65f, -1.0f), 1.6f, 0.95f, Look.Iron, 90f);
                    Art.Cyl("Stack", B, new Vector3(1.15f, 1.0f, -1.55f), 0.3f, 2.4f, dark);
                    v.Smoke = Art.Smoke(B, new Vector3(1.15f, 3.5f, -1.55f), 0.8f);
                    Art.Cube("Firebox", B, new Vector3(1.15f, 0.45f, -0.15f), new Vector3(0.6f, 0.5f, 0.14f), dark);
                    v.Fire = Art.Cube("Fire", B, new Vector3(1.15f, 0.42f, -0.07f), new Vector3(0.42f, 0.3f, 0.04f), Look.Fire);
                    Art.Cube("Cart", B, new Vector3(-1.05f, 0.4f, 1.15f), new Vector3(0.8f, 0.45f, 0.6f), Look.Iron * 0.9f);
                    Art.RockAt("Cart load", B, new Vector3(-1.05f, 0.55f, 1.15f), new Vector3(0.65f, 0.3f, 0.5f), Look.OreRock, 0.3f);
                    Front(B, h);
                    break;
                case BuildingType.Kiln:
                    Art.Cube("Pad", B, new Vector3(0f, 0.06f, 0f), new Vector3(s - 0.4f, 0.12f, s - 0.4f), stone * 0.9f);
                    Art.Cyl("Drum", B, new Vector3(0f, 0.12f, 0f), 3.0f, 1.0f, Look.Brickwork);
                    Art.Ball("Dome", B, new Vector3(0f, 1.1f, 0f), new Vector3(3.0f, 2.7f, 3.0f), Look.Brickwork * 1.04f);
                    Art.Cyl("Band", B, new Vector3(0f, 1.0f, 0f), 3.08f, 0.14f, Look.Iron);
                    Art.Cyl("Vent", B, new Vector3(0f, 2.2f, -0.1f), 0.6f, 0.75f, Look.Brickwork * 0.8f);
                    v.Smoke = Art.Smoke(B, new Vector3(0f, 3.0f, -0.1f), 1.1f);
                    Art.Cube("Mouth", B, new Vector3(0f, 0.55f, 1.42f), new Vector3(0.95f, 0.85f, 0.3f), dark * 0.6f);
                    v.Fire = Art.Cube("Fire", B, new Vector3(0f, 0.48f, 1.58f), new Vector3(0.7f, 0.55f, 0.04f), Look.Fire);
                    Art.Log("Firewood", B, new Vector3(-1.45f, 0.3f, 1.45f), 0.9f, 0.26f, Look.Bark, 20f);
                    Art.Log("Firewood", B, new Vector3(-1.45f, 0.3f, 1.2f), 0.9f, 0.26f, Look.Bark * 1.1f, 10f);
                    Front(B, h);
                    break;
                case BuildingType.Smelter:
                    Art.Cube("Base", B, new Vector3(0f, 0.3f, 0f), new Vector3(s - 0.4f, 0.6f, s - 0.4f), stone * 0.9f);
                    Art.Part("Furnace", B, Art.Frustum, new Vector3(0f, 0.6f, -0.2f), new Vector3(2.4f, 3.2f, 2.4f), stone);
                    Art.Cyl("Band low", B, new Vector3(0f, 1.3f, -0.2f), 2.22f, 0.12f, Look.Iron);
                    Art.Cyl("Band high", B, new Vector3(0f, 2.6f, -0.2f), 1.86f, 0.12f, Look.Iron);
                    Art.Cyl("Stack", B, new Vector3(0f, 3.8f, -0.2f), 0.7f, 1.4f, dark);
                    v.Smoke = Art.Smoke(B, new Vector3(0f, 5.3f, -0.2f), 1.2f);
                    Art.Cube("Tap hole", B, new Vector3(0f, 1.15f, 0.82f), new Vector3(0.85f, 0.75f, 0.5f), dark);
                    v.Fire = Art.Cube("Glow", B, new Vector3(0f, 1.1f, 1.08f), new Vector3(0.6f, 0.5f, 0.04f), Look.Fire);
                    Art.Cube("Bellows", B, new Vector3(-1.45f, 0.95f, -0.2f), new Vector3(0.5f, 0.35f, 0.9f), Look.Bark, new Vector3(0f, 0f, -12f));
                    Art.RockAt("Ore heap", B, new Vector3(1.35f, 0.6f, 1.1f), new Vector3(0.8f, 0.45f, 0.7f), Look.OreRock, 0.6f);
                    Front(B, h);
                    break;
                case BuildingType.Workshop:
                    Art.Cube("Floor", B, new Vector3(0f, 0.07f, 0f), new Vector3(s - 0.3f, 0.14f, s - 0.3f), stone * 0.85f);
                    Art.Cube("Walls", B, new Vector3(0f, 1.14f, -0.1f), new Vector3(s - 0.7f, 2.0f, s - 1.1f), timber);
                    Art.Cube("Sill", B, new Vector3(0f, 0.3f, -0.1f), new Vector3(s - 0.6f, 0.3f, s - 1.0f), stone * 0.8f);
                    Art.RoofAt("Roof", B, new Vector3(0f, 2.14f, -0.1f), s - 0.2f, s - 0.6f, 1.1f, Look.Roof);
                    Art.Cube("Chimney", B, new Vector3(-1.05f, 2.4f, -0.9f), new Vector3(0.5f, 2.3f, 0.5f), stone);
                    v.Smoke = Art.Smoke(B, new Vector3(-1.05f, 3.6f, -0.9f), 0.9f);
                    Art.Cube("Door", B, new Vector3(0.55f, 0.95f, 1.36f), new Vector3(0.8f, 1.4f, 0.05f), Look.Roof);
                    Art.Cube("Forge", B, new Vector3(-0.75f, 0.75f, 1.36f), new Vector3(0.9f, 0.7f, 0.08f), dark);
                    v.Fire = Art.Cube("Forge glow", B, new Vector3(-0.75f, 0.72f, 1.4f), new Vector3(0.65f, 0.45f, 0.04f), Look.Fire);
                    {
                        var pivot = new GameObject("Gear pivot").transform;
                        pivot.SetParent(B, false);
                        pivot.localPosition = new Vector3(1.86f, 1.3f, -0.2f);
                        pivot.localRotation = Quaternion.Euler(0f, 0f, 90f);
                        v.Spin = Art.Gear("Gear", pivot, Vector3.zero, 1.6f, 0.16f, new Color(0.78f, 0.62f, 0.25f), 10);
                        v.SpinSpeed = 45f;
                    }
                    Front(B, h);
                    break;
                case BuildingType.Fisher:
                    Art.Cube("Deck", B, new Vector3(0f, 0.08f, 0f), new Vector3(s - 0.3f, 0.16f, s - 0.3f), planks);
                    Art.Cube("Hut", B, new Vector3(0f, 1.0f, -0.6f), new Vector3(2.2f, 1.7f, 1.8f), timber);
                    Art.RoofAt("Roof", B, new Vector3(0f, 1.85f, -0.6f), 2.6f, 2.2f, 0.9f, Look.Roof);
                    Art.Cube("Drying rack", B, new Vector3(-1.2f, 0.9f, 1f), new Vector3(0.12f, 1.6f, 1.2f), Look.Bark);
                    Art.Cyl("Basket", B, new Vector3(0.9f, 0.16f, 1f), 0.8f, 0.5f, planks * 0.9f);
                    Front(B, h);
                    break;
                case BuildingType.Cabin:
                    Art.Cube("Log walls", B, new Vector3(0f, 1.05f, 0f), new Vector3(s - 1f, 2.0f, s - 1.4f), timber);
                    Art.RoofAt("Roof", B, new Vector3(0f, 2.05f, 0f), s - 0.4f, s - 0.8f, 1.2f, Look.Roof, 90f);
                    Art.Cube("Chimney", B, new Vector3(1f, 2.6f, -0.5f), new Vector3(0.45f, 1.4f, 0.45f), stone);
                    v.Smoke = Art.Smoke(B, new Vector3(1f, 3.4f, -0.5f), 0.5f);
                    Art.Cube("Door", B, new Vector3(0f, 0.65f, (s - 1.4f) / 2f + 0.02f), new Vector3(0.7f, 1.2f, 0.05f), Look.Roof);
                    break;
                case BuildingType.Garage: MakeGarage(B, s, v); break;
                case BuildingType.Hopper: MakeHopper(B, s, v); break;
                case BuildingType.Shaft: MakeShaft(v); break;
                case BuildingType.Waterwheel: MakeWaterwheel(b, v, s); break;
                case BuildingType.Sawmill: MakeSawmill(B, s, v); break;
                case BuildingType.TripHammer: MakeTripHammer(B, s, v); break;
                case BuildingType.SteamEngine: MakeSteamEngine(B, s, v); break;
                case BuildingType.Furnace: MakeFurnace(B, s, v); break;
                case BuildingType.Lodge:
                {
                    float fz = (s - 2.2f) / 2f;   // front wall
                    Art.Cube("Stone base", B, new Vector3(0f, 0.35f, 0f), new Vector3(s - 0.6f, 0.7f, s - 0.6f), stone);
                    Art.Cube("Log walls", B, new Vector3(0f, 1.9f, 0f), new Vector3(s - 1.8f, 2.4f, s - 2.2f), timber);
                    for (int i = 0; i < 4; i++)
                        Art.Cube("Log line", B, new Vector3(0f, 1.05f + i * 0.55f, 0f), new Vector3(s - 1.74f, 0.07f, s - 2.14f), timber * 0.72f);
                    Art.RoofAt("Roof", B, new Vector3(0f, 3.1f, 0f), s - 1.2f, s - 0.6f, 2.4f, Look.Roof, 90f);
                    Art.Cube("Chimney", B, new Vector3(2.1f, 3.7f, -1.3f), new Vector3(0.9f, 3.6f, 0.9f), stone * 0.95f);
                    v.Smoke = Art.Smoke(B, new Vector3(2.1f, 5.6f, -1.3f), 1f);
                    Art.Cube("Door", B, new Vector3(0f, 1.5f, fz + 0.03f), new Vector3(1.1f, 1.7f, 0.06f), Look.Roof);
                    Art.Cube("Porch", B, new Vector3(0f, 0.78f, fz + 0.55f), new Vector3(3.2f, 0.14f, 1.0f), planks);
                    Art.Cube("Porch post L", B, new Vector3(-1.4f, 1.85f, fz + 0.95f), new Vector3(0.14f, 2.1f, 0.14f), timber * 0.9f);
                    Art.Cube("Porch post R", B, new Vector3(1.4f, 1.85f, fz + 0.95f), new Vector3(0.14f, 2.1f, 0.14f), timber * 0.9f);
                    Art.Cube("Porch roof", B, new Vector3(0f, 2.95f, fz + 0.6f), new Vector3(3.4f, 0.12f, 1.3f), Look.Roof, new Vector3(-14f, 0f, 0f));
                    for (int i = 0; i < 2; i++)
                        Art.Cube("Window", B, new Vector3(i == 0 ? -2.0f : 2.0f, 2.1f, fz + 0.03f), new Vector3(0.8f, 0.7f, 0.05f), new Color(0.95f, 0.82f, 0.5f));
                    Art.Cyl("Flag pole", B, new Vector3(-3.4f, 0.7f, -3.4f), 0.14f, 6.5f, Look.Bark);
                    Art.Cube("Flag", B, new Vector3(-2.85f, 6.75f, -3.4f), new Vector3(1.0f, 0.65f, 0.04f), Look.Accent);
                    Art.Cyl("Barrel", B, new Vector3(3.2f, 0.7f, 3.2f), 0.6f, 0.8f, timber * 0.8f);
                    Art.Cyl("Barrel", B, new Vector3(2.55f, 0.7f, 3.3f), 0.6f, 0.8f, timber * 0.75f);
                    for (int i = 0; i < 3; i++)
                        Art.Log("Firewood", B, new Vector3(-3.15f, 0.85f + i * 0.22f, 0.5f - i * 0.1f), 1.6f - i * 0.3f, 0.22f, Look.Bark * (1f + i * 0.08f), 90f);
                    break;
                }
            }
            if (v.Fire != null)
            {
                v.Fire.GetComponent<Renderer>().sharedMaterial = Look.GlowMat(Look.Fire);
                v.Fire.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                v.FireLight = new GameObject("Fire light").AddComponent<Light>();
                v.FireLight.transform.SetParent(B, false);
                v.FireLight.transform.localPosition = v.Fire.transform.localPosition + new Vector3(0f, 0.2f, 0.9f);
                v.FireLight.type = LightType.Point;
                v.FireLight.color = Look.FireLight;
                v.FireLight.intensity = 1.6f;
                v.FireLight.range = 5f;
                v.FireLight.enabled = false;
                v.Fire.SetActive(false);
            }
            Texturize(v.Root.transform);
            // Machines only smoke while they burn; the lodge (and cabins) always have a hearth going.
            if (v.Smoke != null && b.Type != BuildingType.Lodge && b.Type != BuildingType.Cabin) SetSmoke(v.Smoke, false);
            // Belts, splitters and cabins have no floating label: their shapes say what they are (click for details).
            if (b.Type != BuildingType.Belt && b.Type != BuildingType.Splitter && b.Type != BuildingType.Cabin && b.Type != BuildingType.Shaft)
            {
                // One upright label per machine: name on top, status (and fuel) underneath.
                // Neighbouring machines alternate label heights so their names don't stack on each other.
                // Three label heights in a pattern, so any two neighbouring 2x2 machines get different heights.
                float top = (b.Type == BuildingType.Mine || b.Type == BuildingType.Smelter ? 6.0f : 4.4f) + ((b.X / 2 + 2 * (b.Y / 2)) % 3) * 1.8f;
                if (b.Type == BuildingType.Lodge)
                {
                    // Lodge name sits in front (south side), clear of the machines behind it.
                    v.Title = Look.Billboard("", v.Root.transform, new Vector3(0f, 0.5f, -s / 2f - 0.6f), 2.0f, Color.white);
                    v.Title.anchor = TextAnchor.UpperCenter;
                }
                else
                {
                    v.Title = Look.Billboard("", v.Root.transform, new Vector3(0f, top, 0f), 1.3f, Color.white);
                    v.BaseTop = top;
                    // Thin post from the roof up to the label, so a raised label still points at its own building.
                    v.Post = Art.Part("Label post", v.Root.transform, Art.Prim(PrimitiveType.Cube), Vector3.zero, Vector3.one, new Color(0.3f, 0.28f, 0.25f), default, false).transform;
                    SetLabelHeight(v, top);
                }
            }
            return v;
        }

        static readonly (Color c, Look.Surface s)[] SurfaceRefs =
        {
            (Look.Timber, Look.Surface.Wood), (Look.Planks, Look.Surface.Wood), (Look.Roof, Look.Surface.Shingle),
            (Look.Brickwork, Look.Surface.Brick), (Look.StoneWall, Look.Surface.Stone),
        };

        /// <summary>Gives wooden, brick, stone and roof parts their world-space pattern, matched by colour family
        /// (a darker or lighter shade of Timber is still timber).</summary>
        static void Texturize(Transform root)
        {
            foreach (var r in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (r.GetComponent<TextMesh>() != null) continue;
                var m = r.sharedMaterial;
                if (m == null || m.IsKeywordEnabled("_EMISSION")) continue;
                var c = m.color;
                float lum = c.r * 0.3f + c.g * 0.59f + c.b * 0.11f;
                foreach (var (rc, surf) in SurfaceRefs)
                {
                    float rl = rc.r * 0.3f + rc.g * 0.59f + rc.b * 0.11f;
                    float k = lum / Mathf.Max(rl, 1e-4f);
                    if (k < 0.65f || k > 1.4f) continue;
                    float dr = c.r / k - rc.r, dg = c.g / k - rc.g, db = c.b / k - rc.b;
                    if (dr * dr + dg * dg + db * db > 0.03f * 0.03f) continue;
                    r.sharedMaterial = Look.SurfMat(c, surf);
                    break;
                }
            }
        }

        void BeltDeck(Transform parent, Vector3 pos, Vector3 scale)
        {
            var deck = Art.Part("Deck", parent, Art.BeltQuad, pos, scale, Look.Belt, default, false);
            deck.GetComponent<Renderer>().sharedMaterial = _beltMat;
        }

        /// <summary>Yellow step on the output side (the same cue as before), so you can see where things come out.</summary>
        static void Front(Transform parent, float half) =>
            Art.Cube("Front", parent, new Vector3(0f, 0.16f, half - 0.14f), new Vector3(1.3f, 0.32f, 0.28f), Look.Accent);

        static void SetSmoke(ParticleSystem ps, bool on)
        {
            var em = ps.emission;
            if (em.enabled != on) em.enabled = on;
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
                case BuildingType.Fisher: return "FISHER F";
                case BuildingType.Cabin: return "CABIN +" + _w.B.Defs[b.Type].Homes;
                case BuildingType.Lodge: return "LODGE\n<size=40>deliver here</size>";
                case BuildingType.Garage: return "GARAGE";
                case BuildingType.Hopper: return "HOPPER";
                case BuildingType.Waterwheel: return "WATERWHEEL " + _w.B.Defs[b.Type].Hp + " hp";
                case BuildingType.Sawmill: return "SAWMILL P";
                case BuildingType.TripHammer: return "TRIP HAMMER L";
                case BuildingType.SteamEngine: return "STEAM ENGINE " + _w.B.Defs[b.Type].Hp + " hp";
                case BuildingType.Furnace: return "STEEL FURNACE T";
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
                    string status = b.Type == BuildingType.Lodge || b.Type == BuildingType.Cabin || string.IsNullOrEmpty(b.Status) || b.Status == "Working" || b.Status.StartsWith("Full") || b.Status.StartsWith("Empty") || b.Status.StartsWith("Turning") || b.Status == "Running" ? "" : b.Status;
                    string text = TitleFor(b) + (status.Length > 0 ? "\n<size=56><color=#FFB070>! " + status + "</color></size>" : "");
                    if (v.Shown != text) { Look.SetText(v.Title, text); v.Shown = text; v.Recipe = b.Recipe; }
                }
            }

            // Fire glow, flicker and smoke: on while burning fuel, or while a charcoal kiln smoulders.
            // Wheels and gears turn while the machine works.
            float now = Time.time;
            foreach (var b in _w.Buildings.Values)
            {
                var v = _views[b.Id];
                bool working = b.Status == "Working";
                if (v.Fire != null)
                {
                    bool lit = (working && (_w.BurnsFuel(b) || b.Type == BuildingType.Kiln || b.Type == BuildingType.Smelter || b.Type == BuildingType.Furnace))
                        || (b.Type == BuildingType.SteamEngine && b.Status == "Running");
                    if (v.Fire.activeSelf != lit) { v.Fire.SetActive(lit); v.FireLight.enabled = lit; }
                    if (lit) v.FireLight.intensity = 1.2f + 0.8f * Mathf.PerlinNoise(now * 5f, v.Seed);
                    if (v.Smoke != null) SetSmoke(v.Smoke, lit);
                }
                if (v.Spin != null && working && Time.timeScale > 0f)
                {
                    v.SpinAngle = (v.SpinAngle + v.SpinSpeed * Time.deltaTime) % 360f;
                    v.Spin.localRotation = Quaternion.Euler(0f, v.SpinAngle, 0f);
                }
            }

            // Belts move with the game clock (they stop when paused); the river drifts south.
            if (_beltMat != null) _beltMat.mainTextureOffset = new Vector2(0f, -((_w.Time * _w.B.BeltSpeed) % 1f));
            if (_waterMat != null) _waterMat.mainTextureOffset = new Vector2(Mathf.Sin(now * 0.3f) * 0.02f, (now * 0.05f) % 1f);

            bool groundChanged = _w.ChangedGround.Count > 0;
            if (groundChanged)
            {
                _ground.Refresh(_w.ChangedGround);
                _w.ChangedGround.Clear();
                UpdateMarks();
            }
            _ground.Upload();
            if (_w.Buildings.Count != _decoBuildingCount || gone != null || groundChanged) { UpdateDeco(); _grass.Rebuild(); }
            _grass.Draw();
            UpdateVehicles();
            UpdatePowerViews(_w.Buildings.Count != _shaftArmsCount || gone != null);
            UpdateSteamViews();
            foreach (var b in _w.Buildings.Values)
                if (b.Type == BuildingType.Hopper)
                {
                    var hv = _views[b.Id];
                    bool full = _w.HopperStored(b) > 0;
                    if (hv.Extra != null && hv.Extra.activeSelf != full) hv.Extra.SetActive(full);
                }
            if (_w.ChangedLogs.Count > 0)
            {
                foreach (int key in _w.ChangedLogs) if (_trees.ContainsKey(key)) UpdateTree(key);
                _w.ChangedLogs.Clear();
            }

            UpdateItems();

            var cam = Camera.main;
            if (cam != null)
            {
                Look.TurnFlatLabels(cam.transform.eulerAngles.y);
                Look.TurnBillboards(cam.transform.rotation, cam.transform.position);
                DeclutterLabels(cam);
                ScaleItems(cam);
            }
        }

        static void SetLabelHeight(BView v, float y)
        {
            var p = v.Title.transform.localPosition;
            v.Title.transform.localPosition = new Vector3(p.x, y, p.z);
            if (v.Post != null)
            {
                const float from = 2.4f;
                v.Post.localPosition = new Vector3(0f, (y + from) / 2f, 0f);
                v.Post.localScale = new Vector3(0.04f, Mathf.Max(0.01f, y - from), 0.04f);
            }
        }

        readonly List<(BView v, Rect r, float pxPerUp, int prio)> _lab = new List<(BView, Rect, float, int)>();
        readonly List<Rect> _placed = new List<Rect>();
        static readonly System.Text.RegularExpressions.Regex RichTags = new System.Text.RegularExpressions.Regex("<[^>]+>");

        /// <summary>Raises labels that would cover each other on screen, so every machine name stays readable.
        /// Problems and the lodge keep their place first; nearer labels win over far ones.</summary>
        readonly List<(BView v, Rect r)> _fixedLabels = new List<(BView v, Rect r)>();

        void DeclutterLabels(Camera cam)
        {
            _lab.Clear();
            _placed.Clear();
            _fixedLabels.Clear();
            float hudTop = Screen.height * (1f - 0.075f);   // the resource bar across the top of the screen
            float hudBottom = Screen.height * 0.135f;        // build bar and help line along the bottom
            float pxPerMAt(float z) => Screen.height / (2f * z * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad));
            foreach (var v in _views.Values)
            {
                if (v.Title == null) continue;
                if (v.Post != null) SetLabelOffset(v, Vector3.zero);
                ShowLabel(v, true);
                if (string.IsNullOrEmpty(v.Shown)) continue;
                var pos = v.Title.transform.position;
                var a = cam.WorldToScreenPoint(pos);
                // Building off screen (or behind the camera): no label, even for a warning, so nothing gets pinned
                // at the screen edge on top of nearer labels or the HUD.
                if (a.z <= 0f || a.x < 0f || a.x > Screen.width || a.y < hudBottom || a.y > hudTop) { ShowLabel(v, false); continue; }
                // Size from the text itself (renderer bounds lag a frame behind moves): pixels per metre
                // facing the camera, times line height and character count. Capitals run wide, so be generous.
                float pxPerM = pxPerMAt(a.z);
                float k = v.Title.transform.lossyScale.y;
                float lineM = v.Title.characterSize * v.Title.fontSize * 0.1f * k;
                var lines = RichTags.Replace(v.Shown, "").Split('\n');
                float w = 0f, h = 0f;
                for (int li = 0; li < lines.Length; li++)
                {
                    float sz = li == 0 ? 1f : 0.875f;
                    w = Mathf.Max(w, lines[li].Length * 0.7f * lineM * sz * pxPerM);
                    h += 1.2f * lineM * sz * pxPerM;
                }
                w += 12f; h += 6f;
                var r = v.Title.anchor == TextAnchor.UpperCenter ? new Rect(a.x - w / 2f, a.y - h, w, h) : new Rect(a.x - w / 2f, a.y, w, h);
                // The text mesh's own size (local bounds don't depend on where the label is this frame). The label
                // faces the camera, so local x/y are screen right/up. Use whichever box is bigger.
                var tr = v.Title.GetComponent<MeshRenderer>();
                if (tr != null)
                {
                    var lb = tr.localBounds;
                    if (lb.size.x > 0f)
                    {
                        float s2 = k * pxPerM;
                        var rb = Rect.MinMaxRect(a.x + lb.min.x * s2 - 6f, a.y + lb.min.y * s2 - 3f, a.x + lb.max.x * s2 + 6f, a.y + lb.max.y * s2 + 3f);
                        r = Rect.MinMaxRect(Mathf.Min(r.xMin, rb.xMin), Mathf.Min(r.yMin, rb.yMin), Mathf.Max(r.xMax, rb.xMax), Mathf.Max(r.yMax, rb.yMax));
                    }
                }
                // The lodge label doesn't move; it just keeps others off it.
                if (v.Post == null)
                {
                    if (r.yMin < hudBottom || r.yMax > hudTop || r.xMin < 0f || r.xMax > Screen.width) ShowLabel(v, false);
                    else { _placed.Add(r); _fixedLabels.Add((v, r)); }
                    continue;
                }
                int prio = v.Shown.Contains("! ") ? 0 : 1;
                _lab.Add((v, r, pxPerM, prio));
            }
            _lab.Sort((p, q) => p.prio != q.prio ? p.prio.CompareTo(q.prio) : p.r.yMin.CompareTo(q.r.yMin));
            foreach (var (v, r0, pxPerM, prio) in _lab)
            {
                var r = r0;
                float shift = 0f, maxShift = r0.height * 3f;
                bool clear = false;
                for (int tries = 0; tries < 12; tries++)
                {
                    bool hit = false;
                    foreach (var p in _placed)
                        if (p.Overlaps(r))
                        {
                            float d = p.yMax - r.yMin + 3f;
                            r.y += d; shift += d; hit = true;
                        }
                    if (!hit) { clear = true; break; }
                }
                // Can't find a clear spot nearby, or it would sit under the top bar: hide it (click the building
                // to see its details). Warnings always stay.
                bool offScreen = r.xMin < 2f || r.xMax > Screen.width - 2f || r.yMax > hudTop || r.yMin < hudBottom;
                if (prio != 0 && (!clear || shift > maxShift || offScreen))
                {
                    ShowLabel(v, false);
                    continue;
                }
                // Warnings stay, nudged back onto the screen and below the top bar.
                float side = 0f;
                if (r.xMin < 2f) side = 2f - r.xMin;
                else if (r.xMax > Screen.width - 2f) side = Screen.width - 2f - r.xMax;
                float down = r.yMax > hudTop ? hudTop - r.yMax : r.yMin < hudBottom ? hudBottom - r.yMin : 0f;
                r.x += side; r.y += down; shift += down;
                if (!clear || Mathf.Abs(down) > 0.01f)
                {
                    // No room above: try below the building instead.
                    var rd = r0; rd.x += side; float sd = 0f;
                    for (int tries = 0; tries < 12; tries++)
                    {
                        bool hit = false;
                        foreach (var p in _placed)
                            if (p.Overlaps(rd)) { float d = rd.yMax - p.yMin + 3f; rd.y -= d; sd -= d; hit = true; }
                        if (!hit) break;
                    }
                    bool fits = rd.yMin >= hudBottom && rd.yMax <= hudTop;
                    bool rdClear = true; foreach (var p in _placed) if (p.Overlaps(rd)) { rdClear = false; break; }
                    if (fits && rdClear) { r = rd; shift = sd; }
                }
                // Still on top of a fixed label (the lodge): the warning matters more, so the lodge name steps aside.
                foreach (var (fv, fr) in _fixedLabels)
                    if (fr.Overlaps(r)) ShowLabel(fv, false);
                // Lift along the camera's up so a top-down view separates labels as well as a low one.
                if (Mathf.Abs(shift) > 0.01f || Mathf.Abs(side) > 0.01f)
                    SetLabelOffset(v, (cam.transform.up * shift + cam.transform.right * side) / pxPerM);
                _placed.Add(r);
            }
        }

        static void ShowLabel(BView v, bool on)
        {
            if (v.Title.gameObject.activeSelf != on) v.Title.gameObject.SetActive(on);
            if (v.Post != null && v.Post.gameObject.activeSelf != on) v.Post.gameObject.SetActive(on);
        }

        /// <summary>Puts the label at its base height plus a world offset, with the post running from the roof to it.</summary>
        static void SetLabelOffset(BView v, Vector3 off)
        {
            var root = v.Root.transform;
            var at = root.TransformPoint(new Vector3(0f, v.BaseTop, 0f)) + off;
            v.Title.transform.position = at;
            var from = root.TransformPoint(new Vector3(0f, 2.4f, 0f));
            var d = at - from;
            float len = Mathf.Max(0.01f, d.magnitude);
            v.Post.position = from + d / 2f;
            v.Post.rotation = Quaternion.FromToRotation(Vector3.up, d / len);
            v.Post.localScale = new Vector3(0.04f, len, 0.04f);
        }

        /// <summary>Items on belts grow a little when the camera is far away so their letters stay readable.</summary>
        void ScaleItems(Camera cam)
        {
            var cp = cam.transform.position;
            foreach (var list in _pools.Values)
                foreach (var t in list)
                {
                    if (!t.gameObject.activeSelf) continue;
                    float k = Mathf.Clamp(Vector3.Distance(cp, t.position) / 30f, 1f, 2.2f);
                    t.localScale = new Vector3(k, k, k);
                }
        }

        // Items on belts: one pool per kind, each a little model of the thing (log, ore, bar, gear...)
        // with its letter on a dark badge on top, so the kind never depends on colour alone.
        readonly Dictionary<Item, List<Transform>> _pools = new Dictionary<Item, List<Transform>>();
        readonly Dictionary<Item, int> _used = new Dictionary<Item, int>();

        void UpdateItems()
        {
            foreach (var k in _pools.Keys) _used[k] = 0;
            foreach (var b in _w.Buildings.Values)
            {
                if ((b.Type != BuildingType.Belt && b.Type != BuildingType.Splitter) || b.BeltItem == Item.None) continue;
                var tr = ItemFromPool(b.BeltItem);
                var c = TileCenter(b.X, b.Y);
                tr.localPosition = c + DirVec(b.Facing) * ((b.BeltProgress * 2f - 1f) * 0.85f) + new Vector3(0f, 0.4f, 0f);
                tr.localRotation = FacingRot(b.Facing);
            }
            foreach (var kv in _pools)
            {
                int used = _used[kv.Key];
                for (int i = used; i < kv.Value.Count; i++)
                    if (kv.Value[i].gameObject.activeSelf) kv.Value[i].gameObject.SetActive(false);
            }
        }

        Transform ItemFromPool(Item kind)
        {
            if (!_pools.TryGetValue(kind, out var list)) { _pools[kind] = list = new List<Transform>(); _used[kind] = 0; }
            int i = _used[kind]++;
            if (i >= list.Count) list.Add(MakeItem(kind));
            var tr = list[i];
            if (!tr.gameObject.activeSelf) tr.gameObject.SetActive(true);
            return tr;
        }

        static Material _itemTextMat;

        /// <summary>Font material that hides behind trees and buildings (the default text draws on top of everything).</summary>
        static Material ItemTextMat
        {
            get
            {
                if (_itemTextMat != null) return _itemTextMat;
                _itemTextMat = new Material(Look.Font.material);
                _itemTextMat.SetInt("unity_GUIZTestMode", (int)UnityEngine.Rendering.CompareFunction.LessEqual);
                _itemTextMat.renderQueue = 2990;   // before machine labels, so labels always draw on top of item letters
                return _itemTextMat;
            }
        }

        Transform MakeItem(Item kind)
        {
            var go = new GameObject("Item " + kind);
            go.transform.SetParent(_items, false);
            var t = go.transform;
            var col = ItemColor(kind);
            float top;
            switch (kind)
            {
                case Item.Wood:
                    Art.Log("Log", t, new Vector3(0f, 0.17f, 0f), 0.85f, 0.34f, col);
                    top = 0.36f;
                    break;
                case Item.Ore:
                    Art.RockAt("Ore", t, Vector3.zero, new Vector3(0.6f, 0.36f, 0.55f), col, 0.2f);
                    Art.RockAt("Ore", t, new Vector3(0.2f, 0f, 0.18f), new Vector3(0.3f, 0.22f, 0.3f), col * 0.8f, 0.7f);
                    top = 0.38f;
                    break;
                case Item.Stone:
                    Art.RockAt("Stone", t, Vector3.zero, new Vector3(0.62f, 0.38f, 0.56f), col, 0.6f);
                    top = 0.4f;
                    break;
                case Item.Charcoal:
                    Art.RockAt("Lump", t, new Vector3(-0.12f, 0f, 0f), new Vector3(0.36f, 0.24f, 0.32f), col, 0.1f);
                    Art.RockAt("Lump", t, new Vector3(0.15f, 0f, 0.08f), new Vector3(0.32f, 0.2f, 0.3f), col * 1.2f, 0.5f);
                    Art.RockAt("Lump", t, new Vector3(0.02f, 0f, -0.16f), new Vector3(0.28f, 0.22f, 0.28f), col * 0.9f, 0.9f);
                    top = 0.26f;
                    break;
                case Item.Iron:
                    Art.Part("Bar", t, Art.Ingot, Vector3.zero, new Vector3(0.36f, 0.2f, 0.7f), col);
                    top = 0.22f;
                    break;
                case Item.Brick:
                    Art.Cube("Brick", t, new Vector3(-0.16f, 0.12f, 0f), new Vector3(0.28f, 0.24f, 0.58f), col);
                    Art.Cube("Brick", t, new Vector3(0.16f, 0.12f, 0f), new Vector3(0.28f, 0.24f, 0.58f), col * 0.9f);
                    top = 0.26f;
                    break;
                case Item.Gear:
                    Art.Gear("Gear", t, new Vector3(0f, 0.07f, 0f), 0.62f, 0.14f, col, 8);
                    top = 0.16f;
                    break;
                case Item.Plank:
                    for (int k = 0; k < 3; k++)
                        Art.Cube("Plank", t, new Vector3(0f, 0.05f + k * 0.09f, 0f), new Vector3(0.42f, 0.08f, 0.85f), col * (k == 1 ? 0.92f : 1f));
                    top = 0.28f;
                    break;
                case Item.Plate:
                    // A stack of three square sheets, a little skewed, with a bright edge so it reads at a distance.
                    for (int k = 0; k < 3; k++)
                        Art.Cube("Plate", t, new Vector3(0.02f * k, 0.05f + k * 0.08f, -0.02f * k), new Vector3(0.85f, 0.07f, 0.85f), col * (1f + 0.15f * k), new Vector3(0f, k * 8f, 0f));
                    top = 0.3f;
                    break;
                case Item.Coal:
                    // Glossy black lumps, bigger and blockier than charcoal.
                    Art.RockAt("Lump", t, new Vector3(-0.1f, 0f, 0.02f), new Vector3(0.44f, 0.3f, 0.4f), col, 0.3f);
                    Art.RockAt("Lump", t, new Vector3(0.16f, 0f, -0.1f), new Vector3(0.36f, 0.26f, 0.34f), col * 1.4f, 0.8f);
                    top = 0.32f;
                    break;
                case Item.Steel:
                    // Two bright bars crossed: reads differently from the single iron bar.
                    Art.Part("Bar", t, Art.Ingot, new Vector3(0f, 0f, 0f), new Vector3(0.3f, 0.16f, 0.8f), col);
                    Art.Part("Bar", t, Art.Ingot, new Vector3(0f, 0.16f, 0f), new Vector3(0.3f, 0.16f, 0.8f), col * 1.1f, new Vector3(0f, 90f, 0f));
                    top = 0.34f;
                    break;
                case Item.Fish:
                    Art.Ball("Body", t, new Vector3(0f, 0.13f, 0f), new Vector3(0.28f, 0.24f, 0.62f), col);
                    Art.Part("Tail", t, Art.Cone, new Vector3(0f, 0.13f, -0.28f), new Vector3(0.3f, 0.22f, 0.08f), col * 0.85f, new Vector3(-90f, 0f, 0f));
                    top = 0.26f;
                    break;
                default:
                    Art.Cube("Crate", t, new Vector3(0f, 0.25f, 0f), new Vector3(0.5f, 0.5f, 0.5f), col);
                    top = 0.5f;
                    break;
            }
            // Letter badge: white letter on a dark disc, same for every item.
            Art.Cyl("Badge", t, new Vector3(0f, top, 0f), 0.46f, 0.04f, new Color(0.1f, 0.1f, 0.1f));
            var label = Look.Label(Items.Letter(kind), t, new Vector3(0f, top + 0.05f, 0f), 0.55f, Color.white);
            label.fontStyle = FontStyle.Bold;
            label.GetComponent<MeshRenderer>().sharedMaterial = ItemTextMat;
            return t;
        }

        // ------------------------------------------------------------------ ghost / selection

        public void HideGhost()
        {
            HideArea();
            _ghost.SetActive(false);
            foreach (var g in _beltGhosts) g.SetActive(false);
        }

        public void ShowGhost(BuildingType type, int x, int y, int facing, string reason)
        {
            HideArea();
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

        public void ShowBeltGhosts(List<(int x, int y, int d)> path, int okCount, string reason, string noun = "belts")
        {
            HideArea();
            _ghost.SetActive(true);
            _ghostBox.localScale = Vector3.zero;
            _ghostArrow.gameObject.SetActive(false);
            var last = path[path.Count - 1];
            _ghost.transform.localPosition = TileCenter(last.x, last.y);
            _ghostText.text = okCount + " of " + path.Count + " " + noun + (reason != null ? "  ·  " + reason : "  ·  release to build");
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
