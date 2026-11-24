using System.Collections.Generic;
using UnityEngine;

namespace Kilnwood.Game
{
    /// <summary>
    /// Realistic-ish trees (graphics pass 2). Pines are stacked skirts of drooping branch tips; broadleaf trees are
    /// lumpy clusters of leaf masses. Meshes carry self-shadowing in vertex colour (alpha = how exposed the spot is)
    /// and normals that point out of the crown, drawn with KW_Foliage (instanced). A few shared variants make a forest.
    /// Without the KW_Foliage material the old shapes in Art.Tree are used.
    /// </summary>
    public static class Trees
    {
        const int Variants = 4;
        static Mesh[] _pines, _leafy, _leafyFar;
        static Material _src;
        static bool _checked;
        static readonly Dictionary<Color, Material> _mats = new Dictionary<Color, Material>();

        public static bool Available
        {
            get
            {
                if (!_checked)
                {
                    _checked = true;
                    _src = Resources.Load<Material>("KW_Foliage");
                    if (_src != null && (_src.shader == null || !_src.shader.isSupported)) _src = null;
                }
                return _src != null;
            }
        }

        static Material Mat(Color c)
        {
            if (_mats.TryGetValue(c, out var m)) return m;
            m = new Material(_src) { enableInstancing = true };
            m.SetColor("_Color", c);
            _mats[c] = m;
            return m;
        }

        static void Add(string name, Transform parent, Mesh mesh, Material mat, Vector3 pos, Vector3 scale, bool shadows = true)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            if (!shadows) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        /// <summary>One tree at the parent's origin. r picks pine vs broadleaf and the variant; r2 the size and tint.</summary>
        public static void Tree(Transform parent, float r, float r2, bool far = false)
        {
            float s = 0.85f + r2 * 0.45f;
            int v = Mathf.Min(Variants - 1, (int)(Frac(r * 7.31f) * Variants));
            if (r < (far ? 0.7f : 0.62f))
            {
                _pines ??= MakeAll(Pine);
                var g = Art.PineGreens[(int)(r2 * 2.99f)];
                if (!far) Art.Part("Trunk", parent, Art.Frustum, Vector3.zero, new Vector3(0.34f * s, 1.6f * s, 0.34f * s), Look.Bark);
                Add("Pine", parent, _pines[v], Mat(g * 1.25f), Vector3.zero, Vector3.one * s, !far);
            }
            else
            {
                _leafy ??= MakeAll(i => Broadleaf(i, 2));
                _leafyFar ??= MakeAll(i => Broadleaf(i, 1));     // far hills: same shapes, fewer triangles
                var g = Art.LeafGreens[(int)(r2 * 3.99f)];
                if (!far) Art.Part("Trunk", parent, Art.Frustum, Vector3.zero, new Vector3(0.38f * s, 2.6f * s, 0.38f * s), Look.Bark);
                Add("Crown", parent, far ? _leafyFar[v] : _leafy[v], Mat(g * 1.2f), Vector3.zero, Vector3.one * s, !far);
            }
        }

        static float Frac(float x) => x - Mathf.Floor(x);

        static Mesh[] MakeAll(System.Func<int, Mesh> make)
        {
            var a = new Mesh[Variants];
            for (int i = 0; i < Variants; i++) a[i] = make(i);
            return a;
        }

        static float N(int seed, int i) => Art.Hash(i, seed, 41);

        // ------------------------------------------------------------------ pine

