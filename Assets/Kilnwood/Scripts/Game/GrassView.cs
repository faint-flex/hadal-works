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

        public GrassView(World w)
        {
            _w = w;
            var m = Resources.Load<Material>("KW_Grass");
            if (m == null || m.shader == null || !m.shader.isSupported) return;
            _mat = new Material(m) { enableInstancing = true };
            _mesh = TuftMesh();
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
            if (_w.Units[i] != 0 || _w.DigMark[i] || _w.DumpMark[i]) return false;
            return _w.At(x, y) == null && _w.ShaftAt(x, y) == null;
        }

        public void Rebuild()
        {
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
                    int count = edge ? TuftsPerTile / 2 : TuftsPerTile;
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

        public void Draw()
        {
            if (_mat == null) return;
            for (int i = 0; i < _batches.Count; i++) Graphics.RenderMeshInstanced(_rp, _mesh, 0, _batches[i], _counts[i]);
        }

        /// <summary>Seven thin blades leaning out from a point, two greens, darker at the root.</summary>
        static Mesh TuftMesh()
        {
            var v = new List<Vector3>();
            var c = new List<Color>();
            var n = new List<Vector3>();
            var t = new List<int>();
            var rng = new System.Random(7);
            Color[] tips = { new Color(0.52f, 0.62f, 0.3f), new Color(0.45f, 0.58f, 0.26f), new Color(0.6f, 0.64f, 0.34f) };
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
                var rootCol = new Color(0.2f, 0.27f, 0.12f, 0f);
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
