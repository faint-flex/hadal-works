using System.Collections.Generic;
using Kilnwood.Sim;
using UnityEngine;

namespace Kilnwood.Game
{
    /// <summary>0.10 Tier 5 "Electricity" models: the dynamo on the shaft network, timber power poles with glass
    /// insulators and sagging wires, and the wire mill. Lamps on the poles glow while the grid is live (the pole's
    /// status text says the same thing in words).</summary>
    public partial class WorldView
    {
        static readonly Color DynamoGreen = new Color(0.17f, 0.27f, 0.22f);
        static readonly Color Brass = new Color(0.72f, 0.56f, 0.26f);
        static readonly Color Insulator = new Color(0.36f, 0.52f, 0.5f);   // greenish glass
        static readonly Color WireDark = new Color(0.1f, 0.1f, 0.11f);
        static readonly Color LampGlow = new Color(1f, 0.86f, 0.55f);
        const float PoleTop = 6.2f, ArmHalf = 0.62f;

        int _gridCount = -1;
        GameObject _wires;

        // ------------------------------------------------------------------ dynamo

        void MakeDynamo(Transform B, float s, BView v)
        {
            Color stone = Look.StoneWall, iron = Look.Iron, dark = Look.MineDark;
            float h = s / 2f;
            Art.Cube("Plinth", B, new Vector3(0f, 0.2f, 0f), new Vector3(s - 0.4f, 0.4f, s - 0.4f), stone * 0.85f);
            Art.Cube("Bed plate", B, new Vector3(0f, 0.48f, -0.1f), new Vector3(2.4f, 0.16f, 2.6f), dark);
            // Field frame: a big green-painted iron ring on its side, axle front to back toward the shaft.
            Art.Cyl("Field frame", B, new Vector3(0f, 1.55f, -1.0f), 1.9f, 1.6f, DynamoGreen, new Vector3(90f, 0f, 0f));
            for (int k = 0; k < 2; k++)
                Art.Cyl("Frame band", B, new Vector3(0f, 1.55f, -0.95f + k * 1.35f), 1.98f, 0.1f, Brass * 0.85f, new Vector3(90f, 0f, 0f));
            Art.Cube("Foot L", B, new Vector3(-0.8f, 0.85f, -0.2f), new Vector3(0.3f, 0.6f, 1.4f), DynamoGreen * 0.8f);
            Art.Cube("Foot R", B, new Vector3(0.8f, 0.85f, -0.2f), new Vector3(0.3f, 0.6f, 1.4f), DynamoGreen * 0.8f);
            // Brass commutator and brush gear at the back, bearing pedestal and pulley at the front (shaft side).
            Art.Cyl("Commutator", B, new Vector3(0f, 1.55f, -1.55f), 0.7f, 0.55f, Brass, new Vector3(90f, 0f, 0f));
            Art.Cube("Brush rocker", B, new Vector3(0f, 1.55f, -1.4f), new Vector3(1.1f, 0.12f, 0.12f), dark);
            Art.Cube("Bearing", B, new Vector3(0f, 1.05f, 1.15f), new Vector3(0.5f, 0.9f, 0.4f), dark);
            var pul = new GameObject("Pulley").transform;
            pul.SetParent(B, false);
            pul.localPosition = new Vector3(0f, 1.55f, h - 0.35f);
            Art.Cyl("Pulley rim", pul, new Vector3(0f, 0f, -0.18f), 1.3f, 0.36f, iron * 1.2f, new Vector3(90f, 0f, 0f));
            for (int k = 0; k < 3; k++)
                Art.Cube("Pulley spoke", pul, Vector3.zero, new Vector3(0.08f, 1.15f, 0.1f), dark, new Vector3(0f, 0f, k * 60f));
            Art.Cube("Pulley mark", pul, new Vector3(0f, 0.6f, 0.02f), new Vector3(0.18f, 0.12f, 0.06f), Look.Accent);
            Art.Cyl("Axle", B, new Vector3(0f, 1.55f, -1.2f), 0.22f, 3.0f, iron * 1.3f, new Vector3(90f, 0f, 0f));
            v.Anim = pul;
            // Switchboard on a slate panel, with a meter and a lamp that lights while the dynamo makes power.
            Art.Cube("Switchboard", B, new Vector3(1.45f, 1.3f, -0.9f), new Vector3(0.12f, 1.6f, 1.0f), new Color(0.2f, 0.21f, 0.23f));
            Art.Cube("Switchboard leg", B, new Vector3(1.45f, 0.45f, -0.9f), new Vector3(0.1f, 0.9f, 0.1f), Look.Timber * 0.7f);
            Art.Cyl("Meter", B, new Vector3(1.38f, 1.65f, -0.9f), 0.38f, 0.06f, new Color(0.9f, 0.88f, 0.8f), new Vector3(0f, 0f, 90f));
            Art.Cube("Knife switch", B, new Vector3(1.38f, 1.05f, -0.9f), new Vector3(0.08f, 0.35f, 0.25f), Brass);
            v.Extra = Art.Ball("Lamp", B, new Vector3(1.38f, 2.3f, -0.9f), Vector3.one * 0.3f, LampGlow);
            v.Extra.GetComponent<Renderer>().sharedMaterial = Look.GlowMat(LampGlow);
            v.Extra.SetActive(false);
            // Terminal mast where the line starts.
            Art.Cyl("Terminal mast", B, new Vector3(-1.4f, 0.4f, -1.4f), 0.22f, 4.0f, Look.Bark);
            Art.Cube("Terminal arm", B, new Vector3(-1.4f, 4.25f, -1.4f), new Vector3(1.3f, 0.14f, 0.14f), Look.Timber * 0.8f);
            for (int k = -1; k <= 1; k += 2)
                Art.Cyl("Insulator", B, new Vector3(-1.4f + k * ArmHalf, 4.32f, -1.4f), 0.14f, 0.2f, Insulator);
            Art.Cyl("Lead", B, new Vector3(-0.2f, 2.0f, -1.4f), 0.05f, 2.4f, WireDark, new Vector3(0f, 0f, 30f));
        }

