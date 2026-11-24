using System.Collections.Generic;
using Kilnwood.Sim;
using UnityEngine;

namespace Kilnwood.Game
{
    /// <summary>0.8 Tier 4 "Rails" models: track on ballast, stations, and steam trains with wagons.</summary>
    public partial class WorldView
    {
        static readonly Color Ballast = new Color(0.46f, 0.44f, 0.4f);
        static readonly Color Sleeper = new Color(0.33f, 0.24f, 0.17f);
        static readonly Color RailSteel = new Color(0.5f, 0.5f, 0.52f);
        static readonly Color LocoGreen = new Color(0.16f, 0.3f, 0.22f);
        static readonly Color LocoRed = new Color(0.55f, 0.16f, 0.12f);
        int _railCount = -1;

        // ------------------------------------------------------------------ track

        /// <summary>Each rail tile draws a half-track toward every neighbouring rail (or the station it serves),
        /// so straights, corners and junctions join up. Rebuilt whenever the layout changes.</summary>
        void RefreshRails()
        {
            foreach (var b in _w.Buildings.Values)
            {
                if (b.Type != BuildingType.Rail || !_views.TryGetValue(b.Id, out var v)) continue;
                int mask = 0;
                for (int d = 0; d < 4; d++)
                {
                    var n = _w.At(b.X + Dir.DX[d], b.Y + Dir.DY[d]);
                    if (n != null && n.Type == BuildingType.Rail) mask |= 1 << d;
                }
                if (mask == v.RailMask) continue;   // only pieces whose neighbours changed are rebuilt
                v.RailMask = mask;
                var root = v.Root.transform;
                var old = root.Find("Track");
                if (old != null) Destroy(old.gameObject);
                var track = new GameObject("Track").transform;
                track.SetParent(root, false);
                int links = 0;
                for (int d = 0; d < 4; d++) if ((mask & (1 << d)) != 0) links++;
                if (links == 2 && mask != 0b0101 && mask != 0b1010)
                {
                    // A corner: one smooth quarter curve instead of two straights meeting in an L.
                    int d1 = -1, d2 = -1;
                    for (int d = 0; d < 4; d++) if ((mask & (1 << d)) != 0) { if (d1 < 0) d1 = d; else d2 = d; }
                    CurveTrack(track, d1, d2);
                }
                else if (links == 0) { HalfTrack(track, 0); HalfTrack(track, 2); }
                else if (links == 1)
                {
                    // Line end: carry it straight through and finish with a buffer stop.
                    for (int d = 0; d < 4; d++)
                        if ((mask & (1 << d)) != 0) { HalfTrack(track, d); HalfTrack(track, Dir.Opposite(d)); BufferStop(track, Dir.Opposite(d)); break; }
                }
                else for (int d = 0; d < 4; d++) if ((mask & (1 << d)) != 0) HalfTrack(track, d);
            }
        }

        /// <summary>Quarter circle of radius T/2 around the tile corner between directions d1 and d2,
        /// joining the middles of those two tile edges. Ballast, sleepers and both rails follow it.</summary>
        void CurveTrack(Transform parent, int d1, int d2)
        {
            var c = new Vector3((Dir.DX[d1] + Dir.DX[d2]) * T / 2f, 0f, (Dir.DY[d1] + Dir.DY[d2]) * T / 2f);
            var a = new Vector3(Dir.DX[d1] * T / 2f, 0f, Dir.DY[d1] * T / 2f) - c;   // from the corner to edge 1
            var b = new Vector3(Dir.DX[d2] * T / 2f, 0f, Dir.DY[d2] * T / 2f) - c;
            const int N = 6;
            Vector3 P(float t, float off)
            {
                // Point on the arc at t (0 = edge 1, 1 = edge 2), pushed `off` metres away from the corner.
                var dir = Vector3.Slerp(a.normalized, b.normalized, t);
                return c + dir * (T / 2f + off);
            }
            for (int i = 0; i < N; i++)
            {
                float t0 = i / (float)N, t1 = (i + 1) / (float)N, tm = (t0 + t1) / 2f;
                Seg(parent, "Ballast", P(t0, 0f), P(t1, 0f), 1.7f, 0.12f, 0.06f, Ballast, 0.08f);
                var sm = P(tm, 0f); var tan = (P(t1, 0f) - P(t0, 0f)).normalized;
                Art.Cube("Sleeper", parent, sm + Vector3.up * 0.15f, new Vector3(1.45f, 0.07f, 0.22f), Sleeper * (i % 2 == 0 ? 1f : 0.92f), Quaternion.LookRotation(tan).eulerAngles);
                Seg(parent, "Rail", P(t0, -0.5f), P(t1, -0.5f), 0.08f, 0.12f, 0.24f, RailSteel, 0.03f);
                Seg(parent, "Rail", P(t0, 0.5f), P(t1, 0.5f), 0.08f, 0.12f, 0.24f, RailSteel, 0.03f);
            }
        }

