using Kilnwood.Sim;
using NUnit.Framework;

namespace Kilnwood.Tests
{
    /// <summary>0.5 "Open Pit": digging, dumping, excavators, trucks, garage fuel, hoppers.</summary>
    public class DigTests
    {
        Balance B;

        [SetUp]
        public void SetUp() { B = new Balance(); }

        World Empty()
        {
            var w = new World(B);
            w.Place(BuildingType.Lodge, 30, 30, 0, 0, free: true);
            w.Storage[Item.Iron] = 200; w.Storage[Item.Wood] = 200; w.Storage[Item.Stone] = 200;
            return w;
        }

        static void Ground(World w, int x, int y, int wd, int ht, Terrain t)
        {
            for (int dy = 0; dy < ht; dy++) for (int dx = 0; dx < wd; dx++) w.SetGround(x + dx, y + dy, t);
        }

        Building Garage(World w, int x, int y, int fuel = 12)
        {
            var g = w.Place(BuildingType.Garage, x, y, 0);
            Assert.IsNotNull(g);
            g.FuelCharcoal = fuel;
            return g;
        }

        [Test]
        public void Layers_are_dirt_then_stone_under_grass_and_ore_all_the_way_down()
        {
            var w = Empty();
            Ground(w, 10, 10, 1, 1, Terrain.IronOre);
            int grass = 20 * w.W + 20, ore = 10 * w.W + 10;
            Assert.AreEqual(Item.Dirt, w.MaterialAt(grass));
            w.Units[grass] = -B.UnitsPerLevel;
            Assert.AreEqual(Item.Stone, w.MaterialAt(grass));
            w.Units[ore] = -B.UnitsPerLevel * 3;
            Assert.AreEqual(Item.Ore, w.MaterialAt(ore));
            w.Units[ore] = -B.UnitsPerLevel * B.MaxDigDepth;
            Assert.AreEqual(Item.None, w.MaterialAt(ore));
            w.Units[grass] = 3;
            Assert.AreEqual(Item.Dirt, w.MaterialAt(grass));
        }

        [Test]
        public void Marks_skip_water_forest_and_buildings()
        {
            var w = Empty();
            Ground(w, 0, 0, 1, 1, Terrain.Water);
            Ground(w, 1, 0, 1, 1, Terrain.Forest);
            w.Place(BuildingType.Belt, 2, 0, 0);
            Assert.AreEqual(1, w.MarkDig(0, 0, 3, 0));      // only tile 3
            Assert.IsTrue(w.DigMark[3]);
            Assert.AreEqual(1, w.MarkDump(3, 0, 3, 0));     // dump replaces the dig mark
            Assert.IsFalse(w.DigMark[3]);
            Assert.AreEqual(1, w.ClearMarks(0, 0, 5, 5));
        }

        [Test]
        public void Cannot_build_on_dug_or_marked_ground()
        {
            var w = Empty();
            w.MarkDig(5, 5, 5, 5);
            Assert.IsNotNull(w.CanPlace(BuildingType.Belt, 5, 5));
            w.ClearMarks(5, 5, 5, 5);
            w.Units[5 * w.W + 5] = -3;
            Assert.AreEqual("Ground isn't level (dug or filled)", w.CanPlace(BuildingType.Belt, 5, 5));
        }

        [Test]
        public void Excavator_and_truck_dig_ore_into_the_lodge_and_the_ground_goes_down()
        {
            var w = Empty();
            Ground(w, 40, 30, 3, 3, Terrain.IronOre);
            var g = Garage(w, 36, 30);
            Assert.IsNull(w.CanBuy(g, VehicleType.Excavator));
            Assert.IsNotNull(w.Buy(g.Id, VehicleType.Excavator));
            Assert.IsNotNull(w.Buy(g.Id, VehicleType.Truck));
            Assert.AreEqual(200 - 4 - 20 - 12, w.Storage[Item.Iron]);
            w.MarkDig(40, 30, 42, 32);
            int oreBefore = w.Storage[Item.Ore];
            w.Tick(240f);
            Assert.Greater(w.Storage[Item.Ore] - oreBefore, 40, "ore reached the lodge by truck");
            int dug = 0;
            for (int y = 30; y <= 32; y++) for (int x = 40; x <= 42; x++) if (w.Units[y * w.W + x] < 0) dug++;
            Assert.Greater(dug, 4, "several tiles are lower now");
            // Layer by layer: no tile is more than one layer deeper than another marked tile.
            int lo = 0, hi = -99;
            for (int y = 30; y <= 32; y++) for (int x = 40; x <= 42; x++)
            {
                int l = w.Layer(y * w.W + x);
                if (w.DigMark[y * w.W + x]) { lo = System.Math.Min(lo, l); hi = System.Math.Max(hi, l); }
            }
            Assert.LessOrEqual(hi - lo, 1);
        }