        // ------------------------------------------------------------------ power pole

        void MakePole(BView v)
        {
            var B = v.Body;
            Art.Cyl("Pole", B, new Vector3(0f, 0f, 0f), 0.3f, PoleTop + 0.2f, Look.Bark * 0.9f);
            Art.Cube("Cross arm", B, new Vector3(0f, PoleTop - 0.1f, 0f), new Vector3(ArmHalf * 2f + 0.4f, 0.14f, 0.16f), Look.Timber * 0.75f);
            for (int k = -1; k <= 1; k += 2)
            {
                Art.Cube("Brace", B, new Vector3(k * 0.3f, PoleTop - 0.45f, 0.1f), new Vector3(0.05f, 0.75f, 0.04f), Look.Iron, new Vector3(0f, 0f, -k * 40f));
                Art.Cyl("Insulator", B, new Vector3(k * ArmHalf, PoleTop - 0.03f, 0f), 0.14f, 0.2f, Insulator);
            }
            Art.Cube("Step", B, new Vector3(0f, 2.4f, 0.17f), new Vector3(0.3f, 0.05f, 0.05f), Look.Iron);
            Art.Cube("Step", B, new Vector3(0f, 3.2f, -0.17f), new Vector3(0.3f, 0.05f, 0.05f), Look.Iron);
            // Lamp on a short bracket below the arm.
            Art.Cube("Lamp bracket", B, new Vector3(0f, PoleTop - 1.2f, 0.4f), new Vector3(0.05f, 0.05f, 0.7f), Look.Iron);
            Art.ConeAt("Lamp shade", B, new Vector3(0f, PoleTop - 1.35f, 0.72f), 0.42f, 0.2f, DynamoGreen);
            v.Extra = Art.Ball("Lamp", B, new Vector3(0f, PoleTop - 1.45f, 0.72f), Vector3.one * 0.28f, LampGlow);
            v.Extra.GetComponent<Renderer>().sharedMaterial = Look.GlowMat(LampGlow);
            v.Extra.SetActive(false);
            Art.Cube("Footing", B, new Vector3(0f, 0.08f, 0f), new Vector3(0.6f, 0.16f, 0.6f), Look.StoneWall * 0.8f);
        }

