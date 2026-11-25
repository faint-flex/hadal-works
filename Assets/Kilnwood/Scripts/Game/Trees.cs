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
        static Mesh[] _pines, _leafy, _leafyFar, _pineCards;
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

        // Broadleaf greens: deeper and less yellow than the old toy palette, so sunlit crowns stay natural.
        static readonly Color[] Leaf = { new Color(0.19f, 0.34f, 0.13f), new Color(0.22f, 0.37f, 0.14f), new Color(0.17f, 0.31f, 0.13f), new Color(0.24f, 0.36f, 0.15f) };

        static Material Mat(Color c)
        {
            if (_mats.TryGetValue(c, out var m)) return m;
            m = new Material(_src) { enableInstancing = true };
            m.SetColor("_Color", c);
            m.SetFloat("_Translucency", 0.2f);
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
                Add("Pine", parent, _pines[v], Mat(g * 1.15f), Vector3.zero, Vector3.one * s * (far ? 1.35f : 1f), !far);
                if (!far && CardsAvailable)
                {
                    _pineCards ??= MakeAll(PineCards);
                    Add("Twigs", parent, _pineCards[v], CardMat(_twigTex, g), Vector3.zero, Vector3.one * s, true);
                }
            }
            else
            {
                _leafy ??= MakeAll(i => Broadleaf(i, 2));
                _leafyFar ??= MakeAll(i => Broadleaf(i, 1));     // far hills: same shapes, fewer triangles
                var g = Leaf[(int)(r2 * 3.99f)];
                if (!far) Art.Part("Trunk", parent, Art.Frustum, Vector3.zero, new Vector3(0.38f * s, 2.6f * s, 0.38f * s), Look.Bark);
                Add("Crown", parent, far ? _leafyFar[v] : _leafy[v], Mat(g), Vector3.zero, Vector3.one * s * (far ? 1.35f : 1f), !far);
                // Leaf cards (0.18.0, 0.18.2) read as stickers on a smooth blob and cost frame time, so they stay off;
                // broadleaf crowns need a real rebuild (ragged outline) in a later pass.
            }
        }

        static float Frac(float x) => x - Mathf.Floor(x);

        // ------------------------------------------------------------------ 0.18 foliage cards

        static Shader _cardShader;
        static Texture2D _twigTex, _leafTex;
        static bool _cardsChecked;
        static readonly Dictionary<(Texture2D, Color), Material> _cardMats = new Dictionary<(Texture2D, Color), Material>();

        /// <summary>Scanned twig and leaf cards (Resources/Foliage, from Poly Haven, CC0) and their shader.</summary>
        static bool CardsAvailable
        {
            get
            {
                if (!_cardsChecked)
                {
                    _cardsChecked = true;
                    _cardShader = Shader.Find("Kilnwood/FoliageCard");
                    _twigTex = Resources.Load<Texture2D>("Foliage/twig_cards");
                    _leafTex = Resources.Load<Texture2D>("Foliage/leaf_cards");
                    if (_cardShader != null && !_cardShader.isSupported) _cardShader = null;
                }
                return _cardShader != null && _twigTex != null && _leafTex != null;
            }
        }

        static Material CardMat(Texture2D tex, Color crown)
        {
            if (_cardMats.TryGetValue((tex, crown), out var m)) return m;
            m = new Material(_cardShader) { enableInstancing = true, name = "Foliage cards" };
            m.mainTexture = tex;
            // The photo is already green: tint only a little toward the crown's colour so each tree still differs.
            float lum = crown.r * 0.3f + crown.g * 0.59f + crown.b * 0.11f;
            var rel = new Color(crown.r / lum, crown.g / lum, crown.b / lum) * 0.5f;
            m.SetColor("_Color", Color.Lerp(new Color(0.92f, 0.95f, 0.88f), rel, 0.25f));
            m.SetFloat("_Cutoff", 0.45f);
            _cardMats[(tex, crown)] = m;
            return m;
        }

        /// <summary>One card: a trapezoid from base (narrow) to tip (wide) along len, spread along wid; uv cell (u0, v0) of size cw x ch.</summary>
        static void Card(List<Vector3> vs, List<Vector3> ns, List<Color> cs, List<Vector2> uv, List<int> ts, Vector3 basePt, Vector3 tipPt,
                         Vector3 wid, float wBase, float wTip, Vector3 normal, Color baseCol, Color tipCol, float u0, float v0, float cw, float ch)
        {
            int o = vs.Count;
            vs.Add(basePt - wid * wBase); vs.Add(basePt + wid * wBase); vs.Add(tipPt + wid * wTip); vs.Add(tipPt - wid * wTip);
            for (int k = 0; k < 4; k++) ns.Add(normal);
            cs.Add(baseCol); cs.Add(baseCol); cs.Add(tipCol); cs.Add(tipCol);
            float ub = (1f - wBase / Mathf.Max(wTip, 1e-4f)) * 0.5f;
            uv.Add(new Vector2(u0 + cw * ub, v0)); uv.Add(new Vector2(u0 + cw * (1f - ub), v0)); uv.Add(new Vector2(u0 + cw, v0 + ch)); uv.Add(new Vector2(u0, v0 + ch));
            ts.Add(o); ts.Add(o + 2); ts.Add(o + 1); ts.Add(o); ts.Add(o + 3); ts.Add(o + 2);
        }

        /// <summary>Fir twig cards along every branch tip of the pine's skirts (same layout as Pine), two crossed per branch.</summary>
        static Mesh PineCards(int seed)
        {
            var vs = new List<Vector3>(); var ns = new List<Vector3>(); var cs = new List<Color>(); var uv = new List<Vector2>(); var ts = new List<int>();
            const int tiers = 6;
            float height = 5.2f + N(seed, 0) * 0.8f;
            for (int t = 0; t <= tiers; t++)
            {
                bool spire = t == tiers;
                float f = spire ? 1f : t / (float)(tiers - 1);
                float baseY = spire ? height - 0.75f : 1.0f + f * (height - 1.9f);
                float rad = spire ? 0.3f : Mathf.Lerp(1.45f, 0.42f, f) * (0.9f + N(seed, t + 10) * 0.2f);
                float apexY = spire ? height + 0.35f : baseY + Mathf.Lerp(1.25f, 1.0f, f);
                int spokes = spire ? 4 : t < 2 ? 11 : t < 4 ? 9 : 7;
                float twist = spire ? 0f : N(seed, t + 20) * Mathf.PI * 2f;
                float shade = Mathf.Lerp(0.62f, 1.0f, f);
                int sd = seed * 31 + (spire ? 99 : t);
                int n = spokes * 2;
                for (int i = 0; i < n; i += 2)
                {
                    float a = twist + i * Mathf.PI * 2f / n + (N(sd, i) - 0.5f) * 0.25f;
                    float r = rad * (0.88f + N(sd, i + 50) * 0.24f) * 1.12f;
                    float y = baseY - 0.12f - N(sd, i + 90) * 0.18f;
                    var outDir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                    var tip = outDir * r + Vector3.up * (spire ? apexY - baseY + y : y);
                    var basePt = outDir * (rad * 0.12f) + Vector3.up * (spire ? baseY : Mathf.Lerp(apexY, baseY, 0.45f));
                    var len = tip - basePt;
                    var side = Vector3.Cross(len.normalized, Vector3.up).normalized;
                    var nrm = new Vector3(tip.x, 0.35f * rad + 0.25f, tip.z).normalized;
                    float w = Mathf.Max(0.35f, rad * 0.55f);
                    int cell = (i / 2 + t + seed) & 3;
                    float u0 = (cell & 1) * 0.5f, v0 = (cell & 2) == 0 ? 0.5f : 0f;
                    var cb = new Color(0.8f, 0.82f, 0.78f, 0.45f * shade); var ct = new Color(1f, 1f, 0.95f, 1f * shade);
                    Card(vs, ns, cs, uv, ts, basePt, tip, side, w * 0.25f, w, nrm, cb, ct, u0, v0, 0.5f, 0.5f);
                    // A second card on edge, tilted, so the branch has depth from the side.
                    var up2 = Vector3.Cross(side, len.normalized).normalized;
                    Card(vs, ns, cs, uv, ts, basePt, tip, (up2 * 0.6f + side * 0.4f).normalized, w * 0.15f, w * 0.6f, nrm, cb, ct, (cell & 1) == 0 ? 0.5f : 0f, v0, 0.5f, 0.5f);
                }
            }
            return BuildCards("Pine twigs", vs, ns, cs, uv, ts);
        }

        /// <summary>Leaf-spray cards over each leaf mass of the broadleaf crown (same layout as Broadleaf), facing outward.</summary>
        static Mesh LeafCards(int seed)
        {
            var vs = new List<Vector3>(); var ns = new List<Vector3>(); var cs = new List<Color>(); var uv = new List<Vector2>(); var ts = new List<int>();
            var centre = new Vector3(0f, 3.5f, 0f);
            int blobs = 6 + (int)(N(seed, 1) * 3f);
            for (int b = 0; b < blobs; b++)
            {
                float a = b * 2.39996f + N(seed, b + 5);
                float up = b == 0 ? 0.9f : (N(seed, b + 15) - 0.35f) * 1.4f;
                float out_ = b == 0 ? 0f : 0.75f + N(seed, b + 25) * 0.45f;
                var c = centre + new Vector3(Mathf.Cos(a) * out_, up, Mathf.Sin(a) * out_);
                float r = b == 0 ? 1.25f : 0.85f + N(seed, b + 35) * 0.4f;
                int cards = b == 0 ? 16 : 11;
                for (int k = 0; k < cards; k++)
                {
                    // Spread over the mass (golden spiral), a bit more on top and outside.
                    float z = 1f - 2f * (k + 0.5f) / cards, rr = Mathf.Sqrt(1f - z * z), ph = k * 2.39996f + seed;
                    var d = new Vector3(Mathf.Cos(ph) * rr, z * 0.8f + 0.2f, Mathf.Sin(ph) * rr).normalized;
                    var p = c + Vector3.Scale(d, new Vector3(r * 1.1f, r * 0.85f, r * 1.1f)) * 0.95f;
                    var side = Vector3.Cross(d, Vector3.up); if (side.sqrMagnitude < 1e-3f) side = Vector3.right; side.Normalize();
                    var along = Vector3.Cross(side, d).normalized;
                    float sz = 1.05f + N(seed * 7 + b, k) * 0.45f;   // round clusters: big enough to break the crown outline
                    var fromCrown = (p - centre);
                    var nrm = (fromCrown.normalized * 0.7f + d * 0.3f).normalized;
                    float ex = Mathf.Clamp01(0.3f + 0.45f * Mathf.Clamp01(fromCrown.magnitude / 2.2f) + 0.35f * (d.y * 0.5f + 0.5f));
                    var col = new Color(0.95f, 1f, 0.92f, ex);
                    float v0 = (k & 1) * 0.5f;
                    Card(vs, ns, cs, uv, ts, p - along * sz * 0.5f, p + along * sz * 0.5f, side, sz * 0.5f, sz * 0.5f, nrm, col, col, 0f, v0, 1f, 0.5f);
                }
            }
            return BuildCards("Leaf sprays", vs, ns, cs, uv, ts);
        }

        static Mesh BuildCards(string name, List<Vector3> vs, List<Vector3> ns, List<Color> cs, List<Vector2> uv, List<int> ts)
        {
            var m = new Mesh { name = name };
            m.SetVertices(vs); m.SetNormals(ns); m.SetColors(cs); m.SetUVs(0, uv); m.SetTriangles(ts, 0);
            m.RecalculateBounds();
            var b = m.bounds; b.Expand(0.6f); m.bounds = b;
            return m;
        }

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
            if (detail >= 2) return BroadleafClumps(seed);
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

        /// <summary>
        /// 0.19 photoreal pass 3: a near broadleaf crown as a few inner masses wrapped in about 26 small leaf clumps, so the
        /// outline is ragged and the light breaks up into clusters (real crowns read as clumps, not one smooth ball).
        /// Same triangle budget as the old eight smooth blobs; no alpha cards, so no overdraw.
        /// </summary>
        static Mesh BroadleafClumps(int seed)
        {
            var vs = new List<Vector3>(); var ns = new List<Vector3>(); var cs = new List<Color>(); var ts = new List<int>();
            var centre = new Vector3(0f, 3.6f, 0f);
            var env = new Vector3(1.75f + N(seed, 2) * 0.35f, 1.35f + N(seed, 3) * 0.3f, 1.75f + N(seed, 4) * 0.35f);
            // Inner masses fill the crown so no sky shows through the middle.
            for (int b = 0; b < 4; b++)
            {
                float a = b * 1.57f + N(seed, b + 50);
                var c = centre + new Vector3(Mathf.Cos(a) * 0.45f, (b == 0 ? 0.35f : -0.1f), Mathf.Sin(a) * 0.45f);
                Blob(vs, ns, cs, ts, c, Vector3.Scale(env, new Vector3(0.62f, 0.66f, 0.62f)), centre, seed * 31 + b, 1, 0.35f, 0.9f);
            }
            // Outer clumps on the envelope: more on top and the sunny sides, fewer underneath.
            const int clumps = 26;
            for (int k = 0; k < clumps; k++)
            {
                float z = 1f - 1.75f * (k + 0.5f) / clumps;                       // stop short of the very bottom
                float rr = Mathf.Sqrt(Mathf.Max(0f, 1f - z * z)), ph = k * 2.39996f + seed * 1.3f;
                var d = new Vector3(Mathf.Cos(ph) * rr, z, Mathf.Sin(ph) * rr);
                float jitter = 0.82f + N(seed * 3 + 1, k) * 0.3f;
                var c = centre + Vector3.Scale(d, env) * jitter;
                float r = 0.42f + N(seed * 5 + 2, k) * 0.28f;
                float shade = 0.84f + N(seed * 7 + 3, k) * 0.26f;              // clump-to-clump tone variation
                Blob(vs, ns, cs, ts, c, new Vector3(r * 1.15f, r * 0.85f, r * 1.15f), centre, seed * 17 + k, 1, 0.5f, shade);
            }
            return Build("Broadleaf clumps", vs, ns, cs, ts);
        }

        static readonly Vector3[][] _ico = new Vector3[3][];
        static readonly int[][] _icoTris = new int[3][];

        /// <summary>A bumpy ellipsoid; normals and shading measured from the crown centre so the masses read as one crown.</summary>
        static void Blob(List<Vector3> vs, List<Vector3> ns, List<Color> cs, List<int> ts, Vector3 c, Vector3 r, Vector3 crown, int seed, int detail,
                         float own = 0.3f, float shade = 1f)
        {
            if (_ico[detail] == null) Icosphere(detail, out _ico[detail], out _icoTris[detail]);
            int o = vs.Count;
            foreach (var d in _ico[detail])
            {
                float bump = 1f + (Mathf.PerlinNoise(d.x * 2.1f + seed * 0.37f, d.z * 2.1f + d.y * 1.3f) - 0.5f) * 0.45f;
                var p = c + Vector3.Scale(d, r) * bump;
                vs.Add(p);
                var fromCrown = (p - crown);
                ns.Add((fromCrown.normalized * (1f - own) + d * own).normalized);
                // Exposure: top and outside are bright, underneath and deep inside are dark.
                float outside = Mathf.Clamp01(fromCrown.magnitude / 2.2f);
                float top = d.y * 0.5f + 0.5f;
                float ex = Mathf.Clamp01(0.25f + 0.45f * outside + 0.4f * top);
                cs.Add(new Color(0.92f * shade, 1f * shade, 0.9f * shade, ex));
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
