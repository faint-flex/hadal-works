using System;
using System.Collections.Generic;

namespace Kilnwood.Sim
{
    public class Building
    {
        public int Id;
        public BuildingType Type;
        public int X, Y;          // bottom-left tile
        public int Size;
        public int Facing;        // Dir; for tubes, the direction items move
        public int Recipe;        // kilns, smelters, workshops

        // Belt state
        public Item BeltItem;
        public float BeltProgress;

        // Machine state
        public readonly Dictionary<Item, int> Input = new Dictionary<Item, int>();
        public readonly Dictionary<Item, int> Output = new Dictionary<Item, int>();   // stations: unloaded goods waiting to go out
        public Item OutItem;
        public int OutCount;
        public float Work;        // seconds of progress on the current craft
        public bool Crafting;
        public float ExtractTimer;
        public bool Staffed = true;        // has the workers it needs
        public int SplitNext;              // splitters: which output gets the next item
        public int FuelWood, FuelCharcoal, FuelCoal; // fuel items waiting to burn
        public float FuelLeft;             // seconds of burn left from the last fuel item
        public int FuelCount => FuelWood + FuelCharcoal + FuelCoal;
        public void AddFuel(Item it) { if (it == Item.Coal) FuelCoal++; else if (it == Item.Charcoal) FuelCharcoal++; else FuelWood++; }
        public string Status = "";
        public int NetSupply, NetDemand;   // shafts and waterwheels: their network's hp given / wanted
        public float Power = -1f;          // 0..1 share of the power it needs on its shaft network; -1 = not on a powered network

        public bool Covers(int x, int y) => x >= X && x < X + Size && y >= Y && y < Y + Size;
    }

    /// <summary>
    /// The valley factory, simulated in seconds. Pure C# (no Unity) so the rules are unit-testable.
    /// </summary>
    public partial class World
    {
        public readonly Balance B;
        public readonly int W, H;
        public readonly Terrain[] Ground;
        readonly int[] _occ;                       // building id per tile, -1 empty
        readonly int[] _shaftOcc;                  // line shafts run overhead on posts, so they get their own layer
        public readonly Dictionary<int, Building> Buildings = new Dictionary<int, Building>();
        readonly List<Building> _belts = new List<Building>();
        readonly List<Building> _machines = new List<Building>();
        readonly List<Building> _splitters = new List<Building>();
        int _nextId = 1;

        // Forest: logs left per tile, regrowing over time. View reads ChangedLogs to update trees.
        public readonly int[] Logs;
        readonly float[] _regrow;
        readonly HashSet<int> _growing = new HashSet<int>();
        readonly List<int> _grownFull = new List<int>();
        public readonly HashSet<int> ChangedLogs = new HashSet<int>();

        // People.
        public int Population { get; private set; }
        public int WorkersNeeded { get; private set; }
        public int WorkersUsed { get; private set; }
        public bool Hungry { get; private set; }
        float _eat;

        public readonly Dictionary<Item, int> Storage = new Dictionary<Item, int>();
        public readonly Dictionary<Item, int> Delivered = new Dictionary<Item, int>();
        public Building Lodge { get; private set; }
        public float Time { get; private set; }
        public bool GoalReached { get; private set; }
        public float GoalReachedAt { get; private set; } = -1f;
        /// <summary>1 at the start; 2 once the Tier 1 goal is met ("Water Power"); 3 after the Tier 2 goal ("Steam").</summary>
        public int Tier { get; private set; } = 1;
        public float Tier2GoalAt { get; private set; } = -1f;
        public float Tier3GoalAt { get; private set; } = -1f;
        public float Tier4GoalAt { get; private set; } = -1f;

        public World(Balance b)
        {
            B = b;
            W = H = b.MapSize;
            Ground = new Terrain[W * H];
            Logs = new int[W * H];
            _regrow = new float[W * H];
            _occ = new int[W * H];
            _shaftOcc = new int[W * H];
            for (int i = 0; i < _occ.Length; i++) { _occ[i] = -1; _shaftOcc[i] = -1; }
            foreach (Item it in Enum.GetValues(typeof(Item))) { Storage[it] = 0; Delivered[it] = 0; }
            Storage[Item.Wood] = b.StartWood;
            Storage[Item.Stone] = b.StartStone;
            Storage[Item.Iron] = b.StartIron;
            Storage[Item.Fish] = b.StartFish;
            Units = new int[W * H];
            DigMark = new bool[W * H];
            DumpMark = new bool[W * H];
            CoalSeam = new bool[W * H];
        }

