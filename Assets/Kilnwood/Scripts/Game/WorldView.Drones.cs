using System.Collections.Generic;
using Kilnwood.Sim;
using UnityEngine;

namespace Kilnwood.Game
{
    /// <summary>
    /// 0.21 Tier 10 "Drones": the drone works (a finished drone lifts off its roof pad and flies home to the lodge), the
    /// drone port (a raised timber deck with four pads; its drones carry crates to the lodge and come back empty), and the
    /// quadcopter itself. Every crate carries its item letter, like belt items.
    /// </summary>
    public partial class WorldView
    {
        static readonly Color DroneBody = new Color(0.86f, 0.84f, 0.78f);
        static readonly Color DroneArm = new Color(0.2f, 0.2f, 0.22f);
        static readonly Color PadPaint = new Color(0.95f, 0.78f, 0.2f);

        class DroneView { public Transform Root, Cargo; public Transform[] Rotors; public TextMesh Letter; public Renderer Crate; public Item Item = Item.None; }
        class WorksFlight { public DroneView D; public Vector3 From, To; public float Age, Duration; }

        readonly Stack<DroneView> _spareDrones = new Stack<DroneView>();
        readonly List<WorksFlight> _worksFlights = new List<WorksFlight>();
        readonly Dictionary<DroneTrip, DroneView> _tripDrones = new Dictionary<DroneTrip, DroneView>();
        readonly Dictionary<int, int> _worksSeen = new Dictionary<int, int>();
        int _dronesBuilt;

        /// <summary>True while a drone from a drone works is in the air (the screenshot run waits for one).</summary>
        public bool DroneInFlight => _worksFlights.Count > 0;
        /// <summary>True while a drone port has a drone out (the screenshot run waits for one).</summary>
        public bool PortDroneOut => _tripDrones.Count > 0;

        // ------------------------------------------------------------------ the drone

