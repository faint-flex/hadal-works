using System;

namespace Kilnwood.Sim
{
    /// <summary>Tier 1 "First Fire": a fixed hand-made valley so it can be balanced. The lodge sits in the middle.</summary>
    public static class MapGen
    {
        public const int LodgeX = 30, LodgeY = 30;
        public const int RiverX0 = 2, RiverX1 = 4;   // water columns along the west edge
        public const int CoalX0 = 54, CoalY0 = 27;   // 7 x 7 coal seam

        /// <summary>Names painted on the ground at each field (tile x, tile y, text).</summary>
        public static readonly (int x, int y, string text)[] Labels =
        {
            (14, 25, "FOREST  (W)"), (48, 29, "IRON ORE  (O)"), (34, 50, "STONE  (S)"),
            (51, 20, "FOREST  (W)"), (51, 44, "FOREST  (W)"), (35, 57, "FOREST  (W)"),
            (54, 7, "IRON ORE  (O)"), (56, 30, "COAL  (K)  deep"), (13, 51, "STONE  (S)"), (12, 10, "FOREST  (W)"),
        };

        public static World NewTier1(Balance b)
        {
            var w = new World(b);
            var rng = new Random(1846);

            // Forests (wood). Woodcutter spots used by the demo sit inside, away from the ragged edges.
            Patch(w, rng, 9, 23, 11, 19, Terrain.Forest);    // big west wood
            Patch(w, rng, 43, 39, 13, 9, Terrain.Forest);    // north of the ore
            Patch(w, rng, 43, 17, 13, 9, Terrain.Forest);    // south of the ore
            Patch(w, rng, 22, 52, 18, 9, Terrain.Forest);    // north of the stone
            Patch(w, rng, 8, 6, 9, 8, Terrain.Forest);       // far grove
            // Iron ore and stone.
            Patch(w, rng, 43, 27, 9, 12, Terrain.IronOre);
            Patch(w, rng, 26, 45, 10, 7, Terrain.Stone);
            Patch(w, rng, 51, 4, 7, 7, Terrain.IronOre);     // far
            Patch(w, rng, 10, 48, 7, 6, Terrain.Stone);      // far

            // The river down the west edge, wobbling a little.
            for (int y = 0; y < w.H; y++)
            {
                int wob = (int)Math.Round(Math.Sin(y * 0.18) * 1.2);
                for (int x = RiverX0 + wob; x <= RiverX1 + wob; x++) w.SetGround(Math.Max(0, x), y, Terrain.Water);
            }

            // Coal seam (Tier 3): under the open grass east of the ore field, two levels down.
            for (int y = CoalY0; y < CoalY0 + 7; y++)
                for (int x = CoalX0; x < CoalX0 + 7; x++)
                    if (w.InBounds(x, y)) w.CoalSeam[y * w.W + x] = true;

            w.Place(BuildingType.Lodge, LodgeX, LodgeY, 0, 0, free: true);
            return w;
        }

        /// <summary>A rectangle of ground with ragged edges (edge tiles dropped at random).</summary>
        static void Patch(World w, Random rng, int x0, int y0, int wd, int ht, Terrain t)
        {
            for (int y = y0; y < y0 + ht; y++)
                for (int x = x0; x < x0 + wd; x++)
                {
                    bool edge = x == x0 || y == y0 || x == x0 + wd - 1 || y == y0 + ht - 1;
                    if (edge && rng.NextDouble() < 0.35) continue;
                    w.SetGround(x, y, t);
                }
        }
    }
}
