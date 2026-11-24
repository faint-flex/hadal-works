using System;
using Kilnwood.Sim;

/// <summary>Systems Designer's check: how fast the scripted demo factory reaches the Tier 2 goal.</summary>
public static class Program
{
    public static int Main()
    {
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
}
