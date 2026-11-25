using Kilnwood.Sim;
using UnityEngine;
using Terrain = Kilnwood.Sim.Terrain;

namespace Kilnwood.Game
{
    /// <summary>0.6 Tier 2 "Water Power" models: waterwheel, overhead line shafts, sawmill, trip hammer, and their motion.</summary>
    public partial class WorldView
    {
        const float ShaftHeight = 2.7f;
        int _shaftArmsCount = -1;

        // ------------------------------------------------------------------ line shaft

        /// <summary>A post with a bearing on top, and a rod toward each side that has something to drive.
        /// Rods spin (square couplings show it) whenever a waterwheel turns the network.</summary>
        void MakeShaft(BView v)
        {
            var r = v.Root.transform;
            // Portal frame: two posts on opposite corners of the tile and a beam across the top carrying the bearing,
            // so trucks and belt items pass underneath instead of through a post in the middle.
            const float c = 0.8f;   // post offset from the centre along x and z (tile half-width is 1)
            for (int k = -1; k <= 1; k += 2)
            {
                Art.Cube("Post", r, new Vector3(c * k, ShaftHeight / 2f, c * k), new Vector3(0.18f, ShaftHeight, 0.18f), Look.Timber * 0.85f);
                Art.Cube("Foot", r, new Vector3(c * k, 0.08f, c * k), new Vector3(0.36f, 0.16f, 0.36f), Look.StoneWall);
            }
            Art.Cube("Beam", r, new Vector3(0f, ShaftHeight - 0.2f, 0f), new Vector3(0.16f, 0.16f, c * 2f * 1.4142f + 0.18f), Look.Timber * 0.8f, new Vector3(0f, 45f, 0f));
            Art.Cube("Bearing", r, new Vector3(0f, ShaftHeight, 0f), new Vector3(0.36f, 0.3f, 0.36f), Look.Iron);
            v.Anim = new GameObject("Spin").transform;   // rotating parts live under per-arm spinners
            v.Anim.SetParent(r, false);
            v.Arms = new GameObject[4];
            for (int d = 0; d < 4; d++)
            {
                var arm = new GameObject("Arm " + Dir.Names[d]).transform;
                arm.SetParent(r, false);
                arm.localPosition = new Vector3(0f, ShaftHeight, 0f);
                arm.localRotation = FacingRot(d);
                var spin = new GameObject("Rod spin").transform;
                spin.SetParent(arm, false);
                Art.Part("Rod", spin, Art.Prim(PrimitiveType.Cylinder), new Vector3(0f, 0f, T / 4f), new Vector3(0.14f, T / 4f, 0.14f), Look.Iron * 1.25f, new Vector3(90f, 0f, 0f));
                Art.Cube("Coupling", spin, new Vector3(0f, 0f, T / 2f - 0.12f), new Vector3(0.26f, 0.26f, 0.16f), Look.Iron * 0.8f);
                v.Arms[d] = arm.gameObject;
            }
            // Pulley in the middle (axis turned to match the rods when they're refreshed).
            var pulley = new GameObject("Pulley").transform;
            pulley.SetParent(r, false);
            pulley.localPosition = new Vector3(0f, ShaftHeight, 0f);
            v.Anim2 = Art.Gear("Pulley wheel", pulley, Vector3.zero, 0.62f, 0.12f, Look.Timber * 1.1f, 6);
        }

        bool Drives(int x, int y)
        {
            var b = _w.AtOrShaft(x, y);
            if (b == null) return false;
            if (b.Type == BuildingType.Shaft || b.Type == BuildingType.Waterwheel) return true;
            return _w.B.Defs[b.Type].Hp > 0;
        }

        void RefreshShaftArms()
        {
            foreach (var b in _w.Buildings.Values)
            {
                if (b.Type != BuildingType.Shaft || !_views.TryGetValue(b.Id, out var v)) continue;
                bool any = false, ew = false;
                for (int d = 0; d < 4; d++)
                {
                    bool on = Drives(b.X + Dir.DX[d], b.Y + Dir.DY[d]);
                    if (v.Arms[d].activeSelf != on) v.Arms[d].SetActive(on);
                    any |= on;
                    if (on && (d == 1 || d == 3)) ew = true;
                }
                if (!any) v.Arms[1].SetActive(true);
                // Pulley axis along the rods: x for an east-west run, z for north-south.
                v.Anim2.parent.localRotation = ew || !any ? Quaternion.Euler(0f, 0f, 90f) : Quaternion.Euler(90f, 0f, 0f);
            }
        }

        // ------------------------------------------------------------------ waterwheel

