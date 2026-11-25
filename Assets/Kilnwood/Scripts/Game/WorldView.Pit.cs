using System.Collections.Generic;
using Kilnwood.Sim;
using UnityEngine;

namespace Kilnwood.Game
{
    /// <summary>0.5 "Open Pit" visuals: dig/dump marks, the area ghost, excavators and trucks, garage and hopper models.</summary>
    public partial class WorldView
    {
        GroundView _ground;

        public float GroundHeight(float wx, float wz) => _ground != null ? _ground.HeightAt(wx, wz) : 0f;

        // ------------------------------------------------------------------ marks

        static readonly Color DigCol = new Color(0.98f, 0.62f, 0.2f);
        static readonly Color DumpCol = new Color(0.45f, 0.7f, 0.95f);
        readonly Dictionary<int, GameObject> _marks = new Dictionary<int, GameObject>();
        readonly List<GameObject> _markLabels = new List<GameObject>();
        Transform _markRoot;

        /// <summary>A small arrow on each marked tile: pointing down = dig, pointing up = dump (shape, not just colour),
        /// plus one "DIG" / "DUMP" word over each marked area.</summary>
        void UpdateMarks()
        {
            if (_markRoot == null) { _markRoot = new GameObject("Marks").transform; _markRoot.SetParent(transform, false); }
            bool changedSet = false;
            for (int i = 0; i < _w.DigMark.Length; i++)
            {
                bool dig = _w.DigMark[i], dump = _w.DumpMark[i];
                _marks.TryGetValue(i, out var go);
                int want = dig ? 1 : dump ? 2 : 0;
                int have = go == null ? 0 : go.name == "Dig mark" ? 1 : 2;
                if (want != have)
                {
                    changedSet = true;
                    if (go != null) { Destroy(go); _marks.Remove(i); }
                    if (want != 0)
                    {
                        go = new GameObject(want == 1 ? "Dig mark" : "Dump mark");
                        go.transform.SetParent(_markRoot, false);
                        // A fat arrow: head plus shaft. Down = dig, up = dump.
                        var col = want == 1 ? DigCol : DumpCol;
                        float dir = want == 1 ? -1f : 1f;
                        Art.Part("Arrow head", go.transform, Art.Cone, Vector3.zero, new Vector3(1.0f, 0.7f, 1.0f), col,
                            want == 1 ? new Vector3(180f, 0f, 0f) : Vector3.zero, false);
                        Art.Part("Arrow shaft", go.transform, Art.Prim(PrimitiveType.Cylinder), new Vector3(0f, -dir * 0.35f, 0f), new Vector3(0.38f, 0.35f, 0.38f), col, default, false);
                        _marks[i] = go;
                    }
                }
                if (go != null && want != 0)
                {
                    var c = TileCenter(i % _w.W, i / _w.W);
                    float y = GroundHeight(c.x, c.z);
                    go.transform.localPosition = new Vector3(c.x, y + (want == 1 ? 1.1f : 0.75f), c.z);
                }
            }
            if (changedSet) RebuildMarkLabels();
        }

        void RebuildMarkLabels()
        {
            foreach (var g in _markLabels) if (g != null) Destroy(g);
            _markLabels.Clear();
            var seen = new bool[_w.DigMark.Length];
            var stack = new Stack<int>();
            for (int s = 0; s < seen.Length; s++)
            {
                if (seen[s] || (!_w.DigMark[s] && !_w.DumpMark[s])) continue;
                bool dig = _w.DigMark[s];
                // Flood fill the area of the same mark; label at its middle.
                float sx = 0f, sy = 0f; int n = 0;
                stack.Push(s); seen[s] = true;
                while (stack.Count > 0)
                {
                    int c = stack.Pop();
                    sx += c % _w.W; sy += c / _w.W; n++;
                    for (int d = 0; d < 4; d++)
                    {
                        int nx = c % _w.W + Dir.DX[d], ny = c / _w.W + Dir.DY[d];
                        if (!_w.InBounds(nx, ny)) continue;
                        int k = ny * _w.W + nx;
                        if (seen[k] || (dig ? !_w.DigMark[k] : !_w.DumpMark[k])) continue;
                        seen[k] = true;
                        stack.Push(k);
                    }
                }
                var pos = new Vector3((sx / n + 0.5f) * T, 3.2f, (sy / n + 0.5f) * T);
                var label = Look.Billboard(dig ? "DIG" : "DUMP", transform, pos, 1.6f, dig ? new Color(1f, 0.85f, 0.6f) : new Color(0.75f, 0.88f, 1f));
                _markLabels.Add(label.gameObject);
            }
        }

        // ------------------------------------------------------------------ area ghost (dig / dump drag)

        GameObject _area;
        TextMesh _areaText;

