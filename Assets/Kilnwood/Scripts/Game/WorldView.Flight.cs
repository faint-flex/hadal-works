using System.Collections.Generic;
using Kilnwood.Sim;
using UnityEngine;

namespace Kilnwood.Game
{
    /// <summary>0.15 Tier 8 "Flight" models: the rolling mill, the hangar, the aeroplane, and planes flying off a hangar's front.</summary>
    public partial class WorldView
    {
        static readonly Color PlaneSilver = new Color(0.72f, 0.74f, 0.76f);
        static readonly Color PlaneStripe = new Color(0.16f, 0.3f, 0.5f);

        // ------------------------------------------------------------------ rolling mill

        /// <summary>A long brick shed open at the front over a two-high roll stand: housings, two big rolls, a drive motor
        /// on the side with a flywheel that turns while it works, and a stack of finished plates.</summary>
        void MakeRollingMill(Transform R, float s, BView v)
        {
            var B = Scaled(R); s /= T;
            Color brick = Look.Brickwork, stone = Look.StoneWall, iron = Look.Iron, dark = Look.MineDark;
            float h = s / 2f;
            Art.Cube("Floor", B, new Vector3(0f, 0.08f, 0f), new Vector3(s - 0.3f, 0.16f, s - 0.3f), stone * 0.85f);
            // Back shed with a pitched roof.
            Art.Cube("Walls", B, new Vector3(0f, 0.75f, -0.45f), new Vector3(1.6f, 1.2f, 0.75f), brick);
            Art.Cube("Window", B, new Vector3(0f, 0.95f, -0.07f), new Vector3(0.9f, 0.4f, 0.03f), new Color(0.3f, 0.37f, 0.42f));
            for (int k = -1; k <= 1; k += 2)
                Art.Cube("Roof", B, new Vector3(0f, 1.48f, -0.45f + k * 0.2f), new Vector3(1.75f, 0.05f, 0.5f), Look.Roof, new Vector3(k * 30f, 0f, 0f));
            // Roll stand in front: two housings with the rolls between them.
            for (int k = -1; k <= 1; k += 2)
                Art.Cube("Housing", B, new Vector3(k * 0.45f, 0.5f, 0.35f), new Vector3(0.14f, 0.75f, 0.3f), dark);
            Art.Cyl("Top roll", B, new Vector3(-0.4f, 0.62f, 0.35f), 0.26f, 0.8f, iron * 1.35f, new Vector3(0f, 0f, -90f));
            Art.Cyl("Bottom roll", B, new Vector3(-0.4f, 0.36f, 0.35f), 0.26f, 0.8f, iron * 1.35f, new Vector3(0f, 0f, -90f));
            Art.Cube("Cap", B, new Vector3(0f, 0.9f, 0.35f), new Vector3(1.05f, 0.08f, 0.32f), dark);
            Art.Cube("Guide table", B, new Vector3(0f, 0.33f, 0.68f), new Vector3(0.7f, 0.04f, 0.3f), iron);
            // Drive motor and flywheel on the right.
            Art.Cyl("Drive motor", B, new Vector3(0.6f, 0.45f, 0.35f), 0.34f, 0.3f, DynamoGreen, new Vector3(0f, 0f, -90f));
            var fly = new GameObject("Flywheel").transform;
            fly.SetParent(B, false);
            fly.localPosition = new Vector3(0.82f, 0.45f, 0.35f);
            Art.Cyl("Rim", fly, new Vector3(-0.02f, 0f, 0f), 0.6f, 0.05f, dark, new Vector3(0f, 0f, -90f));
            Art.Cube("Spoke", fly, Vector3.zero, new Vector3(0.04f, 0.55f, 0.06f), iron * 1.2f);
            Art.Cube("Rim mark", fly, new Vector3(0f, 0.26f, 0f), new Vector3(0.05f, 0.06f, 0.08f), Look.Accent);
            v.Anim = fly;
            // Finished plates stacked by the stand.
            for (int k = 0; k < 4; k++)
                Art.Cube("Plate", B, new Vector3(-0.72f, 0.2f + k * 0.045f, 0.62f), new Vector3(0.3f, 0.035f, 0.38f), ItemColor(Item.Plate) * (1f - 0.04f * k));
            Art.Cube("Service bracket", B, new Vector3(0.7f, 1.3f, -0.75f), new Vector3(0.05f, 1.2f, 0.05f), iron);
            Art.Cyl("Insulator", B, new Vector3(0.7f, 1.9f, -0.75f), 0.08f, 0.12f, Insulator);
            Front(R, h * T);
        }

        // ------------------------------------------------------------------ hangar

