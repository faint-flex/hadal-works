using System;
using Kilnwood.Sim;

/// <summary>Systems Designer's check: how fast the scripted demo factory reaches the Tier 2 goal.</summary>
public static class Program
{
    public static int Main(string[] args)
    {
        if (args.Length > 0 && args[0] == "pit") return Pit();
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
    static int Pit()
    {
        var b = new Balance();
        var w = MapGen.NewTier1(b);
        var demo = new DemoFactory(false, true);
        bool gift = false;
        float next = 0f;
        while (w.Time < 1200f)
        {
            demo.BuildWhatYouCan(w);
            demo.PitActions(w);
            if (!gift && w.Time >= 300f) { w.Storage[Item.Iron] += 60; gift = true; }
            w.Tick(1f);
            if (w.Time >= next)
            {
                int dug = 0; foreach (var u in w.Units) if (u < 0) dug++;
                Console.Write($"{w.Time,5:0}s built {demo.Placed}/{demo.Total} ore@lodge {w.Delivered[Item.Ore],4} dugTiles {dug,3} gear {w.Delivered[Item.Gear],3} brick {w.Delivered[Item.Brick],3} |");
                foreach (var v in w.Vehicles) Console.Write($" {v.Type} {v.State} '{v.Status}' tank {v.Tank:0};");
                Console.WriteLine();
                next += 60f;
            }
        }
        Console.WriteLine(w.GoalReached ? $"Goal at {w.GoalReachedAt:0}s" : "no goal");
        return 0;
    }
}