        [Test]
        public void Excavator_waits_for_a_truck_when_its_bucket_is_full()
        {
            var w = Empty();
            Ground(w, 40, 30, 2, 2, Terrain.Stone);
            var g = Garage(w, 36, 30);
            var ex = w.Buy(g.Id, VehicleType.Excavator);
            w.MarkDig(40, 30, 41, 31);
            w.Tick(30f);
            Assert.AreEqual(B.BucketCap, ex.BucketCount);
            Assert.AreEqual("Waiting for a truck", ex.Status);
        }

        [Test]
        public void Dirt_needs_a_dump_area_and_raises_the_ground_there()
        {
            var w = Empty();
            var g = Garage(w, 36, 30);
            w.Buy(g.Id, VehicleType.Excavator);
            var truck = w.Buy(g.Id, VehicleType.Truck);
            w.MarkDig(42, 30, 44, 32);     // grass: dirt on top
            w.Tick(60f);
            Assert.AreEqual(Item.Dirt, truck.Load);
            Assert.AreEqual("No dump area marked", truck.Status);
            w.MarkDump(42, 40, 44, 42);
            w.Tick(120f);
            int raised = 0;
            for (int y = 40; y <= 42; y++) for (int x = 42; x <= 44; x++) if (w.Units[y * w.W + x] > 0) raised++;
            Assert.Greater(raised, 0);
            Assert.AreEqual(0, w.Storage[Item.Dirt], "dirt never goes to the lodge");
        }

        [Test]
        public void Hopper_takes_truck_loads_and_puts_them_on_a_belt()
        {
            var w = Empty();
            Ground(w, 44, 30, 3, 3, Terrain.Stone);
            var g = Garage(w, 36, 30);
            w.Buy(g.Id, VehicleType.Excavator);
            w.Buy(g.Id, VehicleType.Truck);
            var hop = w.Place(BuildingType.Hopper, 40, 36, 3);   // facing west
            for (int x = 39; x >= 34; x--) w.Place(BuildingType.Belt, x, 36, 3);
            w.Place(BuildingType.Belt, 33, 36, 2);
            for (int y = 35; y >= 34; y--) w.Place(BuildingType.Belt, 33, y, 2);   // into the lodge top edge
            w.MarkDig(44, 30, 46, 32);
            int before = w.Delivered[Item.Stone];
            w.Tick(240f);
            Assert.Greater(w.Delivered[Item.Stone] - before, 20, "stone went truck -> hopper -> belt -> lodge");
        }

        [Test]
        public void Vehicles_refuel_at_their_garage_and_stop_when_it_is_empty()
        {
            var w = Empty();
            Ground(w, 40, 30, 3, 3, Terrain.IronOre);
            var g = Garage(w, 36, 30, fuel: 0);
            var ex = w.Buy(g.Id, VehicleType.Excavator);
            w.Buy(g.Id, VehicleType.Truck);
            w.MarkDig(40, 30, 42, 32);
            w.Tick(B.TankSeconds + 60f);
            Assert.Less(ex.Tank, B.RefuelBelow);
            g.FuelCharcoal = 6;
            w.Tick(60f);
            Assert.Less(g.FuelCharcoal, 6, "someone took fuel");
        }

        [Test]
        public void Garage_takes_fuel_by_belt_and_demolish_refunds_vehicles()
        {
            var w = Empty();
            var g = Garage(w, 36, 30, fuel: 0);
            Assert.IsTrue(w.Accept(g, Item.Charcoal));
            Assert.IsFalse(w.Accept(g, Item.Ore));
            w.Buy(g.Id, VehicleType.Truck);
            int iron = w.Storage[Item.Iron];
            w.Demolish(g.Id);
            Assert.AreEqual(0, w.Vehicles.Count);
            Assert.AreEqual(iron + B.TruckCost[0].count + 4, w.Storage[Item.Iron]);
        }

        [Test]
        public void Save_round_trip_keeps_ground_marks_and_vehicles()
        {
            var w = MapGen.NewTier1(B);
            w.Storage[Item.Iron] = 100;
            var g = w.Place(BuildingType.Garage, 36, 24, 0);
            w.Buy(g.Id, VehicleType.Excavator);
            w.Units[20 * w.W + 20] = -5;
            w.MarkDig(21, 20, 22, 21);
            w.MarkDump(25, 20, 25, 20);
            var back = SaveData.From(w).ToWorld(B);
            Assert.AreEqual(-5, back.Units[20 * w.W + 20]);
            Assert.IsTrue(back.DigMark[20 * w.W + 21]);
            Assert.IsTrue(back.DumpMark[20 * w.W + 25]);
            Assert.AreEqual(1, back.Vehicles.Count);
            Assert.AreEqual(VehicleType.Excavator, back.Vehicles[0].Type);
        }
    }
}
