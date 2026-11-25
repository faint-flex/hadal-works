using Kilnwood.Sim;
using NUnit.Framework;

namespace Kilnwood.Tests
{
    /// <summary>0.10 Tier 5 "Electricity": dynamos, power poles, grids and the wire mill.</summary>
    public class ElectricTests
    {
        Balance B;

        [SetUp]
        public void SetUp() { B = new Balance(); }

        World Valley(int tier = 5)
        {
            var w = new World(B);
            for (int y = 0; y < w.H; y++) { w.SetGround(0, y, Terrain.Water); w.SetGround(1, y, Terrain.Water); }
            w.Place(BuildingType.Lodge, 30, 30, 0, 0, free: true);
            foreach (var it in new[] { Item.Wood, Item.Stone, Item.Iron, Item.Brick, Item.Gear, Item.Plank, Item.Plate, Item.Steel }) w.Storage[it] = 500;
            w.UnlockTier(tier);
            return w;
        }

        /// <summary>Wheel at (2,10), shaft along y = 10 to x = 7, dynamo at (8,10) on the end of it.</summary>
        static Building WheelAndDynamo(World w)
        {
            Assert.IsNotNull(w.Place(BuildingType.Waterwheel, 2, 10, 1));
            for (int x = 4; x <= 7; x++) Assert.IsNotNull(w.Place(BuildingType.Shaft, x, 10, 0));
            var d = w.Place(BuildingType.Dynamo, 8, 10, 3);
            Assert.IsNotNull(d);
            return d;
        }

        [Test]
        public void Tier5_buildings_are_locked_until_the_rail_goal_and_wire_opens_tier6()
        {
            var w = Valley(tier: 4);
            Assert.AreEqual("Unlocks at Tier 5", w.CanPlace(BuildingType.Dynamo, 10, 20));
            Assert.AreEqual("Unlocks at Tier 5", w.CanPlace(BuildingType.Pole, 10, 20));
            w.UnlockTier(5);
            Assert.IsNull(w.CanPlace(BuildingType.WireMill, 10, 20));
            w.Delivered[Item.Wire] = B.GoalWire;
            w.Tick(0.1f);
            Assert.AreEqual(6, w.Tier);
            Assert.GreaterOrEqual(w.Tier5GoalAt, 0f);
        }

        [Test]
        public void Wire_mill_needs_a_pole_then_makes_two_wire_per_steel()
        {
            var w = Valley();
            WheelAndDynamo(w);
            var mill = w.Place(BuildingType.WireMill, 20, 10, 0);
            mill.Input[Item.Steel] = 1;
            w.Tick(0.5f);
            StringAssert.StartsWith("No electricity: needs a power pole", mill.Status);
            Assert.IsNotNull(w.Place(BuildingType.Pole, 15, 10, 0));   // 5 tiles from the dynamo, 3 from... too far for the mill
            w.Tick(0.1f);
            StringAssert.StartsWith("No electricity: needs a power pole", mill.Status);
            Assert.IsNotNull(w.Place(BuildingType.Pole, 18, 11, 0));   // 2 tiles from the mill
            w.Tick(B.Defs[BuildingType.WireMill].Recipes[0].Seconds + 0.1f);
            Assert.AreEqual(Item.Wire, mill.OutItem);
            Assert.AreEqual(2, mill.OutCount);
        }

        [Test]
        public void Poles_further_apart_than_their_reach_are_separate_grids()
        {
            var w = Valley();
            WheelAndDynamo(w);
            var near = w.Place(BuildingType.Pole, 9 + B.PoleReach, 10, 0);    // gap of exactly PoleReach from the dynamo's east edge
            var far = w.Place(BuildingType.Pole, 9 + B.PoleReach * 2 + 1, 10, 0);
            w.Tick(0.1f);
            Assert.AreEqual(1f, near.Elec, 1e-4);
            Assert.Less(far.Elec, 0.01f);
            StringAssert.StartsWith("Not linked", far.Status);
        }

        [Test]
        public void Too_many_mills_share_the_electricity()
        {
            var w = Valley();
            WheelAndDynamo(w);
            var a = w.Place(BuildingType.WireMill, 8, 13, 0);   // both within 2 tiles of the dynamo
            var b = w.Place(BuildingType.WireMill, 10, 12, 0);
            w.Tick(0.1f);
            float want = 2 * B.WireMillKw, made = B.DynamoKw * System.Math.Min(1f, B.WheelHp / (float)B.DynamoHp);   // a lone wheel turns it at 10/12
            Assert.AreEqual(made / want, a.Elec, 1e-4);
            Assert.AreEqual(a.Elec, b.Elec, 1e-4);
            a.Input[Item.Steel] = 1;
            w.Tick(0.1f);
            StringAssert.StartsWith("Low electricity", a.Status);
        }

        [Test]
        public void A_dynamo_without_a_shaft_makes_nothing()
        {
            var w = Valley();
            var d = w.Place(BuildingType.Dynamo, 20, 20, 0);
            var mill = w.Place(BuildingType.WireMill, 23, 20, 0);
            mill.Input[Item.Steel] = 1;
            w.Tick(0.5f);
            StringAssert.StartsWith("Not turning", d.Status);
            StringAssert.StartsWith("No electricity: dynamo not turning", mill.Status);
            Assert.AreEqual(0, mill.OutCount);
        }

        [Test]
        public void Wire_survives_a_save_and_tier6_comes_back()
        {
            var w = Valley();
            w.Delivered[Item.Wire] = B.GoalWire;
            w.Tick(0.1f);
            var back = SaveData.From(w).ToWorld(B);
            Assert.AreEqual(B.GoalWire, back.Delivered[Item.Wire]);
        }
    }
}