        public void ShowAreaGhost(int x0, int y0, int x1, int y1, bool dig, string note)
        {
            HideGhost();
            if (_area == null)
            {
                _area = new GameObject("Area ghost");
                _area.transform.SetParent(transform, false);
                Look.Box("Fill", _area.transform, Vector3.zero, Vector3.one, DigCol);
                _areaText = Look.Label("", _area.transform, new Vector3(0f, 3f, 0f), 1.4f, Color.white);
            }
            _area.SetActive(true);
            int ax = Mathf.Min(x0, x1), bx = Mathf.Max(x0, x1), ay = Mathf.Min(y0, y1), by = Mathf.Max(y0, y1);
            float w = (bx - ax + 1) * T, h = (by - ay + 1) * T;
            _area.transform.localPosition = new Vector3(ax * T + w / 2f, 0f, ay * T + h / 2f);
            var fill = _area.transform.GetChild(0);
            fill.localScale = new Vector3(w - 0.2f, 0.12f, h - 0.2f);
            fill.localPosition = new Vector3(0f, 0.1f, 0f);
            fill.GetComponent<Renderer>().sharedMaterial = Look.Mat(note != null && x0 == x1 && y0 == y1 ? Look.Bad : dig ? DigCol : DumpCol);
            int tiles = (bx - ax + 1) * (by - ay + 1);
            _areaText.text = (dig ? "DIG " : "DUMP ") + (bx - ax + 1) + " x " + (by - ay + 1) + (note != null ? "\n" + note : tiles == 1 ? "\ndrag to mark an area" : "\nrelease to mark");
        }

        void HideArea() { if (_area != null) _area.SetActive(false); }

        // ------------------------------------------------------------------ vehicles

        class VView
        {
            public Transform Root, Boom, Arm, Bucket, Bed, Cargo;
            public Transform[] Wheels;
            public TextMesh CargoLetter;
            public GameObject CargoBadge;
            public Item CargoItem = Item.None;
            public ParticleSystem Exhaust;
            public float Phase;
            public float HeapH;
            public float Yaw;
            public Vector2 Nudge;     // visual-only push away from a neighbouring machine while parked
            public int Era;           // 0.20: which era this model was built for (rebuilt when the era changes)
        }

        readonly Dictionary<int, VView> _vviews = new Dictionary<int, VView>();
        int _vehicleIconEra = -1;
        static readonly Color MachineOrange = new Color(0.93f, 0.55f, 0.16f);
        static readonly Color MachineDark = new Color(0.16f, 0.15f, 0.14f);
        static readonly Color Glass = new Color(0.2f, 0.27f, 0.32f);

