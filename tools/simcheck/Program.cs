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
        Console.Write($"{w.Time,5:0} steel {w.Delivered[Item.Steel]} digMarks {dig} |");
        foreach (var bd in w.Buildings.Values) if (bd.Type == BuildingType.Furnace || (bd.Type == BuildingType.Hopper && bd.X == 39)) Console.Write($" {bd.Type}: {bd.Status} [{string.Join(",", System.Linq.Enumerable.Select(bd.Input, kv => kv.Key + "=" + kv.Value))}]");
        foreach (var v in w.Vehicles) Console.Write($" | {v.Type} '{v.Status}' {v.Load}x{v.LoadCount}");
        Console.WriteLine();
    }

    static int Jitter(int seeds)
    {
        int fails = 0;
        for (int seed = 1; seed <= seeds; seed++)
        {
            var rng = new Random(seed);
            var b = new Balance();
            var w = MapGen.NewTier1(b);
            var demo = new DemoFactory(false, true, tier2: true, tier3: true, tier4: true);
            bool gift = false, gift3 = false;
            while (w.Time < 4800f && w.Tier < 5)
            {
                demo.BuildWhatYouCan(w);
                demo.PitActions(w);
                demo.RailActions(w);
                if (!gift && w.Time >= 200f) { w.Storage[Item.Iron] += 60; gift = true; }
                if (!gift3 && w.Tier >= 3) { w.Storage[Item.Iron] += 40; gift3 = true; }
                w.Tick((float)(0.02 + rng.NextDouble() * 0.5));
                if (seeds == 1 && w.Tier >= 3 && w.Time >= _trace) { _trace = w.Time + 60f; Trace(w); }
            }
            if (w.Tier < 5) fails++;
            Console.Write($"seed {seed}: T1 {w.GoalReachedAt:0}  T2 {w.Tier2GoalAt:0}  T3 {w.Tier3GoalAt:0}  T4 {w.Tier4GoalAt:0} rail {w.RailHauled}  steel {w.Delivered[Item.Steel]} coal {w.Delivered[Item.Coal]} ore {w.Delivered[Item.Ore]} |");
            foreach (var v in w.Vehicles) Console.Write($" {v.Type} '{v.Status}';");
            Console.WriteLine();
            foreach (var bd in w.Buildings.Values)
                if (w.Tier < 4 && (bd.Type == BuildingType.Furnace || bd.Type == BuildingType.Hopper || bd.Type == BuildingType.Smelter || bd.Type == BuildingType.TripHammer))
                    Console.WriteLine($"    {bd.Type} {bd.X},{bd.Y} f{bd.Facing}: {bd.Status}  in [{string.Join(",", System.Linq.Enumerable.Select(bd.Input, kv => kv.Key + "=" + kv.Value))}]" + (bd.Type == BuildingType.Hopper ? $" wantsOre {w.HopperWants(bd, Item.Ore)} wantsCoal {w.HopperWants(bd, Item.Coal)}" : ""));
        }
        Console.WriteLine(fails == 0 ? "all seeds reached Tier 5" : fails + " seeds did NOT reach Tier 5");
        return fails == 0 ? 0 : 1;
    }

    /// <summary>The screenshot run's open pit: demo factory + pit phase, with the autopilot's iron gift at 300 s.</summary>
    static int Pit(bool map)
    {
        var b = new Balance();
        var w = MapGen.NewTier1(b);
        var demo = new DemoFactory(false, true, tier2: true, tier3: true, tier4: true);
        bool gift3 = false;
        bool gift = false;
        float next = 0f;
        while (w.Time < 4800f && w.Tier < 5)
        {
            demo.BuildWhatYouCan(w);
            demo.PitActions(w);
            demo.RailActions(w);
            if (!gift && w.Time >= 300f) { w.Storage[Item.Iron] += 60; gift = true; }
            if (!gift3 && w.Tier >= 3) { w.Storage[Item.Iron] += 40; gift3 = true; }
            w.Tick(1f);
            if (w.Time >= next)
            {
                int dug = 0; foreach (var u in w.Units) if (u < 0) dug++;
                Console.Write($"{w.Time,5:0}s built {demo.Placed}/{demo.Total} ore@lodge {w.Delivered[Item.Ore],4} dugTiles {dug,3} gear {w.Delivered[Item.Gear],3} brick {w.Delivered[Item.Brick],3} T{w.Tier} plank {w.Delivered[Item.Plank],3} plate {w.Delivered[Item.Plate],3} steel {w.Delivered[Item.Steel],3} coal {w.Delivered[Item.Coal],3} |");
                foreach (var v in w.Vehicles) Console.Write($" {v.Type} {v.State} '{v.Status}' tank {v.Tank:0};");
                Console.WriteLine();
                next += 60f;
            }
        }
        Console.WriteLine(w.GoalReached ? $"Goal at {w.GoalReachedAt:0}s" : "no goal");
        Console.WriteLine(w.Tier >= 3 ? $"Tier 2 goal at {w.Tier2GoalAt:0}s" : "no tier 2 goal");
        Console.WriteLine(w.Tier >= 4 ? $"Tier 3 goal at {w.Tier3GoalAt:0}s" : "no tier 3 goal");
        Console.WriteLine(w.Tier >= 5 ? $"Tier 4 goal at {w.Tier4GoalAt:0}s" : $"no tier 4 goal (hauled {w.RailHauled})");
        foreach (var bd in w.Buildings.Values)
            if (bd.Type == BuildingType.Waterwheel || bd.Type == BuildingType.Sawmill || bd.Type == BuildingType.TripHammer || bd.Type == BuildingType.SteamEngine || bd.Type == BuildingType.Furnace || bd.Type == BuildingType.Hopper)
                Console.WriteLine($"  {bd.Type} at {bd.X},{bd.Y}: {bd.Status} power {bd.Power:0.00}");
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
