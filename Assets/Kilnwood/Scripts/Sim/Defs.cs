using System.Collections.Generic;

namespace Kilnwood.Sim
{
    public enum Terrain { Grass, Forest, IronOre, Stone, Water }

    public enum Item { None, Wood, Ore, Stone, Charcoal, Iron, Brick, Gear, Fish, Dirt, Plank, Plate, Coal, Steel, Wire }

    public enum BuildingType { Belt, Woodcutter, Mine, Kiln, Smelter, Workshop, Lodge, Fisher, Cabin, Splitter, Garage, Hopper, Shaft, Waterwheel, Sawmill, TripHammer, SteamEngine, Furnace, Rail, Station, Dynamo, Pole, WireMill }

    public enum VehicleType { Excavator, Truck, Train }

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
        public int OutCount = 1;
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
        public int Tier = 1;                 // unlocks at this tier
        public int Hp;                       // power it needs (or, for a waterwheel, gives)
        public bool NeedsPower;              // can't run at all without power (sawmill, trip hammer)
        public int Kw;                       // electricity it needs (or, for a dynamo, gives at full speed)
        public bool NeedsElectric;           // runs only on an electric grid (wire mill)
        public int Page => Tier;            // build bar page

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
                case Item.Dirt: return "Dirt";
                case Item.Plank: return "Plank";
                case Item.Plate: return "Iron plate";
                case Item.Coal: return "Coal";
                case Item.Steel: return "Steel";
                case Item.Wire: return "Steel wire";
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
                case Item.Dirt: return "D";
                case Item.Plank: return "P";
                case Item.Plate: return "L";
                case Item.Coal: return "K";
                case Item.Steel: return "T";
                case Item.Wire: return "E";
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
        public bool PeopleEnabled = false;       // workers + food: built, but parked until LyFe wants them (2026-11-24)
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

        // 0.5 "Open Pit": digging, excavators and trucks.
        public int MaxDigDepth = 4;              // levels below the surface (1 level = 1 m)
        public int MaxDumpHeight = 2;            // levels above the surface
        public int UnitsPerLevel = 8;            // items in one level of one tile
        public float DigSeconds = 1.5f;          // excavator: one unit every 1.5 s
        public int BucketCap = 2;
        public float ExcavatorSpeed = 2f;        // tiles per second
        public float TruckSpeed = 4f;
        public int TruckCap = 8;
        public float TipSeconds = 2f;            // truck tipping its load
        public float TankSeconds = 90f;          // vehicle fuel tank, used while driving or digging
        public float RefuelBelow = 15f;
        public int GarageFuelCap = 12;
        public int HopperCap = 40;
        public float HopperOutPerSecond = 2f;
        public (Item item, int count)[] ExcavatorCost = { (Item.Iron, 20), (Item.Wood, 10) };
        public (Item item, int count)[] TruckCost = { (Item.Iron, 12), (Item.Wood, 8) };

        // 0.6 Tier 2 "Water Power".
        public int WheelHp = 10;
        public int GoalPlank = 200;
        public int GoalPlate = 100;

        // 0.7 Tier 3 "Steam".
        public float CoalFuelSeconds = 40f;
        public int CoalFromLevel = 2;            // coal seams start this many levels down
        public int EngineHp = 30;
        public float EngineBurnRate = 3f;        // an engine burns fuel 3x as fast as a machine fire
        public int GoalSteel = 100;
        public int GoalCoal = 100;

        // 0.8 Tier 4 "Rails".
        public int StationCap = 60;              // items waiting to be loaded, and items unloaded waiting to go out
        public int TrainCap = 24;                // a locomotive and two wagons
        public float TrainSpeed = 5f;            // tiles per second on the rails
        public float TrainStopSeconds = 3f;      // loading / unloading at a station
        public int TrainFuelLoad = 12;           // fuel a train takes from the lodge for the far station's machines
        public int YardRange = 6;                // a station this close to the lodge unloads straight into it
        public int GoalRail = 400;               // items hauled to the lodge by train
        public (Item item, int count)[] TrainCost = { (Item.Steel, 20), (Item.Plate, 10), (Item.Gear, 8) };

        // 0.10 Tier 5 "Electricity": dynamos on the shaft network feed a grid of timber poles.
        public int DynamoHp = 12;                // shaft power a dynamo takes
        public int DynamoKw = 12;                // electricity it makes at full shaft speed
        public int PoleReach = 8;                // poles (and dynamos) link up to this many tiles apart
        public int PoleCover = 2;                // machines this close to a pole or dynamo join its grid
        public int WireMillKw = 8;
        public int GoalWire = 150;               // steel wire delivered to the lodge

        public (Item item, int count)[] VehicleCost(VehicleType t) => t == VehicleType.Excavator ? ExcavatorCost : t == VehicleType.Train ? TrainCost : TruckCost;

        public readonly Dictionary<BuildingType, BuildingDef> Defs = new Dictionary<BuildingType, BuildingDef>();

