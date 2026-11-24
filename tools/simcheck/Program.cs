using System;
using Kilnwood.Sim;

/// <summary>Systems Designer's check: how fast the scripted demo factory reaches the Tier 2 goal.</summary>
public static class Program
{
    public static int Main(string[] args)
    {
        if (args.Length > 0 && args[0] == "pit") return Pit(args.Length > 1 && args[1] == "map");
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

    /// <summary>The screenshot run's open pit: demo factory + pit phase, with the autopilot's iron gift at 300 s.</summary>
    static int Pit(bool map)
    {
        var b = new Balance();
        var w = MapGen.NewTier1(b);
        var demo = new DemoFactory(false, true, tier2: true);
        bool gift = false;
        float next = 0f;
        while (w.Time < 2400f && w.Tier < 3)
        {
            demo.BuildWhatYouCan(w);
            demo.PitActions(w);
            if (!gift && w.Time >= 300f) { w.Storage[Item.Iron] += 60; gift = true; }
            w.Tick(1f);
            if (w.Time >= next)
            {
                int dug = 0; foreach (var u in w.Units) if (u < 0) dug++;
                Console.Write($"{w.Time,5:0}s built {demo.Placed}/{demo.Total} ore@lodge {w.Delivered[Item.Ore],4} dugTiles {dug,3} gear {w.Delivered[Item.Gear],3} brick {w.Delivered[Item.Brick],3} T{w.Tier} plank {w.Delivered[Item.Plank],3} plate {w.Delivered[Item.Plate],3} |");
                foreach (var v in w.Vehicles) Console.Write($" {v.Type} {v.State} '{v.Status}' tank {v.Tank:0};");
                Console.WriteLine();
                next += 60f;
            }
        }
        Console.WriteLine(w.GoalReached ? $"Goal at {w.GoalReachedAt:0}s" : "no goal");
        Console.WriteLine(w.Tier >= 3 ? $"Tier 2 goal at {w.Tier2GoalAt:0}s" : "no tier 2 goal");
        foreach (var bd in w.Buildings.Values)
            if (bd.Type == BuildingType.Waterwheel || bd.Type == BuildingType.Sawmill || bd.Type == BuildingType.TripHammer)
                Console.WriteLine($"  {bd.Type} at {bd.X},{bd.Y}: {bd.Status} power {bd.Power:0.00}");
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
