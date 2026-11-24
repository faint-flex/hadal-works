using System.Collections.Generic;

namespace Kilnwood.Sim
{
    /// <summary>
    /// A known-good Tier 1 factory, built step by step as the lodge can afford. Used by the tests (proves the goal
    /// is reachable), the balance check, and the build's screenshot autopilot. Not shown to players.
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

        public DemoFactory(bool people = false)
        {
            const int N = 0, E = 1, S = 2, W = 3;
            const int Charcoal = 0, Brick = 1;
            // Phase 1: wood into the lodge (pays for everything else).
            Add(BuildingType.Woodcutter, 14, 30, E);
            for (int x = 16; x <= 29; x++) Add(BuildingType.Belt, x, 30, E);

            // Phase 1b: food and homes. A fisher on the river, its fish side-loaded onto the wood line; three cabins.
            if (people)
            {
                Add(BuildingType.Fisher, 5, 33, E);
                for (int x = 7; x <= 19; x++) Add(BuildingType.Belt, x, 33, E);
                for (int y = 33; y >= 31; y--) Add(BuildingType.Belt, 20, y, S);
                Add(BuildingType.Cabin, 24, 26, N);
                Add(BuildingType.Cabin, 26, 26, N);
                Add(BuildingType.Cabin, 28, 26, N);
            }

            // Phase 2: bricks. Woodcutter fuels a stone mine and a brick kiln.
            Add(BuildingType.Woodcutter, 30, 53, S);
            for (int y = 52; y >= 48; y--) Add(BuildingType.Belt, 30, y, S);
            Add(BuildingType.Mine, 30, 46, S);
            Add(BuildingType.Kiln, 30, 44, S, Brick);
            Add(BuildingType.Belt, 31, 52, E);
            for (int y = 52; y >= 46; y--) Add(BuildingType.Belt, 32, y, S);
            Add(BuildingType.Belt, 32, 45, W);
            for (int y = 43; y >= 34; y--) Add(BuildingType.Belt, 30, y, S);

            // Phase 3: iron. Charcoal kiln -> smelter <- ore mine (wood-fired).
            Add(BuildingType.Woodcutter, 44, 22, N);
            Add(BuildingType.Kiln, 44, 24, N, Charcoal);
            Add(BuildingType.Belt, 44, 26, W);
            for (int y = 26; y <= 31; y++) Add(BuildingType.Belt, 43, y, N);
            Add(BuildingType.Smelter, 42, 32, N);
            Add(BuildingType.Mine, 44, 32, W);
            Add(BuildingType.Woodcutter, 44, 40, S);
            for (int y = 39; y >= 34; y--) Add(BuildingType.Belt, 45, y, S);

            // Phase 4: gears. Workshop on top of the smelter, wood fuel from the same woodcutter, gears to the lodge.
            for (int y = 39; y >= 36; y--) Add(BuildingType.Belt, 44, y, S);
            Add(BuildingType.Belt, 44, 35, W);
            Add(BuildingType.Workshop, 42, 34, W);
            for (int x = 41; x >= 34; x--) Add(BuildingType.Belt, x, 34, W);
            Add(BuildingType.Belt, 33, 34, S);
        }

        void Add(BuildingType t, int x, int y, int facing, int recipe = 0) =>
            _steps.Add(new Step { Type = t, X = x, Y = y, Facing = facing, Recipe = recipe });

        /// <summary>Places as many next steps as the lodge can afford. Returns how many were placed.</summary>
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
            var demo = new DemoFactory(w.B.PeopleEnabled);
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