        /// <summary>Tiles with a coal seam under them (from CoalFromLevel down). Set by the map.</summary>
        public readonly bool[] CoalSeam;

        public void RestoreTime(float t) => Time = t;

        public void RestoreLogs(IList<int> logs)
        {
            for (int i = 0; i < Logs.Length && i < logs.Count; i++)
            {
                if (Ground[i] != Terrain.Forest) continue;
                Logs[i] = logs[i];
                if (Logs[i] < B.LogsPerTile) _growing.Add(i);
                ChangedLogs.Add(i);
            }
        }

        // ------------------------------------------------------------------ grid

        public bool InBounds(int x, int y) => x >= 0 && y >= 0 && x < W && y < H;
        public Terrain GroundAt(int x, int y) => InBounds(x, y) ? Ground[y * W + x] : Terrain.Water;
        public void SetGround(int x, int y, Terrain t)
        {
            if (!InBounds(x, y)) return;
            int i = y * W + x;
            Ground[i] = t;
            Logs[i] = t == Terrain.Forest ? B.LogsPerTile : 0;
            _growing.Remove(i);
        }

        public int LogsAt(int x, int y) => InBounds(x, y) ? Logs[y * W + x] : 0;

        /// <summary>Fishers must touch the river: any tile right next to the footprint is water.</summary>
        public bool TouchesWater(int x, int y, int size)
        {
            for (int i = -1; i <= size; i++)
            {
                if (GroundAt(x + i, y - 1) == Terrain.Water && InBounds(x + i, y - 1)) return true;
                if (GroundAt(x + i, y + size) == Terrain.Water && InBounds(x + i, y + size)) return true;
                if (GroundAt(x - 1, y + i) == Terrain.Water && InBounds(x - 1, y + i)) return true;
                if (GroundAt(x + size, y + i) == Terrain.Water && InBounds(x + size, y + i)) return true;
            }
            return false;
        }

        public Building At(int x, int y)
        {
            if (!InBounds(x, y)) return null;
            int id = _occ[y * W + x];
            return id < 0 ? null : Buildings[id];
        }

        /// <summary>The line shaft over this tile (shafts sit above belts, so a tile can hold both).</summary>
        public Building ShaftAt(int x, int y)
        {
            if (!InBounds(x, y)) return null;
            int id = _shaftOcc[y * W + x];
            return id < 0 ? null : Buildings[id];
        }

        /// <summary>What a click on this tile means: the building there, else the shaft overhead.</summary>
        public Building AtOrShaft(int x, int y) => At(x, y) ?? ShaftAt(x, y);

        // ------------------------------------------------------------------ building

        public int Count(BuildingType t)
        {
            int n = 0;
            foreach (var b in Buildings.Values) if (b.Type == t) n++;
            return n;
        }