        /// <summary>A quadcopter, nose toward +z: a pale body, four dark arms, motor pods with rotor discs, landing skids,
        /// and a crate slung underneath (hidden when empty).</summary>
        DroneView NewDrone()
        {
            var d = new DroneView { Root = new GameObject("Radio drone").transform };
            d.Root.SetParent(transform, false);
            var t = d.Root;
            Art.Cube("Body", t, new Vector3(0f, 0.12f, 0f), new Vector3(0.34f, 0.12f, 0.46f), DroneBody);
            Art.Cube("Canopy", t, new Vector3(0f, 0.2f, 0.05f), new Vector3(0.22f, 0.06f, 0.26f), DroneBody * 0.92f);
            Art.Cube("Camera", t, new Vector3(0f, 0.08f, 0.25f), new Vector3(0.1f, 0.08f, 0.06f), DroneArm);
            Art.Cube("Aerial", t, new Vector3(0f, 0.3f, -0.18f), new Vector3(0.015f, 0.2f, 0.015f), DroneArm);
            var rotors = new List<Transform>();
            for (int k = 0; k < 4; k++)
            {
                float sx = (k & 1) == 0 ? -1f : 1f, sz = (k & 2) == 0 ? -1f : 1f;
                var arm = new Vector3(sx * 0.36f, 0.14f, sz * 0.36f);
                Art.Cube("Arm", t, arm * 0.5f + new Vector3(0f, 0.07f, 0f), new Vector3(0.05f, 0.035f, 0.5f), DroneArm, new Vector3(0f, sx * sz * 45f, 0f));
                Art.Cyl("Motor", t, arm + new Vector3(0f, 0.02f, 0f), 0.08f, 0.08f, DroneArm);
                var rotor = new GameObject("Rotor").transform;
                rotor.SetParent(t, false);
                rotor.localPosition = arm + new Vector3(0f, 0.08f, 0f);
                Art.Cube("Blade", rotor, Vector3.zero, new Vector3(0.36f, 0.008f, 0.04f), DroneArm * 1.4f);
                Art.Cube("Blade", rotor, Vector3.zero, new Vector3(0.04f, 0.008f, 0.36f), DroneArm * 1.4f);
                rotors.Add(rotor);
                // A faint disc so the spin reads at a distance.
                var disc = Art.Cyl("Rotor disc", t, arm + new Vector3(0f, 0.075f, 0f), 0.36f, 0.004f, new Color(0.55f, 0.55f, 0.55f));
                disc.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            d.Rotors = rotors.ToArray();
            for (int k = -1; k <= 1; k += 2) Art.Cube("Skid", t, new Vector3(k * 0.13f, 0.0f, 0f), new Vector3(0.025f, 0.025f, 0.42f), DroneArm);
            // Slung crate with its letter badge (same look as belt items).
            d.Cargo = new GameObject("Crate").transform;
            d.Cargo.SetParent(t, false);
            d.Cargo.localPosition = new Vector3(0f, -0.32f, 0f);
            d.Crate = Art.Cube("Box", d.Cargo, Vector3.zero, new Vector3(0.3f, 0.26f, 0.3f), Look.Timber).GetComponent<Renderer>();
            Art.Cube("Sling", d.Cargo, new Vector3(0f, 0.2f, 0f), new Vector3(0.015f, 0.18f, 0.015f), DroneArm);
            Art.Cyl("Badge", d.Cargo, new Vector3(0f, 0.135f, 0f), 0.24f, 0.02f, new Color(0.1f, 0.1f, 0.1f));
            d.Letter = Look.Label("", d.Cargo, new Vector3(0f, 0.15f, 0f), 0.3f, Color.white);
            d.Letter.fontStyle = FontStyle.Bold;
            d.Letter.GetComponent<MeshRenderer>().sharedMaterial = ItemTextMat;
            d.Cargo.gameObject.SetActive(false);
            Texturize(t);
            t.localScale = Vector3.one * 2.2f;
            t.gameObject.SetActive(false);
            _dronesBuilt++;
            return d;
        }

        DroneView TakeDrone() { var d = _spareDrones.Count > 0 ? _spareDrones.Pop() : NewDrone(); d.Root.gameObject.SetActive(true); return d; }
        void GiveBack(DroneView d) { d.Root.gameObject.SetActive(false); _spareDrones.Push(d); }

        void SetDroneCargo(DroneView d, Item item)
        {
            bool show = item != Item.None;
            if (d.Cargo.gameObject.activeSelf != show) d.Cargo.gameObject.SetActive(show);
            if (!show || d.Item == item) return;
            d.Item = item;
            d.Crate.sharedMaterial = Look.Mat(Color.Lerp(Look.Timber, ItemColor(item), 0.55f));
            d.Letter.text = Items.Letter(item);
        }

        /// <summary>Puts a drone on the path from a to b at u (0..1): lifts off, cruises high, lets down; faces the way it flies.</summary>
        void PlaceDrone(DroneView d, Vector3 a, Vector3 b, float u, float dt)
        {
            u = Mathf.Clamp01(u);
            float lift = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(u / 0.15f)) * Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((1f - u) / 0.15f));
            float along = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.08f, 0.92f, u));
            var p = Vector3.Lerp(a, b, along);
            p.y = Mathf.Lerp(a.y, b.y, along) + lift * 9f;
            var dir = b - a; dir.y = 0f;
            d.Root.position = p;
            if (dir.sqrMagnitude > 0.01f) d.Root.rotation = Quaternion.LookRotation(dir) * Quaternion.Euler(8f * lift, 0f, 0f);   // nose down a touch in cruise
            foreach (var r in d.Rotors) r.Rotate(Vector3.up, 2200f * dt, Space.Self);
        }

        // ------------------------------------------------------------------ buildings

        /// <summary>A flat-roofed brick-and-concrete works with a big assembly door, a row of windows, a yellow-ringed
        /// launch pad on the roof with a drone sitting on it, a wind sock and a roof aerial.</summary>
        void MakeDroneWorks(Transform R, float s, BView v)
        {
            var B = Scaled(R); s /= T;
            Color brick = Look.Brickwork, iron = Look.Iron, glass = new Color(0.3f, 0.37f, 0.42f);
            float h = s / 2f;
            const float ww = 1.7f, wd = 1.5f, eave = 1.25f;
            Art.Cube("Floor", B, new Vector3(0f, 0.08f, 0f), new Vector3(s - 0.25f, 0.16f, s - 0.25f), Concrete * 0.85f);
            Art.Cube("Walls", B, new Vector3(0f, eave / 2f + 0.1f, -0.05f), new Vector3(ww, eave, wd), brick);
            Art.Cube("Concrete band", B, new Vector3(0f, eave + 0.08f, -0.05f), new Vector3(ww + 0.06f, 0.14f, wd + 0.06f), Concrete);
            Art.Cube("Roof", B, new Vector3(0f, eave + 0.17f, -0.05f), new Vector3(ww + 0.02f, 0.04f, wd + 0.02f), Concrete * 0.7f);
            Art.Cube("Door", B, new Vector3(-0.35f, 0.5f, -0.05f + wd / 2f + 0.01f), new Vector3(0.7f, 0.75f, 0.03f), iron * 0.75f);
            for (int k = 0; k < 4; k++)
                Art.Cube("Door slat", B, new Vector3(-0.35f, 0.2f + k * 0.18f, -0.05f + wd / 2f + 0.025f), new Vector3(0.7f, 0.02f, 0.02f), iron * 0.55f);
            for (int k = 0; k < 3; k++)
                Art.Cube("Window", B, new Vector3(0.28f + k * 0.2f, 0.85f, -0.05f + wd / 2f + 0.01f), new Vector3(0.14f, 0.3f, 0.03f), glass);
            // Roof pad: a dark disc with a yellow ring and an "H" mark, and a parked drone.
            float roofY = eave + 0.19f;
            Art.Cyl("Pad", B, new Vector3(0.1f, roofY, -0.1f), 1.05f, 0.02f, Look.MineDark);
            Art.Cyl("Pad ring", B, new Vector3(0.1f, roofY + 0.005f, -0.1f), 0.95f, 0.02f, PadPaint);
            Art.Cyl("Pad inner", B, new Vector3(0.1f, roofY + 0.01f, -0.1f), 0.85f, 0.02f, Look.MineDark);
            Art.Cube("H left", B, new Vector3(0.1f - 0.15f, roofY + 0.025f, -0.1f), new Vector3(0.06f, 0.01f, 0.4f), PadPaint);
            Art.Cube("H right", B, new Vector3(0.1f + 0.15f, roofY + 0.025f, -0.1f), new Vector3(0.06f, 0.01f, 0.4f), PadPaint);
            Art.Cube("H bar", B, new Vector3(0.1f, roofY + 0.025f, -0.1f), new Vector3(0.3f, 0.01f, 0.06f), PadPaint);
            Art.Cyl("Sock mast", B, new Vector3(-0.75f, roofY + 0.35f, 0.55f), 0.03f, 0.7f, iron);
            Art.Part("Wind sock", B, Art.Cone, new Vector3(-0.75f, roofY + 0.66f, 0.62f), new Vector3(0.08f, 0.18f, 0.08f), new Color(0.95f, 0.5f, 0.2f), new Vector3(80f, 0f, 0f));
            Art.Cube("Roof aerial", B, new Vector3(-0.7f, roofY + 0.45f, -0.6f), new Vector3(0.025f, 0.9f, 0.025f), iron);
            v.Extra = Art.Ball("Roof light", B, new Vector3(-0.7f, roofY + 0.92f, -0.6f), new Vector3(0.1f, 0.1f, 0.1f), MastRed);
            var lr = v.Extra.GetComponent<Renderer>();
            lr.sharedMaterial = Look.GlowMat(MastRed);
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            // A finished drone waiting on the pad (the flying ones are separate).
            var parked = new GameObject("Drone on the pad").transform;
            parked.SetParent(B, false);
            parked.localPosition = new Vector3(0.1f, roofY + 0.03f, -0.1f);
            parked.localScale = Vector3.one * 1.1f;
            BuildDroneStatic(parked);
            Art.Cube("Service bracket", B, new Vector3(0.85f, 1.3f, -0.7f), new Vector3(0.06f, 1.3f, 0.06f), iron);
            Art.Cyl("Insulator", B, new Vector3(0.85f, 2.0f, -0.7f), 0.08f, 0.12f, Insulator);
            Front(R, h * T);
        }

        /// <summary>A small fixed drone for display (no rotors to spin, no crate).</summary>
        static void BuildDroneStatic(Transform t)
        {
            Art.Cube("Body", t, new Vector3(0f, 0.12f, 0f), new Vector3(0.34f, 0.12f, 0.46f), DroneBody);
            for (int k = 0; k < 4; k++)
            {
                float sx = (k & 1) == 0 ? -1f : 1f, sz = (k & 2) == 0 ? -1f : 1f;
                var arm = new Vector3(sx * 0.36f, 0.14f, sz * 0.36f);
                Art.Cube("Arm", t, arm * 0.5f + new Vector3(0f, 0.07f, 0f), new Vector3(0.05f, 0.035f, 0.5f), DroneArm, new Vector3(0f, sx * sz * 45f, 0f));
                Art.Cyl("Motor", t, arm + new Vector3(0f, 0.02f, 0f), 0.08f, 0.08f, DroneArm);
                Art.Cube("Blade", t, arm + new Vector3(0f, 0.08f, 0f), new Vector3(0.36f, 0.008f, 0.04f), DroneArm * 1.4f, new Vector3(0f, 30f * k, 0f));
            }
            for (int k = -1; k <= 1; k += 2) Art.Cube("Skid", t, new Vector3(k * 0.13f, 0.0f, 0f), new Vector3(0.025f, 0.025f, 0.42f), DroneArm);
        }

        /// <summary>A raised timber deck on posts with four landing pads, a ramp-side intake chute at the front, crates
        /// waiting, and a little control hut with an aerial.</summary>
        void MakeDronePort(Transform R, float s, BView v)
        {
            var B = Scaled(R); s /= T;
            Color timber = Look.Timber, iron = Look.Iron;
            float h = s / 2f;
            const float deckY = 0.55f;
            for (int k = 0; k < 4; k++)
            {
                float sx = (k & 1) == 0 ? -1f : 1f, sz = (k & 2) == 0 ? -1f : 1f;
                Art.Cube("Post", B, new Vector3(sx * 0.75f, deckY / 2f, sz * 0.75f), new Vector3(0.1f, deckY, 0.1f), timber * 0.8f);
            }
            Art.Cube("Deck", B, new Vector3(0f, deckY, 0f), new Vector3(s - 0.2f, 0.08f, s - 0.2f), timber);
            for (int k = 0; k < 8; k++)
                Art.Cube("Plank line", B, new Vector3(-0.8f + k * 0.23f, deckY + 0.045f, 0f), new Vector3(0.01f, 0.005f, s - 0.25f), timber * 0.65f);
            // Four pads, one per drone.
            for (int k = 0; k < 4; k++)
            {
                float sx = (k & 1) == 0 ? -1f : 1f, sz = (k & 2) == 0 ? -1f : 1f;
                var c = new Vector3(sx * 0.45f, deckY + 0.05f, sz * 0.45f - 0.05f);
                Art.Cyl("Pad ring", B, c, 0.62f, 0.015f, PadPaint);
                Art.Cyl("Pad", B, c + new Vector3(0f, 0.005f, 0f), 0.52f, 0.015f, Look.MineDark);
            }
            // Intake chute at the front edge where belts deliver, and crates waiting.
            Art.Cube("Chute", B, new Vector3(0f, deckY * 0.6f, h - 0.15f), new Vector3(0.5f, 0.12f, 0.35f), iron, new Vector3(-25f, 0f, 0f));
            for (int k = 0; k < 3; k++)
                Art.Cube("Crate", B, new Vector3(-0.8f + 0.06f * k, deckY + 0.13f + 0.18f * (k / 2), 0.78f - 0.2f * (k % 2)), new Vector3(0.18f, 0.18f, 0.18f), timber * (0.9f + 0.05f * k));
            // Control hut with a window and an aerial.
            Art.Cube("Hut", B, new Vector3(0.78f, deckY + 0.28f, 0.75f), new Vector3(0.36f, 0.5f, 0.36f), Look.Timber * 0.75f);
            Art.Cube("Hut roof", B, new Vector3(0.78f, deckY + 0.55f, 0.75f), new Vector3(0.42f, 0.04f, 0.42f), Look.Roof);
            Art.Cube("Hut window", B, new Vector3(0.78f, deckY + 0.35f, 0.94f), new Vector3(0.2f, 0.14f, 0.02f), new Color(0.3f, 0.37f, 0.42f));
            Art.Cube("Aerial", B, new Vector3(0.88f, deckY + 0.95f, 0.68f), new Vector3(0.02f, 0.8f, 0.02f), iron);
            v.Extra = Art.Ball("Aerial light", B, new Vector3(0.88f, deckY + 1.37f, 0.68f), new Vector3(0.08f, 0.08f, 0.08f), MastRed);
            var lr = v.Extra.GetComponent<Renderer>();
            lr.sharedMaterial = Look.GlowMat(MastRed);
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            Front(R, h * T);
        }

        // ------------------------------------------------------------------ flights

        /// <summary>Works: each finished drone lifts off the roof pad and flies to the lodge. Ports: one drone per trip,
        /// out with a lettered crate, back empty. Lights on both blink while they have drones about.</summary>
        void UpdateDroneViews()
        {
            float dt = Time.deltaTime, now = Time.time;
            var lodge = _w.Lodge != null ? FootCenter(_w.Lodge.X, _w.Lodge.Y, _w.Lodge.Size) : Vector3.zero;
            lodge.y = GroundHeight(lodge.x, lodge.z) + 3.2f;   // over the lodge roof
            HashSet<DroneTrip> live = null;
            foreach (var b in _w.Buildings.Values)
            {
                if (b.Type != BuildingType.DroneWorks && b.Type != BuildingType.DronePort) continue;
                if (!_views.TryGetValue(b.Id, out var v)) continue;
                var at = FootCenter(b.X, b.Y, b.Size);
                if (b.Type == BuildingType.DroneWorks)
                {
                    if (_dronesBuilt == 0) GiveBack(NewDrone());   // one ready before the first launch
                    _worksSeen.TryGetValue(b.Id, out int seen);
                    if (b.Launched > seen)
                    {
                        _worksSeen[b.Id] = b.Launched;
                        if (!(seen == 0 && b.Launched > 1) && _worksFlights.Count < 6)   // a loaded game doesn't fly its whole history
                        {
                            var from = at; from.y = GroundHeight(at.x, at.z) + 2.9f;
                            float dist = Vector3.Distance(new Vector3(from.x, 0f, from.z), new Vector3(lodge.x, 0f, lodge.z)) / T;
                            var d = TakeDrone();
                            SetDroneCargo(d, Item.None);
                            _worksFlights.Add(new WorksFlight { D = d, From = from, To = lodge, Duration = Mathf.Max(4f, dist / _w.B.DroneSpeed + 2f) });
                        }
                    }
                    if (v.Extra != null) { bool on = b.Elec > 0f && Mathf.Repeat(now + v.Seed, 1.2f) < 0.6f; if (v.Extra.activeSelf != on) v.Extra.SetActive(on); }
                }
                else
                {
                    var pad = at; pad.y = GroundHeight(at.x, at.z) + 1.3f;
                    foreach (var trip in b.Trips)
                    {
                        (live ??= new HashSet<DroneTrip>()).Add(trip);
                        if (!_tripDrones.TryGetValue(trip, out var d)) { d = TakeDrone(); _tripDrones[trip] = d; }
                        float u = trip.T / Mathf.Max(0.01f, trip.Duration);
                        if (u < 0.5f) { SetDroneCargo(d, trip.Item); PlaceDrone(d, pad, lodge, u * 2f, dt); }
                        else { SetDroneCargo(d, Item.None); PlaceDrone(d, lodge, pad, u * 2f - 1f, dt); }
                    }
                    if (v.Extra != null) { bool on = b.Drones > 0 && Mathf.Repeat(now + v.Seed, 1.4f) < 0.7f; if (v.Extra.activeSelf != on) v.Extra.SetActive(on); }
                }
            }
            // Trips that ended (or ports that went away): their drones go back in the pool.
            if (_tripDrones.Count > 0)
            {
                List<DroneTrip> done = null;
                foreach (var kv in _tripDrones) if (live == null || !live.Contains(kv.Key)) (done ??= new List<DroneTrip>()).Add(kv.Key);
                if (done != null) foreach (var t in done) { GiveBack(_tripDrones[t]); _tripDrones.Remove(t); }
            }
            for (int i = _worksFlights.Count - 1; i >= 0; i--)
            {
                var f = _worksFlights[i];
                f.Age += dt;
                PlaceDrone(f.D, f.From, f.To, f.Age / f.Duration, dt);
                if (f.Age >= f.Duration) { GiveBack(f.D); _worksFlights.RemoveAt(i); }
            }
        }
    }
}
