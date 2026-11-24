using Kilnwood.Sim;
using NUnit.Framework;

namespace Kilnwood.Tests
{
    /// <summary>0.6 Tier 2 "Water Power": tiers, waterwheels, line shafts, sawmill, trip hammer.</summary>
    public class PowerTests
    {
        Balance B;

        [SetUp]
        public void SetUp() { B = new Balance(); }

        /// <summary>Lodge in the middle, a river along x = 0..1, plenty of everything.</summary>
        World Valley(bool tier2 = true)
        {
            var w = new World(B);
            for (int y = 0; y < w.H; y++) { w.SetGround(0, y, Terrain.Water); w.SetGround(1, y, Terrain.Water); }
            w.Place(BuildingType.Lodge, 30, 30, 0, 0, free: true);
            foreach (var it in new[] { Item.Wood, Item.Stone, Item.Iron, Item.Brick, Item.Gear, Item.Plank }) w.Storage[it] = 500;
            if (tier2) w.UnlockTier(2);
            return w;
        }

        /// <summary>Wheel at (2,10) on the bank, shaft east along y = 10 from x = 4 to x1.</summary>
        static void WheelAndShaft(World w, int x1)
        {
            Assert.IsNotNull(w.Place(BuildingType.Waterwheel, 2, 10, 1));
            for (int x = 4; x <= x1; x++) Assert.IsNotNull(w.Place(BuildingType.Shaft, x, 10, 0));
        }

        [Test]
        public void Tier2_buildings_are_locked_until_the_tier1_goal()
        {
            var w = Valley(tier2: false);
            Assert.AreEqual("Unlocks at Tier 2", w.CanPlace(BuildingType.Sawmill, 10, 20));
            w.Delivered[Item.Gear] = B.GoalGear;
            w.Delivered[Item.Brick] = B.GoalBrick;
            w.Tick(0.1f);
            Assert.AreEqual(2, w.Tier);
            Assert.IsNull(w.CanPlace(BuildingType.Sawmill, 10, 20));
        }

        [Test]
        public void Waterwheels_must_touch_the_river()
        {
            var w = Valley();
            Assert.AreEqual("Waterwheels must touch the river", w.CanPlace(BuildingType.Waterwheel, 10, 10));
            Assert.IsNull(w.CanPlace(BuildingType.Waterwheel, 2, 10));
        }

        [Test]
        public void Sawmill_needs_power_and_makes_two_planks_per_log()
        {
            var w = Valley();
            var saw = w.Place(BuildingType.Sawmill, 8, 11, 0);   // touches the shaft at y = 10 once it is there
            saw.Input[Item.Wood] = 1;
            w.Tick(1f);
            StringAssert.StartsWith("No power", saw.Status);
            WheelAndShaft(w, 9);
            w.Tick(2.1f);
            Assert.AreEqual(Item.Plank, saw.OutItem);
            Assert.AreEqual(2, saw.OutCount);
        }

        [Test]
        public void Too_many_machines_share_the_power_and_slow_down()
        {
            var w = Valley();
            WheelAndShaft(w, 13);
            var a = w.Place(BuildingType.TripHammer, 6, 11, 0);
            var b = w.Place(BuildingType.TripHammer, 8, 11, 0);
            var c = w.Place(BuildingType.TripHammer, 10, 11, 0);
            w.Tick(0.05f);
            Assert.AreEqual(10f / 12f, a.Power, 1e-4);
            Assert.AreEqual(a.Power, c.Power, 1e-4);
            // 3 s craft at 83% takes 3.6 s.
            a.Input[Item.Iron] = 2;
            w.Tick(3.3f);
            Assert.AreEqual(0, a.OutCount);
            StringAssert.StartsWith("Low power 83%", a.Status);
            w.Tick(0.4f);
            Assert.AreEqual(Item.Plate, a.OutItem);
            Assert.That(w.At(2, 10).Status, Does.StartWith("Overloaded"));
            Assert.IsNotNull(b);
        }

