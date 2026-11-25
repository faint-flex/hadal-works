using System;
using System.Collections.Generic;

namespace Kilnwood.Sim
{
    public enum VehicleState { Idle, Driving, Digging, Loading, Tipping, Refuelling, Waiting }

    /// <summary>An excavator or a truck. Positions are in tiles (tile x covers x .. x+1; its centre is x + 0.5).</summary>
    public class Vehicle
    {
        public int Id;
        public VehicleType Type;
        public int GarageId;
        public float X, Y;
        public float Heading;               // degrees, 0 = north (+y), 90 = east
        public VehicleState State;
        public string Status = "";
        public float Tank;                  // seconds of driving / digging left

        // Path being driven.
        public readonly List<int> Path = new List<int>();
        public int PathStep;
        public VehicleState After;          // what to do on arrival

        // Excavator.
        public int DigTile = -1;            // tile index being dug
        public Item Bucket;
        public int BucketCount;
        public float Timer;
        public float WaitingSince = -1f;    // when the bucket filled with no truck

        // Truck.
        public int Serving;                 // excavator id this truck is loading from (0 = none)
        public Item Load;
        public int LoadCount;
        public int DropBuilding;            // hopper / lodge id to tip into (0 = dump tile)
        public int DropTile = -1;

        // Train (GarageId = the station it was bought at).
        public int TargetStation;
        public int FromYard;                // fuel on board that came from the lodge
        public bool LocoLeads = true;       // trains: which end is in front (the loco runs round at each stop)           // station it is running to (0 = none yet)
        public readonly Dictionary<Item, int> Cargo = new Dictionary<Item, int>();
        public int CargoCount { get { int n = 0; foreach (var kv in Cargo) n += kv.Value; return n; } }

        public int TileX => (int)Math.Floor(X);
        public int TileY => (int)Math.Floor(Y);
    }

    /// <summary>0.5 "Open Pit": ground height, dig and dump marks, excavators and trucks.</summary>
    public partial class World
    {
        /// <summary>Ground per tile in units relative to the surface: 0 untouched, -8 = one level dug out,
        /// +8 = one level filled. Height in metres = Units / UnitsPerLevel.</summary>
        public readonly int[] Units;
        public readonly bool[] DigMark, DumpMark;
        public readonly HashSet<int> ChangedGround = new HashSet<int>();
        public readonly List<Vehicle> Vehicles = new List<Vehicle>();
        int _nextVehicle = 1;

        public float HeightAt(int x, int y) => InBounds(x, y) ? Units[y * W + x] / (float)B.UnitsPerLevel : 0f;

        /// <summary>Which layer is being worked: 0 = the top layer (and anything filled), -1 the next one down...</summary>
        public int Layer(int i) => Units[i] >= 0 ? Units[i] / B.UnitsPerLevel : -((-Units[i]) / B.UnitsPerLevel);

        // ------------------------------------------------------------------ what's under the ground

        /// <summary>What digging this tile gives next, or None when it can't be dug any further.</summary>
        public Item MaterialAt(int i)
        {
            int u = Units[i];
            if (u > 0) return Item.Dirt;
            int level = -u / B.UnitsPerLevel;
            if (level >= B.MaxDigDepth) return Item.None;
            if (CoalSeam[i] && level >= B.CoalFromLevel && (Ground[i] == Terrain.Grass || Ground[i] == Terrain.Stone)) return Item.Coal;
            switch (Ground[i])
            {
                case Terrain.IronOre: return Item.Ore;
                case Terrain.Stone: return Item.Stone;
                case Terrain.Grass: return level == 0 ? Item.Dirt : Item.Stone;
                default: return Item.None;   // forest (cut it first) and water
            }
        }

        public string CanMarkDig(int x, int y)
        {
            if (!InBounds(x, y)) return "Off the map";
            int i = y * W + x;
            if (Ground[i] == Terrain.Water) return "Can't dig the river";
            if (Ground[i] == Terrain.Forest) return "Trees in the way";
            if (_occ[i] >= 0 || _shaftOcc[i] >= 0) return "Something is built there";
            if (MaterialAt(i) == Item.None) return "Already as deep as it goes";
            return null;
        }

        public string CanMarkDump(int x, int y)
        {
            if (!InBounds(x, y)) return "Off the map";
            int i = y * W + x;
            if (Ground[i] == Terrain.Water) return "Can't fill the river";
            if (Ground[i] == Terrain.Forest) return "Trees in the way";
            if (_occ[i] >= 0 || _shaftOcc[i] >= 0) return "Something is built there";
            if (Units[i] >= B.MaxDumpHeight * B.UnitsPerLevel) return "Already as high as it goes";
            return null;
        }

