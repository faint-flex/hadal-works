using System.Collections.Generic;
using Kilnwood.Sim;
using UnityEngine;
using Terrain = Kilnwood.Sim.Terrain;

namespace Kilnwood.Game
{
    /// <summary>
    /// The valley floor as a height-mapped mesh (two vertices per tile each way), painted with the ground texture.
    /// Dug tiles sink and filled tiles rise; each tile's centre sits at its own height and its edges take the
    /// neighbour nearest the surface, so pit walls slope down inside the dug tiles and the rim stays flat.
    /// </summary>
    public class GroundView
    {
        const float LevelMetres = 1f;
        const int PixelsPerTile = 16;

        readonly World _w;
        readonly int _n;                 // vertices per side
        readonly float _step;            // metres between vertices
        readonly Vector3[] _verts;
        readonly Mesh _mesh;
        readonly Texture2D _tex;
        readonly Color32[] _px;
        bool _texDirty;

        public GameObject Go { get; }

        public GroundView(Transform parent, World w)
        {
            _w = w;
            _n = w.W * 2 + 1;
            _step = WorldView.T / 2f;
            _verts = new Vector3[_n * _n];
            var uv = new Vector2[_verts.Length];
            float size = w.W * WorldView.T;
            for (int j = 0; j < _n; j++)
                for (int i = 0; i < _n; i++)
                {
                    _verts[j * _n + i] = new Vector3(i * _step, VertexHeight(i, j), j * _step);
                    uv[j * _n + i] = new Vector2(i * _step / size, j * _step / size);
                }
            var tris = new int[(_n - 1) * (_n - 1) * 6];
            int k = 0;
            for (int j = 0; j < _n - 1; j++)
                for (int i = 0; i < _n - 1; i++)
                {
                    int a = j * _n + i, b = a + 1, c = a + _n, d = c + 1;
                    tris[k++] = a; tris[k++] = c; tris[k++] = b;
                    tris[k++] = b; tris[k++] = c; tris[k++] = d;
                }
            _mesh = new Mesh { name = "Valley floor", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            _mesh.MarkDynamic();
            _mesh.vertices = _verts;
            _mesh.uv = uv;
            _mesh.triangles = tris;
            _mesh.RecalculateNormals();
            _mesh.RecalculateTangents();
            _mesh.RecalculateBounds();

            _tex = Art.PaintGround(w);
            _px = _tex.GetPixels32();
            Go = new GameObject("Valley floor");
            Go.transform.SetParent(parent, false);
            Go.AddComponent<MeshFilter>().sharedMesh = _mesh;
            var r = Go.AddComponent<MeshRenderer>();
            r.sharedMaterial = Art.GroundMat(_tex);
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            r.receiveShadows = true;

            // Ground that was already dug in a save.
            for (int t = 0; t < w.Units.Length; t++) if (w.Units[t] != 0) RepaintTile(t);
            _tex.SetPixels32(_px);
            _tex.Apply(true);
        }

        float TileHeight(int x, int y)
        {
            x = Mathf.Clamp(x, 0, _w.W - 1);
            y = Mathf.Clamp(y, 0, _w.H - 1);
            return _w.Units[y * _w.W + x] / (float)_w.B.UnitsPerLevel * LevelMetres;
        }

        /// <summary>Vertex (i, j) on the half-tile grid. Odd/odd = a tile centre; others are shared by 2 or 4 tiles.</summary>
        float VertexHeight(int i, int j)
        {
            bool cx = (i & 1) == 1, cy = (j & 1) == 1;
            int tx0 = cx ? i / 2 : i / 2 - 1, tx1 = i / 2;
            int ty0 = cy ? j / 2 : j / 2 - 1, ty1 = j / 2;
            float best = float.MaxValue;
            for (int ty = ty0; ty <= ty1; ty++)
                for (int tx = tx0; tx <= tx1; tx++)
                {
                    float h = TileHeight(tx, ty);
                    if (Mathf.Abs(h) < Mathf.Abs(best)) best = h;
                }
            return best;
        }

        /// <summary>Ground height in metres at a world position (bilinear over the mesh grid).</summary>
        public float HeightAt(float wx, float wz)
        {
            float fi = Mathf.Clamp(wx / _step, 0f, _n - 1.001f), fj = Mathf.Clamp(wz / _step, 0f, _n - 1.001f);
            int i = (int)fi, j = (int)fj;
            float u = fi - i, v = fj - j;
            float a = _verts[j * _n + i].y, b = _verts[j * _n + i + 1].y, c = _verts[(j + 1) * _n + i].y, d = _verts[(j + 1) * _n + i + 1].y;
            return Mathf.Lerp(Mathf.Lerp(a, b, u), Mathf.Lerp(c, d, u), v);
        }

        /// <summary>Re-shapes and re-paints the tiles that changed since last frame.</summary>
        public void Refresh(ICollection<int> changed)
        {
            if (changed.Count == 0) return;
            foreach (int t in changed)
            {
                int tx = t % _w.W, ty = t / _w.W;
                for (int j = ty * 2 - 1; j <= ty * 2 + 3; j++)
                    for (int i = tx * 2 - 1; i <= tx * 2 + 3; i++)
                    {
                        if (i < 0 || j < 0 || i >= _n || j >= _n) continue;
                        _verts[j * _n + i].y = VertexHeight(i, j);
                    }
                RepaintTile(t);
            }
            _mesh.vertices = _verts;
            _mesh.RecalculateNormals();
            _mesh.RecalculateTangents();
            _mesh.RecalculateBounds();
            _texDirty = true;
        }

        /// <summary>Uploads the repainted ground at most a few times a second (a full texture upload is not free).</summary>
        float _nextUpload;
        public void Upload()
        {
            if (!_texDirty || Time.unscaledTime < _nextUpload) return;
            _tex.SetPixels32(_px);
            _tex.Apply(true);
            _texDirty = false;
            _nextUpload = Time.unscaledTime + 0.25f;
        }

        static readonly Color Dirt = new Color(0.45f, 0.33f, 0.22f);
        static readonly Color DugStone = new Color(0.56f, 0.55f, 0.51f);
        static readonly Color DugOre = new Color(0.52f, 0.29f, 0.19f);
        static readonly Color DugCoal = new Color(0.12f, 0.12f, 0.13f);

        /// <summary>Colour of a worked tile: what's showing at its surface, darker the deeper it goes.</summary>
        Color WorkedColor(int t)
        {
            int u = _w.Units[t];
            if (u > 0) return Dirt * (1f + 0.04f * u / _w.B.UnitsPerLevel);
            var showing = _w.MaterialAt(t);
            if (showing == Item.None)
            {
                // At the bottom: what the last layer was made of.
                var g = _w.Ground[t];
                showing = g == Terrain.IronOre ? Item.Ore : _w.CoalSeam[t] ? Item.Coal : Item.Stone;
            }
            Color c = showing == Item.Ore ? DugOre : showing == Item.Coal ? DugCoal : showing == Item.Stone ? DugStone : Dirt;
            float depth = -u / (float)_w.B.UnitsPerLevel;
            return c * (1f - 0.07f * depth);
        }

        void RepaintTile(int t)
        {
            int tx = t % _w.W, ty = t / _w.W;
            int size = _w.W * PixelsPerTile;
            bool worked = _w.Units[t] != 0;
            Color baseCol = worked ? WorkedColor(t) : Color.clear;
            for (int py = ty * PixelsPerTile; py < (ty + 1) * PixelsPerTile; py++)
                for (int px = tx * PixelsPerTile; px < (tx + 1) * PixelsPerTile; px++)
                {
                    if (!worked)
                    {
                        // Back to untouched ground (filled to exactly 0): repaint the original colour.
                        _px[py * size + px] = Art.GroundPixel(_w, px, py);
                        continue;
                    }
                    float n1 = Mathf.PerlinNoise(px * 0.19f, py * 0.19f);
                    float n2 = Mathf.PerlinNoise(px * 0.05f + 31f, py * 0.05f + 17f);
                    // Terrace lines: a faint darker band every few pixels so the layers read as cut steps.
                    float band = ((px + py) / 5) % 3 == 0 ? 0.93f : 1f;
                    var c = baseCol * (0.86f + 0.2f * n1) * (0.94f + 0.1f * n2) * band;
                    c.a = 1f;
                    _px[py * size + px] = c;
                }
        }
    }
}
