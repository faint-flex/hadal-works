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
        public Item OutItem;
        public int OutCount;
        public float Work;        // seconds of progress on the current craft
        public bool Crafting;
        public float ExtractTimer;
        public int FuelWood, FuelCharcoal; // fuel items waiting to burn
        public float FuelLeft;             // seconds of burn left from the last fuel item
        public int FuelCount => FuelWood + FuelCharcoal;
        public string Status = "";

        public bool Covers(int x, int y) => x >= X && x < X + Size && y >= Y && y < Y + Size;
    }

    /// <summary>
    /// The valley factory, simulated in seconds. Pure C# (no Unity) so the rules are unit-testable.
    /// </summary>
    public class World
    {
        public readonly Balance B;
        public readonly int W, H;
        public readonly Terrain[] Ground;
        readonly int[] _occ;                       // building id per tile, -1 empty
        public readonly Dictionary<int, Building> Buildings = new Dictionary<int, Building>();
        readonly List<Building> _belts = new List<Building>();
        readonly List<Building> _machines = new List<Building>();
        int _nextId = 1;

        public readonly Dictionary<Item, int> Storage = new Dictionary<Item, int>();
        public readonly Dictionary<Item, int> Delivered = new Dictionary<Item, int>();
        public Building Lodge { get; private set; }
        public float Time { get; private set; }
        public bool GoalReached { get; private set; }
        public float GoalReachedAt { get; private set; } = -1f;

        public World(Balance b)
        {
            B = b;
            W = H = b.MapSize;
            Ground = new Terrain[W * H];
            _occ = new int[W * H];
            for (int i = 0; i < _occ.Length; i++) _occ[i] = -1;
            foreach (Item it in Enum.GetValues(typeof(Item))) { Storage[it] = 0; Delivered[it] = 0; }
            Storage[Item.Wood] = b.StartWood;
            Storage[Item.Stone] = b.StartStone;
            Storage[Item.Iron] = b.StartIron;
        }

        public void RestoreTime(float t) => Time = t;

        // ------------------------------------------------------------------ grid

        public bool InBounds(int x, int y) => x >= 0 && y >= 0 && x < W && y < H;
        public Terrain GroundAt(int x, int y) => InBounds(x, y) ? Ground[y * W + x] : Terrain.Water;
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
            for (int dy = 0; dy < s; dy++)
                for (int dx = 0; dx < s; dx++)
                    if (GroundAt(x + dx, y + dy) == Terrain.Water) return "Can't build on the river";
            if (def.SitsOn != null && ExtractorResource(type, x, y, out _) == Item.None)
                return type == BuildingType.Woodcutter ? "Woodcutters must sit on forest" : "Mines must sit on iron ore or stone";
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
            for (int dy = 0; dy < b.Size; dy++)
                for (int dx = 0; dx < b.Size; dx++)
                    _occ[(y + dy) * W + x + dx] = b.Id;
            if (type == BuildingType.Belt) _belts.Add(b);
            else _machines.Add(b);
            if (type == BuildingType.Lodge) Lodge = b;
            return b;
        }

        public bool Demolish(int id)
        {
            if (!Buildings.TryGetValue(id, out var b) || b.Type == BuildingType.Lodge) return false;
            foreach (var c in B.Defs[b.Type].Cost) Storage[c.item] += c.count;
            for (int dy = 0; dy < b.Size; dy++)
                for (int dx = 0; dx < b.Size; dx++)
                    _occ[(b.Y + dy) * W + b.X + dx] = -1;
            Buildings.Remove(id);
            _belts.Remove(b);
            _machines.Remove(b);
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
            if (!BurnsFuel(b)) { Storage[Item.Wood] += b.FuelWood; Storage[Item.Charcoal] += b.FuelCharcoal; b.FuelWood = b.FuelCharcoal = 0; }
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

        public float ExtractRate(BuildingType type, int tiles) =>
            Math.Min(B.ExtractMaxPerSecond, tiles / (type == BuildingType.Woodcutter ? B.WoodSecondsPerTile : B.ExtractSecondsPerTile));

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
            foreach (var t in _belts) StepBelt(t, dt);
            foreach (var m in _machines) StepMachine(m, dt);
            if (!GoalReached && Delivered[Item.Gear] >= B.GoalGear && Delivered[Item.Brick] >= B.GoalBrick)
            {
                GoalReached = true;
                GoalReachedAt = Time;
            }
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
                case BuildingType.Belt:
                    return false;
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
                if (item == Item.Charcoal) m.FuelCharcoal++; else m.FuelWood++;
                return true;
            }
            return false;
        }

        /// <summary>Burns fuel for dt seconds of work. Returns false (and does nothing) when out of fuel.</summary>
        bool Burn(Building m, float dt)
        {
            if (m.FuelLeft <= 0f)
            {
                if (m.FuelCharcoal > 0) { m.FuelCharcoal--; m.FuelLeft += B.CharcoalFuelSeconds; }
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
                case BuildingType.Workshop: StepCrafter(m, dt); break;
                case BuildingType.Lodge: m.Status = "Storing"; return;
            }
            PushOutput(m);
        }

        void StepExtractor(Building m, float dt)
        {
            var res = ExtractorResource(m, out int tiles);
            if (res == Item.None) { m.Status = "Nothing to extract"; return; }
            if (m.OutCount > 0 && m.OutItem != res) { m.Status = "Full, waiting for space"; return; }
            if (m.OutCount >= B.OutputCap) { m.Status = "Full, waiting for space"; return; }
            if (B.Defs[m.Type].BurnsFuel && !Burn(m, dt)) { m.Status = "No fuel"; return; }
            float perSecond = ExtractRate(m.Type, tiles);
            m.ExtractTimer += dt * perSecond;
            while (m.ExtractTimer >= 1f && m.OutCount < B.OutputCap)
            {
                m.ExtractTimer -= 1f;
                m.OutItem = res;
                m.OutCount++;
            }
            m.Status = "Working";
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
            if (r.BurnsFuel && !Burn(m, dt)) { m.Status = "No fuel"; return; }
            m.Work += dt;
            m.Status = "Working";
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