        /// <summary>Undershot wheel hanging off the bank into the river, with a little wheel house on the land side.</summary>
        void MakeWaterwheel(Building b, BView v, float s)
        {
            // Which side is the river? The wheel goes out over it.
            int water = 3;
            for (int d = 0; d < 4; d++)
            {
                int cx = b.X + (Dir.DX[d] > 0 ? b.Size : Dir.DX[d] < 0 ? -1 : 0), cy = b.Y + (Dir.DY[d] > 0 ? b.Size : Dir.DY[d] < 0 ? -1 : 0);
                if (_w.GroundAt(cx, cy) == Terrain.Water || _w.GroundAt(cx + (Dir.DX[d] == 0 ? 1 : 0), cy + (Dir.DY[d] == 0 ? 1 : 0)) == Terrain.Water) { water = d; break; }
            }
            var f = new GameObject("Wheel frame").transform;
            f.SetParent(v.Root.transform, false);
            f.localRotation = FacingRot(water);     // +z points at the water
            Color stone = Look.StoneWall, timber = Look.Timber;
            Art.Cube("Footing", f, new Vector3(0f, 0.35f, -0.3f), new Vector3(s - 0.3f, 0.7f, s - 0.8f), stone);
            Art.Cube("Bank wall", f, new Vector3(0f, 0.1f, s / 2f - 0.15f), new Vector3(s, 1.2f, 0.4f), stone * 0.9f);
            Art.Cube("House", f, new Vector3(0f, 1.75f, -0.75f), new Vector3(s - 0.9f, 2.1f, 1.9f), timber);
            Art.RoofAt("Roof", f, new Vector3(0f, 2.8f, -0.75f), s - 0.5f, 2.4f, 1.0f, Look.Roof, 90f);
            Art.Cube("Door", f, new Vector3(-0.6f, 1.4f, -1.72f), new Vector3(0.7f, 1.3f, 0.05f), Look.Roof);
            // Wheel: it stands in the river beside the bank with its face along the current, the axle running
            // from the house out over the water, so the flow pushes the bottom paddles downstream.
            const float R = 1.8f;
            var hub = new Vector3(0f, 1.25f, s / 2f + 1.05f);
            var mount = new GameObject("Wheel mount").transform;
            mount.SetParent(f, false);
            mount.localPosition = hub;
            mount.localRotation = Quaternion.Euler(0f, 90f, 0f);   // wheel-local x (the axle) now points at the water
            Art.Cube("Outer post", f, new Vector3(-R - 0.2f, 0.35f, hub.z + 0.95f), new Vector3(0.24f, 2.2f, 0.24f), timber * 0.8f);
            Art.Cube("Outer post 2", f, new Vector3(R + 0.2f, 0.35f, hub.z + 0.95f), new Vector3(0.24f, 2.2f, 0.24f), timber * 0.8f);
            Art.Cube("Outer beam", f, new Vector3(0f, 1.25f, hub.z + 0.95f), new Vector3(2f * R + 0.6f, 0.2f, 0.2f), timber * 0.8f);
            Art.Log("Axle", f, new Vector3(0f, 1.25f, (s / 2f - 0.4f + hub.z + 1.0f) / 2f), hub.z + 1.0f - (s / 2f - 0.4f), 0.22f, Look.Iron, 90f);
            var wheel = new GameObject("Wheel").transform;
            wheel.SetParent(mount, false);
            const int paddles = 16;
            for (int i = 0; i < paddles; i++)
            {
                float a = i * 360f / paddles;
                var q = Quaternion.Euler(a, 0f, 0f);
                Art.Cube("Paddle", wheel, q * new Vector3(0f, 0f, R - 0.25f), new Vector3(1.5f, 0.07f, 0.55f), timber * 1.05f, new Vector3(a, 0f, 0f));
                float chord = 2f * R * Mathf.Sin(Mathf.PI / paddles) + 0.05f;
                for (int side = -1; side <= 1; side += 2)
                    Art.Cube("Rim", wheel, q * new Vector3(side * 0.74f, R * Mathf.Cos(Mathf.PI / paddles) - 0.05f, 0f),
                        new Vector3(0.08f, 0.12f, chord), timber * 0.75f, new Vector3(a, 0f, 0f));
                if (i % 2 == 0)
                    for (int side = -1; side <= 1; side += 2)
                        Art.Cube("Spoke", wheel, q * new Vector3(side * 0.6f, R / 2f, 0f), new Vector3(0.09f, R, 0.12f), timber * 0.85f, new Vector3(a, 0f, 0f));
            }
            Art.Cyl("Hub", wheel, new Vector3(0.8f, 0f, 0f), 0.55f, 1.6f, timber * 0.7f, new Vector3(0f, 0f, 90f));
            v.Anim = wheel;
            // Splash where the paddles meet the river.
            var splash = Art.Smoke(f, new Vector3(0f, -0.2f, hub.z), 0.5f);
            var main = splash.main;
            main.startColor = new Color(0.85f, 0.92f, 1f, 0.5f);
            main.startSizeMultiplier = 0.6f;
            main.startLifetimeMultiplier = 0.4f;
            Texturize(f);
        }

