using Kilnwood.Sim;
using NUnit.Framework;

namespace Kilnwood.Tests
{
    /// <summary>0.21 Tier 10 "Drones": the drone works and the drone port.</summary>
    public class DroneTests
    {
        Balance B;

        [SetUp]
        public void SetUp() { B = new Balance(); }

        World Valley(int tier = 10)
        {
            var w = new World(B);
            w.Place(BuildingType.Lodge, 30, 30, 0, 0, free: true);
            foreach (var it in new[] { Item.Wood, Item.Stone, Item.Iron, Item.Brick, Item.Gear, Item.Plank, Item.Plate, Item.Steel, Item.Wire, Item.Motor, Item.Radio }) w.Storage[it] = 500;
            w.UnlockTier(tier);
            return w;
        }

        [Test]
        public void Drone_buildings_unlock_at_tier_10()
        {
            var w = Valley(tier: 9);
            Assert.AreEqual("Unlocks at Tier 10", w.CanPlace(BuildingType.DroneWorks, 10, 10));
            Assert.AreEqual("Unlocks at Tier 10", w.CanPlace(BuildingType.DronePort, 10, 10));
            w.UnlockTier(10);
            Assert.IsNull(w.CanPlace(BuildingType.DronePort, 10, 10));
        }

        [Test]
        public void Drone_port_flies_goods_home_with_drones_from_the_lodge()
        {
            var w = Valley();
            var port = w.Place(BuildingType.DronePort, 30, 20, 0);
            for (int i = 0; i < 8; i++) Assert.IsTrue(w.Accept(port, Item.Wood));
            Assert.IsFalse(w.Accept(port, Item.Drone));
            for (int i = 0; i < 20; i++) w.Tick(0.5f);
            StringAssert.StartsWith("No drones", port.Status);
            int wood = w.Storage[Item.Wood];
            w.Storage[Item.Drone] = 6;
            for (int i = 0; i < 40; i++) w.Tick(0.5f);
            Assert.AreEqual(B.DronePortDrones, port.Drones);
            Assert.AreEqual(2, w.Storage[Item.Drone]);
            Assert.AreEqual(8, w.DroneFlown);
            Assert.AreEqual(wood + 8, w.Storage[Item.Wood]);
            w.Demolish(port.Id);
            Assert.AreEqual(6, w.Storage[Item.Drone]);
        }

        [Test]
        public void Drone_port_must_be_in_range_of_the_lodge()
        {
            var w = Valley();
            w.Storage[Item.Drone] = 4;
            var port = w.Place(BuildingType.DronePort, 0 + 2, 0 + 2, 0);
            Assume.That(port, Is.Not.Null);
            w.Tick(0.5f);
            StringAssert.StartsWith("Lodge too far", port.Status);
        }

        [Test]
        public void Sixty_drones_reach_tier_11()
        {
            var w = Valley();
            w.Delivered[Item.Drone] = B.GoalDrone;
            w.UpdateTier();
            Assert.AreEqual(11, w.Tier);
        }
    }
}