        void UpdateVehicles()
        {
            UpdateTrains();
            foreach (var v in _w.Vehicles)
                if (v.Type != VehicleType.Train && !_vviews.ContainsKey(v.Id)) _vviews[v.Id] = v.Type == VehicleType.Excavator ? MakeExcavator() : MakeTruck();
            List<int> gone = null;
            // 0.20: a new era refits the whole fleet (and the buy-button pictures).
            foreach (var kv in _vviews)
                if (_w.VehicleById(kv.Key) == null || kv.Value.Era != _w.Era) (gone ??= new List<int>()).Add(kv.Key);
            if (_vehicleIconEra != _w.Era) { bool had = _vehicleIconEra >= 0 && _vehicleIconsTried; _vehicleIconEra = _w.Era; if (had) { _vehicleIconsTried = false; MakeVehicleIcons(); } }
            if (gone != null)
            {
                foreach (int id in gone) { Destroy(_vviews[id].Root.gameObject); _vviews.Remove(id); }
                foreach (var v in _w.Vehicles)
                    if (v.Type != VehicleType.Train && !_vviews.ContainsKey(v.Id)) _vviews[v.Id] = v.Type == VehicleType.Excavator ? MakeExcavator() : MakeTruck();
            }

            float dt = Time.deltaTime;
            foreach (var v in _w.Vehicles)
            {
                if (v.Type == VehicleType.Train) continue;
                var vv = _vviews[v.Id];
                // Parked machines ease apart a little from any machine they sit next to, so bodies never overlap
                // (0.20: excavators from excavators and trucks from trucks too, not only trucks from excavators).
                var want = Vector2.zero;
                if (v.State != VehicleState.Driving)
                    foreach (var o in _w.Vehicles)
                    {
                        if (o == v || o.Type == VehicleType.Train) continue;
                        var d = new Vector2(v.X - o.X, v.Y - o.Y);
                        float m = d.magnitude;
                        float clear = v.Type == VehicleType.Excavator && o.Type == VehicleType.Excavator ? 3.0f : 2.0f;   // 0.21.1: shovels need room for the swinging house
                        if (m < 1e-3f) d = new Vector2(Mathf.Sin(v.Id * 2.4f), Mathf.Cos(v.Id * 2.4f)) * 1e-3f;   // same spot: split by id
                        m = d.magnitude;
                        if (m < clear) want += d / m * Mathf.Min(1.2f, clear - m) * (v.Type == VehicleType.Excavator && o.Type != VehicleType.Excavator ? 0.3f : 1f);
                    }
                vv.Nudge = Vector2.Lerp(vv.Nudge, Vector2.ClampMagnitude(want, 1.2f), 1f - Mathf.Exp(-4f * dt));
                float wx = (v.X + vv.Nudge.x) * T, wz = (v.Y + vv.Nudge.y) * T;
                // Lean with the slope: compare the ground ahead and behind.
                var fwd = Quaternion.Euler(0f, v.Heading, 0f) * Vector3.forward;
                // Stand on the highest of the centre and the four ends of the tracks / wheels: a machine digging a single
                // deep tile straddles the hole instead of sinking into it (0.20: steam shovels vanished into the pit floor).
                var side = new Vector3(fwd.z, 0f, -fwd.x);
                float y = GroundHeight(wx, wz);
                for (int k = 0; k < 4; k++)
                {
                    var o = fwd * ((k & 1) == 0 ? 0.95f : -0.95f) + side * ((k & 2) == 0 ? 0.6f : -0.6f);
                    y = Mathf.Max(y, GroundHeight(wx + o.x, wz + o.z) - 0.1f);
                }
                // 0.20.2: each end rests on the higher of its two corners (like the straddle above), and the lean is
                // limited to 12 degrees, so a machine bridging the edge of a dug hole doesn't tip 30-40 degrees.
                float ahead = Mathf.Max(GroundHeight(wx + fwd.x * 1.2f + side.x * 0.6f, wz + fwd.z * 1.2f + side.z * 0.6f), GroundHeight(wx + fwd.x * 1.2f - side.x * 0.6f, wz + fwd.z * 1.2f - side.z * 0.6f));
                float behind = Mathf.Max(GroundHeight(wx - fwd.x * 1.2f + side.x * 0.6f, wz - fwd.z * 1.2f + side.z * 0.6f), GroundHeight(wx - fwd.x * 1.2f - side.x * 0.6f, wz - fwd.z * 1.2f - side.z * 0.6f));
                float pitch = Mathf.Clamp(-Mathf.Atan2(ahead - behind, 2.4f) * Mathf.Rad2Deg, -12f, 12f);
                vv.Yaw = Mathf.LerpAngle(vv.Yaw, v.Heading, 1f - Mathf.Exp(-10f * dt));
                vv.Root.localPosition = new Vector3(wx, y, wz);
                vv.Root.localRotation = Quaternion.Euler(pitch, vv.Yaw, 0f);
                bool moving = v.State == VehicleState.Driving;
                if (vv.Exhaust != null) SetSmoke(vv.Exhaust, moving || v.State == VehicleState.Digging);
                if (vv.Wheels != null && moving)
                {
                    float spin = (v.Type == VehicleType.Truck ? _w.B.TruckSpeed : _w.B.ExcavatorSpeed) * _w.B.EraWork[_w.Era] * T / (Mathf.PI * 0.9f) * 360f * dt;
                    foreach (var wh in vv.Wheels) wh.Rotate(Vector3.up, spin, Space.Self);
                }

                if (v.Type == VehicleType.Excavator)
                {
                    // Scoop: boom dips, arm curls, bucket tips, while digging; rests up otherwise.
                    if (v.State == VehicleState.Digging && v.Status.StartsWith("Digging")) vv.Phase += dt * Mathf.PI * 2f / _w.DigTime;
                    float p = v.State == VehicleState.Digging && v.Status.StartsWith("Digging") ? vv.Phase : 0f;
                    float s = Mathf.Sin(p);
                    vv.Boom.localRotation = Quaternion.Euler(-35f + 22f * s, 0f, 0f);
                    vv.Arm.localRotation = Quaternion.Euler(70f - 30f * s, 0f, 0f);
                    vv.Bucket.localRotation = Quaternion.Euler(-40f + 60f * Mathf.Sin(p - 0.8f), 0f, 0f);
                    SetCargo(vv, v.BucketCount > 0 ? v.Bucket : Item.None, v.BucketCount / (float)_w.B.BucketCap);
                }
                else
                {
                    float tip = v.State == VehicleState.Tipping ? Mathf.Clamp01(v.Timer / (_w.B.TipSeconds * 0.6f)) : 0f;
                    vv.Bed.localRotation = Quaternion.Euler(-48f * tip, 0f, 0f);
                    SetCargo(vv, v.LoadCount > 0 ? v.Load : Item.None, v.LoadCount / (float)_w.TruckCapacity);
                }
            }
        }

        /// <summary>The heap in a truck bed (or excavator bucket): its size shows how full, and a letter badge says what it is.</summary>
        void SetCargo(VView vv, Item item, float fill)
        {
            bool show = item != Item.None && fill > 0f;
            if (vv.Cargo.gameObject.activeSelf != show) vv.Cargo.gameObject.SetActive(show);
            if (!show) return;
            if (vv.CargoItem != item)
            {
                vv.CargoItem = item;
                vv.Cargo.GetChild(0).GetComponent<Renderer>().sharedMaterial = Look.Mat(item == Item.Dirt ? new Color(0.45f, 0.33f, 0.22f) : ItemColor(item));
                if (vv.CargoLetter != null) vv.CargoLetter.text = Items.Letter(item);
            }
            var heap = vv.Cargo.GetChild(0);
            float h = vv.HeapH * Mathf.Lerp(0.25f, 1f, fill);
            heap.localScale = new Vector3(heap.localScale.x, h, heap.localScale.z);
            if (vv.CargoBadge != null)
            {
                vv.CargoBadge.transform.localPosition = new Vector3(0f, h, 0f);
                vv.CargoLetter.transform.localPosition = new Vector3(0f, h + 0.06f, 0f);
            }
        }