        // ------------------------------------------------------------------ sawmill

        void MakeSawmill(Transform B, float s, BView v)
        {
            Color timber = Look.Timber, planks = Look.Planks;
            float h = s / 2f;
            Art.Cube("Floor", B, new Vector3(0f, 0.08f, 0f), new Vector3(s - 0.3f, 0.16f, s - 0.3f), planks * 0.8f);
            for (int i = 0; i < 4; i++)
                Art.Cube("Post", B, new Vector3(i % 2 == 0 ? -1.6f : 1.6f, 1.3f, i < 2 ? -1.6f : 1.6f), new Vector3(0.22f, 2.4f, 0.22f), timber);
            Art.Cube("Back wall", B, new Vector3(0f, 1.3f, -1.62f), new Vector3(3.2f, 2.3f, 0.1f), timber * 0.95f);
            Art.RoofAt("Roof", B, new Vector3(0f, 2.5f, 0f), s - 0.1f, s - 0.1f, 1.2f, Look.Roof, 90f);
            Art.Cube("Saw bench", B, new Vector3(0f, 0.55f, 0f), new Vector3(1.0f, 0.75f, 3.0f), timber * 0.7f);
            Art.Log("Log on bench", B, new Vector3(0f, 1.12f, -0.75f), 1.4f, 0.42f, Look.Bark, 90f);
            var pivot = new GameObject("Blade pivot").transform;
            pivot.SetParent(B, false);
            pivot.localPosition = new Vector3(0f, 0.95f, 0.25f);
            pivot.localRotation = Quaternion.Euler(0f, 0f, 90f);
            v.Anim = Art.Gear("Blade", pivot, Vector3.zero, 1.15f, 0.04f, Look.Iron * 1.5f, 24);
            for (int k = 0; k < 8; k++)
                Art.Cube("Plank stack", B, new Vector3(1.2f, 0.22f + k * 0.11f, 0.4f), new Vector3(0.85f, 0.1f, 2.4f), planks * (k % 2 == 0 ? 1.05f : 0.95f));
            Art.Cube("Sawdust", B, new Vector3(-0.85f, 0.2f, 0.6f), new Vector3(0.7f, 0.1f, 0.7f), planks * 1.1f);
            // Belt pulley up under the roof: the drive comes in from the shaft.
            var pp = new GameObject("Pulley pivot").transform;
            pp.SetParent(B, false);
            pp.localPosition = new Vector3(0f, 2.2f, -1.2f);
            pp.localRotation = Quaternion.Euler(0f, 0f, 90f);
            v.Anim2 = Art.Gear("Drive pulley", pp, Vector3.zero, 0.7f, 0.14f, timber * 1.1f, 6);
            Front(B, h);
        }

        // ------------------------------------------------------------------ trip hammer