        /// <summary>A box from a to b (on the ground plane) at height y, a little longer so pieces overlap without gaps.</summary>
        static void Seg(Transform parent, string name, Vector3 a, Vector3 b, float width, float height, float y, Color col, float extra)
        {
            var d = b - a;
            float len = d.magnitude + extra;
            var mid = (a + b) / 2f + Vector3.up * y;
            Art.Cube(name, parent, mid, new Vector3(width, height, len), col, Quaternion.LookRotation(d.normalized).eulerAngles);
        }

        void HalfTrack(Transform parent, int d)
        {
            var seg = new GameObject("Half " + d).transform;
            seg.SetParent(parent, false);
            seg.localRotation = Quaternion.Euler(0f, d * 90f, 0f);   // local +z points along direction d
            Art.Cube("Ballast", seg, new Vector3(0f, 0.06f, T / 4f), new Vector3(1.7f, 0.12f, T / 2f + 0.02f), Ballast);
            for (int k = 0; k < 2; k++)
                Art.Cube("Sleeper", seg, new Vector3(0f, 0.15f, 0.25f + k * 0.5f), new Vector3(1.45f, 0.07f, 0.22f), Sleeper * (k == 0 ? 1f : 0.92f));
            for (int side = -1; side <= 1; side += 2)
                Art.Cube("Rail", seg, new Vector3(side * 0.5f, 0.24f, T / 4f), new Vector3(0.08f, 0.12f, T / 2f + 0.04f), RailSteel);
        }

        void BufferStop(Transform parent, int d)
        {
            var seg = new GameObject("Buffer stop").transform;
            seg.SetParent(parent, false);
            seg.localRotation = Quaternion.Euler(0f, d * 90f, 0f);
            Art.Cube("Beam", seg, new Vector3(0f, 0.55f, T / 2f - 0.15f), new Vector3(1.3f, 0.3f, 0.2f), LocoRed);
            for (int side = -1; side <= 1; side += 2)
                Art.Cube("Leg", seg, new Vector3(side * 0.5f, 0.35f, T / 2f - 0.3f), new Vector3(0.12f, 0.5f, 0.4f), Look.Iron, new Vector3(-30f, 0f, 0f));
        }

        // ------------------------------------------------------------------ station

        void MakeStation(Building b, Transform B, float s, BView v)
        {
            Color stone = Look.StoneWall, timber = Look.Timber;
            // Raised stone platform, a small brick office with a slate roof, a canopy on posts, a lamp and a bench.
            Art.Cube("Platform", B, new Vector3(0f, 0.35f, 0f), new Vector3(s - 0.2f, 0.7f, s - 0.2f), stone * 0.9f);
            Art.Cube("Platform edge", B, new Vector3(0f, 0.72f, 0f), new Vector3(s - 0.1f, 0.06f, s - 0.1f), new Color(0.85f, 0.82f, 0.7f));
            Art.Cube("Office", B, new Vector3(-0.9f, 1.75f, -0.8f), new Vector3(1.8f, 2.0f, 1.8f), Look.Brickwork);
            Art.RoofAt("Office roof", B, new Vector3(-0.9f, 2.75f, -0.8f), 2.2f, 2.2f, 0.9f, Look.Roof);
            Art.Cube("Door", B, new Vector3(-0.9f, 1.4f, 0.11f), new Vector3(0.6f, 1.2f, 0.05f), Look.Roof);
            Art.Cube("Window", B, new Vector3(-0.1f, 1.95f, -0.8f), new Vector3(0.05f, 0.6f, 0.7f), new Color(0.95f, 0.82f, 0.5f));
            for (int k = 0; k < 2; k++)
                Art.Cube("Canopy post", B, new Vector3(1.4f, 1.85f, -1.2f + k * 2.4f), new Vector3(0.14f, 2.3f, 0.14f), timber * 0.85f);
            Art.Cube("Canopy", B, new Vector3(0.95f, 3.05f, 0f), new Vector3(1.5f, 0.12f, s - 0.3f), Look.Roof, new Vector3(0f, 0f, -8f));
            Art.Cyl("Lamp post", B, new Vector3(1.5f, 0.7f, 1.6f), 0.1f, 2.0f, Look.Iron);
            Art.Cube("Lamp", B, new Vector3(1.5f, 2.8f, 1.6f), new Vector3(0.28f, 0.34f, 0.28f), new Color(1f, 0.85f, 0.55f));
            Art.Cube("Bench", B, new Vector3(0.2f, 0.95f, 1.3f), new Vector3(1.1f, 0.12f, 0.35f), timber);
            // Loading bay: a few crates and a sack pile show it holds goods.
            Art.Cube("Crate", B, new Vector3(0.4f, 1.0f, -1.3f), new Vector3(0.6f, 0.55f, 0.6f), timber * 1.1f);
            Art.Cube("Crate", B, new Vector3(0.9f, 0.95f, -1.45f), new Vector3(0.45f, 0.45f, 0.45f), timber * 0.95f);
            Front(B, s / 2f);
        }

