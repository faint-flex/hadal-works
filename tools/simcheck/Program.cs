using System;
using Kilnwood.Sim;

/// <summary>Systems Designer's check: how fast the scripted demo factory reaches the Tier 2 goal.</summary>
public static class Program
{
    public static int Main(string[] args)
    {
        if (args.Length > 0 && args[0] == "pit") return Pit(args.Length > 1 && args[1] == "map");
        if (args.Length > 0 && args[0] == "jitter") return Jitter(args.Length > 1 ? int.Parse(args[1]) : 8);
        var b = new Balance();
        var w = MapGen.NewTier1(b);
        var demo = new DemoFactory();
        float nextReport = 0f, hungrySeconds = 0f;
        while (w.Time < 3600f && !w.GoalReached)
        {
            demo.BuildWhatYouCan(w);
            w.Tick(1f);
            if (w.Hungry) hungrySeconds += 1f;
            if (w.Time >= nextReport)
            {
                Console.WriteLine($"{w.Time,6:0}s  built {demo.Placed}/{demo.Total}  wood {w.Storage[Item.Wood],4} stone {w.Storage[Item.Stone],3} iron {w.Storage[Item.Iron],3}  gear {w.Delivered[Item.Gear],3}/{b.GoalGear} brick {w.Delivered[Item.Brick],3}/{b.GoalBrick}  people {w.WorkersUsed}/{w.Population} fish {w.Storage[Item.Fish],3}{(w.Hungry ? " HUNGRY" : "")}");
                nextReport += 60f;
            }
        }
        Console.WriteLine(w.GoalReached ? $"Goal reached at {w.GoalReachedAt:0}s ({w.GoalReachedAt / 60f:0.0} min)" : "Goal NOT reached in an hour");
        Console.WriteLine($"Hungry for {hungrySeconds:0}s in total");
        foreach (var bd in w.Buildings.Values)
            if (bd.Type != BuildingType.Belt && bd.Type != BuildingType.Lodge)
                Console.WriteLine($"  {bd.Type,-10} at {bd.X},{bd.Y}: {bd.Status}  fuel {bd.FuelWood}W {bd.FuelCharcoal}C");
        return 0;
    }

    /// <summary>The autopilot run as the game plays it: uneven frame times (seeded), iron gift at 200 s.
    /// Prints each seed's goal times, so a run that stalls shows up here before a PC build.</summary>
    static float _trace;
    static void Trace(World w)
    {
        int dig = 0; foreach (var m in w.DigMark) if (m) dig++;
        Console.Write($"{w.Time,5:0} T{w.Tier} iron {w.Storage[Item.Iron]} ex {w.CountVehicles(VehicleType.Excavator)} dz {w.CountVehicles(VehicleType.Dozer)} steel {w.Delivered[Item.Steel]} digMarks {dig} |");
        foreach (var bd in w.Buildings.Values) if (bd.Type == BuildingType.Furnace || (bd.Type == BuildingType.Hopper && bd.X == 39)) Console.Write($" {bd.Type}: {bd.Status} [{string.Join(",", System.Linq.Enumerable.Select(bd.Input, kv => kv.Key + "=" + kv.Value))}]");
        foreach (var v in w.Vehicles) Console.Write($" | {v.Type} '{v.Status}' {v.Load}x{v.LoadCount} b{v.BucketCount} d{v.DigTile} @{v.TileX},{v.TileY} tank{v.Tank:0} st{v.State}");
        Console.WriteLine();
    }

    /// <summary>Two stopped pit vehicles on the same tile, or two excavators side by side.</summary>
    static bool Crowded(World w)
    {
        var vs = w.Vehicles;
        for (int a = 0; a < vs.Count; a++)
            for (int c = a + 1; c < vs.Count; c++)
            {
                var p = vs[a]; var q = vs[c];
                if (p.Type == VehicleType.Train || q.Type == VehicleType.Train) continue;
                if (p.State == VehicleState.Driving || q.State == VehicleState.Driving) continue;
                if (p.State == VehicleState.Refuelling || q.State == VehicleState.Refuelling || p.State == VehicleState.Waiting || q.State == VehicleState.Waiting) continue;
                int dx = Math.Abs(p.TileX - q.TileX), dy = Math.Abs(p.TileY - q.TileY);
                if (dx == 0 && dy == 0) return true;
                if (p.Type == VehicleType.Excavator && q.Type == VehicleType.Excavator && dx <= 1 && dy <= 1) return true;
            }
        return false;
    }