        public float FuelSeconds(Item it) => it == Item.Coal ? CoalFuelSeconds : it == Item.Charcoal ? CharcoalFuelSeconds : it == Item.Wood ? WoodFuelSeconds : 0f;
        public static bool IsFuel(Item it) => it == Item.Wood || it == Item.Charcoal || it == Item.Coal;

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
                SitsOn = new[] { Terrain.IronOre, Terrain.Stone }, BurnsFuel = true, Workers = 2, Hp = 3,
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
                Type = BuildingType.Workshop, Name = "Workshop", Size = 2, Cost = new[] { (Item.Wood, 10), (Item.Iron, 6) }, Key = '6', Workers = 2, Hp = 3,
                Recipes = new[]
                {
                    new Recipe { Name = "Gear", InItems = new[] { Item.Iron }, InCounts = new[] { 2 }, Out = Item.Gear, Seconds = 4f, BurnsFuel = true },
                }
            });
            Add(new BuildingDef { Type = BuildingType.Lodge, Name = "Lodge", Size = 4, Homes = 6 });
            Add(new BuildingDef { Type = BuildingType.Garage, Name = "Garage", Size = 2, Cost = new[] { (Item.Wood, 12), (Item.Stone, 8), (Item.Iron, 4) }, Key = '7' });
            Add(new BuildingDef { Type = BuildingType.Shaft, Name = "Line shaft", Size = 1, Cost = new[] { (Item.Wood, 1) }, Key = '1', Tier = 2 });
            Add(new BuildingDef
            {
                Type = BuildingType.Waterwheel, Name = "Waterwheel", Size = 2, Key = '2', Tier = 2, Hp = WheelHp,
                Cost = new[] { (Item.Wood, 20), (Item.Brick, 10), (Item.Gear, 8) },
            });
            Add(new BuildingDef
            {
                Type = BuildingType.Sawmill, Name = "Sawmill", Size = 2, Key = '3', Tier = 2, Hp = 3, NeedsPower = true,
                Cost = new[] { (Item.Wood, 10), (Item.Brick, 4), (Item.Gear, 4) },
                Recipes = new[] { new Recipe { Name = "Plank", InItems = new[] { Item.Wood }, InCounts = new[] { 1 }, Out = Item.Plank, OutCount = 2, Seconds = 2f } },
            });
            Add(new BuildingDef
            {
                Type = BuildingType.TripHammer, Name = "Trip hammer", Size = 2, Key = '4', Tier = 2, Hp = 4, NeedsPower = true,
                Cost = new[] { (Item.Plank, 8), (Item.Brick, 6), (Item.Gear, 4) },
                Recipes = new[] { new Recipe { Name = "Iron plate", InItems = new[] { Item.Iron }, InCounts = new[] { 2 }, Out = Item.Plate, Seconds = 3f } },
            });
            Add(new BuildingDef
            {
                Type = BuildingType.SteamEngine, Name = "Steam engine", Size = 2, Key = '1', Tier = 3, Hp = EngineHp, BurnsFuel = true,
                Cost = new[] { (Item.Brick, 20), (Item.Plate, 16), (Item.Gear, 10) },
            });
            Add(new BuildingDef
            {
                Type = BuildingType.Furnace, Name = "Steel furnace", Size = 2, Key = '2', Tier = 3, Hp = 6, NeedsPower = true,
                Cost = new[] { (Item.Brick, 20), (Item.Plate, 10) },
                Recipes = new[] { new Recipe { Name = "Steel", InItems = new[] { Item.Ore, Item.Coal }, InCounts = new[] { 2, 1 }, Out = Item.Steel, Seconds = 4f } },
            });
            Add(new BuildingDef { Type = BuildingType.Rail, Name = "Rail", Size = 1, Key = '1', Tier = 4, Cost = new[] { (Item.Steel, 1), (Item.Wood, 1) } });
            Add(new BuildingDef { Type = BuildingType.Station, Name = "Station", Size = 2, Key = '2', Tier = 4, Cost = new[] { (Item.Plank, 12), (Item.Brick, 10), (Item.Steel, 6) } });
            Add(new BuildingDef
            {
                Type = BuildingType.Dynamo, Name = "Dynamo", Size = 2, Key = '1', Tier = 5, Hp = DynamoHp, Kw = DynamoKw,
                Cost = new[] { (Item.Plate, 20), (Item.Gear, 10), (Item.Steel, 10) },
            });
            Add(new BuildingDef { Type = BuildingType.Pole, Name = "Power pole", Size = 1, Key = '2', Tier = 5, Cost = new[] { (Item.Plank, 2), (Item.Steel, 1) } });
            Add(new BuildingDef
            {
                Type = BuildingType.WireMill, Name = "Wire mill", Size = 2, Key = '3', Tier = 5, Kw = WireMillKw, NeedsElectric = true,
                Cost = new[] { (Item.Plate, 10), (Item.Brick, 10), (Item.Steel, 6) },
                Recipes = new[] { new Recipe { Name = "Steel wire", InItems = new[] { Item.Steel }, InCounts = new[] { 1 }, Out = Item.Wire, OutCount = 2, Seconds = 2.5f } },
            });
            Add(new BuildingDef { Type = BuildingType.Hopper, Name = "Hopper", Size = 2, Cost = new[] { (Item.Wood, 10), (Item.Stone, 6) }, Key = '8' });
        }

        void Add(BuildingDef d) => Defs[d.Type] = d;
    }
}