        // ------------------------------------------------------------------ wire mill

        void MakeWireMill(Transform B, float s, BView v)
        {
            Color brick = Look.Brickwork, stone = Look.StoneWall, iron = Look.Iron, dark = Look.MineDark;
            float h = s / 2f;
            Art.Cube("Floor", B, new Vector3(0f, 0.08f, 0f), new Vector3(s - 0.3f, 0.16f, s - 0.3f), stone * 0.85f);
            // Long brick shed along the back with a raised clerestory roof.
            Art.Cube("Walls", B, new Vector3(0f, 1.25f, -0.75f), new Vector3(s - 0.6f, 2.2f, 1.9f), brick);
            Art.Cube("Plinth course", B, new Vector3(0f, 0.3f, -0.75f), new Vector3(s - 0.5f, 0.3f, 2.0f), stone * 0.8f);
            // Pitched roof: two slabs meeting at a ridge along the shed, brick gables filling the ends.
            const float run = 1.2f, pitch = 28f, eave = 2.35f, zMid = -0.75f;
            float rise = run * Mathf.Sin(pitch * Mathf.Deg2Rad), half = run * Mathf.Cos(pitch * Mathf.Deg2Rad);
            for (int k = -1; k <= 1; k += 2)
                Art.Cube("Roof", B, new Vector3(0f, eave + rise / 2f + 0.04f, zMid + k * half / 2f), new Vector3(s - 0.3f, 0.09f, run + 0.12f), Look.Roof, new Vector3(k * pitch, 0f, 0f));
            Art.Cube("Ridge", B, new Vector3(0f, eave + rise + 0.06f, zMid), new Vector3(s - 0.25f, 0.1f, 0.16f), Look.Roof * 0.8f);
            for (int k = -1; k <= 1; k += 2)
            {
                var gable = new GameObject("Gable").transform;
                gable.SetParent(B, false);
                gable.localPosition = new Vector3(k * (s - 0.8f) / 2f, eave, zMid);
                gable.localScale = new Vector3(1f, rise, half);
                Art.Cube("Gable fill", gable, Vector3.zero, new Vector3(0.18f, 1.414f, 1.414f), brick, new Vector3(45f, 0f, 0f));
            }
            for (int k = -1; k <= 1; k++)
                Art.Cube("Window", B, new Vector3(k * 1.05f, 1.45f, 0.21f), new Vector3(0.55f, 0.8f, 0.05f), new Color(0.32f, 0.36f, 0.38f));
            // Drawing bench in the yard: steel goes in at one end, wire winds onto a big drum.
            Art.Cube("Bench", B, new Vector3(-0.3f, 0.6f, 1.0f), new Vector3(2.5f, 0.2f, 0.7f), Look.Timber * 0.8f);
            for (int k = -1; k <= 1; k += 2) Art.Cube("Bench leg", B, new Vector3(-0.3f + k * 1.1f, 0.3f, 1.0f), new Vector3(0.15f, 0.5f, 0.6f), dark);
            Art.Cube("Die block", B, new Vector3(-1.0f, 0.85f, 1.0f), new Vector3(0.35f, 0.32f, 0.35f), iron);
            Art.Cyl("Wire strand", B, new Vector3(-0.85f, 0.88f, 1.0f), 0.04f, 1.1f, ItemColor(Item.Wire) * 1.3f, new Vector3(0f, 0f, -90f));
            var drum = new GameObject("Drum").transform;
            drum.SetParent(B, false);
            drum.localPosition = new Vector3(0.6f, 1.15f, 1.0f);
            Art.Cyl("Drum core", drum, new Vector3(0f, -0.36f, 0f), 0.6f, 0.72f, ItemColor(Item.Wire));
            Art.Cyl("Drum flange", drum, new Vector3(0f, -0.4f, 0f), 0.95f, 0.06f, Look.Timber * 0.7f);
            Art.Cyl("Drum flange", drum, new Vector3(0f, 0.34f, 0f), 0.95f, 0.06f, Look.Timber * 0.7f);
            Art.Cube("Drum mark", drum, new Vector3(0.42f, 0.38f, 0f), new Vector3(0.14f, 0.05f, 0.1f), Look.Accent);
            v.Anim = drum;
            // Electric motor driving the drum.
            Art.Cyl("Motor", B, new Vector3(1.55f, 0.75f, 1.0f), 0.55f, 0.7f, DynamoGreen, new Vector3(0f, 0f, 90f));
            Art.Cube("Motor foot", B, new Vector3(1.25f, 0.4f, 1.0f), new Vector3(0.6f, 0.2f, 0.5f), dark);
            // Finished coils stacked by the door.
            for (int k = 0; k < 3; k++)
                Art.Cyl("Coil", B, new Vector3(-1.55f, 0.16f + k * 0.17f, 0.35f + (k % 2) * 0.08f), 0.55f, 0.16f, ItemColor(Item.Wire) * (1f + 0.1f * k));
            // Gable insulator where the drop line from the nearest pole comes in.
            Art.Cube("Service bracket", B, new Vector3(-2.0f, 3.1f, -0.75f), new Vector3(0.1f, 1.6f, 0.1f), iron);   // clear of the roof overhang
            Art.Cyl("Insulator", B, new Vector3(-2.0f, 3.9f, -0.75f), 0.14f, 0.2f, Insulator);
            Front(B, h);
        }