        /// <summary>Returns null if it can be placed here, otherwise the reason (for the HUD).</summary>
        public string CanPlace(BuildingType type, int x, int y, bool ignoreCost = false)
        {
            var def = B.Defs[type];
            int s = def.Size;
            for (int dy = 0; dy < s; dy++)
                for (int dx = 0; dx < s; dx++)
                {
                    if (!InBounds(x + dx, y + dy)) return "Off the map";
                    int oi = (y + dy) * W + x + dx;
                    if (type == BuildingType.Shaft)
                    {
                        // Overhead: fine above a belt or splitter, not through a machine or another shaft.
                        if (_shaftOcc[oi] >= 0) return "A line shaft is already there";
                        if (_occ[oi] >= 0 && Buildings[_occ[oi]].Type != BuildingType.Belt && Buildings[_occ[oi]].Type != BuildingType.Splitter && Buildings[_occ[oi]].Type != BuildingType.Rail)
                            return "Something is already there";
                    }
                    else
                    {
                        if (_occ[oi] >= 0) return "Something is already there";
                        if (_shaftOcc[oi] >= 0 && type != BuildingType.Belt && type != BuildingType.Splitter && type != BuildingType.Rail) return "A line shaft is in the way";
                    }
                }
            for (int dy = 0; dy < s; dy++)
                for (int dx = 0; dx < s; dx++)
                {
                    if (GroundAt(x + dx, y + dy) == Terrain.Water) return "Can't build on the river";
                    int ti = (y + dy) * W + x + dx;
                    if (Units[ti] != 0) return "Ground isn't level (dug or filled)";
                    if (DigMark[ti] || DumpMark[ti]) return "Marked for digging or dumping";
                    if (VehicleOn(x + dx, y + dy)) return "A vehicle is in the way";
                }
            if (!ignoreCost && def.Tier > Tier) return "Unlocks at Tier " + def.Tier;
            if (type == BuildingType.Waterwheel && !TouchesWater(x, y, s)) return "Waterwheels must touch the river";
            if (def.SitsOn != null && ExtractorResource(type, x, y, out _) == Item.None)
                return type == BuildingType.Woodcutter ? "Woodcutters must sit on forest" : "Mines must sit on iron ore or stone";
            if (type == BuildingType.Fisher && !TouchesWater(x, y, s)) return "Fishers must touch the river";
            if (!ignoreCost)
                foreach (var c in def.Cost)
                    if (Storage[c.item] < c.count)
                        return "Need " + c.count + " " + Items.Name(c.item).ToLowerInvariant() + " (have " + Storage[c.item] + ")";
            return null;
        }

        public Building Place(BuildingType type, int x, int y, int facing, int recipe = 0, bool free = false)
        {
            if (CanPlace(type, x, y, free) != null) return null;
            var def = B.Defs[type];
            if (!free) foreach (var c in def.Cost) Storage[c.item] -= c.count;
            var b = new Building { Id = _nextId++, Type = type, X = x, Y = y, Size = def.Size, Facing = facing & 3, Recipe = recipe };
            Buildings[b.Id] = b;
            var layer = type == BuildingType.Shaft ? _shaftOcc : _occ;
            for (int dy = 0; dy < b.Size; dy++)
                for (int dx = 0; dx < b.Size; dx++)
                    layer[(y + dy) * W + x + dx] = b.Id;
            if (type == BuildingType.Belt) _belts.Add(b);
            else if (type == BuildingType.Splitter) _splitters.Add(b);
            else _machines.Add(b);
            if (type == BuildingType.Lodge) Lodge = b;
            _powerDirty = true;
            return b;
        }

        public bool Demolish(int id)
        {
            if (!Buildings.TryGetValue(id, out var b) || b.Type == BuildingType.Lodge) return false;
            foreach (var c in B.Defs[b.Type].Cost) Storage[c.item] += c.count;
            var layer = b.Type == BuildingType.Shaft ? _shaftOcc : _occ;
            for (int dy = 0; dy < b.Size; dy++)
                for (int dx = 0; dx < b.Size; dx++)
                    layer[(b.Y + dy) * W + b.X + dx] = -1;
            Buildings.Remove(id);
            if (b.Type == BuildingType.Garage) RemoveVehiclesOf(b.Id);
            _belts.Remove(b);
            _splitters.Remove(b);
            _machines.Remove(b);
            _powerDirty = true;
            return true;
        }

        public void Rotate(int id)
        {
            if (Buildings.TryGetValue(id, out var b) && b.Type != BuildingType.Lodge) b.Facing = (b.Facing + 1) & 3;
        }

        public void SetRecipe(int id, int recipe)
        {
            if (!Buildings.TryGetValue(id, out var b)) return;
            var def = B.Defs[b.Type];
            if (recipe < 0 || recipe >= def.Recipes.Length || recipe == b.Recipe) return;
            b.Recipe = recipe;
            b.Input.Clear();
            if (!BurnsFuel(b)) { Storage[Item.Wood] += b.FuelWood; Storage[Item.Charcoal] += b.FuelCharcoal; Storage[Item.Coal] += b.FuelCoal; b.FuelWood = b.FuelCharcoal = b.FuelCoal = 0; }
            b.Crafting = false;
            b.Work = 0f;
        }