        [Test]
        public void A_shaft_without_a_wheel_gives_nothing()
        {
            var w = Valley();
            for (int x = 10; x <= 14; x++) w.Place(BuildingType.Shaft, x, 20, 0);
            var saw = w.Place(BuildingType.Sawmill, 12, 21, 0);
            saw.Input[Item.Wood] = 1;
            w.Tick(3f);
            Assert.AreEqual(0, saw.OutCount);
            Assert.AreEqual(-1f, saw.Power);
        }

        [Test]
        public void Powered_workshop_runs_without_fuel_and_demolishing_the_wheel_stops_it()
        {
            var w = Valley();
            WheelAndShaft(w, 9);
            var shop = w.Place(BuildingType.Workshop, 6, 11, 0);
            shop.Input[Item.Iron] = 4;
            w.Tick(4.1f);
            Assert.AreEqual(1, shop.OutCount);
            Assert.AreEqual(0, shop.FuelCount);
            w.Demolish(w.At(2, 10).Id);
            w.Tick(5f);
            Assert.AreEqual("No fuel", shop.Status);
        }

        [Test]
        public void Tier2_goal_unlocks_tier3()
        {
            var w = Valley();
            w.Delivered[Item.Plank] = B.GoalPlank;
            w.Delivered[Item.Plate] = B.GoalPlate - 1;
            w.Tick(0.1f);
            Assert.AreEqual(2, w.Tier);
            w.Delivered[Item.Plate]++;
            w.Tick(0.1f);
            Assert.AreEqual(3, w.Tier);
            Assert.Greater(w.Tier2GoalAt, 0f);
        }

        [Test]
        public void Save_keeps_tier_and_water_power()
        {
            var w = MapGen.NewTier1(B);
            w.Delivered[Item.Gear] = B.GoalGear; w.Delivered[Item.Brick] = B.GoalBrick;
            w.Storage[Item.Wood] = 500; w.Storage[Item.Brick] = 500; w.Storage[Item.Gear] = 500;
            w.Tick(0.1f);
            // River runs about x = 2..4 near y = 18: the bank is at x = 5.
            int wx = 5; while (w.CanPlace(BuildingType.Waterwheel, wx, 18) != null && wx < 9) wx++;
            Assert.IsNotNull(w.Place(BuildingType.Waterwheel, wx, 18, 1), w.CanPlace(BuildingType.Waterwheel, wx, 18));
            w.Place(BuildingType.Shaft, wx + 2, 18, 0);
            var back = SaveData.From(w).ToWorld(B);
            Assert.AreEqual(2, back.Tier);
            Assert.AreEqual(BuildingType.Waterwheel, back.At(wx, 18).Type);
            back.Tick(0.1f);
            Assert.GreaterOrEqual(back.ShaftAt(wx + 2, 18).Power, 0f);
        }

        [Test]
        public void Shafts_run_over_belts_but_not_through_machines()
        {
            var w = Valley();
            w.Place(BuildingType.Belt, 20, 20, 1);
            Assert.IsNotNull(w.Place(BuildingType.Shaft, 20, 20, 0));
            Assert.AreEqual(BuildingType.Belt, w.At(20, 20).Type);
            Assert.AreEqual(BuildingType.Shaft, w.ShaftAt(20, 20).Type);
            Assert.AreEqual("A line shaft is already there", w.CanPlace(BuildingType.Shaft, 20, 20));
            w.Place(BuildingType.Sawmill, 24, 24, 0);
            Assert.AreEqual("Something is already there", w.CanPlace(BuildingType.Shaft, 24, 24));
            w.Place(BuildingType.Shaft, 30, 20, 0);
            Assert.AreEqual("A line shaft is in the way", w.CanPlace(BuildingType.Sawmill, 29, 19));
            Assert.IsNull(w.CanPlace(BuildingType.Belt, 30, 20));
        }
    }
}
