using Kilnwood.Sim;
using NUnit.Framework;

namespace Kilnwood.Tests
{
    /// <summary>0.14 Tier 7 "Assembly Line": the steam turbine, the electric furnace and the assembly plant.</summary>
    public class AssemblyTests
    {
        Balance B;

        [SetUp]
        public void SetUp() { B = new Balance(); }

        World Valley(int tier = 7)
        {
            var w = new World(B);
            for (int y = 0; y < w.H; y++) { w.SetGround(0, y, Terrain.Water); w.SetGround(1, y, Terrain.Water); }
            w.Place(BuildingType.Lodge, 30, 30, 0, 0, free: true);
            foreach (var it in new[] { Item.Wood, Item.Stone, Item.Iron, Item.Brick, Item.Gear, Item.Plank, Item.Plate, Item.Steel, Item.Wire, Item.Motor }) w.Storage[it] = 500;
            w.UnlockTier(tier);
            return w;
        }

        [Test]
        public void Tier7_buildings_are_locked_until_the_motor_goal_and_tractors_open_tier8()
        {
            var w = Valley(tier: 6);
            Assert.AreEqual("Unlocks at Tier 7", w.CanPlace(BuildingType.Turbine, 10, 20));
            Assert.AreEqual("Unlocks at Tier 7", w.CanPlace(BuildingType.Assembly, 10, 20));
            w.UnlockTier(7);
            Assert.IsNull(w.CanPlace(BuildingType.ArcFurnace, 10, 20));
            w.Delivered[Item.Tractor] = B.GoalTractor;
            w.Tick(0.1f);
            Assert.AreEqual(8, w.Tier);
            Assert.GreaterOrEqual(w.Tier7GoalAt, 0f);
        }

        [Test]
        public void Turbine_powers_a_grid_only_while_it_has_fuel()
        {
            var w = Valley();
            var t = w.Place(BuildingType.Turbine, 10, 10, 0);
            var mill = w.Place(BuildingType.WireMill, 13, 10, 0);   // 1 tile gap: inside the turbine's cover
            Assert.IsNotNull(t); Assert.IsNotNull(mill);
            mill.Input[Item.Steel] = 5;
            w.Tick(0.2f);
            StringAssert.StartsWith("No fuel", t.Status);
            StringAssert.StartsWith("No electricity", mill.Status);
            Assert.IsTrue(w.Accept(t, Item.Wood));
            w.Tick(0.2f);
            Assert.AreEqual(B.TurbineKw, mill.GridSupply, 0.01f);
            Assert.AreEqual(1f, mill.Elec, 0.001f);
            StringAssert.StartsWith("Making " + B.TurbineKw + " kW", t.Status);
            // One wood lasts WoodFuelSeconds / TurbineBurnRate seconds, then the grid goes dark.
            for (int i = 0; i < 60; i++) w.Tick(0.1f);
            Assert.AreEqual(0f, mill.Elec, 0.001f);
            StringAssert.StartsWith("No fuel", t.Status);
        }

        [Test]
        public void Woodcutter_feeds_a_turbine_directly()
        {
            var w = Valley();
            for (int y = 12; y < 16; y++) for (int x = 9; x < 13; x++) w.SetGround(x, y, Terrain.Forest);
            var t = w.Place(BuildingType.Turbine, 10, 10, 0);
            Assert.IsNotNull(w.Place(BuildingType.Woodcutter, 10, 12, 2));   // faces south into the turbine
            Assert.IsNotNull(w.Place(BuildingType.WireMill, 13, 10, 0));
            for (int i = 0; i < 200; i++) w.Tick(0.1f);
            Assert.IsTrue(t.Fired, t.Status);
        }

        [Test]
        public void Electric_furnace_makes_steel_from_ore_without_coal()
        {
            var w = Valley();
            Assert.IsNotNull(w.Place(BuildingType.Turbine, 10, 10, 0));
            var f = w.Place(BuildingType.ArcFurnace, 13, 10, 1);
            var t = w.At(10, 10);
            t.FuelCoal = 5;
            f.Input[Item.Ore] = 2;
            for (int i = 0; i < 35; i++) w.Tick(0.1f);
            Assert.AreEqual(Item.Steel, f.OutItem);
            Assert.AreEqual(1, f.OutCount);
            Assert.AreEqual(0, f.Input.TryGetValue(Item.Ore, out int left) ? left : 0);
        }

        [Test]
        public void Assembly_plant_turns_two_motors_and_two_steel_into_a_tractor()
        {
            var w = Valley();
            var t = w.Place(BuildingType.Turbine, 10, 10, 0);
            t.FuelCoal = 5;
            var a = w.Place(BuildingType.Assembly, 13, 10, 1);
            Assert.IsNotNull(a);
            a.Input[Item.Motor] = 2;
            a.Input[Item.Steel] = 2;
            for (int i = 0; i < 85; i++) w.Tick(0.1f);
            Assert.AreEqual(Item.Tractor, a.OutItem);
            Assert.AreEqual(1, a.OutCount);
        }
    }
}
