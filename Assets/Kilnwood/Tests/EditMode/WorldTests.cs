using Kilnwood.Sim;
using NUnit.Framework;

namespace Kilnwood.Tests
{
    public class WorldTests
    {
        Balance B;

        [SetUp]
        public void SetUp() { B = new Balance(); }

        World Empty()
        {
            var w = new World(B);
            w.Place(BuildingType.Lodge, 30, 30, 0, 0, free: true);
            return w;
        }

        static void Ground(World w, int x, int y, int size, Terrain t)
        {
            for (int dy = 0; dy < size; dy++) for (int dx = 0; dx < size; dx++) w.SetGround(x + dx, y + dy, t);
        }

        [Test]
        public void Tier1_map_has_lodge_resources_and_river()
        {
            var w = MapGen.NewTier1(B);
            Assert.IsNotNull(w.Lodge);
            int f = 0, o = 0, s = 0, water = 0;
            foreach (var t in w.Ground)
            {
                if (t == Terrain.Forest) f++;
                if (t == Terrain.IronOre) o++;
                if (t == Terrain.Stone) s++;
                if (t == Terrain.Water) water++;
            }
            Assert.Greater(f, 300); Assert.Greater(o, 80); Assert.Greater(s, 60); Assert.Greater(water, 150);
            Assert.AreEqual(B.StartWood, w.Storage[Item.Wood]);
            Assert.AreEqual(B.StartStone, w.Storage[Item.Stone]);
            Assert.AreEqual(B.StartIron, w.Storage[Item.Iron]);
        }

        [Test]
        public void Placing_costs_and_demolish_refunds()
        {
            var w = Empty();
            var k = w.Place(BuildingType.Smelter, 10, 10, 0);
            Assert.IsNotNull(k);
            Assert.AreEqual(B.StartStone - 12, w.Storage[Item.Stone]);
            Assert.AreEqual(B.StartWood - 4, w.Storage[Item.Wood]);
            Assert.IsTrue(w.Demolish(k.Id));
            Assert.AreEqual(B.StartStone, w.Storage[Item.Stone]);
            Assert.AreEqual(B.StartWood, w.Storage[Item.Wood]);
            Assert.IsNull(w.At(10, 10));
        }

        [Test]
        public void Cannot_overlap_leave_map_or_build_on_water()
        {
            var w = Empty();
            Assert.IsNotNull(w.CanPlace(BuildingType.Belt, 31, 31));
            Assert.IsNotNull(w.CanPlace(BuildingType.Kiln, 63, 63));
            Assert.IsNull(w.CanPlace(BuildingType.Kiln, 10, 10));
            w.SetGround(11, 11, Terrain.Water);
            Assert.IsNotNull(w.CanPlace(BuildingType.Kiln, 10, 10));
            Assert.IsNotNull(w.CanPlace(BuildingType.Belt, 11, 11));
        }

        [Test]
        public void Woodcutter_needs_forest_and_mine_needs_ore_or_stone()
        {
            var w = Empty();
            Assert.IsNotNull(w.CanPlace(BuildingType.Woodcutter, 5, 5));
            Assert.IsNotNull(w.CanPlace(BuildingType.Mine, 5, 5));
            w.SetGround(5, 5, Terrain.Forest);
            Assert.IsNull(w.CanPlace(BuildingType.Woodcutter, 5, 5));
            Assert.IsNotNull(w.CanPlace(BuildingType.Mine, 5, 5));
            w.SetGround(10, 10, Terrain.Stone);
            Assert.IsNull(w.CanPlace(BuildingType.Mine, 10, 10));
            var m = w.Place(BuildingType.Mine, 10, 10, 0);
            Assert.AreEqual(Item.Stone, w.ExtractorResource(m, out _));
        }

        [Test]
        public void Cannot_afford_shows_reason()
        {
            var w = Empty();
            w.Storage[Item.Stone] = 3;
            StringAssert.Contains("stone", w.CanPlace(BuildingType.Kiln, 10, 10));
        }

        [Test]
        public void Woodcutter_to_belt_to_lodge_delivers_without_fuel()
        {
            var w = Empty();
            Ground(w, 20, 30, 2, Terrain.Forest);
            w.Place(BuildingType.Woodcutter, 20, 30, 1);
            for (int x = 22; x < 30; x++) w.Place(BuildingType.Belt, x, 30, 1);
            w.Tick(20f);
            Assert.Greater(w.Delivered[Item.Wood], 10);
        }

        [Test]
        public void Mine_stops_without_fuel_and_runs_on_wood()
        {
            var w = Empty();
            Ground(w, 10, 10, 2, Terrain.IronOre);
            var m = w.Place(BuildingType.Mine, 10, 10, 1);
            w.Tick(5f);
            Assert.AreEqual("No fuel", m.Status);
            Assert.AreEqual(0, m.OutCount);
            Assert.IsTrue(w.Accept(m, Item.Wood));
            w.Tick(1f);
            Assert.AreEqual("Working", m.Status);
            Assert.Greater(m.OutCount, 0);
            Assert.AreEqual(Item.Ore, m.OutItem);
        }

        [Test]
        public void Charcoal_burns_three_times_longer_than_wood()
        {
            B.OutputCap = 1000; // never blocked, so the mines run flat out
            var w = Empty();
            Ground(w, 10, 10, 2, Terrain.IronOre);
            Ground(w, 20, 10, 2, Terrain.IronOre);
            var a = w.Place(BuildingType.Mine, 10, 10, 1, 0, free: true);
            var b = w.Place(BuildingType.Mine, 20, 10, 1, 0, free: true);
            w.Accept(a, Item.Wood);
            w.Accept(b, Item.Charcoal);
            w.Tick(12f);
            Assert.AreEqual("No fuel", a.Status);
            Assert.AreEqual("Working", b.Status);
        }

