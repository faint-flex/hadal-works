using System.Collections.Generic;
using Kilnwood.Sim;
using UnityEngine;
using Terrain = Kilnwood.Sim.Terrain;

namespace Kilnwood.Game
{
    /// <summary>
    /// Environment Artist's toolkit: low-poly meshes made in code (cones, roofs, tapered towers), shaped parts,
    /// the painted valley floor, far hills, trees, smoke. Everything is generated at runtime, so no art assets.
    /// </summary>
    public static class Art
    {
        // ------------------------------------------------------------------ meshes

        static readonly Dictionary<PrimitiveType, Mesh> _prims = new Dictionary<PrimitiveType, Mesh>();
        static Mesh _cone, _frustum, _roof, _beltQuad, _rock, _ingot;

        public static Mesh Prim(PrimitiveType t)
        {
            if (_prims.TryGetValue(t, out var m)) return m;
            var go = GameObject.CreatePrimitive(t);
            m = go.GetComponent<MeshFilter>().sharedMesh;
            Object.Destroy(go);
            _prims[t] = m;
            return m;
        }

        /// <summary>Cone: base radius 0.5 at y = 0, point at y = 1. Faceted (flat shaded) on purpose.</summary>
        public static Mesh Cone => _cone ??= Tapered(9, 0f, "Cone");

        /// <summary>Tapered tower: base radius 0.5 at y = 0, top radius 0.3 at y = 1.</summary>
        public static Mesh Frustum => _frustum ??= Tapered(10, 0.6f, "Frustum");

        /// <summary>Chunky 7-sided rock: base radius 0.5, flat top radius 0.27, 1 tall.</summary>
        public static Mesh Rock => _rock ??= RockMesh();

        /// <summary>Rounded, lumpy boulder: 1 wide, 1 tall, uneven rings so it reads as rock, not a post.</summary>
        static Mesh RockMesh()
        {
            const int seg = 8;
            float[] ys = { 0f, 0.4f, 0.78f, 1f };
            float[] rs = { 0.5f, 0.48f, 0.34f, 0.12f };
            var ring = new Vector3[ys.Length, seg];
            for (int r = 0; r < ys.Length; r++)
                for (int i = 0; i < seg; i++)
                {
                    float a = (i + (r % 2) * 0.5f) * Mathf.PI * 2f / seg;
                    float jit = 0.82f + 0.3f * Hash(i, r, 11);
                    ring[r, i] = new Vector3(Mathf.Cos(a) * rs[r] * jit, ys[r] * (r == 0 ? 1f : 0.9f + 0.2f * Hash(i, r, 12)), Mathf.Sin(a) * rs[r] * jit);
                }
            var v = new List<Vector3>();
            var tri = new List<int>();
            for (int r = 0; r < ys.Length - 1; r++)
                for (int i = 0; i < seg; i++)
                {
                    int n = (i + 1) % seg;
                    Tri(v, tri, ring[r, i], ring[r, n], ring[r + 1, i]);
                    Tri(v, tri, ring[r, n], ring[r + 1, n], ring[r + 1, i]);
                }
            var top = new Vector3(0f, 1.12f, 0f);
            for (int i = 0; i < seg; i++) Tri(v, tri, ring[ys.Length - 1, i], ring[ys.Length - 1, (i + 1) % seg], top);
            for (int i = 0; i < seg; i++) Tri(v, tri, ring[0, i], ring[0, (i + 1) % seg], Vector3.zero);
            return Finish(v, tri, "Rock");
        }

        /// <summary>Square ingot / bar: 1 x 1 base, sloped sides, 1 tall.</summary>
        public static Mesh Ingot => _ingot ??= Tapered(4, 0.72f, "Ingot", Mathf.PI / 4f, 1.4142f);

        /// <summary>Gable roof: 1 wide (x), 1 deep (z), 1 tall (y); ridge runs along z.</summary>
        public static Mesh Roof
        {
            get
            {
                if (_roof != null) return _roof;
                var v = new List<Vector3>();
                var tri = new List<int>();
                Vector3 l0 = new Vector3(-0.5f, 0f, -0.5f), l1 = new Vector3(-0.5f, 0f, 0.5f);
                Vector3 r0 = new Vector3(0.5f, 0f, -0.5f), r1 = new Vector3(0.5f, 0f, 0.5f);
                Vector3 t0 = new Vector3(0f, 1f, -0.5f), t1 = new Vector3(0f, 1f, 0.5f);
                Quad(v, tri, l0, l1, t1, t0);      // left slope
                Quad(v, tri, r1, r0, t0, t1);      // right slope
                Tri(v, tri, l0, t0, r0);           // back gable
                Tri(v, tri, r1, t1, l1);           // front gable
                Quad(v, tri, l1, l0, r0, r1);      // underside
                _roof = Finish(v, tri, "Roof");
                return _roof;
            }
        }

