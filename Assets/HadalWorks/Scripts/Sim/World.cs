using System;
using System.Collections.Generic;

namespace HadalWorks.Sim
{
    public class Building
    {
        public int Id;
        public BuildingType Type;
        public int X, Y;          // bottom-left tile
        public int Size;
        public int Facing;        // Dir; for tubes, the direction items move
        public int Recipe;        // processors / fabricators

        // Tube state
        public Item TubeItem;
        public float TubeProgress;

        // Machine state
        public readonly Dictionary<Item, int> Input = new Dictionary<Item, int>();
        public Item OutItem;
        public int OutCount;
        public float Work;        // seconds of progress on the current craft
        public bool Crafting;
        public float ExtractTimer;
        public string Status = "";

        public bool Covers(int x, int y) => x >= X && x < X + Size && y >= Y && y < Y + Size;
    }

    /// <summary>
    /// The seabed factory, simulated in seconds. Pure C# (no Unity) so the rules are unit-testable.
    /// </summary>
    public class World
    {
        public readonly Balance B;
        public readonly int W, H;
        public readonly Terrain[] Ground;
        readonly int[] _occ;                       // building id per tile, -1 empty
        public readonly Dictionary<int, Building> Buildings = new Dictionary<int, Building>();
        readonly List<Building> _tubes = new List<Building>();
        readonly List<Building> _machines = new List<Building>();
        int _nextId = 1;

        public readonly Dictionary<Item, int> Storage = new Dictionary<Item, int>();
        public readonly Dictionary<Item, int> Delivered = new Dictionary<Item, int>();
        public Building Hub { get; private set; }
        public float Time { get; private set; }
        public bool GoalReached { get; private set; }
        public float GoalReachedAt { get; private set; } = -1f;

        public float PowerSupply { get; private set; }
        public float PowerDemand { get; private set; }
        public float PowerRatio { get; private set; } = 1f;

        public World(Balance b)
        {
            B = b;
            W = H = b.MapSize;
            Ground = new Terrain[W * H];
            _occ = new int[W * H];
            for (int i = 0; i < _occ.Length; i++) _occ[i] = -1;
            foreach (Item it in Enum.GetValues(typeof(Item))) { Storage[it] = 0; Delivered[it] = 0; }
            Storage[Item.Plate] = b.StartPlates;
            Storage[Item.Glass] = b.StartGlass;
        }

        public void RestoreTime(float t) => Time = t;

        // ------------------------------------------------------------------ grid

        public bool InBounds(int x, int y) => x >= 0 && y >= 0 && x < W && y < H;
        public Terrain GroundAt(int x, int y) => InBounds(x, y) ? Ground[y * W + x] : Terrain.Sand;
        public void SetGround(int x, int y, Terrain t) { if (InBounds(x, y)) Ground[y * W + x] = t; }