        // 0.20 machine eras: the same rig (tracks, turning body, boom, arm, bucket / chassis, cab, tipping bed) dressed
        // for its era. Era 0 steam shovel and chain-drive lorry, era 1 diesel shovel and tipper truck, era 2 hydraulic
        // excavator and dump truck. Every era keeps the load heap and its letter badge.
        static readonly Color SteamRed = new Color(0.42f, 0.16f, 0.11f);
        static readonly Color DieselYellow = new Color(0.86f, 0.66f, 0.18f);
        static readonly Color LorryRed = new Color(0.5f, 0.15f, 0.1f);
        static readonly Color DumpYellow = new Color(0.95f, 0.74f, 0.14f);

        VView MakeExcavator(int era = -1)
        {
            if (era < 0) era = _w.Era;
            var vv = new VView { Era = era };
            vv.Root = new GameObject(World.VehicleName(VehicleType.Excavator, era)).transform;
            vv.Root.SetParent(transform, false);
            var r = vv.Root;
            var iron = new Color(0.3f, 0.3f, 0.3f);
            // Tracks and undercarriage (steam shovels ran on heavy plate crawlers too).
            for (int side = -1; side <= 1; side += 2)
            {
                Art.Cube("Track", r, new Vector3(side * 0.62f, 0.28f, 0f), new Vector3(0.42f, 0.5f, 2.1f), MachineDark);
                for (int k = -1; k <= 1; k++) Art.Cyl("Roller", r, new Vector3(side * 0.62f, 0.28f, k * 0.75f), 0.36f, 0.46f, iron, new Vector3(0f, 0f, 90f));
            }
            Art.Cube("Frame", r, new Vector3(0f, 0.45f, 0f), new Vector3(0.9f, 0.25f, 1.4f), MachineDark);
            var body = new GameObject("Body").transform;
            body.SetParent(r, false);
            body.localPosition = new Vector3(0f, 0.6f, 0f);
            Color paint = era == 0 ? SteamRed : era == 1 ? DieselYellow : MachineOrange;
            if (era == 0)
            {
                // A timber-clad machinery house with an iron roof, an upright boiler and its tall stack.
                Art.Cube("House", body, new Vector3(0f, 0.55f, -0.2f), new Vector3(1.5f, 1.1f, 1.7f), Look.Timber);
                Art.Cube("Roof", body, new Vector3(0f, 1.15f, -0.2f), new Vector3(1.65f, 0.08f, 1.85f), MachineDark);
                Art.Cube("Sill", body, new Vector3(0f, 0.05f, -0.2f), new Vector3(1.6f, 0.12f, 1.8f), paint);
                Art.Cube("Door", body, new Vector3(-0.76f, 0.5f, 0.2f), new Vector3(0.03f, 0.75f, 0.45f), paint);
                Art.Cyl("Boiler", body, new Vector3(0.3f, 0.75f, -0.7f), 0.6f, 1.2f, MachineDark);
                Art.Cyl("Stack", body, new Vector3(0.3f, 1.65f, -0.7f), 0.2f, 1.0f, MachineDark);
                Art.Cyl("Stack cap", body, new Vector3(0.3f, 2.15f, -0.7f), 0.3f, 0.08f, MachineDark);
                Art.Cyl("Coal box", body, new Vector3(-0.35f, 0.25f, -1.1f), 0.5f, 0.5f, iron);
                vv.Exhaust = Art.Smoke(body, new Vector3(0.3f, 2.25f, -0.7f), 0.6f);
                { var main = vv.Exhaust.main; main.startSizeMultiplier = 0.6f; main.startLifetimeMultiplier = 0.8f; }
                // Boom: two lattice rails, a cable sheave at the head; dipper stick in timber; a riveted dipper.
                vv.Boom = new GameObject("Boom").transform;
                vv.Boom.SetParent(body, false);
                vv.Boom.localPosition = new Vector3(0.1f, 0.4f, 0.65f);
                for (int sd = -1; sd <= 1; sd += 2) Art.Cube("Boom rail", vv.Boom, new Vector3(sd * 0.14f, 0f, 1.0f), new Vector3(0.06f, 0.1f, 2.1f), paint);
                for (int k = 0; k < 5; k++) Art.Cube("Lacing", vv.Boom, new Vector3(0f, 0f, 0.2f + k * 0.42f), new Vector3(0.3f, 0.04f, 0.04f), paint, new Vector3(0f, 35f * (k % 2 == 0 ? 1 : -1), 0f));
                Art.Cyl("Sheave", vv.Boom, new Vector3(0f, 0f, 2.05f), 0.32f, 0.08f, iron, new Vector3(0f, 0f, 90f));
                Art.Cyl("Hoist cable", vv.Boom, new Vector3(0f, 0.25f, 1.0f), 0.025f, 2.1f, new Color(0.15f, 0.15f, 0.15f), new Vector3(90f, 0f, 0f));
                vv.Arm = new GameObject("Dipper stick").transform;
                vv.Arm.SetParent(vv.Boom, false);
                vv.Arm.localPosition = new Vector3(0f, 0f, 2.0f);
                Art.Cube("Stick", vv.Arm, new Vector3(0f, 0f, 0.75f), new Vector3(0.16f, 0.18f, 1.5f), Look.Timber * 0.8f);
                vv.Bucket = new GameObject("Dipper").transform;
                vv.Bucket.SetParent(vv.Arm, false);
                vv.Bucket.localPosition = new Vector3(0f, 0f, 1.5f);
                Art.Cube("Dipper back", vv.Bucket, new Vector3(0f, -0.15f, 0.12f), new Vector3(0.62f, 0.45f, 0.08f), iron);
                Art.Cube("Dipper floor", vv.Bucket, new Vector3(0f, -0.38f, 0.35f), new Vector3(0.62f, 0.08f, 0.5f), iron);
                for (int sd = -1; sd <= 1; sd += 2) Art.Cube("Dipper side", vv.Bucket, new Vector3(sd * 0.3f, -0.2f, 0.35f), new Vector3(0.04f, 0.38f, 0.5f), iron * 0.9f);
                for (int k = -1; k <= 1; k++) Art.Cube("Tooth", vv.Bucket, new Vector3(k * 0.2f, -0.38f, 0.65f), new Vector3(0.07f, 0.05f, 0.15f), new Color(0.55f, 0.55f, 0.55f));
            }
            else
            {
                // Diesel shovel (era 1): a steel house and a box boom; hydraulic excavator (era 2): cab, counterweight, rams.
                Art.Cube("Deck", body, new Vector3(0f, 0.3f, -0.15f), new Vector3(1.55f, 0.6f, 1.7f), paint);
                Art.Cube("Counterweight", body, new Vector3(0f, 0.35f, -1.05f), new Vector3(1.5f, 0.55f, 0.35f), paint * 0.8f);
                Art.Cube("Cab", body, new Vector3(-0.38f, 0.95f, 0.25f), new Vector3(0.72f, 0.75f, 0.8f), paint);
                Art.Cube("Window", body, new Vector3(-0.38f, 1.02f, 0.66f), new Vector3(0.6f, 0.5f, 0.03f), Glass);
                Art.Cube("Side window", body, new Vector3(-0.75f, 1.02f, 0.25f), new Vector3(0.03f, 0.45f, 0.6f), Glass);
                if (era == 1)
                {
                    Art.Cube("Engine house", body, new Vector3(0.35f, 0.85f, -0.45f), new Vector3(0.8f, 0.55f, 1.0f), paint * 0.92f);
                    Art.Cube("Louvres", body, new Vector3(0.76f, 0.85f, -0.45f), new Vector3(0.02f, 0.3f, 0.7f), MachineDark);
                    Art.Cube("A-frame", body, new Vector3(0.1f, 1.45f, 0.35f), new Vector3(0.1f, 0.9f, 0.1f), paint * 0.85f, new Vector3(25f, 0f, 0f));
                }
                Art.Cyl("Exhaust pipe", body, new Vector3(0.5f, 0.6f, -0.7f), 0.1f, era == 1 ? 0.9f : 0.6f, MachineDark);
                float ey = era == 1 ? 1.55f : 1.25f;
                vv.Exhaust = Art.Smoke(body, new Vector3(0.5f, ey, -0.7f), 0.35f);
                { var main = vv.Exhaust.main; main.startSizeMultiplier = 0.35f; main.startLifetimeMultiplier = 0.5f; }
                vv.Boom = new GameObject("Boom").transform;
                vv.Boom.SetParent(body, false);
                vv.Boom.localPosition = new Vector3(0.3f, 0.55f, 0.55f);
                Art.Cube("Boom beam", vv.Boom, new Vector3(0f, 0f, 1.0f), new Vector3(0.24f, 0.28f, 2.1f), paint);
                if (era == 2) Art.Cyl("Ram", vv.Boom, new Vector3(0f, -0.25f, 0.2f), 0.1f, 1.2f, new Color(0.75f, 0.75f, 0.75f), new Vector3(60f, 0f, 0f));
                else Art.Cyl("Hoist cable", vv.Boom, new Vector3(0f, 0.25f, 1.0f), 0.025f, 2.1f, new Color(0.15f, 0.15f, 0.15f), new Vector3(90f, 0f, 0f));
                vv.Arm = new GameObject("Arm").transform;
                vv.Arm.SetParent(vv.Boom, false);
                vv.Arm.localPosition = new Vector3(0f, 0f, 2.0f);
                Art.Cube("Arm beam", vv.Arm, new Vector3(0f, 0f, 0.75f), new Vector3(0.2f, 0.22f, 1.5f), paint * 0.92f);
                if (era == 2) Art.Cyl("Arm ram", vv.Arm, new Vector3(0f, 0.2f, 0.5f), 0.08f, 0.9f, new Color(0.75f, 0.75f, 0.75f), new Vector3(90f, 0f, 0f));
                vv.Bucket = new GameObject("Bucket").transform;
                vv.Bucket.SetParent(vv.Arm, false);
                vv.Bucket.localPosition = new Vector3(0f, 0f, 1.5f);
                Art.Cube("Bucket back", vv.Bucket, new Vector3(0f, -0.15f, 0.12f), new Vector3(0.6f, 0.4f, 0.08f), MachineDark);
                Art.Cube("Bucket floor", vv.Bucket, new Vector3(0f, -0.35f, 0.35f), new Vector3(0.6f, 0.08f, 0.5f), MachineDark);
                for (int k = -1; k <= 1; k++) Art.Cube("Tooth", vv.Bucket, new Vector3(k * 0.2f, -0.35f, 0.65f), new Vector3(0.07f, 0.05f, 0.15f), new Color(0.6f, 0.6f, 0.6f));
            }
            // What's in the bucket.
            vv.Cargo = new GameObject("Load").transform;
            vv.Cargo.SetParent(vv.Bucket, false);
            vv.Cargo.localPosition = new Vector3(0f, -0.3f, 0.35f);
            Art.Part("Heap", vv.Cargo, Art.Rock, Vector3.zero, new Vector3(0.5f, 0.3f, 0.4f), Color.gray);
            vv.HeapH = 0.3f;
            vv.Cargo.gameObject.SetActive(false);
            return vv;
        }