        // ------------------------------------------------------------------ wires and motion

        /// <summary>Where a wire meets this building (world space): insulator k (-1 or +1) on a pole or the
        /// dynamo's terminal mast, or the wire mill's gable insulator.</summary>
        Vector3 WirePoint(Building b, int k)
        {
            var v = _views[b.Id];
            switch (b.Type)
            {
                case BuildingType.Pole: return v.Body.TransformPoint(new Vector3(k * ArmHalf, PoleTop + 0.17f, 0f));
                case BuildingType.Dynamo: return v.Body.TransformPoint(new Vector3(-1.4f + k * ArmHalf, 4.52f, -1.4f));
                default: return v.Body.TransformPoint(new Vector3(-2.0f, 4.1f, -0.75f));
            }
        }

        /// <summary>Strings wire along each grid: every node to its nearest earlier node (a tree, so no crossed
        /// tangles), and a single drop line from the nearest node to each electric machine.</summary>
        void RefreshWires()
        {
            if (_wires != null) Destroy(_wires);
            _wires = new GameObject("Wires");
            _wires.transform.SetParent(transform, false);
            var verts = new List<Vector3>();
            var tris = new List<int>();
            foreach (var g in _w.Grids)
            {
                var nodes = g.Nodes;
                // Turn each pole's cross arm across its line: face the nearest other node.
                foreach (var n in nodes)
                {
                    if (n.Type != BuildingType.Pole || !_views.TryGetValue(n.Id, out var pv)) continue;
                    Building near = null; int best = int.MaxValue;
                    foreach (var o in nodes) { if (o == n) continue; int d = World.Gap(n, o); if (d < best) { best = d; near = o; } }
                    if (near == null) continue;
                    var dir = FootCenter(near.X, near.Y, near.Size) - FootCenter(n.X, n.Y, 1);
                    if (dir.sqrMagnitude > 0.01f) pv.Body.localRotation = Quaternion.LookRotation(dir.normalized);
                }
                for (int i = 1; i < nodes.Count; i++)
                {
                    Building near = null; int best = int.MaxValue;
                    for (int j = 0; j < i; j++) { int d = World.Gap(nodes[i], nodes[j]); if (d < best) { best = d; near = nodes[j]; } }
                    if (near == null || !_views.ContainsKey(near.Id) || !_views.ContainsKey(nodes[i].Id)) continue;
                    // Match insulators side by side so the two conductors don't cross.
                    var a0 = WirePoint(nodes[i], -1); var a1 = WirePoint(nodes[i], 1);
                    var b0 = WirePoint(near, -1); var b1 = WirePoint(near, 1);
                    if ((a0 - b0).sqrMagnitude + (a1 - b1).sqrMagnitude > (a0 - b1).sqrMagnitude + (a1 - b0).sqrMagnitude) { var t = b0; b0 = b1; b1 = t; }
                    Sag(verts, tris, a0, b0);
                    Sag(verts, tris, a1, b1);
                }
                foreach (var m in g.Machines)
                {
                    Building near = null; int best = int.MaxValue;
                    foreach (var n in nodes) { int d = World.Gap(m, n); if (d < best) { best = d; near = n; } }
                    if (near == null || !_views.ContainsKey(m.Id) || !_views.ContainsKey(near.Id)) continue;
                    Sag(verts, tris, WirePoint(near, -1), WirePoint(m, 0));
                }
            }
            if (verts.Count == 0) return;
            var mesh = new Mesh { name = "Wires" };
            if (verts.Count > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            _wires.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = _wires.AddComponent<MeshRenderer>();
            r.sharedMaterial = Look.Mat(WireDark);
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        /// <summary>A thin square tube hanging between two points, sagging about 3% of the span.</summary>
        static void Sag(List<Vector3> verts, List<int> tris, Vector3 a, Vector3 b)
        {
            const int segs = 10; const float r = 0.035f;
            float span = Vector3.Distance(a, b);
            float sag = Mathf.Min(0.9f, span * 0.035f);
            var along = (b - a).normalized;
            var side = Vector3.Cross(Vector3.up, along);
            if (side.sqrMagnitude < 1e-4f) side = Vector3.right;
            side.Normalize();
            var up = Vector3.Cross(along, side).normalized;
            int start = verts.Count;
            for (int i = 0; i <= segs; i++)
            {
                float t = i / (float)segs;
                var p = Vector3.Lerp(a, b, t) + Vector3.down * (sag * 4f * t * (1f - t));
                verts.Add(p + side * r); verts.Add(p + up * r); verts.Add(p - side * r); verts.Add(p - up * r);
            }
            for (int i = 0; i < segs; i++)
                for (int k = 0; k < 4; k++)
                {
                    int a0 = start + i * 4 + k, a1 = start + i * 4 + (k + 1) % 4, b0 = a0 + 4, b1 = a1 + 4;
                    tris.Add(a0); tris.Add(b0); tris.Add(a1);
                    tris.Add(a1); tris.Add(b0); tris.Add(b1);
                }
        }

        void UpdateElectricViews(bool layoutChanged)
        {
            if (layoutChanged) { RefreshWires(); _gridCount = _w.GridVersion; }
            float dt = Time.deltaTime;
            foreach (var b in _w.Buildings.Values)
            {
                if (b.Type != BuildingType.Dynamo && b.Type != BuildingType.Pole && b.Type != BuildingType.WireMill) continue;
                if (!_views.TryGetValue(b.Id, out var v)) continue;
                switch (b.Type)
                {
                    case BuildingType.Dynamo:
                    {
                        bool turning = b.Power > 0f;
                        if (v.Extra != null && v.Extra.activeSelf != turning) v.Extra.SetActive(turning);
                        if (turning && v.Anim != null) { v.AnimAngle += dt * 260f * b.Power; v.Anim.localRotation = Quaternion.Euler(0f, 0f, v.AnimAngle); }
                        break;
                    }
                    case BuildingType.Pole:
                    {
                        bool live = b.Elec > 0f;
                        if (v.Extra != null && v.Extra.activeSelf != live) v.Extra.SetActive(live);
                        break;
                    }
                    case BuildingType.WireMill:
                    {
                        bool running = b.Status == "Working" || b.Status.StartsWith("Low electricity");
                        if (running && v.Anim != null) { v.AnimAngle += dt * 90f * Mathf.Max(0.2f, b.Elec); v.Anim.localRotation = Quaternion.Euler(0f, v.AnimAngle, 0f); }
                        break;
                    }
                }
            }
        }
    }
}