        /// <summary>Flat quad facing up, 1 x 1, with v running back (z = -0.5) to front (z = +0.5): belts scroll along it.</summary>
        public static Mesh BeltQuad
        {
            get
            {
                if (_beltQuad != null) return _beltQuad;
                var m = new Mesh { name = "BeltQuad" };
                m.vertices = new[] { new Vector3(-0.5f, 0f, -0.5f), new Vector3(0.5f, 0f, -0.5f), new Vector3(0.5f, 0f, 0.5f), new Vector3(-0.5f, 0f, 0.5f) };
                m.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f) };
                m.triangles = new[] { 0, 2, 1, 0, 3, 2 };
                m.RecalculateNormals();
                m.RecalculateBounds();
                _beltQuad = m;
                return m;
            }
        }

        static Mesh Tapered(int seg, float topRatio, string name, float angle0 = 0f, float radiusScale = 1f)
        {
            var v = new List<Vector3>();
            var tri = new List<int>();
            float rb = 0.5f * radiusScale, rt = rb * topRatio;
            for (int i = 0; i < seg; i++)
            {
                float a0 = angle0 + i * Mathf.PI * 2f / seg, a1 = angle0 + (i + 1) * Mathf.PI * 2f / seg;
                var b0 = new Vector3(Mathf.Cos(a0) * rb, 0f, Mathf.Sin(a0) * rb);
                var b1 = new Vector3(Mathf.Cos(a1) * rb, 0f, Mathf.Sin(a1) * rb);
                var t0 = new Vector3(Mathf.Cos(a0) * rt, 1f, Mathf.Sin(a0) * rt);
                var t1 = new Vector3(Mathf.Cos(a1) * rt, 1f, Mathf.Sin(a1) * rt);
                if (topRatio <= 0f) Tri(v, tri, b1, b0, new Vector3(0f, 1f, 0f));
                else
                {
                    Quad(v, tri, b1, b0, t0, t1);
                    Tri(v, tri, t1, t0, new Vector3(0f, 1f, 0f));
                }
                Tri(v, tri, b0, b1, Vector3.zero);
            }
            return Finish(v, tri, name);
        }

        static void Tri(List<Vector3> v, List<int> t, Vector3 a, Vector3 b, Vector3 c)
        {
            int i = v.Count;
            v.Add(a); v.Add(b); v.Add(c);
            t.Add(i); t.Add(i + 1); t.Add(i + 2);
        }

        static void Quad(List<Vector3> v, List<int> t, Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            Tri(v, t, a, b, c);
            Tri(v, t, a, c, d);
        }

        static Mesh Finish(List<Vector3> v, List<int> t, string name)
        {
            var m = new Mesh { name = name };
            m.SetVertices(v);
            m.SetTriangles(t, 0);
            // Make sure triangles face outward whatever order we wrote them in: flip any whose normal points inward.
            var verts = m.vertices;
            var tris = m.triangles;
            var center = Vector3.zero;
            foreach (var p in verts) center += p;
            center /= Mathf.Max(1, verts.Length);
            for (int i = 0; i < tris.Length; i += 3)
            {
                Vector3 a = verts[tris[i]], b = verts[tris[i + 1]], c = verts[tris[i + 2]];
                var n = Vector3.Cross(b - a, c - a);
                var mid = (a + b + c) / 3f;
                if (Vector3.Dot(n, mid - center) < 0f) { int tmp = tris[i + 1]; tris[i + 1] = tris[i + 2]; tris[i + 2] = tmp; }
            }
            m.triangles = tris;
            m.RecalculateNormals();
            // 0.17: box-projected UVs so the mesh gets proper tangents (the triplanar shader's scanned normal maps need them).
            var nrm = m.normals;
            var uv = new Vector2[verts.Length];
            for (int i = 0; i < verts.Length; i++)
            {
                var an = new Vector3(Mathf.Abs(nrm[i].x), Mathf.Abs(nrm[i].y), Mathf.Abs(nrm[i].z));
                var p = verts[i];
                uv[i] = an.x >= an.y && an.x >= an.z ? new Vector2(p.z, p.y) : an.y >= an.z ? new Vector2(p.x, p.z) : new Vector2(p.x, p.y);
            }
            m.uv = uv;
            m.RecalculateTangents();
            m.RecalculateBounds();
            return m;
        }

        // ------------------------------------------------------------------ parts

        public static GameObject Part(string name, Transform parent, Mesh mesh, Vector3 pos, Vector3 scale, Color c, Vector3 euler = default, bool shadows = true)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.Euler(euler);
            go.transform.localScale = scale;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = Look.Mat(c);
            if (!shadows) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }

        public static GameObject Cube(string n, Transform p, Vector3 pos, Vector3 size, Color c, Vector3 euler = default) => Part(n, p, Prim(PrimitiveType.Cube), pos, size, c, euler);

        /// <summary>Upright cylinder standing on `pos` (bottom), with diameter and height.</summary>
        public static GameObject Cyl(string n, Transform p, Vector3 pos, float diameter, float height, Color c, Vector3 euler = default) =>
            Part(n, p, Prim(PrimitiveType.Cylinder), pos + Quaternion.Euler(euler) * new Vector3(0f, height / 2f, 0f), new Vector3(diameter, height / 2f, diameter), c, euler);

        public static GameObject Ball(string n, Transform p, Vector3 pos, Vector3 size, Color c, Vector3 euler = default) => Part(n, p, Prim(PrimitiveType.Sphere), pos, size, c, euler);

        public static GameObject ConeAt(string n, Transform p, Vector3 basePos, float diameter, float height, Color c) => Part(n, p, Cone, basePos, new Vector3(diameter, height, diameter), c);

        /// <summary>Gable roof sitting on `basePos` (eaves height), `width` across, `depth` along the ridge.</summary>
        public static GameObject RoofAt(string n, Transform p, Vector3 basePos, float width, float depth, float height, Color c, float yaw = 0f) =>
            Part(n, p, Roof, basePos, new Vector3(width, height, depth), c, new Vector3(0f, yaw, 0f));

        /// <summary>A log lying on its side, centred on `center`, running along local x (yaw turns it).</summary>
        public static GameObject Log(string n, Transform p, Vector3 center, float length, float diameter, Color c, float yaw = 0f) =>
            Part(n, p, Prim(PrimitiveType.Cylinder), center, new Vector3(diameter, length / 2f, diameter), c, new Vector3(0f, yaw, 90f));

        /// <summary>Flat arrow head lying on the ground pointing along local +z (yaw turns it).</summary>
        public static GameObject Arrow(string n, Transform p, Vector3 pos, float width, float length, Color c, float yaw = 0f) =>
            Part(n, p, Cone, pos, new Vector3(width, length, 0.06f), c, new Vector3(90f, yaw, 0f), false);

        /// <summary>Toothed gear lying flat (axis = local y) centred on `pos`.</summary>
        public static Transform Gear(string n, Transform p, Vector3 pos, float diameter, float thickness, Color c, int teeth = 8)
        {
            var g = new GameObject(n).transform;
            g.SetParent(p, false);
            g.localPosition = pos;
            Part("Wheel", g, Prim(PrimitiveType.Cylinder), Vector3.zero, new Vector3(diameter * 0.82f, thickness / 2f, diameter * 0.82f), c);
            Part("Hub", g, Prim(PrimitiveType.Cylinder), Vector3.zero, new Vector3(diameter * 0.25f, thickness * 0.75f, diameter * 0.25f), c * 0.7f);
            float tw = diameter * 0.16f;
            for (int i = 0; i < teeth; i++)
            {
                float a = i * 360f / teeth;
                var dir = Quaternion.Euler(0f, a, 0f) * Vector3.forward;
                Part("Tooth", g, Prim(PrimitiveType.Cube), dir * diameter * 0.43f, new Vector3(tw, thickness, tw), c, new Vector3(0f, a, 0f));
            }
            return g;
        }

        /// <summary>A rock with random tilt and turn. `k` in 0..1 varies it.</summary>
        public static GameObject RockAt(string n, Transform p, Vector3 pos, Vector3 size, Color c, float k) =>
            Part(n, p, Rock, pos, size, c, new Vector3((k - 0.5f) * 14f, k * 360f, (0.5f - k) * 10f));

        // ------------------------------------------------------------------ textures

        static Texture2D _beltTex, _smokeTex, _grassTex, _waterTex;

        /// <summary>Blue water with soft lighter streaks; scrolled along the river to show the current.</summary>
        public static Texture2D WaterTexture
        {
            get
            {
                if (_waterTex != null) return _waterTex;
                const int n = 128;
                var t = new Texture2D(n, n, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Repeat, name = "Water" };
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        float a = TileNoise(x, y, n, 0.06f), b = TileNoise(x * 3 % n, y, n, 0.18f);
                        float streak = Mathf.Clamp01((b - 0.55f) * 4f);
                        var c = Color.Lerp(new Color(0.16f, 0.34f, 0.46f), new Color(0.24f, 0.45f, 0.56f), a);
                        c = Color.Lerp(c, new Color(0.55f, 0.7f, 0.76f), streak * 0.5f);
                        c.a = 1f;
                        t.SetPixel(x, y, c);
                    }
                t.Apply();
                _waterTex = t;
                return t;
            }
        }

        /// <summary>Dark belt with lighter cross slats; scrolls to show flow.</summary>
        public static Texture2D BeltTexture
        {
            get
            {
                if (_beltTex != null) return _beltTex;
                var t = new Texture2D(32, 64, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear, name = "Belt" };
                for (int y = 0; y < 64; y++)
                    for (int x = 0; x < 32; x++)
                    {
                        bool slat = (y % 16) < 3;
                        bool edge = x < 2 || x > 29;
                        float n = Mathf.PerlinNoise(x * 0.3f, y * 0.3f) * 0.05f;
                        var c = slat ? new Color(0.42f, 0.36f, 0.3f) : new Color(0.17f + n, 0.15f + n, 0.13f + n);
                        if (edge) c = new Color(0.1f, 0.09f, 0.08f);
                        t.SetPixel(x, y, c);
                    }
                t.Apply();
                _beltTex = t;
                return t;
            }
        }

        public static Texture2D SmokeTexture
        {
            get
            {
                if (_smokeTex != null) return _smokeTex;
                var t = new Texture2D(64, 64, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "Smoke" };
                for (int y = 0; y < 64; y++)
                    for (int x = 0; x < 64; x++)
                    {
                        float dx = (x - 31.5f) / 31.5f, dy = (y - 31.5f) / 31.5f;
                        float d = Mathf.Sqrt(dx * dx + dy * dy);
                        float a = Mathf.Clamp01(1f - d);
                        a = a * a * (0.7f + 0.3f * Mathf.PerlinNoise(x * 0.15f, y * 0.15f));
                        t.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                    }
                t.Apply();
                _smokeTex = t;
                return t;
            }
        }

        /// <summary>Soft tiling grass for the hills outside the valley.</summary>
        public static Texture2D GrassTexture
        {
            get
            {
                if (_grassTex != null) return _grassTex;
                const int n = 128;
                var t = new Texture2D(n, n, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Repeat, name = "Grass" };
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        float a = TileNoise(x, y, n, 0.08f), b = TileNoise(x, y, n, 0.35f);
                        var c = Color.Lerp(new Color(0.3f, 0.42f, 0.2f), new Color(0.42f, 0.52f, 0.26f), a);
                        c = Color.Lerp(c, c * 0.85f, b * 0.6f);
                        c.a = 1f;
                        t.SetPixel(x, y, c);
                    }
                t.Apply();
                _grassTex = t;
                return t;
            }
        }

        static float TileNoise(int x, int y, int n, float f)
        {
            // Blend four samples so the texture tiles without seams.
            float u = x / (float)n, v = y / (float)n;
            float s = n * f;
            float a = Mathf.PerlinNoise(x * f, y * f), b = Mathf.PerlinNoise((x - n) * f + 100f, y * f);
            float c = Mathf.PerlinNoise(x * f, (y - n) * f + 200f), d = Mathf.PerlinNoise((x - n) * f + 100f, (y - n) * f + 200f);
            return Mathf.Lerp(Mathf.Lerp(a, b, u), Mathf.Lerp(c, d, u), v);
        }

        // ------------------------------------------------------------------ realistic ground and water

        static Texture2D _grain, _grainNormal, _ripples;

        static Texture2D _cloud;

        /// <summary>Sun cookie for drifting cloud shadows: mostly clear (1), soft patches down to about 0.6. Tiles.</summary>
        public static Texture2D CloudCookie
        {
            get
            {
                if (_cloud != null) return _cloud;
                const int n = 256;
                _cloud = new Texture2D(n, n, TextureFormat.Alpha8, true) { name = "Clouds", wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear };
                var px = new Color32[n * n];
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        float f = Fbm(x / (float)n, y / (float)n, 4);
                        float shade = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((f - 0.52f) * 5f));   // clouds cover roughly a third
                        byte a = (byte)(255f * (1f - 0.58f * shade));
                        px[y * n + x] = new Color32(a, a, a, a);
                    }
                _cloud.SetPixels32(px);
                _cloud.Apply(true);
                return _cloud;
            }
        }

        /// <summary>Soft grey noise (also used to weather building surfaces).</summary>
        public static Texture2D Grain { get { if (_grain == null) MakeGrain(); return _grain; } }

        /// <summary>Tileable value noise on a `period` lattice (wraps at 1).</summary>
        static float TileNoise(float u, float v, int period, int seed)
        {
            float x = u * period, y = v * period;
            int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
            float fx = x - x0, fy = y - y0;
            fx = fx * fx * (3f - 2f * fx); fy = fy * fy * (3f - 2f * fy);
            float H(int i, int j) => Hash(((i % period) + period) % period, ((j % period) + period) % period, seed);
            float a = Mathf.Lerp(H(x0, y0), H(x0 + 1, y0), fx), b = Mathf.Lerp(H(x0, y0 + 1), H(x0 + 1, y0 + 1), fx);
            return Mathf.Lerp(a, b, fy);
        }

        static float Fbm(float u, float v, int seed)
        {
            float sum = 0f, amp = 0.5f, norm = 0f;
            int period = 4;
            for (int o = 0; o < 5; o++) { sum += amp * TileNoise(u, v, period, seed + o * 17); norm += amp; amp *= 0.55f; period *= 2; }
            return sum / norm;
        }

        static Texture2D NormalFrom(float[] h, int n, float strength, string name)
        {
            var nt = new Texture2D(n, n, TextureFormat.RGBA32, true, true) { name = name, wrapMode = TextureWrapMode.Repeat, anisoLevel = 4 };
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = h[y * n + (x + 1) % n] - h[y * n + (x - 1 + n) % n];
                    float dy = h[((y + 1) % n) * n + x] - h[((y - 1 + n) % n) * n + x];
                    var v = new Vector3(-dx * strength, -dy * strength, 1f).normalized;
                    px[y * n + x] = new Color32((byte)(v.x * 127.5f + 127.5f), (byte)(v.y * 127.5f + 127.5f), (byte)(v.z * 127.5f + 127.5f), 255);
                }
            nt.SetPixels32(px);
            nt.Apply(true);
            return nt;
        }

        /// <summary>Soil grain: fbm with small pebble speckles, mean about 0.5 (so it brightens and darkens evenly).</summary>
        static void MakeGrain()
        {
            const int n = 256;
            var h = new float[n * n];
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float u = x / (float)n, v = y / (float)n;
                    float f = Fbm(u, v, 11);
                    float peb = TileNoise(u, v, 64, 5);
                    float pebble = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((peb - 0.78f) * 6f));
                    float val = Mathf.Clamp01(0.5f + (f - 0.5f) * 0.9f + pebble * 0.18f - Mathf.Clamp01((0.25f - f) * 2f) * 0.1f);
                    h[y * n + x] = f + pebble * 0.6f;
                    byte b = (byte)(val * 255f);
                    px[y * n + x] = new Color32(b, b, b, 255);
                }
            _grain = new Texture2D(n, n, TextureFormat.RGBA32, true, true) { name = "Grain", wrapMode = TextureWrapMode.Repeat, anisoLevel = 4 };
            _grain.SetPixels32(px);
            _grain.Apply(true);
            _grainNormal = NormalFrom(h, n, 6f, "GrainNormal");
        }

        static Texture2D Ripples
        {
            get
            {
                if (_ripples != null) return _ripples;
                const int n = 256;
                var h = new float[n * n];
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        float u = x / (float)n, v = y / (float)n;
                        // Stretched across the flow a little, like wind ripples on a moving river.
                        h[y * n + x] = Fbm(u, v, 31) * 0.7f + TileNoise(u, v, 16, 9) * 0.3f;
                    }
                _ripples = NormalFrom(h, n, 3.5f, "Ripples");
                return _ripples;
            }
        }

        /// <summary>The valley floor material: painted map plus close-up grain (KW_Ground), or plain if missing.</summary>
        public static Material GroundMat(Texture2D painted)
        {
            var src = Resources.Load<Material>("KW_Ground");
            if (src == null || src.shader == null || !src.shader.isSupported) return TexturedMat(painted, Color.white, 0.05f);
            if (_grain == null) MakeGrain();
            var m = new Material(src);
            m.SetTexture("_MainTex", painted);
            m.SetTexture("_Detail", _grain);
            m.SetTexture("_DetailNormal", _grainNormal);
            // 0.17 photoreal pass: scanned ground layers (Poly Haven, CC0) when they are in Resources/PBR.
            if (m.HasProperty("_HasScans") && Resources.Load<Texture2D>("PBR/grass_detail") != null)
            {
                foreach (var (prop, file) in new[] { ("_Grass", "grass"), ("_Forest", "forest"), ("_Rock", "rock"), ("_Ore", "ore"), ("_Mud", "mud") })
                {
                    m.SetTexture(prop + "D", Resources.Load<Texture2D>("PBR/" + file + "_detail"));
                    m.SetTexture(prop + "N", Resources.Load<Texture2D>("PBR/" + file + "_nor"));
                }
                m.SetColor("_RefGrass", Look.Grass.linear);
                m.SetColor("_RefForest", Look.ForestFloor.linear);
                m.SetColor("_RefRock", Look.StoneGround.linear);
                m.SetColor("_RefOre", Look.OreGround.linear);
                m.SetColor("_RefMud", new Color(0.45f, 0.33f, 0.22f).linear);
                m.SetFloat("_HasScans", 1f);
            }
            return m;
        }

        /// <summary>River material with moving ripples and sky reflections (KW_Water), or the old one if missing.</summary>
        public static Material WaterMat()
        {
            var src = Resources.Load<Material>("KW_Water");
            if (src == null || src.shader == null || !src.shader.isSupported) return TexturedMat(WaterTexture, Color.white, 0.85f);
            var m = new Material(src);
            m.SetTexture("_MainTex", WaterTexture);
            m.SetTexture("_Waves", Ripples);
            if (m.HasProperty("_Deep")) m.SetColor("_Deep", new Color(0.12f, 0.22f, 0.21f));
            if (m.HasProperty("_SkyGlow")) m.SetFloat("_SkyGlow", 0.11f);
            return m;
        }

        public static Material TexturedMat(Texture2D tex, Color tint, float gloss = 0.08f)
        {
            var m = new Material(Look.Mat(tint));
            m.mainTexture = tex;
            m.color = tint;
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", gloss);
            return m;
        }

        // ------------------------------------------------------------------ surface patterns (planks, bricks, stone, shingles)

        static readonly Dictionary<Look.Surface, Texture2D> _patterns = new Dictionary<Look.Surface, Texture2D>();

        /// <summary>How many times a pattern repeats per metre (planks ~0.15 m boards, bricks ~0.12 m courses).</summary>
        public static float PatternRepeats(Look.Surface s)
        {
            switch (s)
            {
                case Look.Surface.Wood: return 1f / 1.2f;
                case Look.Surface.Brick: return 1f / 1.0f;
                case Look.Surface.Stone: return 1f / 1.6f;
                case Look.Surface.Shingle: return 1f / 1.0f;
                default: return 1f;
            }
        }

        /// <summary>Grey-scale patterns (about 0.83 on average) that tint the material colour. Linear, tiling, 128 px.</summary>
        public static Texture2D PatternTexture(Look.Surface s)
        {
            if (_patterns.TryGetValue(s, out var t)) return t;
            const int n = 128;
            t = new Texture2D(n, n, TextureFormat.RGBA32, true, true) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear, anisoLevel = 4, name = "Pattern " + s };
            var px = new Color[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float v;
                    switch (s)
                    {
                        case Look.Surface.Wood:
                        {
                            // 8 boards; each board its own shade, long grain, a dark gap between boards, staggered joints.
                            int row = y / 16, ry = y % 16;
                            int joint = (row * 47) % n;
                            float shade = 0.78f + 0.14f * Hash(row, 3);
                            float grain = Mathf.PerlinNoise(x * 0.06f + row * 9f, y * 0.9f) * 0.16f;
                            v = shade + grain - 0.06f;
                            if (ry < 1) v = 0.42f;
                            else if (ry < 2) v *= 0.8f;
                            if (Mathf.Abs(((x - joint) % n + n) % n) < 1) v = 0.45f;
                            break;
                        }
                        case Look.Surface.Brick:
                        {
                            int row = y / 16, ry = y % 16;
                            int off = (row % 2) * 16;
                            int col = ((x + off) % n) / 32, rx = (x + off) % 32;
                            bool mortar = ry < 2 || rx < 2;
                            float shade = 0.72f + 0.2f * Hash(col, row, 5);
                            v = mortar ? 0.98f : shade + (Mathf.PerlinNoise(x * 0.3f, y * 0.3f) - 0.5f) * 0.08f;
                            break;
                        }
                        case Look.Surface.Stone:
                        {
                            // Three courses of uneven blocks.
                            int row = y * 3 / n, ry = y - row * n / 3;
                            int off = (int)(Hash(row, 9) * 40f);
                            int bw = 34 + (int)(Hash(row, 10) * 16f);
                            int col = ((x + off) % n) / bw, rx = ((x + off) % n) % bw;
                            bool mortar = ry < 3 || rx < 3;
                            float shade = 0.74f + 0.2f * Hash(col, row, 11);
                            float rough = (Mathf.PerlinNoise(x * 0.12f, y * 0.12f) - 0.5f) * 0.14f;
                            v = mortar ? 0.5f : shade + rough;
                            if (!mortar && (ry < 5 || rx < 5)) v *= 0.9f;   // soft edge shading
                            break;
                        }
                        case Look.Surface.Weathered:
                            // Plain painted / bare parts: almost flat, the shader adds the wear.
                            v = 0.83f + (Mathf.PerlinNoise(x * 0.05f, y * 0.05f) - 0.5f) * 0.06f;
                            break;
                        case Look.Surface.Shingle:
                        {
                            int row = y / 12, ry = y % 12;
                            int off = (row % 2) * 8;
                            int col = ((x + off) % n) / 16, rx = (x + off) % 16;
                            float shade = 0.72f + 0.2f * Hash(col, row, 13);
                            v = shade * (0.82f + 0.18f * ry / 11f);   // each row darker at its lower edge
                            if (rx < 1) v *= 0.7f;
                            break;
                        }
                        default: v = 0.83f; break;
                    }
                    px[y * n + x] = new Color(v, v, v, 1f);
                }
            t.SetPixels(px);
            t.Apply(true);
            _patterns[s] = t;
            return t;
        }

        // ------------------------------------------------------------------ the valley floor

        static Color GroundColor(Terrain t)
        {
            switch (t)
            {
                case Terrain.Forest: return new Color(0.24f, 0.3f, 0.15f);
                case Terrain.IronOre: return new Color(0.46f, 0.28f, 0.2f);
                case Terrain.Stone: return new Color(0.58f, 0.57f, 0.53f);
                case Terrain.Water: return new Color(0.2f, 0.27f, 0.22f);   // riverbed / bank under the water
                default: return new Color(0.38f, 0.5f, 0.24f);
            }
        }

        /// <summary>Paints the whole map as one texture: grass with soft patches, ore and stone fields that fade in
        /// at their edges, darker forest floor, and the riverbank. 16 pixels per tile.</summary>
        public static Texture2D PaintGround(World w)
        {
            const int ppt = 16;
            int size = w.W * ppt;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear, anisoLevel = 8, name = "ValleyFloor" };
            var px = new Color32[size * size];
            for (int py = 0; py < size; py++)
                for (int pxl = 0; pxl < size; pxl++)
                    px[py * size + pxl] = GroundPixel(w, pxl, py);
            tex.SetPixels32(px);
            tex.Apply(true);
            return tex;
        }

        /// <summary>One pixel of the untouched valley floor (16 pixels per tile).</summary>
        public static Color32 GroundPixel(World w, int pxl, int py)
        {
            const int ppt = 16;
            float fy = (py + 0.5f) / ppt - 0.5f;
            int ty = Mathf.FloorToInt(fy);
            float wy = fy - ty;
            float fx = (pxl + 0.5f) / ppt - 0.5f;
            int tx = Mathf.FloorToInt(fx);
            float wx = fx - tx;
            // Bilinear blend between the 4 nearest tile colours, warped by noise so edges look natural.
            float warp = (Mathf.PerlinNoise(pxl * 0.05f, py * 0.05f) - 0.5f) * 0.7f;
            float bx = Mathf.Clamp01(Smooth(wx + warp)), by = Mathf.Clamp01(Smooth(wy - warp));
            var c00 = TileCol(w, tx, ty); var c10 = TileCol(w, tx + 1, ty);
            var c01 = TileCol(w, tx, ty + 1); var c11 = TileCol(w, tx + 1, ty + 1);
            var c = Color.Lerp(Color.Lerp(c00, c10, bx), Color.Lerp(c01, c11, bx), by);
            float n1 = Mathf.PerlinNoise(pxl * 0.012f + 7f, py * 0.012f + 3f);
            float n2 = Mathf.PerlinNoise(pxl * 0.21f, py * 0.21f);
            c *= 0.86f + 0.22f * n1;
            c *= 0.94f + 0.1f * n2;
            c.a = 1f;
            return c;
        }

        static float Smooth(float t) => t * t * (3f - 2f * t);

        static Color TileCol(World w, int x, int y)
        {
            x = Mathf.Clamp(x, 0, w.W - 1); y = Mathf.Clamp(y, 0, w.H - 1);
            int i = y * w.W + x;
            var c = GroundColor(w.Ground[i]);
            // The coal seam shows through as darker, sooty ground (the map label names it too).
            if (w.CoalSeam[i]) c = Color.Lerp(c, new Color(0.2f, 0.2f, 0.19f), 0.45f);
            return c;
        }

        /// <summary>A flat plane the size of the map, with the painted texture on it.</summary>
        public static GameObject GroundPlane(Transform parent, World w, float tileSize)
        {
            float s = w.W * tileSize;
            var go = new GameObject("Valley floor");
            go.transform.SetParent(parent, false);
            var m = new Mesh { name = "ValleyFloor" };
            m.vertices = new[] { new Vector3(0f, 0f, 0f), new Vector3(s, 0f, 0f), new Vector3(s, 0f, s), new Vector3(0f, 0f, s) };
            m.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f) };
            m.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            m.RecalculateNormals();
            m.RecalculateTangents();
            m.RecalculateBounds();
            go.AddComponent<MeshFilter>().sharedMesh = m;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = GroundMat(PaintGround(w));
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }

        /// <summary>Rolling hills around the valley (outside the playable map), with the river running out both ends.</summary>
        public static GameObject Hills(Transform parent, float mapSize, float riverX)
        {
            const int n = 120;
            float lo = -mapSize * 1.2f, hi = mapSize * 2.2f, step = (hi - lo) / n;
            var verts = new Vector3[(n + 1) * (n + 1)];
            var uv = new Vector2[verts.Length];
            for (int j = 0; j <= n; j++)
                for (int i = 0; i <= n; i++)
                {
                    float x = lo + i * step, z = lo + j * step;
                    float ox = Mathf.Max(0f, Mathf.Max(-x, x - mapSize)), oz = Mathf.Max(0f, Mathf.Max(-z, z - mapSize));
                    float outside = Mathf.Sqrt(ox * ox + oz * oz);
                    float rise = Mathf.Clamp01((outside - 4f) / 60f);
                    float hgt = rise * rise * (18f + 26f * Mathf.PerlinNoise(x * 0.012f + 50f, z * 0.012f + 20f))
                                + rise * 4f * Mathf.PerlinNoise(x * 0.05f, z * 0.05f);
                    // Keep a valley floor along the river where it leaves the map north and south.
                    float riverDist = Mathf.Abs(x - riverX);
                    hgt *= Mathf.Clamp01((riverDist - 6f) / 26f);
                    // Inside the map the valley floor mesh is the ground; drop the hills well below it so dug pits show.
                    float inset = Mathf.Min(Mathf.Min(x, mapSize - x), Mathf.Min(z, mapSize - z));
                    float y = inset >= step ? -30f : hgt - 0.06f;
                    verts[j * (n + 1) + i] = new Vector3(x, y, z);
                    uv[j * (n + 1) + i] = new Vector2(x / 14f, z / 14f);
                }
            var tris = new int[n * n * 6];
            int k = 0;
            for (int j = 0; j < n; j++)
                for (int i = 0; i < n; i++)
                {
                    int a = j * (n + 1) + i, b = a + 1, c = a + n + 1, d = c + 1;
                    tris[k++] = a; tris[k++] = c; tris[k++] = b;
                    tris[k++] = b; tris[k++] = c; tris[k++] = d;
                }
            var m = new Mesh { name = "Hills", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            m.vertices = verts; m.uv = uv; m.triangles = tris;
            m.RecalculateNormals();
            m.RecalculateBounds();
            var go = new GameObject("Hills");
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = m;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = TexturedMat(GrassTexture, Color.white, 0.04f);
            go.AddComponent<HillHeight>().Init(verts, n, lo, step);
            return go;
        }

        // ------------------------------------------------------------------ trees and rocks

        public static readonly Color[] PineGreens = { new Color(0.12f, 0.3f, 0.17f), new Color(0.15f, 0.34f, 0.19f), new Color(0.1f, 0.26f, 0.16f) };
        public static readonly Color[] LeafGreens = { new Color(0.26f, 0.45f, 0.18f), new Color(0.32f, 0.5f, 0.2f), new Color(0.22f, 0.4f, 0.17f), new Color(0.45f, 0.5f, 0.2f) };
        public static readonly Color TrunkBrown = new Color(0.33f, 0.23f, 0.15f);

        /// <summary>One tree under `parent` at local origin. `r` in 0..1 picks the kind, size and tint.</summary>
        public static void Tree(Transform parent, float r, float r2)
        {
            if (Trees.Available) { Trees.Tree(parent, r, r2); return; }
            float s = 0.85f + r2 * 0.45f;
            if (r < 0.62f)
            {
                // Pine: trunk + three stacked cones.
                var g = PineGreens[(int)(r2 * 2.99f)];
                Cyl("Trunk", parent, Vector3.zero, 0.28f * s, 1.3f * s, TrunkBrown);
                ConeAt("Needles 1", parent, new Vector3(0f, 0.8f * s, 0f), 2.3f * s, 2.4f * s, g);
                ConeAt("Needles 2", parent, new Vector3(0f, 2.0f * s, 0f), 1.8f * s, 2.1f * s, g * 1.06f);
                ConeAt("Needles 3", parent, new Vector3(0f, 3.1f * s, 0f), 1.2f * s, 1.9f * s, g * 1.12f);
            }
            else
            {
                // Leafy tree: trunk + two or three round crowns.
                var g = LeafGreens[(int)(r2 * 3.99f)];
                Cyl("Trunk", parent, Vector3.zero, 0.32f * s, 2.2f * s, TrunkBrown);
                Ball("Crown", parent, new Vector3(0f, 2.9f * s, 0f), new Vector3(2.4f, 2.0f, 2.4f) * s, g);
                Ball("Crown 2", parent, new Vector3(0.6f * s, 2.4f * s, 0.3f * s), new Vector3(1.6f, 1.4f, 1.6f) * s, g * 0.92f);
                if (r > 0.8f) Ball("Crown 3", parent, new Vector3(-0.5f * s, 2.5f * s, -0.4f * s), new Vector3(1.5f, 1.3f, 1.5f) * s, g * 1.05f);
            }
        }

        /// <summary>Cheap distant tree for the hills: one or two shapes, no shadow.</summary>
        public static void FarTree(Transform parent, float r, float r2)
        {
            if (Trees.Available) { Trees.Tree(parent, r, r2, far: true); return; }
            float s = 0.85f + r2 * 0.45f;
            if (r < 0.7f)
            {
                var g = PineGreens[(int)(r2 * 2.99f)];
                Part("Pine", parent, Cone, new Vector3(0f, 0.3f * s, 0f), new Vector3(2.4f, 5.0f, 2.4f) * s, g, default, false);
            }
            else
            {
                var g = LeafGreens[(int)(r2 * 3.99f)];
                Part("Crown", parent, Prim(PrimitiveType.Sphere), new Vector3(0f, 2.4f * s, 0f), new Vector3(3f, 3.4f, 3f) * s, g, default, false);
            }
        }

        public static float Hash(int x, int y, int salt = 0)
        {
            unchecked
            {
                uint h = (uint)(x * 73856093) ^ (uint)(y * 19349663) ^ (uint)(salt * 83492791);
                h ^= h >> 13; h *= 0x5bd1e995; h ^= h >> 15;
                return (h & 0xFFFFFF) / (float)0x1000000;
            }
        }

        // ------------------------------------------------------------------ smoke

        static Material _smokeMat;

        public static ParticleSystem Smoke(Transform parent, Vector3 pos, float amount = 1f)
        {
            var go = new GameObject("Smoke");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.duration = 5f;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(3.5f, 5.5f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.6f, 1.1f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.9f, 1.5f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = new Color(0.9f, 0.9f, 0.9f, 1f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 80;
            main.gravityModifier = -0.02f;
            var em = ps.emission;
            em.rateOverTime = 5f * amount;
            var sh = ps.shape;
            sh.shapeType = ParticleSystemShapeType.Cone;
            sh.angle = 10f;
            sh.radius = 0.15f;
            var sol = ps.sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 2.6f));
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(new[] { new GradientColorKey(new Color(0.62f, 0.6f, 0.58f), 0f), new GradientColorKey(new Color(0.92f, 0.92f, 0.92f), 1f) },
                         new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.85f, 0.1f), new GradientAlphaKey(0.45f, 0.55f), new GradientAlphaKey(0f, 1f) });
            col.color = grad;
            var vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.World;
            vel.x = new ParticleSystem.MinMaxCurve(0.25f, 0.5f);   // a light breeze
            vel.y = new ParticleSystem.MinMaxCurve(0f, 0f);
            vel.z = new ParticleSystem.MinMaxCurve(0.05f, 0.15f);
            var rend = go.GetComponent<ParticleSystemRenderer>();
            rend.sharedMaterial = SmokeMat;
            rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            rend.receiveShadows = false;
            ps.Play();
            return ps;
        }

        /// <summary>Multiplies the smoke tint by the light level (unlit particles would glow white at night).</summary>
        public static void ShadeSmoke(Color light)
        {
            var m = SmokeMat;
            if (m == null || !m.HasProperty("_TintColor")) return;
            var c = new Color(0.73f * light.r, 0.73f * light.g, 0.73f * light.b, 0.5f);
            if (m.GetColor("_TintColor") != c) m.SetColor("_TintColor", c);
        }

        static Material SmokeMat
        {
            get
            {
                if (_smokeMat != null) return _smokeMat;
                var baseMat = Resources.Load<Material>("KW_Smoke");
                if (baseMat != null) _smokeMat = new Material(baseMat);
                else
                {
                    var sh = Shader.Find("Legacy Shaders/Particles/Alpha Blended") ?? Shader.Find("Particles/Standard Unlit") ?? Shader.Find("Sprites/Default");
                    _smokeMat = new Material(sh);
                }
                _smokeMat.mainTexture = SmokeTexture;
                // Legacy particle shaders double the tint; in linear colour space 0.5 grey would come out dark, so use ~0.73.
                if (_smokeMat.HasProperty("_TintColor")) _smokeMat.SetColor("_TintColor", new Color(0.73f, 0.73f, 0.73f, 0.5f));
                return _smokeMat;
            }
        }
    }

    /// <summary>Remembers the hill heights so far trees can sit on the slopes.</summary>
    public class HillHeight : MonoBehaviour
    {
        Vector3[] _v;
        int _n;
        float _lo, _step;

        public void Init(Vector3[] v, int n, float lo, float step) { _v = v; _n = n; _lo = lo; _step = step; }

        public float At(float x, float z)
        {
            float fi = (x - _lo) / _step, fj = (z - _lo) / _step;
            int i = Mathf.Clamp(Mathf.FloorToInt(fi), 0, _n - 1), j = Mathf.Clamp(Mathf.FloorToInt(fj), 0, _n - 1);
            float u = Mathf.Clamp01(fi - i), w = Mathf.Clamp01(fj - j);
            float a = _v[j * (_n + 1) + i].y, b = _v[j * (_n + 1) + i + 1].y, c = _v[(j + 1) * (_n + 1) + i].y, d = _v[(j + 1) * (_n + 1) + i + 1].y;
            return Mathf.Lerp(Mathf.Lerp(a, b, u), Mathf.Lerp(c, d, u), w);
        }
    }
}
