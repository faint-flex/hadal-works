using System.Collections.Generic;

namespace Kilnwood.Sim
{
    public enum Terrain { Grass, Forest, IronOre, Stone, Water }

    public enum Item { None, Wood, Ore, Stone, Charcoal, Iron, Brick, Gear, Fish }

    public enum BuildingType { Belt, Woodcutter, Mine, Kiln, Smelter, Workshop, Lodge, Fisher, Cabin, Splitter }

    /// <summary>0 = north (+y), 1 = east (+x), 2 = south (-y), 3 = west (-x).</summary>
    public static class Dir
    {
        public static readonly int[] DX = { 0, 1, 0, -1 };
        public static readonly int[] DY = { 1, 0, -1, 0 };
        public static int Opposite(int d) => (d + 2) & 3;
        public static readonly string[] Names = { "north", "east", "south", "west" };
    }

    public class Recipe
    {
        public string Name;
        public Item[] InItems;
        public int[] InCounts;
        public Item Out;
        public float Seconds;
        public bool BurnsFuel;

        public int Needs(Item it)
        {
            for (int i = 0; i < InItems.Length; i++) if (InItems[i] == it) return InCounts[i];
            return 0;
        }
    }

    public class BuildingDef
    {
        public BuildingType Type;
        public string Name;
        public int Size;
        public (Item item, int count)[] Cost = new (Item, int)[0];
        public bool BurnsFuel;               // extractors: always burns fuel while working
        public Terrain[] SitsOn;             // extractors: which ground they work on
        public Recipe[] Recipes = new Recipe[0];
        public char Key;
        public int Workers;                  // people needed to run it
        public int Homes;                    // people who can live in it

        public string CostText()
        {
            var parts = new List<string>();
            foreach (var c in Cost) parts.Add(c.count + " " + Items.Name(c.item).ToLowerInvariant());
            return parts.Count == 0 ? "free" : string.Join(" + ", parts);
        }
    }

    public static class Items
    {
        public static string Name(Item i)
        {
            switch (i)
            {
                case Item.Wood: return "Wood";
                case Item.Ore: return "Iron ore";
                case Item.Stone: return "Stone";
                case Item.Charcoal: return "Charcoal";
                case Item.Iron: return "Iron bar";
                case Item.Brick: return "Brick";
                case Item.Gear: return "Gear";
                case Item.Fish: return "Fish";
                default: return "-";
            }
        }

        /// <summary>One-letter badge shown on every item (colour is never the only cue).</summary>
        public static string Letter(Item i)
        {
            switch (i)
            {
                case Item.Wood: return "W";
                case Item.Ore: return "O";
                case Item.Stone: return "S";
                case Item.Charcoal: return "C";
                case Item.Iron: return "I";
                case Item.Brick: return "B";
                case Item.Gear: return "G";
                case Item.Fish: return "F";
                default: return "";
            }
        }

        public static Item FromTerrain(Terrain t)
        {
            switch (t)
            {
                case Terrain.Forest: return Item.Wood;
                case Terrain.IronOre: return Item.Ore;
                case Terrain.Stone: return Item.Stone;
                default: return Item.None;
            }
        }
    }

    /// <summary>Every tunable number for Milestone 1. Mirrors /mnt/project-files/kilnwood/design-m1.md.</summary>
    public class Balance
    {
        public int MapSize = 64;
        public float BeltSpeed = 2f;             // tiles per second
        public float ExtractSecondsPerTile = 2f; // mines: one item per covered resource tile every 2 s
        public float WoodSecondsPerTile = 3f;    // woodcutters: one wood per forest tile (with logs) every 3 s
        public float WoodMaxPerSecond = 4f / 3f;
        public int LogsPerTile = 8;
        public float RegrowSecondsPerLog = 15f;
        public int WoodcutterReach = 1;          // tiles of forest around the woodcutter it can cut
        public float FishSeconds = 4f;           // a fisher makes 1 fish every 4 s
        public float EatSecondsPerPerson = 90f;  // each person eats 1 fish every 90 s
        public float HungrySpeed = 0.5f;
        public int StartFish = 20;
        public float ExtractMaxPerSecond = 2f;
        public int OutputCap = 4;
        public int FuelCap = 4;                  // fuel items a machine can hold
        public float WoodFuelSeconds = 8f;
        public float CharcoalFuelSeconds = 24f;
        public int StartWood = 60;
        public int StartStone = 60;
        public int StartIron = 12;
        public int GoalGear = 100;
        public int GoalBrick = 120;