        /// <summary>Marks every tile it can in the rectangle. Returns how many tiles were marked.</summary>
        public int MarkDig(int x0, int y0, int x1, int y1)
        {
            int n = 0;
            ForRect(x0, y0, x1, y1, (x, y) =>
            {
                if (CanMarkDig(x, y) != null) return;
                int i = y * W + x;
                DumpMark[i] = false;
                if (!DigMark[i]) { DigMark[i] = true; ChangedGround.Add(i); n++; }
            });
            return n;
        }

        public int MarkDump(int x0, int y0, int x1, int y1)
        {
            int n = 0;
            ForRect(x0, y0, x1, y1, (x, y) =>
            {
                if (CanMarkDump(x, y) != null) return;
                int i = y * W + x;
                DigMark[i] = false;
                if (!DumpMark[i]) { DumpMark[i] = true; ChangedGround.Add(i); n++; }
            });
            return n;
        }

        public int ClearMarks(int x0, int y0, int x1, int y1)
        {
            int n = 0;
            ForRect(x0, y0, x1, y1, (x, y) =>
            {
                if (!InBounds(x, y)) return;
                int i = y * W + x;
                if (DigMark[i] || DumpMark[i]) { DigMark[i] = DumpMark[i] = false; ChangedGround.Add(i); n++; }
            });
            return n;
        }

        static void ForRect(int x0, int y0, int x1, int y1, Action<int, int> f)
        {
            for (int y = Math.Min(y0, y1); y <= Math.Max(y0, y1); y++)
                for (int x = Math.Min(x0, x1); x <= Math.Max(x0, x1); x++)
                    f(x, y);
        }

        /// <summary>Takes one unit out of a tile. Returns what came out.</summary>
        Item DigOne(int i)
        {
            var it = MaterialAt(i);
            if (it == Item.None) return it;
            Units[i]--;
            ChangedGround.Add(i);
            if (MaterialAt(i) == Item.None) DigMark[i] = false;
            return it;
        }

        /// <summary>Drops one unit of dirt on a tile. Returns false if the tile is full.</summary>
        bool FillOne(int i)
        {
            int max = B.MaxDumpHeight * B.UnitsPerLevel;
            if (Units[i] >= max) { DumpMark[i] = false; return false; }
            Units[i]++;
            ChangedGround.Add(i);
            if (Units[i] >= max) DumpMark[i] = false;
            return true;
        }

        // ------------------------------------------------------------------ vehicles

        public bool VehicleOn(int x, int y)
        {
            foreach (var v in Vehicles) if (v.TileX == x && v.TileY == y) return true;
            return false;
        }

        public Vehicle VehicleById(int id)
        {
            foreach (var v in Vehicles) if (v.Id == id) return v;
            return null;
        }

        public int CountVehicles(VehicleType t)
        {
            int n = 0;
            foreach (var v in Vehicles) if (v.Type == t) n++;
            return n;
        }

        /// <summary>True for open ground: no belt, splitter or track on it (vehicles cross those but never stop there).</summary>
        bool OffRoad(int tile) => _occ[tile] < 0;

        /// <summary>Vehicles drive on any land tile that has no machine on it (they cross belts).</summary>
        public bool Drivable(int x, int y)
        {
            if (!InBounds(x, y)) return false;
            int i = y * W + x;
            if (Ground[i] == Terrain.Water) return false;
            int occ = _occ[i];
            if (occ < 0) return true;
            var t = Buildings[occ].Type;
            return t == BuildingType.Belt || t == BuildingType.Splitter || t == BuildingType.Rail;   // level crossings
        }

        public string CanBuy(Building garage, VehicleType t)
        {
            if (t == VehicleType.Train) return CanBuyTrain(garage);
            if (garage == null || garage.Type != BuildingType.Garage) return "Click a garage";
            foreach (var c in B.VehicleCost(t))
                if (Storage[c.item] < c.count)
                    return "Need " + c.count + " " + Items.Name(c.item).ToLowerInvariant() + " (have " + Storage[c.item] + ")";
            if (SpawnTile(garage) < 0) return "No room next to the garage";
            return null;
        }

        public Vehicle Buy(int garageId, VehicleType t, bool free = false)
        {
            Buildings.TryGetValue(garageId, out var g);
            if (!free && CanBuy(g, t) != null) return null;
            if (g == null) return null;
            if (t == VehicleType.Train) return BuyTrain(g, free);
            int tile = SpawnTile(g);
            if (tile < 0) return null;
            if (!free) foreach (var c in B.VehicleCost(t)) Storage[c.item] -= c.count;
            var v = new Vehicle
            {
                Id = _nextVehicle++, Type = t, GarageId = g.Id, X = tile % W + 0.5f, Y = tile / W + 0.5f,
                Tank = B.TankSeconds, Heading = g.Facing * 90f,
            };
            Vehicles.Add(v);
            return v;
        }

