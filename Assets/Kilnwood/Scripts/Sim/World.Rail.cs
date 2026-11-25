using System;
using System.Collections.Generic;

namespace Kilnwood.Sim
{
    /// <summary>0.8 Tier 4 "Rails": rail track, stations and trains.
    /// A station takes items from belts (and from machines facing it) into its loading bay. A train runs back and forth
    /// along the rails between its home station and the farthest other station it can reach. At each stop it unloads,
    /// then loads what the station has waiting. A station near the lodge (the yard) unloads straight into the lodge and
    /// sends fuel from the lodge out to the far end, where the station hands it to fuel-burning machines touching it.</summary>
    public partial class World
    {
        /// <summary>Items a train has brought to the lodge. Tier 4 goal.</summary>
        public int RailHauled { get; private set; }
        public void RestoreRailHauled(int n) => RailHauled = n;

        public static int StationStored(Dictionary<Item, int> d) { int n = 0; foreach (var kv in d) n += kv.Value; return n; }

        /// <summary>Close enough to the lodge to unload straight into it.</summary>
        public bool IsYard(Building st)
        {
            if (Lodge == null || st.Type != BuildingType.Station) return false;
            int dx = Math.Max(0, Math.Max(Lodge.X - (st.X + st.Size - 1), st.X - (Lodge.X + Lodge.Size - 1)));
            int dy = Math.Max(0, Math.Max(Lodge.Y - (st.Y + st.Size - 1), st.Y - (Lodge.Y + Lodge.Size - 1)));
            return Math.Max(dx, dy) <= B.YardRange;
        }

        bool IsRail(int i) => _occ[i] >= 0 && Buildings[_occ[i]].Type == BuildingType.Rail;

        /// <summary>Rail tiles right beside a station (not diagonal): where trains stop for it.</summary>
        public List<int> StopTiles(Building st)
        {
            var list = new List<int>();
            for (int k = 0; k < st.Size; k++)
            {
                foreach (var (x, y) in new[] { (st.X + k, st.Y - 1), (st.X + k, st.Y + st.Size), (st.X - 1, st.Y + k), (st.X + st.Size, st.Y + k) })
                    if (InBounds(x, y) && IsRail(y * W + x)) list.Add(y * W + x);
            }
            return list;
        }

