using Kilnwood.Sim;
using NUnit.Framework;

namespace Kilnwood.Tests
{
    /// <summary>0.7 Tier 3 "Steam": coal seams, steam engines, the steel furnace, mixed hoppers.</summary>
    public class SteamTests
    {
        Balance B;

        [SetUp]
        public void SetUp() { B = new Balance(); }

        World Valley()
        {
            var w = new World(B);
            w.Place(BuildingType.Lodge, 30, 30, 0, 0, free: true);
            foreach (var it in new[] { Item.Wood, Item.Stone, Item.Iron, Item.Brick, Item.Gear, Item.Plank, Item.Plate }) w.Storage[it] = 500;
            w.UnlockTier(3);
            return w;
        }

        [Test]
        public void Coal_lies_two_levels_down_in_the_seam()
        {
            var w = Valley();
            int i = 10 * w.W + 10;
            w.CoalSeam[i] = true;
            Assert.AreEqual(Item.Dirt, w.MaterialAt(i));
            w.Units[i] = -B.UnitsPerLevel;
            Assert.AreEqual(Item.Stone, w.MaterialAt(i));
            w.Units[i] = -B.UnitsPerLevel * 2;
            Assert.AreEqual(Item.Coal, w.MaterialAt(i));
            w.Units[i + 1] = -B.UnitsPerLevel * 2;
            Assert.AreEqual(Item.Stone, w.MaterialAt(i + 1));   // same depth, no seam
        }

        [Test]
        public void Map_has_a_coal_seam()
        {
            var w = MapGen.NewTier1(B);
            int n = 0; foreach (var c in w.CoalSeam) if (c) n++;
            Assert.AreEqual(MapGen.CoalW * MapGen.CoalH, n);
        }

        [Test]
        public void Steam_engine_powers_a_furnace_only_while_fired()
        {
            var w = Valley();
            var eng = w.Place(BuildingType.SteamEngine, 10, 10, 0);
            var fur = w.Place(BuildingType.Furnace, 12, 10, 0);   // touching the engine
            fur.Input[Item.Ore] = 2; fur.Input[Item.Coal] = 1;
            w.Tick(1f);
            StringAssert.StartsWith("No power", fur.Status);
            StringAssert.StartsWith("No fuel", eng.Status);
            Assert.IsTrue(w.Accept(eng, Item.Coal));
            w.Tick(4.1f);
            Assert.AreEqual(Item.Steel, fur.OutItem);
            Assert.AreEqual(1f, fur.Power, 1e-4);
            // One coal lasts 40 s of fire; the engine burns 3x as fast.
            w.Tick(10f);
            StringAssert.StartsWith("No fuel", eng.Status);
        }

        [Test]
        public void Engine_adds_to_a_waterwheel_network()
        {
            var w = Valley();
            for (int y = 0; y < w.H; y++) { w.SetGround(0, y, Terrain.Water); w.SetGround(1, y, Terrain.Water); }
            w.Place(BuildingType.Waterwheel, 2, 10, 1);
            for (int x = 4; x <= 12; x++) w.Place(BuildingType.Shaft, x, 10, 0);
            var eng = w.Place(BuildingType.SteamEngine, 13, 10, 0);
            eng.FuelCoal = 4;
            for (int k = 0; k < 4; k++) w.Place(BuildingType.Furnace, 4 + k * 2, 11, 0);   // 24 hp
            w.Tick(0.1f);
            Assert.AreEqual(40, w.At(2, 10).NetSupply);
            Assert.AreEqual(1f, w.At(4, 11).Power, 1e-4);
            eng.FuelCoal = 0; eng.FuelLeft = 0f;
            w.Tick(0.1f);
            Assert.AreEqual(10f / 24f, w.At(4, 11).Power, 1e-3);
        }

        [Test]
        public void Hopper_feeds_a_two_input_machine_both_things()
        {
            var w = Valley();
            var h = w.Place(BuildingType.Hopper, 10, 10, 0);
            var fur = w.Place(BuildingType.Furnace, 10, 12, 0);
            var eng = w.Place(BuildingType.SteamEngine, 12, 12, 0);
            eng.FuelCoal = 4;
            h.Input[Item.Ore] = 20; h.Input[Item.Coal] = 10;
            Assert.IsTrue(w.HopperWants(h, Item.Coal));
            Assert.IsFalse(w.HopperWants(h, Item.Stone));
            w.Tick(30f);
            Assert.Greater(w.Delivered[Item.Steel] + fur.OutCount, 0);
        }

        [Test]
        public void Tier3_goal_unlocks_tier4()
        {
            var w = Valley();
            w.Delivered[Item.Steel] = B.GoalSteel;
            w.Delivered[Item.Coal] = B.GoalCoal;
            w.Tick(0.1f);
            Assert.AreEqual(4, w.Tier);
        }

        [Test]
        public void Demo_reaches_the_tier3_goal()
        {
            var w = MapGen.NewTier1(B);
            var demo = new DemoFactory(false, pit: true, tier2: true, tier3: true);
            bool g1 = false, g3 = false;
            while (w.Time < 3600f && w.Tier < 4)
            {
                demo.BuildWhatYouCan(w);
                demo.PitActions(w);
                if (!g1 && w.Time >= 300f) { w.Storage[Item.Iron] += 60; g1 = true; }
                if (!g3 && w.Tier >= 3) { w.Storage[Item.Iron] += 40; g3 = true; }
                w.Tick(1f);
            }
            Assert.AreEqual(4, w.Tier, "tier 3 goal not reached in an hour");
        }
    }
}
