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
            public bool Remove;     // demolish what's there instead of building
            public int MinTier;     // for removals: wait until this tier
        }

        readonly List<Step> _steps = new List<Step>();
        int _next;

        public bool Done => _next >= _steps.Count;
        public int Total => _steps.Count;
        public int Placed => _next;
        /// <summary>The step it is waiting on, and why (for simcheck).</summary>
        public string Waiting(World w)
        {
            if (Done) return "done";
            var s = _steps[_next];
            return s.Type + " at " + s.X + "," + s.Y + (s.Remove ? " (remove)" : ": " + (w.CanPlace(s.Type, s.X, s.Y) ?? "ok"));
        }

        readonly bool _pit;
        bool _marked, _marked3;

        readonly bool _tier3;

        readonly bool _tier4;
        readonly bool _tier5, _tier6, _tier7, _tier8;
        readonly int _railsEnd;
        public const int FieldStationX = 54, FieldStationY = 7, YardStationX = 35, YardStationY = 26, DynamoX = 42, DynamoY = 20, ShopX = 36, ShopY = 32, TurbineX = 48, TurbineY = 20, AssemblyX = 31, AssemblyY = 35, HangarX = 37, HangarY = 39;

        public DemoFactory(bool people = false, bool pit = false, bool tier2 = false, bool tier3 = false, bool tier4 = false, bool tier5 = false, bool tier6 = false, bool tier7 = false, bool tier8 = false)
        {
            _pit = pit;
            _tier3 = tier3;
            _tier4 = tier4;
            _tier5 = tier5;
            _tier6 = tier6;
            _tier7 = tier7;
            _tier8 = tier8;
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

            // Phase 5 (0.5, screenshot run only): an open pit in the ore field. A woodcutter fuels a garage;
            // one excavator and two trucks dig ore and tip it into a hopper beside the lodge.
            if (pit)
            {
                Add(BuildingType.Woodcutter, 54, 40, S);   // north-east woods
                Add(BuildingType.Belt, 54, 39, S);
                Add(BuildingType.Belt, 54, 38, S);
                Add(BuildingType.Garage, 54, 36, N);       // open grass beside the ore field
                Add(BuildingType.Hopper, 34, 31, W);   // tips straight into the lodge beside it
            }

            // Phase 6 (0.6 Tier 2, waits for the unlock): a waterwheel on the west bank drives one long line shaft east
            // across the valley. A sawmill fed straight by a woodcutter sends planks to the lodge; the workshop makes
            // way for a trip hammer that turns the smelter's iron into plates. Shafts run overhead, over belts.
            if (tier2)
            {
                Add(BuildingType.Waterwheel, 5, 18, E);
                for (int x = 7; x <= 15; x++) Add(BuildingType.Shaft, x, 19, N);
                for (int y = 20; y <= 22; y++) Add(BuildingType.Shaft, 15, y, N);
                Add(BuildingType.Woodcutter, 16, 24, S);
                Add(BuildingType.Sawmill, 16, 22, E);
                for (int x = 18; x <= 30; x++) Add(BuildingType.Belt, x, 22, E);
                for (int y = 22; y <= 29; y++) Add(BuildingType.Belt, 31, y, N);
                for (int x = 16; x <= 41; x++) Add(BuildingType.Shaft, x, 19, N);
                for (int y = 20; y <= 35; y++) Add(BuildingType.Shaft, 41, y, N);
                _steps.Add(new Step { Type = BuildingType.Workshop, X = 42, Y = 34, Remove = true, MinTier = 2 });
                Add(BuildingType.TripHammer, 42, 34, W);
            }

            // Phase 7 (0.7 Tier 3 "Steam"): a steam engine fired with wood split off the west wood line joins the
            // shaft network; a hopper of pit ore and coal feeds a steel furnace east of the lodge. The pit phase
            // digs the coal seam (under dirt and stone) and more ore.
            if (tier3)
            {
                _steps.Add(new Step { Type = BuildingType.Belt, X = 25, Y = 30, Remove = true, MinTier = 3 });
                Add(BuildingType.Splitter, 25, 30, E);
                Add(BuildingType.Belt, 25, 29, S);
                Add(BuildingType.Belt, 25, 28, S);
                Add(BuildingType.SteamEngine, 25, 26, N);
                for (int y = 20; y <= 25; y++) Add(BuildingType.Shaft, 25, y, N);
                Add(BuildingType.Hopper, 39, 27, N);
                Add(BuildingType.Furnace, 39, 29, W);
                for (int x = 38; x >= 34; x--) Add(BuildingType.Belt, x, 30, W);
            }

            // Phase 8 (0.8 Tier 4 "Rails"): three mines on the far north-east ore field tip straight into a station;
            // a railway runs west along y = 14 and south down x = 35 to a yard station beside the lodge. Trains bring
            // the ore home and take coal out for the mines.
            if (tier4)
            {
                Add(BuildingType.Station, FieldStationX, FieldStationY, N);
                Add(BuildingType.Mine, 52, 7, E);
                Add(BuildingType.Mine, 56, 7, W);
                Add(BuildingType.Mine, 54, 5, N);
                for (int y = 9; y <= 14; y++) Add(BuildingType.Rail, 55, y, N);
                for (int x = 54; x >= 35; x--) Add(BuildingType.Rail, x, 14, W);
                for (int y = 15; y <= 25; y++) Add(BuildingType.Rail, 35, y, S);
                Add(BuildingType.Station, YardStationX, YardStationY, N);
            }
            _railsEnd = _steps.Count;

            // Phase 9 (0.10 Tier 5 "Electricity"): a dynamo hangs off the shaft column, poles carry the line south-west,
            // and a wire mill drops into the steel line between the furnace and the lodge.
            if (tier5)
            {
                Add(BuildingType.Dynamo, DynamoX, DynamoY, W);
                Add(BuildingType.Pole, 40, 23, N);
                Add(BuildingType.Pole, 37, 28, N);
                _steps.Add(new Step { Type = BuildingType.Belt, X = 37, Y = 30, Remove = true, MinTier = 5 });
                _steps.Add(new Step { Type = BuildingType.Belt, X = 36, Y = 30, Remove = true, MinTier = 5 });
                Add(BuildingType.WireMill, 36, 29, W);
            }

            // Phase 10 (0.12 Tier 6 "Machine Age"): a second dynamo; the wire mill turns north to feed a machine shop
            // that also takes the trip hammer's plates; motors go to the lodge. The brick line's mine goes electric.
            if (tier6)
            {
                Add(BuildingType.Dynamo, 42, 22, W);
                Add(BuildingType.Pole, 38, 31, N);
                _steps.Add(new Step { Type = BuildingType.WireMill, X = 36, Y = 29, Remove = true, MinTier = 6 });
                Add(BuildingType.WireMill, 36, 29, N);
                Add(BuildingType.Belt, 36, 31, N);
                Add(BuildingType.MachineShop, ShopX, ShopY, W);
                Add(BuildingType.Belt, 35, 33, W);
                Add(BuildingType.Belt, 34, 33, W);
                // The motor belt boxes in the lodge hopper, so trucks would tip from the belts: move it to the lodge's south side.
                _steps.Add(new Step { Type = BuildingType.Hopper, X = 34, Y = 31, Remove = true, MinTier = 6 });
                Add(BuildingType.Hopper, 32, 28, N);
                _steps.Add(new Step { Type = BuildingType.Belt, X = 36, Y = 34, Remove = true, MinTier = 6 });
                Add(BuildingType.Belt, 36, 34, S);
                Add(BuildingType.Pole, 33, 38, N);
                Add(BuildingType.Pole, 32, 44, N);
                _steps.Add(new Step { Type = BuildingType.Mine, X = 30, Y = 46, Remove = true, MinTier = 6 });
                Add(BuildingType.ElectricMine, 30, 46, S);
            }

            // Phase 11 (0.14 Tier 7 "Assembly Line"): a wood-fired turbine for more electricity, then the assembly plant
            // north of the lodge, fed steel by an electric furnace (ore from a truck hopper) and motors from the machine shop.
            if (tier7)
            {
                Add(BuildingType.Turbine, TurbineX, TurbineY, N);
                Add(BuildingType.Woodcutter, TurbineX, TurbineY + 2, S);   // feeds the turbine straight from the woods
                _steps.Add(new Step { Type = BuildingType.Belt, X = 34, Y = 34, Remove = true, MinTier = 7 });
                _steps.Add(new Step { Type = BuildingType.Belt, X = 33, Y = 34, Remove = true, MinTier = 7 });
                Add(BuildingType.Assembly, AssemblyX, AssemblyY, S);
                Add(BuildingType.Belt, 32, 34, S);
                Add(BuildingType.ArcFurnace, 31, 38, S);
                Add(BuildingType.Hopper, 31, 40, S);
                // Motors: north out of the machine shop, then west into the assembly plant.
                _steps.Add(new Step { Type = BuildingType.Belt, X = 35, Y = 33, Remove = true, MinTier = 7 });
                Add(BuildingType.Belt, 35, 33, N);
                _steps.Add(new Step { Type = BuildingType.Belt, X = 35, Y = 34, Remove = true, MinTier = 7 });
                Add(BuildingType.Belt, 35, 34, N);
                Add(BuildingType.Belt, 35, 35, W);
                Add(BuildingType.Belt, 34, 35, W);
            }

            // Phase 12 (0.15 Tier 8 "Flight"): a rolling mill turns steel from a second electric furnace into plates for a
            // hangar east of the assembly plant; a splitter shares the machine shop's motors between the two. Aeroplanes
            // fly off the hangar's runway to the north. Two more wood-fired turbines carry the extra load.
            if (tier8)
            {
                Add(BuildingType.RollingMill, 39, 37, N);   // first: Tier 8 gates the rest of the phase
                Add(BuildingType.Turbine, TurbineX + 3, TurbineY, N);
                Add(BuildingType.Woodcutter, TurbineX + 3, TurbineY + 2, S);
                Add(BuildingType.Turbine, TurbineX + 6, TurbineY, N);
                Add(BuildingType.Woodcutter, TurbineX + 6, TurbineY + 2, S);
                Add(BuildingType.Pole, 41, 38, N);
                Add(BuildingType.ArcFurnace, 39, 35, N);
                Add(BuildingType.Hopper, 37, 36, E);
                Add(BuildingType.Hangar, HangarX, HangarY, N);
                for (int y = 35; y <= 37; y++) Add(BuildingType.Belt, 36, y, N);
                Add(BuildingType.Belt, 36, 38, E);
                Add(BuildingType.Belt, 37, 38, N);
                _steps.Add(new Step { Type = BuildingType.Belt, X = 35, Y = 35, Remove = true, MinTier = 8 });
                Add(BuildingType.Splitter, 35, 35, N);   // motors: left to the assembly plant, right to the hangar
            }
        }

        /// <summary>Rails phase: buy trains at the yard once the line is finished.</summary>
        public void RailActions(World w)
        {
            if (!_tier4 || _next < _railsEnd) return;
            var yard = w.At(YardStationX, YardStationY);
            if (yard == null || yard.Type != BuildingType.Station) return;
            int want = w.Storage[Item.Steel] >= 40 ? 2 : 1;
            if (w.CountTrains() < want) w.Buy(yard.Id, VehicleType.Train);
        }

        /// <summary>Screenshot run: once the garage stands, buy vehicles as iron allows and mark the pit.</summary>
        float _remarkAt;

        public void PitActions(World w)
        {
            if (!_pit) return;
            var g = w.At(54, 36);
            if (g == null || g.Type != BuildingType.Garage) return;
            if (!_marked) { w.MarkDig(46, 28, 50, 31); _marked = true; }
            if (_tier3 && w.Tier >= 3 && !_marked3)
            {
                _marked3 = true;
                w.MarkDig(MapGen.CoalX0, MapGen.CoalY0, MapGen.CoalX0 + 6, MapGen.CoalY0 + 6);           // open the whole coal seam
                w.MarkDig(47, 33, 50, 36);                                                               // more ore
                w.MarkDump(57, 20, 61, 24);                                                              // the seam's dirt goes here
            }
            if (_tier3 && w.Tier >= 3 && w.Time >= _remarkAt)
            {
                // The first marks run out before the goal: keep the pit going by widening and deepening it.
                _remarkAt = w.Time + 30f;
                int left = 0; foreach (var m in w.DigMark) if (m) left++;
                if (left < 6)
                {
                    w.MarkDig(45, 27, 51, 38);                                                               // ore field, next layer down
                    w.MarkDig(MapGen.CoalX0, MapGen.CoalY0, MapGen.CoalX0 + MapGen.CoalW - 1, MapGen.CoalY0 + MapGen.CoalH - 1);   // the whole seam
                }
                int dumpLeft = 0;
                for (int i = 0; i < w.DumpMark.Length; i++) if (w.DumpMark[i]) dumpLeft++;
                if (dumpLeft < 4) w.MarkDump(56, 14, 62, 25);                                           // more room for the dirt
            }
            int wantEx = _tier3 && w.Tier >= 3 ? 3 : 1, wantTr = _tier3 && w.Tier >= 3 ? 4 : 2;
            if (w.CountVehicles(VehicleType.Excavator) < wantEx) w.Buy(g.Id, VehicleType.Excavator);
            else if (w.CountVehicles(VehicleType.Truck) < wantTr) w.Buy(g.Id, VehicleType.Truck);
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
                var existing = s.Type == BuildingType.Shaft ? w.ShaftAt(s.X, s.Y) : w.At(s.X, s.Y);
                if (s.Remove)
                {
                    if (w.Tier < s.MinTier) break;
                    if (existing != null && existing.Type == s.Type) w.Demolish(existing.Id);
                    _next++;
                    continue;
                }
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