        /// <summary>Path along rail tiles only (no diagonals) from `start` to the nearest tile where goal is true.</summary>
        bool FindRailPath(int start, Func<int, bool> goal, List<int> path)
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
                for (int d = 0; d < 4; d++)
                {
                    int nx = cx + Dir.DX[d], ny = cy + Dir.DY[d];
                    if (!InBounds(nx, ny)) continue;
                    int n = ny * W + nx;
                    if (_prev[n] != -2 || !IsRail(n)) continue;
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

        // ------------------------------------------------------------------ buying

        string CanBuyTrain(Building st)
        {
            if (st == null || st.Type != BuildingType.Station) return "Click a station";
            if (Tier < 4) return "Trains unlock at Tier 4";
            foreach (var c in B.TrainCost)
                if (Storage[c.item] < c.count) return "Need " + c.count + " " + Items.Name(c.item).ToLowerInvariant() + " (have " + Storage[c.item] + ")";
            if (StopTiles(st).Count == 0) return "Lay a rail right beside the station first";
            return null;
        }

        Vehicle BuyTrain(Building st, bool free)
        {
            if (!free && CanBuyTrain(st) != null) return null;
            var stops = StopTiles(st);
            if (stops.Count == 0) return null;
            if (!free) foreach (var c in B.TrainCost) Storage[c.item] -= c.count;
            var v = new Vehicle { Id = _nextVehicle++, Type = VehicleType.Train, GarageId = st.Id, X = stops[0] % W + 0.5f, Y = stops[0] / W + 0.5f, Tank = B.TankSeconds };
            v.State = VehicleState.Loading;   // starts by loading at home
            v.TargetStation = st.Id;
            Vehicles.Add(v);
            return v;
        }

        public int CountTrains() { int n = 0; foreach (var v in Vehicles) if (v.Type == VehicleType.Train) n++; return n; }

        // ------------------------------------------------------------------ stations

        void StepStation(Building m, float dt)
        {
            // Unloaded goods go out the front (to a belt or machine), fuel goes to fuel burners touching the station.
            if (m.Output.Count > 0)
            {
                foreach (var t in Touching(m))
                {
                    if (!BurnsFuel(t) && t.Type != BuildingType.SteamEngine && t.Type != BuildingType.Turbine && t.Type != BuildingType.Garage) continue;
                    foreach (var fuel in new[] { Item.Coal, Item.Charcoal, Item.Wood })
                    {
                        m.Output.TryGetValue(fuel, out int have);
                        if (have <= 0 || t.FuelCount >= B.FuelCap) continue;
                        if (Accept(t, fuel)) m.Output[fuel] = have - 1;
                    }
                }
                if (m.OutCount == 0)
                    foreach (var kv in m.Output)
                        if (kv.Value > 0 && !Balance.IsFuel(kv.Key)) { m.OutItem = kv.Key; m.OutCount = 1; m.Output[kv.Key] = kv.Value - 1; break; }
                PushOutput(m);
            }
            else PushOutput(m);
            int waiting = StationStored(m.Input);
            bool hasTrain = CountTrains() > 0;
            m.Status = StopTiles(m).Count == 0 ? "No rail beside it"
                : !hasTrain ? "No train yet (buy one here)"
                : IsYard(m) ? "Lodge yard: " + waiting + " waiting"
                : waiting + " waiting to load";
        }

        IEnumerable<Building> Touching(Building m)
        {
            var seen = new HashSet<int>();
            foreach (int t in RingTiles(m))
            {
                int id = _occ[t];
                if (id < 0 || !seen.Add(id)) continue;
                yield return Buildings[id];
            }
        }

        /// <summary>The farthest other station reachable on the rails from this one (0 if none).</summary>
        int OtherStation(Vehicle v, int fromId)
        {
            int start = TileOf(v);
            int best = 0, bestLen = -1;
            var path = new List<int>();
            foreach (var b in _machines)
            {
                if (b.Type != BuildingType.Station || b.Id == fromId) continue;
                var stops = new HashSet<int>(StopTiles(b));
                if (stops.Count == 0 || !FindRailPath(start, stops.Contains, path)) continue;
                if (path.Count > bestLen) { best = b.Id; bestLen = path.Count; }
            }
            return best;
        }

        // ------------------------------------------------------------------ trains

        void StepTrain(Vehicle v, float dt)
        {
            Buildings.TryGetValue(v.TargetStation, out var at);
            switch (v.State)
            {
                case VehicleState.Driving:
                    if (DriveTrain(v, dt)) { v.State = VehicleState.Tipping; v.Timer = 0f; }
                    if (at != null) v.Status = "Running to the " + (IsYard(at) ? "lodge yard" : "station at " + at.X + "," + at.Y);
                    return;
                case VehicleState.Tipping:
                    // Unload.
                    if (at == null) { v.State = VehicleState.Waiting; return; }
                    v.Timer += dt;
                    v.Status = "Unloading";
                    if (v.Timer < B.TrainStopSeconds) return;
                    UnloadTrain(v, at);
                    v.State = VehicleState.Loading; v.Timer = 0f;
                    return;
                case VehicleState.Loading:
                    if (at == null) { v.State = VehicleState.Waiting; return; }
                    v.Timer += dt;
                    v.Status = "Loading";
                    if (v.Timer < B.TrainStopSeconds) return;
                    LoadTrain(v, at);
                    v.State = VehicleState.Waiting; v.Timer = 0f;
                    return;
                default:
                {
                    // Pick the next station and go.
                    int next = OtherStation(v, at != null ? at.Id : 0);
                    if (next == 0) { v.Status = "No other station on this line"; return; }
                    var stops = new HashSet<int>(StopTiles(Buildings[next]));
                    // Reverse out: the train is 3 tiles long, so its new front is the tile its last wagon stands on.
                    // That tile is where it came from, so the whole train stays on the line.
                    if (v.Path.Count > 0)
                    {
                        int back = Math.Max(0, v.Path.Count - 1 - TrainTiles);
                        int rear = v.Path[back];
                        v.X = rear % W + 0.5f; v.Y = rear / W + 0.5f;
                        v.LocoLeads = !v.LocoLeads;
                        v.Heading += 180f;
                    }
                    if (!FindRailPath(TileOf(v), stops.Contains, v.Path)) { v.Status = "Can't reach the other station"; return; }
                    v.PathStep = 0;
                    v.TargetStation = next;
                    v.State = VehicleState.Driving;
                    return;
                }
            }
        }

        /// <summary>Length of a train in tiles behind its front: loco + two wagons, 3.2 m each.</summary>
        public const int TrainTiles = 3;

        bool DriveTrain(Vehicle v, float dt)
        {
            if (v.PathStep >= v.Path.Count) return true;
            float speed = B.TrainSpeed * (v.Tank > 0f ? 1f : 0.25f);
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

        void UnloadTrain(Vehicle v, Building st)
        {
            bool yard = IsYard(st);
            int returned = 0;   // fuel that went out from the lodge and came back undelivered doesn't count as hauled
            var kinds = new List<Item>(v.Cargo.Keys);
            foreach (var it in kinds)
            {
                int n = v.Cargo[it];
                if (n <= 0) continue;
                if (yard)
                {
                    Storage[it] += n;
                    if (Balance.IsFuel(it)) { int back = Math.Min(n, v.FromYard - returned); returned += back; Delivered[it] += n - back; RailHauled += n - back; }
                    else { Delivered[it] += n; RailHauled += n; }
                    v.Cargo[it] = 0;
                }
                else
                {
                    int room = B.StationCap - StationStored(st.Output);
                    int put = Math.Min(n, room);
                    st.Output.TryGetValue(it, out int have);
                    st.Output[it] = have + put;
                    v.Cargo[it] = n - put;
                    if (Balance.IsFuel(it)) v.FromYard = Math.Max(0, v.FromYard - put);
                }
            }
            if (yard) v.FromYard = 0;
        }

        void LoadTrain(Vehicle v, Building st)
        {
            int room = B.TrainCap - v.CargoCount;
            if (IsYard(st))
            {
                // Top the tank up from the lodge, then take fuel out for the far station's machines.
                if (v.Tank < B.TankSeconds)
                {
                    foreach (var fuel in new[] { Item.Coal, Item.Charcoal })
                        if (Storage[fuel] > 0) { Storage[fuel]--; v.Tank = B.TankSeconds; break; }
                }
                // Only as much as the far station has fuel burners for and room to hold.
                int next = OtherStation(v, st.Id);
                int wanted = 0;
                if (next != 0)
                {
                    var far = Buildings[next];
                    foreach (var t in Touching(far)) if (BurnsFuel(t)) wanted += B.TrainFuelLoad / 2;
                    wanted = Math.Min(wanted, B.TrainFuelLoad);
                    int farFuel = 0;
                    foreach (var kv in far.Output) if (Balance.IsFuel(kv.Key)) farFuel += kv.Value;
                    wanted = Math.Max(0, Math.Min(wanted, B.TrainFuelLoad * 2 - farFuel));
                }
                int fuelRoom = Math.Min(room, wanted - CargoOf(v, Item.Coal) - CargoOf(v, Item.Charcoal));
                foreach (var fuel in new[] { Item.Coal, Item.Charcoal })
                {
                    int keep = fuel == Item.Coal ? B.GoalCoal / 4 : 10;   // leave a little in the lodge
                    int take = Math.Max(0, Math.Min(fuelRoom, Storage[fuel] - keep));
                    if (take <= 0) continue;
                    Storage[fuel] -= take;
                    AddCargo(v, fuel, take);
                    v.FromYard += take;
                    fuelRoom -= take; room -= take;
                }
            }
            foreach (var it in new List<Item>(st.Input.Keys))
            {
                if (room <= 0) break;
                int n = Math.Min(room, st.Input[it]);
                if (n <= 0) continue;
                st.Input[it] -= n;
                AddCargo(v, it, n);
                room -= n;
            }
        }

        static int CargoOf(Vehicle v, Item it) => v.Cargo.TryGetValue(it, out int n) ? n : 0;
        static void AddCargo(Vehicle v, Item it, int n) => v.Cargo[it] = CargoOf(v, it) + n;
    }
}