        VView MakeTruck(int era = -1)
        {
            if (era < 0) era = _w.Era;
            var vv = new VView { Era = era };
            vv.Root = new GameObject(World.VehicleName(VehicleType.Truck, era)).transform;
            vv.Root.SetParent(transform, false);
            var r = vv.Root;
            Color paint = era == 0 ? LorryRed : era == 1 ? new Color(0.25f, 0.42f, 0.3f) : DumpYellow;
            float wheelD = era == 0 ? 0.8f : era == 1 ? 0.9f : 1.15f, wheelW = era == 0 ? 0.1f : era == 2 ? 0.24f : 0.14f;
            float axleY = wheelD / 2f;
            Art.Cube("Chassis", r, new Vector3(0f, axleY + 0.1f, 0f), new Vector3(era == 2 ? 1.1f : 0.9f, 0.22f, 2.6f), MachineDark);
            if (era == 0)
            {
                // Open cab on posts behind a long bonnet and a brass-edged radiator.
                Art.Cube("Bonnet", r, new Vector3(0f, 0.85f, 1.05f), new Vector3(0.7f, 0.45f, 0.55f), paint);
                Art.Cube("Radiator", r, new Vector3(0f, 0.88f, 1.34f), new Vector3(0.66f, 0.52f, 0.05f), new Color(0.62f, 0.5f, 0.25f));
                Art.Cube("Seat box", r, new Vector3(0f, 0.85f, 0.55f), new Vector3(1.0f, 0.45f, 0.45f), paint);
                for (int sd = -1; sd <= 1; sd += 2)
                    for (int fz = 0; fz < 2; fz++)
                        Art.Cube("Post", r, new Vector3(sd * 0.46f, 1.35f, 0.38f + fz * 0.38f), new Vector3(0.04f, 0.6f, 0.04f), MachineDark);
                Art.Cube("Canopy", r, new Vector3(0f, 1.67f, 0.57f), new Vector3(1.05f, 0.05f, 0.6f), MachineDark);
                Art.Cyl("Lamp", r, new Vector3(0.38f, 1.0f, 1.36f), 0.14f, 0.1f, new Color(0.62f, 0.5f, 0.25f), new Vector3(90f, 0f, 0f));
            }
            else if (era == 1)
            {
                Art.Cube("Cab", r, new Vector3(0f, 1.05f, 0.95f), new Vector3(1.05f, 0.85f, 0.7f), paint);
                Art.Cube("Windscreen", r, new Vector3(0f, 1.2f, 1.31f), new Vector3(0.9f, 0.4f, 0.03f), Glass);
                Art.Cube("Bumper", r, new Vector3(0f, 0.55f, 1.33f), new Vector3(1.0f, 0.18f, 0.06f), new Color(0.6f, 0.6f, 0.6f));
            }
            else
            {
                // Big off-road dump truck: a cab offset to one side on a deck over the engine, steps, a wide grille.
                Art.Cube("Engine deck", r, new Vector3(0f, 1.0f, 1.0f), new Vector3(1.25f, 0.4f, 0.7f), paint);
                Art.Cube("Grille", r, new Vector3(0f, 0.95f, 1.36f), new Vector3(1.0f, 0.32f, 0.04f), MachineDark);
                Art.Cube("Cab", r, new Vector3(-0.32f, 1.5f, 0.95f), new Vector3(0.55f, 0.6f, 0.6f), paint);
                Art.Cube("Windscreen", r, new Vector3(-0.32f, 1.58f, 1.26f), new Vector3(0.48f, 0.34f, 0.03f), Glass);
                Art.Cube("Rail", r, new Vector3(0f, 1.24f, 1.32f), new Vector3(1.25f, 0.04f, 0.04f), MachineDark);
                for (int k = 0; k < 3; k++) Art.Cube("Step", r, new Vector3(0.55f, 0.45f + k * 0.22f, 1.3f), new Vector3(0.18f, 0.03f, 0.12f), MachineDark);
            }
            float pipeY = era == 2 ? 1.45f : 0.9f;
            Art.Cyl("Exhaust pipe", r, new Vector3(0.45f, pipeY, 0.55f), 0.08f, era == 0 ? 0.5f : 0.9f, MachineDark);
            vv.Exhaust = Art.Smoke(r, new Vector3(0.45f, pipeY + (era == 0 ? 0.3f : 0.95f), 0.55f), 0.3f);
            { var main = vv.Exhaust.main; main.startSizeMultiplier = 0.3f; main.startLifetimeMultiplier = 0.5f; }
            var wheels = new List<Transform>();
            var zs = era == 2 ? new[] { 0.85f, -0.75f } : era == 0 ? new[] { 0.85f, -0.75f } : new[] { 0.85f, -0.35f, -0.95f };
            foreach (float z in zs)
                for (int side = -1; side <= 1; side += 2)
                {
                    var wpivot = new GameObject("Wheel").transform;
                    wpivot.SetParent(r, false);
                    wpivot.localPosition = new Vector3(side * (era == 2 ? 0.62f : 0.52f), axleY, z);
                    wpivot.localRotation = Quaternion.Euler(0f, 0f, 90f);
                    Art.Part("Tyre", wpivot, Art.Prim(PrimitiveType.Cylinder), Vector3.zero, new Vector3(wheelD, wheelW, wheelD), MachineDark);
                    Art.Part("Hub", wpivot, Art.Prim(PrimitiveType.Cylinder), new Vector3(0f, -0.02f * side, 0f), new Vector3(wheelD * 0.45f, wheelW + 0.01f, wheelD * 0.45f), era == 0 ? Look.Timber : new Color(0.65f, 0.6f, 0.5f));
                    if (era == 0)
                        for (int k = 0; k < 4; k++)   // wooden spokes
                            Art.Part("Spoke", wpivot, Art.Prim(PrimitiveType.Cube), new Vector3(0f, -0.03f * side, 0f), new Vector3(wheelD * 0.85f, 0.03f, 0.05f), Look.Timber, new Vector3(0f, k * 45f, 0f));
                    wheels.Add(wpivot);
                }
            vv.Wheels = wheels.ToArray();
            // Bed hinged at the back so it tips up. Era 0 is planked timber; era 2 is a wide steel body with a cab guard.
            vv.Bed = new GameObject("Bed").transform;
            vv.Bed.SetParent(r, false);
            vv.Bed.localPosition = new Vector3(0f, axleY + 0.25f, -1.25f);
            Color bed = era == 0 ? Look.Timber : paint;
            float bw = era == 2 ? 1.35f : 1.1f;
            Art.Cube("Bed floor", vv.Bed, new Vector3(0f, 0.05f, 0.85f), new Vector3(bw, 0.1f, 1.7f), bed * 0.9f);
            Art.Cube("Bed left", vv.Bed, new Vector3(-bw / 2f + 0.03f, 0.35f, 0.85f), new Vector3(0.06f, 0.55f, 1.7f), bed);
            Art.Cube("Bed right", vv.Bed, new Vector3(bw / 2f - 0.03f, 0.35f, 0.85f), new Vector3(0.06f, 0.55f, 1.7f), bed);
            Art.Cube("Bed front", vv.Bed, new Vector3(0f, 0.4f, 1.68f), new Vector3(bw, 0.65f, 0.06f), bed);
            if (era == 2) Art.Cube("Cab guard", vv.Bed, new Vector3(0f, 0.72f, 1.95f), new Vector3(bw, 0.05f, 0.55f), paint * 0.9f);
            if (era == 0) for (int k = 0; k < 3; k++) Art.Cube("Plank line", vv.Bed, new Vector3(-bw / 2f - 0.005f, 0.17f + k * 0.17f, 0.85f), new Vector3(0.01f, 0.015f, 1.7f), MachineDark);
            vv.Cargo = new GameObject("Load").transform;
            vv.Cargo.SetParent(vv.Bed, false);
            vv.Cargo.localPosition = new Vector3(0f, 0.1f, 0.85f);
            Art.Part("Heap", vv.Cargo, Art.Rock, Vector3.zero, new Vector3(bw - 0.1f, 0.7f, 1.5f), Color.gray);
            vv.HeapH = 0.7f;
            // Letter badge riding on the load: white letter on a dark disc, like the belt items.
            vv.CargoBadge = Art.Cyl("Badge", vv.Cargo, new Vector3(0f, 0.75f, 0f), 0.5f, 0.04f, new Color(0.1f, 0.1f, 0.1f));
            vv.CargoLetter = Look.Label("", vv.Cargo, new Vector3(0f, 0.81f, 0f), 0.6f, Color.white);
            vv.CargoLetter.fontStyle = FontStyle.Bold;
            vv.CargoLetter.GetComponent<MeshRenderer>().sharedMaterial = ItemTextMat;
            vv.Cargo.gameObject.SetActive(false);
            return vv;
        }

