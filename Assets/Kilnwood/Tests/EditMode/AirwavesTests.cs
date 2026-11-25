using Kilnwood.Sim;
using NUnit.Framework;

namespace Kilnwood.Tests
{
    /// <summary>0.16 Tier 9 "Airwaves": the hydro dam, the glassworks and the radio works.</summary>
    public class AirwavesTests
    {
        Balance B;

        [SetUp]
        public void SetUp() { B = new Balance(); }

        World Valley(int tier = 9)
        {
            var w = new World(B);
            for (int y = 0; y < w.H; y++) { w.SetGround(0, y, Terrain.Water); w.SetGround(1, y, Terrain.Water); }
            w.Place(BuildingType.Lodge, 30, 30, 0, 0, free: true);
            foreach (var it in new[] { Item.Wood, Item.Stone, Item.Iron, Item.Brick, Item.Gear, Item.Plank, Item.Plate, Item.Steel, Item.Wire, Item.Motor }) w.Storage[it] = 500;
            w.UnlockTier(tier);
            return w;
        }

        [Test]
        public void Tier9_buildings_are_locked_until_the_aeroplane_goal()
        {
            var w = Valley(tier: 8);
            Assert.AreEqual("Unlocks at Tier 9", w.CanPlace(BuildingType.HydroDam, 2, 10));
            Assert.AreEqual("Unlocks at Tier 9", w.CanPlace(BuildingType.Glassworks, 10, 10));
            Assert.AreEqual("Unlocks at Tier 9", w.CanPlace(BuildingType.RadioWorks, 10, 10));
            w.UnlockTier(9);
            Assert.IsNull(w.CanPlace(BuildingType.Glassworks, 10, 10));
        }

        [Test]
        public void Hydro_dam_must_touch_the_river_and_never_needs_fuel()
        {
            var w = Valley();
            Assert.AreEqual("Hydro dams must touch the river", w.CanPlace(BuildingType.HydroDam, 10, 10));
            var dam = w.Place(BuildingType.HydroDam, 2, 10, 1);
            Assert.IsNotNull(dam);
            var glass = w.Place(BuildingType.Glassworks, 5, 10, 0);   // 1 tile gap: inside the dam's cover
            Assert.IsNotNull(glass);
            for (int i = 0; i < 600; i++) w.Tick(0.1f);   // a minute: still running, nothing burned
            Assert.AreEqual(B.HydroKw, glass.GridSupply, 0.01f);
            Assert.AreEqual(1f, glass.Elec, 0.001f);
            StringAssert.StartsWith("Making " + B.HydroKw + " kW", dam.Status);
            Assert.IsFalse(w.Accept(dam, Item.Wood));
        }

        [Test]
        public void Glassworks_melts_two_stone_into_one_glass()
        {
            var w = Valley();
            w.Place(BuildingType.HydroDam, 2, 10, 1);
            var g = w.Place(BuildingType.Glassworks, 5, 10, 1);
            g.Input[Item.Stone] = 2;
            for (int i = 0; i < 35; i++) w.Tick(0.1f);
            Assert.AreEqual(Item.Glass, g.OutItem);
            Assert.AreEqual(1, g.OutCount);
        }

        [Test]
        public void Radio_works_builds_a_radio_set_and_radios_open_tier10()
        {
            var w = Valley();
            w.Place(BuildingType.HydroDam, 2, 10, 1);
            var r = w.Place(BuildingType.RadioWorks, 5, 10, 1);
            Assert.IsNotNull(r);
            Assert.IsTrue(w.Accept(r, Item.Glass));
            Assert.IsTrue(w.Accept(r, Item.Glass));
            Assert.IsTrue(w.Accept(r, Item.Wood));
            Assert.IsFalse(w.Accept(r, Item.Stone));
            for (int i = 0; i < 65; i++) w.Tick(0.1f);
            Assert.AreEqual(Item.Radio, r.OutItem);
            Assert.AreEqual(1, r.OutCount);
            w.Delivered[Item.Radio] = B.GoalRadio;
            w.Tick(0.1f);
            Assert.AreEqual(10, w.Tier);
            Assert.GreaterOrEqual(w.Tier9GoalAt, 0f);
        }
    }
}