        public Building At(int x, int y)
        {
            if (!InBounds(x, y)) return null;
            int id = _occ[y * W + x];
            return id < 0 ? null : Buildings[id];
        }

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
                    if (_occ[(y + dy) * W + x + dx] >= 0) return "Something is already there";
                }
            if (type == BuildingType.VentTap && GroundAt(x, y) != Terrain.Vent) return "Vent taps go on a thermal vent";
            if (type != BuildingType.VentTap)
                for (int dy = 0; dy < s; dy++)
                    for (int dx = 0; dx < s; dx++)
                        if (GroundAt(x + dx, y + dy) == Terrain.Vent) return "Keep vents clear (vent taps only)";
            if (type == BuildingType.Extractor && ExtractorResource(x, y, s, out _) == Item.None) return "Extractors must sit on ferrite, silica or kelp";
            if (!ignoreCost)
            {
                if (Storage[Item.Plate] < def.CostPlate) return "Need " + def.CostPlate + " plates (have " + Storage[Item.Plate] + ")";
                if (Storage[Item.Glass] < def.CostGlass) return "Need " + def.CostGlass + " glass (have " + Storage[Item.Glass] + ")";
            }
            return null;
        }

        public Building Place(BuildingType type, int x, int y, int facing, int recipe = 0, bool free = false)
        {
            if (CanPlace(type, x, y, free) != null) return null;
            var def = B.Defs[type];
            if (!free)
            {
                Storage[Item.Plate] -= def.CostPlate;
                Storage[Item.Glass] -= def.CostGlass;
            }
            var b = new Building { Id = _nextId++, Type = type, X = x, Y = y, Size = def.Size, Facing = facing & 3, Recipe = recipe };
            Buildings[b.Id] = b;
            for (int dy = 0; dy < b.Size; dy++)
                for (int dx = 0; dx < b.Size; dx++)
                    _occ[(y + dy) * W + x + dx] = b.Id;
            if (type == BuildingType.Tube) _tubes.Add(b);
            else _machines.Add(b);
            if (type == BuildingType.Hub) Hub = b;
            return b;
        }

        public bool Demolish(int id)
        {
            if (!Buildings.TryGetValue(id, out var b) || b.Type == BuildingType.Hub) return false;
            var def = B.Defs[b.Type];
            Storage[Item.Plate] += def.CostPlate;
            Storage[Item.Glass] += def.CostGlass;
            for (int dy = 0; dy < b.Size; dy++)
                for (int dx = 0; dx < b.Size; dx++)
                    _occ[(b.Y + dy) * W + b.X + dx] = -1;
            Buildings.Remove(id);
            _tubes.Remove(b);
            _machines.Remove(b);
            return true;
        }

        public void Rotate(int id)
        {
            if (Buildings.TryGetValue(id, out var b) && b.Type != BuildingType.Hub) b.Facing = (b.Facing + 1) & 3;
        }

        public void SetRecipe(int id, int recipe)
        {
            if (!Buildings.TryGetValue(id, out var b)) return;
            var def = B.Defs[b.Type];
            if (recipe < 0 || recipe >= def.Recipes.Length || recipe == b.Recipe) return;
            b.Recipe = recipe;
            b.Input.Clear();
            b.Crafting = false;
            b.Work = 0f;
        }

        /// <summary>The resource an extractor at this spot would pull: the most common one under it.</summary>
        public Item ExtractorResource(int x, int y, int size, out int tiles)
        {
            int f = 0, s = 0, k = 0;
            for (int dy = 0; dy < size; dy++)
                for (int dx = 0; dx < size; dx++)
                    switch (GroundAt(x + dx, y + dy))
                    {
                        case Terrain.Ferrite: f++; break;
                        case Terrain.Silica: s++; break;
                        case Terrain.Kelp: k++; break;
                    }
            tiles = Math.Max(f, Math.Max(s, k));
            if (tiles == 0) return Item.None;
            if (f == tiles) return Item.Ferrite;
            if (s == tiles) return Item.Silica;
            return Item.Kelp;
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
            UpdatePower();
            foreach (var t in _tubes) StepTube(t, dt);
            foreach (var m in _machines) StepMachine(m, dt * PowerRatio);
            if (!GoalReached && Delivered[Item.Hull] >= B.GoalHull && Delivered[Item.Viewport] >= B.GoalViewport)
            {
                GoalReached = true;
                GoalReachedAt = Time;
            }
        }

        void UpdatePower()
        {
            float supply = 0f, demand = 0f;
            foreach (var m in _machines)
            {
                var def = B.Defs[m.Type];
                supply += def.Supply;
                demand += def.Power;
            }
            PowerSupply = supply;
            PowerDemand = demand;
            PowerRatio = demand <= 0f ? 1f : Math.Min(1f, supply / demand);
        }

        void StepTube(Building t, float dt)
        {
            if (t.TubeItem == Item.None) return;
            if (t.TubeProgress < 1f) t.TubeProgress = Math.Min(1f, t.TubeProgress + B.TubeSpeed * dt);
            if (t.TubeProgress < 1f) return;
            int nx = t.X + Dir.DX[t.Facing], ny = t.Y + Dir.DY[t.Facing];
            var next = At(nx, ny);
            if (next == null) return;
            if (TryInsert(next, t.TubeItem, t.Facing))
            {
                t.TubeItem = Item.None;
                t.TubeProgress = 0f;
            }
        }

        /// <summary>Push an item into a building from a neighbour moving in direction `moving`.</summary>
        bool TryInsert(Building target, Item item, int moving)
        {
            if (target.Type == BuildingType.Tube)
            {
                if (target.TubeItem != Item.None) return false;
                if (target.Facing == Dir.Opposite(moving)) return false; // head-on
                target.TubeItem = item;
                target.TubeProgress = 0f;
                return true;
            }
            return Accept(target, item);
        }

        /// <summary>Machine or hub input.</summary>
        public bool Accept(Building m, Item item)
        {
            switch (m.Type)
            {
                case BuildingType.Hub:
                    Storage[item]++;
                    Delivered[item]++;
                    return true;
                case BuildingType.Processor:
                case BuildingType.Fabricator:
                    var r = RecipeOf(m);
                    int need = r.Needs(item);
                    if (need == 0) return false;
                    m.Input.TryGetValue(item, out int have);
                    if (have >= need * 2) return false;
                    m.Input[item] = have + 1;
                    return true;
                default:
                    return false;
            }
        }

        void StepMachine(Building m, float dt)
        {
            switch (m.Type)
            {
                case BuildingType.Extractor: StepExtractor(m, dt); break;
                case BuildingType.Processor:
                case BuildingType.Fabricator: StepCrafter(m, dt); break;
                case BuildingType.VentTap: m.Status = "Supplying " + B.Defs[m.Type].Supply + " power"; return;
                case BuildingType.Hub: m.Status = "Storing"; return;
            }
            PushOutput(m);
        }

        void StepExtractor(Building m, float dt)
        {
            var res = ExtractorResource(m.X, m.Y, m.Size, out int tiles);
            if (res == Item.None) { m.Status = "Nothing to extract"; return; }
            if (m.OutCount > 0 && m.OutItem != res) { m.Status = "Full, waiting for space"; return; }
            if (m.OutCount >= B.OutputCap) { m.Status = "Full, waiting for space"; return; }
            float perSecond = Math.Min(B.ExtractMaxPerSecond, tiles / B.ExtractSecondsPerTile);
            m.ExtractTimer += dt * perSecond;
            while (m.ExtractTimer >= 1f && m.OutCount < B.OutputCap)
            {
                m.ExtractTimer -= 1f;
                m.OutItem = res;
                m.OutCount++;
            }
            m.Status = PowerRatio < 0.999f ? "Low power" : "Working";
        }

        void StepCrafter(Building m, float dt)
        {
            var r = RecipeOf(m);
            if (!m.Crafting)
            {
                if (m.OutCount >= B.OutputCap) { m.Status = "Full, waiting for space"; return; }
                for (int i = 0; i < r.InItems.Length; i++)
                {
                    m.Input.TryGetValue(r.InItems[i], out int have);
                    if (have < r.InCounts[i]) { m.Status = "Waiting for " + Items.Name(r.InItems[i]).ToLowerInvariant(); return; }
                }
                for (int i = 0; i < r.InItems.Length; i++) m.Input[r.InItems[i]] -= r.InCounts[i];
                m.Crafting = true;
                m.Work = 0f;
            }
            m.Work += dt;
            m.Status = PowerRatio < 0.999f ? "Low power" : "Working";
            if (m.Work >= r.Seconds)
            {
                if (m.OutCount > 0 && m.OutItem != r.Out) { m.Status = "Full, waiting for space"; m.Work = r.Seconds; return; }
                m.Crafting = false;
                m.Work = 0f;
                m.OutItem = r.Out;
                m.OutCount++;
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
                if (t.Type == BuildingType.Tube)
                {
                    if (t.Facing == Dir.Opposite(m.Facing) || t.TubeItem != Item.None) continue;
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