        public void RestoreVehicle(VehicleType t, int garageId, float x, float y, float tank)
        {
            var v = new Vehicle { Id = _nextVehicle++, Type = t, GarageId = garageId, X = x, Y = y, Tank = tank };
            Vehicles.Add(v);
        }

        int SpawnTile(Building g)
        {
            foreach (var (x, y) in FrontTiles(g)) if (Drivable(x, y)) return y * W + x;
            foreach (int i in RingTiles(g)) if (Drivable(i % W, i / W)) return i;
            return -1;
        }

        void RemoveVehiclesOf(int garageId)
        {
            for (int k = Vehicles.Count - 1; k >= 0; k--)
            {
                var v = Vehicles[k];
                if (v.GarageId != garageId) continue;
                foreach (var c in B.VehicleCost(v.Type)) Storage[c.item] += c.count;   // full refund, like buildings
                Vehicles.RemoveAt(k);
            }
        }

        /// <summary>Tiles touching a building's footprint (its outer ring, corners included).</summary>
        IEnumerable<int> RingTiles(Building b)
        {
            for (int y = b.Y - 1; y <= b.Y + b.Size; y++)
                for (int x = b.X - 1; x <= b.X + b.Size; x++)
                {
                    if (b.Covers(x, y) || !InBounds(x, y)) continue;
                    yield return y * W + x;
                }
        }

        // ------------------------------------------------------------------ path finding

        int[] _prev;
        readonly Queue<int> _open = new Queue<int>();
        static readonly int[] DX8 = { 0, 1, 0, -1, 1, 1, -1, -1 };
        static readonly int[] DY8 = { 1, 0, -1, 0, 1, -1, -1, 1 };

        /// <summary>Breadth-first search from `start` to the nearest tile where `goal` is true. Fills `path`
        /// (excluding the start). Diagonal steps only when both side tiles are drivable. Returns false if unreachable.</summary>
        public bool FindPath(int start, Func<int, bool> goal, List<int> path)
        {
            path.Clear();
            if (goal(start)) return true;
            if (_prev == null) _prev = new int[W * H];
            for (int i = 0; i < _prev.Length; i++) _prev[i] = -2;
            _open.Clear();
            _open.Enqueue(start);
            _prev[start] = -1;
            while (_open.Count > 0)
            {
                int c = _open.Dequeue();
                int cx = c % W, cy = c / W;
                for (int d = 0; d < 8; d++)
                {
                    int nx = cx + DX8[d], ny = cy + DY8[d];
                    if (!Drivable(nx, ny)) continue;
                    if (d >= 4 && (!Drivable(cx + DX8[d], cy) || !Drivable(cx, cy + DY8[d]))) continue;
                    int n = ny * W + nx;
                    if (_prev[n] != -2) continue;
                    _prev[n] = c;
                    if (goal(n))
                    {
                        for (int k = n; k != start; k = _prev[k]) path.Add(k);
                        path.Reverse();
                        return true;
                    }
                    _open.Enqueue(n);
                }
            }
            return false;
        }

        static bool Near(int a, int b, int w) => Math.Abs(a % w - b % w) <= 1 && Math.Abs(a / w - b / w) <= 1;

        int TileOf(Vehicle v) => Math.Max(0, Math.Min(H - 1, v.TileY)) * W + Math.Max(0, Math.Min(W - 1, v.TileX));

        bool Go(Vehicle v, Func<int, bool> goal, VehicleState after)
        {
            if (!FindPath(TileOf(v), goal, v.Path)) return false;
            v.PathStep = 0;
            v.After = after;
            v.State = VehicleState.Driving;
            return true;
        }

        /// <summary>Where a vehicle is (or will be once it arrives): the end of its path while driving.</summary>
        int ParkedAt(Vehicle v) => v.State == VehicleState.Driving && v.Path.Count > 0 ? v.Path[v.Path.Count - 1] : TileOf(v);

        /// <summary>True if another pit vehicle is standing on, or heading for, this tile.</summary>
        bool Occupied(int tile, Vehicle self)
        {
            foreach (var o in Vehicles)
                if (o != self && o.Type != VehicleType.Train && (ParkedAt(o) == tile || TileOf(o) == tile)) return true;
            return false;
        }

        // ------------------------------------------------------------------ stepping

        void StepVehicles(float dt)
        {
            foreach (var v in Vehicles)
            {
                if (v.Type == VehicleType.Excavator) StepExcavator(v, dt);
                else if (v.Type == VehicleType.Train) StepTrain(v, dt);
                else StepTruck(v, dt);
            }
        }