    static bool StoppedOnBelt(World w)
    {
        foreach (var v in w.Vehicles)
        {
            if (v.Type != VehicleType.Truck || v.State == VehicleState.Driving) continue;
            var b = w.At(v.TileX, v.TileY);
            if (b != null && (b.Type == BuildingType.Belt || b.Type == BuildingType.Splitter)) { if (Environment.GetEnvironmentVariable("BELTDBG") != null) Console.WriteLine($"  t={w.Time:0} truck at {v.TileX},{v.TileY} {v.State} {v.Status} drop={(w.Buildings.Count > v.DropBuilding ? w.Buildings[v.DropBuilding].Type + "@" + w.Buildings[v.DropBuilding].X + "," + w.Buildings[v.DropBuilding].Y : "?")}"); return true; }
        }
        return false;
    }

    static string MapChar(BuildingType t)
    {
        switch (t)
        {
            case BuildingType.Belt: return "=";
            case BuildingType.Rail: return "#";
            case BuildingType.Shaft: return "-";
            case BuildingType.Pole: return "|";
            case BuildingType.Lodge: return "@";
            case BuildingType.MachineShop: return "Q";
            case BuildingType.WireMill: return "E";
            case BuildingType.Dynamo: return "Y";
            case BuildingType.SteamEngine: return "X";
            case BuildingType.Furnace: return "F";
            case BuildingType.ElectricMine: return "Z";
            case BuildingType.HydroDam: return "D";
            case BuildingType.Glassworks: return "g";
            case BuildingType.RadioWorks: return "r";
            case BuildingType.DroneWorks: return "n";
            case BuildingType.DronePort: return "p";
            default: return t.ToString().Substring(0, 1);
        }
    }

    static bool _t9seen;
    static int Jitter(int seeds)
    {
        int fails = 0;
        for (int seed = 1; seed <= seeds; seed++)
        {
            _t9seen = false;
            var rng = new Random(seed);
            var b = new Balance();
            var w = MapGen.NewTier1(b);
            var demo = new DemoFactory(false, true, tier2: true, tier3: true, tier4: true, tier5: true, tier6: true, tier7: true, tier8: true, tier9: true, tier10: true);
            bool gift = false, gift3 = false;
            int samples = 0, crowded = 0, onBelt = 0; float nextCrowd = 0f;
            while (w.Time < 10500f && w.Tier < 11)
            {
                demo.BuildWhatYouCan(w);
                demo.PitActions(w);
                demo.RailActions(w);
                if (!gift && w.Time >= 200f) { w.Storage[Item.Iron] += 60; gift = true; }
                if (!gift3 && w.Tier >= 3) { w.Storage[Item.Iron] += 80; gift3 = true; }
                w.Tick((float)(0.02 + rng.NextDouble() * 0.5));
                if (w.Tier == 9 && !_t9seen && w.Time > w.Tier8GoalAt + 20f) { _t9seen = true; Console.WriteLine($"seed {seed}: 20 s into Tier 9: waiting {demo.Waiting(w)}; lodge steel {w.Storage[Item.Steel]} wire {w.Storage[Item.Wire]} plate {w.Storage[Item.Plate]} plank {w.Storage[Item.Plank]} brick {w.Storage[Item.Brick]} motor {w.Storage[Item.Motor]}"); }
                if (w.Time >= nextCrowd) { nextCrowd = w.Time + 5f; samples++; if (Crowded(w)) crowded++; if (StoppedOnBelt(w)) onBelt++; }
                if (seeds == 1 && w.Tier >= 3 && w.Time >= _trace) { _trace = w.Time + 60f; Trace(w); }
            }
            if (Environment.GetEnvironmentVariable("BELTDBG") != null)
                for (int y = w.H - 1; y >= 0; y--) { var line = y.ToString("00") + " "; for (int x = 0; x < w.W; x++) { var bb = w.AtOrShaft(x, y); var gt = w.Ground[y * w.W + x]; line += bb != null ? MapChar(bb.Type) : gt == Terrain.Water ? "~" : gt == Terrain.IronOre ? "o" : gt == Terrain.Stone ? "s" : w.CoalSeam[y * w.W + x] ? "k" : w.Units[y * w.W + x] < 0 ? "_" : w.Logs[y * w.W + x] > 0 ? "t" : "."; } Console.WriteLine(line); }
            if (w.Tier < 11) { fails++; Console.WriteLine($"seed {seed}: demo stuck on {demo.Waiting(w)}"); }
            Console.WriteLine($"seed {seed}: pit vehicles crowded (two parked on one tile / excavators touching) in {crowded} of {samples} samples, truck stopped on a belt in {onBelt}");
            Console.Write($"seed {seed}: T1 {w.GoalReachedAt:0}  T2 {w.Tier2GoalAt:0}  T3 {w.Tier3GoalAt:0}  T4 {w.Tier4GoalAt:0}  T5 {w.Tier5GoalAt:0} wire {w.Delivered[Item.Wire]}  T6 {w.Tier6GoalAt:0} motor {w.Delivered[Item.Motor]}  T7 {w.Tier7GoalAt:0} tractor {w.Delivered[Item.Tractor]}  T8 {w.Tier8GoalAt:0} plane {w.Delivered[Item.Plane]}  T9 {w.Tier9GoalAt:0} radio {w.Delivered[Item.Radio]}  T10 {w.Tier10GoalAt:0} drone {w.Delivered[Item.Drone]} flown {w.DroneFlown} dozer trees {w.TreesCleared} left {w.CountLevelMarks()} rail {w.RailHauled}  steel {w.Delivered[Item.Steel]} coal {w.Delivered[Item.Coal]} ore {w.Delivered[Item.Ore]} |");
            foreach (var v in w.Vehicles) Console.Write($" {v.Type} '{v.Status}';");
            Console.WriteLine();
            foreach (var bd in w.Buildings.Values)
                if (w.Tier < 4 && (bd.Type == BuildingType.Furnace || bd.Type == BuildingType.Hopper || bd.Type == BuildingType.Smelter || bd.Type == BuildingType.TripHammer))
                    Console.WriteLine($"    {bd.Type} {bd.X},{bd.Y} f{bd.Facing}: {bd.Status}  in [{string.Join(",", System.Linq.Enumerable.Select(bd.Input, kv => kv.Key + "=" + kv.Value))}]" + (bd.Type == BuildingType.Hopper ? $" wantsOre {w.HopperWants(bd, Item.Ore)} wantsCoal {w.HopperWants(bd, Item.Coal)}" : ""));
        }
        Console.WriteLine(fails == 0 ? "all seeds reached Tier 11 (drone goal)" : fails + " seeds did NOT reach Tier 11");
        return fails == 0 ? 0 : 1;
    }

