using Kilnwood.Sim;
using NUnit.Framework;

namespace Kilnwood.Tests
{
    /// <summary>0.22 Bulldozers: the Level mark, clearing trees, shaving heaps, filling holes.</summary>
    public class DozerTests
    {
        Balance B;

        [SetUp]
        public void SetUp() { B = new Balance(); }

        World Empty()
        {
            var w = new World(B);
            w.Place(BuildingType.Lodge, 30, 30, 0, 0, free: true);
            w.Storage[Item.Iron] = 200; w.Storage[Item.Wood] = 200; w.Storage[Item.Stone] = 200; w.Storage[Item.Steel] = 200;
            return w;
        }

        Vehicle Dozer(World w)
        {
            var g = w.Place(BuildingType.Garage, 36, 30, 0);
            Assert.IsNotNull(g);
            g.FuelCharcoal = 40;
            Assert.IsNull(w.CanBuy(g, VehicleType.Dozer));
            var d = w.Buy(g.Id, VehicleType.Dozer);
            Assert.IsNotNull(d);
            return d;
        }

        static void Run(World w, float seconds) { for (float t = 0f; t < seconds; t += 0.5f) w.Tick(0.5f); }

        [Test]
        public void Level_mark_needs_uneven_ground_or_trees()
        {
            var w = Empty();
            Assert.AreEqual("Already level", w.CanMarkLevel(20, 20));
            w.Units[20 * w.W + 20] = 5;
            Assert.IsNull(w.CanMarkLevel(20, 20));
            w.SetGround(21, 20, Terrain.Forest);
            Assert.IsNull(w.CanMarkLevel(21, 20));
            Assert.AreEqual(2, w.MarkLevel(19, 20, 22, 20));
            Assert.AreEqual("Marked for digging, dumping or levelling", w.CanPlace(BuildingType.Belt, 21, 20));
        }

        [Test]
        public void Dozer_clears_marked_trees_and_sends_logs_home()
        {
            var w = Empty();
            var d = Dozer(w);
            int wood = w.Storage[Item.Wood];
            for (int x = 40; x < 42; x++) w.SetGround(x, 30, Terrain.Forest);
            w.MarkLevel(40, 30, 41, 30);
            Run(w, 60f);
            Assert.AreEqual(Terrain.Grass, w.GroundAt(40, 30));
            Assert.AreEqual(Terrain.Grass, w.GroundAt(41, 30));
            Assert.AreEqual(0, w.CountLevelMarks());
            Assert.AreEqual(2, w.TreesCleared);
            Assert.Greater(w.Storage[Item.Wood], wood);
            Assert.AreEqual(VehicleState.Waiting, d.State);
        }

        [Test]
        public void Dozer_pushes_a_heap_into_a_hole_until_both_are_level()
        {
            var w = Empty();
            Dozer(w);
            int heap = 34 * w.W + 40, hole = 34 * w.W + 43;
            w.Units[heap] = 6;
            w.Units[hole] = -6;
            w.MarkLevel(40, 34, 43, 34);
            Run(w, 120f);
            Assert.AreEqual(0, w.Units[heap]);
            Assert.AreEqual(0, w.Units[hole]);
            Assert.AreEqual(0, w.CountLevelMarks());
            Assert.IsNull(w.CanPlace(BuildingType.Belt, 43, 34));
        }

        [Test]
        public void Holes_are_filled_from_the_nearest_spoil_heap()
        {
            var w = Empty();
            Dozer(w);
            int spoil = 36 * w.W + 44, hole = 36 * w.W + 40;
            w.Units[spoil] = 12;
            w.Units[hole] = -4;
            w.MarkLevel(40, 36, 40, 36);
            Run(w, 90f);
            Assert.AreEqual(0, w.Units[hole]);
            Assert.AreEqual(8, w.Units[spoil]);   // only what the hole needed
        }

        [Test]
        public void Spare_dirt_goes_to_the_dump()
        {
            var w = Empty();
            Dozer(w);
            int heap = 38 * w.W + 40;
            w.Units[heap] = 5;
            w.MarkLevel(40, 38, 40, 38);
            w.MarkDump(44, 38, 44, 38);
            Run(w, 90f);
            Assert.AreEqual(0, w.Units[heap]);
            Assert.AreEqual(5, w.Units[38 * w.W + 44]);
        }

        [Test]
        public void Cleared_forest_and_level_marks_survive_a_save()
        {
            var w = MapGen.NewTier1(B);
            int forest = -1;
            for (int i = 0; i < w.Ground.Length && forest < 0; i++) if (w.Ground[i] == Terrain.Forest) forest = i;
            int grass = -1;
            for (int i = 0; i < w.Ground.Length && grass < 0; i++) if (w.Ground[i] == Terrain.Grass && w.CanMarkLevel(i % w.W, i / w.W) == "Already level") grass = i;
            w.RestoreCleared(forest);
            w.Units[grass] = 3;
            Assert.AreEqual(1, w.MarkLevel(grass % w.W, grass / w.W, grass % w.W, grass / w.W));
            var back = SaveData.From(w).ToWorld(B);
            Assert.AreEqual(Terrain.Grass, back.Ground[forest]);
            Assert.IsTrue(back.LevelMark[grass]);
        }
    }
}
