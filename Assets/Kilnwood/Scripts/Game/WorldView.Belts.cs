using System.Collections.Generic;
using Kilnwood.Sim;
using UnityEngine;

namespace Kilnwood.Game
{
    /// <summary>Belt shapes (view only; the rules still treat every belt as one straight tile).
    /// A belt fed from exactly one side and not from behind draws as a smooth quarter curve,
    /// and items on it ride around the bend.</summary>
    public partial class WorldView
    {
        // Shape keys, kept in BView.RailMask for belts.
        const int BeltStraight = 0, BeltFromLeft = 1, BeltFromRight = 2;
        const float BeltItemInset = 0.15f;   // items start / end this far in from the tile edge (same as straight belts)
        static readonly Mesh[] _cornerDeck = new Mesh[2];
        bool _beltShapesDirty = true;

        /// <summary>Builds the belt model for `shape` under a fresh "Shape" child of the body (the old one is removed).</summary>
        void BuildBeltShape(BView v, int shape)
        {
            if (v.Shape != null) Destroy(v.Shape.gameObject);
            v.Shape = new GameObject("Shape").transform;
            v.Shape.SetParent(v.Body, false);
            v.RailMask = shape;
            var B = v.Shape;
            if (shape == BeltStraight)
            {
                Art.Cube("Rail L", B, new Vector3(-0.72f, 0.3f, 0f), new Vector3(0.14f, 0.24f, T), Look.BeltFrame);
                Art.Cube("Rail R", B, new Vector3(0.72f, 0.3f, 0f), new Vector3(0.14f, 0.24f, T), Look.BeltFrame);
                Art.Cube("Cross back", B, new Vector3(0f, 0.12f, -0.6f), new Vector3(1.4f, 0.16f, 0.18f), Look.MineDark);
                Art.Cube("Cross front", B, new Vector3(0f, 0.12f, 0.6f), new Vector3(1.4f, 0.16f, 0.18f), Look.MineDark);
                BeltDeck(B, new Vector3(0f, 0.38f, 0f), new Vector3(1.32f, 1f, T));
                // Arrow heads on the rails: direction reads even when the belt is still (and in screenshots).
                Art.Arrow("Arrow L", B, new Vector3(-0.72f, 0.43f, 0.3f), 0.3f, 0.45f, Look.Accent);
                Art.Arrow("Arrow R", B, new Vector3(0.72f, 0.43f, 0.3f), 0.3f, 0.45f, Look.Accent);
                return;
            }

            // Corner: quarter circle of radius T/2 around the tile corner between the entry side and the front.
            // s = +1: enters from the left edge, turns left to the front; s = -1: mirrored (enters from the right).
            float s = shape == BeltFromLeft ? 1f : -1f;
            var deck = Art.Part("Deck", B, CornerDeck(shape), new Vector3(0f, 0.38f, 0f), Vector3.one, Look.Belt, default, false);
            deck.GetComponent<Renderer>().sharedMaterial = _beltMat;
            CurvedRail("Rail outer", B, s, 0.72f + T / 2f, 6);
            CurvedRail("Rail inner", B, s, T / 2f - 0.72f, 2);
            for (int k = 0; k < 2; k++)
            {
                float phi = k == 0 ? 0.4f : Mathf.PI / 2f - 0.4f;
                Art.Cube("Cross", B, CornerPoint(s, T / 2f, phi, 0.12f), new Vector3(1.4f, 0.16f, 0.18f), Look.MineDark, new Vector3(0f, CornerYaw(s, phi), 0f));
            }
            const float mid = Mathf.PI / 4f;
            Art.Arrow("Arrow outer", B, CornerPoint(s, 0.72f + T / 2f, mid, 0.43f), 0.3f, 0.45f, Look.Accent, CornerYaw(s, mid));
            Art.Arrow("Arrow inner", B, CornerPoint(s, T / 2f - 0.72f, mid, 0.43f), 0.3f, 0.45f, Look.Accent, CornerYaw(s, mid));
        }

        /// <summary>Point on the corner arc of radius r at angle phi (0 = entry edge, pi/2 = front edge), in belt-local space.</summary>
        static Vector3 CornerPoint(float s, float r, float phi, float y) =>
            new Vector3(s * (r * Mathf.Sin(phi) - T / 2f), y, T / 2f - r * Mathf.Cos(phi));

        /// <summary>Yaw (degrees) that turns local +z along the direction of travel at angle phi.</summary>
        static float CornerYaw(float s, float phi) => s * (90f - phi * Mathf.Rad2Deg);

        static void CurvedRail(string name, Transform parent, float s, float r, int segs)
        {
            float step = Mathf.PI / 2f / segs;
            float len = r * step + 0.04f;   // a little overlap so the segments read as one rail
            for (int i = 0; i < segs; i++)
            {
                float phi = (i + 0.5f) * step;
                Art.Cube(name, parent, CornerPoint(s, r, phi, 0.3f), new Vector3(0.14f, 0.24f, len), Look.BeltFrame, new Vector3(0f, CornerYaw(s, phi), 0f));
            }
        }

