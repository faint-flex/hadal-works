using Kilnwood.Sim;
using NUnit.Framework;

namespace Kilnwood.Tests
{
    public class RailTests
    {
        static readonly Balance B = new Balance();

        World Valley()
        {
            var w = new World(B);
            w.Place(BuildingType.Lodge, 30, 30, 0, 0, free: true);
            foreach (var it in new[] { Item.Wood, Item.Stone, Item.Iron, Item.Brick, Item.Gear, Item.Plank, Item.Plate, Item.Steel, Item.Coal }) w.Storage[it] = 500;
            w.UnlockTier(4);
            return w;
        }

        /// <summary>Yard station beside the lodge, field station 15 tiles east, rail along y = 29 between them.</summary>
        (World w, Building yard, Building field) Line()
        {
            var w = Valley();
            var yard = w.Place(BuildingType.Station, 35, 30, 0);
            var field = w.Place(BuildingType.Station, 50, 30, 0);
            for (int x = 35; x <= 51; x++) Assert.NotNull(w.Place(BuildingType.Rail, x, 29, 1), "rail at " + x);
            return (w, yard, field);
        }

        [Test]
        public void Rails_unlock_at_tier_4_and_cost_steel()
        {
            var w = new World(B);
            w.Place(BuildingType.Lodge, 30, 30, 0, 0, free: true);
            w.Storage[Item.Steel] = 10; w.Storage[Item.Wood] = 10;
            StringAssert.Contains("Tier 4", w.CanPlace(BuildingType.Rail, 10, 10));
            w.UnlockTier(4);
            Assert.NotNull(w.Place(BuildingType.Rail, 10, 10, 0));
            Assert.AreEqual(9, w.Storage[Item.Steel]);
        }

        [Test]
        public void Yard_is_near_the_lodge_only()
        {
            var (w, yard, field) = Line();
            Assert.IsTrue(w.IsYard(yard));
            Assert.IsFalse(w.IsYard(field));
        }

        [Test]
        public void Train_needs_a_rail_beside_the_station()
        {
            var w = Valley();
            var st = w.Place(BuildingType.Station, 40, 40, 0);
            StringAssert.Contains("rail", w.CanBuy(st, VehicleType.Train));
            w.Place(BuildingType.Rail, 40, 39, 0);
            Assert.IsNull(w.CanBuy(st, VehicleType.Train));
            int steel = w.Storage[Item.Steel];
            Assert.NotNull(w.Buy(st.Id, VehicleType.Train));
            Assert.AreEqual(steel - 20, w.Storage[Item.Steel]);
        }

        [Test]
        public void Train_hauls_from_the_field_to_the_lodge()
        {
            var (w, yard, field) = Line();
            for (int i = 0; i < 30; i++) Assert.IsTrue(w.Accept(field, Item.Ore));
            Assert.NotNull(w.Buy(field.Id, VehicleType.Train));
            int ore = w.Storage[Item.Ore];
            for (int t = 0; t < 120; t++) w.Tick(1f);
            Assert.GreaterOrEqual(w.RailHauled, 24, "first load (24) reaches the lodge");
            TestContext.WriteLine("ore " + (w.Storage[Item.Ore] - ore) + " coal " + w.Storage[Item.Coal] + " hauled " + w.RailHauled + " fieldOut " + World.StationStored(field.Output));
            Assert.AreEqual(ore + w.RailHauled, w.Storage[Item.Ore]);
        }

        [Test]
        public void Train_takes_fuel_out_and_the_field_station_feeds_a_mine()
        {
            var (w, yard, field) = Line();
            // A mine touching the field station, on ore, facing into the station.
            for (int y = 32; y <= 33; y++) for (int x = 50; x <= 51; x++) w.SetGround(x, y, Terrain.IronOre);
            var mine = w.Place(BuildingType.Mine, 50, 32, 2);
            Assert.NotNull(mine);
            Assert.NotNull(w.Buy(yard.Id, VehicleType.Train));
            for (int t = 0; t < 120; t++) w.Tick(1f);
            Assert.Greater(w.RailHauled, 0, "mine ore came back by train");
            Assert.AreNotEqual("No fuel", mine.Status);
        }

        [Test]
        public void Tier_5_after_the_rail_goal()
        {
            var (w, yard, field) = Line();
            Assert.NotNull(w.Buy(field.Id, VehicleType.Train));
            for (int t = 0; t < 1500 && w.Tier < 5; t++)
            {
                while (w.Accept(field, Item.Stone)) { }
                w.Tick(1f);
            }
            Assert.AreEqual(5, w.Tier);
            Assert.GreaterOrEqual(w.RailHauled, B.GoalRail);
        }

        [Test]
        public void Save_keeps_rails_stations_trains_and_the_count()
        {
            var w = MapGen.NewTier1(B);
            foreach (var it in new[] { Item.Wood, Item.Plank, Item.Brick, Item.Steel, Item.Plate, Item.Gear }) w.Storage[it] = 500;
            w.UnlockTier(4);
            var st = w.Place(BuildingType.Station, 36, 40, 0);
            Assert.NotNull(st);
            for (int x = 36; x <= 44; x++) w.Place(BuildingType.Rail, x, 39, 1);
            w.Place(BuildingType.Station, 43, 40, 0);
            Assert.NotNull(w.Buy(st.Id, VehicleType.Train));
            w.RestoreRailHauled(77);
            var back = SaveData.From(w).ToWorld(B);
            Assert.AreEqual(w.Buildings.Count, back.Buildings.Count);
            Assert.AreEqual(1, back.CountTrains());
            Assert.AreEqual(77, back.RailHauled);
        }
    }
}