        bool Drive(Vehicle v, float dt)
        {
            if (v.PathStep >= v.Path.Count) return true;
            float speed = (v.Type == VehicleType.Excavator ? B.ExcavatorSpeed : B.TruckSpeed) * (v.Tank > 0f ? 1f : 0.25f);
            UseFuel(v, dt);
            float move = speed * dt;
            while (move > 0f && v.PathStep < v.Path.Count)
            {
                int t = v.Path[v.PathStep];
                float tx = t % W + 0.5f, ty = t / W + 0.5f;
                float dx = tx - v.X, dy = ty - v.Y;
                float dist = (float)Math.Sqrt(dx * dx + dy * dy);
                if (dist > 1e-4f) v.Heading = (float)(Math.Atan2(dx, dy) * 180.0 / Math.PI);
                if (dist <= move) { v.X = tx; v.Y = ty; move -= dist; v.PathStep++; }
                else { v.X += dx / dist * move; v.Y += dy / dist * move; move = 0f; }
            }
            return v.PathStep >= v.Path.Count;
        }

        void UseFuel(Vehicle v, float dt) => v.Tank = Math.Max(0f, v.Tank - dt);

        void Face(Vehicle v, int tile)
        {
            float dx = tile % W + 0.5f - v.X, dy = tile / W + 0.5f - v.Y;
            if (dx * dx + dy * dy > 1e-4f) v.Heading = (float)(Math.Atan2(dx, dy) * 180.0 / Math.PI);
        }

        void FaceAlongside(Vehicle v, int tile)
        {
            float dx = tile % W + 0.5f - v.X, dy = tile / W + 0.5f - v.Y;
            if (dx * dx + dy * dy <= 1e-4f) return;
            float h = (float)(Math.Atan2(dx, dy) * 180.0 / Math.PI) + 90f;
            if (h > 180f) h -= 360f;
            v.Heading = h;
        }

        /// <summary>Heads home to refuel when the tank runs low. Returns true if it is busy with that.</summary>
        bool HandleFuel(Vehicle v, float dt)
        {
            if (v.State == VehicleState.Refuelling)
            {
                Buildings.TryGetValue(v.GarageId, out var g);
                if (g == null) { v.State = VehicleState.Idle; return false; }
                while (v.Tank < B.TankSeconds - B.WoodFuelSeconds + 0.01f)
                {
                    if (g.FuelCoal > 0) { g.FuelCoal--; v.Tank = Math.Min(B.TankSeconds, v.Tank + B.CoalFuelSeconds); }
                    else if (g.FuelCharcoal > 0) { g.FuelCharcoal--; v.Tank = Math.Min(B.TankSeconds, v.Tank + B.CharcoalFuelSeconds); }
                    else if (g.FuelWood > 0) { g.FuelWood--; v.Tank = Math.Min(B.TankSeconds, v.Tank + B.WoodFuelSeconds); }
                    else break;
                }
                if (v.Tank >= B.RefuelBelow * 2f) { v.State = VehicleState.Idle; return false; }
                v.Status = "No fuel at the garage";
                return true;
            }
            if (v.State != VehicleState.Idle || v.Tank >= B.RefuelBelow) return false;
            Buildings.TryGetValue(v.GarageId, out var garage);
            if (garage == null) return false;
            var ring = new HashSet<int>(RingTiles(garage));
            if (Go(v, ring.Contains, VehicleState.Refuelling)) { v.Status = "Going to refuel"; return true; }
            return false;
        }

        void StepExcavator(Vehicle v, float dt)
        {
            if (HandleFuel(v, dt)) return;
            switch (v.State)
            {
                case VehicleState.Driving:
                    if (Drive(v, dt)) v.State = v.After;
                    if (v.State == VehicleState.Refuelling) HandleFuel(v, 0f);
                    return;
                case VehicleState.Idle:
                {
                    if (v.BucketCount > 0 && v.DigTile < 0) { v.State = VehicleState.Digging; return; }
                    int target = PickDigTile(v);
                    if (target < 0) { v.Status = "Nothing marked to dig"; return; }
                    v.DigTile = target;
                    if (Near(TileOf(v), target, W) && TileOf(v) != target) { v.State = VehicleState.Digging; return; }
                    if (!Go(v, n => n != target && Near(n, target, W) && !Occupied(n, v), VehicleState.Digging)
                        && !Go(v, n => n != target && Near(n, target, W), VehicleState.Digging))
                    {
                        v.DigTile = -1;
                        v.Status = "Can't reach the dig area";
                    }
                    else v.Status = "Driving to dig";
                    return;
                }
                case VehicleState.Digging:
                {
                    if (v.DigTile >= 0) Face(v, v.DigTile);
                    // Is this still a good tile? (Layer by layer: leave it once it is deeper than the shallowest marked tile.)
                    bool tileDone = v.DigTile < 0 || !DigMark[v.DigTile] || MaterialAt(v.DigTile) == Item.None
                                    || Layer(v.DigTile) < ShallowestFor(v, v.DigTile);
                    if (v.BucketCount >= B.BucketCap || (v.BucketCount > 0 && v.DigTile >= 0 && MaterialAt(v.DigTile) != v.Bucket))
                    {
                        v.Status = "Waiting for a truck";
                        if (v.WaitingSince < 0f) v.WaitingSince = Time;
                        return;
                    }
                    if (tileDone)
                    {
                        if (v.BucketCount > 0) { v.Status = "Waiting for a truck"; if (v.WaitingSince < 0f) v.WaitingSince = Time; }
                        v.DigTile = -1;
                        if (v.BucketCount == 0) v.State = VehicleState.Idle;
                        else
                        {
                            // Keep digging nearby only if the next tile gives the same thing; otherwise wait to be emptied.
                            int next = PickDigTile(v);
                            if (next >= 0 && Near(TileOf(v), next, W) && TileOf(v) != next && MaterialAt(next) == v.Bucket) v.DigTile = next;
                        }
                        return;
                    }
                    UseFuel(v, dt);
                    v.Timer += dt * (v.Tank > 0f ? 1f : 0.25f);
                    v.Status = "Digging " + Items.Name(MaterialAt(v.DigTile)).ToLowerInvariant();
                    if (v.Timer >= B.DigSeconds)
                    {
                        v.Timer -= B.DigSeconds;
                        var got = DigOne(v.DigTile);
                        if (got != Item.None) { v.Bucket = got; v.BucketCount++; }
                    }
                    return;
                }
            }
        }