        /// <summary>Quarter-annulus deck (same width as the straight deck). v runs along the curve (0 at entry, 1 at exit)
        /// so the scrolling belt texture follows the bend; u is 0 on the left of travel, as on the straight quad.</summary>
        static Mesh CornerDeck(int shape)
        {
            int idx = shape == BeltFromLeft ? 0 : 1;
            if (_cornerDeck[idx] != null) return _cornerDeck[idx];
            float s = shape == BeltFromLeft ? 1f : -1f;
            const int segs = 12;
            const float half = 0.66f;
            float rIn = T / 2f - half, rOut = T / 2f + half;
            // Inner edge (towards the arc centre) is the left of travel when turning left (s = +1), the right otherwise.
            float uIn = s > 0f ? 0f : 1f, uOut = 1f - uIn;
            var verts = new Vector3[(segs + 1) * 2];
            var uvs = new Vector2[verts.Length];
            var normals = new Vector3[verts.Length];
            for (int i = 0; i <= segs; i++)
            {
                float t = (float)i / segs, phi = t * Mathf.PI / 2f;
                verts[i * 2] = CornerPoint(s, rIn, phi, 0f);
                verts[i * 2 + 1] = CornerPoint(s, rOut, phi, 0f);
                uvs[i * 2] = new Vector2(uIn, t);
                uvs[i * 2 + 1] = new Vector2(uOut, t);
                normals[i * 2] = Vector3.up;
                normals[i * 2 + 1] = Vector3.up;
            }
            var tris = new List<int>(segs * 6);
            for (int i = 0; i < segs; i++)
            {
                int a = i * 2, b = a + 1, c = a + 2, d = a + 3;
                UpTri(verts, tris, a, c, b);
                UpTri(verts, tris, b, c, d);
            }
            var m = new Mesh { name = shape == BeltFromLeft ? "BeltCornerL" : "BeltCornerR" };
            m.vertices = verts;
            m.uv = uvs;
            m.normals = normals;
            m.triangles = tris.ToArray();
            m.RecalculateBounds();
            _cornerDeck[idx] = m;
            return m;
        }

        /// <summary>Adds a triangle wound so its front face points up (clockwise seen from above, as in Art.BeltQuad).</summary>
        static void UpTri(Vector3[] v, List<int> tris, int a, int b, int c)
        {
            float area = (v[b].x - v[a].x) * (v[c].z - v[a].z) - (v[b].z - v[a].z) * (v[c].x - v[a].x);
            if (area > 0f) { int tmp = b; b = c; c = tmp; }
            tris.Add(a); tris.Add(b); tris.Add(c);
        }

        /// <summary>Re-checks every belt's shape after something was placed, removed or rotated; rebuilds only the ones that changed.</summary>
        void RefreshBeltShapes()
        {
            foreach (var b in _w.Buildings.Values)
            {
                if (b.Type != BuildingType.Belt || !_views.TryGetValue(b.Id, out var v)) continue;
                int shape = BeltShapeOf(b);
                if (shape == v.RailMask) continue;
                BuildBeltShape(v, shape);
                Texturize(v.Shape);
            }
        }

        /// <summary>Corner when exactly one belt or splitter feeds it from the left or right and nothing feeds it from behind.</summary>
        int BeltShapeOf(Building b)
        {
            int f = b.Facing;
            if (FeedsInto(b, Dir.Opposite(f))) return BeltStraight;
            bool left = FeedsInto(b, (f + 3) & 3), right = FeedsInto(b, (f + 1) & 3);
            if (left == right) return BeltStraight;
            return left ? BeltFromLeft : BeltFromRight;
        }

        /// <summary>Does the belt or splitter on side `side` of b push items into b?</summary>
        bool FeedsInto(Building b, int side)
        {
            var n = _w.At(b.X + Dir.DX[side], b.Y + Dir.DY[side]);
            if (n == null) return false;
            int toward = Dir.Opposite(side);   // direction from n to b
            if (n.Type == BuildingType.Belt) return n.Facing == toward;
            if (n.Type == BuildingType.Splitter) return toward != Dir.Opposite(n.Facing);   // splitters push front, right and left
            return false;
        }

        /// <summary>Where an item sits on a belt (and which way it points), following the curve on corners.</summary>
        void BeltItemPose(Building b, int shape, out Vector3 pos, out Quaternion rot)
        {
            var c = TileCenter(b.X, b.Y);
            if (shape != BeltFromLeft && shape != BeltFromRight)
            {
                pos = c + DirVec(b.Facing) * ((b.BeltProgress * 2f - 1f) * 0.85f) + new Vector3(0f, 0.4f, 0f);
                rot = FacingRot(b.Facing);
                return;
            }
            float s = shape == BeltFromLeft ? 1f : -1f;
            float r = T / 2f, phi0 = BeltItemInset / r;
            float phi = phi0 + Mathf.Clamp01(b.BeltProgress) * (Mathf.PI / 2f - 2f * phi0);
            var face = FacingRot(b.Facing);
            pos = c + face * CornerPoint(s, r, phi, 0.4f);
            rot = face * Quaternion.Euler(0f, CornerYaw(s, phi), 0f);
        }
    }
}