        /// <summary>A round-roofed hangar (a half cylinder of corrugated iron) with big doors and a glazed band at the front,
        /// brick end walls, a windsock mast at the corner and a finished aeroplane standing in the doorway.</summary>
        void MakeHangar(Transform R, float s, BView v)
        {
            var B = Scaled(R); s /= T;
            Color brick = Look.Brickwork, stone = Look.StoneWall, iron = Look.Iron;
            float h = s / 2f;
            Art.Cube("Apron", B, new Vector3(0f, 0.08f, 0f), new Vector3(s - 0.2f, 0.16f, s - 0.2f), stone * 0.95f);
            // Arched roof: a cylinder along z, sunk so only its top half shows.
            const float span = 2.6f, depth = 2.3f, zc = -0.15f;
            Art.Cyl("Arched roof", B, new Vector3(0f, 0.15f, zc - depth / 2f), span, depth, iron * 1.15f, new Vector3(90f, 0f, 0f));
            for (int k = 0; k < 5; k++)
                Art.Cyl("Rib", B, new Vector3(0f, 0.15f, zc - depth / 2f + 0.05f + k * (depth - 0.1f) / 4f), span + 0.06f, 0.05f, iron * 0.85f, new Vector3(90f, 0f, 0f));
            // Back wall and the front wall's door frame, glazed band and doors.
            // Brick end walls: discs on the cylinder's ends, so they follow the arch.
            Art.Cyl("Back wall", B, new Vector3(0f, 0.15f, zc - depth / 2f - 0.03f), span + 0.02f, 0.06f, brick, new Vector3(90f, 0f, 0f));
            float fz = zc + depth / 2f;
            Art.Cyl("Front wall", B, new Vector3(0f, 0.15f, fz - 0.03f), span + 0.02f, 0.06f, brick, new Vector3(90f, 0f, 0f));
            Art.Cube("Window", B, new Vector3(0f, 1.16f, fz + 0.04f), new Vector3(1.0f, 0.2f, 0.03f), new Color(0.3f, 0.37f, 0.42f));
            Art.Cube("Lintel", B, new Vector3(0f, 0.98f, fz + 0.05f), new Vector3(1.8f, 0.08f, 0.06f), stone);
            Art.Cube("Inside", B, new Vector3(0f, 0.55f, fz + 0.04f), new Vector3(0.85f, 0.8f, 0.02f), Look.MineDark * 0.6f);
            Art.Cube("Door left", B, new Vector3(-0.65f, 0.55f, fz + 0.05f), new Vector3(0.45f, 0.8f, 0.04f), iron * 0.8f);
            Art.Cube("Door right", B, new Vector3(0.65f, 0.55f, fz + 0.05f), new Vector3(0.45f, 0.8f, 0.04f), iron * 0.8f);
            // An aeroplane on show in the open doorway.
            var p = new GameObject("Plane on show").transform;
            p.SetParent(B, false);
            p.localPosition = new Vector3(0f, 0.16f, fz + 0.15f);
            p.localRotation = Quaternion.Euler(0f, -90f, 0f);   // nose out of the door (+z)
            p.localScale = Vector3.one * 0.9f;
            BuildPlane(p, PlaneSilver);
            // Windsock mast at the front corner.
            Art.Cyl("Windsock mast", B, new Vector3(1.3f, 0.16f, 1.25f), 0.04f, 1.5f, iron);
            Art.Part("Windsock", B, Art.Cone, new Vector3(1.3f, 1.6f, 1.38f), new Vector3(0.14f, 0.3f, 0.14f), new Color(0.95f, 0.5f, 0.2f), new Vector3(80f, 0f, 0f));
            Art.Cube("Service bracket", B, new Vector3(-1.3f, 1.4f, -1.3f), new Vector3(0.05f, 1.5f, 0.05f), iron);
            Art.Cyl("Insulator", B, new Vector3(-1.3f, 2.15f, -1.3f), 0.08f, 0.12f, Insulator);
            Front(R, h * T);
        }

