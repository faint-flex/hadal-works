using System.Collections.Generic;

namespace HadalWorks.Sim
{
    public enum Terrain { Sand, Ferrite, Silica, Kelp, Vent }

    public enum Item { None, Ferrite, Silica, Kelp, Plate, Glass, Sealant, Hull, Viewport }

    public enum BuildingType { Tube, Extractor, Processor, Fabricator, VentTap, Hub }

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
        public int CostPlate;
        public int CostGlass;
        public float Power;      // + supplies, - would be odd; demand is positive here
        public float Supply;
        public Recipe[] Recipes = new Recipe[0];
        public char Key;
    }

    public static class Items
    {
        public static string Name(Item i)
        {
            switch (i)
            {
                case Item.Ferrite: return "Ferrite nodules";
                case Item.Silica: return "Silica sand";
                case Item.Kelp: return "Glowkelp";
                case Item.Plate: return "Ferrite plate";
                case Item.Glass: return "Glass";
                case Item.Sealant: return "Sealant";
                case Item.Hull: return "Hull panel";
                case Item.Viewport: return "Viewport";
                default: return "-";
            }
        }

        /// <summary>One-letter badge shown on every item (colour is never the only cue).</summary>
        public static string Letter(Item i)
        {
            switch (i)
            {
                case Item.Ferrite: return "F";
                case Item.Silica: return "S";
                case Item.Kelp: return "K";
                case Item.Plate: return "P";
                case Item.Glass: return "G";
                case Item.Sealant: return "L";
                case Item.Hull: return "H";
                case Item.Viewport: return "V";
                default: return "";
            }
        }

        public static Item FromTerrain(Terrain t)
        {
            switch (t)
            {
                case Terrain.Ferrite: return Item.Ferrite;
                case Terrain.Silica: return Item.Silica;
                case Terrain.Kelp: return Item.Kelp;
                default: return Item.None;
            }
        }
    }

    /// <summary>Every tunable number for Milestone 1. Mirrors /mnt/project-files/hadal-works/design-m1.md.</summary>
    public class Balance
    {
        public int MapSize = 64;
        public float TubeSpeed = 2f;             // tiles per second
        public float ExtractSecondsPerTile = 2f; // one item per covered resource tile every 2 s
        public float ExtractMaxPerSecond = 2f;
        public int OutputCap = 4;
        public float HubSupply = 6f;
        public int StartPlates = 80;
        public int StartGlass = 12;
        public int GoalHull = 120;
        public int GoalViewport = 60;

        public readonly Dictionary<BuildingType, BuildingDef> Defs = new Dictionary<BuildingType, BuildingDef>();

        public Balance()
        {
            Add(new BuildingDef { Type = BuildingType.Tube, Name = "Pressure tube", Size = 1, CostPlate = 1, Key = '1' });
            Add(new BuildingDef { Type = BuildingType.Extractor, Name = "Extractor", Size = 2, CostPlate = 6, Power = 3, Key = '2' });
            Add(new BuildingDef
            {
                Type = BuildingType.Processor, Name = "Processor", Size = 2, CostPlate = 8, Power = 4, Key = '3',
                Recipes = new[]
                {
                    new Recipe { Name = "Ferrite plate", InItems = new[] { Item.Ferrite }, InCounts = new[] { 2 }, Out = Item.Plate, Seconds = 3f },
                    new Recipe { Name = "Glass", InItems = new[] { Item.Silica }, InCounts = new[] { 2 }, Out = Item.Glass, Seconds = 3f },
                    new Recipe { Name = "Sealant", InItems = new[] { Item.Kelp }, InCounts = new[] { 2 }, Out = Item.Sealant, Seconds = 4f },
                }
            });
            Add(new BuildingDef
            {
                Type = BuildingType.Fabricator, Name = "Fabricator", Size = 2, CostPlate = 10, CostGlass = 4, Power = 6, Key = '4',
                Recipes = new[]
                {
                    new Recipe { Name = "Hull panel", InItems = new[] { Item.Plate, Item.Sealant }, InCounts = new[] { 2, 1 }, Out = Item.Hull, Seconds = 6f },
                    new Recipe { Name = "Viewport", InItems = new[] { Item.Glass }, InCounts = new[] { 3 }, Out = Item.Viewport, Seconds = 6f },
                }
            });
            Add(new BuildingDef { Type = BuildingType.VentTap, Name = "Vent tap", Size = 1, CostPlate = 6, Supply = 12, Key = '5' });
            Add(new BuildingDef { Type = BuildingType.Hub, Name = "Hub", Size = 4, Supply = 6 });
        }

        void Add(BuildingDef d) => Defs[d.Type] = d;
    }
}