        // ------------------------------------------------------------------ trains

        class TView
        {
            public Transform Loco;
            public Transform[] Wagons;
            public Transform[] Heaps;
            public TextMesh[] Letters;
            public Transform[] Wheels;
            public ParticleSystem Smoke;
            public readonly List<Vector3> Trail = new List<Vector3>();
            public float Yaw;
            public bool LocoLeads = true;
        }

        readonly Dictionary<int, TView> _tviews = new Dictionary<int, TView>();
        const float CarGap = 3.2f;   // metres between car centres

        void UpdateTrains()
        {
            foreach (var v in _w.Vehicles)
                if (v.Type == VehicleType.Train && !_tviews.ContainsKey(v.Id)) _tviews[v.Id] = MakeTrain();
            List<int> gone = null;
            foreach (var kv in _tviews) if (_w.VehicleById(kv.Key) == null) (gone ??= new List<int>()).Add(kv.Key);
            if (gone != null) foreach (int id in gone) { var tv = _tviews[id]; Destroy(tv.Loco.gameObject); foreach (var w in tv.Wagons) Destroy(w.gameObject); _tviews.Remove(id); }

            float dt = Time.deltaTime;
            foreach (var v in _w.Vehicles)
            {
                if (v.Type != VehicleType.Train) continue;
                var tv = _tviews[v.Id];
                var pos = new Vector3(v.X * T, GroundHeight(v.X * T, v.Y * T) + 0.28f, v.Y * T);   // up on the rails
                if (tv.LocoLeads != v.LocoLeads)
                {
                    // The train reversed out of a stop: its old last wagon is the new front. The track it now has
                    // behind it is the part of the trail between that point and where the old front stood.
                    tv.LocoLeads = v.LocoLeads;
                    int near = 0; float best = float.MaxValue;
                    for (int i = 0; i < tv.Trail.Count; i++) { float d2 = (tv.Trail[i] - pos).sqrMagnitude; if (d2 < best) { best = d2; near = i; } }
                    var flipped = new List<Vector3>();
                    for (int i = tv.Trail.Count - 1; i >= near; i--) flipped.Add(tv.Trail[i]);
                    tv.Trail.Clear(); tv.Trail.AddRange(flipped);
                    tv.Yaw = v.Heading;
                }
                // Breadcrumb trail of where the front has been; the other cars sit on it at fixed distances behind.
                if (tv.Trail.Count == 0 || (tv.Trail[tv.Trail.Count - 1] - pos).sqrMagnitude > 0.04f) tv.Trail.Add(pos);
                if (tv.Trail.Count > 400) tv.Trail.RemoveRange(0, tv.Trail.Count - 400);
                tv.Yaw = Mathf.LerpAngle(tv.Yaw, v.Heading, 1f - Mathf.Exp(-12f * dt));
                var frontFwd = Quaternion.Euler(0f, tv.Yaw, 0f) * Vector3.forward;
                // Cars from the front: loco then wagons, or (reversing) wagons first with the loco pushing at the back.
                var cars = v.LocoLeads ? new[] { tv.Loco, tv.Wagons[0], tv.Wagons[1] } : new[] { tv.Wagons[1], tv.Wagons[0], tv.Loco };
                for (int k = 0; k < cars.Length; k++)
                {
                    var (p, fwd) = k == 0 ? (pos, frontFwd) : AlongTrail(tv.Trail, pos, CarGap * k, frontFwd);
                    p.y = GroundHeight(p.x, p.z) + 0.28f;
                    cars[k].localPosition = p;
                    if (fwd.sqrMagnitude > 1e-4f)
                    {
                        cars[k].localRotation = Quaternion.Slerp(cars[k].localRotation, Quaternion.LookRotation(fwd), 1f - Mathf.Exp(-14f * dt));
                    }
                }
                bool moving = v.State == VehicleState.Driving;
                SetSmoke(tv.Smoke, moving || v.State == VehicleState.Loading || v.State == VehicleState.Tipping);
                if (moving)
                    foreach (var wh in tv.Wheels) wh.Rotate(Vector3.up, _w.B.TrainSpeed * T / (Mathf.PI * 0.8f) * 360f * dt, Space.Self);
                // Cargo: split what's on board across the two wagons, largest kind first.
                Item main = Item.None, second = Item.None; int mainN = 0, secondN = 0;
                foreach (var kv in v.Cargo)
                {
                    if (kv.Value > mainN) { second = main; secondN = mainN; main = kv.Key; mainN = kv.Value; }
                    else if (kv.Value > secondN) { second = kv.Key; secondN = kv.Value; }
                }
                int half = _w.B.TrainCap / 2;
                if (second == Item.None) { SetWagon(tv, 0, main, Mathf.Min(mainN, half)); SetWagon(tv, 1, main, Mathf.Max(0, mainN - half)); }
                else { SetWagon(tv, 0, main, mainN); SetWagon(tv, 1, second, secondN); }
            }
        }