        /// <summary>Shallowest marked tile of the same kind of ground (coal seam, ore field, or other), so each pit
        /// goes down layer by layer on its own and an excavator sent to one pit isn't pulled back to another.</summary>
        int ShallowestMarked(int like)
        {
            int kind = DigKind(like);
            int best = int.MinValue;
            for (int i = 0; i < DigMark.Length; i++)
                if (DigMark[i] && Layer(i) > best && MaterialAt(i) != Item.None && DigKind(i) == kind) best = Layer(i);
            return best;
        }

        int ShallowestOf(int i, int[] shallow) => shallow[DigKind(i)];

        int DigKind(int i) => CoalSeam[i] ? 1 : Ground[i] == Terrain.IronOre ? 2 : 0;

        /// <summary>Is tile i near another excavator's hole or where it stands? gap 0: only the very tile.</summary>
        bool Taken(Vehicle v, int i, int gap)
        {
            foreach (var o in Vehicles)
            {
                if (o == v || o.Type != VehicleType.Excavator) continue;
                // Its hole, and where it stands (a parked excavator waiting for a truck has no hole but still takes room).
                if (o.DigTile >= 0 && Math.Abs(o.DigTile % W - i % W) <= gap && Math.Abs(o.DigTile / W - i / W) <= gap) return true;
                int at = ParkedAt(o);
                if (at == i || (gap > 0 && Math.Abs(at % W - i % W) <= gap + 1 && Math.Abs(at / W - i / W) <= gap + 1)) return true;
            }
            return false;
        }

        /// <summary>Shallowest layer this excavator may work on for that kind of ground: same rule PickDigTile uses
        /// (tiles clear of other excavators first), so it never picks a tile and then drops it as "too deep".</summary>
        int ShallowestFor(Vehicle v, int like)
        {
            int kind = DigKind(like);
            for (int pass = 0; pass < 2; pass++)
            {
                int best = int.MinValue;
                for (int i = 0; i < DigMark.Length; i++)
                    if (DigMark[i] && MaterialAt(i) != Item.None && DigKind(i) == kind && Layer(i) > best && (i == v.DigTile || !Taken(v, i, pass == 0 ? 2 : 0))) best = Layer(i);
                if (best != int.MinValue) return best;
            }
            return int.MinValue;
        }

        /// <summary>Shallowest marked tile first (layer by layer), nearest first; not one another excavator has.</summary>
        int PickDigTile(Vehicle v)
        {
            int me = TileOf(v);
            // Dig toward what a machine is short of: if a hopper feeding a furnace has room for coal but not ore,
            // work the coal seam first. Otherwise keep the pit level (highest layer first), nearest first.
            bool wantOre = false, wantCoal = false;
            foreach (var h in _machines)
            {
                if (h.Type != BuildingType.Hopper) continue;
                if (!wantOre && HopperFeedsMachine(h, Item.Ore) && HopperRoomFor(h, Item.Ore) >= B.TruckCap) wantOre = true;
                if (!wantCoal && HopperFeedsMachine(h, Item.Coal) && HopperRoomFor(h, Item.Coal) >= B.TruckCap) wantCoal = true;
            }
            var shallow = new int[3];
            for (int k = 0; k < 3; k++) shallow[k] = int.MinValue;
            for (int i = 0; i < DigMark.Length; i++)
                if (DigMark[i] && MaterialAt(i) != Item.None) { int k = DigKind(i); if (Layer(i) > shallow[k]) shallow[k] = Layer(i); }
            int best = -1, bestW = -1, bestH = int.MinValue, bestD = int.MaxValue;
            for (int pass = 0; pass < 2 && best < 0; pass++)
            for (int i = 0; i < DigMark.Length; i++)
            {
                if (!DigMark[i] || MaterialAt(i) == Item.None) continue;
                // Keep clear of other excavators' spots (2 tiles), so machines and their trucks don't pile up.
                bool taken = Taken(v, i, pass == 0 ? 2 : 0);
                if (taken) continue;
                int d = Math.Abs(i % W - me % W) + Math.Abs(i / W - me / W);
                int layer = Layer(i) - ShallowestOf(i, shallow);
                int wanted = wantCoal && CoalSeam[i] ? 1 : wantOre && Ground[i] == Terrain.IronOre ? 1 : 0;
                if (wanted > bestW || (wanted == bestW && (layer > bestH || (layer == bestH && d < bestD)))) { best = i; bestW = wanted; bestH = layer; bestD = d; }
            }
            return best;
        }