        /// <summary>What a woodcutter or mine at this spot would pull: the most common ground it can work under it.</summary>
        public Item ExtractorResource(BuildingType type, int x, int y, out int tiles)
        {
            tiles = 0;
            var def = B.Defs[type];
            if (def.SitsOn == null) return Item.None;
            Item best = Item.None;
            foreach (var t in def.SitsOn)
            {
                int n = 0;
                for (int dy = 0; dy < def.Size; dy++)
                    for (int dx = 0; dx < def.Size; dx++)
                        if (GroundAt(x + dx, y + dy) == t) n++;
                if (n > tiles) { tiles = n; best = Items.FromTerrain(t); }
            }
            return best;
        }

        public float ExtractRate(BuildingType type, int tiles) => type == BuildingType.Woodcutter
            ? Math.Min(B.WoodMaxPerSecond, tiles / B.WoodSecondsPerTile)
            : Math.Min(B.ExtractMaxPerSecond, tiles / B.ExtractSecondsPerTile);

        /// <summary>Forest tiles a woodcutter can reach that still have logs (its footprint plus a ring; not under other buildings).</summary>
        public int TreesInReach(Building m, out int bestIndex)
        {
            int n = 0, best = 0;
            bestIndex = -1;
            int r = B.WoodcutterReach;
            for (int y = m.Y - r; y < m.Y + m.Size + r; y++)
                for (int x = m.X - r; x < m.X + m.Size + r; x++)
                {
                    if (!InBounds(x, y)) continue;
                    int i = y * W + x;
                    if (Logs[i] <= 0) continue;
                    int occ = _occ[i];
                    if (occ >= 0 && occ != m.Id) continue;
                    n++;
                    if (Logs[i] > best) { best = Logs[i]; bestIndex = i; }
                }
            return n;
        }
        public Item ExtractorResource(Building b, out int tiles) => ExtractorResource(b.Type, b.X, b.Y, out tiles);

        /// <summary>Does this machine burn fuel right now (by type, or by its current recipe)?</summary>
        public bool BurnsFuel(Building b)
        {
            var def = B.Defs[b.Type];
            if (def.BurnsFuel) return true;
            var r = RecipeOf(b);
            return r != null && r.BurnsFuel;
        }

        public Recipe RecipeOf(Building b)
        {
            var r = B.Defs[b.Type].Recipes;
            return r.Length == 0 ? null : r[Math.Min(b.Recipe, r.Length - 1)];
        }

        // ------------------------------------------------------------------ simulation

        public void Tick(float dt)
        {
            while (dt > 0f)
            {
                float step = dt > 0.05f ? 0.05f : dt;
                dt -= step;
                Step(step);
            }
        }

        void Step(float dt)
        {
            Time += dt;
            StepPeople(dt);
            StepForest(dt);
            if (_powerDirty) RebuildPower();
            StepPower(dt);
            foreach (var t in _belts) StepBelt(t, dt);
            foreach (var sp in _splitters) StepSplitter(sp, dt);
            float speed = Hungry ? B.HungrySpeed : 1f;
            foreach (var m in _machines)
            {
                if (!m.Staffed) { m.Status = "No workers"; continue; }
                StepMachine(m, dt * speed);
            }
            StepVehicles(dt);
            UpdateTier();
        }

        /// <summary>Tests and the dev cheat: jump straight to a tier.</summary>
        public void UnlockTier(int t) { if (t > Tier) Tier = t; if (t >= 2) { GoalReached = true; if (GoalReachedAt < 0f) GoalReachedAt = Time; } }

        /// <summary>Tier goals, checked every step (and after loading a save, from the delivered counts).</summary>
        public void UpdateTier()
        {
            if (!GoalReached && Delivered[Item.Gear] >= B.GoalGear && Delivered[Item.Brick] >= B.GoalBrick)
            {
                GoalReached = true;
                GoalReachedAt = Time;
            }
            if (GoalReached && Tier < 2) Tier = 2;
            if (Tier == 2 && Delivered[Item.Plank] >= B.GoalPlank && Delivered[Item.Plate] >= B.GoalPlate)
            {
                Tier = 3;
                Tier2GoalAt = Time;
            }
            if (Tier == 3 && Delivered[Item.Steel] >= B.GoalSteel && Delivered[Item.Coal] >= B.GoalCoal)
            {
                Tier = 4;
                Tier3GoalAt = Time;
            }
            if (Tier == 4 && RailHauled >= B.GoalRail)
            {
                Tier = 5;
                Tier4GoalAt = Time;
            }
        }

