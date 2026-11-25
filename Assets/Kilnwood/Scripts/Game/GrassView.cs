using System.Collections.Generic;
using Kilnwood.Sim;
using UnityEngine;
using Terrain = Kilnwood.Sim.Terrain;

namespace Kilnwood.Game
{
    /// <summary>
    /// Tufts of grass on open grass tiles (not under buildings, belts, shafts, dug or marked ground), drawn with GPU
    /// instancing. Rebuilt when the layout changes. Material KW_Grass in Resources (made by Setup); none = no grass.
    /// </summary>
    public class GrassView
    {
        const int TuftsPerTile = 9;
        readonly World _w;
        readonly Mesh _mesh;
        readonly Material _mat;
        readonly List<Matrix4x4[]> _batches = new List<Matrix4x4[]>();
        readonly List<int> _counts = new List<int>();
        RenderParams _rp;
        bool _cards;

        // 0.18.1: the card clumps drew black through instanced KW_Grass, so they are now baked into 16x16-tile chunk meshes
        // drawn with the foliage card shader (the one the pine twigs use). Only chunks whose open tiles changed are rebuilt.
        const int Chunk = 16;
        Transform _root;
        Material _cardMat;
        MeshFilter[] _chunkMesh;
        ulong[] _chunkKey;

        public GrassView(World w, Transform parent = null)
        {
            _w = w;
            var cardTex = Resources.Load<Texture2D>("Foliage/grass_cards");
            var cardShader = Shader.Find("Kilnwood/FoliageCard");
            if (parent != null && cardTex != null && cardShader != null && cardShader.isSupported)
            {
                _cardMat = new Material(cardShader) { name = "Grass cards", mainTexture = cardTex };
                _cardMat.SetFloat("_Cutoff", 0.45f);
                _cardMat.SetFloat("_Wind", 0.35f);
                _cardMat.SetFloat("_Translucency", 0.35f);
                _cardMat.color = new Color(1.05f, 1.05f, 0.95f);
                _root = new GameObject("Grass").transform;
                _root.SetParent(parent, false);
                int cw = (w.W + Chunk - 1) / Chunk, ch = (w.H + Chunk - 1) / Chunk;
                _chunkMesh = new MeshFilter[cw * ch];
                _chunkKey = new ulong[cw * ch];
                _cards = true;
                return;
            }
            var m = Resources.Load<Material>("KW_Grass");
            if (m == null || m.shader == null || !m.shader.isSupported) return;
            _mat = new Material(m) { enableInstancing = true };
            // 0.18 photoreal pass 2: scanned grass clumps on crossed cards (Poly Haven, CC0) when they are there.
            var cards = Resources.Load<Texture2D>("Foliage/grass_cards");
            if (cards != null && _mat.HasProperty("_Cutoff"))
            {
                _mat.mainTexture = cards;
                _mat.SetFloat("_Cutoff", 0.45f);
                _mesh = CardTuftMesh();
                _cards = true;
            }
            else _mesh = TuftMesh();
            _rp = new RenderParams(_mat)
            {
                shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off,
                receiveShadows = true,
                worldBounds = new Bounds(new Vector3(w.W * WorldView.T / 2f, 0f, w.H * WorldView.T / 2f), new Vector3(w.W * WorldView.T + 10f, 10f, w.H * WorldView.T + 10f)),
            };
        }

        bool Open(int x, int y)
        {
            if (!_w.InBounds(x, y) || _w.GroundAt(x, y) != Terrain.Grass) return false;
            int i = y * _w.W + x;
            if (_w.Units[i] != 0 || _w.DigMark[i] || _w.DumpMark[i] || _w.LevelMark[i]) return false;
            return _w.At(x, y) == null && _w.ShaftAt(x, y) == null;
        }

        public void Rebuild()
        {
            if (_cardMat != null) { RebuildChunks(); return; }
            if (_mat == null) return;
            _batches.Clear();
            _counts.Clear();
            var cur = new Matrix4x4[1023];
            int n = 0;
            for (int y = 0; y < _w.H; y++)
                for (int x = 0; x < _w.W; x++)
                {
                    if (!Open(x, y)) continue;
                    // Thinner next to things (paths get trodden), fuller in open meadow.
                    bool edge = !Open(x + 1, y) || !Open(x - 1, y) || !Open(x, y + 1) || !Open(x, y - 1);
                    int perTile = _cards ? 6 : TuftsPerTile;   // card clumps are fuller, so fewer of them
                    int count = edge ? perTile / 2 : perTile;
                    for (int k = 0; k < count; k++)
                    {
                        float jx = Art.Hash(x * 7 + k, y, 21), jz = Art.Hash(x, y * 5 + k, 22), r = Art.Hash(x + k, y + k, 23);
                        var p = new Vector3((x + 0.05f + 0.9f * jx) * WorldView.T, 0f, (y + 0.05f + 0.9f * jz) * WorldView.T);
                        float sc = 0.7f + 0.7f * r;
                        // Patchy: a slow noise field makes some meadow taller and some short.
                        float patch = Mathf.PerlinNoise(p.x * 0.06f + 3f, p.z * 0.06f + 9f);
                        if (patch < 0.28f && k % 2 == 1) continue;
                        sc *= 0.6f + 0.8f * patch;
                        cur[n++] = Matrix4x4.TRS(p, Quaternion.Euler(0f, r * 360f, 0f), new Vector3(sc, sc * (0.8f + 0.5f * jx), sc));
                        if (n == cur.Length) { _batches.Add(cur); _counts.Add(n); cur = new Matrix4x4[1023]; n = 0; }
                    }
                }
            if (n > 0) { _batches.Add(cur); _counts.Add(n); }
        }