        static (Vector3, Vector3) AlongTrail(List<Vector3> trail, Vector3 head, float back, Vector3 fallbackFwd)
        {
            float left = back;
            var cur = head;
            for (int i = trail.Count - 1; i >= 0; i--)
            {
                var prev = trail[i];
                float seg = Vector3.Distance(cur, prev);
                if (seg >= left && seg > 1e-4f)
                {
                    var p = Vector3.Lerp(cur, prev, left / seg);
                    var fwd = cur - prev; fwd.y = 0f;
                    return (p, fwd.normalized);
                }
                left -= seg;
                cur = prev;
            }
            // Not enough trail yet (just bought): line up straight behind.
            return (head - fallbackFwd * back, fallbackFwd);
        }

        void SetWagon(TView tv, int k, Item item, int count)
        {
            bool show = item != Item.None && count > 0;
            var heap = tv.Heaps[k];
            if (heap.gameObject.activeSelf != show) heap.gameObject.SetActive(show);
            if (!show) return;
            float fill = Mathf.Clamp01(count / (float)(_w.B.TrainCap / 2));
            var body = heap.GetChild(0);
            body.GetComponent<Renderer>().sharedMaterial = Look.Mat(ItemColor(item));
            body.localScale = new Vector3(1.35f, Mathf.Lerp(0.2f, 0.7f, fill), 2.1f);
            if (tv.Letters[k].text != Items.Letter(item)) tv.Letters[k].text = Items.Letter(item);
        }

