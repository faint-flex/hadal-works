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
        }

        readonly Dictionary<int, VView> _vviews = new Dictionary<int, VView>();
        static readonly Color MachineOrange = new Color(0.93f, 0.55f, 0.16f);
        static readonly Color MachineDark = new Color(0.16f, 0.15f, 0.14f);
        static readonly Color Glass = new Color(0.2f, 0.27f, 0.32f);

        void UpdateVehicles()
        {
            UpdateTrains();
            foreach (var v in _w.Vehicles)
                if (v.Type != VehicleType.Train && !_vviews.ContainsKey(v.Id)) _vviews[v.Id] = v.Type == VehicleType.Excavator ? MakeExcavator() : MakeTruck();
            List<int> gone = null;
            foreach (var kv in _vviews)
                if (_w.VehicleById(kv.Key) == null) (gone ??= new List<int>()).Add(kv.Key);
            if (gone != null) foreach (int id in gone) { Destroy(_vviews[id].Root.gameObject); _vviews.Remove(id); }

            float dt = Time.deltaTime;
            foreach (var v in _w.Vehicles)
            {
                if (v.Type == VehicleType.Train) continue;
                var vv = _vviews[v.Id];
                // Parked trucks ease back a little from an excavator they sit next to, so bodies never overlap.
                var want = Vector2.zero;
                if (v.Type == VehicleType.Truck && v.State != VehicleState.Driving)
                    foreach (var o in _w.Vehicles)
                    {
                        if (o.Type != VehicleType.Excavator) continue;
                        var d = new Vector2(v.X - o.X, v.Y - o.Y);
                        float m = d.magnitude;
                        const float clear = 1.55f;   // tiles between centres that keeps the excavator's tail out of the bed
                        if (m > 1e-3f && m < clear) want += d / m * Mathf.Min(0.55f, clear - m);
                    }
                vv.Nudge = Vector2.Lerp(vv.Nudge, Vector2.ClampMagnitude(want, 0.55f), 1f - Mathf.Exp(-4f * dt));
                float wx = (v.X + vv.Nudge.x) * T, wz = (v.Y + vv.Nudge.y) * T;
                float y = GroundHeight(wx, wz);
                // Lean with the slope: compare the ground ahead and behind.
                var fwd = Quaternion.Euler(0f, v.Heading, 0f) * Vector3.forward;
                float ahead = GroundHeight(wx + fwd.x * 1.2f, wz + fwd.z * 1.2f), behind = GroundHeight(wx - fwd.x * 1.2f, wz - fwd.z * 1.2f);
                float pitch = -Mathf.Atan2(ahead - behind, 2.4f) * Mathf.Rad2Deg;
                vv.Yaw = Mathf.LerpAngle(vv.Yaw, v.Heading, 1f - Mathf.Exp(-10f * dt));
                vv.Root.localPosition = new Vector3(wx, y, wz);
                vv.Root.localRotation = Quaternion.Euler(pitch, vv.Yaw, 0f);
                bool moving = v.State == VehicleState.Driving;
                if (vv.Exhaust != null) SetSmoke(vv.Exhaust, moving || v.State == VehicleState.Digging);
                if (vv.Wheels != null && moving)
                {
                    float spin = (v.Type == VehicleType.Truck ? _w.B.TruckSpeed : _w.B.ExcavatorSpeed) * T / (Mathf.PI * 0.9f) * 360f * dt;
                    foreach (var wh in vv.Wheels) wh.Rotate(Vector3.up, spin, Space.Self);
                }

                if (v.Type == VehicleType.Excavator)
                {
                    // Scoop: boom dips, arm curls, bucket tips, while digging; rests up otherwise.
                    if (v.State == VehicleState.Digging && v.Status.StartsWith("Digging")) vv.Phase += dt * Mathf.PI * 2f / _w.B.DigSeconds;
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
                    SetCargo(vv, v.LoadCount > 0 ? v.Load : Item.None, v.LoadCount / (float)_w.B.TruckCap);
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

        VView MakeExcavator()
        {
            var vv = new VView();
            vv.Root = new GameObject("Excavator").transform;
            vv.Root.SetParent(transform, false);
            var r = vv.Root;
            // Tracks and undercarriage.
            for (int side = -1; side <= 1; side += 2)
            {
                Art.Cube("Track", r, new Vector3(side * 0.62f, 0.28f, 0f), new Vector3(0.42f, 0.5f, 2.1f), MachineDark);
                for (int k = -1; k <= 1; k++) Art.Cyl("Roller", r, new Vector3(side * 0.62f, 0.28f, k * 0.75f), 0.36f, 0.46f, new Color(0.3f, 0.3f, 0.3f), new Vector3(0f, 0f, 90f));
            }
            Art.Cube("Frame", r, new Vector3(0f, 0.45f, 0f), new Vector3(0.9f, 0.25f, 1.4f), MachineDark);
            // Upper body: cab on the left, engine and counterweight behind.
            var body = new GameObject("Body").transform;
            body.SetParent(r, false);
            body.localPosition = new Vector3(0f, 0.6f, 0f);
            Art.Cube("Deck", body, new Vector3(0f, 0.3f, -0.15f), new Vector3(1.55f, 0.6f, 1.7f), MachineOrange);
            Art.Cube("Counterweight", body, new Vector3(0f, 0.35f, -1.05f), new Vector3(1.5f, 0.55f, 0.35f), MachineOrange * 0.8f);
            Art.Cube("Cab", body, new Vector3(-0.38f, 0.95f, 0.25f), new Vector3(0.72f, 0.75f, 0.8f), MachineOrange);
            Art.Cube("Window", body, new Vector3(-0.38f, 1.02f, 0.66f), new Vector3(0.6f, 0.5f, 0.03f), Glass);
            Art.Cube("Side window", body, new Vector3(-0.75f, 1.02f, 0.25f), new Vector3(0.03f, 0.45f, 0.6f), Glass);
            Art.Cyl("Exhaust pipe", body, new Vector3(0.5f, 0.6f, -0.7f), 0.1f, 0.6f, MachineDark);
            vv.Exhaust = Art.Smoke(body, new Vector3(0.5f, 1.25f, -0.7f), 0.35f);
            { var main = vv.Exhaust.main; main.startSizeMultiplier = 0.35f; main.startLifetimeMultiplier = 0.5f; }
            // Boom -> arm -> bucket, hinged at the front right of the deck.
            vv.Boom = new GameObject("Boom").transform;
            vv.Boom.SetParent(body, false);
            vv.Boom.localPosition = new Vector3(0.3f, 0.55f, 0.55f);
            Art.Cube("Boom beam", vv.Boom, new Vector3(0f, 0f, 1.0f), new Vector3(0.24f, 0.28f, 2.1f), MachineOrange);
            Art.Cyl("Ram", vv.Boom, new Vector3(0f, -0.25f, 0.2f), 0.1f, 1.2f, new Color(0.75f, 0.75f, 0.75f), new Vector3(60f, 0f, 0f));
            vv.Arm = new GameObject("Arm").transform;
            vv.Arm.SetParent(vv.Boom, false);
            vv.Arm.localPosition = new Vector3(0f, 0f, 2.0f);
            Art.Cube("Arm beam", vv.Arm, new Vector3(0f, 0f, 0.75f), new Vector3(0.2f, 0.22f, 1.5f), MachineOrange * 0.92f);
            vv.Bucket = new GameObject("Bucket").transform;
            vv.Bucket.SetParent(vv.Arm, false);
            vv.Bucket.localPosition = new Vector3(0f, 0f, 1.5f);
            Art.Cube("Bucket back", vv.Bucket, new Vector3(0f, -0.15f, 0.12f), new Vector3(0.6f, 0.4f, 0.08f), MachineDark);
            Art.Cube("Bucket floor", vv.Bucket, new Vector3(0f, -0.35f, 0.35f), new Vector3(0.6f, 0.08f, 0.5f), MachineDark);
            for (int k = -1; k <= 1; k++) Art.Cube("Tooth", vv.Bucket, new Vector3(k * 0.2f, -0.35f, 0.65f), new Vector3(0.07f, 0.05f, 0.15f), new Color(0.6f, 0.6f, 0.6f));
            // What's in the bucket.
            vv.Cargo = new GameObject("Load").transform;
            vv.Cargo.SetParent(vv.Bucket, false);
            vv.Cargo.localPosition = new Vector3(0f, -0.3f, 0.35f);
            Art.Part("Heap", vv.Cargo, Art.Rock, Vector3.zero, new Vector3(0.5f, 0.3f, 0.4f), Color.gray);
            vv.HeapH = 0.3f;
            vv.Cargo.gameObject.SetActive(false);
            return vv;
        }

        VView MakeTruck()
        {
            var vv = new VView();
            vv.Root = new GameObject("Truck").transform;
            vv.Root.SetParent(transform, false);
            var r = vv.Root;
            var green = new Color(0.25f, 0.42f, 0.3f);
            Art.Cube("Chassis", r, new Vector3(0f, 0.55f, 0f), new Vector3(0.9f, 0.22f, 2.6f), MachineDark);
            Art.Cube("Cab", r, new Vector3(0f, 1.05f, 0.95f), new Vector3(1.05f, 0.85f, 0.7f), green);
            Art.Cube("Windscreen", r, new Vector3(0f, 1.2f, 1.31f), new Vector3(0.9f, 0.4f, 0.03f), Glass);
            Art.Cube("Bumper", r, new Vector3(0f, 0.55f, 1.33f), new Vector3(1.0f, 0.18f, 0.06f), new Color(0.6f, 0.6f, 0.6f));
            Art.Cyl("Exhaust pipe", r, new Vector3(0.45f, 0.9f, 0.55f), 0.08f, 0.9f, MachineDark);
            vv.Exhaust = Art.Smoke(r, new Vector3(0.45f, 1.85f, 0.55f), 0.3f);
            { var main = vv.Exhaust.main; main.startSizeMultiplier = 0.3f; main.startLifetimeMultiplier = 0.5f; }
            var wheels = new List<Transform>();
            foreach (float z in new[] { 0.85f, -0.35f, -0.95f })
                for (int side = -1; side <= 1; side += 2)
                {
                    var wpivot = new GameObject("Wheel").transform;
                    wpivot.SetParent(r, false);
                    wpivot.localPosition = new Vector3(side * 0.52f, 0.45f, z);
                    wpivot.localRotation = Quaternion.Euler(0f, 0f, 90f);
                    Art.Part("Tyre", wpivot, Art.Prim(PrimitiveType.Cylinder), Vector3.zero, new Vector3(0.9f, 0.14f, 0.9f), MachineDark);
                    Art.Part("Hub", wpivot, Art.Prim(PrimitiveType.Cylinder), new Vector3(0f, -0.02f * side, 0f), new Vector3(0.4f, 0.15f, 0.4f), new Color(0.65f, 0.6f, 0.5f));
                    wheels.Add(wpivot);
                }
            vv.Wheels = wheels.ToArray();
            // Bed hinged at the back so it tips up.
            vv.Bed = new GameObject("Bed").transform;
            vv.Bed.SetParent(r, false);
            vv.Bed.localPosition = new Vector3(0f, 0.7f, -1.25f);
            Art.Cube("Bed floor", vv.Bed, new Vector3(0f, 0.05f, 0.85f), new Vector3(1.1f, 0.1f, 1.7f), green * 0.9f);
            Art.Cube("Bed left", vv.Bed, new Vector3(-0.53f, 0.35f, 0.85f), new Vector3(0.06f, 0.55f, 1.7f), green);
            Art.Cube("Bed right", vv.Bed, new Vector3(0.53f, 0.35f, 0.85f), new Vector3(0.06f, 0.55f, 1.7f), green);
            Art.Cube("Bed front", vv.Bed, new Vector3(0f, 0.4f, 1.68f), new Vector3(1.1f, 0.65f, 0.06f), green);
            vv.Cargo = new GameObject("Load").transform;
            vv.Cargo.SetParent(vv.Bed, false);
            vv.Cargo.localPosition = new Vector3(0f, 0.1f, 0.85f);
            Art.Part("Heap", vv.Cargo, Art.Rock, Vector3.zero, new Vector3(1.0f, 0.7f, 1.5f), Color.gray);
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