    /// <summary>The screenshot run's open pit: demo factory + pit phase, with the autopilot's iron gift at 300 s.</summary>
    static int Pit(bool map)
    {
        var b = new Balance();
        var w = MapGen.NewTier1(b);
        var demo = new DemoFactory(false, true, tier2: true, tier3: true, tier4: true, tier5: true, tier6: true, tier7: true, tier8: true, tier9: true, tier10: true);
        bool gift3 = false;
        bool gift = false;
        float next = 0f;
        while (w.Time < 10500f && w.Tier < 11)
        {
            demo.BuildWhatYouCan(w);
            demo.PitActions(w);
            demo.RailActions(w);
            if (!gift && w.Time >= 300f) { w.Storage[Item.Iron] += 60; gift = true; }
            if (!gift3 && w.Tier >= 3) { w.Storage[Item.Iron] += 80; gift3 = true; }
            w.Tick(1f);
            if (w.Time >= next)
            {
                int dug = 0; foreach (var u in w.Units) if (u < 0) dug++;
                Console.Write($"{w.Time,5:0}s built {demo.Placed}/{demo.Total} ore@lodge {w.Delivered[Item.Ore],4} dugTiles {dug,3} gear {w.Delivered[Item.Gear],3} brick {w.Delivered[Item.Brick],3} T{w.Tier} plank {w.Delivered[Item.Plank],3} plate {w.Delivered[Item.Plate],3} steel {w.Delivered[Item.Steel],3} coal {w.Delivered[Item.Coal],3} wire {w.Delivered[Item.Wire],3} motor {w.Delivered[Item.Motor],3} tractor {w.Delivered[Item.Tractor],3} plane {w.Delivered[Item.Plane],3} |");
                foreach (var bd in w.Buildings.Values) if (w.Tier >= 5 && (bd.Type == BuildingType.WireMill || bd.Type == BuildingType.MachineShop)) Console.Write($" [{bd.Type} {bd.X},{bd.Y} {bd.Status} in {string.Join(",", System.Linq.Enumerable.Select(bd.Input, kv => kv.Key + "=" + kv.Value))}]");
                foreach (var v in w.Vehicles) Console.Write($" {v.Type} {v.State} '{v.Status}' tank {v.Tank:0};");
                Console.WriteLine();
                next += 60f;
            }
        }
        Console.WriteLine(w.GoalReached ? $"Goal at {w.GoalReachedAt:0}s" : "no goal");
        Console.WriteLine(w.Tier >= 3 ? $"Tier 2 goal at {w.Tier2GoalAt:0}s" : "no tier 2 goal");
        Console.WriteLine(w.Tier >= 4 ? $"Tier 3 goal at {w.Tier3GoalAt:0}s" : "no tier 3 goal");
        Console.WriteLine(w.Tier >= 5 ? $"Tier 4 goal at {w.Tier4GoalAt:0}s" : $"no tier 4 goal (hauled {w.RailHauled})");
        Console.WriteLine(w.Tier >= 10 ? $"Tier 9 goal at {w.Tier9GoalAt:0}s, " : "NO tier 9 goal, "); Console.WriteLine(w.Tier >= 9 ? $"Tier 8 goal at {w.Tier8GoalAt:0}s, Tier 7 at {w.Tier7GoalAt:0}s, Tier 6 at {w.Tier6GoalAt:0}s" : w.Tier >= 8 ? $"Tier 7 goal at {w.Tier7GoalAt:0}s, NO tier 8 goal (planes {w.Delivered[Item.Plane]}, built {demo.Placed}/{demo.Total})" : w.Tier >= 7 ? $"Tier 6 goal at {w.Tier6GoalAt:0}s, NO tier 7 goal (tractors {w.Delivered[Item.Tractor]}, built {demo.Placed}/{demo.Total})" : $"no tier 6 goal (motor {w.Delivered[Item.Motor]}, built {demo.Placed}/{demo.Total})");
        Console.WriteLine(w.Tier >= 6 ? $"Tier 5 goal at {w.Tier5GoalAt:0}s" : $"no tier 5 goal (wire {w.Delivered[Item.Wire]}, built {demo.Placed}/{demo.Total})");
        foreach (var bd in w.Buildings.Values)
            if (bd.Type == BuildingType.Waterwheel || bd.Type == BuildingType.Sawmill || bd.Type == BuildingType.TripHammer || bd.Type == BuildingType.SteamEngine || bd.Type == BuildingType.Furnace || bd.Type == BuildingType.Hopper || bd.Type == BuildingType.Dynamo || bd.Type == BuildingType.Pole || bd.Type == BuildingType.WireMill || bd.Type == BuildingType.MachineShop || bd.Type == BuildingType.ElectricMine || bd.Type == BuildingType.Smelter || bd.Type == BuildingType.Mine || bd.Type == BuildingType.Kiln || bd.Type == BuildingType.Turbine || bd.Type == BuildingType.ArcFurnace || bd.Type == BuildingType.Assembly || bd.Type == BuildingType.RollingMill || bd.Type == BuildingType.Hangar || bd.Type == BuildingType.Splitter)
                Console.WriteLine($"  {bd.Type} at {bd.X},{bd.Y}: {bd.Status} power {bd.Power:0.00} elec {bd.Elec:0.00}");
        foreach (var gb in w.Buildings.Values) if (gb.Type == BuildingType.Garage) Console.WriteLine("  CanBuy excavator: " + (w.CanBuy(gb, VehicleType.Excavator) ?? "ok") + " iron " + w.Storage[Item.Iron]);
        if (map) PrintMap(w);
        return 0;
    }

    /// <summary>ASCII map (north at the top): ground . f o s ~, buildings by first letter, belts &gt;, dug tiles _.</summary>
    static void PrintMap(World w)
    {
        for (int y = w.H - 1; y >= 0; y--)
        {
            var sb = new System.Text.StringBuilder($"{y,3} ");
            for (int x = 0; x < w.W; x++)
            {
                var bld = w.At(x, y);
                int t = y * w.W + x;
                char c = bld != null ? (bld.Type == BuildingType.Belt ? '=' : char.ToUpperInvariant(bld.Type.ToString()[0]))
                    : w.Units[t] < 0 ? '_'
                    : w.Ground[t] switch { Terrain.Forest => 'f', Terrain.IronOre => 'o', Terrain.Stone => 's', Terrain.Water => '~', _ => '.' };
                sb.Append(c);
            }
            Console.WriteLine(sb.ToString());
        }
        var head = new System.Text.StringBuilder("    ");
        for (int x = 0; x < w.W; x++) head.Append(x % 10 == 0 ? (char)('0' + x / 10 % 10) : ' ');
        Console.WriteLine(head.ToString());
    }
}
