using HadalWorks.Sim;
using NUnit.Framework;

namespace HadalWorks.Tests
{
    public class WorldTests
    {
        Balance B;

        [SetUp]
        public void SetUp() { B = new Balance(); }

        World Empty()
        {
            var w = new World(B);
            w.Place(BuildingType.Hub, 30, 30, 0, 0, free: true);
            return w;
        }

        [Test]
        public void Tier1_map_has_hub_resources_and_vents()
        {
            var w = MapGen.NewTier1(B);
            Assert.IsNotNull(w.Hub);
            int f = 0, s = 0, k = 0, v = 0;
            foreach (var t in w.Ground)
            {
                if (t == Terrain.Ferrite) f++;
                if (t == Terrain.Silica) s++;
                if (t == Terrain.Kelp) k++;
                if (t == Terrain.Vent) v++;
            }
            Assert.Greater(f, 50); Assert.Greater(s, 50); Assert.Greater(k, 50);
            Assert.AreEqual(8, v);
            Assert.AreEqual(B.StartPlates, w.Storage[Item.Plate]);
        }

        [Test]
        public void Placing_costs_plates_and_demolish_refunds()
        {
            var w = Empty();
            var t = w.Place(BuildingType.Tube, 10, 10, Dir.Opposite(0));
            Assert.IsNotNull(t);
            Assert.AreEqual(B.StartPlates - 1, w.Storage[Item.Plate]);
            Assert.IsTrue(w.Demolish(t.Id));
            Assert.AreEqual(B.StartPlates, w.Storage[Item.Plate]);
            Assert.IsNull(w.At(10, 10));
        }

        [Test]
        public void Cannot_overlap_or_leave_map()
        {
            var w = Empty();
            Assert.IsNotNull(w.CanPlace(BuildingType.Tube, 31, 31));
            Assert.IsNotNull(w.CanPlace(BuildingType.Processor, 63, 63));
            Assert.IsNull(w.CanPlace(BuildingType.Processor, 10, 10));
        }

        [Test]
        public void Extractor_needs_resource_and_vent_tap_needs_vent()
        {
            var w = Empty();
            Assert.IsNotNull(w.CanPlace(BuildingType.Extractor, 5, 5));
            w.SetGround(5, 5, Terrain.Ferrite);
            Assert.IsNull(w.CanPlace(BuildingType.Extractor, 5, 5));
            Assert.IsNotNull(w.CanPlace(BuildingType.VentTap, 8, 8));
            w.SetGround(8, 8, Terrain.Vent);
            Assert.IsNull(w.CanPlace(BuildingType.VentTap, 8, 8));
            Assert.IsNotNull(w.CanPlace(BuildingType.Tube, 8, 8));
        }

        [Test]
        public void Cannot_afford_shows_reason()
        {
            var w = Empty();
            w.Storage[Item.Plate] = 3;
            StringAssert.Contains("plates", w.CanPlace(BuildingType.Processor, 10, 10));
        }

        [Test]
        public void Extractor_to_tube_to_hub_delivers()
        {
            var w = Empty();
            for (int x = 20; x < 22; x++) for (int y = 30; y < 32; y++) w.SetGround(x, y, Terrain.Ferrite);
            w.Place(BuildingType.Extractor, 20, 30, 1);
            for (int x = 22; x < 30; x++) w.Place(BuildingType.Tube, x, 30, 1);
            w.Tick(20f);
            Assert.Greater(w.Delivered[Item.Ferrite], 10);
        }

        [Test]
        public void Processor_turns_two_ferrite_into_a_plate()
        {
            var w = Empty();
            var p = w.Place(BuildingType.Processor, 10, 10, 1, 0);
            Assert.IsTrue(w.Accept(p, Item.Ferrite));
            Assert.IsTrue(w.Accept(p, Item.Ferrite));
            Assert.IsFalse(w.Accept(p, Item.Silica));
            w.Tick(3.1f);
            Assert.AreEqual(1, p.OutCount);
            Assert.AreEqual(Item.Plate, p.OutItem);
        }

        [Test]
        public void Input_buffer_is_capped()
        {
            var w = Empty();
            var p = w.Place(BuildingType.Processor, 10, 10, 1, 0);
            for (int i = 0; i < 4; i++) Assert.IsTrue(w.Accept(p, Item.Ferrite));
            Assert.IsFalse(w.Accept(p, Item.Ferrite));
        }

        [Test]
        public void Changing_recipe_clears_inputs()
        {
            var w = Empty();
            var p = w.Place(BuildingType.Processor, 10, 10, 1, 0);
            w.Accept(p, Item.Ferrite);
            w.SetRecipe(p.Id, 1);
            Assert.AreEqual(0, p.Input.Count);
            Assert.IsTrue(w.Accept(p, Item.Silica));
        }

        [Test]
        public void Machine_feeds_hub_directly_when_touching()
        {
            var w = Empty();
            var f = w.Place(BuildingType.Fabricator, 30, 34, 2, 1); // front edge touches hub top
            for (int i = 0; i < 3; i++) w.Accept(f, Item.Glass);
            w.Tick(6.5f);
            Assert.AreEqual(1, w.Delivered[Item.Viewport]);
        }

        [Test]
        public void Tubes_do_not_push_head_on()
        {
            var w = Empty();
            var a = w.Place(BuildingType.Tube, 10, 10, 1);
            var b = w.Place(BuildingType.Tube, 11, 10, 3);
            a.TubeItem = Item.Ferrite;
            w.Tick(2f);
            Assert.AreEqual(Item.Ferrite, a.TubeItem);
            Assert.AreEqual(Item.None, b.TubeItem);
        }

        [Test]
        public void Side_loading_merges_tubes()
        {
            var w = Empty();
            var side = w.Place(BuildingType.Tube, 10, 11, 2); // points south into the main line
            var main = w.Place(BuildingType.Tube, 10, 10, 1);
            side.TubeItem = Item.Kelp;
            w.Tick(0.6f);
            Assert.AreEqual(Item.Kelp, main.TubeItem);
        }

        [Test]
        public void Low_power_slows_machines()
        {
            var w = Empty();
            for (int i = 0; i < 3; i++) w.Place(BuildingType.Processor, 2 + i * 3, 2, 1, 0, free: true);
            w.Tick(0.1f);
            Assert.AreEqual(6f, w.PowerSupply);
            Assert.AreEqual(12f, w.PowerDemand);
            Assert.AreEqual(0.5f, w.PowerRatio, 1e-4f);
        }

        [Test]
        public void Demo_factory_reaches_tier2_goal()
        {
            var w = MapGen.NewTier1(B);
            float t = DemoFactory.RunToGoal(w, 3600f);
            Assert.Greater(t, 0f, "demo never reached the goal");
            Assert.Less(t, 2400f);
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
            Assert.AreEqual(w.Storage[Item.Plate], w2.Storage[Item.Plate]);
            Assert.AreEqual(w.Delivered[Item.Plate], w2.Delivered[Item.Plate]);
        }
    }
}