        // ------------------------------------------------------------------ garage and hopper

        void MakeGarage(Transform B, float s, BView v)
        {
            Art.Cube("Pad", B, new Vector3(0f, 0.05f, 0f), new Vector3(s - 0.2f, 0.1f, s - 0.2f), new Color(0.45f, 0.43f, 0.4f));
            Art.Cube("Walls", B, new Vector3(0f, 1.25f, -0.25f), new Vector3(s - 0.5f, 2.3f, s - 1.0f), Look.Timber);
            Art.RoofAt("Roof", B, new Vector3(0f, 2.4f, -0.25f), s - 0.2f, s - 0.6f, 0.9f, Look.Roof, 90f);
            Art.Cube("Door", B, new Vector3(0f, 1.0f, (s - 1.0f) / 2f - 0.25f + 0.02f), new Vector3(2.2f, 1.8f, 0.05f), new Color(0.35f, 0.33f, 0.3f));
            for (int k = 0; k < 4; k++)
                Art.Cube("Door slat", B, new Vector3(0f, 0.3f + k * 0.45f, (s - 1.0f) / 2f - 0.25f + 0.05f), new Vector3(2.2f, 0.05f, 0.03f), new Color(0.25f, 0.24f, 0.22f));
            Art.Cyl("Fuel drum", B, new Vector3(1.55f, 0.1f, 1.45f), 0.5f, 0.75f, new Color(0.6f, 0.22f, 0.15f));
            Art.Cyl("Fuel drum", B, new Vector3(1.0f, 0.1f, 1.55f), 0.5f, 0.75f, new Color(0.25f, 0.35f, 0.5f));
            Art.Log("Firewood", B, new Vector3(-1.5f, 0.25f, 1.4f), 0.8f, 0.22f, Look.Bark, 90f);
            Art.Log("Firewood", B, new Vector3(-1.3f, 0.25f, 1.4f), 0.8f, 0.22f, Look.Bark * 1.1f, 90f);
            Front(B, s / 2f);
        }