        void StepTruck(Vehicle v, float dt)
        {
            if (HandleFuel(v, dt)) return;
            switch (v.State)
            {
                case VehicleState.Driving:
                {
                    // Chasing an excavator that moved on: re-route.
                    if (v.After == VehicleState.Loading)
                    {
                        var ex = VehicleById(v.Serving);
                        if (ex == null) { v.Serving = 0; v.State = VehicleState.Idle; return; }
                        int goal = v.Path.Count > 0 ? v.Path[v.Path.Count - 1] : TileOf(v);
                        if (!Near(goal, TileOf(ex), W)) { v.State = VehicleState.Idle; return; }
                    }
                    if (Drive(v, dt))
                    {
                        v.State = v.After;
                        if (v.State == VehicleState.Refuelling) HandleFuel(v, 0f);
                        if (v.State == VehicleState.Tipping) v.Timer = 0f;
                    }
                    return;
                }
                case VehicleState.Idle:
                case VehicleState.Waiting:
                {
                    if (v.LoadCount > 0) { StartHaul(v); return; }
                    // Serve the excavator that has waited longest (else the nearest without a truck).
                    Vehicle pick = null;
                    float bestWait = float.MaxValue; int bestD = int.MaxValue;
                    foreach (var ex in Vehicles)
                    {
                        if (ex.Type != VehicleType.Excavator) continue;
                        bool served = false;
                        foreach (var t in Vehicles) if (t != v && t.Type == VehicleType.Truck && t.Serving == ex.Id) { served = true; break; }
                        if (served) continue;
                        if (ex.State != VehicleState.Digging && ex.BucketCount == 0) continue;
                        float wait = ex.WaitingSince >= 0f ? ex.WaitingSince : float.MaxValue / 2f;
                        int d = Math.Abs(ex.TileX - v.TileX) + Math.Abs(ex.TileY - v.TileY);
                        if (wait < bestWait || (wait == bestWait && d < bestD)) { pick = ex; bestWait = wait; bestD = d; }
                    }
                    if (pick == null)
                    {
                        // Don't wait on a belt or a track: pull off onto open ground first.
                        if (!OffRoad(TileOf(v)) && Go(v, n => OffRoad(n) && !Occupied(n, v), VehicleState.Waiting)) { v.Status = "Pulling off the belt"; return; }
                        v.Status = "Waiting for an excavator"; v.State = VehicleState.Waiting; return;
                    }
                    v.Serving = pick.Id;
                    int at = TileOf(pick), dig = pick.DigTile;
                    if (Near(TileOf(v), at, W) && TileOf(v) != at && TileOf(v) != dig) { v.State = VehicleState.Loading; return; }
                    // Park beside the excavator, away from the hole it is digging (its arm swings that way).
                    if (!Go(v, n => n != at && n != dig && Near(n, at, W) && (dig < 0 || !Near(n, dig, W)) && !Occupied(n, v) && OffRoad(n), VehicleState.Loading)
                        && !Go(v, n => n != at && n != dig && Near(n, at, W) && (dig < 0 || !Near(n, dig, W)) && !Occupied(n, v), VehicleState.Loading)
                        && !Go(v, n => n != at && n != dig && Near(n, at, W) && !Occupied(n, v), VehicleState.Loading)
                        && !Go(v, n => n != at && n != dig && Near(n, at, W) && (dig < 0 || !Near(n, dig, W)), VehicleState.Loading)
                        && !Go(v, n => n != at && n != dig && Near(n, at, W), VehicleState.Loading)) { v.Serving = 0; v.Status = "Can't reach the excavator"; v.State = VehicleState.Waiting; return; }
                    v.Status = "Driving to the excavator";
                    return;
                }
                case VehicleState.Loading:
                {
                    var ex = VehicleById(v.Serving);
                    if (ex == null || !Near(TileOf(v), TileOf(ex), W)) { v.Serving = 0; v.State = VehicleState.Idle; return; }
                    // Side-on to the excavator, so the bed is alongside it rather than the nose driving into it.
                    FaceAlongside(v, TileOf(ex));
                    v.Status = "Being loaded " + v.LoadCount + " / " + B.TruckCap;
                    if (ex.BucketCount > 0)
                    {
                        if (v.LoadCount > 0 && ex.Bucket != v.Load) { Leave(v); return; }
                        int take = Math.Min(ex.BucketCount, B.TruckCap - v.LoadCount);
                        v.Load = ex.Bucket;
                        v.LoadCount += take;
                        ex.BucketCount -= take;
                        if (ex.BucketCount == 0) { ex.Bucket = Item.None; ex.WaitingSince = -1f; }
                    }
                    // Full, or the excavator has run out of work: off it goes.
                    if (v.LoadCount >= B.TruckCap || (v.LoadCount > 0 && ex.State == VehicleState.Idle && ex.DigTile < 0)) Leave(v);
                    else if (ex.State == VehicleState.Driving || (ex.State == VehicleState.Idle && v.LoadCount == 0 && ex.BucketCount == 0 && PickDigTile(ex) < 0))
                    { v.Serving = 0; v.State = VehicleState.Idle; }
                    return;
                }
                case VehicleState.Tipping:
                {
                    v.Timer += dt;
                    v.Status = "Tipping " + Items.Name(v.Load).ToLowerInvariant();
                    if (v.Timer < B.TipSeconds) return;
                    Unload(v);
                    v.State = VehicleState.Idle;
                    return;
                }
            }
        }