        void RebuildChunks()
        {
            int cw = (_w.W + Chunk - 1) / Chunk, ch = (_w.H + Chunk - 1) / Chunk;
            var v = new List<Vector3>(); var c = new List<Color>(); var nm = new List<Vector3>(); var uv = new List<Vector2>(); var t = new List<int>();
            for (int cy = 0; cy < ch; cy++)
                for (int cx = 0; cx < cw; cx++)
                {
                    // A cheap fingerprint of which tiles in (and just around) the chunk are open.
                    ulong key = 1469598103934665603UL;
                    for (int y = cy * Chunk - 1; y <= cy * Chunk + Chunk; y++)
                        for (int x = cx * Chunk - 1; x <= cx * Chunk + Chunk; x++)
                            key = (key ^ (Open(x, y) ? 1UL : 2UL)) * 1099511628211UL;
                    int ci = cy * cw + cx;
                    if (_chunkMesh[ci] != null && _chunkKey[ci] == key) continue;
                    _chunkKey[ci] = key;
                    v.Clear(); c.Clear(); nm.Clear(); uv.Clear(); t.Clear();
                    for (int y = cy * Chunk; y < Mathf.Min(_w.H, cy * Chunk + Chunk); y++)
                        for (int x = cx * Chunk; x < Mathf.Min(_w.W, cx * Chunk + Chunk); x++)
                        {
                            if (!Open(x, y)) continue;
                            bool edge = !Open(x + 1, y) || !Open(x - 1, y) || !Open(x, y + 1) || !Open(x, y - 1);
                            int count = edge ? 3 : 6;
                            for (int k = 0; k < count; k++)
                            {
                                float jx = Art.Hash(x * 7 + k, y, 21), jz = Art.Hash(x, y * 5 + k, 22), r = Art.Hash(x + k, y + k, 23);
                                var p = new Vector3((x + 0.05f + 0.9f * jx) * WorldView.T, 0f, (y + 0.05f + 0.9f * jz) * WorldView.T);
                                float sc = 0.7f + 0.7f * r;
                                float patch = Mathf.PerlinNoise(p.x * 0.06f + 3f, p.z * 0.06f + 9f);
                                if (patch < 0.28f && k % 2 == 1) continue;
                                sc *= 0.6f + 0.8f * patch;
                                AddTuft(v, c, nm, uv, t, p, r * 360f, sc, sc * (0.8f + 0.5f * jx), (x * 3 + y + k) & 3);
                            }
                        }
                    var mf = _chunkMesh[ci];
                    if (mf == null)
                    {
                        var go = new GameObject("Grass " + cx + "," + cy);
                        go.transform.SetParent(_root, false);
                        mf = go.AddComponent<MeshFilter>();
                        var mr = go.AddComponent<MeshRenderer>();
                        mr.sharedMaterial = _cardMat;
                        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                        mr.receiveShadows = true;
                        mf.sharedMesh = new Mesh { name = go.name, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
                        _chunkMesh[ci] = mf;
                    }
                    var mesh = mf.sharedMesh;
                    mesh.Clear();
                    mesh.SetVertices(v); mesh.SetColors(c); mesh.SetNormals(nm); mesh.SetUVs(0, uv); mesh.SetTriangles(t, 0);
                    mesh.RecalculateBounds();
                    mf.gameObject.SetActive(v.Count > 0);
                }
        }

        /// <summary>Three crossed cards around p, each one of the four scanned clumps; root shaded, tip light.</summary>
        static void AddTuft(List<Vector3> v, List<Color> c, List<Vector3> nm, List<Vector2> uv, List<int> t, Vector3 p, float yaw, float sw, float sh, int cell0)
        {
            var root = new Color(0.8f, 0.82f, 0.74f, 0.2f); var tip = new Color(1f, 1f, 0.95f, 1f);
            for (int k = 0; k < 3; k++)
            {
                var side = Quaternion.Euler(0f, yaw + k * 60f, 0f) * Vector3.right * (0.42f * sw);
                var up = Vector3.up * (0.42f * sh);
                float u0 = ((cell0 + k) & 3) * 0.25f + 0.004f, u1 = u0 + 0.242f;
                int o = v.Count;
                v.Add(p - side); v.Add(p + side); v.Add(p + side + up); v.Add(p - side + up);
                c.Add(root); c.Add(root); c.Add(tip); c.Add(tip);
                for (int q = 0; q < 4; q++) nm.Add(Vector3.up);
                uv.Add(new Vector2(u0, 0f)); uv.Add(new Vector2(u1, 0f)); uv.Add(new Vector2(u1, 1f)); uv.Add(new Vector2(u0, 1f));
                t.Add(o); t.Add(o + 2); t.Add(o + 1); t.Add(o); t.Add(o + 3); t.Add(o + 2);
            }
        }

        public void Draw()
        {
            if (_mat == null) return;
            for (int i = 0; i < _batches.Count; i++) Graphics.RenderMeshInstanced(_rp, _mesh, 0, _batches[i], _counts[i]);
        }

        /// <summary>0.18: three crossed cards, each showing one of the four scanned grass clumps; root dark, tip light.</summary>
        static Mesh CardTuftMesh()
        {
            var v = new List<Vector3>(); var c = new List<Color>(); var n = new List<Vector3>(); var uv = new List<Vector2>(); var t = new List<int>();
            for (int k = 0; k < 3; k++)
            {
                float a = k * 60f + 17f;
                var side = Quaternion.Euler(0f, a, 0f) * Vector3.right;
                float w = 0.42f, h = 0.42f;
                int cell = (k * 3 + 1) & 3;
                float u0 = cell * 0.25f, u1 = u0 + 0.25f;
                var root = new Color(0.72f, 0.74f, 0.66f, 0f); var tip = new Color(1f, 1f, 0.95f, 1f);
                int o = v.Count;
                v.Add(-side * w); v.Add(side * w); v.Add(side * w + Vector3.up * h); v.Add(-side * w + Vector3.up * h);
                c.Add(root); c.Add(root); c.Add(tip); c.Add(tip);
                for (int q = 0; q < 4; q++) n.Add(Vector3.up);
                uv.Add(new Vector2(u0, 0f)); uv.Add(new Vector2(u1, 0f)); uv.Add(new Vector2(u1, 1f)); uv.Add(new Vector2(u0, 1f));
                t.AddRange(new[] { o, o + 2, o + 1, o, o + 3, o + 2 });
            }
            var m = new Mesh { name = "Grass clump cards" };
            m.SetVertices(v); m.SetColors(c); m.SetNormals(n); m.SetUVs(0, uv); m.SetTriangles(t, 0);
            m.RecalculateBounds();
            return m;
        }

        /// <summary>Seven thin blades leaning out from a point, two greens, darker at the root.</summary>
        static Mesh TuftMesh()
        {
            var v = new List<Vector3>();
            var c = new List<Color>();
            var n = new List<Vector3>();
            var t = new List<int>();
            var rng = new System.Random(7);
            // Vertex colours are not converted to linear space by Unity, so convert here (raw values made the grass pale cream).
            // Tips sit close to the painted ground colour so tufts add texture, not speckle.
            Color[] tips = { new Color(0.38f, 0.49f, 0.24f).linear, new Color(0.33f, 0.45f, 0.21f).linear, new Color(0.42f, 0.5f, 0.26f).linear };
            for (int b = 0; b < 7; b++)
            {
                float a = b * 51.4f + (float)rng.NextDouble() * 20f;
                var dir = Quaternion.Euler(0f, a, 0f) * Vector3.forward;
                var side = Quaternion.Euler(0f, a + 90f, 0f) * Vector3.forward;
                float h = 0.28f + 0.22f * (float)rng.NextDouble();
                float lean = 0.08f + 0.12f * (float)rng.NextDouble();
                float wdt = 0.035f + 0.015f * (float)rng.NextDouble();
                var root = dir * 0.05f;
                var mid = root + dir * lean * 0.4f + Vector3.up * h * 0.55f;
                var tip = root + dir * lean + Vector3.up * h;
                var tipCol = tips[b % tips.Length];
                var rootCol = new Color(0.27f, 0.34f, 0.17f).linear; rootCol.a = 0f;
                var midCol = Color.Lerp(rootCol, tipCol, 0.6f); midCol.a = 0.55f;
                tipCol.a = 1f;
                int i0 = v.Count;
                v.Add(root - side * wdt); v.Add(root + side * wdt);
                v.Add(mid - side * wdt * 0.7f); v.Add(mid + side * wdt * 0.7f);
                v.Add(tip);
                c.Add(rootCol); c.Add(rootCol); c.Add(midCol); c.Add(midCol); c.Add(tipCol);
                for (int k = 0; k < 5; k++) n.Add(Vector3.up);
                t.AddRange(new[] { i0, i0 + 2, i0 + 1, i0 + 1, i0 + 2, i0 + 3, i0 + 2, i0 + 4, i0 + 3 });
            }
            var m = new Mesh { name = "Grass tuft" };
            m.SetVertices(v); m.SetColors(c); m.SetNormals(n); m.SetTriangles(t, 0);
            m.RecalculateBounds();
            return m;
        }
    }
}