        /// <summary>Homes, who works where (in build order), and eating fish from the lodge.</summary>
        void StepPeople(float dt)
        {
            if (!B.PeopleEnabled)
            {
                foreach (var m in _machines) m.Staffed = true;
                Population = WorkersNeeded = WorkersUsed = 0;
                Hungry = false;
                return;
            }
            int pop = 0, need = 0, used = 0;
            foreach (var m in _machines) pop += B.Defs[m.Type].Homes;
            int free = pop;
            foreach (var m in _machines)
            {
                int w = B.Defs[m.Type].Workers;
                need += w;
                if (w == 0) { m.Staffed = true; continue; }
                m.Staffed = free >= w;
                if (m.Staffed) { free -= w; used += w; }
            }
            Population = pop;
            WorkersNeeded = need;
            WorkersUsed = used;

            _eat += dt * pop / B.EatSecondsPerPerson;
            while (_eat >= 1f)
            {
                _eat -= 1f;
                if (Storage[Item.Fish] > 0) Storage[Item.Fish]--;
            }
            Hungry = pop > 0 && Storage[Item.Fish] <= 0;
        }

        void StepForest(float dt)
        {
            if (_growing.Count == 0) return;
            _grownFull.Clear();
            foreach (int i in _growing)
            {
                _regrow[i] += dt;
                if (_regrow[i] < B.RegrowSecondsPerLog) continue;
                _regrow[i] -= B.RegrowSecondsPerLog;
                Logs[i]++;
                ChangedLogs.Add(i);
                if (Logs[i] >= B.LogsPerTile) _grownFull.Add(i);
            }
            foreach (int i in _grownFull) { _growing.Remove(i); _regrow[i] = 0f; }
        }

        void CutLog(int i)
        {
            Logs[i]--;
            ChangedLogs.Add(i);
            _growing.Add(i);
        }

        void StepSplitter(Building sp, float dt)
        {
            if (sp.BeltItem == Item.None) { sp.Status = "Empty, waiting for items"; return; }
            sp.Status = "Splitting " + Items.Name(sp.BeltItem).ToLowerInvariant();
            if (sp.BeltProgress < 1f) sp.BeltProgress = Math.Min(1f, sp.BeltProgress + B.BeltSpeed * 2f * dt);
            if (sp.BeltProgress < 1f) return;
            // Front, then right, then left, starting from whoever is next in turn.
            for (int k = 0; k < 3; k++)
            {
                int slot = (sp.SplitNext + k) % 3;
                int d = slot == 0 ? sp.Facing : slot == 1 ? (sp.Facing + 1) & 3 : (sp.Facing + 3) & 3;
                var t = At(sp.X + Dir.DX[d], sp.Y + Dir.DY[d]);
                if (t == null) continue;
                if ((t.Type == BuildingType.Belt || t.Type == BuildingType.Splitter) && t.Facing == Dir.Opposite(d)) continue;
                if (TryInsert(t, sp.BeltItem, d))
                {
                    sp.BeltItem = Item.None;
                    sp.BeltProgress = 0f;
                    sp.SplitNext = (slot + 1) % 3;
                    return;
                }
            }
            sp.Status = "All outputs blocked";
        }

        void StepBelt(Building t, float dt)
        {
            if (t.BeltItem == Item.None) return;
            if (t.BeltProgress < 1f) t.BeltProgress = Math.Min(1f, t.BeltProgress + B.BeltSpeed * dt);
            if (t.BeltProgress < 1f) return;
            int nx = t.X + Dir.DX[t.Facing], ny = t.Y + Dir.DY[t.Facing];
            var next = At(nx, ny);
            if (next == null) return;
            if (TryInsert(next, t.BeltItem, t.Facing))
            {
                t.BeltItem = Item.None;
                t.BeltProgress = 0f;
            }
        }

        /// <summary>Push an item into a building from a neighbour moving in direction `moving`.</summary>
        bool TryInsert(Building target, Item item, int moving)
        {
            if (target.Type == BuildingType.Belt)
            {
                if (target.BeltItem != Item.None) return false;
                if (target.Facing == Dir.Opposite(moving)) return false; // head-on
                target.BeltItem = item;
                target.BeltProgress = 0f;
                return true;
            }
            if (target.Type == BuildingType.Splitter)
            {
                if (target.BeltItem != Item.None) return false;
                if (moving == Dir.Opposite(target.Facing)) return false; // can't feed it from its front
                target.BeltItem = item;
                target.BeltProgress = 0f;
                return true;
            }
            return Accept(target, item);
        }