        void MakeHopper(Transform B, float s, BView v)
        {
            // An open timber bin on a stone footing: trucks tip in over the top, a short chute feeds the belt at the front.
            Art.Cube("Footing", B, new Vector3(0f, 0.3f, 0f), new Vector3(3.3f, 0.6f, 3.3f), Look.StoneWall);
            const float wallH = 1.5f, half = 1.55f, baseY = 0.6f;
            for (int side = 0; side < 4; side++)
            {
                bool alongX = side < 2;
                float off = side % 2 == 0 ? -half : half;
                var pos = alongX ? new Vector3(0f, baseY + wallH / 2f, off) : new Vector3(off, baseY + wallH / 2f, 0f);
                var size = alongX ? new Vector3(3.3f, wallH, 0.14f) : new Vector3(0.14f, wallH, 3.3f);
                Art.Cube("Wall", B, pos, size, Look.Timber);
                // Board seams so it reads as planks.
                for (int k = 1; k < 4; k++)
                {
                    var seam = pos; seam.y = baseY + k * wallH / 4f;
                    seam += alongX ? new Vector3(0f, 0f, off * 0.03f) : new Vector3(off * 0.03f, 0f, 0f);
                    Art.Cube("Seam", B, seam, alongX ? new Vector3(3.3f, 0.04f, 0.16f) : new Vector3(0.16f, 0.04f, 3.3f), Look.Timber * 0.6f);
                }
            }
            for (int i = 0; i < 4; i++)
                Art.Cube("Post", B, new Vector3(i % 2 == 0 ? -half : half, baseY + wallH / 2f + 0.1f, i < 2 ? -half : half), new Vector3(0.26f, wallH + 0.2f, 0.26f), Look.Timber * 0.75f);
            // Dark inside floor, so an empty bin looks empty.
            Art.Cube("Floor", B, new Vector3(0f, baseY + 0.25f, 0f), new Vector3(3.0f, 0.1f, 3.0f), new Color(0.2f, 0.17f, 0.14f));
            Art.Cube("Chute", B, new Vector3(0f, 0.75f, half + 0.35f), new Vector3(0.7f, 0.12f, 0.8f), Look.Iron, new Vector3(18f, 0f, 0f));
            v.Spin = null;
            var heap = Art.Part("Contents", B, Art.Rock, new Vector3(0f, baseY + 0.9f, 0f), new Vector3(2.8f, 1.2f, 2.8f), Look.OreRock);
            heap.SetActive(false);
            v.Extra = heap;
            Front(B, s / 2f);
        }
    }
}