        public readonly Dictionary<BuildingType, BuildingDef> Defs = new Dictionary<BuildingType, BuildingDef>();

        public float FuelSeconds(Item it) => it == Item.Charcoal ? CharcoalFuelSeconds : it == Item.Wood ? WoodFuelSeconds : 0f;
        public static bool IsFuel(Item it) => it == Item.Wood || it == Item.Charcoal;

        public Balance()
        {
            Add(new BuildingDef { Type = BuildingType.Belt, Name = "Belt", Size = 1, Cost = new[] { (Item.Wood, 1) }, Key = '1' });
            Add(new BuildingDef { Type = BuildingType.Fisher, Name = "Fisher", Size = 2, Cost = new[] { (Item.Wood, 6) }, Key = '7', Workers = 1 });
            Add(new BuildingDef { Type = BuildingType.Cabin, Name = "Cabin", Size = 2, Cost = new[] { (Item.Wood, 8), (Item.Stone, 4) }, Key = '8', Homes = 4 });
            Add(new BuildingDef { Type = BuildingType.Splitter, Name = "Splitter", Size = 1, Cost = new[] { (Item.Wood, 2) }, Key = '9' });
            Add(new BuildingDef
            {
                Type = BuildingType.Woodcutter, Name = "Woodcutter", Size = 2, Cost = new[] { (Item.Wood, 6) }, Key = '2',
                SitsOn = new[] { Terrain.Forest }, Workers = 1,
            });
            Add(new BuildingDef
            {
                Type = BuildingType.Mine, Name = "Mine", Size = 2, Cost = new[] { (Item.Wood, 8), (Item.Stone, 4) }, Key = '3',
                SitsOn = new[] { Terrain.IronOre, Terrain.Stone }, BurnsFuel = true, Workers = 2,
            });
            Add(new BuildingDef
            {
                Type = BuildingType.Kiln, Name = "Kiln", Size = 2, Cost = new[] { (Item.Stone, 10) }, Key = '4', Workers = 1,
                Recipes = new[]
                {
                    new Recipe { Name = "Charcoal", InItems = new[] { Item.Wood }, InCounts = new[] { 2 }, Out = Item.Charcoal, Seconds = 3f },
                    new Recipe { Name = "Brick", InItems = new[] { Item.Stone }, InCounts = new[] { 2 }, Out = Item.Brick, Seconds = 4f, BurnsFuel = true },
                }
            });
            Add(new BuildingDef
            {
                Type = BuildingType.Smelter, Name = "Smelter", Size = 2, Cost = new[] { (Item.Stone, 12), (Item.Wood, 4) }, Key = '5', Workers = 2,
                Recipes = new[]
                {
                    new Recipe { Name = "Iron bar", InItems = new[] { Item.Ore, Item.Charcoal }, InCounts = new[] { 2, 1 }, Out = Item.Iron, Seconds = 3f },
                }
            });
            Add(new BuildingDef
            {
                Type = BuildingType.Workshop, Name = "Workshop", Size = 2, Cost = new[] { (Item.Wood, 10), (Item.Iron, 6) }, Key = '6', Workers = 2,
                Recipes = new[]
                {
                    new Recipe { Name = "Gear", InItems = new[] { Item.Iron }, InCounts = new[] { 2 }, Out = Item.Gear, Seconds = 4f, BurnsFuel = true },
                }
            });
            Add(new BuildingDef { Type = BuildingType.Lodge, Name = "Lodge", Size = 4, Homes = 6 });
        }

        void Add(BuildingDef d) => Defs[d.Type] = d;
    }
}