        [Test]
        public void Fuel_slot_is_capped_and_only_takes_fuel()
        {
            var w = Empty();
            Ground(w, 10, 10, 2, Terrain.Stone);
            var m = w.Place(BuildingType.Mine, 10, 10, 1);
            for (int i = 0; i < B.FuelCap; i++) Assert.IsTrue(w.Accept(m, Item.Wood));
            Assert.IsFalse(w.Accept(m, Item.Charcoal));
            Assert.IsFalse(w.Accept(m, Item.Stone));
        }

        [Test]
        public void Kiln_makes_charcoal_from_wood_without_fuel()
        {
            var w = Empty();
            var k = w.Place(BuildingType.Kiln, 10, 10, 1, 0);
            Assert.IsTrue(w.Accept(k, Item.Wood));
            Assert.IsTrue(w.Accept(k, Item.Wood));
            Assert.IsFalse(w.Accept(k, Item.Stone));
            w.Tick(3.1f);
            Assert.AreEqual(1, k.OutCount);
            Assert.AreEqual(Item.Charcoal, k.OutItem);
        }

        [Test]
        public void Brick_kiln_takes_stone_as_recipe_and_wood_as_fuel()
        {
            var w = Empty();
            var k = w.Place(BuildingType.Kiln, 10, 10, 1, 1);
            Assert.IsTrue(w.Accept(k, Item.Stone));
            Assert.IsTrue(w.Accept(k, Item.Stone));
            w.Tick(5f);
            Assert.AreEqual("No fuel", k.Status);
            Assert.IsTrue(w.Accept(k, Item.Wood));
            Assert.AreEqual(1, k.FuelWood);
            w.Tick(4.1f);
            Assert.AreEqual(Item.Brick, k.OutItem);
        }

        [Test]
        public void Smelter_needs_ore_and_charcoal()
        {
            var w = Empty();
            var s = w.Place(BuildingType.Smelter, 10, 10, 1);
            w.Accept(s, Item.Ore); w.Accept(s, Item.Ore);
            w.Tick(4f);
            StringAssert.Contains("charcoal", s.Status);
            Assert.IsTrue(w.Accept(s, Item.Charcoal));
            Assert.IsFalse(w.Accept(s, Item.Wood), "smelters don't burn wood");
            w.Tick(3.1f);
            Assert.AreEqual(Item.Iron, s.OutItem);
        }

        [Test]
        public void Input_buffer_is_capped()
        {
            var w = Empty();
            var k = w.Place(BuildingType.Kiln, 10, 10, 1, 0);
            for (int i = 0; i < 4; i++) Assert.IsTrue(w.Accept(k, Item.Wood));
            Assert.IsFalse(w.Accept(k, Item.Wood));
        }

        [Test]
        public void Changing_recipe_clears_inputs()
        {
            var w = Empty();
            var k = w.Place(BuildingType.Kiln, 10, 10, 1, 0);
            w.Accept(k, Item.Wood);
            w.SetRecipe(k.Id, 1);
            Assert.AreEqual(0, k.Input.Count);
            Assert.IsTrue(w.Accept(k, Item.Stone));
        }

        [Test]
        public void Machine_feeds_lodge_directly_when_touching()
        {
            var w = Empty();
            var k = w.Place(BuildingType.Kiln, 30, 34, 2, 0); // front edge touches lodge top
            w.Accept(k, Item.Wood); w.Accept(k, Item.Wood);
            w.Tick(3.5f);
            Assert.AreEqual(1, w.Delivered[Item.Charcoal]);
        }

        [Test]
        public void Belts_do_not_push_head_on()
        {
            var w = Empty();
            var a = w.Place(BuildingType.Belt, 10, 10, 1);
            var b = w.Place(BuildingType.Belt, 11, 10, 3);
            a.BeltItem = Item.Wood;
            w.Tick(2f);
            Assert.AreEqual(Item.Wood, a.BeltItem);
            Assert.AreEqual(Item.None, b.BeltItem);
        }

        [Test]
        public void Side_loading_merges_belts()
        {
            var w = Empty();
            var side = w.Place(BuildingType.Belt, 10, 11, 2); // points south into the main line
            var main = w.Place(BuildingType.Belt, 10, 10, 1);
            side.BeltItem = Item.Stone;
            w.Tick(0.6f);
            Assert.AreEqual(Item.Stone, main.BeltItem);
        }

        [Test]
        public void Demo_factory_reaches_tier2_goal()
        {
            var w = MapGen.NewTier1(B);
            float t = DemoFactory.RunToGoal(w, 3600f);
            Assert.Greater(t, 0f, "demo never reached the goal");
            Assert.Less(t, 1500f);
        }

        [Test]
        public void Save_round_trip_keeps_buildings_and_storage()
        {
            var w = MapGen.NewTier1(B);
            var demo = new DemoFactory();
            demo.BuildWhatYouCan(w);
            w.Tick(30f);
            var data = SaveData.From(w);
            var w2 = data.ToWorld(B);
            Assert.AreEqual(w.Buildings.Count, w2.Buildings.Count);
            Assert.AreEqual(w.Storage[Item.Wood], w2.Storage[Item.Wood]);
            Assert.AreEqual(w.Delivered[Item.Wood], w2.Delivered[Item.Wood]);
        }
    }
}