        /// <summary>Machine or hub input.</summary>
        public bool Accept(Building m, Item item)
        {
            switch (m.Type)
            {
                case BuildingType.Lodge:
                    Storage[item]++;
                    Delivered[item]++;
                    return true;
                case BuildingType.Station:
                    if (StationStored(m.Input) >= B.StationCap) return false;
                    m.Input.TryGetValue(item, out int waiting);
                    m.Input[item] = waiting + 1;
                    return true;
                case BuildingType.Rail:
                case BuildingType.Belt:
                case BuildingType.Hopper:   // only trucks fill hoppers
                case BuildingType.Shaft:
                case BuildingType.Waterwheel:
                    return false;
                case BuildingType.Garage:
                    if (!Balance.IsFuel(item) || m.FuelCount >= B.GarageFuelCap) return false;
                    m.AddFuel(item);
                    return true;
                case BuildingType.SteamEngine:
                    if (!Balance.IsFuel(item) || m.FuelCount >= B.FuelCap) return false;
                    m.AddFuel(item);
                    return true;
            }
            // Recipe first (up to 2 crafts' worth), then the fuel slot.
            var r = RecipeOf(m);
            if (r != null)
            {
                int need = r.Needs(item);
                if (need > 0)
                {
                    m.Input.TryGetValue(item, out int have);
                    if (have < need * 2) { m.Input[item] = have + 1; return true; }
                    return false;
                }
            }
            if (Balance.IsFuel(item) && BurnsFuel(m) && m.FuelCount < B.FuelCap)
            {
                m.AddFuel(item);
                return true;
            }
            return false;
        }

        /// <summary>Burns fuel for dt seconds of work. Returns false (and does nothing) when out of fuel.</summary>
        bool Burn(Building m, float dt)
        {
            if (m.FuelLeft <= 0f)
            {
                if (m.FuelCoal > 0) { m.FuelCoal--; m.FuelLeft += B.CoalFuelSeconds; }
                else if (m.FuelCharcoal > 0) { m.FuelCharcoal--; m.FuelLeft += B.CharcoalFuelSeconds; }
                else if (m.FuelWood > 0) { m.FuelWood--; m.FuelLeft += B.WoodFuelSeconds; }
                else return false;
            }
            m.FuelLeft -= dt;
            return true;
        }

        void StepMachine(Building m, float dt)
        {
            switch (m.Type)
            {
                case BuildingType.Woodcutter:
                case BuildingType.Mine: StepExtractor(m, dt); break;
                case BuildingType.Kiln:
                case BuildingType.Smelter:
                case BuildingType.Workshop:
                case BuildingType.Sawmill:
                case BuildingType.TripHammer:
                case BuildingType.Furnace: StepCrafter(m, dt); break;
                case BuildingType.SteamEngine: return;   // fired and its status set in StepPower
                case BuildingType.Waterwheel: m.Status = WheelStatus(m); return;
                case BuildingType.Shaft: m.Status = m.Power > 0f ? "Turning" : m.Power == 0f ? "Stopped (engine out of fuel)" : "Not turning (no waterwheel or engine)"; return;
                case BuildingType.Fisher: StepFisher(m, dt); break;
                case BuildingType.Cabin: m.Status = "Home for " + B.Defs[m.Type].Homes; return;
                case BuildingType.Lodge: m.Status = "Storing"; return;
                case BuildingType.Garage:
                {
                    bool hasVehicles = false;
                    foreach (var v in Vehicles) if (v.GarageId == m.Id) { hasVehicles = true; break; }
                    m.Status = m.FuelCount > 0 || !hasVehicles ? "Working" : "No fuel for vehicles";
                    return;
                }
                case BuildingType.Hopper: StepHopper(m, dt); break;
                case BuildingType.Rail: m.Status = ""; return;
                case BuildingType.Station: StepStation(m, dt); return;
            }
            PushOutput(m);
        }