        /// <summary>Pine about 5 m tall: 6 tiers of branch skirts, each a star of drooping tips, narrowing upward.</summary>
        static Mesh Pine(int seed)
        {
            var vs = new List<Vector3>(); var ns = new List<Vector3>(); var cs = new List<Color>(); var ts = new List<int>();
            const int tiers = 6;
            float height = 5.2f + N(seed, 0) * 0.8f;
            for (int t = 0; t < tiers; t++)
            {
                float f = t / (float)(tiers - 1);                       // 0 bottom .. 1 top
                float baseY = 1.0f + f * (height - 1.9f);
                float rad = Mathf.Lerp(1.45f, 0.42f, f) * (0.9f + N(seed, t + 10) * 0.2f);
                float apexY = baseY + Mathf.Lerp(1.25f, 1.0f, f);
                int spokes = t < 2 ? 11 : t < 4 ? 9 : 7;
                float twist = N(seed, t + 20) * Mathf.PI * 2f;
                float shade = Mathf.Lerp(0.62f, 1.0f, f);               // lower tiers sit in the shade of upper ones
                Skirt(vs, ns, cs, ts, apexY, baseY, rad, spokes, twist, seed * 31 + t, shade);
            }
            // Spire.
            Skirt(vs, ns, cs, ts, height + 0.35f, height - 0.75f, 0.26f, 5, 0f, seed * 31 + 99, 1f);
            return Build("Pine", vs, ns, cs, ts);
        }

        /// <summary>A cone-ish skirt: apex in the middle, ring of tips (low, far out) and notches (higher, further in),
        /// with a shallow underside so it looks solid from below.</summary>
        static void Skirt(List<Vector3> vs, List<Vector3> ns, List<Color> cs, List<int> ts, float apexY, float rimY, float rad,
                          int spokes, float twist, int seed, float shade)
        {
            int apex = vs.Count;
            vs.Add(new Vector3(0f, apexY, 0f)); ns.Add(Vector3.up); cs.Add(new Color(0.85f, 0.88f, 0.8f, 0.55f * shade));
            int ring = vs.Count, n = spokes * 2;
            float centreY = (apexY + rimY) * 0.5f;
            for (int i = 0; i < n; i++)
            {
                bool tip = i % 2 == 0;
                float a = twist + i * Mathf.PI * 2f / n + (N(seed, i) - 0.5f) * 0.25f;
                float r = tip ? rad * (0.88f + N(seed, i + 50) * 0.24f) : rad * 0.62f;
                float y = tip ? rimY - 0.12f - N(seed, i + 90) * 0.18f : rimY + 0.22f;
                var p = new Vector3(Mathf.Cos(a) * r, y, Mathf.Sin(a) * r);
                vs.Add(p);
                ns.Add((new Vector3(p.x, (p.y - centreY) * 0.6f + 0.35f * rad, p.z)).normalized);
                float lit = tip ? 1f : 0.7f;
                cs.Add(new Color(lit * 0.95f + 0.05f, lit, lit * 0.9f, (tip ? 1f : 0.6f) * shade));
            }
            int under = vs.Count;
            vs.Add(new Vector3(0f, rimY + 0.15f, 0f)); ns.Add(Vector3.down); cs.Add(new Color(0.45f, 0.45f, 0.42f, 0.2f * shade));
            for (int i = 0; i < n; i++)
            {
                int a = ring + i, b = ring + (i + 1) % n;
                ts.Add(apex); ts.Add(b); ts.Add(a);       // top (clockwise seen from above in Unity's left-handed space)
                ts.Add(under); ts.Add(a); ts.Add(b);      // underside
            }
        }

        // ------------------------------------------------------------------ broadleaf

        /// <summary>Broadleaf crown: 6-8 lumpy leaf masses around a centre about 3.4 m up.</summary>
        static Mesh Broadleaf(int seed, int detail)
        {
            var vs = new List<Vector3>(); var ns = new List<Vector3>(); var cs = new List<Color>(); var ts = new List<int>();
            var centre = new Vector3(0f, 3.5f, 0f);
            int blobs = 6 + (int)(N(seed, 1) * 3f);
            for (int b = 0; b < blobs; b++)
            {
                float a = b * 2.39996f + N(seed, b + 5);                       // golden angle spread
                float up = b == 0 ? 0.9f : (N(seed, b + 15) - 0.35f) * 1.4f;
                float out_ = b == 0 ? 0f : 0.75f + N(seed, b + 25) * 0.45f;
                var c = centre + new Vector3(Mathf.Cos(a) * out_, up, Mathf.Sin(a) * out_);
                float r = b == 0 ? 1.25f : 0.85f + N(seed, b + 35) * 0.4f;
                Blob(vs, ns, cs, ts, c, new Vector3(r * 1.1f, r * 0.85f, r * 1.1f), centre, seed * 17 + b, detail);
            }
            return Build("Broadleaf", vs, ns, cs, ts);
        }

