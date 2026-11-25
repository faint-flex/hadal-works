using Kilnwood.Sim;
using NUnit.Framework;

namespace Kilnwood.Tests
{
    /// <summary>0.20 "Machine eras": pit machines and locomotives refit as the tiers rise.</summary>
    public class MachineEraTests
    {
        [Test]
        public void Era_follows_the_tier()
        {
            var w = new World(new Balance());
            Assert.AreEqual(0, w.Era);
            w.UnlockTier(3); Assert.AreEqual(0, w.Era);
            w.UnlockTier(4); Assert.AreEqual(1, w.Era);
            w.UnlockTier(6); Assert.AreEqual(1, w.Era);
            w.UnlockTier(7); Assert.AreEqual(2, w.Era);
        }

        [Test]
        public void Later_eras_dig_faster_and_trucks_carry_more()
        {
            var b = new Balance();
            var w = new World(b);
            float dig0 = w.DigTime; int cap0 = w.TruckCapacity;
            w.UnlockTier(7);
            Assert.Less(w.DigTime, dig0);
            Assert.Greater(w.TruckCapacity, cap0);
            Assert.AreEqual(b.TruckCap, cap0);
        }

        [Test]
        public void Machines_have_era_names()
        {
            Assert.AreEqual("Steam shovel", World.VehicleName(VehicleType.Excavator, 0));
            Assert.AreEqual("Hydraulic excavator", World.VehicleName(VehicleType.Excavator, 2));
            Assert.AreEqual("Dump truck", World.VehicleName(VehicleType.Truck, 2));
            Assert.AreEqual("Diesel locomotive", World.VehicleName(VehicleType.Train, 2));
        }
    }
}
