using System;
using HadalWorks.Sim;

/// <summary>Systems Designer's check: how fast the scripted demo factory reaches the Tier 2 goal.</summary>
public static class Program
{
    public static int Main()
    {
        var b = new Balance();
        var w = MapGen.NewTier1(b);
        var demo = new DemoFactory();
        float nextReport = 0f;
        while (w.Time < 3600f && !w.GoalReached)
        {
            demo.BuildWhatYouCan(w);
            w.Tick(1f);
            if (w.Time >= nextReport)
            {
                Console.WriteLine($"{w.Time,6:0}s  built {demo.Placed}/{demo.Total}  plates {w.Storage[Item.Plate],4} glass {w.Storage[Item.Glass],3}  hull {w.Delivered[Item.Hull],3}/{b.GoalHull} viewport {w.Delivered[Item.Viewport],3}/{b.GoalViewport}  power {w.PowerSupply}/{w.PowerDemand}");
                nextReport += 60f;
            }
        }
        Console.WriteLine(w.GoalReached ? $"Goal reached at {w.GoalReachedAt:0}s ({w.GoalReachedAt / 60f:0.0} min)" : "Goal NOT reached in an hour");
        foreach (var bd in w.Buildings.Values)
            if (bd.Type != BuildingType.Tube && bd.Type != BuildingType.Hub)
                Console.WriteLine($"  {bd.Type,-10} at {bd.X},{bd.Y}: {bd.Status}");
        return 0;
    }
}