        void StepFisher(Building m, float dt)
        {
            if (m.OutCount >= B.OutputCap) { m.Status = "Full, waiting for space"; return; }
            m.ExtractTimer += dt / B.FishSeconds;
            while (m.ExtractTimer >= 1f && m.OutCount < B.OutputCap)
            {
                m.ExtractTimer -= 1f;
                m.OutItem = Item.Fish;
                m.OutCount++;
            }
            m.Status = "Working";
        }

        void StepExtractor(Building m, float dt)
        {
            var res = ExtractorResource(m, out int tiles);
            int cutFrom = -1;
            if (m.Type == BuildingType.Woodcutter)
            {
                tiles = TreesInReach(m, out cutFrom);
                if (tiles == 0) { m.Status = "No trees left, waiting for regrowth"; return; }
            }
            if (res == Item.None) { m.Status = "Nothing to extract"; return; }
            if (m.OutCount > 0 && m.OutItem != res) { m.Status = "Full, waiting for space"; return; }
            if (m.OutCount >= B.OutputCap) { m.Status = "Full, waiting for space"; return; }
            if (!Run(m, ref dt, B.Defs[m.Type].BurnsFuel)) return;
            float perSecond = ExtractRate(m.Type, tiles);
            m.ExtractTimer += dt * perSecond;
            while (m.ExtractTimer >= 1f && m.OutCount < B.OutputCap)
            {
                m.ExtractTimer -= 1f;
                if (m.Type == BuildingType.Woodcutter)
                {
                    TreesInReach(m, out cutFrom);
                    if (cutFrom < 0) break;
                    CutLog(cutFrom);
                }
                m.OutItem = res;
                m.OutCount++;
            }
            SetWorking(m);
        }

        void StepCrafter(Building m, float dt)
        {
            var r = RecipeOf(m);
            if (!m.Crafting)
            {
                if (m.OutCount + r.OutCount > B.OutputCap) { m.Status = "Full, waiting for space"; return; }
                for (int i = 0; i < r.InItems.Length; i++)
                {
                    m.Input.TryGetValue(r.InItems[i], out int have);
                    if (have < r.InCounts[i]) { m.Status = "Waiting for " + Items.Name(r.InItems[i]).ToLowerInvariant(); return; }
                }
                for (int i = 0; i < r.InItems.Length; i++) m.Input[r.InItems[i]] -= r.InCounts[i];
                m.Crafting = true;
                m.Work = 0f;
            }
            if (!Run(m, ref dt, r.BurnsFuel)) return;
            m.Work += dt;
            SetWorking(m);
            if (m.Work >= r.Seconds)
            {
                if (m.OutCount > 0 && m.OutItem != r.Out) { m.Status = "Full, waiting for space"; m.Work = r.Seconds; return; }
                m.Crafting = false;
                m.Work = 0f;
                m.OutItem = r.Out;
                m.OutCount += r.OutCount;
            }
        }

        void PushOutput(Building m)
        {
            if (m.OutCount <= 0) return;
            foreach (var (x, y) in FrontTiles(m))
            {
                if (m.OutCount <= 0) break;
                var t = At(x, y);
                if (t == null || t == m) continue;
                if (t.Type == BuildingType.Belt)
                {
                    if (t.Facing == Dir.Opposite(m.Facing) || t.BeltItem != Item.None) continue;
                }
                if (TryInsert(t, m.OutItem, m.Facing))
                {
                    m.OutCount--;
                    if (m.OutCount == 0) m.OutItem = Item.None;
                }
            }
            if (m.OutCount >= B.OutputCap && m.Status == "Working") m.Status = "Full, waiting for space";
        }

        /// <summary>The tiles just in front of a building's front edge.</summary>
        public IEnumerable<(int x, int y)> FrontTiles(Building m)
        {
            int s = m.Size;
            switch (m.Facing)
            {
                case 0: for (int i = 0; i < s; i++) yield return (m.X + i, m.Y + s); break;
                case 1: for (int i = 0; i < s; i++) yield return (m.X + s, m.Y + i); break;
                case 2: for (int i = 0; i < s; i++) yield return (m.X + i, m.Y - 1); break;
                default: for (int i = 0; i < s; i++) yield return (m.X - 1, m.Y + i); break;
            }
        }
    }
}
