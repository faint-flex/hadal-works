using System.Collections.Generic;

namespace HadalWorks.Sim
{
    /// <summary>
    /// A known-good Tier 1 factory, built step by step as plates allow. Used by the tests (proves the goal is
    /// reachable), the balance check, and the build's screenshot autopilot. Not shown to players.
    /// </summary>
    public class DemoFactory
    {
        struct Step
        {
            public BuildingType Type;
            public int X, Y, Facing, Recipe;
        }

        readonly List<Step> _steps = new List<Step>();
        int _next;

        public bool Done => _next >= _steps.Count;
        public int Total => _steps.Count;
        public int Placed => _next;

        public DemoFactory()
        {
            const int N = 0, E = 1, S = 2, W = 3;
            // Phase 1: ferrite -> plates -> hub, plus power.
            Add(BuildingType.VentTap, 24, 26, N);
            Add(BuildingType.Extractor, 14, 30, E);
            Add(BuildingType.Processor, 16, 30, E, 0);
            Line(18, 29, 30, E);
            // Phase 2: hull panels (plates + sealant).
            Add(BuildingType.VentTap, 40, 26, N);
            Add(BuildingType.Fabricator, 30, 36, S, 0);
            Add(BuildingType.Tube, 30, 35, S);
            Add(BuildingType.Tube, 30, 34, S);
            Add(BuildingType.Extractor, 14, 36, E);
            Add(BuildingType.Processor, 16, 36, E, 0);
            Line(18, 29, 36, E);
            Add(BuildingType.Extractor, 30, 47, S);
            Add(BuildingType.Processor, 30, 45, S, 2);
            for (int y = 44; y >= 38; y--) Add(BuildingType.Tube, 30, y, S);
            // Phase 3: viewports (glass).
            Add(BuildingType.VentTap, 33, 22, N);
            Add(BuildingType.Fabricator, 32, 36, S, 1);
            Add(BuildingType.Tube, 32, 35, S);
            Add(BuildingType.Tube, 32, 34, S);
            Add(BuildingType.Extractor, 48, 36, W);
            Add(BuildingType.Processor, 46, 36, W, 1);
            for (int x = 45; x >= 34; x--) Add(BuildingType.Tube, x, 36, W);
        }

        void Add(BuildingType t, int x, int y, int facing, int recipe = 0) =>
            _steps.Add(new Step { Type = t, X = x, Y = y, Facing = facing, Recipe = recipe });

        void Line(int x0, int x1, int y, int facing)
        {
            for (int x = x0; x <= x1; x++) Add(BuildingType.Tube, x, y, facing);
        }

        /// <summary>Places as many next steps as the hub can afford. Returns how many were placed.</summary>
        public int BuildWhatYouCan(World w)
        {
            int placed = 0;
            while (!Done)
            {
                var s = _steps[_next];
                var existing = w.At(s.X, s.Y);
                if (existing != null && existing.Type == s.Type && existing.X == s.X && existing.Y == s.Y) { _next++; continue; }
                if (w.Place(s.Type, s.X, s.Y, s.Facing, s.Recipe) == null) break;
                _next++;
                placed++;
            }
            return placed;
        }

        /// <summary>Runs the demo from a fresh map until the goal or the time limit. Returns seconds taken (or -1).</summary>
        public static float RunToGoal(World w, float limitSeconds = 3600f)
        {
            var demo = new DemoFactory();
            while (w.Time < limitSeconds)
            {
                demo.BuildWhatYouCan(w);
                w.Tick(1f);
                if (w.GoalReached) return w.GoalReachedAt;
            }
            return -1f;
        }
    }
}