        void Leave(Vehicle v)
        {
            v.Serving = 0;
            StartHaul(v);
        }

        /// <summary>Ore and stone: nearest hopper with room, else the lodge. Dirt: the nearest dump tile.</summary>
        void StartHaul(Vehicle v)
        {
            v.Serving = 0;
            if (v.Load == Item.Dirt)
            {
                v.DropBuilding = 0;
                bool any = false;
                for (int i = 0; i < DumpMark.Length; i++) if (DumpMark[i]) { any = true; break; }
                if (!any) { v.Status = "No dump area marked"; v.State = VehicleState.Waiting; return; }
                if (Go(v, n => DumpMark[n], VehicleState.Tipping)) { v.DropTile = v.Path.Count > 0 ? v.Path[v.Path.Count - 1] : TileOf(v); v.Status = "Taking dirt to the dump"; if (v.Path.Count == 0) { v.State = VehicleState.Tipping; v.Timer = 0f; } return; }
                v.Status = "Can't reach the dump area";
                v.State = VehicleState.Waiting;
                return;
            }
            // Hoppers with room, then the lodge.
            var drops = new Dictionary<int, int>();   // ring tile -> building id
            // First choice: hoppers that feed a machine using this load (a furnace short of ore beats the lodge's store).
            for (int pass = 0; pass < 2 && drops.Count == 0; pass++)
                foreach (var b in _machines)
                {
                    if (b.Type != BuildingType.Hopper || HopperRoomFor(b, v.Load) < v.LoadCount || !HopperWants(b, v.Load)) continue;
                    if (pass == 0 && !HopperFeedsMachine(b, v.Load)) continue;
                    foreach (int t in RingTiles(b)) if (!drops.ContainsKey(t)) drops[t] = b.Id;
                }
            if (drops.Count == 0 && Lodge != null)
                foreach (int t in RingTiles(Lodge)) drops[t] = Lodge.Id;
            if (drops.Count == 0) { v.Status = "Nowhere to tip"; v.State = VehicleState.Waiting; return; }
            if (drops.ContainsKey(TileOf(v))) { v.DropBuilding = drops[TileOf(v)]; v.State = VehicleState.Tipping; v.Timer = 0f; return; }
            if (Go(v, drops.ContainsKey, VehicleState.Tipping))
            {
                v.DropBuilding = drops[v.Path[v.Path.Count - 1]];
                var b = Buildings[v.DropBuilding];
                v.Status = "Taking " + Items.Name(v.Load).ToLowerInvariant() + " to the " + B.Defs[b.Type].Name.ToLowerInvariant();
                return;
            }
            v.Status = "Can't reach a hopper or the lodge";
            v.State = VehicleState.Waiting;
        }