        void MakeTripHammer(Transform B, float s, BView v)
        {
            // Open-sided: a low roof on four posts, so the big helve hammer and anvil read from any angle.
            Color timber = Look.Timber, stone = Look.StoneWall, dark = Look.MineDark;
            float h = s / 2f;
            Art.Cube("Floor", B, new Vector3(0f, 0.08f, 0f), new Vector3(s - 0.3f, 0.16f, s - 0.3f), stone * 0.85f);
            for (int i = 0; i < 4; i++)
                Art.Cube("Post", B, new Vector3(i % 2 == 0 ? -1.65f : 1.65f, 1.5f, i < 2 ? -1.65f : 1.65f), new Vector3(0.2f, 2.9f, 0.2f), timber * 0.9f);
            Art.RoofAt("Roof", B, new Vector3(0f, 2.95f, 0f), s - 0.1f, s - 0.1f, 0.9f, Look.Roof, 90f);
            // Anvil on a stone block at the front right.
            Art.Cube("Anvil block", B, new Vector3(0.9f, 0.45f, 0.3f), new Vector3(0.9f, 0.7f, 0.9f), stone * 0.7f);
            Art.Cube("Anvil", B, new Vector3(0.9f, 0.95f, 0.3f), new Vector3(0.75f, 0.3f, 0.55f), dark);
            Art.Cube("Anvil horn", B, new Vector3(1.4f, 0.98f, 0.3f), new Vector3(0.3f, 0.18f, 0.25f), dark);
            // Helve: a heavy beam on a pivot frame, hammer head over the anvil, tail over the cam.
            for (int side = -1; side <= 1; side += 2)
                Art.Cube("Helve post", B, new Vector3(-0.55f, 0.95f, 0.3f + side * 0.32f), new Vector3(0.26f, 1.6f, 0.2f), timber * 0.8f);
            var helve = new GameObject("Helve").transform;
            helve.SetParent(B, false);
            helve.localPosition = new Vector3(-0.55f, 1.65f, 0.3f);
            Art.Cube("Beam", helve, new Vector3(0.55f, 0f, 0f), new Vector3(3.1f, 0.32f, 0.3f), timber * 0.75f);
            Art.Cube("Band", helve, new Vector3(1.2f, 0f, 0f), new Vector3(0.12f, 0.36f, 0.34f), Look.Iron);
            Art.Cube("Hammer head", helve, new Vector3(1.45f, -0.3f, 0f), new Vector3(0.6f, 0.62f, 0.55f), dark);
            v.Anim = helve;
            var cam = new GameObject("Cam pivot").transform;
            cam.SetParent(B, false);
            cam.localPosition = new Vector3(-1.35f, 1.15f, 0.3f);
            cam.localRotation = Quaternion.Euler(90f, 0f, 0f);
            v.Anim2 = Art.Gear("Cam wheel", cam, Vector3.zero, 1.1f, 0.24f, timber * 1.05f, 4);
            for (int k = 0; k < 5; k++)
                Art.Cube("Plate stack", B, new Vector3(-0.9f, 0.24f + k * 0.09f, 1.35f), new Vector3(0.9f, 0.08f, 0.9f), Look.Iron * (k % 2 == 0 ? 1.5f : 1.35f));
            Front(B, h);
        }

        // ------------------------------------------------------------------ motion

        void UpdatePowerViews(bool layoutChanged)
        {
            if (layoutChanged) { RefreshShaftArms(); _shaftArmsCount = _w.Buildings.Count; }
            float dt = Time.deltaTime;
            foreach (var b in _w.Buildings.Values)
            {
                if (!_views.TryGetValue(b.Id, out var v) || v.Anim == null) continue;
                switch (b.Type)
                {
                    case BuildingType.Waterwheel:
                        // Always turning with the river; a little slower when the load is heavy.
                        float load = b.NetSupply > 0 ? Mathf.Clamp01(b.NetDemand / (float)b.NetSupply) : 0f;
                        // Turn so the bottom paddles move downstream (the river flows south, world -z).
                        var axle = v.Anim.parent.TransformDirection(Vector3.right);
                        float with = Vector3.Dot(-Vector3.Cross(axle, Vector3.down), Vector3.back) >= 0f ? 1f : -1f;
                        v.AnimAngle -= dt * (40f - 12f * load) * with;
                        v.Anim.localRotation = Quaternion.Euler(v.AnimAngle, 0f, 0f);
                        break;
                    case BuildingType.Shaft:
                        if (b.Power <= 0f) break;
                        v.AnimAngle += dt * 220f;
                        foreach (var arm in v.Arms)
                            if (arm.activeSelf) arm.transform.GetChild(0).localRotation = Quaternion.Euler(0f, 0f, v.AnimAngle);
                        v.Anim2.localRotation = Quaternion.Euler(0f, v.AnimAngle, 0f);
                        break;
                    case BuildingType.Sawmill:
                    case BuildingType.TripHammer:
                    {
                        bool running = b.Status == "Working" || b.Status.StartsWith("Low power");
                        if (!running) break;
                        float rate = Mathf.Max(0.2f, b.Power);
                        v.AnimAngle += dt * rate;
                        if (b.Type == BuildingType.Sawmill)
                        {
                            v.Anim.localRotation = Quaternion.Euler(0f, v.AnimAngle * 900f, 0f);
                            v.Anim2.localRotation = Quaternion.Euler(0f, v.AnimAngle * 300f, 0f);
                        }
                        else
                        {
                            // Cam lifts the helve slowly, then it drops on the anvil: about two blows a second.
                            float p = (v.AnimAngle * 2f) % 1f;
                            float lift = p < 0.8f ? p / 0.8f : 1f - (p - 0.8f) / 0.2f;
                            v.Anim.localRotation = Quaternion.Euler(0f, 0f, 16f * lift);
                            v.Anim2.localRotation = Quaternion.Euler(0f, v.AnimAngle * 360f, 0f);
                        }
                        break;
                    }
                }
            }
        }
    }
}