        static readonly Vector3[][] _ico = new Vector3[3][];
        static readonly int[][] _icoTris = new int[3][];

        /// <summary>A bumpy ellipsoid; normals and shading measured from the crown centre so the masses read as one crown.</summary>
        static void Blob(List<Vector3> vs, List<Vector3> ns, List<Color> cs, List<int> ts, Vector3 c, Vector3 r, Vector3 crown, int seed, int detail)
        {
            if (_ico[detail] == null) Icosphere(detail, out _ico[detail], out _icoTris[detail]);
            int o = vs.Count;
            foreach (var d in _ico[detail])
            {
                float bump = 1f + (Mathf.PerlinNoise(d.x * 2.1f + seed * 0.37f, d.z * 2.1f + d.y * 1.3f) - 0.5f) * 0.45f;
                var p = c + Vector3.Scale(d, r) * bump;
                vs.Add(p);
                var fromCrown = (p - crown);
                ns.Add((fromCrown.normalized * 0.7f + d * 0.3f).normalized);
                // Exposure: top and outside are bright, underneath and deep inside are dark.
                float outside = Mathf.Clamp01(fromCrown.magnitude / 2.2f);
                float top = d.y * 0.5f + 0.5f;
                float ex = Mathf.Clamp01(0.25f + 0.45f * outside + 0.4f * top);
                cs.Add(new Color(0.9f + 0.1f * top, 1f, 0.85f + 0.1f * top, ex));
            }
            foreach (int t in _icoTris[detail]) ts.Add(o + t);
        }

        static void Icosphere(int subdiv, out Vector3[] verts, out int[] tris)
        {
            float t = (1f + Mathf.Sqrt(5f)) / 2f;
            var v = new List<Vector3>
            {
                new Vector3(-1, t, 0), new Vector3(1, t, 0), new Vector3(-1, -t, 0), new Vector3(1, -t, 0),
                new Vector3(0, -1, t), new Vector3(0, 1, t), new Vector3(0, -1, -t), new Vector3(0, 1, -t),
                new Vector3(t, 0, -1), new Vector3(t, 0, 1), new Vector3(-t, 0, -1), new Vector3(-t, 0, 1),
            };
            for (int i = 0; i < v.Count; i++) v[i] = v[i].normalized;
            var f = new List<int> { 0,11,5, 0,5,1, 0,1,7, 0,7,10, 0,10,11, 1,5,9, 5,11,4, 11,10,2, 10,7,6, 7,1,8,
                                    3,9,4, 3,4,2, 3,2,6, 3,6,8, 3,8,9, 4,9,5, 2,4,11, 6,2,10, 8,6,7, 9,8,1 };
            for (int s = 0; s < subdiv; s++)
            {
                var mid = new Dictionary<long, int>();
                var nf = new List<int>();
                int Mid(int a, int b)
                {
                    long key = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
                    if (mid.TryGetValue(key, out int m)) return m;
                    v.Add(((v[a] + v[b]) * 0.5f).normalized);
                    mid[key] = v.Count - 1;
                    return v.Count - 1;
                }
                for (int i = 0; i < f.Count; i += 3)
                {
                    int a = f[i], b = f[i + 1], c = f[i + 2];
                    int ab = Mid(a, b), bc = Mid(b, c), ca = Mid(c, a);
                    nf.AddRange(new[] { a, ab, ca, b, bc, ab, c, ca, bc, ab, bc, ca });
                }
                f = nf;
            }
            verts = v.ToArray();
            tris = f.ToArray();
        }

        static Mesh Build(string name, List<Vector3> vs, List<Vector3> ns, List<Color> cs, List<int> ts)
        {
            var m = new Mesh { name = name };
            m.SetVertices(vs); m.SetNormals(ns); m.SetColors(cs); m.SetTriangles(ts, 0);
            m.RecalculateBounds();
            // Room for the sway so the crown isn't culled at the screen edge.
            var b = m.bounds; b.Expand(0.6f); m.bounds = b;
            return m;
        }
    }
}