        void Unload(Vehicle v)
        {
            if (v.Load == Item.Dirt)
            {
                int i = TileOf(v);
                while (v.LoadCount > 0 && FillOne(i)) v.LoadCount--;
                // Spill what's left onto marked tiles right beside it.
                for (int d = 0; d < 8 && v.LoadCount > 0; d++)
                {
                    int nx = i % W + DX8[d], ny = i / W + DY8[d];
                    if (!InBounds(nx, ny)) continue;
                    int n = ny * W + nx;
                    if (!DumpMark[n]) continue;
                    while (v.LoadCount > 0 && FillOne(n)) v.LoadCount--;
                }
            }
            else if (Buildings.TryGetValue(v.DropBuilding, out var b))
            {
                if (b.Type == BuildingType.Lodge)
                {
                    Storage[v.Load] += v.LoadCount;
                    Delivered[v.Load] += v.LoadCount;
                    v.LoadCount = 0;
                }
                else if (b.Type == BuildingType.Hopper)
                {
                    int put = Math.Min(v.LoadCount, HopperRoom(b));
                    b.Input.TryGetValue(v.Load, out int have);
                    b.Input[v.Load] = have + put;
                    v.LoadCount -= put;
                }
            }
            if (v.LoadCount == 0) v.Load = Item.None;
            v.DropBuilding = 0;
            v.DropTile = -1;
        }

        public int HopperStored(Building h)
        {
            int n = h.OutCount;
            foreach (var kv in h.Input) n += kv.Value;
            return n;
        }

        int HopperRoom(Building h) => Math.Max(0, B.HopperCap - HopperStored(h));

        /// <summary>Room for one kind. A hopper feeding a machine that takes two things keeps half for each,
        /// so a pile of ore can't shut out the coal it needs.</summary>
        int HopperRoomFor(Building h, Item it)
        {
            int room = HopperRoom(h);
            foreach (var (x, y) in FrontTiles(h))
            {
                var r = At(x, y) is Building t && t != h ? RecipeOf(t) : null;
                if (r != null && r.InItems.Length > 1)
                {
                    h.Input.TryGetValue(it, out int have);
                    return Math.Min(room, B.HopperCap / 2 - have);
                }
            }
            return room;
        }

        /// <summary>Does what's in front of this hopper take this item? Belts and the lodge take anything; a machine
        /// only what its recipe or fire uses. Trucks skip hoppers that couldn't pass their load on.</summary>
        public bool HopperWants(Building h, Item it)
        {
            bool anything = false;
            foreach (var (x, y) in FrontTiles(h))
            {
                var t = At(x, y);
                if (t == null || t == h) continue;
                anything = true;
                if (t.Type == BuildingType.Belt || t.Type == BuildingType.Splitter || t.Type == BuildingType.Lodge) return true;
                var r = RecipeOf(t);
                if (r != null && r.Needs(it) > 0) return true;
                if (Balance.IsFuel(it) && (BurnsFuel(t) || t.Type == BuildingType.SteamEngine || t.Type == BuildingType.Garage)) return true;
            }
            return !anything;   // nothing in front yet: it just stores
        }

        /// <summary>True when the hopper tips into a machine whose recipe or fire uses this item (not a belt or the lodge).</summary>
        bool HopperFeedsMachine(Building h, Item it)
        {
            foreach (var (x, y) in FrontTiles(h))
            {
                var t = At(x, y);
                if (t == null || t == h) continue;
                var r = RecipeOf(t);
                if (r != null && r.Needs(it) > 0) return true;
                if (Balance.IsFuel(it) && (BurnsFuel(t) || t.Type == BuildingType.SteamEngine)) return true;
            }
            return false;
        }

        static readonly Item[] HopperOrder = (Item[])Enum.GetValues(typeof(Item));

        void StepHopper(Building m, float dt)
        {
            m.Status = HopperStored(m) == 0 ? "Empty, waiting for trucks" : "Working";
            if (m.OutCount > 0)
            {
                // Couldn't hand it on for a moment (the machine wants something else): put it back, try the next kind.
                m.Work += dt;
                if (m.Work > 0.5f)
                {
                    m.Input.TryGetValue(m.OutItem, out int back);
                    m.Input[m.OutItem] = back + m.OutCount;
                    m.OutCount = 0;
                    m.OutItem = Item.None;
                    m.Work = 0f;
                    m.SplitNext++;
                }
                return;
            }
            m.Work = 0f;
            m.ExtractTimer = Math.Min(1f, m.ExtractTimer + dt * B.HopperOutPerSecond);
            if (m.ExtractTimer < 1f) return;
            // Take kinds in turn, so a machine that needs two things (ore and coal) gets both.
            int n = HopperOrder.Length;
            for (int k = 0; k < n; k++)
            {
                var it = HopperOrder[(m.SplitNext + k) % n];
                if (!m.Input.TryGetValue(it, out int c) || c <= 0) continue;
                m.Input[it] = c - 1;
                m.OutItem = it;
                m.OutCount = 1;
                m.ExtractTimer = 0f;
                m.SplitNext = (m.SplitNext + k + 1) % n;
                break;
            }
        }
    }
}