        TView MakeTrain()
        {
            var tv = new TView();
            var wheels = new List<Transform>();
            // Locomotive: frame, boiler drum with smokebox and chimney, steam dome, cab at the back, red buffer beam.
            var r = new GameObject("Locomotive").transform;
            r.SetParent(transform, false);
            tv.Loco = r;
            Art.Cube("Frame", r, new Vector3(0f, 0.55f, 0f), new Vector3(1.2f, 0.25f, 3.0f), MachineDark);
            Art.Cyl("Boiler", r, new Vector3(0f, 1.25f, -0.7f), 1.1f, 2.0f, LocoGreen, new Vector3(90f, 0f, 0f));
            Art.Cyl("Smokebox", r, new Vector3(0f, 1.25f, 1.3f), 1.12f, 0.25f, MachineDark, new Vector3(90f, 0f, 0f));
            Art.Cyl("Chimney", r, new Vector3(0f, 1.7f, 1.15f), 0.32f, 0.7f, MachineDark);
            Art.Cyl("Dome", r, new Vector3(0f, 1.75f, 0.3f), 0.42f, 0.3f, new Color(0.75f, 0.6f, 0.3f));
            Art.Cube("Cab", r, new Vector3(0f, 1.55f, -1.05f), new Vector3(1.3f, 1.4f, 0.95f), LocoGreen * 0.9f);
            Art.Cube("Cab roof", r, new Vector3(0f, 2.3f, -1.05f), new Vector3(1.45f, 0.1f, 1.1f), MachineDark);
            Art.Cube("Cab window", r, new Vector3(0f, 1.75f, -0.57f), new Vector3(0.9f, 0.4f, 0.03f), Glass);
            Art.Cube("Buffer beam", r, new Vector3(0f, 0.6f, 1.55f), new Vector3(1.35f, 0.3f, 0.12f), LocoRed);
            Art.Cube("Lamp", r, new Vector3(0f, 0.95f, 1.6f), new Vector3(0.22f, 0.22f, 0.12f), new Color(1f, 0.9f, 0.6f));
            tv.Smoke = Art.Smoke(r, new Vector3(0f, 2.5f, 1.15f), 1.1f);
            foreach (float z in new[] { 0.9f, 0f, -0.9f }) AddWheelPair(r, z, 0.8f, wheels);
            // Two open wagons with a heap of cargo and a letter badge.
            tv.Wagons = new Transform[2];
            tv.Heaps = new Transform[2];
            tv.Letters = new TextMesh[2];
            for (int k = 0; k < 2; k++)
            {
                var wgn = new GameObject("Wagon " + k).transform;
                wgn.SetParent(transform, false);
                tv.Wagons[k] = wgn;
                var body = new Color(0.45f, 0.3f, 0.2f) * (k == 0 ? 1f : 0.9f);
                Art.Cube("Floor", wgn, new Vector3(0f, 0.6f, 0f), new Vector3(1.5f, 0.15f, 2.6f), MachineDark);
                Art.Cube("Side L", wgn, new Vector3(-0.72f, 1.0f, 0f), new Vector3(0.08f, 0.7f, 2.6f), body);
                Art.Cube("Side R", wgn, new Vector3(0.72f, 1.0f, 0f), new Vector3(0.08f, 0.7f, 2.6f), body);
                Art.Cube("End F", wgn, new Vector3(0f, 1.0f, 1.28f), new Vector3(1.5f, 0.7f, 0.08f), body);
                Art.Cube("End B", wgn, new Vector3(0f, 1.0f, -1.28f), new Vector3(1.5f, 0.7f, 0.08f), body);
                foreach (float z in new[] { 0.8f, -0.8f }) AddWheelPair(wgn, z, 0.7f, wheels);
                var heap = new GameObject("Load").transform;
                heap.SetParent(wgn, false);
                heap.localPosition = new Vector3(0f, 0.7f, 0f);
                Art.Part("Heap", heap, Art.Rock, Vector3.zero, new Vector3(1.35f, 0.6f, 2.1f), Color.gray);
                Art.Cyl("Badge", heap, new Vector3(0f, 0.9f, 0f), 0.6f, 0.04f, new Color(0.1f, 0.1f, 0.1f));
                tv.Letters[k] = Look.Label("", heap, new Vector3(0f, 0.96f, 0f), 0.7f, Color.white);
                tv.Letters[k].fontStyle = FontStyle.Bold;
                tv.Letters[k].GetComponent<MeshRenderer>().sharedMaterial = ItemTextMat;
                tv.Heaps[k] = heap;
                heap.gameObject.SetActive(false);
            }
            tv.Wheels = wheels.ToArray();
            return tv;
        }

        static void AddWheelPair(Transform r, float z, float dia, List<Transform> wheels)
        {
            for (int side = -1; side <= 1; side += 2)
            {
                var p = new GameObject("Wheel").transform;
                p.SetParent(r, false);
                p.localPosition = new Vector3(side * 0.5f, 0.3f + dia * 0.1f, z);
                p.localRotation = Quaternion.Euler(0f, 0f, 90f);
                Art.Part("Rim", p, Art.Prim(PrimitiveType.Cylinder), Vector3.zero, new Vector3(dia, 0.06f, dia), MachineDark);
                Art.Part("Spoke", p, Art.Prim(PrimitiveType.Cube), Vector3.zero, new Vector3(dia * 0.9f, 0.07f, 0.08f), RailSteel);
                wheels.Add(p);
            }
        }
    }
}