        /// <summary>A small silver monoplane, nose toward +x: fuselage, wing with a stripe, tailplane, fin, wheels and a
        /// propeller (returned so a flying one can spin it).</summary>
        static Transform BuildPlane(Transform t, Color body)
        {
            Art.Cyl("Fuselage", t, new Vector3(-0.55f, 0.32f, 0f), 0.2f, 0.95f, body, new Vector3(0f, 0f, -90f));
            Art.Part("Nose", t, Art.Cone, new Vector3(0.4f, 0.32f, 0f), new Vector3(0.2f, 0.12f, 0.2f), body * 0.9f, new Vector3(0f, 0f, -90f));
            Art.Cube("Wing", t, new Vector3(0.1f, 0.3f, 0f), new Vector3(0.28f, 0.03f, 1.4f), body);
            Art.Cube("Wing stripe", t, new Vector3(0.1f, 0.318f, 0f), new Vector3(0.29f, 0.01f, 0.3f), PlaneStripe);
            Art.Cube("Tailplane", t, new Vector3(-0.5f, 0.35f, 0f), new Vector3(0.16f, 0.025f, 0.5f), body);
            Art.Cube("Fin", t, new Vector3(-0.52f, 0.47f, 0f), new Vector3(0.16f, 0.22f, 0.025f), PlaneStripe);
            Art.Cube("Canopy", t, new Vector3(-0.05f, 0.43f, 0f), new Vector3(0.18f, 0.08f, 0.12f), new Color(0.3f, 0.37f, 0.42f));
            for (int k = -1; k <= 1; k += 2)
            {
                Art.Cube("Strut", t, new Vector3(0.15f, 0.17f, k * 0.12f), new Vector3(0.025f, 0.18f, 0.025f), Look.MineDark);
                Art.Cyl("Wheel", t, new Vector3(0.15f, 0.07f, k * 0.12f - 0.02f), 0.12f, 0.04f, Look.MineDark, new Vector3(90f, 0f, 0f));
            }
            var prop = new GameObject("Propeller").transform;
            prop.SetParent(t, false);
            prop.localPosition = new Vector3(0.47f, 0.32f, 0f);
            Art.Cube("Blade", prop, Vector3.zero, new Vector3(0.02f, 0.5f, 0.05f), Look.Timber * 0.8f);
            return prop;
        }

        // ------------------------------------------------------------------ flights

        class Flight { public Transform Plane, Prop; public Vector3 From, Dir; public float Age; }
        readonly List<Flight> _flights = new List<Flight>();
        readonly Dictionary<int, int> _launchSeen = new Dictionary<int, int>();

        /// <summary>True while an aeroplane is in the air (the screenshot run waits for one).</summary>
        public bool PlaneInFlight => _flights.Count > 0;

        /// <summary>Each aeroplane a hangar finishes rolls out of its doors, runs along the ground in front and climbs away.</summary>
        void UpdateFlights()
        {
            foreach (var b in _w.Buildings.Values)
            {
                if (b.Type != BuildingType.Hangar || !_views.TryGetValue(b.Id, out var v)) continue;
                _launchSeen.TryGetValue(b.Id, out int seen);
                if (b.Launched > seen)
                {
                    _launchSeen[b.Id] = b.Launched;
                    if (seen == 0 && b.Launched > 1) continue;   // a loaded game: don't fly the whole history
                    var plane = new GameObject("Aeroplane").transform;
                    plane.SetParent(transform, false);
                    plane.localScale = Vector3.one * 2.2f;
                    var f = new Flight { Plane = plane, Prop = BuildPlane(plane, PlaneSilver) };
                    var fwd = v.Body.forward;
                    f.Dir = new Vector3(fwd.x, 0f, fwd.z).normalized;
                    f.From = v.Root.transform.position + f.Dir * (b.Size * T / 2f - 0.5f);
                    plane.rotation = Quaternion.LookRotation(f.Dir) * Quaternion.Euler(0f, -90f, 0f);   // model nose is +x
                    Texturize(plane);
                    _flights.Add(f);
                }
            }
            float dt = Time.deltaTime;
            for (int i = _flights.Count - 1; i >= 0; i--)
            {
                var f = _flights[i];
                f.Age += dt;
                // 0-3 s: roll and speed up along the ground; then climb at about 14 degrees, faster and faster.
                float a = f.Age, dist = a < 3f ? 1.2f * a * a : 10.8f + 7.2f * (a - 3f) + 1.5f * (a - 3f) * (a - 3f);
                float alt = a < 3f ? 0f : (dist - 10.8f) * 0.25f;
                f.Plane.position = f.From + f.Dir * dist + Vector3.up * alt;
                float pitch = a < 3f ? 0f : Mathf.Min(14f, (a - 3f) * 10f);
                f.Plane.rotation = Quaternion.LookRotation(f.Dir) * Quaternion.Euler(0f, -90f, pitch);   // nose up about the wing axis
                if (f.Prop != null) f.Prop.localRotation = Quaternion.Euler(f.Age * 2400f, 0f, 0f);
                if (a > 14f) { Destroy(f.Plane.gameObject); _flights.RemoveAt(i); }
            }
        }
    }
}
